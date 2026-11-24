'use client';

import React, { useState, useEffect } from 'react';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  Material,
  Category,
  Purity,
  Specification,
  SpecificationOption,
  StockLocation,
  MakingChargeMethod,
  materialApi,
  categoryApi,
  purityApi,
  specificationApi,
  stockLocationApi,
  makingChargeMethodApi,
  ApiError,
} from '@/lib/api';

type TabType = 'materials' | 'categories' | 'purities' | 'specifications' | 'locations' | 'methods';

export default function AttributesPage() {
  const { user } = useAuth();
  const { branches, activeBranchId } = useBranch();
  const [activeTab, setActiveTab] = useState<TabType>('materials');

  // Permissions
  const canCreate = user?.isGlobalAdmin || user?.permissions.includes('ATTRIBUTES_CREATE');
  const canEdit = user?.isGlobalAdmin || user?.permissions.includes('ATTRIBUTES_EDIT');
  const canToggle = user?.isGlobalAdmin || user?.permissions.includes('ATTRIBUTES_ACTIVATE_DEACTIVATE');

  // Data states
  const [materials, setMaterials] = useState<Material[]>([]);
  const [categories, setCategories] = useState<Category[]>([]);
  const [purities, setPurities] = useState<Purity[]>([]);
  const [specifications, setSpecifications] = useState<Specification[]>([]);
  const [stockLocations, setStockLocations] = useState<StockLocation[]>([]);
  const [makingMethods, setMakingMethods] = useState<MakingChargeMethod[]>([]);

  // Filter / selection
  const [search, setSearch] = useState('');
  const [showInactive, setShowInactive] = useState(false);
  const [selectedBranchId, setSelectedBranchId] = useState<string>('');

  // UI state
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Modal states
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<any | null>(null);

  // Option Modal state (for Specifications)
  const [isOptionModalOpen, setIsOptionModalOpen] = useState(false);
  const [activeSpecForOption, setActiveSpecForOption] = useState<Specification | null>(null);
  const [optionValue, setOptionValue] = useState('');
  const [optionCode, setOptionCode] = useState('');
  const [optionDesc, setOptionDesc] = useState('');
  const [optionSortOrder, setOptionSortOrder] = useState('0');

  // Form states
  const [formName, setFormName] = useState('');
  const [formCode, setFormCode] = useState('');
  const [formDesc, setFormDesc] = useState('');
  const [formSortOrder, setFormSortOrder] = useState('0');
  const [formIsActive, setFormIsActive] = useState(true);

  // Purity specific form fields
  const [formKaratValue, setFormKaratValue] = useState('22.0');
  const [formPurityPercentage, setFormPurityPercentage] = useState('91.67');

  // Stock Location specific form fields
  const [formBranchId, setFormBranchId] = useState('');
  const [formLocationType, setFormLocationType] = useState('Display');

  // Auto-select branch for locations
  useEffect(() => {
    if (activeBranchId) {
      setSelectedBranchId(activeBranchId);
      setFormBranchId(activeBranchId);
    } else if (branches.length > 0) {
      setSelectedBranchId(branches[0].id);
      setFormBranchId(branches[0].id);
    }
  }, [activeBranchId, branches]);

  // Load data for active tab
  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      if (activeTab === 'materials') {
        const data = await materialApi.getMaterials(showInactive ? undefined : true);
        setMaterials(data);
      } else if (activeTab === 'categories') {
        const data = await categoryApi.getCategories(showInactive ? undefined : true);
        setCategories(data);
      } else if (activeTab === 'purities') {
        const data = await purityApi.getPurities(showInactive ? undefined : true);
        setPurities(data);
      } else if (activeTab === 'specifications') {
        const data = await specificationApi.getSpecifications(showInactive ? undefined : true);
        setSpecifications(data);
      } else if (activeTab === 'locations') {
        const targetBranch = selectedBranchId || activeBranchId || undefined;
        const data = await stockLocationApi.getStockLocations(targetBranch, showInactive ? undefined : true);
        setStockLocations(data);
      } else if (activeTab === 'methods') {
        const data = await makingChargeMethodApi.getMakingChargeMethods();
        setMakingMethods(data);
      }
    } catch (err: any) {
      setError(err?.message || 'Failed to load master attribute data');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, [activeTab, showInactive, selectedBranchId]);

  // Handle open modal for Create
  const handleOpenCreateModal = () => {
    setEditingItem(null);
    setFormName('');
    setFormCode('');
    setFormDesc('');
    setFormSortOrder('0');
    setFormIsActive(true);
    setFormKaratValue('22.0');
    setFormPurityPercentage('91.67');
    setFormLocationType('Display');
    setFormBranchId(selectedBranchId || activeBranchId || (branches[0]?.id ?? ''));
    setError(null);
    setIsModalOpen(true);
  };

  // Handle open modal for Edit
  const handleOpenEditModal = (item: any) => {
    setEditingItem(item);
    setFormName(item.name || '');
    setFormCode(item.code || '');
    setFormDesc(item.description || '');
    setFormSortOrder(String(item.sortOrder || 0));
    setFormIsActive(item.isActive);
    if (activeTab === 'purities') {
      setFormKaratValue(String(item.karatValue));
      setFormPurityPercentage(String(item.purityPercentage));
    }
    if (activeTab === 'locations') {
      setFormLocationType(item.locationType || 'Display');
      setFormBranchId(item.branchId);
    }
    setError(null);
    setIsModalOpen(true);
  };

  // Handle submit modal form
  const handleSubmitForm = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      const sortOrderNum = parseInt(formSortOrder, 10) || 0;

      if (activeTab === 'materials') {
        if (editingItem) {
          await materialApi.updateMaterial(editingItem.id, {
            name: formName,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Material updated successfully.');
        } else {
          await materialApi.createMaterial({
            name: formName,
            code: formCode,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Material created successfully.');
        }
      } else if (activeTab === 'categories') {
        if (editingItem) {
          await categoryApi.updateCategory(editingItem.id, {
            name: formName,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Category updated successfully.');
        } else {
          await categoryApi.createCategory({
            name: formName,
            code: formCode,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Category created successfully.');
        }
      } else if (activeTab === 'purities') {
        const karatNum = parseFloat(formKaratValue);
        const purityNum = parseFloat(formPurityPercentage);
        if (isNaN(karatNum) || isNaN(purityNum)) {
          throw new Error('Karat and Purity percentage must be valid numbers.');
        }
        if (editingItem) {
          await purityApi.updatePurity(editingItem.id, {
            name: formName,
            karatValue: karatNum,
            purityPercentage: purityNum,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Purity updated successfully.');
        } else {
          await purityApi.createPurity({
            name: formName,
            karatValue: karatNum,
            purityPercentage: purityNum,
            code: formCode,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Purity created successfully.');
        }
      } else if (activeTab === 'specifications') {
        if (editingItem) {
          await specificationApi.updateSpecification(editingItem.id, {
            name: formName,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Specification updated successfully.');
        } else {
          await specificationApi.createSpecification({
            name: formName,
            code: formCode,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Specification created successfully.');
        }
      } else if (activeTab === 'locations') {
        if (editingItem) {
          await stockLocationApi.updateStockLocation(editingItem.id, {
            name: formName,
            locationType: formLocationType,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Stock location updated successfully.');
        } else {
          if (!formBranchId) throw new Error('Branch must be selected.');
          await stockLocationApi.createStockLocation({
            branchId: formBranchId,
            name: formName,
            code: formCode,
            locationType: formLocationType,
            description: formDesc,
            isActive: formIsActive,
            sortOrder: sortOrderNum,
          });
          setSuccess('Stock location created successfully.');
        }
      }

      setIsModalOpen(false);
      loadData();
    } catch (err: any) {
      setError(err?.message || 'Operation failed');
    }
  };

  // Toggle status
  const handleToggleStatus = async (item: any) => {
    try {
      if (activeTab === 'materials') await materialApi.toggleStatus(item.id);
      else if (activeTab === 'categories') await categoryApi.toggleStatus(item.id);
      else if (activeTab === 'purities') await purityApi.toggleStatus(item.id);
      else if (activeTab === 'specifications') await specificationApi.toggleStatus(item.id);
      else if (activeTab === 'locations') await stockLocationApi.toggleStatus(item.id);

      setSuccess(`Status changed for ${item.name || item.code}`);
      loadData();
    } catch (err: any) {
      setError(err?.message || 'Failed to toggle status');
    }
  };

  // Add Option to Specification
  const handleAddOption = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeSpecForOption) return;
    try {
      await specificationApi.addOption(activeSpecForOption.id, {
        value: optionValue,
        code: optionCode,
        description: optionDesc,
        sortOrder: parseInt(optionSortOrder, 10) || 0,
        isActive: true,
      });
      setSuccess(`Option '${optionValue}' added to ${activeSpecForOption.name}.`);
      setIsOptionModalOpen(false);
      setOptionValue('');
      setOptionCode('');
      setOptionDesc('');
      setOptionSortOrder('0');
      loadData();
    } catch (err: any) {
      setError(err?.message || 'Failed to add option');
    }
  };

  // Toggle Specification Option status
  const handleToggleOptionStatus = async (specId: string, optId: string) => {
    try {
      await specificationApi.toggleOptionStatus(specId, optId);
      setSuccess('Option status toggled.');
      loadData();
    } catch (err: any) {
      setError(err?.message || 'Failed to toggle option status');
    }
  };

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-gray-200 pb-5">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-gray-900 flex items-center space-x-2">
              <span>Attributes Master</span>
              <span className="text-xs px-2.5 py-0.5 rounded-full font-semibold bg-[#0D3826]/10 text-[#0D3826] border border-[#0D3826]/20">
                Module 3
              </span>
            </h1>
            <p className="text-sm text-gray-500 mt-1">
              Configure master jewellery properties: materials, categories, purities, specifications, display/stock locations, and making charge methods.
            </p>
          </div>

          {activeTab !== 'methods' && canCreate && (
            <button
              onClick={handleOpenCreateModal}
              className="inline-flex items-center space-x-2 px-4 py-2 bg-[#0D3826] hover:bg-[#1B6543] text-white text-sm font-semibold rounded-lg shadow-sm border border-[#D4AF37]/50 transition"
            >
              <span>+</span>
              <span>
                Add{' '}
                {activeTab === 'materials'
                  ? 'Material'
                  : activeTab === 'categories'
                  ? 'Category'
                  : activeTab === 'purities'
                  ? 'Purity'
                  : activeTab === 'specifications'
                  ? 'Specification'
                  : 'Location'}
              </span>
            </button>
          )}
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

        {/* Tabs Bar */}
        <div className="border-b border-gray-200">
          <nav className="-mb-px flex space-x-6 overflow-x-auto">
            {[
              { id: 'materials', label: 'Materials', icon: '🪙' },
              { id: 'categories', label: 'Categories', icon: '🏷️' },
              { id: 'purities', label: 'Purities / Karats', icon: '✨' },
              { id: 'specifications', label: 'Specifications & Options', icon: '⚙️' },
              { id: 'locations', label: 'Stock & Display Locations', icon: '📍' },
              { id: 'methods', label: 'Making Charge Methods', icon: '📐' },
            ].map((tab) => (
              <button
                key={tab.id}
                onClick={() => {
                  setActiveTab(tab.id as TabType);
                  setSearch('');
                  setError(null);
                }}
                className={`whitespace-nowrap py-3 px-1 border-b-2 font-medium text-sm flex items-center space-x-2 transition ${
                  activeTab === tab.id
                    ? 'border-[#0D3826] text-[#0D3826] font-bold'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                <span>{tab.icon}</span>
                <span>{tab.label}</span>
              </button>
            ))}
          </nav>
        </div>

        {/* Filters and Controls */}
        {activeTab !== 'methods' && (
          <div className="flex flex-col sm:flex-row items-center justify-between gap-3 bg-white p-3 rounded-xl border border-gray-200">
            <div className="flex flex-1 items-center space-x-3 w-full sm:w-auto">
              <input
                type="text"
                placeholder="Search by name or code..."
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="w-full sm:w-72 px-3 py-1.5 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826] focus:border-[#0D3826]"
              />

              {activeTab === 'locations' && (
                <div className="flex items-center space-x-2">
                  <span className="text-xs font-semibold text-gray-600">Branch:</span>
                  <select
                    value={selectedBranchId}
                    onChange={(e) => setSelectedBranchId(e.target.value)}
                    className="px-2 py-1.5 text-xs border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826] bg-white font-medium"
                  >
                    {user?.isGlobalAdmin && <option value="">All Branches</option>}
                    {branches.map((b) => (
                      <option key={b.id} value={b.id}>
                        {b.code} - {b.name}
                      </option>
                    ))}
                  </select>
                </div>
              )}
            </div>

            <div className="flex items-center space-x-3 self-end sm:self-auto">
              <label className="flex items-center space-x-2 text-xs text-gray-600 cursor-pointer">
                <input
                  type="checkbox"
                  checked={showInactive}
                  onChange={(e) => setShowInactive(e.target.checked)}
                  className="rounded border-gray-300 text-[#0D3826] focus:ring-[#0D3826]"
                />
                <span>Show Inactive</span>
              </label>
              <button
                onClick={loadData}
                disabled={loading}
                className="px-3 py-1.5 text-xs bg-gray-100 hover:bg-gray-200 text-gray-700 font-semibold rounded-lg transition"
              >
                {loading ? 'Refreshing...' : 'Refresh'}
              </button>
            </div>
          </div>
        )}

        {/* Tab 1: Materials Table */}
        {activeTab === 'materials' && (
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
            <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
              <thead className="bg-[#0D3826]/5 text-gray-700 font-bold uppercase tracking-wider">
                <tr>
                  <th className="px-4 py-3">Material Name</th>
                  <th className="px-4 py-3">Code</th>
                  <th className="px-4 py-3">Description</th>
                  <th className="px-4 py-3 text-center">Sort Order</th>
                  <th className="px-4 py-3 text-center">Status</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {materials
                  .filter((m) =>
                    !search ||
                    m.name.toLowerCase().includes(search.toLowerCase()) ||
                    m.code.toLowerCase().includes(search.toLowerCase())
                  )
                  .map((m) => (
                    <tr key={m.id} className="hover:bg-gray-50/80 transition">
                      <td className="px-4 py-3 font-semibold text-gray-900">{m.name}</td>
                      <td className="px-4 py-3 font-mono font-bold text-[#0D3826]">{m.code}</td>
                      <td className="px-4 py-3 text-gray-500">{m.description || '—'}</td>
                      <td className="px-4 py-3 text-center text-gray-600 font-mono">{m.sortOrder}</td>
                      <td className="px-4 py-3 text-center">
                        <span
                          className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold ${
                            m.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-100 text-gray-600'
                          }`}
                        >
                          {m.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right space-x-2">
                        {canEdit && (
                          <button
                            onClick={() => handleOpenEditModal(m)}
                            className="text-[#0D3826] hover:underline font-semibold"
                          >
                            Edit
                          </button>
                        )}
                        {canToggle && (
                          <button
                            onClick={() => handleToggleStatus(m)}
                            className={`font-semibold hover:underline ${m.isActive ? 'text-amber-700' : 'text-emerald-700'}`}
                          >
                            {m.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                {materials.length === 0 && !loading && (
                  <tr>
                    <td colSpan={6} className="text-center py-8 text-gray-500">
                      No materials found. Click "Add Material" to create one.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}

        {/* Tab 2: Categories Table */}
        {activeTab === 'categories' && (
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
            <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
              <thead className="bg-[#0D3826]/5 text-gray-700 font-bold uppercase tracking-wider">
                <tr>
                  <th className="px-4 py-3">Category Name</th>
                  <th className="px-4 py-3">Code</th>
                  <th className="px-4 py-3">Description</th>
                  <th className="px-4 py-3 text-center">Sort Order</th>
                  <th className="px-4 py-3 text-center">Status</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {categories
                  .filter((c) =>
                    !search ||
                    c.name.toLowerCase().includes(search.toLowerCase()) ||
                    c.code.toLowerCase().includes(search.toLowerCase())
                  )
                  .map((c) => (
                    <tr key={c.id} className="hover:bg-gray-50/80 transition">
                      <td className="px-4 py-3 font-semibold text-gray-900">{c.name}</td>
                      <td className="px-4 py-3 font-mono font-bold text-[#0D3826]">{c.code}</td>
                      <td className="px-4 py-3 text-gray-500">{c.description || '—'}</td>
                      <td className="px-4 py-3 text-center text-gray-600 font-mono">{c.sortOrder}</td>
                      <td className="px-4 py-3 text-center">
                        <span
                          className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold ${
                            c.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-100 text-gray-600'
                          }`}
                        >
                          {c.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right space-x-2">
                        {canEdit && (
                          <button
                            onClick={() => handleOpenEditModal(c)}
                            className="text-[#0D3826] hover:underline font-semibold"
                          >
                            Edit
                          </button>
                        )}
                        {canToggle && (
                          <button
                            onClick={() => handleToggleStatus(c)}
                            className={`font-semibold hover:underline ${c.isActive ? 'text-amber-700' : 'text-emerald-700'}`}
                          >
                            {c.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                {categories.length === 0 && !loading && (
                  <tr>
                    <td colSpan={6} className="text-center py-8 text-gray-500">
                      No categories found. Click "Add Category" to create one.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}

        {/* Tab 3: Purities Table */}
        {activeTab === 'purities' && (
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
            <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
              <thead className="bg-[#0D3826]/5 text-gray-700 font-bold uppercase tracking-wider">
                <tr>
                  <th className="px-4 py-3">Purity / Display Name</th>
                  <th className="px-4 py-3">Code</th>
                  <th className="px-4 py-3 text-center">Karat Value</th>
                  <th className="px-4 py-3 text-center">Fineness / Purity %</th>
                  <th className="px-4 py-3">Description</th>
                  <th className="px-4 py-3 text-center">Status</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {purities
                  .filter((p) =>
                    !search ||
                    p.name.toLowerCase().includes(search.toLowerCase()) ||
                    p.code.toLowerCase().includes(search.toLowerCase())
                  )
                  .map((p) => (
                    <tr key={p.id} className="hover:bg-gray-50/80 transition">
                      <td className="px-4 py-3 font-semibold text-gray-900">{p.name}</td>
                      <td className="px-4 py-3 font-mono font-bold text-[#0D3826]">{p.code}</td>
                      <td className="px-4 py-3 text-center font-mono font-bold text-amber-700">
                        {p.karatValue.toFixed(1)}K
                      </td>
                      <td className="px-4 py-3 text-center font-mono font-semibold text-emerald-800">
                        {p.purityPercentage.toFixed(2)}%
                      </td>
                      <td className="px-4 py-3 text-gray-500">{p.description || '—'}</td>
                      <td className="px-4 py-3 text-center">
                        <span
                          className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold ${
                            p.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-100 text-gray-600'
                          }`}
                        >
                          {p.isActive ? 'Active' : 'Inactive'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right space-x-2">
                        {canEdit && (
                          <button
                            onClick={() => handleOpenEditModal(p)}
                            className="text-[#0D3826] hover:underline font-semibold"
                          >
                            Edit
                          </button>
                        )}
                        {canToggle && (
                          <button
                            onClick={() => handleToggleStatus(p)}
                            className={`font-semibold hover:underline ${p.isActive ? 'text-amber-700' : 'text-emerald-700'}`}
                          >
                            {p.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                {purities.length === 0 && !loading && (
                  <tr>
                    <td colSpan={7} className="text-center py-8 text-gray-500">
                      No purities found. Click "Add Purity" to create one.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}

        {/* Tab 4: Specifications & Options Table */}
        {activeTab === 'specifications' && (
          <div className="space-y-4">
            {specifications
              .filter((s) =>
                !search ||
                s.name.toLowerCase().includes(search.toLowerCase()) ||
                s.code.toLowerCase().includes(search.toLowerCase())
              )
              .map((s) => (
                <div key={s.id} className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
                  <div className="bg-[#0D3826]/5 px-4 py-3 flex items-center justify-between border-b border-gray-200">
                    <div className="flex items-center space-x-3">
                      <span className="font-bold text-gray-900 text-sm">{s.name}</span>
                      <span className="font-mono text-xs font-bold text-[#0D3826] bg-[#0D3826]/10 px-2 py-0.5 rounded">
                        {s.code}
                      </span>
                      <span
                        className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold ${
                          s.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-100 text-gray-600'
                        }`}
                      >
                        {s.isActive ? 'Active' : 'Inactive'}
                      </span>
                      {s.description && <span className="text-xs text-gray-500 italic">({s.description})</span>}
                    </div>

                    <div className="flex items-center space-x-3">
                      {canCreate && (
                        <button
                          onClick={() => {
                            setActiveSpecForOption(s);
                            setOptionValue('');
                            setOptionCode('');
                            setOptionDesc('');
                            setOptionSortOrder('0');
                            setIsOptionModalOpen(true);
                          }}
                          className="px-2.5 py-1 text-xs bg-[#D4AF37] hover:bg-[#b89528] text-white font-semibold rounded shadow-sm transition"
                        >
                          + Add Option
                        </button>
                      )}
                      {canEdit && (
                        <button
                          onClick={() => handleOpenEditModal(s)}
                          className="text-xs text-[#0D3826] hover:underline font-semibold"
                        >
                          Edit
                        </button>
                      )}
                      {canToggle && (
                        <button
                          onClick={() => handleToggleStatus(s)}
                          className={`text-xs font-semibold hover:underline ${
                            s.isActive ? 'text-amber-700' : 'text-emerald-700'
                          }`}
                        >
                          {s.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      )}
                    </div>
                  </div>

                  {/* Options List */}
                  <div className="p-4">
                    <p className="text-[11px] font-bold uppercase tracking-wider text-gray-400 mb-2">Configured Options</p>
                    {s.options.length === 0 ? (
                      <p className="text-xs text-gray-500 italic">No options configured yet for this specification.</p>
                    ) : (
                      <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-2">
                        {s.options.map((opt) => (
                          <div
                            key={opt.id}
                            className={`flex items-center justify-between p-2 rounded-lg border text-xs ${
                              opt.isActive
                                ? 'bg-gray-50 border-gray-200'
                                : 'bg-gray-100/60 border-dashed border-gray-300 opacity-60'
                            }`}
                          >
                            <div>
                              <span className="font-semibold text-gray-800">{opt.value}</span>
                              <span className="ml-1.5 font-mono text-[10px] text-gray-500">({opt.code})</span>
                              {opt.description && <p className="text-[10px] text-gray-400">{opt.description}</p>}
                            </div>

                            {canToggle && (
                              <button
                                onClick={() => handleToggleOptionStatus(s.id, opt.id)}
                                title={opt.isActive ? 'Deactivate option' : 'Activate option'}
                                className={`text-[10px] px-1.5 py-0.5 rounded font-semibold ${
                                  opt.isActive
                                    ? 'bg-emerald-100 text-emerald-800 hover:bg-emerald-200'
                                    : 'bg-gray-200 text-gray-600 hover:bg-gray-300'
                                }`}
                              >
                                {opt.isActive ? 'Active' : 'Inactive'}
                              </button>
                            )}
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </div>
              ))}
            {specifications.length === 0 && !loading && (
              <div className="bg-white rounded-xl p-8 text-center text-gray-500 border border-gray-200">
                No specifications found. Click "Add Specification" to configure one.
              </div>
            )}
          </div>
        )}

        {/* Tab 5: Stock & Display Locations Table */}
        {activeTab === 'locations' && (
          <div className="space-y-3">
            <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 text-xs text-amber-800 flex items-start space-x-2">
              <span className="text-sm">🛡️</span>
              <div>
                <span className="font-bold">Branch Isolation Enforced:</span> Display and stock locations belong strictly to their parent branch. Items stored in a location can only be accessed or transacted within that branch.
              </div>
            </div>

            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
              <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                <thead className="bg-[#0D3826]/5 text-gray-700 font-bold uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Location Name</th>
                    <th className="px-4 py-3">Code</th>
                    <th className="px-4 py-3">Branch</th>
                    <th className="px-4 py-3">Type</th>
                    <th className="px-4 py-3">Description</th>
                    <th className="px-4 py-3 text-center">Status</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {stockLocations
                    .filter((l) =>
                      !search ||
                      l.name.toLowerCase().includes(search.toLowerCase()) ||
                      l.code.toLowerCase().includes(search.toLowerCase())
                    )
                    .map((l) => (
                      <tr key={l.id} className="hover:bg-gray-50/80 transition">
                        <td className="px-4 py-3 font-semibold text-gray-900">{l.name}</td>
                        <td className="px-4 py-3 font-mono font-bold text-[#0D3826]">{l.code}</td>
                        <td className="px-4 py-3 font-semibold text-gray-700">
                          {l.branchCode} - {l.branchName}
                        </td>
                        <td className="px-4 py-3">
                          <span className="px-2 py-0.5 rounded bg-blue-50 text-blue-700 font-medium border border-blue-200 text-[10px]">
                            {l.locationType}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-gray-500">{l.description || '—'}</td>
                        <td className="px-4 py-3 text-center">
                          <span
                            className={`inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold ${
                              l.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-100 text-gray-600'
                            }`}
                          >
                            {l.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right space-x-2">
                          {canEdit && (
                            <button
                              onClick={() => handleOpenEditModal(l)}
                              className="text-[#0D3826] hover:underline font-semibold"
                            >
                              Edit
                            </button>
                          )}
                          {canToggle && (
                            <button
                              onClick={() => handleToggleStatus(l)}
                              className={`font-semibold hover:underline ${l.isActive ? 'text-amber-700' : 'text-emerald-700'}`}
                            >
                              {l.isActive ? 'Deactivate' : 'Activate'}
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  {stockLocations.length === 0 && !loading && (
                    <tr>
                      <td colSpan={7} className="text-center py-8 text-gray-500">
                        No locations found for this branch filter. Click "Add Location" to create one.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Tab 6: Making Charge Methods Reference */}
        {activeTab === 'methods' && (
          <div className="space-y-4">
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
              <div className="bg-[#0D3826]/5 px-4 py-3 border-b border-gray-200">
                <h3 className="font-bold text-gray-900 text-sm">Supported Making Charge Methods</h3>
                <p className="text-xs text-gray-500 mt-0.5">
                  Saranga Jewellery System calculates making charges using the confirmed methods below. Charge rates and VAT calculation are configured under Module 9.
                </p>
              </div>

              <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                <thead className="bg-gray-50 text-gray-700 font-bold uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Method Name</th>
                    <th className="px-4 py-3">Code / Identifier</th>
                    <th className="px-4 py-3">Role in System</th>
                    <th className="px-4 py-3">Description</th>
                    <th className="px-4 py-3 text-center">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {makingMethods.map((m) => (
                    <tr key={m.id} className="hover:bg-gray-50/80 transition">
                      <td className="px-4 py-3 font-semibold text-gray-900 flex items-center space-x-2">
                        <span>{m.name}</span>
                        {m.isDefault && (
                          <span className="px-1.5 py-0.2 rounded bg-amber-100 text-amber-800 text-[10px] font-bold border border-amber-300">
                            Default
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-3 font-mono font-bold text-[#0D3826]">{m.code}</td>
                      <td className="px-4 py-3 text-gray-700 font-medium">
                        {m.code === 'PER_GRAM' ? 'Standard Jewellery (Gold/Silver)' : 'Custom/Artistic Pieces'}
                      </td>
                      <td className="px-4 py-3 text-gray-500">{m.description}</td>
                      <td className="px-4 py-3 text-center">
                        <span className="inline-flex px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800">
                          Active
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Modal: Create / Edit Attribute */}
        {isModalOpen && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="bg-white rounded-xl shadow-xl max-w-md w-full overflow-hidden border border-gray-200 animate-in fade-in zoom-in-95 duration-150">
              <div className="bg-[#0D3826] px-6 py-4 text-white flex items-center justify-between">
                <h3 className="font-bold text-sm tracking-wide">
                  {editingItem ? 'Edit' : 'Create'}{' '}
                  {activeTab === 'materials'
                    ? 'Material'
                    : activeTab === 'categories'
                    ? 'Category'
                    : activeTab === 'purities'
                    ? 'Purity'
                    : activeTab === 'specifications'
                    ? 'Specification'
                    : 'Stock Location'}
                </h3>
                <button onClick={() => setIsModalOpen(false)} className="text-gray-300 hover:text-white font-bold">
                  ✕
                </button>
              </div>

              <form onSubmit={handleSubmitForm} className="p-6 space-y-4 text-xs">
                {/* Branch Selection for Stock Location */}
                {activeTab === 'locations' && !editingItem && (
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Target Branch *</label>
                    <select
                      value={formBranchId}
                      onChange={(e) => setFormBranchId(e.target.value)}
                      required
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                    >
                      <option value="">Select a branch...</option>
                      {branches.map((b) => (
                        <option key={b.id} value={b.id}>
                          {b.code} - {b.name} ({b.currencyCode})
                        </option>
                      ))}
                    </select>
                  </div>
                )}

                <div>
                  <label className="block font-semibold text-gray-700 mb-1">Name / Display Name *</label>
                  <input
                    type="text"
                    required
                    value={formName}
                    onChange={(e) => setFormName(e.target.value)}
                    placeholder="e.g. 22K Gold, Necklaces, Diamond Clarity..."
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                  />
                </div>

                {!editingItem && (
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Unique Code *</label>
                    <input
                      type="text"
                      required
                      value={formCode}
                      onChange={(e) => setFormCode(e.target.value)}
                      placeholder="e.g. GOLD-22K, NECK, DISP-01..."
                      className="w-full px-3 py-2 font-mono border border-gray-300 rounded-lg uppercase focus:ring-1 focus:ring-[#0D3826]"
                    />
                    <p className="text-[10px] text-gray-400 mt-1">Codes must be unique and cannot be changed once created.</p>
                  </div>
                )}

                {/* Purity-specific fields */}
                {activeTab === 'purities' && (
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="block font-semibold text-gray-700 mb-1">Karat Value (K) *</label>
                      <input
                        type="number"
                        step="0.01"
                        required
                        value={formKaratValue}
                        onChange={(e) => setFormKaratValue(e.target.value)}
                        placeholder="22.0"
                        className="w-full px-3 py-2 font-mono border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                      />
                    </div>
                    <div>
                      <label className="block font-semibold text-gray-700 mb-1">Purity Fineness (%) *</label>
                      <input
                        type="number"
                        step="0.01"
                        required
                        value={formPurityPercentage}
                        onChange={(e) => setFormPurityPercentage(e.target.value)}
                        placeholder="91.67"
                        className="w-full px-3 py-2 font-mono border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                      />
                    </div>
                  </div>
                )}

                {/* Location-specific fields */}
                {activeTab === 'locations' && (
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Location Type *</label>
                    <select
                      value={formLocationType}
                      onChange={(e) => setFormLocationType(e.target.value)}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                    >
                      <option value="Display">Display Showcase</option>
                      <option value="Vault">Vault / Safe</option>
                      <option value="Workshop">Workshop / Repair</option>
                      <option value="Transit">In Transit</option>
                    </select>
                  </div>
                )}

                <div>
                  <label className="block font-semibold text-gray-700 mb-1">Description</label>
                  <textarea
                    rows={2}
                    value={formDesc}
                    onChange={(e) => setFormDesc(e.target.value)}
                    placeholder="Optional description or operational notes..."
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                  />
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block font-semibold text-gray-700 mb-1">Sort Order</label>
                    <input
                      type="number"
                      value={formSortOrder}
                      onChange={(e) => setFormSortOrder(e.target.value)}
                      className="w-full px-3 py-2 font-mono border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                    />
                  </div>

                  <div className="flex items-center pt-5">
                    <label className="flex items-center space-x-2 text-gray-700 font-semibold cursor-pointer">
                      <input
                        type="checkbox"
                        checked={formIsActive}
                        onChange={(e) => setFormIsActive(e.target.checked)}
                        className="rounded border-gray-300 text-[#0D3826] focus:ring-[#0D3826]"
                      />
                      <span>Active for selection</span>
                    </label>
                  </div>
                </div>

                <div className="flex justify-end space-x-3 pt-4 border-t border-gray-200">
                  <button
                    type="button"
                    onClick={() => setIsModalOpen(false)}
                    className="px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-semibold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="px-4 py-2 bg-[#0D3826] hover:bg-[#1B6543] text-white rounded-lg font-semibold shadow-sm border border-[#D4AF37]/50"
                  >
                    {editingItem ? 'Save Changes' : 'Create'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Modal: Add Option to Specification */}
        {isOptionModalOpen && activeSpecForOption && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="bg-white rounded-xl shadow-xl max-w-sm w-full overflow-hidden border border-gray-200 animate-in fade-in zoom-in-95 duration-150">
              <div className="bg-[#0D3826] px-5 py-3 text-white flex items-center justify-between">
                <div>
                  <h3 className="font-bold text-xs tracking-wide">Add Specification Option</h3>
                  <p className="text-[10px] text-gray-300">Spec: {activeSpecForOption.name}</p>
                </div>
                <button onClick={() => setIsOptionModalOpen(false)} className="text-gray-300 hover:text-white font-bold">
                  ✕
                </button>
              </div>

              <form onSubmit={handleAddOption} className="p-5 space-y-3 text-xs">
                <div>
                  <label className="block font-semibold text-gray-700 mb-1">Option Value *</label>
                  <input
                    type="text"
                    required
                    value={optionValue}
                    onChange={(e) => setOptionValue(e.target.value)}
                    placeholder="e.g. VVS1, Round Brilliant, Size 7..."
                    className="w-full px-3 py-1.5 border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                  />
                </div>

                <div>
                  <label className="block font-semibold text-gray-700 mb-1">Option Code *</label>
                  <input
                    type="text"
                    required
                    value={optionCode}
                    onChange={(e) => setOptionCode(e.target.value)}
                    placeholder="e.g. VVS1, RND, SZ-07..."
                    className="w-full px-3 py-1.5 font-mono uppercase border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                  />
                </div>

                <div>
                  <label className="block font-semibold text-gray-700 mb-1">Description</label>
                  <input
                    type="text"
                    value={optionDesc}
                    onChange={(e) => setOptionDesc(e.target.value)}
                    placeholder="Optional details..."
                    className="w-full px-3 py-1.5 border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                  />
                </div>

                <div>
                  <label className="block font-semibold text-gray-700 mb-1">Sort Order</label>
                  <input
                    type="number"
                    value={optionSortOrder}
                    onChange={(e) => setOptionSortOrder(e.target.value)}
                    className="w-full px-3 py-1.5 font-mono border border-gray-300 rounded-lg focus:ring-1 focus:ring-[#0D3826]"
                  />
                </div>

                <div className="flex justify-end space-x-2 pt-3 border-t border-gray-200">
                  <button
                    type="button"
                    onClick={() => setIsOptionModalOpen(false)}
                    className="px-3 py-1.5 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 font-semibold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="px-3 py-1.5 bg-[#0D3826] hover:bg-[#1B6543] text-white rounded-lg font-semibold shadow-sm"
                  >
                    Add Option
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
