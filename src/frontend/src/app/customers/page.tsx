'use client';

import React, { useState, useEffect, useCallback } from 'react';
import AppLayout from '@/components/AppLayout';
import {
  Customer,
  CreateCustomerRequest,
  UpdateCustomerRequest,
  customerApi,
  CustomerDuplicateCheckResult
} from '@/lib/api';

export default function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Search & Filtering
  const [searchTerm, setSearchTerm] = useState('');
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('active');
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [totalCount, setTotalCount] = useState(0);

  // Modals & Drawers
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isDetailDrawerOpen, setIsDetailDrawerOpen] = useState(false);
  const [isStatusModalOpen, setIsStatusModalOpen] = useState(false);

  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [statusReason, setStatusReason] = useState('');
  const [detailTab, setDetailTab] = useState<'profile' | 'purchases' | 'orders' | 'repairs' | 'oldGold' | 'payments'>('profile');

  // Add Form State
  const [formData, setFormData] = useState<CreateCustomerRequest>({
    fullName: '',
    primaryPhone: '',
    secondaryPhone: '',
    email: '',
    addressLine1: '',
    addressLine2: '',
    city: '',
    districtProvince: '',
    postalCode: '',
    country: 'Sri Lanka',
    dateOfBirth: '',
    notes: '',
  });

  // Edit Form State
  const [editFormData, setEditFormData] = useState<UpdateCustomerRequest>({
    fullName: '',
    primaryPhone: '',
    secondaryPhone: '',
    email: '',
    addressLine1: '',
    addressLine2: '',
    city: '',
    districtProvince: '',
    postalCode: '',
    country: 'Sri Lanka',
    dateOfBirth: '',
    notes: '',
    rowVersion: '',
  });

  // Duplicate Check Warning State
  const [duplicateWarning, setDuplicateWarning] = useState<CustomerDuplicateCheckResult | null>(null);
  const [isCheckingDuplicate, setIsCheckingDuplicate] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Fetch Customers
  const fetchCustomers = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const isActive = activeFilter === 'all' ? undefined : activeFilter === 'active';
      const result = await customerApi.getCustomers({
        search: searchTerm.trim() || undefined,
        isActive,
        page,
        pageSize,
        sortBy: 'CreatedAt',
        sortDescending: true,
      });
      setCustomers(result.items);
      setTotalCount(result.totalCount);
    } catch (err: any) {
      setError(err.message || 'Failed to load customers');
    } finally {
      setLoading(false);
    }
  }, [searchTerm, activeFilter, page, pageSize]);

  useEffect(() => {
    const handler = setTimeout(() => {
      fetchCustomers();
    }, 300);
    return () => clearTimeout(handler);
  }, [fetchCustomers]);

  // Duplicate check on phone / email blur
  const handleCheckDuplicate = async (phone: string, email?: string | null, excludeId?: string | null) => {
    if (!phone || phone.trim().length < 6) {
      setDuplicateWarning(null);
      return;
    }
    setIsCheckingDuplicate(true);
    try {
      const res = await customerApi.checkDuplicate(phone.trim(), email?.trim() || null, excludeId);
      if (res.isDuplicate) {
        setDuplicateWarning(res);
      } else {
        setDuplicateWarning(null);
      }
    } catch {
      // Ignore background check errors
    } finally {
      setIsCheckingDuplicate(false);
    }
  };

  // Open Add Modal
  const handleOpenAdd = () => {
    setFormData({
      fullName: '',
      primaryPhone: '',
      secondaryPhone: '',
      email: '',
      addressLine1: '',
      addressLine2: '',
      city: '',
      districtProvince: '',
      postalCode: '',
      country: 'Sri Lanka',
      dateOfBirth: '',
      notes: '',
    });
    setDuplicateWarning(null);
    setIsAddModalOpen(true);
  };

  // Submit Add Customer
  const handleCreateCustomer = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formData.fullName.trim()) {
      setError('Customer Full Name is required.');
      return;
    }
    if (!formData.primaryPhone.trim()) {
      setError('Primary Phone is required.');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const created = await customerApi.createCustomer(formData);
      setSuccessMessage(`Customer ${created.fullName} (${created.customerCode}) created successfully.`);
      setIsAddModalOpen(false);
      fetchCustomers();
    } catch (err: any) {
      setError(err.message || 'Failed to create customer');
    } finally {
      setSubmitting(false);
    }
  };

  // Open Edit Modal
  const handleOpenEdit = (c: Customer) => {
    setSelectedCustomer(c);
    setEditFormData({
      fullName: c.fullName,
      primaryPhone: c.primaryPhone,
      secondaryPhone: c.secondaryPhone || '',
      email: c.email || '',
      addressLine1: c.addressLine1 || '',
      addressLine2: c.addressLine2 || '',
      city: c.city || '',
      districtProvince: c.districtProvince || '',
      postalCode: c.postalCode || '',
      country: c.country || 'Sri Lanka',
      dateOfBirth: c.dateOfBirth ? c.dateOfBirth.split('T')[0] : '',
      notes: c.notes || '',
      rowVersion: c.rowVersion,
    });
    setDuplicateWarning(null);
    setIsEditModalOpen(true);
  };

  // Submit Edit Customer
  const handleUpdateCustomer = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCustomer) return;

    setSubmitting(true);
    setError(null);
    try {
      const updated = await customerApi.updateCustomer(selectedCustomer.id, editFormData);
      setSuccessMessage(`Customer ${updated.fullName} updated successfully.`);
      setIsEditModalOpen(false);
      if (selectedCustomer.id === updated.id) {
        setSelectedCustomer(updated);
      }
      fetchCustomers();
    } catch (err: any) {
      if (err.message && err.message.includes('modified by another user')) {
        setError('Concurrency Conflict: This record was modified by another user. Please refresh and review.');
      } else {
        setError(err.message || 'Failed to update customer');
      }
    } finally {
      setSubmitting(false);
    }
  };

  // Open Detail Drawer
  const handleOpenDetail = (c: Customer) => {
    setSelectedCustomer(c);
    setDetailTab('profile');
    setIsDetailDrawerOpen(true);
  };

  // Open Status Modal
  const handleOpenStatusModal = (c: Customer) => {
    setSelectedCustomer(c);
    setStatusReason('');
    setIsStatusModalOpen(true);
  };

  // Toggle Status
  const handleToggleStatus = async () => {
    if (!selectedCustomer) return;

    setSubmitting(true);
    setError(null);
    try {
      const newStatus = !selectedCustomer.isActive;
      const updated = await customerApi.updateCustomerStatus(selectedCustomer.id, {
        isActive: newStatus,
        reason: statusReason.trim() || undefined,
      });
      setSuccessMessage(
        `Customer ${updated.fullName} is now ${newStatus ? 'Active' : 'Inactive'}.`
      );
      setIsStatusModalOpen(false);
      setSelectedCustomer(updated);
      fetchCustomers();
    } catch (err: any) {
      setError(err.message || 'Failed to update customer status');
    } finally {
      setSubmitting(false);
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  return (
    <AppLayout>
      <div className="p-6 max-w-7xl mx-auto space-y-6">
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 pb-4 border-b border-gray-200">
          <div>
            <h1 className="text-2xl font-bold text-[#0D3826] flex items-center gap-2">
              <span>👤</span> Customer Directory
            </h1>
            <p className="text-sm text-gray-600 mt-1">
              Centralized customer registry for POS sales, custom orders, repairs, and transaction history.
            </p>
          </div>
          <div className="flex items-center gap-3">
            <button
              onClick={handleOpenAdd}
              className="inline-flex items-center gap-2 px-4 py-2.5 bg-[#0D3826] hover:bg-[#134e35] text-white text-sm font-semibold rounded-lg shadow-sm transition border border-[#D4AF37]/40"
            >
              <span>+</span> Add Customer
            </button>
          </div>
        </div>

        {/* Alerts */}
        {error && (
          <div className="p-4 bg-red-50 border-l-4 border-red-500 rounded-md flex items-start justify-between">
            <div className="flex items-center gap-2 text-sm text-red-700">
              <span className="font-bold">Error:</span> {error}
            </div>
            <button
              onClick={() => setError(null)}
              className="text-red-500 hover:text-red-700 font-bold ml-4"
            >
              ×
            </button>
          </div>
        )}

        {successMessage && (
          <div className="p-4 bg-green-50 border-l-4 border-green-500 rounded-md flex items-start justify-between">
            <div className="flex items-center gap-2 text-sm text-green-700">
              <span className="font-bold">Success:</span> {successMessage}
            </div>
            <button
              onClick={() => setSuccessMessage(null)}
              className="text-green-500 hover:text-green-700 font-bold ml-4"
            >
              ×
            </button>
          </div>
        )}

        {/* Search & Filters */}
        <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm flex flex-col md:flex-row gap-4 items-center justify-between">
          <div className="w-full md:w-96 relative">
            <input
              type="text"
              placeholder="Search by code (CUS-000001), name, phone, email..."
              value={searchTerm}
              onChange={(e) => {
                setSearchTerm(e.target.value);
                setPage(1);
              }}
              className="w-full pl-9 pr-4 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#0D3826] focus:border-transparent"
            />
            <span className="absolute left-3 top-2.5 text-gray-400 text-sm">🔍</span>
          </div>

          <div className="flex items-center gap-2 w-full md:w-auto">
            <span className="text-xs font-semibold uppercase text-gray-500 tracking-wider">Status:</span>
            <div className="inline-flex rounded-lg border border-gray-200 bg-gray-50 p-1 text-xs font-medium">
              <button
                onClick={() => {
                  setActiveFilter('all');
                  setPage(1);
                }}
                className={`px-3 py-1.5 rounded-md transition ${
                  activeFilter === 'all'
                    ? 'bg-white shadow text-[#0D3826] font-bold'
                    : 'text-gray-600 hover:text-gray-900'
                }`}
              >
                All
              </button>
              <button
                onClick={() => {
                  setActiveFilter('active');
                  setPage(1);
                }}
                className={`px-3 py-1.5 rounded-md transition ${
                  activeFilter === 'active'
                    ? 'bg-white shadow text-[#0D3826] font-bold'
                    : 'text-gray-600 hover:text-gray-900'
                }`}
              >
                Active
              </button>
              <button
                onClick={() => {
                  setActiveFilter('inactive');
                  setPage(1);
                }}
                className={`px-3 py-1.5 rounded-md transition ${
                  activeFilter === 'inactive'
                    ? 'bg-white shadow text-[#0D3826] font-bold'
                    : 'text-gray-600 hover:text-gray-900'
                }`}
              >
                Inactive
              </button>
            </div>
          </div>
        </div>

        {/* Customer Table */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-gray-600">
              <thead className="bg-[#0D3826]/5 text-[#0D3826] font-semibold text-xs uppercase tracking-wider border-b border-gray-200">
                <tr>
                  <th className="py-3.5 px-4">Customer Code</th>
                  <th className="py-3.5 px-4">Full Name</th>
                  <th className="py-3.5 px-4">Primary Phone</th>
                  <th className="py-3.5 px-4">Email</th>
                  <th className="py-3.5 px-4">Location</th>
                  <th className="py-3.5 px-4">Status</th>
                  <th className="py-3.5 px-4">Created Date</th>
                  <th className="py-3.5 px-4 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {loading ? (
                  <tr>
                    <td colSpan={8} className="text-center py-12 text-gray-500">
                      <div className="inline-block w-6 h-6 border-2 border-[#0D3826] border-t-transparent rounded-full animate-spin mr-2"></div>
                      Loading customers...
                    </td>
                  </tr>
                ) : customers.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="text-center py-12">
                      <p className="text-gray-500 font-medium">No customers found</p>
                      <p className="text-xs text-gray-400 mt-1">
                        Try adjusting your search query or add your first customer.
                      </p>
                    </td>
                  </tr>
                ) : (
                  customers.map((c) => (
                    <tr key={c.id} className="hover:bg-gray-50/80 transition">
                      <td className="py-3 px-4 font-mono font-bold text-[#0D3826]">
                        <span className="bg-[#D4AF37]/15 text-[#0D3826] px-2 py-0.5 rounded text-xs border border-[#D4AF37]/30">
                          {c.customerCode}
                        </span>
                      </td>
                      <td className="py-3 px-4 font-medium text-gray-900">
                        {c.fullName}
                      </td>
                      <td className="py-3 px-4 font-mono text-xs text-gray-700">
                        {c.primaryPhone}
                      </td>
                      <td className="py-3 px-4 text-xs text-gray-500">
                        {c.email || '—'}
                      </td>
                      <td className="py-3 px-4 text-xs text-gray-600">
                        {[c.city, c.country].filter(Boolean).join(', ') || '—'}
                      </td>
                      <td className="py-3 px-4">
                        {c.isActive ? (
                          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
                            Active
                          </span>
                        ) : (
                          <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-gray-100 text-gray-600 border border-gray-200">
                            Inactive
                          </span>
                        )}
                      </td>
                      <td className="py-3 px-4 text-xs text-gray-500">
                        {new Date(c.createdAt).toLocaleDateString()}
                      </td>
                      <td className="py-3 px-4 text-right space-x-2">
                        <button
                          onClick={() => handleOpenDetail(c)}
                          className="px-2.5 py-1 text-xs font-medium text-[#0D3826] bg-[#0D3826]/10 hover:bg-[#0D3826]/20 rounded transition"
                        >
                          View
                        </button>
                        <button
                          onClick={() => handleOpenEdit(c)}
                          className="px-2.5 py-1 text-xs font-medium text-amber-800 bg-amber-50 hover:bg-amber-100 rounded transition"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => handleOpenStatusModal(c)}
                          className={`px-2.5 py-1 text-xs font-medium rounded transition ${
                            c.isActive
                              ? 'text-red-700 bg-red-50 hover:bg-red-100'
                              : 'text-emerald-700 bg-emerald-50 hover:bg-emerald-100'
                          }`}
                        >
                          {c.isActive ? 'Deactivate' : 'Activate'}
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          <div className="p-4 bg-gray-50 border-t border-gray-200 flex flex-col sm:flex-row items-center justify-between text-xs text-gray-500 gap-3">
            <div>
              Showing {customers.length > 0 ? (page - 1) * pageSize + 1 : 0} to{' '}
              {Math.min(page * pageSize, totalCount)} of {totalCount} customers
            </div>
            <div className="flex items-center gap-1">
              <button
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="px-3 py-1.5 border border-gray-300 rounded hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Previous
              </button>
              <span className="px-2 font-semibold text-gray-700">
                Page {page} of {totalPages}
              </span>
              <button
                disabled={page >= totalPages}
                onClick={() => setPage((p) => p + 1)}
                className="px-3 py-1.5 border border-gray-300 rounded hover:bg-white disabled:opacity-40 disabled:cursor-not-allowed"
              >
                Next
              </button>
            </div>
          </div>
        </div>

        {/* ----------------- ADD CUSTOMER MODAL ----------------- */}
        {isAddModalOpen && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-2xl w-full shadow-2xl overflow-hidden border border-[#D4AF37]/30 my-8">
              <div className="bg-[#0D3826] text-white px-6 py-4 flex items-center justify-between">
                <div>
                  <h3 className="text-lg font-bold">Add New Customer</h3>
                  <p className="text-xs text-gray-300 mt-0.5">
                    Customer Code will be generated automatically as CUS-XXXXXX
                  </p>
                </div>
                <button
                  onClick={() => setIsAddModalOpen(false)}
                  className="text-gray-300 hover:text-white font-bold text-xl"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleCreateCustomer} className="p-6 space-y-6">
                {/* Duplicate Warning Banner */}
                {duplicateWarning && (
                  <div className="p-3 bg-amber-50 border-l-4 border-amber-500 rounded-md">
                    <p className="text-xs font-semibold text-amber-800">
                      ⚠️ Duplicate Alert: {duplicateWarning.duplicateReason}
                    </p>
                    <p className="text-xs text-amber-700 mt-0.5">
                      Please verify before proceeding to prevent duplicate profiles.
                    </p>
                  </div>
                )}

                {/* Section 1: Basic Identity */}
                <div className="space-y-4">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-[#0D3826] border-b pb-1">
                    Basic Identity & Contact (Required)
                  </h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Full Name <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="text"
                        required
                        placeholder="e.g. Priyantha Jayawardena"
                        value={formData.fullName}
                        onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Primary Phone <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="tel"
                        required
                        placeholder="e.g. 077 123 4567 or +94771234567"
                        value={formData.primaryPhone}
                        onChange={(e) => setFormData({ ...formData, primaryPhone: e.target.value })}
                        onBlur={() => handleCheckDuplicate(formData.primaryPhone, formData.email)}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                      {isCheckingDuplicate && (
                        <p className="text-[10px] text-gray-400 mt-0.5">Checking phone duplicates...</p>
                      )}
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Secondary Phone (Optional)
                      </label>
                      <input
                        type="tel"
                        placeholder="e.g. 011 234 5678"
                        value={formData.secondaryPhone || ''}
                        onChange={(e) => setFormData({ ...formData, secondaryPhone: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Email Address (Optional)
                      </label>
                      <input
                        type="email"
                        placeholder="e.g. priyantha@example.com"
                        value={formData.email || ''}
                        onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                        onBlur={() => handleCheckDuplicate(formData.primaryPhone, formData.email)}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>
                  </div>
                </div>

                {/* Section 2: Address Information */}
                <div className="space-y-4">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-[#0D3826] border-b pb-1">
                    Address Details (Optional)
                  </h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Address Line 1
                      </label>
                      <input
                        type="text"
                        placeholder="Street or Building"
                        value={formData.addressLine1 || ''}
                        onChange={(e) => setFormData({ ...formData, addressLine1: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Address Line 2
                      </label>
                      <input
                        type="text"
                        placeholder="Apartment, suite, unit"
                        value={formData.addressLine2 || ''}
                        onChange={(e) => setFormData({ ...formData, addressLine2: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">City</label>
                      <input
                        type="text"
                        placeholder="e.g. Jaffna, Colombo"
                        value={formData.city || ''}
                        onChange={(e) => setFormData({ ...formData, city: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        District / Province
                      </label>
                      <input
                        type="text"
                        placeholder="e.g. Northern Province"
                        value={formData.districtProvince || ''}
                        onChange={(e) =>
                          setFormData({ ...formData, districtProvince: e.target.value })
                        }
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Postal Code
                      </label>
                      <input
                        type="text"
                        placeholder="e.g. 40000"
                        value={formData.postalCode || ''}
                        onChange={(e) => setFormData({ ...formData, postalCode: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">Country</label>
                      <input
                        type="text"
                        value={formData.country || 'Sri Lanka'}
                        onChange={(e) => setFormData({ ...formData, country: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>
                  </div>
                </div>

                {/* Section 3: Personal & Notes */}
                <div className="space-y-4">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-[#0D3826] border-b pb-1">
                    Optional Profile & Notes
                  </h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Date of Birth
                      </label>
                      <input
                        type="date"
                        value={formData.dateOfBirth || ''}
                        onChange={(e) => setFormData({ ...formData, dateOfBirth: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Internal Customer Notes
                      </label>
                      <textarea
                        rows={2}
                        placeholder="Preferences, family relationships, special requests..."
                        value={formData.notes || ''}
                        onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>
                  </div>
                </div>

                {/* Actions */}
                <div className="pt-4 border-t flex items-center justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setIsAddModalOpen(false)}
                    className="px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-100 rounded-lg transition"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={submitting}
                    className="px-6 py-2 bg-[#0D3826] hover:bg-[#134e35] text-white text-sm font-semibold rounded-lg shadow transition disabled:opacity-50"
                  >
                    {submitting ? 'Saving...' : 'Save Customer'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* ----------------- EDIT CUSTOMER MODAL ----------------- */}
        {isEditModalOpen && selectedCustomer && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-2xl w-full shadow-2xl overflow-hidden border border-[#D4AF37]/30 my-8">
              <div className="bg-[#0D3826] text-white px-6 py-4 flex items-center justify-between">
                <div>
                  <h3 className="text-lg font-bold">Edit Customer</h3>
                  <p className="text-xs text-amber-300 font-mono mt-0.5">
                    {selectedCustomer.customerCode} (Immutable Identifier)
                  </p>
                </div>
                <button
                  onClick={() => setIsEditModalOpen(false)}
                  className="text-gray-300 hover:text-white font-bold text-xl"
                >
                  ×
                </button>
              </div>

              <form onSubmit={handleUpdateCustomer} className="p-6 space-y-6">
                {/* Section 1: Basic Identity */}
                <div className="space-y-4">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-[#0D3826] border-b pb-1">
                    Customer Information
                  </h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Full Name <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="text"
                        required
                        value={editFormData.fullName}
                        onChange={(e) => setEditFormData({ ...editFormData, fullName: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Primary Phone <span className="text-red-500">*</span>
                      </label>
                      <input
                        type="tel"
                        required
                        value={editFormData.primaryPhone}
                        onChange={(e) =>
                          setEditFormData({ ...editFormData, primaryPhone: e.target.value })
                        }
                        onBlur={() =>
                          handleCheckDuplicate(
                            editFormData.primaryPhone,
                            editFormData.email,
                            selectedCustomer.id
                          )
                        }
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Secondary Phone (Optional)
                      </label>
                      <input
                        type="tel"
                        value={editFormData.secondaryPhone || ''}
                        onChange={(e) =>
                          setEditFormData({ ...editFormData, secondaryPhone: e.target.value })
                        }
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Email Address (Optional)
                      </label>
                      <input
                        type="email"
                        value={editFormData.email || ''}
                        onChange={(e) => setEditFormData({ ...editFormData, email: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>
                  </div>
                </div>

                {/* Section 2: Address */}
                <div className="space-y-4">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-[#0D3826] border-b pb-1">
                    Address Details
                  </h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Address Line 1
                      </label>
                      <input
                        type="text"
                        value={editFormData.addressLine1 || ''}
                        onChange={(e) =>
                          setEditFormData({ ...editFormData, addressLine1: e.target.value })
                        }
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Address Line 2
                      </label>
                      <input
                        type="text"
                        value={editFormData.addressLine2 || ''}
                        onChange={(e) =>
                          setEditFormData({ ...editFormData, addressLine2: e.target.value })
                        }
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">City</label>
                      <input
                        type="text"
                        value={editFormData.city || ''}
                        onChange={(e) => setEditFormData({ ...editFormData, city: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        District / Province
                      </label>
                      <input
                        type="text"
                        value={editFormData.districtProvince || ''}
                        onChange={(e) =>
                          setEditFormData({ ...editFormData, districtProvince: e.target.value })
                        }
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Postal Code
                      </label>
                      <input
                        type="text"
                        value={editFormData.postalCode || ''}
                        onChange={(e) =>
                          setEditFormData({ ...editFormData, postalCode: e.target.value })
                        }
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">Country</label>
                      <input
                        type="text"
                        value={editFormData.country || 'Sri Lanka'}
                        onChange={(e) => setEditFormData({ ...editFormData, country: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>
                  </div>
                </div>

                {/* Section 3: Personal & Notes */}
                <div className="space-y-4">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-[#0D3826] border-b pb-1">
                    Profile & Notes
                  </h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Date of Birth
                      </label>
                      <input
                        type="date"
                        value={editFormData.dateOfBirth || ''}
                        onChange={(e) =>
                          setEditFormData({ ...editFormData, dateOfBirth: e.target.value })
                        }
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>

                    <div className="md:col-span-2">
                      <label className="block text-xs font-semibold text-gray-700 mb-1">
                        Internal Notes
                      </label>
                      <textarea
                        rows={2}
                        value={editFormData.notes || ''}
                        onChange={(e) => setEditFormData({ ...editFormData, notes: e.target.value })}
                        className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                      />
                    </div>
                  </div>
                </div>

                {/* Actions */}
                <div className="pt-4 border-t flex items-center justify-end gap-3">
                  <button
                    type="button"
                    onClick={() => setIsEditModalOpen(false)}
                    className="px-4 py-2 text-sm font-semibold text-gray-700 hover:bg-gray-100 rounded-lg transition"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={submitting}
                    className="px-6 py-2 bg-[#0D3826] hover:bg-[#134e35] text-white text-sm font-semibold rounded-lg shadow transition disabled:opacity-50"
                  >
                    {submitting ? 'Updating...' : 'Save Changes'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* ----------------- CUSTOMER DETAIL DRAWER / SLIDE-OVER ----------------- */}
        {isDetailDrawerOpen && selectedCustomer && (
          <div className="fixed inset-0 z-50 overflow-hidden bg-black/50 backdrop-blur-sm flex justify-end">
            <div className="w-full max-w-2xl bg-white shadow-2xl h-full flex flex-col overflow-hidden">
              {/* Drawer Header */}
              <div className="bg-[#0D3826] text-white p-6 flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <span className="bg-[#D4AF37] text-[#0D3826] text-xs font-mono font-bold px-2 py-0.5 rounded">
                      {selectedCustomer.customerCode}
                    </span>
                    <span
                      className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                        selectedCustomer.isActive
                          ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40'
                          : 'bg-gray-500/20 text-gray-300 border border-gray-500/40'
                      }`}
                    >
                      {selectedCustomer.isActive ? 'ACTIVE' : 'INACTIVE'}
                    </span>
                  </div>
                  <h2 className="text-xl font-bold mt-2">{selectedCustomer.fullName}</h2>
                  <p className="text-xs text-gray-300 mt-1 font-mono">
                    📞 {selectedCustomer.primaryPhone}
                    {selectedCustomer.email && ` • ✉️ ${selectedCustomer.email}`}
                  </p>
                </div>
                <button
                  onClick={() => setIsDetailDrawerOpen(false)}
                  className="text-gray-300 hover:text-white text-2xl font-bold"
                >
                  ×
                </button>
              </div>

              {/* Tabs */}
              <div className="border-b border-gray-200 bg-gray-50 px-6 flex space-x-4 overflow-x-auto text-xs font-semibold">
                <button
                  onClick={() => setDetailTab('profile')}
                  className={`py-3 border-b-2 whitespace-nowrap transition ${
                    detailTab === 'profile'
                      ? 'border-[#0D3826] text-[#0D3826] font-bold'
                      : 'border-transparent text-gray-500 hover:text-gray-900'
                  }`}
                >
                  👤 Profile & Info
                </button>
                <button
                  onClick={() => setDetailTab('purchases')}
                  className={`py-3 border-b-2 whitespace-nowrap transition ${
                    detailTab === 'purchases'
                      ? 'border-[#0D3826] text-[#0D3826] font-bold'
                      : 'border-transparent text-gray-500 hover:text-gray-900'
                  }`}
                >
                  🧾 Purchases (POS)
                </button>
                <button
                  onClick={() => setDetailTab('orders')}
                  className={`py-3 border-b-2 whitespace-nowrap transition ${
                    detailTab === 'orders'
                      ? 'border-[#0D3826] text-[#0D3826] font-bold'
                      : 'border-transparent text-gray-500 hover:text-gray-900'
                  }`}
                >
                  💍 Custom Orders
                </button>
                <button
                  onClick={() => setDetailTab('repairs')}
                  className={`py-3 border-b-2 whitespace-nowrap transition ${
                    detailTab === 'repairs'
                      ? 'border-[#0D3826] text-[#0D3826] font-bold'
                      : 'border-transparent text-gray-500 hover:text-gray-900'
                  }`}
                >
                  🔧 Repairs
                </button>
                <button
                  onClick={() => setDetailTab('oldGold')}
                  className={`py-3 border-b-2 whitespace-nowrap transition ${
                    detailTab === 'oldGold'
                      ? 'border-[#0D3826] text-[#0D3826] font-bold'
                      : 'border-transparent text-gray-500 hover:text-gray-900'
                  }`}
                >
                  🪙 Old Gold
                </button>
                <button
                  onClick={() => setDetailTab('payments')}
                  className={`py-3 border-b-2 whitespace-nowrap transition ${
                    detailTab === 'payments'
                      ? 'border-[#0D3826] text-[#0D3826] font-bold'
                      : 'border-transparent text-gray-500 hover:text-gray-900'
                  }`}
                >
                  💵 Payments
                </button>
              </div>

              {/* Tab Content */}
              <div className="flex-1 overflow-y-auto p-6 space-y-6">
                {detailTab === 'profile' && (
                  <div className="space-y-6">
                    {/* Identity & Contact Card */}
                    <div className="bg-gray-50 rounded-xl p-4 border border-gray-200 space-y-3">
                      <h4 className="text-xs font-bold text-gray-500 uppercase tracking-wider">
                        Contact Details
                      </h4>
                      <div className="grid grid-cols-2 gap-4 text-xs">
                        <div>
                          <span className="text-gray-500 block">Primary Phone</span>
                          <span className="font-mono font-medium text-gray-900">
                            {selectedCustomer.primaryPhone}
                          </span>
                        </div>
                        <div>
                          <span className="text-gray-500 block">Secondary Phone</span>
                          <span className="font-mono font-medium text-gray-900">
                            {selectedCustomer.secondaryPhone || 'None'}
                          </span>
                        </div>
                        <div>
                          <span className="text-gray-500 block">Email Address</span>
                          <span className="font-medium text-gray-900">
                            {selectedCustomer.email || 'None'}
                          </span>
                        </div>
                        <div>
                          <span className="text-gray-500 block">Date of Birth</span>
                          <span className="font-medium text-gray-900">
                            {selectedCustomer.dateOfBirth
                              ? new Date(selectedCustomer.dateOfBirth).toLocaleDateString()
                              : 'Not Specified'}
                          </span>
                        </div>
                      </div>
                    </div>

                    {/* Address Card */}
                    <div className="bg-gray-50 rounded-xl p-4 border border-gray-200 space-y-3">
                      <h4 className="text-xs font-bold text-gray-500 uppercase tracking-wider">
                        Address Information
                      </h4>
                      <div className="text-xs text-gray-800 space-y-1">
                        <p>{selectedCustomer.addressLine1 || 'No street address specified'}</p>
                        {selectedCustomer.addressLine2 && <p>{selectedCustomer.addressLine2}</p>}
                        <p>
                          {[
                            selectedCustomer.city,
                            selectedCustomer.districtProvince,
                            selectedCustomer.postalCode,
                          ]
                            .filter(Boolean)
                            .join(', ')}
                        </p>
                        <p className="font-semibold">{selectedCustomer.country || 'Sri Lanka'}</p>
                      </div>
                    </div>

                    {/* Notes Card */}
                    {selectedCustomer.notes && (
                      <div className="bg-amber-50/60 rounded-xl p-4 border border-amber-200 space-y-2">
                        <h4 className="text-xs font-bold text-amber-900 uppercase tracking-wider">
                          Internal Notes
                        </h4>
                        <p className="text-xs text-amber-900 whitespace-pre-wrap">
                          {selectedCustomer.notes}
                        </p>
                      </div>
                    )}

                    {/* System Audit Info */}
                    <div className="text-[11px] text-gray-400 border-t pt-4 space-y-1">
                      <p>Customer ID: {selectedCustomer.id}</p>
                      <p>Registered: {new Date(selectedCustomer.createdAt).toLocaleString()}</p>
                      {selectedCustomer.updatedAt && (
                        <p>Last Modified: {new Date(selectedCustomer.updatedAt).toLocaleString()}</p>
                      )}
                    </div>
                  </div>
                )}

                {/* Clean Empty States for Future Transaction Tabs */}
                {detailTab !== 'profile' && (
                  <div className="py-16 text-center space-y-3">
                    <div className="text-4xl">
                      {detailTab === 'purchases' && '🧾'}
                      {detailTab === 'orders' && '💍'}
                      {detailTab === 'repairs' && '🔧'}
                      {detailTab === 'oldGold' && '🪙'}
                      {detailTab === 'payments' && '💵'}
                    </div>
                    <h3 className="text-sm font-bold text-gray-800">
                      No {detailTab.charAt(0).toUpperCase() + detailTab.slice(1)} Recorded Yet
                    </h3>
                    <p className="text-xs text-gray-500 max-w-sm mx-auto">
                      Transactions will automatically populate here as POS sales, custom repair orders, and gold purchases are processed for {selectedCustomer.fullName} in future modules.
                    </p>
                  </div>
                )}
              </div>

              {/* Drawer Footer Actions */}
              <div className="p-4 border-t bg-gray-50 flex items-center justify-between">
                <button
                  onClick={() => {
                    setIsDetailDrawerOpen(false);
                    handleOpenStatusModal(selectedCustomer);
                  }}
                  className={`px-3 py-1.5 text-xs font-semibold rounded-lg transition ${
                    selectedCustomer.isActive
                      ? 'text-red-700 bg-red-100 hover:bg-red-200'
                      : 'text-emerald-700 bg-emerald-100 hover:bg-emerald-200'
                  }`}
                >
                  {selectedCustomer.isActive ? 'Deactivate Customer' : 'Activate Customer'}
                </button>
                <div className="flex gap-2">
                  <button
                    onClick={() => {
                      setIsDetailDrawerOpen(false);
                      handleOpenEdit(selectedCustomer);
                    }}
                    className="px-4 py-2 bg-[#0D3826] text-white text-xs font-semibold rounded-lg hover:bg-[#134e35] transition"
                  >
                    Edit Profile
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* ----------------- STATUS TOGGLE MODAL ----------------- */}
        {isStatusModalOpen && selectedCustomer && (
          <div className="fixed inset-0 z-50 bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-md w-full shadow-2xl p-6 border border-gray-200 space-y-4">
              <h3 className="text-lg font-bold text-gray-900">
                {selectedCustomer.isActive ? 'Deactivate Customer' : 'Activate Customer'}
              </h3>
              <p className="text-xs text-gray-600">
                Are you sure you want to {selectedCustomer.isActive ? 'deactivate' : 'activate'}{' '}
                <span className="font-semibold text-gray-900">{selectedCustomer.fullName}</span> (
                {selectedCustomer.customerCode})?
              </p>
              {selectedCustomer.isActive && (
                <p className="text-xs text-amber-700 bg-amber-50 p-2.5 rounded border border-amber-200">
                  Note: Deactivated customers cannot be selected for new sales or orders in POS, but historical records are permanently preserved.
                </p>
              )}

              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">
                  Reason (Optional)
                </label>
                <input
                  type="text"
                  placeholder="e.g. Customer requested deactivation"
                  value={statusReason}
                  onChange={(e) => setStatusReason(e.target.value)}
                  className="w-full px-3 py-2 text-sm border rounded-lg focus:ring-2 focus:ring-[#0D3826] focus:outline-none"
                />
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setIsStatusModalOpen(false)}
                  className="px-4 py-2 text-xs font-semibold text-gray-600 hover:bg-gray-100 rounded-lg"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  disabled={submitting}
                  onClick={handleToggleStatus}
                  className={`px-4 py-2 text-xs font-semibold text-white rounded-lg shadow transition ${
                    selectedCustomer.isActive
                      ? 'bg-red-600 hover:bg-red-700'
                      : 'bg-emerald-600 hover:bg-emerald-700'
                  }`}
                >
                  {submitting
                    ? 'Updating...'
                    : selectedCustomer.isActive
                    ? 'Confirm Deactivation'
                    : 'Confirm Activation'}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
