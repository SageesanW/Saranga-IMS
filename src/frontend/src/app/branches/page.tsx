'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import AppLayout from '@/components/AppLayout';
import { Branch, branchApi, ApiError } from '@/lib/api';
import { useBranch } from '@/lib/branch-context';

export default function BranchesPage() {
  const queryClient = useQueryClient();
  const { refreshBranches } = useBranch();

  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [editingBranch, setEditingBranch] = useState<Branch | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Form states for Add Branch
  const [formData, setFormData] = useState({
    name: '',
    code: '',
    country: 'Sri Lanka',
    currencyCode: 'LKR',
    timeZone: 'Asia/Colombo',
    isActive: true,
  });

  const { data: branches = [], isLoading } = useQuery({
    queryKey: ['branches'],
    queryFn: () => branchApi.getBranches(),
  });

  const createMutation = useMutation({
    mutationFn: branchApi.createBranch,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['branches'] });
      refreshBranches();
      setIsAddModalOpen(false);
      setFormData({
        name: '',
        code: '',
        country: 'Sri Lanka',
        currencyCode: 'LKR',
        timeZone: 'Asia/Colombo',
        isActive: true,
      });
      setErrorMsg(null);
    },
    onError: (err: ApiError) => {
      setErrorMsg(err.message || 'Failed to create branch');
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: any }) => branchApi.updateBranch(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['branches'] });
      refreshBranches();
      setEditingBranch(null);
      setErrorMsg(null);
    },
    onError: (err: ApiError) => {
      setErrorMsg(err.message || 'Failed to update branch');
    },
  });

  const toggleMutation = useMutation({
    mutationFn: branchApi.toggleStatus,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['branches'] });
      refreshBranches();
    },
    onError: (err: ApiError) => {
      alert(err.message || 'Failed to toggle status');
    },
  });

  const handleCreateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);
    createMutation.mutate(formData);
  };

  const handleUpdateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingBranch) return;
    setErrorMsg(null);
    updateMutation.mutate({
      id: editingBranch.id,
      data: {
        name: editingBranch.name,
        country: editingBranch.country,
        currencyCode: editingBranch.currencyCode,
        timeZone: editingBranch.timeZone,
        isActive: editingBranch.isActive,
      },
    });
  };

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 bg-white p-6 rounded-xl border border-gray-200 shadow-sm">
          <div>
            <h1 className="text-xl font-bold text-gray-900">Branch Management</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Configure enterprise branch locations, default currencies, and operational statuses.
            </p>
          </div>
          <button
            onClick={() => {
              setErrorMsg(null);
              setIsAddModalOpen(true);
            }}
            className="inline-flex items-center justify-center space-x-2 bg-[#0D3826] hover:bg-[#1B6543] text-white px-4 py-2.5 rounded-lg text-sm font-semibold shadow-sm transition border border-[#D4AF37]/50"
          >
            <span>➕</span>
            <span>Add Branch</span>
          </button>
        </div>

        {/* Table Card */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          {isLoading ? (
            <div className="p-12 text-center text-sm text-gray-500 font-medium">Loading branches...</div>
          ) : branches.length === 0 ? (
            <div className="p-12 text-center text-sm text-gray-500 font-medium">No branch records found.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50 text-[11px] font-bold text-gray-500 uppercase tracking-wider text-left">
                  <tr>
                    <th className="px-6 py-3.5">Code</th>
                    <th className="px-6 py-3.5">Branch Name</th>
                    <th className="px-6 py-3.5">Country</th>
                    <th className="px-6 py-3.5">Currency</th>
                    <th className="px-6 py-3.5">Time Zone</th>
                    <th className="px-6 py-3.5">Status</th>
                    <th className="px-6 py-3.5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 text-sm">
                  {branches.map((b) => (
                    <tr key={b.id} className="hover:bg-gray-50/80 transition">
                      <td className="px-6 py-4 font-bold text-[#0D3826]">
                        <span className="bg-emerald-50 text-emerald-800 border border-emerald-200 px-2.5 py-1 rounded-md text-xs font-mono">
                          {b.code}
                        </span>
                      </td>
                      <td className="px-6 py-4 font-medium text-gray-900">{b.name}</td>
                      <td className="px-6 py-4 text-gray-600">{b.country}</td>
                      <td className="px-6 py-4">
                        <span className="font-semibold text-gray-800">{b.currencyCode}</span>
                        {b.currencyCode === 'LKR' && <span className="text-xs text-gray-400 ml-1">(Rs.)</span>}
                      </td>
                      <td className="px-6 py-4 text-xs text-gray-500 font-mono">{b.timeZone}</td>
                      <td className="px-6 py-4">
                        {b.isActive ? (
                          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
                            Active
                          </span>
                        ) : (
                          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-red-100 text-red-800">
                            Inactive
                          </span>
                        )}
                      </td>
                      <td className="px-6 py-4 text-right space-x-2">
                        <button
                          onClick={() => {
                            setErrorMsg(null);
                            setEditingBranch(b);
                          }}
                          className="text-xs font-semibold text-indigo-600 hover:text-indigo-900 px-2.5 py-1 rounded border border-indigo-200 hover:bg-indigo-50"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => toggleMutation.mutate(b.id)}
                          className={`text-xs font-semibold px-2.5 py-1 rounded border transition ${
                            b.isActive
                              ? 'text-red-700 border-red-200 hover:bg-red-50'
                              : 'text-emerald-700 border-emerald-200 hover:bg-emerald-50'
                          }`}
                        >
                          {b.isActive ? 'Deactivate' : 'Activate'}
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

      {/* Add Branch Modal */}
      {isAddModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6 space-y-4 border border-gray-200">
            <div className="flex justify-between items-center border-b pb-3">
              <h2 className="text-lg font-bold text-gray-900">Add New Branch</h2>
              <button onClick={() => setIsAddModalOpen(false)} className="text-gray-400 hover:text-gray-600">✕</button>
            </div>

            {errorMsg && (
              <div className="bg-red-50 border border-red-200 text-red-700 p-3 rounded-lg text-xs font-medium">
                {errorMsg}
              </div>
            )}

            <form onSubmit={handleCreateSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Branch Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Jaffna Main Branch"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Branch Code (Unique)</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. JAF-01"
                  value={formData.code}
                  onChange={(e) => setFormData({ ...formData, code: e.target.value.toUpperCase() })}
                  className="w-full px-3 py-2 border rounded-lg text-sm uppercase font-mono focus:ring-2 focus:ring-[#0D3826] outline-none"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Country</label>
                  <input
                    type="text"
                    required
                    value={formData.country}
                    onChange={(e) => setFormData({ ...formData, country: e.target.value })}
                    className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Currency Code</label>
                  <input
                    type="text"
                    required
                    value={formData.currencyCode}
                    onChange={(e) => setFormData({ ...formData, currencyCode: e.target.value.toUpperCase() })}
                    className="w-full px-3 py-2 border rounded-lg text-sm uppercase font-mono focus:ring-2 focus:ring-[#0D3826] outline-none"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Time Zone</label>
                <input
                  type="text"
                  required
                  value={formData.timeZone}
                  onChange={(e) => setFormData({ ...formData, timeZone: e.target.value })}
                  className="w-full px-3 py-2 border rounded-lg text-sm font-mono focus:ring-2 focus:ring-[#0D3826] outline-none"
                />
              </div>

              <div className="flex items-center space-x-2 pt-2">
                <input
                  type="checkbox"
                  id="isActive"
                  checked={formData.isActive}
                  onChange={(e) => setFormData({ ...formData, isActive: e.target.checked })}
                  className="w-4 h-4 text-[#0D3826] rounded"
                />
                <label htmlFor="isActive" className="text-xs font-medium text-gray-700">Active Branch</label>
              </div>

              <div className="flex justify-end space-x-3 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setIsAddModalOpen(false)}
                  className="px-4 py-2 text-xs font-semibold text-gray-700 border rounded-lg hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={createMutation.isPending}
                  className="px-4 py-2 text-xs font-semibold text-white bg-[#0D3826] hover:bg-[#1B6543] rounded-lg shadow-sm border border-[#D4AF37]/50"
                >
                  {createMutation.isPending ? 'Saving...' : 'Create Branch'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit Branch Modal */}
      {editingBranch && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6 space-y-4 border border-gray-200">
            <div className="flex justify-between items-center border-b pb-3">
              <h2 className="text-lg font-bold text-gray-900">Edit Branch: {editingBranch.code}</h2>
              <button onClick={() => setEditingBranch(null)} className="text-gray-400 hover:text-gray-600">✕</button>
            </div>

            {errorMsg && (
              <div className="bg-red-50 border border-red-200 text-red-700 p-3 rounded-lg text-xs font-medium">
                {errorMsg}
              </div>
            )}

            <form onSubmit={handleUpdateSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Branch Name</label>
                <input
                  type="text"
                  required
                  value={editingBranch.name}
                  onChange={(e) => setEditingBranch({ ...editingBranch, name: e.target.value })}
                  className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Country</label>
                  <input
                    type="text"
                    required
                    value={editingBranch.country}
                    onChange={(e) => setEditingBranch({ ...editingBranch, country: e.target.value })}
                    className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Currency Code</label>
                  <input
                    type="text"
                    required
                    value={editingBranch.currencyCode}
                    onChange={(e) => setEditingBranch({ ...editingBranch, currencyCode: e.target.value.toUpperCase() })}
                    className="w-full px-3 py-2 border rounded-lg text-sm uppercase font-mono focus:ring-2 focus:ring-[#0D3826] outline-none"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Time Zone</label>
                <input
                  type="text"
                  required
                  value={editingBranch.timeZone}
                  onChange={(e) => setEditingBranch({ ...editingBranch, timeZone: e.target.value })}
                  className="w-full px-3 py-2 border rounded-lg text-sm font-mono focus:ring-2 focus:ring-[#0D3826] outline-none"
                />
              </div>

              <div className="flex items-center space-x-2 pt-2">
                <input
                  type="checkbox"
                  id="editIsActive"
                  checked={editingBranch.isActive}
                  onChange={(e) => setEditingBranch({ ...editingBranch, isActive: e.target.checked })}
                  className="w-4 h-4 text-[#0D3826] rounded"
                />
                <label htmlFor="editIsActive" className="text-xs font-medium text-gray-700">Active Branch</label>
              </div>

              <div className="flex justify-end space-x-3 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setEditingBranch(null)}
                  className="px-4 py-2 text-xs font-semibold text-gray-700 border rounded-lg hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={updateMutation.isPending}
                  className="px-4 py-2 text-xs font-semibold text-white bg-[#0D3826] hover:bg-[#1B6543] rounded-lg shadow-sm border border-[#D4AF37]/50"
                >
                  {updateMutation.isPending ? 'Updating...' : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
