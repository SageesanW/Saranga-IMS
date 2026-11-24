'use client';

import React, { useState, useEffect, Suspense } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import { BarcodeDisplay } from '@/components/BarcodeDisplay';
import {
  Branch,
  StockLocation,
  EligibleChildItem,
  InventoryComposition,
  branchApi,
  stockLocationApi,
  compositionApi,
} from '@/lib/api';

function CompositionCreationContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { user } = useAuth();
  const { activeBranchId } = useBranch();

  // Permission check
  const canCreate =
    user?.isGlobalAdmin ||
    user?.permissions.includes('INVENTORY_COMPOSITION_CREATE') ||
    user?.permissions.includes('INVENTORY_CREATE');

  // Stepper state: 1: Branch/Location, 2: Identity, 3: Members, 4: Review
  const [currentStep, setCurrentStep] = useState<number>(1);

  // Master lookups
  const [branches, setBranches] = useState<Branch[]>([]);
  const [locations, setLocations] = useState<StockLocation[]>([]);
  const [isLoadingLookups, setIsLoadingLookups] = useState<boolean>(true);

  // Form State
  const [branchId, setBranchId] = useState<string>('');
  const [stockLocationId, setStockLocationId] = useState<string>('');
  const [type, setType] = useState<number>(() => {
    const queryType = searchParams.get('type');
    return queryType === 'group' ? 2 : 1; // 1 = Assembly, 2 = Group
  });
  const [name, setName] = useState<string>('');
  const [description, setDescription] = useState<string>('');

  // Selected Members State
  const [selectedMembers, setSelectedMembers] = useState<EligibleChildItem[]>([]);

  // Eligible Children Candidate Search & Scan State
  const [candidateChildren, setCandidateChildren] = useState<EligibleChildItem[]>([]);
  const [isLoadingCandidates, setIsLoadingCandidates] = useState<boolean>(false);
  const [searchFilter, setSearchFilter] = useState<string>('');
  const [scanInput, setScanInput] = useState<string>('');

  // Submission & Result States
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [createdComposition, setCreatedComposition] = useState<InventoryComposition | null>(null);

  // Load initial branches
  useEffect(() => {
    async function loadMasters() {
      try {
        setIsLoadingLookups(true);
        const bList = await branchApi.getBranches(true);
        setBranches(bList);

        const initialBranchId = activeBranchId || (bList.length > 0 ? bList[0].id : '');
        setBranchId(initialBranchId);

        if (initialBranchId) {
          const locList = await stockLocationApi.getStockLocations(initialBranchId, true);
          setLocations(locList);
          if (locList.length > 0) {
            setStockLocationId(locList[0].id);
          }
        }
      } catch (err: any) {
        setFormError(err?.message || 'Failed to load branch data');
      } finally {
        setIsLoadingLookups(false);
      }
    }

    if (canCreate) {
      loadMasters();
    }
  }, [canCreate, activeBranchId]);

  // Handle branch change
  const handleBranchChange = async (newBranchId: string) => {
    setBranchId(newBranchId);
    setStockLocationId('');
    setSelectedMembers([]);
    try {
      const locList = await stockLocationApi.getStockLocations(newBranchId, true);
      setLocations(locList);
      if (locList.length > 0) {
        setStockLocationId(locList[0].id);
      }
    } catch {
      setLocations([]);
    }
  };

  // Fetch eligible candidates when in Step 3 or when filters change
  useEffect(() => {
    async function fetchCandidates() {
      if (!branchId || !stockLocationId || currentStep !== 3) return;

      try {
        setIsLoadingCandidates(true);
        // If Assembly and we already have at least 1 member, lock candidate category to the first member's category
        const categoryFilter =
          type === 1 && selectedMembers.length > 0 ? selectedMembers[0].categoryId : undefined;

        const candidates = await compositionApi.getEligibleChildren({
          branchId,
          stockLocationId,
          categoryId: categoryFilter,
          searchTerm: searchFilter.trim() || undefined,
        });

        // Exclude already selected members from candidates
        const selectedIds = new Set(selectedMembers.map((m) => m.id));
        setCandidateChildren(candidates.filter((c) => !selectedIds.has(c.id)));
      } catch (err: any) {
        setFormError(err?.message || 'Failed to fetch candidate items');
      } finally {
        setIsLoadingCandidates(false);
      }
    }

    fetchCandidates();
  }, [branchId, stockLocationId, currentStep, type, selectedMembers, searchFilter]);

  // Live Aggregate Totals
  const totalGrossWeight = selectedMembers.reduce((sum, m) => sum + m.grossWeight, 0);
  const totalStoneWeight = selectedMembers.reduce((sum, m) => sum + m.stoneWeight, 0);
  const totalNetWeight = selectedMembers.reduce((sum, m) => sum + m.netWeight, 0);
  const totalPieceCount = selectedMembers.reduce((sum, m) => sum + m.pieceCount, 0);

  // Add Candidate
  const handleAddCandidate = (item: EligibleChildItem) => {
    setFormError(null);

    // Assembly Category Enforcement
    if (type === 1 && selectedMembers.length > 0) {
      const lockedCategory = selectedMembers[0].categoryName;
      if (item.categoryId !== selectedMembers[0].categoryId) {
        setFormError(
          `Assembly items must all share the same category ('${lockedCategory}'). '${item.name}' belongs to '${item.categoryName}'. To mix categories, create a Group instead.`
        );
        return;
      }
    }

    setSelectedMembers((prev) => [...prev, item]);
    setCandidateChildren((prev) => prev.filter((c) => c.id !== item.id));
  };

  // Remove Member
  const handleRemoveMember = (itemId: string) => {
    const itemToRemove = selectedMembers.find((m) => m.id === itemId);
    if (!itemToRemove) return;

    setSelectedMembers((prev) => prev.filter((m) => m.id !== itemId));
    setCandidateChildren((prev) => [itemToRemove, ...prev]);
  };

  // Handle Quick Barcode/Tag Scan
  const handleScanSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!scanInput.trim()) return;

    const term = scanInput.trim().toUpperCase();
    const match = candidateChildren.find(
      (c) => c.barcode.toUpperCase() === term || c.tagNumber.toUpperCase() === term
    );

    if (match) {
      handleAddCandidate(match);
      setScanInput('');
    } else {
      setFormError(`Item with barcode/tag '${scanInput}' was not found in available eligible stock.`);
    }
  };

  // Validation
  const validateStep = (): boolean => {
    setFormError(null);
    switch (currentStep) {
      case 1:
        if (!branchId) {
          setFormError('Please select a branch.');
          return false;
        }
        if (!stockLocationId) {
          setFormError('Please select a physical stock location.');
          return false;
        }
        return true;

      case 2:
        if (!name.trim()) {
          setFormError('Composition Name is required.');
          return false;
        }
        return true;

      case 3:
        if (selectedMembers.length < 2) {
          setFormError('An Assembly or Group must contain at least 2 distinct inventory items.');
          return false;
        }
        return true;

      default:
        return true;
    }
  };

  const handleNext = () => {
    if (validateStep()) {
      setCurrentStep((prev) => Math.min(4, prev + 1));
    }
  };

  const handlePrev = () => {
    setFormError(null);
    setCurrentStep((prev) => Math.max(1, prev - 1));
  };

  // Final Submit
  const handleSubmit = async () => {
    if (!validateStep()) return;

    setIsSubmitting(true);
    setFormError(null);

    try {
      const payload = {
        branchId,
        stockLocationId,
        name: name.trim(),
        type,
        description: description.trim() || undefined,
        childItemIds: selectedMembers.map((m) => m.id),
      };

      const result = await compositionApi.createComposition(payload);
      setCreatedComposition(result);
    } catch (err: any) {
      setFormError(err?.message || 'Failed to create composition');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResetForNew = () => {
    setCreatedComposition(null);
    setCurrentStep(1);
    setName('');
    setDescription('');
    setSelectedMembers([]);
    setFormError(null);
  };

  const selectedBranch = branches.find((b) => b.id === branchId);
  const selectedLocation = locations.find((l) => l.id === stockLocationId);

  // Authorization Guard
  if (!canCreate) {
    return (
      <AppLayout>
        <div className="max-w-4xl mx-auto py-12 px-4 text-center">
          <div className="bg-red-50 border border-red-200 rounded-xl p-8 shadow-sm">
            <span className="text-4xl">🔒</span>
            <h2 className="text-xl font-bold text-red-800 mt-3">Access Restricted</h2>
            <p className="text-sm text-red-600 mt-2">
              You do not have permission (<code>INVENTORY_COMPOSITION_CREATE</code>) to create assemblies or groups.
            </p>
            <Link
              href="/inventory"
              className="inline-block mt-4 px-4 py-2 bg-[#0D3826] text-white text-xs font-semibold rounded-lg hover:bg-[#1B6543]"
            >
              Return to Inventory
            </Link>
          </div>
        </div>
      </AppLayout>
    );
  }

  // Success Screen
  if (createdComposition) {
    return (
      <AppLayout>
        <div className="max-w-3xl mx-auto py-8 px-4">
          <div className="bg-white rounded-2xl shadow-lg border border-gray-100 p-8 text-center">
            <div className="w-16 h-16 bg-emerald-100 rounded-full flex items-center justify-center mx-auto text-2xl">
              ✓
            </div>
            <h2 className="text-2xl font-bold text-gray-900 mt-4">
              {createdComposition.typeName} Created Successfully!
            </h2>
            <p className="text-sm text-gray-500 mt-1">
              {createdComposition.name} ({createdComposition.activeMemberCount} items linked)
            </p>

            {/* Generated Identifiers */}
            <div className="bg-gradient-to-r from-emerald-50 to-teal-50 border border-emerald-200 rounded-xl p-6 mt-6 max-w-lg mx-auto">
              <div className="grid grid-cols-2 gap-4 text-left">
                <div>
                  <span className="text-xs text-gray-500 font-semibold uppercase">Parent Tag Number</span>
                  <div className="font-mono text-base font-bold text-[#0D3826]">{createdComposition.tagNumber}</div>
                </div>
                <div>
                  <span className="text-xs text-gray-500 font-semibold uppercase">Composition Type</span>
                  <div>
                    <span
                      className={`inline-block px-2.5 py-0.5 text-xs font-bold rounded-full ${
                        createdComposition.type === 1
                          ? 'bg-purple-100 text-purple-800'
                          : 'bg-indigo-100 text-indigo-800'
                      }`}
                    >
                      {createdComposition.typeName}
                    </span>
                  </div>
                </div>
              </div>

              <div className="mt-6 pt-4 border-t border-emerald-200 flex flex-col items-center">
                <span className="text-xs text-gray-500 font-semibold uppercase mb-2">Machine-Scannable Barcode</span>
                <BarcodeDisplay value={createdComposition.barcode} />
              </div>
            </div>

            {/* Aggregated Totals Card */}
            <div className="grid grid-cols-4 gap-3 bg-gray-50 p-4 rounded-xl border border-gray-200 mt-6 max-w-lg mx-auto text-center">
              <div>
                <span className="block text-[11px] text-gray-500 uppercase font-semibold">Gross Wt</span>
                <span className="text-sm font-bold text-gray-800">{createdComposition.totalGrossWeight.toFixed(4)}g</span>
              </div>
              <div>
                <span className="block text-[11px] text-gray-500 uppercase font-semibold">Stone Wt</span>
                <span className="text-sm font-bold text-gray-800">{createdComposition.totalStoneWeight.toFixed(4)}g</span>
              </div>
              <div>
                <span className="block text-[11px] text-gray-500 uppercase font-semibold">Net Wt</span>
                <span className="text-sm font-bold text-emerald-800">{createdComposition.totalNetWeight.toFixed(4)}g</span>
              </div>
              <div>
                <span className="block text-[11px] text-gray-500 uppercase font-semibold">Pieces</span>
                <span className="text-sm font-bold text-gray-800">{createdComposition.totalPieceCount}</span>
              </div>
            </div>

            {/* Actions */}
            <div className="flex justify-center space-x-3 mt-8">
              <button
                onClick={handleResetForNew}
                className="px-5 py-2.5 bg-gray-100 hover:bg-gray-200 text-gray-800 text-sm font-semibold rounded-lg transition"
              >
                Create Another Composition
              </button>
              <Link
                href="/inventory"
                className="px-6 py-2.5 bg-[#0D3826] hover:bg-[#1B6543] text-white text-sm font-semibold rounded-lg shadow transition"
              >
                Go to Inventory List
              </Link>
            </div>
          </div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout>
      <div className="max-w-5xl mx-auto py-6 px-4">
        {/* Navigation Breadcrumbs */}
        <div className="flex items-center space-x-2 text-xs text-gray-500 mb-4">
          <Link href="/inventory" className="hover:text-[#0D3826]">Inventory</Link>
          <span>/</span>
          <span className="text-gray-800 font-semibold">
            New {type === 1 ? 'Assembly' : 'Group'} Composition
          </span>
        </div>

        {/* Header */}
        <div className="flex justify-between items-center mb-6">
          <div>
            <h1 className="text-2xl font-bold text-gray-900">
              Create {type === 1 ? 'Assembly' : 'Group'} Composition
            </h1>
            <p className="text-xs text-gray-500 mt-0.5">
              {type === 1
                ? 'Bundle 2 or more individually tracked items from the exact same category.'
                : 'Bundle 2 or more individually tracked items across different categories.'}
            </p>
          </div>
          <Link
            href="/inventory"
            className="px-4 py-2 border border-gray-300 rounded-lg text-xs font-semibold text-gray-700 hover:bg-gray-50 transition"
          >
            Cancel
          </Link>
        </div>

        {/* Stepper Wizard Indicator */}
        <div className="grid grid-cols-4 gap-2 mb-6">
          {[
            { step: 1, label: 'Branch & Location' },
            { step: 2, label: 'Identity & Type' },
            { step: 3, label: 'Select Members' },
            { step: 4, label: 'Review & Create' },
          ].map((s) => (
            <div
              key={s.step}
              className={`p-3 rounded-xl border text-center transition ${
                currentStep === s.step
                  ? 'bg-[#0D3826] text-white border-[#0D3826] shadow-sm'
                  : currentStep > s.step
                  ? 'bg-emerald-50 text-emerald-800 border-emerald-300 font-medium'
                  : 'bg-white text-gray-400 border-gray-200'
              }`}
            >
              <div className="text-[10px] uppercase font-bold tracking-wider">Step {s.step}</div>
              <div className="text-xs font-semibold truncate mt-0.5">{s.label}</div>
            </div>
          ))}
        </div>

        {/* Error Banner */}
        {formError && (
          <div className="mb-6 p-4 bg-red-50 border-l-4 border-red-500 text-red-700 text-xs rounded-r-lg flex justify-between items-center">
            <span>{formError}</span>
            <button onClick={() => setFormError(null)} className="text-red-500 font-bold ml-2">✕</button>
          </div>
        )}

        {/* Step Contents */}
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6 mb-6">
          {/* STEP 1: Branch & Location */}
          {currentStep === 1 && (
            <div className="space-y-6">
              <h3 className="text-base font-bold text-gray-900 border-b pb-2">
                Step 1: Operational Branch & Stock Location
              </h3>
              <p className="text-xs text-gray-500">
                All physical items bundled into this composition must belong to the selected branch and physical display/stock location.
              </p>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">
                    Branch <span className="text-red-500">*</span>
                  </label>
                  <select
                    value={branchId}
                    onChange={(e) => handleBranchChange(e.target.value)}
                    className="w-full px-3 py-2 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                  >
                    {branches.map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.name} ({b.code}) - {b.country}
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">
                    Physical Stock / Display Location <span className="text-red-500">*</span>
                  </label>
                  <select
                    value={stockLocationId}
                    onChange={(e) => {
                      setStockLocationId(e.target.value);
                      setSelectedMembers([]);
                    }}
                    className="w-full px-3 py-2 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                  >
                    {locations.map((l) => (
                      <option key={l.id} value={l.id}>
                        {l.name} ({l.code}) - {l.locationType}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            </div>
          )}

          {/* STEP 2: Identity & Type */}
          {currentStep === 2 && (
            <div className="space-y-6">
              <h3 className="text-base font-bold text-gray-900 border-b pb-2">
                Step 2: Composition Type & Details
              </h3>

              {/* Type Switcher */}
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-2">
                  Composition Type <span className="text-red-500">*</span>
                </label>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <button
                    type="button"
                    onClick={() => {
                      setType(1);
                      setSelectedMembers([]);
                    }}
                    className={`p-4 rounded-xl border text-left transition ${
                      type === 1
                        ? 'border-purple-600 bg-purple-50/50 ring-2 ring-purple-600/20'
                        : 'border-gray-200 hover:border-gray-300 bg-white'
                    }`}
                  >
                    <div className="flex items-center space-x-2">
                      <span className="text-purple-700 font-bold text-sm">ASSEMBLY</span>
                      <span className="text-[10px] bg-purple-100 text-purple-800 px-2 py-0.5 rounded-full font-semibold">
                        Same Category
                      </span>
                    </div>
                    <p className="text-xs text-gray-600 mt-2">
                      2 or more items from the <strong>exact same category</strong>. E.g. Sovereign Bangle Pair, Stud Earring Pair.
                    </p>
                  </button>

                  <button
                    type="button"
                    onClick={() => {
                      setType(2);
                      setSelectedMembers([]);
                    }}
                    className={`p-4 rounded-xl border text-left transition ${
                      type === 2
                        ? 'border-indigo-600 bg-indigo-50/50 ring-2 ring-indigo-600/20'
                        : 'border-gray-200 hover:border-gray-300 bg-white'
                    }`}
                  >
                    <div className="flex items-center space-x-2">
                      <span className="text-indigo-700 font-bold text-sm">GROUP</span>
                      <span className="text-[10px] bg-indigo-100 text-indigo-800 px-2 py-0.5 rounded-full font-semibold">
                        Mixed Categories
                      </span>
                    </div>
                    <p className="text-xs text-gray-600 mt-2">
                      2 or more items that <strong>may span different categories</strong>. E.g. Bridal Jewellery Set (Necklace + Earrings + Ring).
                    </p>
                  </button>
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">
                  Composition Name <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  placeholder="e.g. Royal Sovereign 22K Bangle Pair (Set of 2)"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full px-3 py-2 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Description / Notes</label>
                <textarea
                  rows={3}
                  placeholder="Optional details regarding composition pairing, display instructions, or packaging..."
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  className="w-full px-3 py-2 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                />
              </div>
            </div>
          )}

          {/* STEP 3: Select Members */}
          {currentStep === 3 && (
            <div className="space-y-6">
              <div className="flex justify-between items-center border-b pb-2">
                <h3 className="text-base font-bold text-gray-900">
                  Step 3: Select & Scan Child Inventory Items
                </h3>
                {type === 1 && selectedMembers.length > 0 && (
                  <span className="text-xs bg-purple-100 text-purple-800 font-semibold px-2.5 py-1 rounded-full">
                    Category Locked: {selectedMembers[0].categoryName}
                  </span>
                )}
              </div>

              {/* Fast Barcode/Tag Scanner Bar */}
              <div className="bg-gradient-to-r from-[#0D3826] to-[#154c34] p-4 rounded-xl text-white">
                <div className="text-xs font-bold text-[#D4AF37] uppercase tracking-wider mb-1">
                  Scan Physical Barcode or Tag Number
                </div>
                <form onSubmit={handleScanSubmit} className="flex space-x-2 mt-2">
                  <input
                    type="text"
                    placeholder="Scan or enter Barcode / TagNumber..."
                    value={scanInput}
                    onChange={(e) => setScanInput(e.target.value)}
                    className="flex-1 px-3 py-2 text-xs text-gray-900 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#D4AF37]"
                  />
                  <button
                    type="submit"
                    className="px-4 py-2 bg-[#D4AF37] hover:bg-[#b89528] text-white text-xs font-bold rounded-lg shadow transition"
                  >
                    Add Scanned Item
                  </button>
                </form>
              </div>

              {/* Selected Members Section */}
              <div>
                <div className="flex justify-between items-center mb-2">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-gray-700">
                    Selected Members ({selectedMembers.length})
                  </h4>
                  {selectedMembers.length < 2 && (
                    <span className="text-[11px] text-amber-700 font-semibold bg-amber-50 px-2 py-0.5 rounded">
                      ⚠️ Minimum 2 items required
                    </span>
                  )}
                </div>

                {selectedMembers.length === 0 ? (
                  <div className="p-6 border-2 border-dashed border-gray-200 rounded-xl text-center text-xs text-gray-400">
                    No child items selected yet. Choose from candidate items below or scan a barcode/tag.
                  </div>
                ) : (
                  <div className="overflow-x-auto border border-gray-200 rounded-xl">
                    <table className="min-w-full divide-y divide-gray-200 text-xs">
                      <thead className="bg-gray-50 text-gray-600 font-semibold">
                        <tr>
                          <th className="px-3 py-2 text-left">Item Name</th>
                          <th className="px-3 py-2 text-left">Tag / Barcode</th>
                          <th className="px-3 py-2 text-left">Category</th>
                          <th className="px-3 py-2 text-right">Gross Wt</th>
                          <th className="px-3 py-2 text-right">Net Wt</th>
                          <th className="px-3 py-2 text-right">Pieces</th>
                          <th className="px-3 py-2 text-center">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 bg-white">
                        {selectedMembers.map((m) => (
                          <tr key={m.id} className="hover:bg-gray-50">
                            <td className="px-3 py-2 font-medium text-gray-900">{m.name}</td>
                            <td className="px-3 py-2 font-mono text-gray-500">
                              <div>{m.tagNumber}</div>
                              <div className="text-[10px] text-gray-400">{m.barcode}</div>
                            </td>
                            <td className="px-3 py-2 text-gray-600">{m.categoryName}</td>
                            <td className="px-3 py-2 text-right font-medium">{m.grossWeight.toFixed(4)}g</td>
                            <td className="px-3 py-2 text-right font-semibold text-emerald-700">{m.netWeight.toFixed(4)}g</td>
                            <td className="px-3 py-2 text-right">{m.pieceCount}</td>
                            <td className="px-3 py-2 text-center">
                              <button
                                type="button"
                                onClick={() => handleRemoveMember(m.id)}
                                className="text-red-500 hover:text-red-700 font-bold px-2 py-1 rounded hover:bg-red-50 transition"
                              >
                                Remove
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              {/* Live Aggregated Totals Card */}
              {selectedMembers.length > 0 && (
                <div className="grid grid-cols-4 gap-3 bg-gradient-to-r from-emerald-50 to-teal-50 p-4 rounded-xl border border-emerald-200 text-center">
                  <div>
                    <span className="block text-[11px] text-gray-500 uppercase font-semibold">Total Gross</span>
                    <span className="text-sm font-bold text-gray-900">{totalGrossWeight.toFixed(4)}g</span>
                  </div>
                  <div>
                    <span className="block text-[11px] text-gray-500 uppercase font-semibold">Total Stone</span>
                    <span className="text-sm font-bold text-gray-700">{totalStoneWeight.toFixed(4)}g</span>
                  </div>
                  <div>
                    <span className="block text-[11px] text-gray-500 uppercase font-semibold">Total Net</span>
                    <span className="text-sm font-bold text-emerald-800">{totalNetWeight.toFixed(4)}g</span>
                  </div>
                  <div>
                    <span className="block text-[11px] text-gray-500 uppercase font-semibold">Total Pieces</span>
                    <span className="text-sm font-bold text-gray-900">{totalPieceCount}</span>
                  </div>
                </div>
              )}

              {/* Eligible Candidates Browser */}
              <div className="pt-4 border-t">
                <div className="flex justify-between items-center mb-3">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-gray-700">
                    Available Candidate Stock in Location ({candidateChildren.length})
                  </h4>
                  <input
                    type="text"
                    placeholder="Filter by name, tag, or barcode..."
                    value={searchFilter}
                    onChange={(e) => setSearchFilter(e.target.value)}
                    className="w-64 px-3 py-1.5 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                  />
                </div>

                {isLoadingCandidates ? (
                  <div className="text-center py-6 text-xs text-gray-400">Loading candidates...</div>
                ) : candidateChildren.length === 0 ? (
                  <div className="p-4 bg-gray-50 rounded-xl text-center text-xs text-gray-500">
                    No eligible available items found matching branch & location filters.
                  </div>
                ) : (
                  <div className="max-h-60 overflow-y-auto border border-gray-200 rounded-xl">
                    <table className="min-w-full divide-y divide-gray-200 text-xs">
                      <thead className="bg-gray-50 text-gray-600 font-semibold sticky top-0">
                        <tr>
                          <th className="px-3 py-2 text-left">Item Name</th>
                          <th className="px-3 py-2 text-left">Tag</th>
                          <th className="px-3 py-2 text-left">Category</th>
                          <th className="px-3 py-2 text-right">Net Wt</th>
                          <th className="px-3 py-2 text-center">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 bg-white">
                        {candidateChildren.map((item) => (
                          <tr key={item.id} className="hover:bg-gray-50">
                            <td className="px-3 py-2 font-medium text-gray-900">{item.name}</td>
                            <td className="px-3 py-2 font-mono text-gray-500">{item.tagNumber}</td>
                            <td className="px-3 py-2 text-gray-600">{item.categoryName}</td>
                            <td className="px-3 py-2 text-right font-medium">{item.netWeight.toFixed(4)}g</td>
                            <td className="px-3 py-2 text-center">
                              <button
                                type="button"
                                onClick={() => handleAddCandidate(item)}
                                className="px-2.5 py-1 bg-[#0D3826] hover:bg-[#1B6543] text-white text-xs font-semibold rounded shadow-sm transition"
                              >
                                + Add
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* STEP 4: Review & Finalize */}
          {currentStep === 4 && (
            <div className="space-y-6">
              <h3 className="text-base font-bold text-gray-900 border-b pb-2">
                Step 4: Review & Finalize Composition
              </h3>

              <div className="grid grid-cols-2 md:grid-cols-4 gap-4 bg-gray-50 p-4 rounded-xl border">
                <div>
                  <span className="text-[11px] text-gray-500 uppercase font-semibold">Composition Name</span>
                  <div className="font-bold text-sm text-gray-900">{name}</div>
                </div>
                <div>
                  <span className="text-[11px] text-gray-500 uppercase font-semibold">Type</span>
                  <div>
                    <span
                      className={`inline-block px-2 py-0.5 text-xs font-bold rounded-full ${
                        type === 1 ? 'bg-purple-100 text-purple-800' : 'bg-indigo-100 text-indigo-800'
                      }`}
                    >
                      {type === 1 ? 'ASSEMBLY' : 'GROUP'}
                    </span>
                  </div>
                </div>
                <div>
                  <span className="text-[11px] text-gray-500 uppercase font-semibold">Branch</span>
                  <div className="font-medium text-xs text-gray-800">{selectedBranch?.name} ({selectedBranch?.code})</div>
                </div>
                <div>
                  <span className="text-[11px] text-gray-500 uppercase font-semibold">Stock Location</span>
                  <div className="font-medium text-xs text-gray-800">{selectedLocation?.name}</div>
                </div>
              </div>

              {/* Totals Summary */}
              <div className="grid grid-cols-4 gap-3 bg-gradient-to-r from-emerald-50 to-teal-50 p-4 rounded-xl border border-emerald-200 text-center">
                <div>
                  <span className="block text-[11px] text-gray-500 uppercase font-semibold">Total Gross Weight</span>
                  <span className="text-base font-bold text-gray-900">{totalGrossWeight.toFixed(4)}g</span>
                </div>
                <div>
                  <span className="block text-[11px] text-gray-500 uppercase font-semibold">Total Stone Weight</span>
                  <span className="text-base font-bold text-gray-700">{totalStoneWeight.toFixed(4)}g</span>
                </div>
                <div>
                  <span className="block text-[11px] text-gray-500 uppercase font-semibold">Total Net Weight</span>
                  <span className="text-base font-bold text-emerald-800">{totalNetWeight.toFixed(4)}g</span>
                </div>
                <div>
                  <span className="block text-[11px] text-gray-500 uppercase font-semibold">Total Pieces</span>
                  <span className="text-base font-bold text-gray-900">{totalPieceCount}</span>
                </div>
              </div>

              {/* Members List */}
              <div>
                <h4 className="text-xs font-bold uppercase tracking-wider text-gray-700 mb-2">
                  Linked Child Items ({selectedMembers.length})
                </h4>
                <div className="border border-gray-200 rounded-xl overflow-hidden">
                  <table className="min-w-full divide-y divide-gray-200 text-xs">
                    <thead className="bg-gray-50 text-gray-600 font-semibold">
                      <tr>
                        <th className="px-3 py-2 text-left">Item Name</th>
                        <th className="px-3 py-2 text-left">Tag Number</th>
                        <th className="px-3 py-2 text-left">Category</th>
                        <th className="px-3 py-2 text-right">Gross Wt</th>
                        <th className="px-3 py-2 text-right">Net Wt</th>
                        <th className="px-3 py-2 text-right">Piece Count</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100 bg-white">
                      {selectedMembers.map((m) => (
                        <tr key={m.id}>
                          <td className="px-3 py-2 font-medium text-gray-900">{m.name}</td>
                          <td className="px-3 py-2 font-mono text-gray-500">{m.tagNumber}</td>
                          <td className="px-3 py-2 text-gray-600">{m.categoryName}</td>
                          <td className="px-3 py-2 text-right font-medium">{m.grossWeight.toFixed(4)}g</td>
                          <td className="px-3 py-2 text-right font-semibold text-emerald-700">{m.netWeight.toFixed(4)}g</td>
                          <td className="px-3 py-2 text-right">{m.pieceCount}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Automated Tag & Barcode Notification */}
              <div className="bg-emerald-50 border border-emerald-200 p-3.5 rounded-xl flex items-start space-x-3 text-xs">
                <span className="text-base">🔒</span>
                <div>
                  <h4 className="font-bold text-emerald-900">Automatic Namespace-Separated Identifiers</h4>
                  <p className="text-emerald-700 mt-0.5">
                    Parent Tag Number and Barcode are generated automatically by the backend with dedicated collision-free namespaces ({type === 1 ? 'ASM namespace for Assembly' : 'GRP namespace for Group'}). Manual override is disabled to protect historical identity integrity.
                  </p>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Wizard Controls */}
        <div className="flex justify-between items-center">
          {currentStep > 1 ? (
            <button
              type="button"
              onClick={handlePrev}
              disabled={isSubmitting}
              className="px-4 py-2 border border-gray-300 rounded-lg text-xs font-semibold text-gray-700 hover:bg-gray-50 transition"
            >
              ← Back
            </button>
          ) : (
            <div />
          )}

          {currentStep < 4 ? (
            <button
              type="button"
              onClick={handleNext}
              className="px-6 py-2 bg-[#0D3826] hover:bg-[#1B6543] text-white text-xs font-bold rounded-lg shadow transition"
            >
              Continue →
            </button>
          ) : (
            <button
              type="button"
              onClick={handleSubmit}
              disabled={isSubmitting}
              className={`px-8 py-2.5 text-white text-xs font-bold rounded-lg shadow-md transition ${
                isSubmitting ? 'bg-gray-400 cursor-not-allowed' : 'bg-emerald-700 hover:bg-emerald-800'
              }`}
            >
              {isSubmitting ? 'Creating Composition...' : '✓ Confirm & Create Composition'}
            </button>
          )}
        </div>
      </div>
    </AppLayout>
  );
}

export default function CompositionCreationPage() {
  return (
    <Suspense fallback={<div className="p-8 text-center text-xs text-gray-400">Loading wizard...</div>}>
      <CompositionCreationContent />
    </Suspense>
  );
}
