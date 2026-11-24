'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  branchApi,
  stockLocationApi,
  materialApi,
  categoryApi,
  purityApi,
  specificationApi,
  inventoryApi,
  Branch,
  StockLocation,
  Material,
  Category,
  Purity,
  Specification,
  InventoryItem,
} from '@/lib/api';
import { BarcodeDisplay } from '@/components/BarcodeDisplay';

interface UploadedMediaItem {
  fileName: string;
  storagePath: string;
  contentType: string;
  fileSize: number;
  mediaType: number; // 1 = Image, 2 = Document
  isPrimary: boolean;
  sortOrder: number;
}

export default function NewInventoryItemPage() {
  const router = useRouter();
  const { user } = useAuth();
  const { activeBranchId } = useBranch();
  const canCreate = !!(user?.isGlobalAdmin || user?.permissions.includes('INVENTORY_CREATE'));

  // Stepper state
  const [currentStep, setCurrentStep] = useState<number>(1);
  const totalSteps = 8;

  // Master Data Lookups
  const [branches, setBranches] = useState<Branch[]>([]);
  const [locations, setLocations] = useState<StockLocation[]>([]);
  const [materials, setMaterials] = useState<Material[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [purities, setPurities] = useState<Purity[]>([]);
  const [specifications, setSpecifications] = useState<Specification[]>([]);

  // Form Fields
  // Step 1: Branch & Location
  const [branchId, setBranchId] = useState<string>('');
  const [stockLocationId, setStockLocationId] = useState<string>('');

  // Step 2: Item Information
  const [name, setName] = useState<string>('');
  const [itemCode, setItemCode] = useState<string>('');
  const [description, setDescription] = useState<string>('');
  const [pieceCount, setPieceCount] = useState<string>('1');

  // Step 3: Classification
  const [materialId, setMaterialId] = useState<string>('');
  const [categoryId, setCategoryId] = useState<string>('');
  const [purityId, setPurityId] = useState<string>('');

  // Step 4: Weight
  const [grossWeight, setGrossWeight] = useState<string>('');
  const [stoneWeight, setStoneWeight] = useState<string>('0.0000');

  // Step 5: Making Charge
  const [makingChargeMethod, setMakingChargeMethod] = useState<number>(1); // 1 = PER_GRAM, 2 = PER_ITEM
  const [makingChargeValue, setMakingChargeValue] = useState<string>('0.00');

  // Step 6: Specifications
  const [selectedSpecs, setSelectedSpecs] = useState<
    { specificationId: string; specificationOptionId?: string; customValue?: string }[]
  >([]);

  // Step 7: Media
  const [mediaList, setMediaList] = useState<UploadedMediaItem[]>([]);
  const [isUploadingMedia, setIsUploadingMedia] = useState<boolean>(false);

  // UX Feedback
  const [isLoadingLookups, setIsLoadingLookups] = useState<boolean>(true);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [createdItem, setCreatedItem] = useState<InventoryItem | null>(null);

  // Live Net Weight Calculation
  const parsedGross = parseFloat(grossWeight) || 0;
  const parsedStone = parseFloat(stoneWeight) || 0;
  const netWeightPreview = (Math.max(0, parsedGross - parsedStone)).toFixed(4);
  const hasWeightError = parsedStone > parsedGross && parsedGross > 0;

  // Initial Data Load
  useEffect(() => {
    async function fetchMasters() {
      try {
        setIsLoadingLookups(true);
        const [bList, mList, cList, pList, sList] = await Promise.all([
          branchApi.getBranches(true),
          materialApi.getMaterials(true),
          categoryApi.getCategories(true),
          purityApi.getPurities(true),
          specificationApi.getSpecifications(true),
        ]);

        setBranches(bList);
        setMaterials(mList);
        setCategories(cList);
        setPurities(pList);
        setSpecifications(sList);

        // Default Branch
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
        setFormError(err?.message || 'Failed to load master metadata');
      } finally {
        setIsLoadingLookups(false);
      }
    }

    if (canCreate) {
      fetchMasters();
    }
  }, [canCreate, activeBranchId]);

  // Handle Branch Change
  const handleBranchChange = async (newBranchId: string) => {
    setBranchId(newBranchId);
    setStockLocationId('');
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

  // Step Validation before Next
  const validateCurrentStep = (): boolean => {
    setFormError(null);

    switch (currentStep) {
      case 1:
        if (!branchId) {
          setFormError('Please select a branch.');
          return false;
        }
        if (!stockLocationId) {
          setFormError('Please select an active stock/display location.');
          return false;
        }
        return true;

      case 2:
        if (!name.trim()) {
          setFormError('Item Name is required.');
          return false;
        }
        const pc = parseInt(pieceCount, 10);
        if (isNaN(pc) || pc < 1) {
          setFormError('Piece Count must be an integer of at least 1.');
          return false;
        }
        return true;

      case 3:
        if (!materialId) {
          setFormError('Please select a material.');
          return false;
        }
        if (!categoryId) {
          setFormError('Please select a category.');
          return false;
        }
        if (!purityId) {
          setFormError('Please select a purity / karat.');
          return false;
        }
        return true;

      case 4:
        if (isNaN(parsedGross) || parsedGross <= 0) {
          setFormError('Gross weight must be a positive number greater than 0.');
          return false;
        }
        if (isNaN(parsedStone) || parsedStone < 0) {
          setFormError('Stone weight cannot be negative.');
          return false;
        }
        if (parsedStone > parsedGross) {
          setFormError(`Stone weight (${parsedStone}g) cannot exceed gross weight (${parsedGross}g).`);
          return false;
        }
        return true;

      case 5:
        const mc = parseFloat(makingChargeValue);
        if (isNaN(mc) || mc < 0) {
          setFormError('Making charge value must be a non-negative number.');
          return false;
        }
        return true;

      case 6:
        // Validate specifications
        for (const spec of specifications) {
          const selected = selectedSpecs.find((s) => s.specificationId === spec.id);
          const type = (spec.inputType || 'PREDEFINED').toUpperCase();

          if (selected) {
            if (type === 'PREDEFINED' && selected.customValue) {
              setFormError(`Specification "${spec.name}" uses predefined options only; custom value is disallowed.`);
              return false;
            }
            if (type === 'FREE_TEXT' && !selected.customValue?.trim()) {
              setFormError(`Specification "${spec.name}" requires a custom text value.`);
              return false;
            }
          }
        }
        return true;

      case 7:
        // Media step is optional; ready to review
        return true;

      default:
        return true;
    }
  };

  const handleNext = () => {
    if (validateCurrentStep()) {
      setCurrentStep((prev) => Math.min(totalSteps, prev + 1));
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  };

  const handleBack = () => {
    setFormError(null);
    setCurrentStep((prev) => Math.max(1, prev - 1));
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  // Media Upload Handler
  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;

    setIsUploadingMedia(true);
    setFormError(null);

    try {
      for (let i = 0; i < files.length; i++) {
        const file = files[i];
        const res = await inventoryApi.uploadMedia(file);
        setMediaList((prev) => [
          ...prev,
          {
            fileName: res.fileName,
            storagePath: res.storagePath,
            contentType: res.contentType,
            fileSize: res.fileSize,
            mediaType: res.mediaType,
            isPrimary: prev.length === 0, // First item default to primary
            sortOrder: prev.length + 1,
          },
        ]);
      }
    } catch (err: any) {
      setFormError(err?.message || 'File upload failed');
    } finally {
      setIsUploadingMedia(false);
      e.target.value = '';
    }
  };

  const handleSetPrimaryMedia = (index: number) => {
    setMediaList((prev) =>
      prev.map((item, idx) => ({
        ...item,
        isPrimary: idx === index,
      }))
    );
  };

  const handleRemoveMedia = (index: number) => {
    setMediaList((prev) => {
      const filtered = prev.filter((_, idx) => idx !== index);
      // Ensure at least one primary if images remain
      if (filtered.length > 0 && !filtered.some((m) => m.isPrimary)) {
        filtered[0].isPrimary = true;
      }
      return filtered;
    });
  };

  // Final Form Submission
  const handleSubmit = async () => {
    if (!validateCurrentStep()) return;

    setIsSubmitting(true);
    setFormError(null);

    try {
      const specsPayload = selectedSpecs
        .filter((s) => s.specificationOptionId || s.customValue)
        .map((s) => ({
          specificationId: s.specificationId,
          specificationOptionId: s.specificationOptionId || null,
          customValue: s.customValue || null,
        }));

      const mediaPayload = mediaList.map((m) => ({
        fileName: m.fileName,
        storagePath: m.storagePath,
        contentType: m.contentType,
        fileSize: m.fileSize,
        mediaType: m.mediaType,
        sortOrder: m.sortOrder,
        isPrimary: m.isPrimary,
      }));

      const payload = {
        branchId,
        stockLocationId,
        name: name.trim(),
        itemCode: itemCode.trim() || undefined,
        materialId,
        categoryId,
        purityId,
        grossWeight: parsedGross,
        stoneWeight: parsedStone,
        pieceCount: parseInt(pieceCount, 10) || 1,
        makingChargeMethod,
        makingChargeValue: parseFloat(makingChargeValue) || 0.0,
        description: description.trim() || undefined,
        isActive: true,
        specifications: specsPayload.length > 0 ? specsPayload : undefined,
        media: mediaPayload.length > 0 ? mediaPayload : undefined,
      };

      const result = await inventoryApi.createInventoryItem(payload);
      setCreatedItem(result);
    } catch (err: any) {
      setFormError(err?.message || 'Failed to create inventory item');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResetForNewItem = () => {
    setCreatedItem(null);
    setCurrentStep(1);
    setName('');
    setItemCode('');
    setDescription('');
    setPieceCount('1');
    setGrossWeight('');
    setStoneWeight('0.0000');
    setMakingChargeMethod(1);
    setMakingChargeValue('0.00');
    setSelectedSpecs([]);
    setMediaList([]);
    setFormError(null);
  };

  // Helper Labels for Review
  const selectedBranch = branches.find((b) => b.id === branchId);
  const selectedLocation = locations.find((l) => l.id === stockLocationId);
  const selectedMaterial = materials.find((m) => m.id === materialId);
  const selectedCategory = categories.find((c) => c.id === categoryId);
  const selectedPurity = purities.find((p) => p.id === purityId);

  // Permission Check
  if (!canCreate) {
    return (
      <AppLayout>
        <div className="max-w-2xl mx-auto my-12 bg-white rounded-xl shadow-md border border-red-200 p-8 text-center">
          <div className="w-16 h-16 bg-red-100 text-red-600 rounded-full flex items-center justify-center mx-auto mb-4">
            <svg className="w-8 h-8" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>
          <h2 className="text-xl font-bold text-gray-900 mb-2">Access Restricted</h2>
          <p className="text-gray-600 mb-6 text-sm">
            You do not possess the <code className="font-mono text-red-700 bg-red-50 px-1.5 py-0.5 rounded">INVENTORY_CREATE</code> permission required to create inventory items.
          </p>
          <Link
            href="/inventory"
            className="inline-flex items-center px-4 py-2 border border-transparent rounded-lg shadow-sm text-sm font-medium text-white bg-[#0D3826] hover:bg-[#09261a]"
          >
            Return to Inventory
          </Link>
        </div>
      </AppLayout>
    );
  }

  // Success Screen
  if (createdItem) {
    return (
      <AppLayout>
        <div className="max-w-3xl mx-auto my-6 bg-white rounded-2xl shadow-xl border border-emerald-100 overflow-hidden">
          {/* Header */}
          <div className="bg-gradient-to-r from-[#0D3826] to-[#165a3e] p-8 text-white text-center">
            <div className="w-16 h-16 bg-white/20 rounded-full flex items-center justify-center mx-auto mb-3 backdrop-blur-sm">
              <svg className="w-10 h-10 text-[#D4AF37]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
              </svg>
            </div>
            <h1 className="text-2xl font-bold tracking-tight">Inventory Item Created Successfully</h1>
            <p className="text-emerald-100 text-sm mt-1">
              Physical inventory identity assigned with immutable tag and machine-scannable barcode.
            </p>
          </div>

          {/* Details Card */}
          <div className="p-8 space-y-6">
            {/* Visual Barcode Display */}
            <div className="text-center py-4 bg-gray-50 border border-gray-200 rounded-xl">
              <span className="text-[11px] font-bold uppercase tracking-wider text-gray-500 block mb-2">
                Official Machine-Scannable Barcode (Code-128)
              </span>
              <BarcodeDisplay value={createdItem.barcode} width={320} height={85} />
            </div>

            {/* Key Grid */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              <div className="bg-[#FAF8F5] p-3.5 rounded-xl border border-[#D4AF37]/30">
                <span className="text-[10px] font-bold uppercase tracking-wider text-gray-500 block">Inventory Tag</span>
                <span className="font-mono font-bold text-base text-[#0D3826]">{createdItem.tagNumber}</span>
              </div>
              <div className="bg-[#FAF8F5] p-3.5 rounded-xl border border-[#D4AF37]/30">
                <span className="text-[10px] font-bold uppercase tracking-wider text-gray-500 block">Net Weight</span>
                <span className="font-bold text-base text-gray-900">{createdItem.netWeight.toFixed(4)} g</span>
              </div>
              <div className="bg-gray-50 p-3.5 rounded-xl border border-gray-200">
                <span className="text-[10px] font-bold uppercase tracking-wider text-gray-500 block">Operational Status</span>
                <span className="inline-block mt-0.5 px-2 py-0.5 text-xs font-bold rounded-full bg-emerald-100 text-emerald-800">
                  {createdItem.statusName}
                </span>
              </div>
              <div className="bg-gray-50 p-3.5 rounded-xl border border-gray-200">
                <span className="text-[10px] font-bold uppercase tracking-wider text-gray-500 block">Visibility Record</span>
                <span className="inline-block mt-0.5 px-2 py-0.5 text-xs font-bold rounded-full bg-blue-100 text-blue-800">
                  {createdItem.isActive ? 'ACTIVE' : 'INACTIVE'}
                </span>
              </div>
            </div>

            {/* Classification & Location Summary */}
            <div className="border border-gray-200 rounded-xl divide-y divide-gray-100 text-sm">
              <div className="p-3.5 flex justify-between bg-white">
                <span className="text-gray-500">Item Name</span>
                <span className="font-semibold text-gray-900">{createdItem.name}</span>
              </div>
              <div className="p-3.5 flex justify-between bg-white">
                <span className="text-gray-500">Branch & Location</span>
                <span className="font-semibold text-gray-900">
                  {createdItem.branchName} ({createdItem.branchCode}) &bull; {createdItem.locationName}
                </span>
              </div>
              <div className="p-3.5 flex justify-between bg-white">
                <span className="text-gray-500">Classification</span>
                <span className="font-semibold text-gray-900">
                  {createdItem.materialName} &bull; {createdItem.categoryName} &bull; {createdItem.purityName} ({createdItem.karatValue}K)
                </span>
              </div>
              <div className="p-3.5 flex justify-between bg-white">
                <span className="text-gray-500">Weights Breakdown</span>
                <span className="font-mono text-gray-900">
                  Gross: {createdItem.grossWeight.toFixed(4)}g | Stone: {createdItem.stoneWeight.toFixed(4)}g | Net: <strong>{createdItem.netWeight.toFixed(4)}g</strong>
                </span>
              </div>
              <div className="p-3.5 flex justify-between bg-white">
                <span className="text-gray-500">Making Charge</span>
                <span className="font-semibold text-gray-900">
                  Rs. {createdItem.makingChargeValue.toLocaleString('en-LK', { minimumFractionDigits: 2 })} ({createdItem.makingChargeMethodName})
                </span>
              </div>
            </div>

            {/* Actions */}
            <div className="flex flex-col sm:flex-row gap-3 pt-4 border-t border-gray-100">
              <button
                type="button"
                onClick={handleResetForNewItem}
                className="flex-1 py-3 px-4 rounded-xl font-bold text-white bg-[#0D3826] hover:bg-[#09261a] transition-colors shadow-sm text-center"
              >
                + Add Another Item
              </button>
              <Link
                href={`/inventory?search=${encodeURIComponent(createdItem.tagNumber)}`}
                className="flex-1 py-3 px-4 rounded-xl font-bold text-gray-800 bg-[#FAF8F5] border border-[#D4AF37]/50 hover:bg-[#f3ede1] transition-colors text-center"
              >
                View in Inventory List
              </Link>
              <Link
                href="/inventory"
                className="py-3 px-4 rounded-xl font-semibold text-gray-600 hover:text-gray-900 hover:bg-gray-100 transition-colors text-center"
              >
                Return
              </Link>
            </div>
          </div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout>
      <div className="max-w-4xl mx-auto space-y-6 pb-12">
        {/* Header Breadcrumbs */}
        <div className="flex items-center justify-between">
          <div>
            <div className="flex items-center space-x-2 text-xs text-gray-500 mb-1">
              <Link href="/inventory" className="hover:text-gray-800">Inventory</Link>
              <span>/</span>
              <span className="text-gray-800 font-semibold">Manual Creation</span>
            </div>
            <h1 className="text-2xl font-bold text-gray-900">Add Inventory Item</h1>
            <p className="text-xs text-gray-500">
              Guided manual entry for physical single jewellery inventory items.
            </p>
          </div>
          <Link
            href="/inventory"
            className="text-xs font-semibold text-gray-600 hover:text-gray-900 px-3 py-1.5 border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            Cancel
          </Link>
        </div>

        {/* Stepper Progress Bar */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
          <div className="flex items-center justify-between text-xs font-medium text-gray-500 mb-3">
            <span>Step {currentStep} of {totalSteps}</span>
            <span className="font-bold text-[#0D3826]">
              {currentStep === 1 && 'Branch & Display Location'}
              {currentStep === 2 && 'Item Information & Identity'}
              {currentStep === 3 && 'Metal & Category Classification'}
              {currentStep === 4 && 'Weights & Net Measurement'}
              {currentStep === 5 && 'Making Charge Configuration'}
              {currentStep === 6 && 'Attributes & Specifications'}
              {currentStep === 7 && 'Item Media & Certifications'}
              {currentStep === 8 && 'Review & Final Confirmation'}
            </span>
          </div>

          <div className="grid grid-cols-8 gap-1.5">
            {[1, 2, 3, 4, 5, 6, 7, 8].map((step) => {
              const isCompleted = step < currentStep;
              const isCurrent = step === currentStep;
              return (
                <div
                  key={step}
                  className={`h-2 rounded-full transition-all ${
                    isCompleted
                      ? 'bg-[#0D3826]'
                      : isCurrent
                      ? 'bg-[#D4AF37]'
                      : 'bg-gray-200'
                  }`}
                  title={`Step ${step}`}
                />
              );
            })}
          </div>
        </div>

        {/* Form Error Alert */}
        {formError && (
          <div className="p-4 rounded-xl bg-red-50 border border-red-200 flex items-start space-x-3 text-red-800 text-sm">
            <svg className="w-5 h-5 flex-shrink-0 text-red-600 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <div>
              <span className="font-bold block">Validation Notice</span>
              <span>{formError}</span>
            </div>
          </div>
        )}

        {/* Step Cards Container */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-200 p-6 sm:p-8">
          {isLoadingLookups ? (
            <div className="py-12 text-center text-gray-500">
              <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-[#0D3826] mb-2" />
              <p className="text-sm">Loading metadata catalogs...</p>
            </div>
          ) : (
            <form onSubmit={(e) => e.preventDefault()} className="space-y-6">
              {/* STEP 1: Branch & Location */}
              {currentStep === 1 && (
                <div className="space-y-5">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">Step 1 — Branch & Location</h2>
                    <p className="text-xs text-gray-500">
                      Select the physical branch and the initial stock or display location holding this item.
                    </p>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Branch <span className="text-red-500">*</span>
                      </label>
                      <select
                        value={branchId}
                        onChange={(e) => handleBranchChange(e.target.value)}
                        className="w-full px-3.5 py-2.5 text-sm border border-gray-300 rounded-xl bg-white focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                      >
                        {branches.map((b) => (
                          <option key={b.id} value={b.id}>
                            {b.name} ({b.code}) — {b.currencyCode}
                          </option>
                        ))}
                      </select>
                      <span className="text-[11px] text-gray-400 mt-1 block">
                        Changing branch refreshes branch-specific stock locations.
                      </span>
                    </div>

                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Stock / Display Location <span className="text-red-500">*</span>
                      </label>
                      <select
                        value={stockLocationId}
                        onChange={(e) => setStockLocationId(e.target.value)}
                        className="w-full px-3.5 py-2.5 text-sm border border-gray-300 rounded-xl bg-white focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                      >
                        {locations.length === 0 ? (
                          <option value="">No active locations configured for this branch</option>
                        ) : (
                          locations.map((l) => (
                            <option key={l.id} value={l.id}>
                              {l.name} ({l.code}) [{l.locationType}]
                            </option>
                          ))
                        )}
                      </select>
                      <span className="text-[11px] text-gray-400 mt-1 block">
                        Items can only belong to physical locations within their assigned branch.
                      </span>
                    </div>
                  </div>
                </div>
              )}

              {/* STEP 2: Item Information */}
              {currentStep === 2 && (
                <div className="space-y-5">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">Step 2 — Item Information</h2>
                    <p className="text-xs text-gray-500">
                      Define the human-readable identity and packaging details for this jewellery item.
                    </p>
                  </div>

                  <div className="space-y-4">
                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Item Name <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="text"
                        value={name}
                        onChange={(e) => setName(e.target.value)}
                        placeholder="e.g., 22K Traditional Sovereign Gold Bangle"
                        className="w-full px-3.5 py-2.5 text-sm border border-gray-300 rounded-xl focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                      />
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      <div>
                        <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                          Internal Item Code / SKU <span className="text-gray-400 font-normal">(Optional)</span>
                        </label>
                        <input
                          type="text"
                          value={itemCode}
                          onChange={(e) => setItemCode(e.target.value)}
                          placeholder="e.g., SKU-BGL-001"
                          className="w-full px-3.5 py-2.5 text-sm border border-gray-300 rounded-xl focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                        />
                        <span className="text-[11px] text-gray-400 mt-1 block">
                          If omitted, defaults to the generated jewellery tag number.
                        </span>
                      </div>

                      <div>
                        <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                          Piece Count <span className="text-red-500">*</span>
                        </label>
                        <input
                          type="number"
                          min="1"
                          step="1"
                          value={pieceCount}
                          onChange={(e) => setPieceCount(e.target.value)}
                          className="w-full px-3.5 py-2.5 text-sm border border-gray-300 rounded-xl focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                        />
                        <span className="text-[11px] text-gray-400 mt-1 block">
                          Individually tracked jewellery items default to 1 piece.
                        </span>
                      </div>
                    </div>

                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Description / Craftsmanship Notes <span className="text-gray-400 font-normal">(Optional)</span>
                      </label>
                      <textarea
                        rows={3}
                        value={description}
                        onChange={(e) => setDescription(e.target.value)}
                        placeholder="Details about craftsmanship, hallmark, motif, certifications..."
                        className="w-full px-3.5 py-2.5 text-sm border border-gray-300 rounded-xl focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                      />
                    </div>
                  </div>
                </div>
              )}

              {/* STEP 3: Classification */}
              {currentStep === 3 && (
                <div className="space-y-5">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">Step 3 — Classification</h2>
                    <p className="text-xs text-gray-500">
                      Classify the item by metal material, category, and purity standard.
                    </p>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-5">
                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Material <span className="text-red-500">*</span>
                      </label>
                      <select
                        value={materialId}
                        onChange={(e) => setMaterialId(e.target.value)}
                        className="w-full px-3.5 py-2.5 text-sm border border-gray-300 rounded-xl bg-white focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                      >
                        <option value="">Select Material...</option>
                        {materials.map((m) => (
                          <option key={m.id} value={m.id}>
                            {m.name} ({m.code})
                          </option>
                        ))}
                      </select>
                    </div>

                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Category <span className="text-red-500">*</span>
                      </label>
                      <select
                        value={categoryId}
                        onChange={(e) => setCategoryId(e.target.value)}
                        className="w-full px-3.5 py-2.5 text-sm border border-gray-300 rounded-xl bg-white focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                      >
                        <option value="">Select Category...</option>
                        {categories.map((c) => (
                          <option key={c.id} value={c.id}>
                            {c.name} ({c.code})
                          </option>
                        ))}
                      </select>
                    </div>

                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Purity / Karat <span className="text-red-500">*</span>
                      </label>
                      <select
                        value={purityId}
                        onChange={(e) => setPurityId(e.target.value)}
                        className="w-full px-3.5 py-2.5 text-sm border border-gray-300 rounded-xl bg-white focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                      >
                        <option value="">Select Purity...</option>
                        {purities.map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.name} ({p.karatValue}K - {p.purityPercentage}%)
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>
                </div>
              )}

              {/* STEP 4: Weight Entry */}
              {currentStep === 4 && (
                <div className="space-y-5">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">Step 4 — Weight & Measurement</h2>
                    <p className="text-xs text-gray-500">
                      Enter precision weights with four decimal places (0.0001g). Net weight is calculated strictly by backend.
                    </p>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-5">
                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Gross Weight (g) <span className="text-red-500">*</span>
                      </label>
                      <div className="relative">
                        <input
                          type="number"
                          step="0.0001"
                          min="0.0001"
                          value={grossWeight}
                          onChange={(e) => setGrossWeight(e.target.value)}
                          placeholder="0.0000"
                          className="w-full pr-8 pl-3.5 py-2.5 text-base font-semibold border border-gray-300 rounded-xl focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                        />
                        <span className="absolute right-3 top-3 text-xs font-bold text-gray-400">g</span>
                      </div>
                      <span className="text-[11px] text-gray-400 mt-1 block">Total weight on electronic scale</span>
                    </div>

                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Stone Weight (g) <span className="text-red-500">*</span>
                      </label>
                      <div className="relative">
                        <input
                          type="number"
                          step="0.0001"
                          min="0.0000"
                          value={stoneWeight}
                          onChange={(e) => setStoneWeight(e.target.value)}
                          placeholder="0.0000"
                          className="w-full pr-8 pl-3.5 py-2.5 text-base font-semibold border border-gray-300 rounded-xl focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                        />
                        <span className="absolute right-3 top-3 text-xs font-bold text-gray-400">g</span>
                      </div>
                      <span className="text-[11px] text-gray-400 mt-1 block">Gems, stones, or enamel tare</span>
                    </div>

                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Net Metal Weight (g)
                      </label>
                      <div className={`p-2.5 rounded-xl border flex items-center justify-between ${
                        hasWeightError ? 'bg-red-50 border-red-300 text-red-700' : 'bg-[#FAF8F5] border-[#D4AF37]/50 text-[#0D3826]'
                      }`}>
                        <span className="text-xs font-bold">Calculated Net:</span>
                        <span className="font-mono text-lg font-black">{netWeightPreview} g</span>
                      </div>
                      <span className="text-[11px] text-gray-400 mt-1 block">
                        Authoritative value is computed server-side: Gross - Stone.
                      </span>
                    </div>
                  </div>

                  {hasWeightError && (
                    <p className="text-xs text-red-600 font-semibold">
                      Stone weight cannot exceed gross weight. Please correct weight entries.
                    </p>
                  )}
                </div>
              )}

              {/* STEP 5: Making Charge */}
              {currentStep === 5 && (
                <div className="space-y-5">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">Step 5 — Making Charge Configuration</h2>
                    <p className="text-xs text-gray-500">
                      Configure craftsmanship and making charges in Sri Lankan Rupees (LKR).
                    </p>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Calculation Method <span className="text-red-500">*</span>
                      </label>
                      <div className="grid grid-cols-2 gap-3">
                        <button
                          type="button"
                          onClick={() => setMakingChargeMethod(1)}
                          className={`p-3 rounded-xl border text-left transition-all ${
                            makingChargeMethod === 1
                              ? 'border-[#0D3826] bg-[#0D3826]/5 text-[#0D3826] ring-2 ring-[#0D3826]'
                              : 'border-gray-200 bg-white text-gray-700 hover:bg-gray-50'
                          }`}
                        >
                          <span className="block font-bold text-sm">Per Gram</span>
                          <span className="text-[11px] text-gray-500">Applied per net gram</span>
                        </button>

                        <button
                          type="button"
                          onClick={() => setMakingChargeMethod(2)}
                          className={`p-3 rounded-xl border text-left transition-all ${
                            makingChargeMethod === 2
                              ? 'border-[#0D3826] bg-[#0D3826]/5 text-[#0D3826] ring-2 ring-[#0D3826]'
                              : 'border-gray-200 bg-white text-gray-700 hover:bg-gray-50'
                          }`}
                        >
                          <span className="block font-bold text-sm">Per Item</span>
                          <span className="text-[11px] text-gray-500">Fixed charge per piece</span>
                        </button>
                      </div>
                    </div>

                    <div>
                      <label className="block text-xs font-bold uppercase tracking-wider text-gray-700 mb-1.5">
                        Making Charge Value (LKR) <span className="text-red-500">*</span>
                      </label>
                      <div className="relative">
                        <span className="absolute left-3.5 top-3 text-xs font-bold text-gray-500">Rs.</span>
                        <input
                          type="number"
                          step="0.01"
                          min="0.00"
                          value={makingChargeValue}
                          onChange={(e) => setMakingChargeValue(e.target.value)}
                          placeholder="0.00"
                          className="w-full pl-10 pr-3.5 py-2.5 text-base font-semibold border border-gray-300 rounded-xl focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
                        />
                      </div>
                      <span className="text-[11px] text-gray-400 mt-1 block">
                        Example: Rs. 2,500.00 per gram or Rs. 15,000.00 fixed
                      </span>
                    </div>
                  </div>

                  <div className="p-3.5 rounded-xl bg-amber-50 border border-amber-200 text-amber-800 text-xs flex items-center space-x-2">
                    <svg className="w-4 h-4 text-amber-600 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    <span>
                      Gold selling rates and VAT will be calculated during billing/POS based on daily market boards (Module 9). No pricing is estimated here.
                    </span>
                  </div>
                </div>
              )}

              {/* STEP 6: Specifications */}
              {currentStep === 6 && (
                <div className="space-y-5">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">Step 6 — Attributes & Specifications</h2>
                    <p className="text-xs text-gray-500">
                      Configure dynamic attributes based on master settings (PREDEFINED options, FREE_TEXT, or HYBRID).
                    </p>
                  </div>

                  {specifications.length === 0 ? (
                    <div className="p-6 text-center text-gray-400 bg-gray-50 rounded-xl border border-dashed border-gray-200">
                      No optional specifications configured in master catalogs.
                    </div>
                  ) : (
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                      {specifications.map((spec) => {
                        const currentSelection = selectedSpecs.find((s) => s.specificationId === spec.id);
                        const isFreeText = spec.inputType === 'FREE_TEXT';
                        const isHybrid = spec.inputType === 'HYBRID';

                        return (
                          <div key={spec.id} className="p-4 border rounded-xl bg-gray-50 space-y-2 border-gray-200">
                            <div className="flex justify-between items-center">
                              <label className="block font-bold text-xs text-gray-800">{spec.name}</label>
                              <span className="text-[10px] uppercase font-mono px-1.5 py-0.5 rounded bg-gray-200 text-gray-700">
                                {spec.inputType || 'PREDEFINED'}
                              </span>
                            </div>

                            {!isFreeText && (
                              <select
                                value={currentSelection?.specificationOptionId || ''}
                                onChange={(e) => {
                                  const val = e.target.value;
                                  setSelectedSpecs((prev) => {
                                    const filtered = prev.filter((s) => s.specificationId !== spec.id);
                                    if (val) {
                                      return [
                                        ...filtered,
                                        {
                                          specificationId: spec.id,
                                          specificationOptionId: val,
                                          customValue: isHybrid ? currentSelection?.customValue : undefined,
                                        },
                                      ];
                                    }
                                    if (isHybrid && currentSelection?.customValue) {
                                      return [
                                        ...filtered,
                                        {
                                          specificationId: spec.id,
                                          specificationOptionId: undefined,
                                          customValue: currentSelection.customValue,
                                        },
                                      ];
                                    }
                                    return filtered;
                                  });
                                }}
                                className="w-full px-3 py-2 text-xs border border-gray-300 rounded-lg bg-white"
                              >
                                <option value="">Select Option (Not Specified)</option>
                                {spec.options.map((opt) => (
                                  <option key={opt.id} value={opt.id}>
                                    {opt.value} ({opt.code})
                                  </option>
                                ))}
                              </select>
                            )}

                            {(isFreeText || isHybrid) && (
                              <input
                                type="text"
                                placeholder={isFreeText ? "Enter custom specification..." : "Optional custom detail..."}
                                value={currentSelection?.customValue || ''}
                                onChange={(e) => {
                                  const val = e.target.value;
                                  setSelectedSpecs((prev) => {
                                    const filtered = prev.filter((s) => s.specificationId !== spec.id);
                                    if (val) {
                                      return [
                                        ...filtered,
                                        {
                                          specificationId: spec.id,
                                          specificationOptionId: isFreeText ? undefined : currentSelection?.specificationOptionId,
                                          customValue: val,
                                        },
                                      ];
                                    }
                                    if (currentSelection?.specificationOptionId) {
                                      return [
                                        ...filtered,
                                        {
                                          specificationId: spec.id,
                                          specificationOptionId: currentSelection.specificationOptionId,
                                          customValue: undefined,
                                        },
                                      ];
                                    }
                                    return filtered;
                                  });
                                }}
                                className="w-full px-3 py-2 text-xs border border-gray-300 rounded-lg bg-white"
                              />
                            )}
                          </div>
                        );
                      })}
                    </div>
                  )}
                </div>
              )}

              {/* STEP 7: Media Upload */}
              {currentStep === 7 && (
                <div className="space-y-5">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">Step 7 — Media & Certificates</h2>
                    <p className="text-xs text-gray-500">
                      Upload high-resolution jewellery photographs (JPEG, PNG, WebP up to 5MB) or appraisal certificates (PDF up to 10MB).
                    </p>
                  </div>

                  {/* Upload Box */}
                  <div className="border-2 border-dashed border-gray-300 rounded-2xl p-6 text-center hover:border-[#0D3826] transition-colors bg-gray-50">
                    <input
                      type="file"
                      id="media-upload-input"
                      multiple
                      accept="image/jpeg,image/png,image/webp,application/pdf"
                      onChange={handleFileUpload}
                      className="hidden"
                      disabled={isUploadingMedia}
                    />
                    <label
                      htmlFor="media-upload-input"
                      className="cursor-pointer inline-flex flex-col items-center justify-center space-y-2"
                    >
                      <div className="w-12 h-12 bg-white rounded-full shadow-sm flex items-center justify-center text-[#0D3826]">
                        {isUploadingMedia ? (
                          <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-[#0D3826]" />
                        ) : (
                          <svg className="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16l4.586-4.586a2 2 0 012.828 0L16 16m-2-2l1.586-1.586a2 2 0 012.828 0L20 14m-6-6h.01M6 20h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z" />
                          </svg>
                        )}
                      </div>
                      <span className="text-sm font-semibold text-gray-800">
                        {isUploadingMedia ? 'Uploading media...' : 'Click to select files or drag and drop'}
                      </span>
                      <span className="text-xs text-gray-400">
                        Images (max 5MB) & Documents / Certificates (max 10MB)
                      </span>
                    </label>
                  </div>

                  {/* Uploaded Media List */}
                  {mediaList.length > 0 && (
                    <div className="space-y-3 pt-2">
                      <span className="text-xs font-bold uppercase tracking-wider text-gray-700 block">
                        Attached Files ({mediaList.length})
                      </span>
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                        {mediaList.map((m, idx) => (
                          <div
                            key={idx}
                            className={`p-3.5 rounded-xl border flex items-center justify-between ${
                              m.isPrimary ? 'border-[#D4AF37] bg-[#FAF8F5]' : 'border-gray-200 bg-white'
                            }`}
                          >
                            <div className="flex items-center space-x-3 overflow-hidden">
                              <span className="p-2 rounded-lg bg-gray-100 text-gray-600 text-xs font-bold uppercase">
                                {m.mediaType === 1 ? 'IMG' : 'DOC'}
                              </span>
                              <div className="truncate">
                                <p className="text-xs font-semibold text-gray-900 truncate">{m.fileName}</p>
                                <p className="text-[10px] text-gray-400">
                                  {(m.fileSize / 1024).toFixed(1)} KB &bull; {m.contentType}
                                </p>
                              </div>
                            </div>

                            <div className="flex items-center space-x-2 flex-shrink-0">
                              {m.mediaType === 1 && (
                                <button
                                  type="button"
                                  onClick={() => handleSetPrimaryMedia(idx)}
                                  className={`px-2 py-1 text-[11px] rounded font-semibold transition-colors ${
                                    m.isPrimary
                                      ? 'bg-[#D4AF37] text-white'
                                      : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                                  }`}
                                >
                                  {m.isPrimary ? 'Primary' : 'Set Primary'}
                                </button>
                              )}
                              <button
                                type="button"
                                onClick={() => handleRemoveMedia(idx)}
                                className="text-red-500 hover:text-red-700 p-1"
                                title="Remove file"
                              >
                                &times;
                              </button>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              )}

              {/* STEP 8: Review & Submit */}
              {currentStep === 8 && (
                <div className="space-y-6">
                  <div>
                    <h2 className="text-lg font-bold text-gray-900">Step 8 — Review & Final Confirmation</h2>
                    <p className="text-xs text-gray-500">
                      Verify all physical parameters before saving. Tag and barcode will be automatically generated.
                    </p>
                  </div>

                  <div className="border border-gray-200 rounded-2xl divide-y divide-gray-100 overflow-hidden text-sm">
                    {/* Section 1 */}
                    <div className="p-4 bg-white flex justify-between items-center">
                      <div>
                        <span className="text-[11px] font-bold uppercase tracking-wider text-gray-400 block">Branch & Location</span>
                        <p className="font-semibold text-gray-900">{selectedBranch?.name} ({selectedBranch?.code})</p>
                        <p className="text-xs text-gray-600">{selectedLocation?.name} [{selectedLocation?.locationType}]</p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setCurrentStep(1)}
                        className="text-xs font-semibold text-[#0D3826] hover:underline"
                      >
                        Edit
                      </button>
                    </div>

                    {/* Section 2 */}
                    <div className="p-4 bg-white flex justify-between items-center">
                      <div>
                        <span className="text-[11px] font-bold uppercase tracking-wider text-gray-400 block">Item Identity</span>
                        <p className="font-semibold text-gray-900">{name}</p>
                        <p className="text-xs text-gray-600">
                          SKU: {itemCode || 'Auto Tag'} &bull; Pieces: {pieceCount}
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setCurrentStep(2)}
                        className="text-xs font-semibold text-[#0D3826] hover:underline"
                      >
                        Edit
                      </button>
                    </div>

                    {/* Section 3 */}
                    <div className="p-4 bg-white flex justify-between items-center">
                      <div>
                        <span className="text-[11px] font-bold uppercase tracking-wider text-gray-400 block">Classification</span>
                        <p className="font-semibold text-gray-900">
                          {selectedMaterial?.name} &bull; {selectedCategory?.name} &bull; {selectedPurity?.name} ({selectedPurity?.karatValue}K)
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setCurrentStep(3)}
                        className="text-xs font-semibold text-[#0D3826] hover:underline"
                      >
                        Edit
                      </button>
                    </div>

                    {/* Section 4 */}
                    <div className="p-4 bg-white flex justify-between items-center">
                      <div>
                        <span className="text-[11px] font-bold uppercase tracking-wider text-gray-400 block">Weights</span>
                        <p className="font-mono text-sm text-gray-900 font-semibold">
                          Gross: {parsedGross.toFixed(4)}g | Stone: {parsedStone.toFixed(4)}g | Net:{' '}
                          <span className="text-[#0D3826] font-bold">{netWeightPreview}g</span>
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setCurrentStep(4)}
                        className="text-xs font-semibold text-[#0D3826] hover:underline"
                      >
                        Edit
                      </button>
                    </div>

                    {/* Section 5 */}
                    <div className="p-4 bg-white flex justify-between items-center">
                      <div>
                        <span className="text-[11px] font-bold uppercase tracking-wider text-gray-400 block">Making Charge</span>
                        <p className="font-semibold text-gray-900">
                          Rs. {parseFloat(makingChargeValue || '0').toLocaleString('en-LK', { minimumFractionDigits: 2 })}{' '}
                          <span className="text-xs font-normal text-gray-500">
                            ({makingChargeMethod === 1 ? 'Per Gram' : 'Per Item'})
                          </span>
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setCurrentStep(5)}
                        className="text-xs font-semibold text-[#0D3826] hover:underline"
                      >
                        Edit
                      </button>
                    </div>

                    {/* Section 6 & 7 */}
                    <div className="p-4 bg-white flex justify-between items-center">
                      <div>
                        <span className="text-[11px] font-bold uppercase tracking-wider text-gray-400 block">Specs & Media</span>
                        <p className="text-xs text-gray-600">
                          Specifications configured: {selectedSpecs.filter((s) => s.specificationOptionId || s.customValue).length} &bull; Attached media: {mediaList.length} files
                        </p>
                      </div>
                      <button
                        type="button"
                        onClick={() => setCurrentStep(6)}
                        className="text-xs font-semibold text-[#0D3826] hover:underline"
                      >
                        Edit
                      </button>
                    </div>
                  </div>
                </div>
              )}

              {/* Bottom Stepper Navigation */}
              <div className="flex items-center justify-between pt-6 border-t border-gray-100">
                <button
                  type="button"
                  onClick={handleBack}
                  disabled={currentStep === 1 || isSubmitting}
                  className="px-5 py-2.5 rounded-xl border border-gray-300 text-sm font-semibold text-gray-700 hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  &larr; Back
                </button>

                {currentStep < totalSteps ? (
                  <button
                    type="button"
                    onClick={handleNext}
                    className="px-6 py-2.5 rounded-xl bg-[#0D3826] text-white text-sm font-bold hover:bg-[#09261a] transition-colors shadow-sm"
                  >
                    Continue &rarr;
                  </button>
                ) : (
                  <button
                    type="button"
                    onClick={handleSubmit}
                    disabled={isSubmitting}
                    className="px-7 py-3 rounded-xl bg-[#0D3826] text-[#D4AF37] font-bold text-sm hover:bg-[#09261a] transition-all shadow-md flex items-center space-x-2 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {isSubmitting ? (
                      <>
                        <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-[#D4AF37]" />
                        <span>Creating Inventory Item...</span>
                      </>
                    ) : (
                      <>
                        <span>Confirm & Create Item</span>
                        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                        </svg>
                      </>
                    )}
                  </button>
                )}
              </div>
            </form>
          )}
        </div>
      </div>
    </AppLayout>
  );
}
