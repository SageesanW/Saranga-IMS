'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import AppLayout from '@/components/AppLayout';
import { User, userApi, roleApi, branchApi, ApiError } from '@/lib/api';
import { useAuth } from '@/lib/auth-context';

export default function UsersPage() {
  const queryClient = useQueryClient();
  const { user: currentUser } = useAuth();

  // Filters
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedBranchId, setSelectedBranchId] = useState('');
  const [selectedRoleId, setSelectedRoleId] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');

  // Modals state
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [editingUser, setEditingUser] = useState<User | null>(null);
  const [accessModalUser, setAccessModalUser] = useState<User | null>(null);
  const [resetPasswordUser, setResetPasswordUser] = useState<User | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Form states for Add User
  const [newUser, setNewUser] = useState({
    username: '',
    email: '',
    fullName: '',
    password: '',
    roleIds: [] as string[],
    branchIds: [] as string[],
    defaultBranchId: '' as string,
    isGlobalAdmin: false,
  });

  const [newPassword, setNewPassword] = useState('');

  // Queries
  const { data: usersData, isLoading: loadingUsers } = useQuery({
    queryKey: ['users', searchTerm, selectedBranchId, selectedRoleId, statusFilter],
    queryFn: () =>
      userApi.getUsers({
        search: searchTerm || undefined,
        branchId: selectedBranchId || undefined,
        roleId: selectedRoleId || undefined,
        isActive: statusFilter === '' ? undefined : statusFilter === 'true',
      }),
  });

  const { data: roles = [] } = useQuery({
    queryKey: ['roles'],
    queryFn: () => roleApi.getRoles(),
  });

  const { data: branches = [] } = useQuery({
    queryKey: ['branches'],
    queryFn: () => branchApi.getBranches(),
  });

  // Mutations
  const createMutation = useMutation({
    mutationFn: userApi.createUser,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setIsAddModalOpen(false);
      setNewUser({
        username: '',
        email: '',
        fullName: '',
        password: '',
        roleIds: [],
        branchIds: [],
        defaultBranchId: '',
        isGlobalAdmin: false,
      });
      setSuccessMsg('User account created successfully.');
      setErrorMsg(null);
    },
    onError: (err: ApiError) => {
      setErrorMsg(err.message || 'Failed to create user');
    },
  });

  const updateMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: any }) => userApi.updateUser(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setEditingUser(null);
      setSuccessMsg('User updated successfully.');
      setErrorMsg(null);
    },
    onError: (err: ApiError) => {
      setErrorMsg(err.message || 'Failed to update user');
    },
  });

  const assignRolesMutation = useMutation({
    mutationFn: ({ id, roleIds }: { id: string; roleIds: string[] }) => userApi.assignRoles(id, roleIds),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setSuccessMsg('User roles updated successfully.');
    },
  });

  const assignBranchesMutation = useMutation({
    mutationFn: ({ id, branchIds, defaultBranchId }: { id: string; branchIds: string[]; defaultBranchId?: string | null }) =>
      userApi.assignBranches(id, branchIds, defaultBranchId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setSuccessMsg('User branch access updated successfully.');
    },
  });

  const toggleStatusMutation = useMutation({
    mutationFn: userApi.toggleStatus,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['users'] });
      setSuccessMsg('User status changed successfully.');
    },
    onError: (err: ApiError) => {
      alert(err.message || 'Failed to toggle user status');
    },
  });

  const resetPasswordMutation = useMutation({
    mutationFn: ({ id, pass }: { id: string; pass: string }) => userApi.resetPassword(id, pass),
    onSuccess: () => {
      setResetPasswordUser(null);
      setNewPassword('');
      setSuccessMsg('Password has been reset and all existing sessions revoked.');
      setErrorMsg(null);
    },
    onError: (err: ApiError) => {
      setErrorMsg(err.message || 'Failed to reset password');
    },
  });

  const usersList = usersData?.items || [];

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 bg-white p-6 rounded-xl border border-gray-200 shadow-sm">
          <div>
            <h1 className="text-xl font-bold text-gray-900">User Management</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Control staff accounts, assigned roles, multi-branch permissions, and access statuses.
            </p>
          </div>
          <button
            onClick={() => {
              setErrorMsg(null);
              setSuccessMsg(null);
              setIsAddModalOpen(true);
            }}
            className="inline-flex items-center justify-center space-x-2 bg-[#0D3826] hover:bg-[#1B6543] text-white px-4 py-2.5 rounded-lg text-sm font-semibold shadow-sm transition border border-[#D4AF37]/50"
          >
            <span>➕</span>
            <span>Create User</span>
          </button>
        </div>

        {/* Notifications */}
        {successMsg && (
          <div className="bg-emerald-50 border border-emerald-200 text-emerald-800 px-4 py-3 rounded-lg text-sm flex justify-between items-center">
            <span>{successMsg}</span>
            <button onClick={() => setSuccessMsg(null)} className="text-emerald-600 font-bold">✕</button>
          </div>
        )}

        {/* Filters Card */}
        <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm grid grid-cols-1 sm:grid-cols-4 gap-3">
          <div>
            <label className="block text-[11px] font-semibold text-gray-500 uppercase mb-1">Search</label>
            <input
              type="text"
              placeholder="Username, email, name..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className="w-full px-3 py-2 border rounded-lg text-sm outline-none focus:ring-2 focus:ring-[#0D3826]"
            />
          </div>
          <div>
            <label className="block text-[11px] font-semibold text-gray-500 uppercase mb-1">Branch</label>
            <select
              value={selectedBranchId}
              onChange={(e) => setSelectedBranchId(e.target.value)}
              className="w-full px-3 py-2 border rounded-lg text-sm outline-none focus:ring-2 focus:ring-[#0D3826]"
            >
              <option value="">All Branches</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>{b.code} - {b.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-[11px] font-semibold text-gray-500 uppercase mb-1">Role</label>
            <select
              value={selectedRoleId}
              onChange={(e) => setSelectedRoleId(e.target.value)}
              className="w-full px-3 py-2 border rounded-lg text-sm outline-none focus:ring-2 focus:ring-[#0D3826]"
            >
              <option value="">All Roles</option>
              {roles.map((r) => (
                <option key={r.id} value={r.id}>{r.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-[11px] font-semibold text-gray-500 uppercase mb-1">Status</label>
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="w-full px-3 py-2 border rounded-lg text-sm outline-none focus:ring-2 focus:ring-[#0D3826]"
            >
              <option value="">All Statuses</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </div>
        </div>

        {/* Users Table */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          {loadingUsers ? (
            <div className="p-12 text-center text-sm text-gray-500 font-medium">Loading users...</div>
          ) : usersList.length === 0 ? (
            <div className="p-12 text-center text-sm text-gray-500 font-medium">No users match your criteria.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50 text-[11px] font-bold text-gray-500 uppercase tracking-wider text-left">
                  <tr>
                    <th className="px-6 py-3.5">User</th>
                    <th className="px-6 py-3.5">Roles</th>
                    <th className="px-6 py-3.5">Accessible Branches</th>
                    <th className="px-6 py-3.5">Default Branch</th>
                    <th className="px-6 py-3.5">Status</th>
                    <th className="px-6 py-3.5 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100 text-sm">
                  {usersList.map((u) => {
                    const defaultBranch = branches.find((b) => b.id === u.defaultBranchId);
                    return (
                      <tr key={u.id} className="hover:bg-gray-50/80 transition">
                        <td className="px-6 py-4">
                          <div className="flex items-center space-x-3">
                            <div className="w-8 h-8 rounded-full bg-[#0D3826] text-[#D4AF37] font-bold text-xs flex items-center justify-center">
                              {u.username.substring(0, 2).toUpperCase()}
                            </div>
                            <div>
                              <div className="font-semibold text-gray-900 flex items-center space-x-1.5">
                                <span>{u.fullName}</span>
                                {u.isGlobalAdmin && (
                                  <span className="text-[10px] bg-amber-100 text-amber-800 font-bold px-1.5 py-0.2 rounded border border-amber-300">
                                    Global Admin
                                  </span>
                                )}
                              </div>
                              <div className="text-xs text-gray-500">@{u.username} • {u.email}</div>
                            </div>
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <div className="flex flex-wrap gap-1">
                            {u.roles.map((r) => (
                              <span key={r} className="px-2 py-0.5 rounded text-xs font-semibold bg-[#FAF4DB] text-[#67471E] border border-[#E4BF59]/60">
                                {r}
                              </span>
                            ))}
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          <div className="flex flex-wrap gap-1">
                            {u.branchIds.map((bid) => {
                              const b = branches.find((item) => item.id === bid);
                              return (
                                <span key={bid} className="px-2 py-0.5 rounded text-xs font-mono font-medium bg-emerald-50 text-emerald-800 border border-emerald-200">
                                  {b ? b.code : bid.substring(0, 6)}
                                </span>
                              );
                            })}
                            {u.branchIds.length === 0 && (
                              <span className="text-xs text-gray-400 italic">None</span>
                            )}
                          </div>
                        </td>
                        <td className="px-6 py-4">
                          {defaultBranch ? (
                            <span className="text-xs font-medium text-gray-700 bg-gray-100 px-2 py-0.5 rounded">
                              {defaultBranch.code} ({defaultBranch.name})
                            </span>
                          ) : (
                            <span className="text-xs text-gray-400 italic">None</span>
                          )}
                        </td>
                        <td className="px-6 py-4">
                          {u.isActive ? (
                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
                              Active
                            </span>
                          ) : (
                            <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-red-100 text-red-800">
                              Inactive
                            </span>
                          )}
                        </td>
                        <td className="px-6 py-4 text-right space-x-1 whitespace-nowrap">
                          <button
                            onClick={() => {
                              setErrorMsg(null);
                              setEditingUser(u);
                            }}
                            className="text-xs font-semibold text-indigo-600 hover:text-indigo-900 px-2 py-1 rounded border border-indigo-200 hover:bg-indigo-50"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => {
                              setErrorMsg(null);
                              setAccessModalUser(u);
                            }}
                            className="text-xs font-semibold text-emerald-700 hover:text-emerald-900 px-2 py-1 rounded border border-emerald-200 hover:bg-emerald-50"
                          >
                            Access
                          </button>
                          <button
                            onClick={() => {
                              setErrorMsg(null);
                              setResetPasswordUser(u);
                            }}
                            className="text-xs font-semibold text-amber-700 hover:text-amber-900 px-2 py-1 rounded border border-amber-200 hover:bg-amber-50"
                          >
                            Reset Key
                          </button>
                          {currentUser?.id !== u.id && (
                            <button
                              onClick={() => toggleStatusMutation.mutate(u.id)}
                              className={`text-xs font-semibold px-2 py-1 rounded border transition ${
                                u.isActive
                                  ? 'text-red-700 border-red-200 hover:bg-red-50'
                                  : 'text-emerald-700 border-emerald-200 hover:bg-emerald-50'
                              }`}
                            >
                              {u.isActive ? 'Deactivate' : 'Activate'}
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>

      {/* Create User Modal */}
      {isAddModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 overflow-y-auto">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4 border border-gray-200 my-8">
            <div className="flex justify-between items-center border-b pb-3">
              <h2 className="text-lg font-bold text-gray-900">Create New Staff User</h2>
              <button onClick={() => setIsAddModalOpen(false)} className="text-gray-400 hover:text-gray-600">✕</button>
            </div>

            {errorMsg && (
              <div className="bg-red-50 border border-red-200 text-red-700 p-3 rounded-lg text-xs font-medium">
                {errorMsg}
              </div>
            )}

            <form
              onSubmit={(e) => {
                e.preventDefault();
                setErrorMsg(null);
                createMutation.mutate({
                  ...newUser,
                  defaultBranchId: newUser.defaultBranchId || null,
                });
              }}
              className="space-y-4"
            >
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Username</label>
                  <input
                    type="text"
                    required
                    placeholder="e.g. jaffna_cashier1"
                    value={newUser.username}
                    onChange={(e) => setNewUser({ ...newUser, username: e.target.value })}
                    className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Full Name</label>
                  <input
                    type="text"
                    required
                    placeholder="e.g. Sivanathan Raman"
                    value={newUser.fullName}
                    onChange={(e) => setNewUser({ ...newUser, fullName: e.target.value })}
                    className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Email</label>
                  <input
                    type="email"
                    required
                    placeholder="staff@sarangajewellery.lk"
                    value={newUser.email}
                    onChange={(e) => setNewUser({ ...newUser, email: e.target.value })}
                    className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Initial Password</label>
                  <input
                    type="password"
                    required
                    minLength={8}
                    placeholder="Min 8 characters"
                    value={newUser.password}
                    onChange={(e) => setNewUser({ ...newUser, password: e.target.value })}
                    className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                  />
                </div>
              </div>

              {/* Roles Selection */}
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1.5">Assign Roles</label>
                <div className="grid grid-cols-2 gap-2 border p-3 rounded-lg bg-gray-50 max-h-36 overflow-y-auto">
                  {roles.map((r) => {
                    const checked = newUser.roleIds.includes(r.id);
                    return (
                      <label key={r.id} className="flex items-center space-x-2 text-xs text-gray-700 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={(e) => {
                            if (e.target.checked) {
                              setNewUser({ ...newUser, roleIds: [...newUser.roleIds, r.id] });
                            } else {
                              setNewUser({ ...newUser, roleIds: newUser.roleIds.filter((id) => id !== r.id) });
                            }
                          }}
                          className="rounded text-[#0D3826]"
                        />
                        <span className="font-medium">{r.name}</span>
                      </label>
                    );
                  })}
                </div>
              </div>

              {/* Branches Selection */}
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1.5">Accessible Branches</label>
                <div className="grid grid-cols-2 gap-2 border p-3 rounded-lg bg-gray-50 max-h-36 overflow-y-auto">
                  {branches.map((b) => {
                    const checked = newUser.branchIds.includes(b.id);
                    return (
                      <label key={b.id} className="flex items-center space-x-2 text-xs text-gray-700 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={(e) => {
                            if (e.target.checked) {
                              setNewUser({ ...newUser, branchIds: [...newUser.branchIds, b.id] });
                            } else {
                              setNewUser({
                                ...newUser,
                                branchIds: newUser.branchIds.filter((id) => id !== b.id),
                                defaultBranchId: newUser.defaultBranchId === b.id ? '' : newUser.defaultBranchId,
                              });
                            }
                          }}
                          className="rounded text-[#0D3826]"
                        />
                        <span className="font-mono font-bold text-gray-900">{b.code}</span>
                        <span className="text-gray-600 truncate">{b.name}</span>
                      </label>
                    );
                  })}
                </div>
              </div>

              {/* Default Branch */}
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Default Branch</label>
                <select
                  value={newUser.defaultBranchId}
                  onChange={(e) => setNewUser({ ...newUser, defaultBranchId: e.target.value })}
                  className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                >
                  <option value="">Select Default Branch</option>
                  {branches
                    .filter((b) => newUser.branchIds.includes(b.id))
                    .map((b) => (
                      <option key={b.id} value={b.id}>{b.code} - {b.name}</option>
                    ))}
                </select>
              </div>

              {/* Global Admin Toggle */}
              {currentUser?.isGlobalAdmin && (
                <div className="flex items-center space-x-2 pt-1 bg-amber-50 p-2.5 rounded-lg border border-amber-200">
                  <input
                    type="checkbox"
                    id="isGlobalAdmin"
                    checked={newUser.isGlobalAdmin}
                    onChange={(e) => setNewUser({ ...newUser, isGlobalAdmin: e.target.checked })}
                    className="w-4 h-4 text-[#0D3826] rounded"
                  />
                  <div>
                    <label htmlFor="isGlobalAdmin" className="text-xs font-bold text-amber-900 cursor-pointer">
                      Grant Global Admin Oversight
                    </label>
                    <p className="text-[10px] text-amber-700">Gives unrestricted enterprise-wide access across all branches.</p>
                  </div>
                </div>
              )}

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
                  {createMutation.isPending ? 'Creating...' : 'Create User'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit User Modal */}
      {editingUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6 space-y-4 border border-gray-200">
            <div className="flex justify-between items-center border-b pb-3">
              <h2 className="text-lg font-bold text-gray-900">Edit User: @{editingUser.username}</h2>
              <button onClick={() => setEditingUser(null)} className="text-gray-400 hover:text-gray-600">✕</button>
            </div>

            {errorMsg && (
              <div className="bg-red-50 border border-red-200 text-red-700 p-3 rounded-lg text-xs font-medium">
                {errorMsg}
              </div>
            )}

            <form
              onSubmit={(e) => {
                e.preventDefault();
                setErrorMsg(null);
                updateMutation.mutate({
                  id: editingUser.id,
                  data: {
                    fullName: editingUser.fullName,
                    email: editingUser.email,
                    defaultBranchId: editingUser.defaultBranchId || null,
                  },
                });
              }}
              className="space-y-4"
            >
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Full Name</label>
                <input
                  type="text"
                  required
                  value={editingUser.fullName}
                  onChange={(e) => setEditingUser({ ...editingUser, fullName: e.target.value })}
                  className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Email</label>
                <input
                  type="email"
                  required
                  value={editingUser.email}
                  onChange={(e) => setEditingUser({ ...editingUser, email: e.target.value })}
                  className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Default Branch</label>
                <select
                  value={editingUser.defaultBranchId || ''}
                  onChange={(e) => setEditingUser({ ...editingUser, defaultBranchId: e.target.value || null })}
                  className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                >
                  <option value="">None</option>
                  {branches
                    .filter((b) => editingUser.branchIds.includes(b.id) || editingUser.isGlobalAdmin)
                    .map((b) => (
                      <option key={b.id} value={b.id}>{b.code} - {b.name}</option>
                    ))}
                </select>
              </div>

              <div className="flex justify-end space-x-3 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setEditingUser(null)}
                  className="px-4 py-2 text-xs font-semibold text-gray-700 border rounded-lg hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={updateMutation.isPending}
                  className="px-4 py-2 text-xs font-semibold text-white bg-[#0D3826] hover:bg-[#1B6543] rounded-lg shadow-sm border border-[#D4AF37]/50"
                >
                  {updateMutation.isPending ? 'Saving...' : 'Save Changes'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Access (Roles & Branches) Modal */}
      {accessModalUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4 border border-gray-200">
            <div className="flex justify-between items-center border-b pb-3">
              <h2 className="text-lg font-bold text-gray-900">Manage Access: @{accessModalUser.username}</h2>
              <button onClick={() => setAccessModalUser(null)} className="text-gray-400 hover:text-gray-600">✕</button>
            </div>

            <div className="space-y-4">
              <div>
                <h3 className="text-xs font-bold uppercase text-gray-500 mb-2">Role Assignments</h3>
                <div className="space-y-2 border p-3 rounded-lg bg-gray-50 max-h-40 overflow-y-auto">
                  {roles.map((r) => {
                    const isAssigned = accessModalUser.roles.includes(r.name);
                    return (
                      <label key={r.id} className="flex items-center space-x-2 text-xs text-gray-700 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={isAssigned}
                          onChange={(e) => {
                            const newRoles = e.target.checked
                              ? [...accessModalUser.roles, r.name]
                              : accessModalUser.roles.filter((name) => name !== r.name);
                            setAccessModalUser({ ...accessModalUser, roles: newRoles });
                            const targetRoleIds = roles.filter((role) => newRoles.includes(role.name)).map((role) => role.id);
                            assignRolesMutation.mutate({ id: accessModalUser.id, roleIds: targetRoleIds });
                          }}
                          className="rounded text-[#0D3826]"
                        />
                        <span className="font-semibold text-gray-900">{r.name}</span>
                        <span className="text-[11px] text-gray-400 truncate">{r.description}</span>
                      </label>
                    );
                  })}
                </div>
              </div>

              <div>
                <h3 className="text-xs font-bold uppercase text-gray-500 mb-2">Branch Access</h3>
                <div className="space-y-2 border p-3 rounded-lg bg-gray-50 max-h-40 overflow-y-auto">
                  {branches.map((b) => {
                    const isAssigned = accessModalUser.branchIds.includes(b.id);
                    return (
                      <label key={b.id} className="flex items-center space-x-2 text-xs text-gray-700 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={isAssigned}
                          onChange={(e) => {
                            const newBranchIds = e.target.checked
                              ? [...accessModalUser.branchIds, b.id]
                              : accessModalUser.branchIds.filter((id) => id !== b.id);
                            setAccessModalUser({ ...accessModalUser, branchIds: newBranchIds });
                            assignBranchesMutation.mutate({
                              id: accessModalUser.id,
                              branchIds: newBranchIds,
                              defaultBranchId: accessModalUser.defaultBranchId,
                            });
                          }}
                          className="rounded text-[#0D3826]"
                        />
                        <span className="font-mono font-bold text-gray-900">{b.code}</span>
                        <span className="text-gray-700 font-medium">{b.name}</span>
                      </label>
                    );
                  })}
                </div>
              </div>

              <div className="flex justify-end pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setAccessModalUser(null)}
                  className="px-4 py-2 text-xs font-semibold text-white bg-[#0D3826] rounded-lg"
                >
                  Done
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Reset Password Modal */}
      {resetPasswordUser && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-sm w-full p-6 space-y-4 border border-gray-200">
            <div className="flex justify-between items-center border-b pb-3">
              <h2 className="text-lg font-bold text-gray-900">Reset Password</h2>
              <button onClick={() => setResetPasswordUser(null)} className="text-gray-400 hover:text-gray-600">✕</button>
            </div>

            <p className="text-xs text-gray-600">
              Resetting password for <strong className="text-gray-900">@{resetPasswordUser.username}</strong> will immediately revoke all active refresh tokens and sessions.
            </p>

            {errorMsg && (
              <div className="bg-red-50 border border-red-200 text-red-700 p-3 rounded-lg text-xs font-medium">
                {errorMsg}
              </div>
            )}

            <form
              onSubmit={(e) => {
                e.preventDefault();
                setErrorMsg(null);
                resetPasswordMutation.mutate({
                  id: resetPasswordUser.id,
                  pass: newPassword,
                });
              }}
              className="space-y-4"
            >
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">New Password</label>
                <input
                  type="password"
                  required
                  minLength={8}
                  placeholder="Min 8 characters"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#0D3826] outline-none"
                />
              </div>

              <div className="flex justify-end space-x-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setResetPasswordUser(null)}
                  className="px-4 py-2 text-xs font-semibold text-gray-700 border rounded-lg hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={resetPasswordMutation.isPending}
                  className="px-4 py-2 text-xs font-semibold text-white bg-amber-700 hover:bg-amber-800 rounded-lg shadow-sm"
                >
                  {resetPasswordMutation.isPending ? 'Resetting...' : 'Confirm Reset'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
