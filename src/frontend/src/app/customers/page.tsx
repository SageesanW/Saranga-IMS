'use client';

import React, { useState, useEffect, useCallback } from 'react';
import AppLayout from '@/components/AppLayout';
import {
  Customer,
  CreateCustomerRequest,
  UpdateCustomerRequest,
  customerApi,
  CustomerDuplicateCheckResult,
} from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Textarea } from '@/components/ui/Textarea';
import { Modal } from '@/components/ui/Modal';
import { Table } from '@/components/ui/Table';
import { Badge } from '@/components/ui/Badge';
import { Pagination } from '@/components/ui/Pagination';
import { EmptyState } from '@/components/ui/EmptyState';
import {
  UserPlus,
  Search,
  Users,
  Edit,
  Power,
  Eye,
  CheckCircle2,
  AlertTriangle,
  X,
  Phone,
  Mail,
  MapPin,
  Calendar,
  FileText,
} from 'lucide-react';

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
  const [detailTab, setDetailTab] = useState<'profile' | 'purchases' | 'orders' | 'repairs' | 'payments'>('profile');

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

  // Automatic duplicate check on phone blur
  const checkDuplicatePhone = async (phone: string, excludeId?: string) => {
    if (!phone || phone.length < 9) return;
    setIsCheckingDuplicate(true);
    try {
      const res = await customerApi.checkDuplicate(phone, undefined, excludeId);
      if (res.isDuplicate) {
        setDuplicateWarning(res);
      } else {
        setDuplicateWarning(null);
      }
    } catch (err) {
      console.error('Duplicate check error:', err);
    } finally {
      setIsCheckingDuplicate(false);
    }
  };

  const handleCreateCustomer = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      await customerApi.createCustomer(formData);
      setIsAddModalOpen(false);
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
      setSuccessMessage('Customer created successfully.');
      fetchCustomers();
    } catch (err: any) {
      setError(err.message || 'Failed to create customer');
    } finally {
      setSubmitting(false);
    }
  };

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
      dateOfBirth: c.dateOfBirth ? c.dateOfBirth.substring(0, 10) : '',
      notes: c.notes || '',
      rowVersion: c.rowVersion,
    });
    setDuplicateWarning(null);
    setIsEditModalOpen(true);
  };

  const handleUpdateCustomer = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCustomer) return;
    setSubmitting(true);
    setError(null);
    try {
      await customerApi.updateCustomer(selectedCustomer.id, editFormData);
      setIsEditModalOpen(false);
      setSuccessMessage('Customer details updated successfully.');
      fetchCustomers();
    } catch (err: any) {
      setError(err.message || 'Failed to update customer');
    } finally {
      setSubmitting(false);
    }
  };

  const handleOpenStatusModal = (c: Customer) => {
    setSelectedCustomer(c);
    setStatusReason('');
    setIsStatusModalOpen(true);
  };

  const handleToggleStatus = async () => {
    if (!selectedCustomer) return;
    if (!statusReason.trim()) {
      setError('A mandatory reason is required to modify customer status.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await customerApi.updateCustomerStatus(selectedCustomer.id, {
        isActive: !selectedCustomer.isActive,
        reason: statusReason,
      });
      setSuccessMessage(`Customer ${selectedCustomer.fullName} status updated.`);
      setIsStatusModalOpen(false);
      fetchCustomers();
    } catch (err: any) {
      setError(err.message || 'Failed to toggle customer status');
    } finally {
      setSubmitting(false);
    }
  };

  const handleViewDetail = (c: Customer) => {
    setSelectedCustomer(c);
    setDetailTab('profile');
    setIsDetailDrawerOpen(true);
  };

  return (
    <AppLayout>
      <PageHeader
        title="Customer Directory"
        description="Manage customer profiles, E.164 canonical contact records, and transactional purchase history."
        actions={
          <Button
            variant="primary"
            size="sm"
            onClick={() => {
              setDuplicateWarning(null);
              setIsAddModalOpen(true);
            }}
            leftIcon={<UserPlus className="w-4 h-4" />}
          >
            Add Customer
          </Button>
        }
      />

      {/* Notifications */}
      {successMessage && (
        <div className="mb-4 p-3 rounded-lg bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-600 flex-shrink-0" />
            <span>{successMessage}</span>
          </div>
          <button onClick={() => setSuccessMessage(null)} className="text-emerald-700 hover:text-emerald-900">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {error && (
        <div className="mb-4 p-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-800 text-xs flex items-center justify-between">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 text-rose-600 flex-shrink-0" />
            <span>{error}</span>
          </div>
          <button onClick={() => setError(null)} className="text-rose-700 hover:text-rose-900">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {/* Search & Filter Toolbar */}
      <div className="bg-white p-4 rounded-xl border border-[#E2E6E3] shadow-xs mb-5 flex flex-col sm:flex-row items-center justify-between gap-3">
        <div className="w-full sm:w-96 relative">
          <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-[#69736E]">
            <Search className="w-4 h-4" />
          </div>
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder="Search by name, phone or customer code"
            className="w-full pl-9 pr-3.5 py-1.5 text-xs rounded-lg border border-[#E2E6E3] focus:outline-none focus:ring-1 focus:ring-[#123B32] focus:border-[#123B32] text-[#202824] placeholder-gray-400"
          />
        </div>

        <div className="flex items-center gap-2 w-full sm:w-auto justify-end">
          <span className="text-xs font-semibold text-[#69736E]">Status:</span>
          <div className="inline-flex rounded-lg border border-[#E2E6E3] p-0.5 bg-[#F8F7F4]">
            {(['active', 'all', 'inactive'] as const).map((filter) => (
              <button
                key={filter}
                onClick={() => {
                  setActiveFilter(filter);
                  setPage(1);
                }}
                className={`px-3 py-1 text-xs font-medium rounded-md capitalize transition-colors ${
                  activeFilter === filter
                    ? 'bg-white text-[#123B32] font-semibold shadow-xs'
                    : 'text-[#69736E] hover:text-[#202824]'
                }`}
              >
                {filter}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Customer Data Table */}
      {loading ? (
        <div className="bg-white p-12 rounded-xl border border-[#E2E6E3] text-center">
          <div className="w-8 h-8 border-2 border-[#123B32] border-t-transparent rounded-full animate-spin mx-auto mb-3" />
          <p className="text-xs text-[#69736E]">Loading customer records...</p>
        </div>
      ) : customers.length === 0 ? (
        <EmptyState
          title="No Customers Found"
          description={
            searchTerm
              ? `No customer profiles matching "${searchTerm}". Try a different name, phone or code.`
              : 'No customer profiles are registered yet. Add your first customer to begin.'
          }
          icon={<Users className="w-6 h-6 text-[#69736E]" />}
          actionLabel="Add Customer"
          onAction={() => setIsAddModalOpen(true)}
        />
      ) : (
        <div className="space-y-4">
          <Table
            headers={[
              'Customer Code',
              'Full Name',
              'Primary Phone',
              'City / Region',
              'Registered Date',
              'Status',
              'Actions',
            ]}
          >
            {customers.map((c) => (
              <tr key={c.id} className="hover:bg-[#F8F7F4]/60 transition-colors">
                <td className="px-4 py-3 font-mono font-medium text-xs text-[#202824]">
                  {c.customerCode}
                </td>
                <td className="px-4 py-3 font-semibold text-xs text-[#123B32]">
                  {c.fullName}
                </td>
                <td className="px-4 py-3 text-xs font-mono text-[#202824] tabular-nums">
                  {c.primaryPhone}
                </td>
                <td className="px-4 py-3 text-xs text-[#69736E]">
                  {c.city || '—'}
                  {c.districtProvince ? `, ${c.districtProvince}` : ''}
                </td>
                <td className="px-4 py-3 text-xs text-[#69736E] tabular-nums">
                  {new Date(c.createdAt).toLocaleDateString('en-US', {
                    year: 'numeric',
                    month: 'short',
                    day: 'numeric',
                  })}
                </td>
                <td className="px-4 py-3">
                  <Badge variant={c.isActive ? 'success' : 'neutral'} size="sm">
                    {c.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </td>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-1.5">
                    <button
                      onClick={() => handleViewDetail(c)}
                      title="View Profile & History"
                      className="p-1 rounded text-[#69736E] hover:text-[#123B32] hover:bg-[#F8F7F4] transition-colors"
                    >
                      <Eye className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => handleOpenEdit(c)}
                      title="Edit Customer"
                      className="p-1 rounded text-[#69736E] hover:text-[#123B32] hover:bg-[#F8F7F4] transition-colors"
                    >
                      <Edit className="w-4 h-4" />
                    </button>
                    <button
                      onClick={() => handleOpenStatusModal(c)}
                      title={c.isActive ? 'Deactivate Customer' : 'Activate Customer'}
                      className={`p-1 rounded transition-colors ${
                        c.isActive
                          ? 'text-rose-600 hover:bg-rose-50'
                          : 'text-emerald-700 hover:bg-emerald-50'
                      }`}
                    >
                      <Power className="w-4 h-4" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </Table>

          <Pagination
            currentPage={page}
            totalCount={totalCount}
            pageSize={pageSize}
            onPageChange={setPage}
            isLoading={loading}
          />
        </div>
      )}

      {/* Add Customer Modal */}
      <Modal
        isOpen={isAddModalOpen}
        onClose={() => setIsAddModalOpen(false)}
        title="Register New Customer"
        description="Add a verified customer record. Primary phone is normalized to canonical E.164 format."
        maxWidth="2xl"
        footer={
          <>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setIsAddModalOpen(false)}
              disabled={submitting}
            >
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleCreateCustomer}
              isLoading={submitting}
            >
              Save Customer
            </Button>
          </>
        }
      >
        <form onSubmit={handleCreateCustomer} className="space-y-4">
          {duplicateWarning && duplicateWarning.isDuplicate && (
            <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-xs text-amber-800">
              <div className="flex items-start gap-2">
                <AlertTriangle className="w-4 h-4 text-amber-600 flex-shrink-0 mt-0.5" />
                <div>
                  <p className="font-semibold">Potential Duplicate Contact Identified:</p>
                  <p className="mt-0.5">
                    {duplicateWarning.duplicateReason || 'A customer with this phone number already exists in the system.'}
                  </p>
                </div>
              </div>
            </div>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Input
              label="Full Name"
              requiredIndicator
              placeholder="e.g. Arjun Rajan"
              value={formData.fullName}
              onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
              required
            />

            <Input
              label="Primary Phone"
              requiredIndicator
              placeholder="e.g. 077 123 4567"
              value={formData.primaryPhone}
              onChange={(e) => setFormData({ ...formData, primaryPhone: e.target.value })}
              onBlur={() => checkDuplicatePhone(formData.primaryPhone)}
              helperText="Normalized to canonical E.164 international format (+94...)"
              required
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Input
              label="Secondary Phone"
              placeholder="Alternative contact number"
              value={formData.secondaryPhone || ''}
              onChange={(e) => setFormData({ ...formData, secondaryPhone: e.target.value })}
            />

            <Input
              label="Email"
              type="email"
              placeholder="e.g. customer@example.com"
              value={formData.email || ''}
              onChange={(e) => setFormData({ ...formData, email: e.target.value })}
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Input
              label="Address Line 1"
              placeholder="House number, street and locality"
              value={formData.addressLine1 || ''}
              onChange={(e) => setFormData({ ...formData, addressLine1: e.target.value })}
            />

            <Input
              label="City"
              placeholder="e.g. Jaffna"
              value={formData.city || ''}
              onChange={(e) => setFormData({ ...formData, city: e.target.value })}
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <Input
              label="District / Province"
              placeholder="Select the customer's district"
              value={formData.districtProvince || ''}
              onChange={(e) => setFormData({ ...formData, districtProvince: e.target.value })}
            />

            <Input
              label="Postal Code"
              placeholder="e.g. 40000"
              value={formData.postalCode || ''}
              onChange={(e) => setFormData({ ...formData, postalCode: e.target.value })}
            />

            <Input
              label="Date of Birth"
              type="date"
              value={formData.dateOfBirth || ''}
              onChange={(e) => setFormData({ ...formData, dateOfBirth: e.target.value })}
            />
          </div>

          <Textarea
            label="Customer Notes"
            placeholder="Relevant customer preferences or additional information"
            rows={2}
            value={formData.notes || ''}
            onChange={(e) => setFormData({ ...formData, notes: e.target.value })}
          />
        </form>
      </Modal>

      {/* Edit Customer Modal */}
      <Modal
        isOpen={isEditModalOpen}
        onClose={() => setIsEditModalOpen(false)}
        title="Edit Customer Profile"
        description="Update profile details. Concurrency is protected via optimistic row locking."
        maxWidth="2xl"
        footer={
          <>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setIsEditModalOpen(false)}
              disabled={submitting}
            >
              Cancel
            </Button>
            <Button
              variant="primary"
              size="sm"
              onClick={handleUpdateCustomer}
              isLoading={submitting}
            >
              Save Changes
            </Button>
          </>
        }
      >
        <form onSubmit={handleUpdateCustomer} className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Input
              label="Full Name"
              requiredIndicator
              placeholder="e.g. Arjun Rajan"
              value={editFormData.fullName}
              onChange={(e) => setEditFormData({ ...editFormData, fullName: e.target.value })}
              required
            />

            <Input
              label="Primary Phone"
              requiredIndicator
              placeholder="e.g. 077 123 4567"
              value={editFormData.primaryPhone}
              onChange={(e) => setEditFormData({ ...editFormData, primaryPhone: e.target.value })}
              onBlur={() => checkDuplicatePhone(editFormData.primaryPhone, selectedCustomer?.id)}
              helperText="Canonical E.164 normalization is applied"
              required
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Input
              label="Secondary Phone"
              placeholder="Alternative contact number"
              value={editFormData.secondaryPhone || ''}
              onChange={(e) => setEditFormData({ ...editFormData, secondaryPhone: e.target.value })}
            />

            <Input
              label="Email"
              type="email"
              placeholder="e.g. customer@example.com"
              value={editFormData.email || ''}
              onChange={(e) => setEditFormData({ ...editFormData, email: e.target.value })}
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Input
              label="Address Line 1"
              placeholder="House number, street and locality"
              value={editFormData.addressLine1 || ''}
              onChange={(e) => setEditFormData({ ...editFormData, addressLine1: e.target.value })}
            />

            <Input
              label="City"
              placeholder="e.g. Jaffna"
              value={editFormData.city || ''}
              onChange={(e) => setEditFormData({ ...editFormData, city: e.target.value })}
            />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <Input
              label="District / Province"
              placeholder="Select the customer's district"
              value={editFormData.districtProvince || ''}
              onChange={(e) => setEditFormData({ ...editFormData, districtProvince: e.target.value })}
            />

            <Input
              label="Postal Code"
              placeholder="e.g. 40000"
              value={editFormData.postalCode || ''}
              onChange={(e) => setEditFormData({ ...editFormData, postalCode: e.target.value })}
            />

            <Input
              label="Date of Birth"
              type="date"
              value={editFormData.dateOfBirth || ''}
              onChange={(e) => setEditFormData({ ...editFormData, dateOfBirth: e.target.value })}
            />
          </div>

          <Textarea
            label="Customer Notes"
            placeholder="Relevant customer preferences or additional information"
            rows={2}
            value={editFormData.notes || ''}
            onChange={(e) => setEditFormData({ ...editFormData, notes: e.target.value })}
          />
        </form>
      </Modal>

      {/* Status Toggle Modal */}
      <Modal
        isOpen={isStatusModalOpen}
        onClose={() => setIsStatusModalOpen(false)}
        title={selectedCustomer?.isActive ? 'Deactivate Customer Profile' : 'Activate Customer Profile'}
        description={`Confirming this action will ${selectedCustomer?.isActive ? 'suspend' : 're-enable'} transactional access for this profile.`}
        footer={
          <>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setIsStatusModalOpen(false)}
              disabled={submitting}
            >
              Cancel
            </Button>
            <Button
              variant={selectedCustomer?.isActive ? 'danger' : 'primary'}
              size="sm"
              onClick={handleToggleStatus}
              isLoading={submitting}
            >
              {selectedCustomer?.isActive ? 'Confirm Deactivation' : 'Confirm Activation'}
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <p className="text-xs text-[#202824]">
            Customer: <strong className="font-semibold text-[#123B32]">{selectedCustomer?.fullName}</strong> ({selectedCustomer?.customerCode})
          </p>

          <Textarea
            label="Action Reason"
            requiredIndicator
            placeholder="Explain why this customer account is being activated or deactivated"
            value={statusReason}
            onChange={(e) => setStatusReason(e.target.value)}
            helperText="A mandatory reason is required for audit logs."
            rows={3}
            required
          />
        </div>
      </Modal>

      {/* Detail Slide-over / Drawer */}
      {isDetailDrawerOpen && selectedCustomer && (
        <div className="fixed inset-0 z-50 overflow-hidden flex justify-end">
          <div
            className="fixed inset-0 bg-black/40 backdrop-blur-xs transition-opacity"
            onClick={() => setIsDetailDrawerOpen(false)}
          />
          <div className="relative w-full max-w-lg bg-white shadow-2xl border-l border-[#E2E6E3] z-10 flex flex-col h-full">
            {/* Header */}
            <div className="px-6 py-4.5 border-b border-[#E2E6E3] bg-[#F8F7F4] flex items-center justify-between">
              <div>
                <span className="text-[10px] font-bold text-[#69736E] uppercase tracking-wider">
                  {selectedCustomer.customerCode}
                </span>
                <h3 className="text-base font-bold text-[#123B32]">{selectedCustomer.fullName}</h3>
              </div>
              <button
                onClick={() => setIsDetailDrawerOpen(false)}
                className="p-1 rounded-md text-gray-400 hover:text-gray-600 hover:bg-gray-100"
              >
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Profile Overview */}
            <div className="p-6 overflow-y-auto flex-1 space-y-6">
              <div className="flex items-center justify-between p-3.5 rounded-lg bg-[#F8F7F4] border border-[#E2E6E3]">
                <div>
                  <p className="text-[10px] uppercase font-bold text-[#69736E]">Status</p>
                  <p className="text-xs font-semibold text-[#202824] mt-0.5">
                    {selectedCustomer.isActive ? 'Active Profile' : 'Suspended Profile'}
                  </p>
                </div>
                <Badge variant={selectedCustomer.isActive ? 'success' : 'neutral'}>
                  {selectedCustomer.isActive ? 'Active' : 'Inactive'}
                </Badge>
              </div>

              <div className="space-y-3">
                <h4 className="text-xs font-bold text-[#123B32] uppercase tracking-wider border-b border-[#E2E6E3] pb-1">
                  Contact Information
                </h4>
                <div className="grid grid-cols-2 gap-3 text-xs">
                  <div>
                    <span className="text-[#69736E] block text-[11px]">Primary Phone</span>
                    <span className="font-mono font-medium text-[#202824]">{selectedCustomer.primaryPhone}</span>
                  </div>
                  <div>
                    <span className="text-[#69736E] block text-[11px]">Canonical E.164</span>
                    <span className="font-mono text-[#123B32]">{selectedCustomer.normalizedPrimaryPhone || selectedCustomer.primaryPhone}</span>
                  </div>
                  <div>
                    <span className="text-[#69736E] block text-[11px]">Secondary Phone</span>
                    <span className="font-mono text-[#202824]">{selectedCustomer.secondaryPhone || '—'}</span>
                  </div>
                  <div>
                    <span className="text-[#69736E] block text-[11px]">Email</span>
                    <span className="text-[#202824]">{selectedCustomer.email || '—'}</span>
                  </div>
                </div>
              </div>

              <div className="space-y-3">
                <h4 className="text-xs font-bold text-[#123B32] uppercase tracking-wider border-b border-[#E2E6E3] pb-1">
                  Address & Demographics
                </h4>
                <div className="space-y-2 text-xs">
                  <div>
                    <span className="text-[#69736E] block text-[11px]">Street Address</span>
                    <span className="text-[#202824]">{selectedCustomer.addressLine1 || '—'}</span>
                  </div>
                  <div className="grid grid-cols-3 gap-2">
                    <div>
                      <span className="text-[#69736E] block text-[11px]">City</span>
                      <span className="text-[#202824]">{selectedCustomer.city || '—'}</span>
                    </div>
                    <div>
                      <span className="text-[#69736E] block text-[11px]">District</span>
                      <span className="text-[#202824]">{selectedCustomer.districtProvince || '—'}</span>
                    </div>
                    <div>
                      <span className="text-[#69736E] block text-[11px]">Postal Code</span>
                      <span className="text-[#202824]">{selectedCustomer.postalCode || '—'}</span>
                    </div>
                  </div>
                </div>
              </div>

              {selectedCustomer.notes && (
                <div className="space-y-1 text-xs">
                  <span className="text-[#69736E] font-semibold uppercase text-[10px] tracking-wider">Customer Notes</span>
                  <p className="p-3 bg-[#F8F7F4] border border-[#E2E6E3] rounded-lg text-[#202824]">
                    {selectedCustomer.notes}
                  </p>
                </div>
              )}
            </div>

            {/* Footer */}
            <div className="p-4 border-t border-[#E2E6E3] bg-[#F8F7F4] flex justify-end">
              <Button
                variant="secondary"
                size="sm"
                onClick={() => {
                  setIsDetailDrawerOpen(false);
                  handleOpenEdit(selectedCustomer);
                }}
              >
                Edit Customer Profile
              </Button>
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
