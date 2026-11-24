'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import { BarcodeDisplay } from '@/components/BarcodeDisplay';
import {
  InventoryItem,
  InventoryLookup,
  Material,
  Category,
  Purity,
  Specification,
  StockLocation,
  InventoryComposition,
  inventoryApi,
  compositionApi,
  materialApi,
  categoryApi,
  purityApi,
  specificationApi,
  stockLocationApi,
  PagedResult,
} from '@/lib/api';

export default function InventoryPage() {
  const { user } = useAuth();
  const { branches, activeBranchId } = useBranch();

  // Permissions
  const canCreate = user?.isGlobalAdmin || user?.permissions.includes('INVENTORY_CREATE') || user?.permissions.includes('INVENTORY_COMPOSITION_CREATE');
  const canEdit = user?.isGlobalAdmin || user?.permissions.includes('INVENTORY_EDIT');
  const canDissolve = user?.isGlobalAdmin || user?.permissions.includes('INVENTORY_COMPOSITION_DISSOLVE');

  // Master Reference Data
  const [materials, setMaterials] = useState<Material[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [purities, setPurities] = useState<Purity[]>([]);
  const [specifications, setSpecifications] = useState<Specification[]>([]);
  const [branchLocations, setBranchLocations] = useState<StockLocation[]>([]);

  // Inventory Data & Pagination State
  const [inventoryResult, setInventoryResult] = useState<PagedResult<InventoryItem>>({
    items: [],
    totalCount: 0,
    page: 1,
    pageSize: 20,
    totalPages: 0,
    hasNextPage: false,
    hasPreviousPage: false,
  });

  // Query & Filter States
  const [search, setSearch] = useState('');
  const [selectedBranchId, setSelectedBranchId] = useState<string>('');
  const [selectedLocationId, setSelectedLocationId] = useState<string>('');
  const [selectedMaterialId, setSelectedMaterialId] = useState<string>('');
  const [selectedCategoryId, setSelectedCategoryId] = useState<string>('');
  const [selectedPurityId, setSelectedPurityId] = useState<string>('');
  const [selectedStatus, setSelectedStatus] = useState<string>('');
  const [showInactive, setShowInactive] = useState(false);
  const [currentPage, setCurrentPage] = useState(1);

  // UI States
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Quick Barcode / Tag Scanner State
  const [scanInput, setScanInput] = useState('');
  const [scannedResult, setScannedResult] = useState<InventoryLookup | null>(null);
  const [isScanModalOpen, setIsScanModalOpen] = useState(false);

  // Item Details Modal State
  const [selectedItem, setSelectedItem] = useState<InventoryItem | null>(null);
  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  // Create / Edit Modal State
  const [isFormModalOpen, setIsFormModalOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<InventoryItem | null>(null);

  // Module 6: View Mode & Compositions State
  const [viewMode, setViewMode] = useState<'single' | 'compositions'>('single');
  const [compositions, setCompositions] = useState<InventoryComposition[]>([]);
  const [isLoadingCompositions, setIsLoadingCompositions] = useState(false);
  const [selectedComposition, setSelectedComposition] = useState<InventoryComposition | null>(null);
  const [isCompositionModalOpen, setIsCompositionModalOpen] = useState(false);
  const [isAddMenuOpen, setIsAddMenuOpen] = useState(false);
  const [isDissolving, setIsDissolving] = useState(false);

  const fetchCompositions = async () => {
    try {
      setIsLoadingCompositions(true);
      setError(null);
      const res = await compositionApi.getCompositions({
        branchId: selectedBranchId || undefined,
        stockLocationId: selectedLocationId || undefined,
        searchTerm: search.trim() || undefined,
        page: 1,
        pageSize: 50,
      });
      setCompositions(res.items);
    } catch (err: any) {
      setError(err?.message || 'Failed to fetch compositions');
    } finally {
      setIsLoadingCompositions(false);
    }
  };

  useEffect(() => {
    if (viewMode === 'compositions') {
      fetchCompositions();
    }
  }, [viewMode, selectedBranchId, selectedLocationId, search]);

  const handleDissolveComposition = async (compositionId: string) => {
    if (!confirm('Are you sure you want to dissolve this composition? All child items will remain intact and available in stock.')) {
      return;
    }
    setIsDissolving(true);
    try {
      const updated = await compositionApi.dissolveComposition(compositionId);
      setSelectedComposition(updated);
      setSuccess(`Composition '${updated.tagNumber}' dissolved successfully.`);
      fetchCompositions();
    } catch (err: any) {
      setError(err?.message || 'Failed to dissolve composition');
    } finally {
      setIsDissolving(false);
    }
  };

  // Form Fields
  const [formBranchId, setFormBranchId] = useState('');
  const [formLocationId, setFormLocationId] = useState('');
  const [formName, setFormName] = useState('');
  const [formItemCode, setFormItemCode] = useState('');
  const [formTagNumber, setFormTagNumber] = useState('');
  const [formBarcode, setFormBarcode] = useState('');
  const [formMaterialId, setFormMaterialId] = useState('');
  const [formCategoryId, setFormCategoryId] = useState('');
  const [formPurityId, setFormPurityId] = useState('');
  const [formGrossWeight, setFormGrossWeight] = useState('10.0000');
  const [formStoneWeight, setFormStoneWeight] = useState('0.0000');
  const [formPieceCount, setFormPieceCount] = useState('1');
  const [formMakingChargeMethod, setFormMakingChargeMethod] = useState('1'); // 1 = PerGram, 2 = PerItem
  const [formMakingChargeValue, setFormMakingChargeValue] = useState('0.00');
  const [formDescription, setFormDescription] = useState('');
  const [formIsActive, setFormIsActive] = useState(true);
  const [formSelectedSpecs, setFormSelectedSpecs] = useState<{ specificationId: string; specificationOptionId?: string; customValue?: string }[]>([]);

  // Computed net weight
  const computedNetWeight = (
    Math.max(0, (parseFloat(formGrossWeight) || 0) - (parseFloat(formStoneWeight) || 0))
  ).toFixed(4);

  // Sync active branch
  useEffect(() => {
    if (activeBranchId) {
      setSelectedBranchId(activeBranchId);
      setFormBranchId(activeBranchId);
    } else if (branches.length > 0) {
      setSelectedBranchId(branches[0].id);
      setFormBranchId(branches[0].id);
    }
  }, [activeBranchId, branches]);

  // Load Reference Masters once
  useEffect(() => {
    const loadMasters = async () => {
      try {
        const [mats, cats, purs, specs] = await Promise.all([
          materialApi.getMaterials(true),
          categoryApi.getCategories(true),
          purityApi.getPurities(true),
          specificationApi.getSpecifications(true),
        ]);
        setMaterials(mats);
        setCategories(cats);
        setPurities(purs);
        setSpecifications(specs);

        if (mats.length > 0) setFormMaterialId(mats[0].id);
        if (cats.length > 0) setFormCategoryId(cats[0].id);
        if (purs.length > 0) setFormPurityId(purs[0].id);
      } catch (err: any) {
        console.error('Error loading masters', err);
      }
    };
    loadMasters();
  }, []);

  // Load locations for the selected branch
  useEffect(() => {
    const loadLocations = async () => {
      const targetBranch = formBranchId || selectedBranchId || activeBranchId;
      if (!targetBranch) return;
      try {
        const locs = await stockLocationApi.getStockLocations(targetBranch, true);
        setBranchLocations(locs);
        if (locs.length > 0 && !formLocationId) {
          setFormLocationId(locs[0].id);
        }
      } catch (err: any) {
        console.error('Error loading stock locations', err);
      }
    };
    loadLocations();
  }, [selectedBranchId, formBranchId, activeBranchId]);

  // Load Inventory list
  const loadInventory = async (page = currentPage) => {
    setLoading(true);
    setError(null);
    try {
      const res = await inventoryApi.getInventory({
        search: search.trim() || undefined,
        branchId: selectedBranchId || undefined,
        stockLocationId: selectedLocationId || undefined,
        materialId: selectedMaterialId || undefined,
        categoryId: selectedCategoryId || undefined,
        purityId: selectedPurityId || undefined,
        status: selectedStatus ? parseInt(selectedStatus, 10) : undefined,
        isActive: showInactive ? undefined : true,
        page,
        pageSize: 20,
      });
      setInventoryResult(res);
      setCurrentPage(page);
    } catch (err: any) {
      setError(err?.message || 'Failed to load inventory records');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadInventory(1);
  }, [selectedBranchId, selectedLocationId, selectedMaterialId, selectedCategoryId, selectedPurityId, selectedStatus, showInactive]);

  // Quick Barcode / Tag Scan Lookup (Single Items or Compositions)
  const handleQuickScan = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!scanInput.trim()) return;
    setError(null);
    const term = scanInput.trim();
    try {
      let lookupResult: InventoryLookup;
      try {
        lookupResult = await inventoryApi.lookupByBarcode(term);
        setScannedResult(lookupResult);
        setIsScanModalOpen(true);
        setScanInput('');
        return;
      } catch {
        lookupResult = await inventoryApi.lookupByTag(term);
        setScannedResult(lookupResult);
        setIsScanModalOpen(true);
        setScanInput('');
        return;
      }
    } catch {
      // If not single item, try composition lookup
      try {
        let comp: InventoryComposition;
        try {
          comp = await compositionApi.lookupByBarcode(term);
        } catch {
          comp = await compositionApi.lookupByTag(term);
        }
        setSelectedComposition(comp);
        setIsCompositionModalOpen(true);
        setScanInput('');
        return;
      } catch (err: any) {
        setError(err?.message || `No inventory item or composition found matching '${scanInput}'.`);
      }
    }
  };

  // Open Create Modal
  const handleOpenCreate = () => {
    setEditingItem(null);
    setFormName('');
    setFormItemCode('');
    setFormTagNumber('');
    setFormBarcode('');
    setFormGrossWeight('10.0000');
    setFormStoneWeight('0.0000');
    setFormPieceCount('1');
    setFormMakingChargeMethod('1');
    setFormMakingChargeValue('0.00');
    setFormDescription('');
    setFormIsActive(true);
    setFormSelectedSpecs([]);
    if (branchLocations.length > 0) setFormLocationId(branchLocations[0].id);
    if (materials.length > 0) setFormMaterialId(materials[0].id);
    if (categories.length > 0) setFormCategoryId(categories[0].id);
    if (purities.length > 0) setFormPurityId(purities[0].id);
    setError(null);
    setIsFormModalOpen(true);
  };

  // Open Edit Modal
  const handleOpenEdit = (item: InventoryItem) => {
    setEditingItem(item);
    setFormBranchId(item.branchId);
    setFormLocationId(item.stockLocationId);
    setFormName(item.name);
    setFormItemCode(item.itemCode);
    setFormTagNumber(item.tagNumber);
    setFormBarcode(item.barcode);
    setFormMaterialId(item.materialId);
    setFormCategoryId(item.categoryId);
    setFormPurityId(item.purityId);
    setFormGrossWeight(item.grossWeight.toFixed(4));
    setFormStoneWeight(item.stoneWeight.toFixed(4));
    setFormPieceCount(String(item.pieceCount));
    setFormMakingChargeMethod(String(item.makingChargeMethod));
    setFormMakingChargeValue(item.makingChargeValue.toFixed(2));
    setFormDescription(item.description || '');
    setFormIsActive(item.isActive);
    setFormSelectedSpecs(
      item.specifications
        .filter((s) => s.specificationOptionId)
        .map((s) => ({
          specificationId: s.specificationId,
          specificationOptionId: s.specificationOptionId!,
          customValue: s.customValue || undefined,
        }))
    );
    setError(null);
    setIsFormModalOpen(true);
  };

  // Submit Form (Create / Edit)
  const handleSubmitForm = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const gross = parseFloat(formGrossWeight);
      const stone = parseFloat(formStoneWeight);
      const pieces = parseInt(formPieceCount, 10);
      const mcValue = parseFloat(formMakingChargeValue);
      const mcMethod = parseInt(formMakingChargeMethod, 10);

      if (isNaN(gross) || gross < 0) throw new Error('Gross weight must be a valid non-negative number.');
      if (isNaN(stone) || stone < 0) throw new Error('Stone weight must be a valid non-negative number.');
      if (stone > gross) throw new Error('Stone weight cannot exceed gross weight.');
      if (isNaN(pieces) || pieces <= 0) throw new Error('Piece count must be at least 1.');
      if (isNaN(mcValue) || mcValue < 0) throw new Error('Making charge value must be non-negative.');

      const specsPayload = formSelectedSpecs.filter(s => s.specificationOptionId || s.customValue).map(s => ({
        specificationId: s.specificationId,
        specificationOptionId: s.specificationOptionId || undefined,
        customValue: s.customValue || undefined,
      }));

      if (editingItem) {
        await inventoryApi.updateInventoryItem(editingItem.id, {
          stockLocationId: formLocationId,
          name: formName.trim(),
          itemCode: formItemCode.trim() || undefined,
          materialId: formMaterialId,
          categoryId: formCategoryId,
          purityId: formPurityId,
          grossWeight: gross,
          stoneWeight: stone,
          pieceCount: pieces,
          makingChargeMethod: mcMethod,
          makingChargeValue: mcValue,
          description: formDescription.trim() || undefined,
          isActive: formIsActive,
          specifications: specsPayload,
          version: editingItem.version,
        });
        setSuccess(`Inventory item '${formName}' updated successfully.`);
      } else {
        await inventoryApi.createInventoryItem({
          branchId: formBranchId,
          stockLocationId: formLocationId,
          name: formName.trim(),
          itemCode: formItemCode.trim() || undefined,
          tagNumber: formTagNumber.trim() || undefined,
          barcode: formBarcode.trim() || undefined,
          materialId: formMaterialId,
          categoryId: formCategoryId,
          purityId: formPurityId,
          grossWeight: gross,
          stoneWeight: stone,
          pieceCount: pieces,
          makingChargeMethod: mcMethod,
          makingChargeValue: mcValue,
          description: formDescription.trim() || undefined,
          isActive: formIsActive,
          specifications: specsPayload,
        });
        setSuccess(`Inventory item '${formName}' created successfully.`);
      }

      setIsFormModalOpen(false);
      loadInventory(currentPage);
    } catch (err: any) {
      if (err?.status === 409 || err?.message?.includes('modified by another user')) {
        setError('This inventory item was modified by another user. Refresh the item before saving again.');
      } else {
        setError(err?.message || 'Failed to save inventory item');
      }
    }
  };

  // Toggle active/inactive
  const handleToggleStatus = async (item: InventoryItem) => {
    try {
      await inventoryApi.toggleActive(item.id);
      setSuccess(`Active visibility updated for ${item.name} (${item.tagNumber})`);
      loadInventory(currentPage);
    } catch (err: any) {
      setError(err?.message || 'Failed to toggle active status');
    }
  };

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Top Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-gray-200 pb-5">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-gray-900 flex items-center space-x-2">
              <span>Inventory Core & Barcodes</span>
              <span className="text-xs px-2.5 py-0.5 rounded-full font-semibold bg-[#0D3826]/10 text-[#0D3826] border border-[#0D3826]/20">
                Module 5
              </span>
            </h1>
            <p className="text-sm text-gray-500 mt-1">
              Physical jewellery inventory tracking, single-item creation workflow, weights (Net = Gross - Stone), barcodes, and branch isolation.
            </p>
          </div>

          <div className="relative">
            <button
              type="button"
              onClick={() => setIsAddMenuOpen(!isAddMenuOpen)}
              disabled={!canCreate}
              className={`inline-flex items-center space-x-2 px-4 py-2 text-white text-sm font-semibold rounded-lg shadow-sm border border-[#D4AF37]/50 transition ${
                canCreate
                  ? 'bg-[#0D3826] hover:bg-[#1B6543]'
                  : 'bg-gray-400 cursor-not-allowed opacity-60'
              }`}
              title={canCreate ? "Create single item, assembly, or group" : "Creation permission required"}
            >
              <span className="text-base font-bold">+</span>
              <span>Add Inventory Item</span>
              <span className="text-xs">▾</span>
            </button>

            {isAddMenuOpen && canCreate && (
              <div className="absolute right-0 mt-2 w-64 bg-white rounded-xl shadow-xl border border-gray-200 py-2 z-50 animate-in fade-in zoom-in-95">
                <Link
                  href="/inventory/new"
                  onClick={() => setIsAddMenuOpen(false)}
                  className="flex items-start px-4 py-2.5 hover:bg-gray-50 transition"
                >
                  <span className="text-lg mr-3">💍</span>
                  <div>
                    <div className="text-xs font-bold text-gray-900">Single Item</div>
                    <div className="text-[11px] text-gray-500">Create one individually tracked jewellery item.</div>
                  </div>
                </Link>

                <div className="border-t border-gray-100 my-1" />

                <Link
                  href="/inventory/compositions/new?type=assembly"
                  onClick={() => setIsAddMenuOpen(false)}
                  className="flex items-start px-4 py-2.5 hover:bg-gray-50 transition"
                >
                  <span className="text-lg mr-3">🪡</span>
                  <div>
                    <div className="text-xs font-bold text-purple-900 flex items-center space-x-1.5">
                      <span>Assembly</span>
                      <span className="text-[9px] bg-purple-100 text-purple-700 px-1.5 py-0.2 rounded font-semibold">Same Cat</span>
                    </div>
                    <div className="text-[11px] text-gray-500">Bundle 2+ items from the exact same category (e.g. Bangle pair).</div>
                  </div>
                </Link>

                <Link
                  href="/inventory/compositions/new?type=group"
                  onClick={() => setIsAddMenuOpen(false)}
                  className="flex items-start px-4 py-2.5 hover:bg-gray-50 transition"
                >
                  <span className="text-lg mr-3">👑</span>
                  <div>
                    <div className="text-xs font-bold text-indigo-900 flex items-center space-x-1.5">
                      <span>Group</span>
                      <span className="text-[9px] bg-indigo-100 text-indigo-700 px-1.5 py-0.2 rounded font-semibold">Mixed Cat</span>
                    </div>
                    <div className="text-[11px] text-gray-500">Bundle 2+ items across different categories (e.g. Bridal set).</div>
                  </div>
                </Link>
              </div>
            )}
          </div>
        </div>

        {/* Notifications */}
        {error && (
          <div className="p-3 bg-red-50 border-l-4 border-red-500 text-red-700 text-sm rounded-r-lg flex justify-between items-center">
            <span>{error}</span>
            <button onClick={() => setError(null)} className="text-red-500 font-bold ml-2">✕</button>
          </div>
        )}
        {success && (
          <div className="p-3 bg-emerald-50 border-l-4 border-emerald-500 text-emerald-800 text-sm rounded-r-lg flex justify-between items-center">
            <span>{success}</span>
            <button onClick={() => setSuccess(null)} className="text-emerald-500 font-bold ml-2">✕</button>
          </div>
        )}

        {/* View Mode Tabs (Single Items vs Compositions) */}
        <div className="flex border-b border-gray-200">
          <button
            type="button"
            onClick={() => setViewMode('single')}
            className={`py-3 px-6 text-sm font-bold border-b-2 transition flex items-center space-x-2 ${
              viewMode === 'single'
                ? 'border-[#0D3826] text-[#0D3826]'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            <span>💍</span>
            <span>Single Inventory Items ({inventoryResult.totalCount})</span>
          </button>
          <button
            type="button"
            onClick={() => {
              setViewMode('compositions');
              fetchCompositions();
            }}
            className={`py-3 px-6 text-sm font-bold border-b-2 transition flex items-center space-x-2 ${
              viewMode === 'compositions'
                ? 'border-[#0D3826] text-[#0D3826]'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            <span>👑</span>
            <span>Compositions (Assemblies & Groups)</span>
            {compositions.length > 0 && (
              <span className="text-[10px] bg-[#0D3826]/10 text-[#0D3826] px-1.5 py-0.5 rounded-full font-bold">
                {compositions.length}
              </span>
            )}
          </button>
        </div>

        {/* Fast Scanner Bar & Search Controls */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
          {/* Fast Barcode / Tag Lookup Bar */}
          <div className="lg:col-span-1 bg-gradient-to-r from-[#0D3826] to-[#154c34] p-4 rounded-xl text-white shadow-sm flex flex-col justify-between">
            <div>
              <div className="flex items-center space-x-2 text-[#D4AF37] font-bold text-xs uppercase tracking-wider mb-1">
                <span>⚡</span>
                <span>Quick Scanner / POS Lookup</span>
              </div>
              <p className="text-xs text-gray-200 mb-3">
                Scan or type any Tag Number or Barcode for instant lookup across your branch stock.
              </p>
            </div>
            <form onSubmit={handleQuickScan} className="flex space-x-2">
              <input
                type="text"
                placeholder="Scan Barcode or Tag..."
                value={scanInput}
                onChange={(e) => setScanInput(e.target.value)}
                className="flex-1 px-3 py-1.5 text-xs text-gray-900 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#D4AF37]"
              />
              <button
                type="submit"
                className="px-3 py-1.5 bg-[#D4AF37] hover:bg-[#b89528] text-white text-xs font-bold rounded-lg shadow transition"
              >
                Lookup
              </button>
            </form>
          </div>

          {/* Search & Branch Selection */}
          <div className="lg:col-span-2 bg-white p-4 rounded-xl border border-gray-200 shadow-sm flex flex-col justify-between space-y-3">
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
              <div>
                <label className="block text-[11px] font-semibold text-gray-600 mb-1">Search Keywords</label>
                <input
                  type="text"
                  placeholder="Item Name, SKU, Tag, Barcode..."
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  className="w-full px-3 py-1.5 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                />
              </div>

              <div>
                <label className="block text-[11px] font-semibold text-gray-600 mb-1">Branch Context</label>
                <select
                  value={selectedBranchId}
                  onChange={(e) => setSelectedBranchId(e.target.value)}
                  className="w-full px-2 py-1.5 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826] bg-white font-medium"
                >
                  {user?.isGlobalAdmin && <option value="">🌐 All Branches (Global)</option>}
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.code} - {b.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-[11px] font-semibold text-gray-600 mb-1">Display / Stock Location</label>
                <select
                  value={selectedLocationId}
                  onChange={(e) => setSelectedLocationId(e.target.value)}
                  className="w-full px-2 py-1.5 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826] bg-white font-medium"
                >
                  <option value="">All Locations</option>
                  {branchLocations.map((l) => (
                    <option key={l.id} value={l.id}>
                      {l.code} - {l.name} ({l.locationType})
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {/* Sub-Filters */}
            <div className="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-gray-100 text-xs">
              <div className="flex flex-wrap items-center gap-2">
                <select
                  value={selectedMaterialId}
                  onChange={(e) => setSelectedMaterialId(e.target.value)}
                  className="px-2 py-1 text-xs border border-gray-300 rounded-lg bg-white"
                >
                  <option value="">All Materials</option>
                  {materials.map((m) => (
                    <option key={m.id} value={m.id}>{m.name}</option>
                  ))}
                </select>

                <select
                  value={selectedCategoryId}
                  onChange={(e) => setSelectedCategoryId(e.target.value)}
                  className="px-2 py-1 text-xs border border-gray-300 rounded-lg bg-white"
                >
                  <option value="">All Categories</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>

                <select
                  value={selectedPurityId}
                  onChange={(e) => setSelectedPurityId(e.target.value)}
                  className="px-2 py-1 text-xs border border-gray-300 rounded-lg bg-white"
                >
                  <option value="">All Purities</option>
                  {purities.map((p) => (
                    <option key={p.id} value={p.id}>{p.name} ({p.karatValue}K)</option>
                  ))}
                </select>

                <select
                  value={selectedStatus}
                  onChange={(e) => setSelectedStatus(e.target.value)}
                  className="px-2 py-1 text-xs border border-gray-300 rounded-lg bg-white font-medium"
                >
                  <option value="">All Statuses</option>
                  <option value="1">Available</option>
                  <option value="2">Reserved</option>
                  <option value="3">Sold</option>
                </select>
              </div>

              <div className="flex items-center space-x-3">
                <label className="flex items-center space-x-1.5 text-gray-600 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={showInactive}
                    onChange={(e) => setShowInactive(e.target.checked)}
                    className="rounded border-gray-300 text-[#0D3826] focus:ring-[#0D3826]"
                  />
                  <span>Show Inactive</span>
                </label>
                <button
                  onClick={() => loadInventory(currentPage)}
                  disabled={loading}
                  className="px-3 py-1 bg-gray-100 hover:bg-gray-200 text-gray-700 font-semibold rounded-lg transition"
                >
                  {loading ? 'Refreshing...' : 'Apply Filters'}
                </button>
              </div>
            </div>
          </div>
        </div>

        {/* Inventory Data Table (Single Items) */}
        {viewMode === 'single' && (
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
            <div className="px-4 py-3 bg-[#0D3826]/5 border-b border-gray-200 flex items-center justify-between text-xs">
              <span className="font-bold text-gray-700 uppercase tracking-wider">
                Tracked Physical Inventory ({inventoryResult.totalCount} items)
              </span>
              <span className="text-gray-500 text-[11px]">
                Page {inventoryResult.page} of {inventoryResult.totalPages || 1}
              </span>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                <thead className="bg-gray-50 text-gray-700 font-bold uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Tag Number / Barcode</th>
                    <th className="px-4 py-3">Item Name & SKU</th>
                    <th className="px-4 py-3">Classification</th>
                    <th className="px-4 py-3 text-right">Weights (Gross / Stone / Net)</th>
                    <th className="px-4 py-3 text-center">Piece</th>
                    <th className="px-4 py-3">Branch & Location</th>
                    <th className="px-4 py-3 text-center">Status</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {inventoryResult.items.map((item) => (
                    <tr key={item.id} className="hover:bg-gray-50/80 transition">
                      <td className="px-4 py-3">
                        <div className="font-mono font-bold text-[#0D3826] text-xs">{item.tagNumber}</div>
                        <div className="font-mono text-[10px] text-gray-400">{item.barcode}</div>
                      </td>
                      <td className="px-4 py-3">
                        <div className="font-semibold text-gray-900">{item.name}</div>
                        <div className="text-[10px] text-gray-500 font-mono">{item.itemCode}</div>
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex flex-wrap gap-1">
                          <span className="px-1.5 py-0.5 rounded bg-emerald-50 text-emerald-800 text-[10px] font-semibold border border-emerald-200">
                            {item.materialName}
                          </span>
                          <span className="px-1.5 py-0.5 rounded bg-amber-50 text-amber-800 text-[10px] font-semibold border border-amber-200">
                            {item.karatValue}K
                          </span>
                          <span className="px-1.5 py-0.5 rounded bg-gray-100 text-gray-700 text-[10px] font-medium">
                            {item.categoryName}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3 text-right font-mono text-xs">
                        <div><span className="text-gray-400 text-[10px]">Net: </span><span className="font-bold text-gray-900">{item.netWeight.toFixed(4)} g</span></div>
                        <div className="text-[10px] text-gray-400">G: {item.grossWeight.toFixed(4)}g | S: {item.stoneWeight.toFixed(4)}g</div>
                      </td>
                      <td className="px-4 py-3 text-center font-mono font-bold text-gray-700">
                        {item.pieceCount}
                      </td>
                      <td className="px-4 py-3">
                        <div className="font-medium text-gray-800">{item.locationName}</div>
                        <div className="text-[10px] text-gray-400">{item.branchCode} • {item.locationType}</div>
                      </td>
                      <td className="px-4 py-3 text-center space-y-1">
                        <div>
                          <span
                            className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold ${
                              item.status === 1
                                ? 'bg-emerald-100 text-emerald-800'
                                : item.status === 2
                                ? 'bg-amber-100 text-amber-800'
                                : 'bg-red-100 text-red-800'
                            }`}
                          >
                            {item.statusName}
                          </span>
                        </div>
                        <div>
                          <span
                            className={`inline-flex px-1.5 py-0.2 rounded text-[9px] font-bold uppercase ${
                              item.isActive ? 'bg-blue-50 text-blue-700 border border-blue-200' : 'bg-gray-100 text-gray-500'
                            }`}
                          >
                            {item.isActive ? 'ACTIVE' : 'INACTIVE'}
                          </span>
                        </div>
                      </td>
                      <td className="px-4 py-3 text-right space-x-2">
                        <button
                          onClick={() => {
                            setSelectedItem(item);
                            setIsDetailsModalOpen(true);
                          }}
                          className="text-blue-600 hover:underline font-semibold"
                        >
                          View
                        </button>
                        {canEdit && item.status !== 3 && (
                          <button
                            onClick={() => handleOpenEdit(item)}
                            className="text-[#0D3826] hover:underline font-semibold"
                          >
                            Edit
                          </button>
                        )}
                        {item.status === 3 && (
                          <span className="text-[10px] text-gray-400 font-medium italic">
                            Locked
                          </span>
                        )}
                        {canEdit && item.status !== 3 && (
                          <button
                            onClick={() => handleToggleStatus(item)}
                            className={`font-semibold hover:underline ${item.isActive ? 'text-amber-700' : 'text-emerald-700'}`}
                          >
                            {item.isActive ? 'Deact' : 'Act'}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                  {inventoryResult.items.length === 0 && !loading && (
                    <tr>
                      <td colSpan={8} className="text-center py-12 text-gray-500">
                        No inventory items found matching current filters.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            {/* Pagination Footer */}
            {inventoryResult.totalPages > 1 && (
              <div className="px-4 py-3 bg-gray-50 border-t border-gray-200 flex items-center justify-between text-xs">
                <button
                  disabled={!inventoryResult.hasPreviousPage}
                  onClick={() => loadInventory(currentPage - 1)}
                  className={`px-3 py-1 rounded border ${
                    inventoryResult.hasPreviousPage
                      ? 'bg-white hover:bg-gray-100 text-gray-700 border-gray-300'
                      : 'bg-gray-100 text-gray-400 border-gray-200 cursor-not-allowed'
                  }`}
                >
                  Previous
                </button>
                <span className="text-gray-600 font-medium">
                  Page {inventoryResult.page} of {inventoryResult.totalPages}
                </span>
                <button
                  disabled={!inventoryResult.hasNextPage}
                  onClick={() => loadInventory(currentPage + 1)}
                  className={`px-3 py-1 rounded border ${
                    inventoryResult.hasNextPage
                      ? 'bg-white hover:bg-gray-100 text-gray-700 border-gray-300'
                      : 'bg-gray-100 text-gray-400 border-gray-200 cursor-not-allowed'
                  }`}
                >
                  Next
                </button>
              </div>
            )}
          </div>
        )}

        {/* Compositions Data Table (Module 6: Assemblies & Groups) */}
        {viewMode === 'compositions' && (
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
            <div className="px-4 py-3 bg-[#0D3826]/5 border-b border-gray-200 flex items-center justify-between text-xs">
              <span className="font-bold text-gray-700 uppercase tracking-wider flex items-center space-x-2">
                <span>👑</span>
                <span>Active Compositions ({compositions.length} items)</span>
              </span>
              <button
                onClick={fetchCompositions}
                disabled={isLoadingCompositions}
                className="text-[#0D3826] hover:underline font-semibold"
              >
                {isLoadingCompositions ? 'Refreshing...' : 'Refresh'}
              </button>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                <thead className="bg-gray-50 text-gray-700 font-bold uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Tag / Barcode</th>
                    <th className="px-4 py-3">Composition Details</th>
                    <th className="px-4 py-3 text-center">Type</th>
                    <th className="px-4 py-3 text-right">Aggregate Weights (G / S / Net)</th>
                    <th className="px-4 py-3 text-center">Pieces / Members</th>
                    <th className="px-4 py-3">Branch & Location</th>
                    <th className="px-4 py-3 text-center">Status</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {compositions.map((comp) => (
                    <tr key={comp.id} className="hover:bg-gray-50/80 transition">
                      <td className="px-4 py-3">
                        <div className="font-mono font-bold text-[#0D3826] text-xs">{comp.tagNumber}</div>
                        <div className="font-mono text-[10px] text-gray-400">{comp.barcode}</div>
                      </td>
                      <td className="px-4 py-3">
                        <div className="font-semibold text-gray-900">{comp.name}</div>
                        {comp.description && (
                          <div className="text-[11px] text-gray-500 line-clamp-1">{comp.description}</div>
                        )}
                      </td>
                      <td className="px-4 py-3 text-center">
                        {comp.type === 1 ? (
                          <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-purple-100 text-purple-800 border border-purple-200">
                            Assembly {comp.members?.[0]?.categoryName ? `• ${comp.members[0].categoryName}` : ''}
                          </span>
                        ) : (
                          <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-indigo-100 text-indigo-800 border border-indigo-200">
                            Group (Mixed)
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-right font-mono text-xs">
                        <div>
                          <span className="text-gray-400 text-[10px]">Net: </span>
                          <span className="font-bold text-gray-900">{comp.totalNetWeight.toFixed(4)} g</span>
                        </div>
                        <div className="text-[10px] text-gray-400">
                          G: {comp.totalGrossWeight.toFixed(4)}g | S: {comp.totalStoneWeight.toFixed(4)}g
                        </div>
                      </td>
                      <td className="px-4 py-3 text-center">
                        <div className="font-mono font-bold text-gray-800">{comp.totalPieceCount} Pcs</div>
                        <div className="text-[10px] text-gray-500">{comp.activeMemberCount} items</div>
                      </td>
                      <td className="px-4 py-3">
                        <div className="font-medium text-gray-800">{comp.stockLocationName}</div>
                        <div className="text-[10px] text-gray-400">{comp.branchCode}</div>
                      </td>
                      <td className="px-4 py-3 text-center">
                        <span
                          className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold ${
                            comp.status === 1
                              ? 'bg-emerald-100 text-emerald-800'
                              : 'bg-gray-100 text-gray-600'
                          }`}
                        >
                          {comp.statusName}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right space-x-2">
                        <button
                          onClick={() => {
                            setSelectedComposition(comp);
                            setIsCompositionModalOpen(true);
                          }}
                          className="text-blue-600 hover:underline font-semibold"
                        >
                          View Members
                        </button>
                        {canDissolve && comp.status === 1 && (
                          <button
                            onClick={() => handleDissolveComposition(comp.id)}
                            disabled={isDissolving}
                            className="text-red-600 hover:underline font-semibold"
                          >
                            Dissolve
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                  {compositions.length === 0 && !isLoadingCompositions && (
                    <tr>
                      <td colSpan={8} className="text-center py-12 text-gray-500">
                        No compositions found for selected branch and location.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Quick Scan / Lookup Result Modal */}
        {isScanModalOpen && scannedResult && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="bg-white rounded-xl shadow-xl max-w-md w-full overflow-hidden border border-gray-200 animate-in fade-in zoom-in-95 duration-150">
              <div className="bg-[#0D3826] px-6 py-4 text-white flex items-center justify-between">
                <div className="flex items-center space-x-2">
                  <span>⚡</span>
                  <h3 className="font-bold text-sm tracking-wide">Scanned Item Result</h3>
                </div>
                <button onClick={() => setIsScanModalOpen(false)} className="text-gray-300 hover:text-white font-bold">
                  ✕
                </button>
              </div>

              <div className="p-6 space-y-4 text-xs">
                <div className="flex justify-between items-start border-b pb-3">
                  <div>
                    <h4 className="font-bold text-sm text-gray-900">{scannedResult.name}</h4>
                    <p className="font-mono text-gray-500 text-xs">Tag: {scannedResult.tagNumber}</p>
                    <p className="font-mono text-gray-400 text-[11px]">Barcode: {scannedResult.barcode}</p>
                  </div>
                  <span
                    className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                      scannedResult.status === 1 ? 'bg-emerald-100 text-emerald-800' : 'bg-red-100 text-red-800'
                    }`}
                  >
                    {scannedResult.statusName}
                  </span>
                </div>

                {/* Scanned Barcode Display */}
                <div className="flex justify-center p-2.5 bg-gray-50 border border-gray-200 rounded-xl">
                  <BarcodeDisplay value={scannedResult.barcode} width={260} height={65} />
                </div>

                <div className="grid grid-cols-2 gap-3 bg-gray-50 p-3 rounded-lg">
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Material & Purity</span>
                    <p className="font-semibold text-gray-800">{scannedResult.materialName} ({scannedResult.karatValue}K)</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Category</span>
                    <p className="font-semibold text-gray-800">{scannedResult.categoryName}</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Net Gold Weight</span>
                    <p className="font-mono font-bold text-[#0D3826] text-sm">{scannedResult.netWeight.toFixed(4)} g</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Location</span>
                    <p className="font-medium text-gray-800">{scannedResult.branchCode} • {scannedResult.locationName}</p>
                  </div>
                </div>

                <div className="flex justify-end pt-2">
                  <button
                    onClick={() => setIsScanModalOpen(false)}
                    className="px-4 py-1.5 bg-[#0D3826] text-white font-semibold rounded-lg"
                  >
                    Close
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Item Details Full Modal */}
        {isDetailsModalOpen && selectedItem && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="bg-white rounded-xl shadow-xl max-w-2xl w-full overflow-hidden border border-gray-200 animate-in fade-in zoom-in-95 duration-150">
              <div className="bg-[#0D3826] px-6 py-4 text-white flex items-center justify-between">
                <div>
                  <h3 className="font-bold text-sm tracking-wide">{selectedItem.name}</h3>
                  <p className="text-[11px] text-[#D4AF37] font-mono">Tag: {selectedItem.tagNumber} | Barcode: {selectedItem.barcode}</p>
                </div>
                <button onClick={() => setIsDetailsModalOpen(false)} className="text-gray-300 hover:text-white font-bold">
                  ✕
                </button>
              </div>

              <div className="p-6 space-y-5 text-xs max-h-[80vh] overflow-y-auto">
                {/* Visual Barcode */}
                <div className="flex justify-center p-3 bg-gray-50 border border-gray-200 rounded-xl">
                  <BarcodeDisplay value={selectedItem.barcode} width={280} height={70} />
                </div>

                {/* Weight Breakdown Card */}
                <div className="bg-amber-50/50 border border-amber-200 p-4 rounded-xl">
                  <h4 className="font-bold text-amber-900 text-xs uppercase tracking-wider mb-2">Weight Breakdown (Jewellery Standard)</h4>
                  <div className="grid grid-cols-3 gap-4 text-center">
                    <div className="bg-white p-2.5 rounded-lg border border-amber-200/60 shadow-sm">
                      <span className="text-[10px] uppercase font-bold text-gray-400">Gross Weight</span>
                      <p className="font-mono text-base font-bold text-gray-900">{selectedItem.grossWeight.toFixed(4)} g</p>
                    </div>
                    <div className="bg-white p-2.5 rounded-lg border border-amber-200/60 shadow-sm">
                      <span className="text-[10px] uppercase font-bold text-gray-400">Stone Weight</span>
                      <p className="font-mono text-base font-bold text-gray-900">{selectedItem.stoneWeight.toFixed(4)} g</p>
                    </div>
                    <div className="bg-white p-2.5 rounded-lg border border-emerald-300 shadow-sm">
                      <span className="text-[10px] uppercase font-bold text-emerald-800">Net Metal Weight</span>
                      <p className="font-mono text-base font-bold text-emerald-900">{selectedItem.netWeight.toFixed(4)} g</p>
                    </div>
                  </div>
                </div>

                {/* Classification & Attributes */}
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-gray-50 p-4 rounded-xl border border-gray-200">
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Material</span>
                    <p className="font-semibold text-gray-800">{selectedItem.materialName} ({selectedItem.materialCode})</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Category</span>
                    <p className="font-semibold text-gray-800">{selectedItem.categoryName}</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Karat & Fineness</span>
                    <p className="font-semibold text-amber-700">{selectedItem.karatValue}K ({selectedItem.purityPercentage}%)</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Piece Count</span>
                    <p className="font-semibold text-gray-800">{selectedItem.pieceCount}</p>
                  </div>
                </div>

                {/* Making Charge & Location */}
                <div className="grid grid-cols-2 gap-3 bg-gray-50 p-4 rounded-xl border border-gray-200">
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Making Charge Config</span>
                    <p className="font-semibold text-gray-800">{selectedItem.makingChargeMethodName} @ Rs. {selectedItem.makingChargeValue.toFixed(2)}</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Storage Location</span>
                    <p className="font-semibold text-gray-800">{selectedItem.branchName} • {selectedItem.locationName} ({selectedItem.locationType})</p>
                  </div>
                </div>

                {/* Specifications */}
                <div>
                  <h4 className="font-bold text-gray-900 uppercase tracking-wider text-[11px] mb-2">Configured Specifications</h4>
                  {selectedItem.specifications.length === 0 ? (
                    <p className="text-gray-400 italic">No custom specifications configured.</p>
                  ) : (
                    <div className="grid grid-cols-2 gap-2">
                      {selectedItem.specifications.map((s) => (
                        <div key={s.id} className="p-2.5 bg-gray-50 rounded-lg border border-gray-200 flex justify-between items-center">
                          <span className="font-semibold text-gray-700">{s.specificationName}:</span>
                          <span className="font-mono text-gray-900 font-bold">{s.optionValue || s.customValue || '—'}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>

                {/* Description */}
                {selectedItem.description && (
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Description / Notes</span>
                    <p className="text-gray-700 mt-1">{selectedItem.description}</p>
                  </div>
                )}
              </div>

              <div className="bg-gray-50 px-6 py-3 border-t border-gray-200 flex justify-end">
                <button
                  onClick={() => setIsDetailsModalOpen(false)}
                  className="px-4 py-1.5 bg-[#0D3826] text-white font-semibold rounded-lg text-xs"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Composition Details & Members Modal (Module 6) */}
        {isCompositionModalOpen && selectedComposition && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="bg-white rounded-xl shadow-xl max-w-3xl w-full overflow-hidden border border-gray-200 animate-in fade-in zoom-in-95 duration-150">
              <div className="bg-[#0D3826] px-6 py-4 text-white flex items-center justify-between">
                <div>
                  <div className="flex items-center space-x-2">
                    <span className="text-lg">👑</span>
                    <h3 className="font-bold text-sm tracking-wide">{selectedComposition.name}</h3>
                    <span className="text-xs px-2 py-0.5 rounded-full font-bold bg-[#D4AF37] text-white">
                      {selectedComposition.type === 1 ? 'Assembly' : 'Group'}
                    </span>
                  </div>
                  <p className="text-[11px] text-[#D4AF37] font-mono mt-0.5">
                    Parent Tag: {selectedComposition.tagNumber} | Barcode: {selectedComposition.barcode}
                  </p>
                </div>
                <button
                  onClick={() => setIsCompositionModalOpen(false)}
                  className="text-gray-300 hover:text-white font-bold"
                >
                  ✕
                </button>
              </div>

              <div className="p-6 space-y-5 text-xs max-h-[80vh] overflow-y-auto">
                {/* Visual Barcode Display */}
                <div className="flex justify-center p-3 bg-gray-50 border border-gray-200 rounded-xl">
                  <BarcodeDisplay value={selectedComposition.barcode} width={300} height={70} />
                </div>

                {/* Status Alert if Dissolved */}
                {selectedComposition.status === 2 && (
                  <div className="p-3 bg-amber-50 border border-amber-300 rounded-lg text-amber-800 text-xs">
                    <strong>Note:</strong> This composition was dissolved. All individual member items have been released back into available branch stock.
                  </div>
                )}

                {/* Aggregated Totals Card */}
                <div className="bg-amber-50/50 border border-amber-200 p-4 rounded-xl">
                  <h4 className="font-bold text-amber-900 text-xs uppercase tracking-wider mb-2">
                    Calculated Composition Totals (Derived from Children)
                  </h4>
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                    <div className="bg-white p-2.5 rounded-lg border border-amber-100 shadow-sm">
                      <span className="text-[10px] text-gray-500 uppercase font-semibold">Total Gross Wt</span>
                      <p className="font-mono font-bold text-gray-900 text-sm mt-0.5">
                        {selectedComposition.totalGrossWeight.toFixed(4)} g
                      </p>
                    </div>
                    <div className="bg-white p-2.5 rounded-lg border border-amber-100 shadow-sm">
                      <span className="text-[10px] text-gray-500 uppercase font-semibold">Total Stone Wt</span>
                      <p className="font-mono font-bold text-gray-700 text-sm mt-0.5">
                        {selectedComposition.totalStoneWeight.toFixed(4)} g
                      </p>
                    </div>
                    <div className="bg-white p-2.5 rounded-lg border border-amber-100 shadow-sm">
                      <span className="text-[10px] text-emerald-700 uppercase font-bold">Total Net Wt</span>
                      <p className="font-mono font-bold text-[#0D3826] text-sm mt-0.5">
                        {selectedComposition.totalNetWeight.toFixed(4)} g
                      </p>
                    </div>
                    <div className="bg-white p-2.5 rounded-lg border border-amber-100 shadow-sm">
                      <span className="text-[10px] text-gray-500 uppercase font-semibold">Total Pieces</span>
                      <p className="font-mono font-bold text-gray-900 text-sm mt-0.5">
                        {selectedComposition.totalPieceCount} Pcs
                      </p>
                    </div>
                  </div>
                </div>

                {/* Location & Metadata */}
                <div className="grid grid-cols-2 gap-3 bg-gray-50 p-3 rounded-lg border border-gray-200">
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Branch & Location</span>
                    <p className="font-semibold text-gray-800">
                      {selectedComposition.branchCode} • {selectedComposition.stockLocationName}
                    </p>
                  </div>
                  <div>
                    <span className="text-[10px] text-gray-400 uppercase font-semibold">Created / Created By</span>
                    <p className="text-gray-700">
                      {new Date(selectedComposition.createdAt).toLocaleDateString()}
                    </p>
                  </div>
                  {selectedComposition.description && (
                    <div className="col-span-2">
                      <span className="text-[10px] text-gray-400 uppercase font-semibold">Description / Notes</span>
                      <p className="text-gray-700 mt-0.5">{selectedComposition.description}</p>
                    </div>
                  )}
                </div>

                {/* Member Items Table */}
                <div>
                  <h4 className="font-bold text-gray-800 text-xs uppercase tracking-wider mb-2">
                    Child Member Items ({selectedComposition.members.length} items)
                  </h4>
                  <div className="overflow-x-auto border border-gray-200 rounded-lg">
                    <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                      <thead className="bg-gray-50 font-bold text-gray-700 uppercase tracking-wider">
                        <tr>
                          <th className="px-3 py-2">Tag / Barcode</th>
                          <th className="px-3 py-2">Item Name & SKU</th>
                          <th className="px-3 py-2">Category / Karat</th>
                          <th className="px-3 py-2 text-right">Net Wt</th>
                          <th className="px-3 py-2 text-center">Pcs</th>
                          <th className="px-3 py-2 text-center">Member Status</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-200">
                        {selectedComposition.members.map((m) => (
                          <tr key={m.id} className={m.isActive ? 'hover:bg-gray-50' : 'bg-gray-50/50 text-gray-400'}>
                            <td className="px-3 py-2">
                              <div className="font-mono font-bold text-[#0D3826]">{m.tagNumber}</div>
                              <div className="font-mono text-[10px] text-gray-400">{m.barcode}</div>
                            </td>
                            <td className="px-3 py-2">
                              <div className="font-semibold text-gray-900">{m.itemName}</div>
                              <div className="text-[10px] text-gray-500 font-mono">{m.itemCode}</div>
                            </td>
                            <td className="px-3 py-2">
                              <span className="px-1.5 py-0.5 rounded bg-gray-100 text-gray-700 text-[10px]">
                                {m.categoryName} ({m.purityKarat}K)
                              </span>
                            </td>
                            <td className="px-3 py-2 text-right font-mono font-bold text-gray-900">
                              {m.netWeight.toFixed(4)} g
                            </td>
                            <td className="px-3 py-2 text-center font-mono font-bold">
                              {m.pieceCount}
                            </td>
                            <td className="px-3 py-2 text-center">
                              <span
                                className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                                  m.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-100 text-gray-500'
                                }`}
                              >
                                {m.isActive ? 'Active Member' : 'Removed'}
                              </span>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>

              <div className="bg-gray-50 px-6 py-3 border-t border-gray-200 flex justify-between items-center">
                {canDissolve && selectedComposition.status === 1 ? (
                  <button
                    onClick={() => handleDissolveComposition(selectedComposition.id)}
                    disabled={isDissolving}
                    className="px-4 py-1.5 bg-red-600 hover:bg-red-700 text-white font-semibold rounded-lg text-xs shadow-sm transition"
                  >
                    {isDissolving ? 'Dissolving...' : 'Dissolve Composition'}
                  </button>
                ) : <div />}
                <button
                  onClick={() => setIsCompositionModalOpen(false)}
                  className="px-4 py-1.5 bg-[#0D3826] text-white font-semibold rounded-lg text-xs"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        )}
        {isFormModalOpen && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="bg-white rounded-xl shadow-xl max-w-2xl w-full overflow-hidden border border-gray-200 animate-in fade-in zoom-in-95 duration-150">
              <div className="bg-[#0D3826] px-6 py-4 text-white flex items-center justify-between">
                <div>
                  <h3 className="font-bold text-sm tracking-wide">
                    {editingItem ? 'Edit Inventory Item' : 'New Jewellery Inventory Item'}
                  </h3>
                  <p className="text-[10px] text-gray-300">
                    {editingItem ? `Tag: ${editingItem.tagNumber}` : 'Auto-generates unique collision-safe tag & barcode'}
                  </p>
                </div>
                <button onClick={() => setIsFormModalOpen(false)} className="text-gray-300 hover:text-white font-bold">
                  ✕
                </button>
              </div>

              <form onSubmit={handleSubmitForm} className="p-6 space-y-4 text-xs max-h-[80vh] overflow-y-auto">
                {/* Branch & Location (Branch Scoped) */}
                <div className="grid grid-cols-2 gap-3 bg-amber-50/50 p-3 rounded-xl border border-amber-200">
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Assigned Branch *</label>
                    <select
                      value={formBranchId}
                      disabled={!!editingItem}
                      onChange={(e) => setFormBranchId(e.target.value)}
                      required
                      className="w-full px-3 py-1.5 border border-gray-300 rounded-lg bg-white"
                    >
                      {branches.map((b) => (
                        <option key={b.id} value={b.id}>{b.code} - {b.name}</option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Stock / Display Location *</label>
                    <select
                      value={formLocationId}
                      onChange={(e) => setFormLocationId(e.target.value)}
                      required
                      className="w-full px-3 py-1.5 border border-gray-300 rounded-lg bg-white font-semibold text-[#0D3826]"
                    >
                      {branchLocations.map((l) => (
                        <option key={l.id} value={l.id}>
                          {l.code} - {l.name} ({l.locationType})
                        </option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Name & SKU */}
                <div className="grid grid-cols-3 gap-3">
                  <div className="col-span-2">
                    <label className="block font-semibold text-gray-700 mb-1">Jewellery Item Name *</label>
                    <input
                      type="text"
                      required
                      value={formName}
                      onChange={(e) => setFormName(e.target.value)}
                      placeholder="e.g. 22K Traditional Peacock Necklace"
                      className="w-full px-3 py-1.5 border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                    />
                  </div>
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Item SKU / Code</label>
                    <input
                      type="text"
                      value={formItemCode}
                      onChange={(e) => setFormItemCode(e.target.value)}
                      placeholder="Optional SKU..."
                      className="w-full px-3 py-1.5 font-mono uppercase border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                    />
                  </div>
                </div>

                {/* Material, Category, Purity */}
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Material *</label>
                    <select
                      value={formMaterialId}
                      onChange={(e) => setFormMaterialId(e.target.value)}
                      required
                      className="w-full px-3 py-1.5 border border-gray-300 rounded-lg bg-white"
                    >
                      {materials.map((m) => (
                        <option key={m.id} value={m.id}>{m.name} ({m.code})</option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Category *</label>
                    <select
                      value={formCategoryId}
                      onChange={(e) => setFormCategoryId(e.target.value)}
                      required
                      className="w-full px-3 py-1.5 border border-gray-300 rounded-lg bg-white"
                    >
                      {categories.map((c) => (
                        <option key={c.id} value={c.id}>{c.name}</option>
                      ))}
                    </select>
                  </div>

                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Purity / Karat *</label>
                    <select
                      value={formPurityId}
                      onChange={(e) => setFormPurityId(e.target.value)}
                      required
                      className="w-full px-3 py-1.5 border border-gray-300 rounded-lg bg-white font-semibold"
                    >
                      {purities.map((p) => (
                        <option key={p.id} value={p.id}>{p.name} ({p.karatValue}K)</option>
                      ))}
                    </select>
                  </div>
                </div>

                {/* Weights Calculation Card */}
                <div className="bg-gray-50 p-4 rounded-xl border border-gray-200 space-y-3">
                  <div className="flex justify-between items-center border-b pb-2">
                    <span className="font-bold text-gray-800 uppercase text-[11px]">Weight Configuration (Grams)</span>
                    <span className="font-mono text-emerald-800 font-bold text-xs">
                      Net Metal: {computedNetWeight} g
                    </span>
                  </div>

                  <div className="grid grid-cols-3 gap-3">
                    <div>
                      <label className="block font-semibold text-gray-700 mb-1">Gross Weight (g) *</label>
                      <input
                        type="number"
                        step="0.0001"
                        required
                        value={formGrossWeight}
                        onChange={(e) => setFormGrossWeight(e.target.value)}
                        className="w-full px-3 py-1.5 font-mono border border-gray-300 rounded-lg"
                      />
                    </div>
                    <div>
                      <label className="block font-semibold text-gray-700 mb-1">Stone Weight (g) *</label>
                      <input
                        type="number"
                        step="0.0001"
                        required
                        value={formStoneWeight}
                        onChange={(e) => setFormStoneWeight(e.target.value)}
                        className="w-full px-3 py-1.5 font-mono border border-gray-300 rounded-lg"
                      />
                    </div>
                    <div>
                      <label className="block font-semibold text-gray-500 mb-1">Computed Net (g)</label>
                      <input
                        type="text"
                        disabled
                        value={`${computedNetWeight} g`}
                        className="w-full px-3 py-1.5 font-mono font-bold bg-gray-100 text-[#0D3826] border border-gray-200 rounded-lg"
                      />
                    </div>
                  </div>
                </div>

                {/* Making Charge & Pieces */}
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Making Charge Method</label>
                    <select
                      value={formMakingChargeMethod}
                      onChange={(e) => setFormMakingChargeMethod(e.target.value)}
                      className="w-full px-3 py-1.5 border border-gray-300 rounded-lg bg-white"
                    >
                      <option value="1">Per Gram (Standard)</option>
                      <option value="2">Per Item (Artistic)</option>
                    </select>
                  </div>
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Making Charge (Rs.)</label>
                    <input
                      type="number"
                      step="0.01"
                      value={formMakingChargeValue}
                      onChange={(e) => setFormMakingChargeValue(e.target.value)}
                      className="w-full px-3 py-1.5 font-mono border border-gray-300 rounded-lg"
                    />
                  </div>
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Piece Count</label>
                    <input
                      type="number"
                      min="1"
                      value={formPieceCount}
                      onChange={(e) => setFormPieceCount(e.target.value)}
                      className="w-full px-3 py-1.5 font-mono border border-gray-300 rounded-lg"
                    />
                  </div>
                </div>

                {/* Specifications Options */}
                {specifications.length > 0 && (
                  <div className="space-y-2 border-t pt-3">
                    <span className="font-bold text-gray-700 uppercase text-[11px] block">Optional Specifications</span>
                    <div className="grid grid-cols-2 gap-3">
                      {specifications.map((spec) => {
                        const currentSelection = formSelectedSpecs.find((s) => s.specificationId === spec.id);
                        const isFreeText = spec.inputType === 'FREE_TEXT';
                        const isHybrid = spec.inputType === 'HYBRID';

                        return (
                          <div key={spec.id} className="p-2 border rounded-lg bg-gray-50 space-y-1">
                            <div className="flex justify-between items-center">
                              <label className="block font-semibold text-gray-800 text-[11px]">{spec.name}</label>
                              <span className="text-[9px] uppercase px-1 rounded bg-gray-200 text-gray-600 font-mono">
                                {spec.inputType || 'PREDEFINED'}
                              </span>
                            </div>

                            {/* Option Dropdown if PREDEFINED or HYBRID */}
                            {!isFreeText && (
                              <select
                                value={currentSelection?.specificationOptionId || ''}
                                onChange={(e) => {
                                  const val = e.target.value;
                                  setFormSelectedSpecs((prev) => {
                                    const filtered = prev.filter((s) => s.specificationId !== spec.id);
                                    if (val) {
                                      return [...filtered, {
                                        specificationId: spec.id,
                                        specificationOptionId: val,
                                        customValue: isHybrid ? currentSelection?.customValue : undefined,
                                      }];
                                    }
                                    if (isHybrid && currentSelection?.customValue) {
                                      return [...filtered, {
                                        specificationId: spec.id,
                                        specificationOptionId: undefined,
                                        customValue: currentSelection.customValue,
                                      }];
                                    }
                                    return filtered;
                                  });
                                }}
                                className="w-full px-2 py-1 text-xs border border-gray-300 rounded bg-white"
                              >
                                <option value="">Not Specified</option>
                                {spec.options.map((opt) => (
                                  <option key={opt.id} value={opt.id}>{opt.value} ({opt.code})</option>
                                ))}
                              </select>
                            )}

                            {/* Free text custom value input if FREE_TEXT or HYBRID */}
                            {(isFreeText || isHybrid) && (
                              <input
                                type="text"
                                placeholder={isFreeText ? "Enter custom value..." : "Or custom detail..."}
                                value={currentSelection?.customValue || ''}
                                onChange={(e) => {
                                  const val = e.target.value;
                                  setFormSelectedSpecs((prev) => {
                                    const filtered = prev.filter((s) => s.specificationId !== spec.id);
                                    if (val) {
                                      return [...filtered, {
                                        specificationId: spec.id,
                                        specificationOptionId: isFreeText ? undefined : currentSelection?.specificationOptionId,
                                        customValue: val,
                                      }];
                                    }
                                    if (currentSelection?.specificationOptionId) {
                                      return [...filtered, {
                                        specificationId: spec.id,
                                        specificationOptionId: currentSelection.specificationOptionId,
                                        customValue: undefined,
                                      }];
                                    }
                                    return filtered;
                                  });
                                }}
                                className="w-full px-2 py-1 text-xs border border-gray-300 rounded bg-white"
                              />
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}

                {/* Description & Active */}
                <div>
                  <label className="block font-semibold text-gray-700 mb-1">Description / Internal Notes</label>
                  <textarea
                    rows={2}
                    value={formDescription}
                    onChange={(e) => setFormDescription(e.target.value)}
                    placeholder="Details about craftsmanship, certifications, hallmark..."
                    className="w-full px-3 py-1.5 border border-gray-300 rounded-lg"
                  />
                </div>

                <div className="flex items-center">
                  <label className="flex items-center space-x-2 text-gray-700 font-semibold cursor-pointer">
                    <input
                      type="checkbox"
                      checked={formIsActive}
                      onChange={(e) => setFormIsActive(e.target.checked)}
                      className="rounded border-gray-300 text-[#0D3826] focus:ring-[#0D3826]"
                    />
                    <span>Active for operational transactions</span>
                  </label>
                </div>

                <div className="flex justify-end space-x-3 pt-4 border-t border-gray-200">
                  <button
                    type="button"
                    onClick={() => setIsFormModalOpen(false)}
                    className="px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-semibold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="px-4 py-2 bg-[#0D3826] hover:bg-[#1B6543] text-white rounded-lg font-semibold shadow-sm border border-[#D4AF37]/50"
                  >
                    {editingItem ? 'Save Changes' : 'Create Inventory Item'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
