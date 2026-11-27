'use client';

import React, { useState, useEffect } from 'react';
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
import { PageHeader } from '@/components/ui/PageHeader';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Textarea } from '@/components/ui/Textarea';
import { Badge } from '@/components/ui/Badge';
import {
  ArrowLeft,
  CheckCircle2,
  AlertTriangle,
  UploadCloud,
  X,
  Plus,
  Gem,
  Scale,
  MapPin,
} from 'lucide-react';

interface UploadedMediaItem {
  fileName: string;
  storagePath: string;
  contentType: string;
  fileSize: number;
  mediaType: number; // 1 = Image, 2 = Document
  isPrimary: boolean;
  sortOrder: number;
}

function generateIdempotencyKey() {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  return 'idemp-' + Date.now() + '-' + Math.random().toString(36).substring(2, 9);
}

export default function NewInventoryItemPage() {
  const { user } = useAuth();
  const { activeBranchId } = useBranch();
  const canCreate = !!(user?.isGlobalAdmin || user?.permissions.includes('INVENTORY_CREATE'));

  // Master Data Lookups
  const [branches, setBranches] = useState<Branch[]>([]);
  const [locations, setLocations] = useState<StockLocation[]>([]);
  const [materials, setMaterials] = useState<Material[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [purities, setPurities] = useState<Purity[]>([]);
  const [specifications, setSpecifications] = useState<Specification[]>([]);

  // Form Fields
  const [branchId, setBranchId] = useState<string>('');
  const [stockLocationId, setStockLocationId] = useState<string>('');
  const [name, setName] = useState<string>('');
  const [itemCode, setItemCode] = useState<string>('');
  const [description, setDescription] = useState<string>('');
  const [pieceCount, setPieceCount] = useState<string>('1');
  const [materialId, setMaterialId] = useState<string>('');
  const [categoryId, setCategoryId] = useState<string>('');
  const [purityId, setPurityId] = useState<string>('');
  const [grossWeight, setGrossWeight] = useState<string>('');
  const [stoneWeight, setStoneWeight] = useState<string>('0');
  const [makingChargeMethod, setMakingChargeMethod] = useState<number>(1); // 1 = PER_GRAM, 2 = PER_ITEM
  const [makingChargeValue, setMakingChargeValue] = useState<string>('0.00');
  const [selectedSpecs, setSelectedSpecs] = useState<
    { specificationId: string; specificationOptionId?: string; customValue?: string }[]
  >([]);
  const [mediaList, setMediaList] = useState<UploadedMediaItem[]>([]);
  const [isUploadingMedia, setIsUploadingMedia] = useState<boolean>(false);

  // UX State
  const [isLoadingLookups, setIsLoadingLookups] = useState<boolean>(true);
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [createdItem, setCreatedItem] = useState<InventoryItem | null>(null);
  const [idempotencyKey, setIdempotencyKey] = useState<string>(generateIdempotencyKey);

  const activeBranch = branches.find((b) => b.id === (branchId || activeBranchId));
  const currency = activeBranch?.currencyCode || 'LKR';

  // Live Weight Calculations
  const parsedGross = parseFloat(grossWeight) || 0;
  const parsedStone = parseFloat(stoneWeight) || 0;
  const netWeightPreview = Math.max(0, parsedGross - parsedStone).toFixed(4);
  const hasWeightError = parsedStone > parsedGross && parsedGross > 0;

  // Load Master Data
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
        setFormError(err?.message || 'Failed to load master metadata.');
      } finally {
        setIsLoadingLookups(false);
      }
    }

    if (canCreate) {
      fetchMasters();
    }
  }, [canCreate, activeBranchId]);

  const handleBranchChange = async (newBranchId: string) => {
    setBranchId(newBranchId);
    setStockLocationId('');
    if (!newBranchId) {
      setLocations([]);
      return;
    }
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

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;

    setIsUploadingMedia(true);
    setFormError(null);

    try {
      for (let i = 0; i < files.length; i++) {
        const res = await inventoryApi.uploadMedia(files[i]);
        setMediaList((prev) => [
          ...prev,
          {
            fileName: res.fileName,
            storagePath: res.storagePath,
            contentType: res.contentType,
            fileSize: res.fileSize,
            mediaType: res.mediaType,
            isPrimary: prev.length === 0,
            sortOrder: prev.length + 1,
          },
        ]);
      }
    } catch (err: any) {
      setFormError(err?.message || 'File upload failed.');
    } finally {
      setIsUploadingMedia(false);
      e.target.value = '';
    }
  };

  const handleSetPrimaryMedia = (index: number) => {
    setMediaList((prev) => prev.map((item, idx) => ({ ...item, isPrimary: idx === index })));
  };

  const handleRemoveMedia = (index: number) => {
    setMediaList((prev) => {
      const filtered = prev.filter((_, idx) => idx !== index);
      if (filtered.length > 0 && !filtered.some((m) => m.isPrimary)) {
        filtered[0].isPrimary = true;
      }
      return filtered;
    });
  };

  const validateAll = (): boolean => {
    if (!name.trim()) {
      setFormError('Item Name is required.');
      return false;
    }
    const pc = parseInt(pieceCount, 10);
    if (isNaN(pc) || pc < 1) {
      setFormError('Piece Count must be at least 1.');
      return false;
    }
    if (!materialId || !categoryId || !purityId) {
      setFormError('Material, Category, and Purity are all required.');
      return false;
    }
    if (isNaN(parsedGross) || parsedGross <= 0) {
      setFormError('Gross Weight must be greater than 0.');
      return false;
    }
    if (isNaN(parsedStone) || parsedStone < 0) {
      setFormError('Stone Weight cannot be negative.');
      return false;
    }
    if (parsedStone > parsedGross) {
      setFormError(`Stone Weight (${parsedStone}g) cannot exceed Gross Weight (${parsedGross}g).`);
      return false;
    }
    const mc = parseFloat(makingChargeValue);
    if (isNaN(mc) || mc < 0) {
      setFormError('Making Charge must be 0 or greater.');
      return false;
    }
    if (!branchId || !stockLocationId) {
      setFormError('Target Branch and Stock Location are required.');
      return false;
    }
    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);
    if (!validateAll()) return;

    setIsSubmitting(true);
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

      const result = await inventoryApi.createInventoryItem(
        {
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
        },
        idempotencyKey
      );
      setCreatedItem(result);
    } catch (err: any) {
      setFormError(err?.message || 'Failed to create inventory item.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleReset = () => {
    setCreatedItem(null);
    setName('');
    setItemCode('');
    setDescription('');
    setPieceCount('1');
    setGrossWeight('');
    setStoneWeight('0');
    setMakingChargeMethod(1);
    setMakingChargeValue('0.00');
    setSelectedSpecs([]);
    setMediaList([]);
    setFormError(null);
    setIdempotencyKey(generateIdempotencyKey());
  };

  const selectedBranch = branches.find((b) => b.id === branchId);
  const selectedLocation = locations.find((l) => l.id === stockLocationId);
  const selectedMaterial = materials.find((m) => m.id === materialId);
  const selectedCategory = categories.find((c) => c.id === categoryId);
  const selectedPurity = purities.find((p) => p.id === purityId);

  if (!canCreate) {
    return (
      <AppLayout>
        <div className="max-w-xl mx-auto mt-10 bg-white border border-rose-200 rounded-xl p-6 text-center shadow-xs">
          <AlertTriangle className="w-8 h-8 text-rose-600 mx-auto mb-2" />
          <h2 className="text-base font-bold text-gray-900">Access Denied</h2>
          <p className="text-xs text-gray-600 mt-1">
            You need <code className="font-mono text-rose-700">INVENTORY_CREATE</code> permission to add items.
          </p>
          <div className="mt-4">
            <Link href="/inventory">
              <Button variant="secondary" size="sm">Return to Inventory</Button>
            </Link>
          </div>
        </div>
      </AppLayout>
    );
  }

  if (createdItem) {
    return (
      <AppLayout>
        <div className="max-w-3xl mx-auto my-4 bg-white rounded-xl shadow-sm border border-[#E2E6E3] overflow-hidden">
          <div className="bg-[#123B32] px-6 py-6 text-white flex items-center gap-4">
            <div className="w-11 h-11 rounded-full bg-white/15 flex items-center justify-center flex-shrink-0">
              <CheckCircle2 className="w-6 h-6 text-[#B89A60]" />
            </div>
            <div>
              <h1 className="text-lg font-bold">Jewellery Item Created</h1>
              <p className="text-xs text-emerald-100 mt-0.5">
                Unique tag and barcode assigned to branch inventory.
              </p>
            </div>
          </div>

          <div className="p-6 space-y-5">
            <div className="text-center py-4 bg-[#F8F7F4] border border-[#E2E6E3] rounded-xl">
              <span className="text-[10px] font-bold uppercase tracking-wider text-[#69736E] block mb-2">
                Scannable Barcode (Code-128)
              </span>
              <BarcodeDisplay value={createdItem.barcode} width={300} height={75} />
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              <div className="bg-[#F8F7F4] p-3 rounded-lg border border-[#B89A60]/40">
                <span className="text-[10px] font-bold uppercase text-[#69736E] block">Tag Number</span>
                <span className="font-mono font-bold text-sm text-[#123B32]">{createdItem.tagNumber}</span>
              </div>
              <div className="bg-[#F8F7F4] p-3 rounded-lg border border-[#B89A60]/40">
                <span className="text-[10px] font-bold uppercase text-[#69736E] block">Net Weight</span>
                <span className="font-mono font-bold text-sm text-[#202824]">{createdItem.netWeight.toFixed(4)} g</span>
              </div>
              <div className="bg-white p-3 rounded-lg border border-[#E2E6E3]">
                <span className="text-[10px] font-bold uppercase text-[#69736E] block">Status</span>
                <Badge variant="success" size="sm" className="mt-0.5">{createdItem.statusName}</Badge>
              </div>
              <div className="bg-white p-3 rounded-lg border border-[#E2E6E3]">
                <span className="text-[10px] font-bold uppercase text-[#69736E] block">Location</span>
                <span className="font-semibold text-xs text-[#202824]">{createdItem.locationName}</span>
              </div>
            </div>

            <div className="border border-[#E2E6E3] rounded-lg divide-y divide-[#E2E6E3] text-xs">
              <div className="px-4 py-2.5 flex justify-between">
                <span className="text-[#69736E]">Item Name</span>
                <span className="font-semibold text-[#202824]">{createdItem.name}</span>
              </div>
              <div className="px-4 py-2.5 flex justify-between">
                <span className="text-[#69736E]">Classification</span>
                <span className="font-semibold text-[#202824]">
                  {createdItem.materialName} • {createdItem.categoryName} • {createdItem.purityName}
                </span>
              </div>
              <div className="px-4 py-2.5 flex justify-between">
                <span className="text-[#69736E]">Weights</span>
                <span className="font-mono text-[#202824]">
                  Gross {createdItem.grossWeight.toFixed(4)}g | Stone {createdItem.stoneWeight.toFixed(4)}g | Net{' '}
                  <strong>{createdItem.netWeight.toFixed(4)}g</strong>
                </span>
              </div>
              <div className="px-4 py-2.5 flex justify-between">
                <span className="text-[#69736E]">Making Charge</span>
                <span className="font-mono font-semibold text-[#202824]">
                  {currency} {createdItem.makingChargeValue.toLocaleString('en-LK', { minimumFractionDigits: 2 })} (
                  {createdItem.makingChargeMethodName})
                </span>
              </div>
            </div>

            <div className="flex flex-wrap justify-between gap-3 pt-2">
              <Link href="/inventory">
                <Button variant="secondary" size="md" leftIcon={<ArrowLeft className="w-4 h-4" />}>
                  Back to Inventory
                </Button>
              </Link>
              <Button variant="primary" size="md" onClick={handleReset} leftIcon={<Plus className="w-4 h-4" />}>
                Add Another Item
              </Button>
            </div>
          </div>
        </div>
      </AppLayout>
    );
  }

  return (
    <AppLayout>
      <div className="max-w-6xl mx-auto space-y-5">
        <PageHeader
          title="Add Jewellery Item"
          description="Register a single tagged jewellery piece into branch stock."
          actions={
            <Link href="/inventory">
              <Button variant="secondary" size="sm" leftIcon={<ArrowLeft className="w-3.5 h-3.5" />}>
                Back to Inventory
              </Button>
            </Link>
          }
        />

        {formError && (
          <div className="bg-rose-50 border border-rose-200 text-rose-800 px-4 py-3 rounded-xl text-xs flex items-center justify-between">
            <div className="flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 text-rose-600 flex-shrink-0" />
              <span>{formError}</span>
            </div>
            <button onClick={() => setFormError(null)} className="text-rose-500 hover:text-rose-800">
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {isLoadingLookups ? (
          <div className="bg-white border border-[#E2E6E3] rounded-xl p-12 text-center text-xs text-[#69736E]">
            Loading materials, categories, purities, and branch locations...
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
            {/* Left Column: Form Sections */}
            <div className="lg:col-span-8 space-y-5">
              {/* Section 1: Item & Classification */}
              <div className="bg-white border border-[#E2E6E3] rounded-xl p-5 shadow-xs space-y-4">
                <div className="flex items-center gap-2 pb-2.5 border-b border-[#E2E6E3]">
                  <Gem className="w-4 h-4 text-[#123B32]" />
                  <h2 className="text-xs font-bold uppercase tracking-wider text-[#123B32]">
                    1. Item & Classification
                  </h2>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-12 gap-4">
                  <div className="sm:col-span-6">
                    <Input
                      label="Item Name"
                      requiredIndicator
                      required
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      placeholder="e.g. 22K Traditional Gold Bangle"
                    />
                  </div>
                  <div className="sm:col-span-4">
                    <Input
                      label="Item Code"
                      required={false}
                      value={itemCode}
                      onChange={(e) => setItemCode(e.target.value)}
                      placeholder="e.g. BGL-2026-01"
                    />
                  </div>
                  <div className="sm:col-span-2">
                    <Input
                      label="Pieces"
                      requiredIndicator
                      required
                      type="number"
                      min={1}
                      value={pieceCount}
                      onChange={(e) => setPieceCount(e.target.value)}
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <Select
                    label="Material"
                    requiredIndicator
                    required
                    value={materialId}
                    onChange={(e) => setMaterialId(e.target.value)}
                    options={[
                      { value: '', label: 'Select material...' },
                      ...materials.map((m) => ({ value: m.id, label: `${m.name} (${m.code})` })),
                    ]}
                  />
                  <Select
                    label="Category"
                    requiredIndicator
                    required
                    value={categoryId}
                    onChange={(e) => setCategoryId(e.target.value)}
                    options={[
                      { value: '', label: 'Select category...' },
                      ...categories.map((c) => ({ value: c.id, label: `${c.name} (${c.code})` })),
                    ]}
                  />
                  <Select
                    label="Purity / Karat"
                    requiredIndicator
                    required
                    value={purityId}
                    onChange={(e) => setPurityId(e.target.value)}
                    options={[
                      { value: '', label: 'Select purity...' },
                      ...purities.map((p) => ({
                        value: p.id,
                        label: `${p.name} (${p.purityPercentage}%)`,
                      })),
                    ]}
                  />
                </div>

                <Textarea
                  label="Description"
                  required={false}
                  rows={2}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Optional design notes, craftsmanship details, or hallmark remarks"
                />
              </div>

              {/* Section 2: Weight & Making Charge */}
              <div className="bg-white border border-[#E2E6E3] rounded-xl p-5 shadow-xs space-y-4">
                <div className="flex items-center justify-between pb-2.5 border-b border-[#E2E6E3]">
                  <div className="flex items-center gap-2">
                    <Scale className="w-4 h-4 text-[#123B32]" />
                    <h2 className="text-xs font-bold uppercase tracking-wider text-[#123B32]">
                      2. Weight & Making Charge
                    </h2>
                  </div>
                  <span className="text-[11px] font-mono font-semibold text-[#123B32] bg-emerald-50 border border-emerald-200 px-2.5 py-0.5 rounded-md">
                    Net Weight: {netWeightPreview} g
                  </span>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <Input
                    label="Gross Weight"
                    unit="g"
                    requiredIndicator
                    required
                    type="number"
                    step="0.0001"
                    min="0.0001"
                    value={grossWeight}
                    onChange={(e) => setGrossWeight(e.target.value)}
                    placeholder="0.0000"
                  />
                  <Input
                    label="Stone Weight"
                    unit="g"
                    required={false}
                    type="number"
                    step="0.0001"
                    min="0"
                    value={stoneWeight}
                    onChange={(e) => setStoneWeight(e.target.value)}
                    error={hasWeightError ? 'Cannot exceed Gross Weight' : undefined}
                    placeholder="0.0000"
                  />
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 items-end">
                  <div>
                    <span className="block text-xs font-semibold text-[#202824] uppercase tracking-wider mb-1.5">
                      Making Charge Method <span className="text-rose-600">*</span>
                    </span>
                    <div className="grid grid-cols-2 gap-2">
                      {[
                        { id: 1, label: 'Per Gram', sub: 'Rate × Net g' },
                        { id: 2, label: 'Per Item', sub: 'Fixed amount' },
                      ].map((m) => (
                        <button
                          key={m.id}
                          type="button"
                          onClick={() => setMakingChargeMethod(m.id)}
                          className={`px-3 py-2 rounded-lg border text-left transition ${
                            makingChargeMethod === m.id
                              ? 'border-[#123B32] bg-[#123B32]/5 text-[#123B32] ring-1 ring-[#123B32]'
                              : 'border-[#E2E6E3] bg-white text-[#202824] hover:bg-[#F8F7F4]'
                          }`}
                        >
                          <span className="block text-xs font-bold">{m.label}</span>
                          <span className="block text-[10px] text-[#69736E]">{m.sub}</span>
                        </button>
                      ))}
                    </div>
                  </div>

                  <Input
                    label="Making Charge Value"
                    unit={currency}
                    requiredIndicator
                    required
                    type="number"
                    step="0.01"
                    min="0"
                    value={makingChargeValue}
                    onChange={(e) => setMakingChargeValue(e.target.value)}
                    placeholder="0.00"
                  />
                </div>
              </div>

              {/* Section 3: Location, Specifications & Media */}
              <div className="bg-white border border-[#E2E6E3] rounded-xl p-5 shadow-xs space-y-4">
                <div className="flex items-center gap-2 pb-2.5 border-b border-[#E2E6E3]">
                  <MapPin className="w-4 h-4 text-[#123B32]" />
                  <h2 className="text-xs font-bold uppercase tracking-wider text-[#123B32]">
                    3. Location, Attributes & Media
                  </h2>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <Select
                    label="Branch"
                    requiredIndicator
                    required
                    value={branchId}
                    onChange={(e) => handleBranchChange(e.target.value)}
                    options={[
                      { value: '', label: 'Select branch...' },
                      ...branches.map((b) => ({ value: b.id, label: `${b.name} (${b.code})` })),
                    ]}
                  />
                  <Select
                    label="Stock Location"
                    requiredIndicator
                    required
                    value={stockLocationId}
                    onChange={(e) => setStockLocationId(e.target.value)}
                    options={[
                      { value: '', label: locations.length ? 'Select location...' : 'No locations in branch' },
                      ...locations.map((l) => ({
                        value: l.id,
                        label: `${l.name} (${l.locationType})`,
                      })),
                    ]}
                  />
                </div>

                {specifications.length > 0 && (
                  <div className="pt-2 space-y-2.5">
                    <span className="block text-xs font-semibold text-[#202824] uppercase tracking-wider">
                      Specifications (Optional)
                    </span>
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                      {specifications.map((spec) => {
                        const currentSelection = selectedSpecs.find((s) => s.specificationId === spec.id);
                        const isFreeText = spec.inputType === 'FREE_TEXT';
                        const isHybrid = spec.inputType === 'HYBRID';

                        return (
                          <div key={spec.id} className="p-3 rounded-lg bg-[#F8F7F4] border border-[#E2E6E3] space-y-2">
                            <span className="block text-xs font-semibold text-[#202824]">{spec.name}</span>

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
                                          customValue: currentSelection.customValue,
                                        },
                                      ];
                                    }
                                    return filtered;
                                  });
                                }}
                                className="w-full px-2.5 py-1.5 text-xs border border-[#E2E6E3] rounded-md bg-white text-[#202824]"
                              >
                                <option value="">Not specified</option>
                                {spec.options.map((opt) => (
                                  <option key={opt.id} value={opt.id}>
                                    {opt.value}
                                  </option>
                                ))}
                              </select>
                            )}

                            {(isFreeText || isHybrid) && (
                              <input
                                type="text"
                                placeholder="Enter value..."
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
                                          specificationOptionId: isFreeText
                                            ? undefined
                                            : currentSelection?.specificationOptionId,
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
                                        },
                                      ];
                                    }
                                    return filtered;
                                  });
                                }}
                                className="w-full px-2.5 py-1.5 text-xs border border-[#E2E6E3] rounded-md bg-white text-[#202824]"
                              />
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}

                {/* Media Upload */}
                <div className="pt-2 space-y-2">
                  <span className="block text-xs font-semibold text-[#202824] uppercase tracking-wider">
                    Photos & Certificates (Optional)
                  </span>
                  <label
                    htmlFor="media-upload-input"
                    className="flex items-center justify-center gap-2 border border-dashed border-[#D1D7D3] hover:border-[#123B32] rounded-lg px-4 py-3 text-xs text-[#69736E] hover:bg-[#F8F7F4] cursor-pointer transition"
                  >
                    <UploadCloud className="w-4 h-4 text-[#123B32]" />
                    <span>{isUploadingMedia ? 'Uploading...' : 'Click to attach images or PDF certificates'}</span>
                    <input
                      id="media-upload-input"
                      type="file"
                      multiple
                      accept="image/jpeg,image/png,image/webp,application/pdf"
                      onChange={handleFileUpload}
                      disabled={isUploadingMedia}
                      className="hidden"
                    />
                  </label>

                  {mediaList.length > 0 && (
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 pt-1">
                      {mediaList.map((m, idx) => (
                        <div
                          key={idx}
                          className={`px-3 py-2 rounded-lg border text-xs flex items-center justify-between ${
                            m.isPrimary ? 'border-[#B89A60] bg-[#F8F7F4]' : 'border-[#E2E6E3] bg-white'
                          }`}
                        >
                          <span className="truncate font-medium text-[#202824]">{m.fileName}</span>
                          <div className="flex items-center gap-1.5 flex-shrink-0 ml-2">
                            {m.mediaType === 1 && (
                              <button
                                type="button"
                                onClick={() => handleSetPrimaryMedia(idx)}
                                className={`px-1.5 py-0.5 text-[10px] rounded font-semibold ${
                                  m.isPrimary ? 'bg-[#B89A60] text-white' : 'bg-gray-100 text-gray-600'
                                }`}
                              >
                                {m.isPrimary ? 'Primary' : 'Set Primary'}
                              </button>
                            )}
                            <button
                              type="button"
                              onClick={() => handleRemoveMedia(idx)}
                              className="text-rose-500 hover:text-rose-700"
                            >
                              <X className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* Right Column: Sticky Live Summary & Submit */}
            <div className="lg:col-span-4 lg:sticky lg:top-20 space-y-4">
              <div className="bg-white border border-[#E2E6E3] rounded-xl p-5 shadow-xs space-y-4">
                <h3 className="text-xs font-bold uppercase tracking-wider text-[#123B32] border-b border-[#E2E6E3] pb-2.5">
                  Item Summary
                </h3>

                <div className="space-y-2.5 text-xs">
                  <div className="flex justify-between">
                    <span className="text-[#69736E]">Item</span>
                    <span className="font-semibold text-[#202824] text-right truncate max-w-[180px]">
                      {name.trim() || '—'}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-[#69736E]">Classification</span>
                    <span className="font-medium text-[#202824] text-right">
                      {[selectedMaterial?.code, selectedCategory?.name, selectedPurity?.code]
                        .filter(Boolean)
                        .join(' • ') || '—'}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-[#69736E]">Branch / Location</span>
                    <span className="font-medium text-[#202824] text-right">
                      {selectedBranch ? `${selectedBranch.code}` : '—'}
                      {selectedLocation ? ` • ${selectedLocation.name}` : ''}
                    </span>
                  </div>

                  <div className="border-t border-[#E2E6E3] pt-2.5 space-y-1.5 font-mono">
                    <div className="flex justify-between">
                      <span className="text-[#69736E] font-sans">Gross Weight</span>
                      <span>{parsedGross.toFixed(4)} g</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-[#69736E] font-sans">Stone Weight</span>
                      <span>{parsedStone.toFixed(4)} g</span>
                    </div>
                    <div className="flex justify-between font-bold text-[#123B32]">
                      <span className="font-sans">Net Weight</span>
                      <span>{netWeightPreview} g</span>
                    </div>
                  </div>

                  <div className="border-t border-[#E2E6E3] pt-2.5 flex justify-between">
                    <span className="text-[#69736E]">Making Charge</span>
                    <span className="font-mono font-semibold text-[#202824]">
                      {currency} {(parseFloat(makingChargeValue) || 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}{' '}
                      <span className="text-[10px] text-[#69736E] font-sans">
                        ({makingChargeMethod === 1 ? '/g' : 'fixed'})
                      </span>
                    </span>
                  </div>
                </div>

                <p className="text-[11px] text-[#69736E] bg-[#F8F7F4] p-2.5 rounded-lg border border-[#E2E6E3]">
                  Tag number and Code-128 barcode are generated automatically when you save.
                </p>

                <div className="space-y-2 pt-1">
                  <Button
                    type="submit"
                    variant="primary"
                    size="lg"
                    isLoading={isSubmitting}
                    disabled={hasWeightError}
                    className="w-full"
                    leftIcon={<CheckCircle2 className="w-4 h-4" />}
                  >
                    Create Jewellery Item
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={handleReset}
                    disabled={isSubmitting}
                    className="w-full"
                  >
                    Clear Form
                  </Button>
                </div>
              </div>
            </div>
          </form>
        )}
      </div>
    </AppLayout>
  );
}
