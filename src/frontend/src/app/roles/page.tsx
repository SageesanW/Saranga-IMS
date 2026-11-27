'use client';

import React, { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import AppLayout from '@/components/AppLayout';
import { Role, Permission, roleApi, permissionApi, ApiError } from '@/lib/api';

const EMPTY_ROLES: Role[] = [];
const EMPTY_PERMISSIONS: Permission[] = [];

export default function RolesPage() {
  const queryClient = useQueryClient();

  const [selectedRoleId, setSelectedRoleId] = useState<string | null>(null);
  const [selectedPermissionIds, setSelectedPermissionIds] = useState<string[]>([]);
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // New role form state
  const [newRole, setNewRole] = useState({
    name: '',
    description: '',
    permissionIds: [] as string[],
  });

  // Queries
  const { data: roles = EMPTY_ROLES, isLoading: loadingRoles } = useQuery({
    queryKey: ['roles'],
    queryFn: () => roleApi.getRoles(),
  });

  const { data: permissions = EMPTY_PERMISSIONS, isLoading: loadingPermissions } = useQuery({
    queryKey: ['permissions'],
    queryFn: () => permissionApi.getPermissions(),
  });

  // Set initial selected role once loaded
  const activeRole = roles.find((r) => r.id === selectedRoleId) || roles[0] || null;
  const activeRoleId = activeRole?.id ?? null;

  React.useEffect(() => {
    if (!activeRole || permissions.length === 0) return;
    const pIds = permissions
      .filter((p) => activeRole.permissions.includes(p.name))
      .map((p) => p.id);
    setSelectedPermissionIds((prev) =>
      prev.length === pIds.length && prev.every((id, i) => id === pIds[i]) ? prev : pIds
    );
  }, [activeRoleId, activeRole, permissions]);

  // Mutations
  const createRoleMutation = useMutation({
    mutationFn: roleApi.createRole,
    onSuccess: (created) => {
      queryClient.invalidateQueries({ queryKey: ['roles'] });
      setIsAddModalOpen(false);
      setSelectedRoleId(created.id);
      setNewRole({ name: '', description: '', permissionIds: [] });
      setSuccessMsg(`Role "${created.name}" created successfully.`);
      setErrorMsg(null);
    },
    onError: (err: ApiError) => {
      setErrorMsg(err.message || 'Failed to create role');
    },
  });

  const savePermissionsMutation = useMutation({
    mutationFn: ({ roleId, permissionIds }: { roleId: string; permissionIds: string[] }) =>
      roleApi.assignPermissions(roleId, permissionIds),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['roles'] });
      setSuccessMsg('Role permissions updated successfully.');
      setErrorMsg(null);
    },
    onError: (err: ApiError) => {
      setErrorMsg(err.message || 'Failed to update permissions');
    },
  });

  // Group permissions by module
  const permissionsByModule = permissions.reduce((acc, p) => {
    if (!acc[p.module]) acc[p.module] = [];
    acc[p.module].push(p);
    return acc;
  }, {} as Record<string, Permission[]>);

  const isOwnerRole = activeRole?.name === 'Owner / Super Admin';

  const handleTogglePermission = (permId: string) => {
    if (isOwnerRole) return;
    if (selectedPermissionIds.includes(permId)) {
      setSelectedPermissionIds(selectedPermissionIds.filter((id) => id !== permId));
    } else {
      setSelectedPermissionIds([...selectedPermissionIds, permId]);
    }
  };

  const handleToggleModuleAll = (moduleName: string, enable: boolean) => {
    if (isOwnerRole) return;
    const modPermIds = (permissionsByModule[moduleName] || []).map((p) => p.id);
    if (enable) {
      const combined = Array.from(new Set([...selectedPermissionIds, ...modPermIds]));
      setSelectedPermissionIds(combined);
    } else {
      setSelectedPermissionIds(selectedPermissionIds.filter((id) => !modPermIds.includes(id)));
    }
  };

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Header */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 bg-white p-6 rounded-xl border border-gray-200 shadow-sm">
          <div>
            <h1 className="text-xl font-bold text-gray-900">Roles & Granular Permissions</h1>
            <p className="text-sm text-gray-500 mt-0.5">
              Define operational role templates and map specific module-level security capabilities.
            </p>
          </div>
          <button
            onClick={() => {
              setErrorMsg(null);
              setSuccessMsg(null);
              setIsAddModalOpen(true);
            }}
            className="inline-flex items-center justify-center space-x-2 bg-[#123B32] hover:bg-[#1B6543] text-white px-4 py-2.5 rounded-lg text-sm font-semibold shadow-sm transition border border-[#B89A60]/50"
          >
            
            <span>New Custom Role</span>
          </button>
        </div>

        {/* Notifications */}
        {successMsg && (
          <div className="bg-emerald-50 border border-emerald-200 text-emerald-800 px-4 py-3 rounded-lg text-sm flex justify-between items-center">
            <span>{successMsg}</span>
            <button onClick={() => setSuccessMsg(null)} className="text-emerald-600 font-bold">✕</button>
          </div>
        )}

        {errorMsg && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm flex justify-between items-center">
            <span>{errorMsg}</span>
            <button onClick={() => setErrorMsg(null)} className="text-red-600 font-bold">✕</button>
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Left Column: Roles List */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-4 space-y-3">
            <h2 className="text-xs font-bold uppercase tracking-wider text-gray-500 px-1">Configured Roles</h2>
            {loadingRoles ? (
              <div className="text-center py-8 text-xs text-gray-400">Loading roles...</div>
            ) : (
              <div className="space-y-2">
                {roles.map((r) => {
                  const isSelected = activeRole?.id === r.id;
                  return (
                    <button
                      key={r.id}
                      onClick={() => {
                        setSelectedRoleId(r.id);
                        setErrorMsg(null);
                        setSuccessMsg(null);
                      }}
                      className={`w-full text-left p-3.5 rounded-xl border transition flex flex-col space-y-1.5 ${
                        isSelected
                          ? 'bg-[#123B32] text-white border-[#B89A60] shadow-sm'
                          : 'bg-gray-50 hover:bg-gray-100 text-gray-800 border-gray-200'
                      }`}
                    >
                      <div className="flex items-center justify-between">
                        <span className="font-bold text-sm tracking-tight">{r.name}</span>
                        {r.isSystemRole ? (
                          <span
                            className={`text-[10px] font-bold px-2 py-0.5 rounded-full uppercase tracking-wider ${
                              isSelected
                                ? 'bg-[#B89A60] text-[#123B32]'
                                : 'bg-[#FAF4DB] text-[#7A571E] border border-[#E4BF59]'
                            }`}
                          >
                            System Role
                          </span>
                        ) : (
                          <span
                            className={`text-[10px] font-semibold px-2 py-0.5 rounded-full ${
                              isSelected ? 'bg-white/20 text-white' : 'bg-gray-200 text-gray-700'
                            }`}
                          >
                            Custom
                          </span>
                        )}
                      </div>
                      <p className={`text-xs line-clamp-2 ${isSelected ? 'text-gray-200' : 'text-gray-500'}`}>
                        {r.description || 'No description provided.'}
                      </p>
                      <div className={`text-[11px] font-mono pt-1 ${isSelected ? 'text-[#B89A60]' : 'text-gray-400'}`}>
                        {r.permissions.length} active permissions
                      </div>
                    </button>
                  );
                })}
              </div>
            )}
          </div>

          {/* Right Column: Permission Matrix for Active Role */}
          <div className="lg:col-span-2 bg-white rounded-xl border border-gray-200 shadow-sm p-6 space-y-6">
            {activeRole ? (
              <>
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between border-b pb-4 gap-3">
                  <div>
                    <div className="flex items-center space-x-2">
                      <h2 className="text-lg font-bold text-gray-900">{activeRole.name}</h2>
                      {activeRole.isSystemRole && (
                        <span className="text-[10px] bg-amber-50 text-amber-800 font-bold px-2 py-0.5 rounded border border-amber-300">
                          Protected
                        </span>
                      )}
                    </div>
                    <p className="text-xs text-gray-500 mt-1">{activeRole.description}</p>
                  </div>

                  {!isOwnerRole && (
                    <button
                      onClick={() =>
                        savePermissionsMutation.mutate({
                          roleId: activeRole.id,
                          permissionIds: selectedPermissionIds,
                        })
                      }
                      disabled={savePermissionsMutation.isPending}
                      className="px-4 py-2 bg-[#123B32] hover:bg-[#1B6543] text-white text-xs font-bold rounded-lg shadow-sm border border-[#B89A60]/50 transition"
                    >
                      {savePermissionsMutation.isPending ? 'Saving...' : 'Save Permissions'}
                    </button>
                  )}
                </div>

                {isOwnerRole && (
                  <div className="bg-amber-50 border border-amber-200 p-3 rounded-lg text-xs text-amber-900 font-medium flex items-center space-x-2">
                    
                    <span>
                      The <strong>Owner / Super Admin</strong> role possesses all system permissions by design and cannot be modified or reduced.
                    </span>
                  </div>
                )}

                {loadingPermissions ? (
                  <div className="text-center py-12 text-sm text-gray-400">Loading permission catalog...</div>
                ) : (
                  <div className="space-y-6">
                    {Object.entries(permissionsByModule).map(([moduleName, perms]) => {
                      const allChecked = perms.every((p) => selectedPermissionIds.includes(p.id));
                      return (
                        <div key={moduleName} className="border rounded-xl p-4 bg-gray-50/50 space-y-3">
                          <div className="flex items-center justify-between border-b pb-2">
                            <span className="text-xs font-bold uppercase tracking-wider text-gray-700 flex items-center space-x-1.5">
                              
                              <span>{moduleName}</span>
                              <span className="text-gray-400 font-normal">({perms.length})</span>
                            </span>
                            {!isOwnerRole && (
                              <button
                                type="button"
                                onClick={() => handleToggleModuleAll(moduleName, !allChecked)}
                                className="text-[11px] font-semibold text-[#123B32] hover:underline"
                              >
                                {allChecked ? 'Deselect All' : 'Select All'}
                              </button>
                            )}
                          </div>

                          <div className="grid grid-cols-1 md:grid-cols-2 gap-2.5">
                            {perms.map((p) => {
                              const checked = isOwnerRole || selectedPermissionIds.includes(p.id);
                              return (
                                <label
                                  key={p.id}
                                  className={`flex items-start space-x-2.5 p-2 rounded-lg border text-xs transition cursor-pointer ${
                                    checked
                                      ? 'bg-emerald-50/80 border-emerald-300 text-emerald-950 font-medium'
                                      : 'bg-white border-gray-200 text-gray-600 hover:bg-gray-50'
                                  } ${isOwnerRole ? 'cursor-default opacity-85' : ''}`}
                                >
                                  <input
                                    type="checkbox"
                                    checked={checked}
                                    disabled={isOwnerRole}
                                    onChange={() => handleTogglePermission(p.id)}
                                    className="mt-0.5 rounded text-[#123B32] focus:ring-[#123B32]"
                                  />
                                  <div>
                                    <div className="font-mono font-bold tracking-tight text-[11px] text-gray-900">
                                      {p.name}
                                    </div>
                                    <div className="text-[10px] text-gray-500 leading-snug">
                                      {p.description}
                                    </div>
                                  </div>
                                </label>
                              );
                            })}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                )}
              </>
            ) : (
              <div className="text-center py-12 text-sm text-gray-400">Select a role to configure permissions.</div>
            )}
          </div>
        </div>
      </div>

      {/* Add Custom Role Modal */}
      {isAddModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6 space-y-4 border border-gray-200">
            <div className="flex justify-between items-center border-b pb-3">
              <h2 className="text-lg font-bold text-gray-900">Create Custom Role</h2>
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
                createRoleMutation.mutate(newRole);
              }}
              className="space-y-4"
            >
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Role Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Senior Gold Appraiser"
                  value={newRole.name}
                  onChange={(e) => setNewRole({ ...newRole, name: e.target.value })}
                  className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#123B32] outline-none"
                />
              </div>

              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Description</label>
                <textarea
                  rows={3}
                  required
                  placeholder="Responsibilities and access scope of this role"
                  value={newRole.description}
                  onChange={(e) => setNewRole({ ...newRole, description: e.target.value })}
                  className="w-full px-3 py-2 border rounded-lg text-sm focus:ring-2 focus:ring-[#123B32] outline-none"
                />
              </div>

              <div className="flex justify-end space-x-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setIsAddModalOpen(false)}
                  className="px-4 py-2 text-xs font-semibold text-gray-700 border rounded-lg hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={createRoleMutation.isPending}
                  className="px-4 py-2 text-xs font-semibold text-white bg-[#123B32] hover:bg-[#1B6543] rounded-lg shadow-sm border border-[#B89A60]/50"
                >
                  {createRoleMutation.isPending ? 'Creating...' : 'Create Role'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
