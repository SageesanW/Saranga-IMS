'use client';

import React, { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  inventoryCountApi,
  branchApi,
  stockLocationApi,
  InventoryCount,
  Branch,
  StockLocation,
  ActiveCountCheck
} from '@/lib/api';

export default function InventoryCountListPage() {
  const router = useRouter();
  const { user } = useAuth();
  const { activeBranchId } = useBranch();

  const [counts, setCounts] = useState<InventoryCount[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(15);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filter state
  const [branches, setBranches] = useState<Branch[]>([]);
  const [locations, setLocations] = useState<StockLocation[]>([]);
  const [selectedBranchId, setSelectedBranchId] = useState<string>(activeBranchId || '');
  const [selectedLocationId, setSelectedLocationId] = useState<string>('');
  const [selectedStatus, setSelectedStatus] = useState<string>('');

  // Start Count Modal state
  const [isStartModalOpen, setIsStartModalOpen] = useState(false);
  const [modalBranchId, setModalBranchId] = useState<string>(activeBranchId || '');
  const [modalLocationId, setModalLocationId] = useState<string>('');
  const [modalLocations, setModalLocations] = useState<StockLocation[]>([]);
  const [modalNotes, setModalNotes] = useState('');
  const [activeCheck, setActiveCheck] = useState<ActiveCountCheck | null>(null);
  const [isCheckingActive, setIsCheckingActive] = useState(false);
  const [isStarting, setIsStarting] = useState(false);
  const [modalError, setModalError] = useState<string | null>(null);

  // Sync active branch context
  useEffect(() => {
    if (activeBranchId) {
      setSelectedBranchId(activeBranchId);
      setModalBranchId(activeBranchId);
    }
  }, [activeBranchId]);

  // Load branches
  useEffect(() => {
    async function loadBranches() {
      try {
        const branchList = await branchApi.getBranches(true);
        setBranches(branchList);
      } catch (err: any) {
        console.error('Failed to load branches', err);
      }
    }
    loadBranches();
  }, []);

  // Load locations when filter branch changes
  useEffect(() => {
    async function loadFilterLocations() {
      if (!selectedBranchId) {
        setLocations([]);
        return;
      }
      try {
        const locList = await stockLocationApi.getStockLocations(selectedBranchId, true);
        setLocations(locList);
      } catch (err) {
        console.error('Failed to load locations', err);
      }
    }
    loadFilterLocations();
  }, [selectedBranchId]);

  // Load locations for start modal
  useEffect(() => {
    async function loadModalLocations() {
      if (!modalBranchId) {
        setModalLocations([]);
        return;
      }
      try {
        const locList = await stockLocationApi.getStockLocations(modalBranchId, true);
        setModalLocations(locList);
        if (locList.length > 0 && !modalLocationId) {
          setModalLocationId(locList[0].id);
        }
      } catch (err) {
        console.error('Failed to load modal locations', err);
      }
    }
    loadModalLocations();
  }, [modalBranchId]);

  // Check active count whenever modal branch & location change
  useEffect(() => {
    async function verifyActiveCount() {
      if (!modalBranchId || !modalLocationId) {
        setActiveCheck(null);
        return;
      }
      setIsCheckingActive(true);
      try {
        const res = await inventoryCountApi.checkActive(modalBranchId, modalLocationId);
        setActiveCheck(res);
      } catch (err) {
        console.error('Failed to check active count', err);
      } finally {
        setIsCheckingActive(false);
      }
    }
    if (isStartModalOpen) {
      verifyActiveCount();
    }
  }, [modalBranchId, modalLocationId, isStartModalOpen]);

  // Fetch counts list
  const fetchCounts = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const res = await inventoryCountApi.getCounts({
        branchId: selectedBranchId || undefined,
        stockLocationId: selectedLocationId || undefined,
        status: selectedStatus ? parseInt(selectedStatus) : undefined,
        page,
        pageSize,
      });
      setCounts(res.items);
      setTotalCount(res.totalCount);
    } catch (err: any) {
      setError(err.message || 'Failed to load inventory counts.');
    } finally {
      setIsLoading(false);
    }
  }, [selectedBranchId, selectedLocationId, selectedStatus, page, pageSize]);

  useEffect(() => {
    fetchCounts();
  }, [fetchCounts]);

  const handleStartCount = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!modalBranchId || !modalLocationId) {
      setModalError('Please select both Branch and Stock Location.');
      return;
    }
    if (activeCheck?.hasActiveCount) {
      setModalError('An active count is already in progress for this location.');
      return;
    }

    setIsStarting(true);
    setModalError(null);
    try {
      const newCount = await inventoryCountApi.startCount({
        branchId: modalBranchId,
        stockLocationId: modalLocationId,
        notes: modalNotes.trim() || undefined,
      });
      setIsStartModalOpen(false);
      router.push(`/inventory/count/${newCount.id}`);
    } catch (err: any) {
      setModalError(err.message || 'Failed to start inventory count.');
    } finally {
      setIsStarting(false);
    }
  };

  const getStatusBadge = (status: number, statusName: string) => {
    switch (status) {
      case 1: // InProgress
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-blue-100 text-blue-800 border border-blue-200">
            <span className="w-1.5 h-1.5 mr-1.5 rounded-full bg-blue-600 animate-pulse"></span>
            IN PROGRESS
          </span>
        );
      case 2: // RequiresReview
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-200">
            Requires Review
          </span>
        );
      case 3: // Completed
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 border border-emerald-200">
            Completed
          </span>
        );
      case 4: // Cancelled
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-gray-100 text-gray-700 border border-gray-200">
            CANCELLED
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
            {statusName}
          </span>
        );
    }
  };

  const totalPages = Math.ceil(totalCount / pageSize) || 1;

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Header Section */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 bg-white p-6 rounded-xl shadow-sm border border-gray-200">
          <div>
            <div className="flex items-center space-x-3">
              
              <div>
                <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Inventory Count & Stock Verification</h1>
                <p className="text-sm text-gray-500 mt-1">
                  Physical stocktaking and variance verification against immutable frozen baselines.
                </p>
              </div>
            </div>
          </div>
          <div className="flex items-center space-x-3">
            <Link
              href="/inventory"
              className="px-4 py-2 text-sm font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition"
            >
              ← Back to Inventory
            </Link>
            <button
              onClick={() => {
                setModalError(null);
                setModalNotes('');
                setIsStartModalOpen(true);
              }}
              className="inline-flex items-center px-4 py-2 border border-transparent rounded-lg shadow-sm text-sm font-semibold text-white bg-[#123B32] hover:bg-[#1B6543] focus:outline-none transition shadow-sm"
            >
              <span className="mr-2 text-base">＋</span> Start Inventory Count
            </button>
          </div>
        </div>

        {/* Filter Bar */}
        <div className="bg-white p-4 rounded-xl shadow-sm border border-gray-200 flex flex-wrap items-center gap-4">
          <div className="flex-1 min-w-[200px]">
            <label className="block text-xs font-semibold uppercase tracking-wider text-gray-500 mb-1">
              Branch Filter
            </label>
            <select
              value={selectedBranchId}
              onChange={(e) => {
                setSelectedBranchId(e.target.value);
                setSelectedLocationId('');
                setPage(1);
              }}
              className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-[#123B32] focus:ring-[#123B32] bg-gray-50 p-2 border"
            >
              <option value="">All Accessible Branches</option>
              {branches.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.code} - {b.name}
                </option>
              ))}
            </select>
          </div>

          <div className="flex-1 min-w-[200px]">
            <label className="block text-xs font-semibold uppercase tracking-wider text-gray-500 mb-1">
              Stock Location Filter
            </label>
            <select
              value={selectedLocationId}
              onChange={(e) => {
                setSelectedLocationId(e.target.value);
                setPage(1);
              }}
              className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-[#123B32] focus:ring-[#123B32] bg-gray-50 p-2 border"
              disabled={!selectedBranchId}
            >
              <option value="">All Locations in Branch</option>
              {locations.map((loc) => (
                <option key={loc.id} value={loc.id}>
                  {loc.code} - {loc.name}
                </option>
              ))}
            </select>
          </div>

          <div className="w-48">
            <label className="block text-xs font-semibold uppercase tracking-wider text-gray-500 mb-1">
              Status Filter
            </label>
            <select
              value={selectedStatus}
              onChange={(e) => {
                setSelectedStatus(e.target.value);
                setPage(1);
              }}
              className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-[#123B32] focus:ring-[#123B32] bg-gray-50 p-2 border"
            >
              <option value="">All Statuses</option>
              <option value="1">In Progress</option>
              <option value="2">Requires Review</option>
              <option value="3">Completed</option>
              <option value="4">Cancelled</option>
            </select>
          </div>

          {(selectedBranchId || selectedLocationId || selectedStatus) && (
            <div className="flex items-end">
              <button
                onClick={() => {
                  setSelectedBranchId(activeBranchId || '');
                  setSelectedLocationId('');
                  setSelectedStatus('');
                  setPage(1);
                }}
                className="px-3 py-2 text-xs font-semibold text-gray-600 hover:text-gray-900 bg-gray-100 hover:bg-gray-200 rounded-lg transition"
              >
                Reset Filters
              </button>
            </div>
          )}
        </div>

        {/* Error Alert */}
        {error && (
          <div className="p-4 bg-red-50 border border-red-200 rounded-xl text-red-700 text-sm font-medium">
            {error}
          </div>
        )}

        {/* Counts Table */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200 text-left text-sm">
              <thead className="bg-gray-50 text-gray-500 font-semibold uppercase text-xs tracking-wider">
                <tr>
                  <th scope="col" className="px-6 py-3">Session Details</th>
                  <th scope="col" className="px-6 py-3">Branch & Location</th>
                  <th scope="col" className="px-6 py-3">Status</th>
                  <th scope="col" className="px-6 py-3">Expected Items</th>
                  <th scope="col" className="px-6 py-3">Physical Counted</th>
                  <th scope="col" className="px-6 py-3">Variances</th>
                  <th scope="col" className="px-6 py-3">Progress</th>
                  <th scope="col" className="px-6 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {isLoading ? (
                  <tr>
                    <td colSpan={8} className="px-6 py-12 text-center text-gray-500 font-medium">
                      Loading inventory count sessions...
                    </td>
                  </tr>
                ) : counts.length === 0 ? (
                  <tr>
                    <td colSpan={8} className="px-6 py-12 text-center text-gray-500">
                      
                      <p className="font-semibold text-gray-700">No inventory count sessions found.</p>
                      <p className="text-xs text-gray-400 mt-1">
                        Select a branch and stock location, then click &quot;Start Inventory Count&quot; to begin stock verification.
                      </p>
                    </td>
                  </tr>
                ) : (
                  counts.map((c) => {
                    const totalVariances = c.missingCount + c.unexpectedCount;
                    return (
                      <tr key={c.id} className="hover:bg-gray-50 transition">
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="font-bold text-gray-900">
                            #{c.id.substring(0, 8).toUpperCase()}
                          </div>
                          <div className="text-xs text-gray-500">
                            Started {new Date(c.startedAt).toLocaleString('en-GB')}
                          </div>
                          {c.startedByName && (
                            <div className="text-[11px] text-gray-400">By: {c.startedByName}</div>
                          )}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="font-semibold text-gray-900">{c.branchCode} - {c.branchName}</div>
                          <div className="text-xs text-gray-500">
                            {c.stockLocationCode} ({c.stockLocationName})
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          {getStatusBadge(c.status, c.statusName)}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap font-semibold text-gray-700">
                          {c.expectedItemCount} items
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="font-bold text-emerald-700">{c.countedExpectedItemCount} items</div>
                          <div className="text-xs text-gray-400">Matched baseline</div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          {c.status === 1 ? (
                            <span className="text-xs text-gray-400 italic">Determined on finish</span>
                          ) : totalVariances === 0 ? (
                            <span className="text-xs font-semibold text-emerald-600">✓ No Variances</span>
                          ) : (
                            <div className="space-y-0.5 text-xs">
                              {c.missingCount > 0 && (
                                <div className="text-red-600 font-semibold">Missing: {c.missingCount} Missing</div>
                              )}
                              {c.unexpectedCount > 0 && (
                                <div className="text-amber-700 font-semibold">Unexpected: {c.unexpectedCount} Unexpected</div>
                              )}
                            </div>
                          )}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="w-28">
                            <div className="flex justify-between text-xs font-semibold mb-1">
                              <span>{c.progressPercentage.toFixed(1)}%</span>
                            </div>
                            <div className="w-full bg-gray-200 rounded-full h-2 overflow-hidden">
                              <div
                                className={`h-2 rounded-full transition-all duration-300 ${
                                  c.progressPercentage >= 100 ? 'bg-emerald-600' : 'bg-[#B89A60]'
                                }`}
                                style={{ width: `${Math.min(100, c.progressPercentage)}%` }}
                              ></div>
                            </div>
                          </div>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-right text-sm font-medium">
                          {c.status === 1 ? (
                            <Link
                              href={`/inventory/count/${c.id}`}
                              className="inline-flex items-center px-3 py-1.5 border border-transparent text-xs font-bold rounded-lg text-white bg-[#123B32] hover:bg-[#1B6543] shadow-sm transition"
                            >
                              Scan & Count →
                            </Link>
                          ) : c.status === 2 ? (
                            <Link
                              href={`/inventory/count/${c.id}`}
                              className="inline-flex items-center px-3 py-1.5 border border-amber-300 text-xs font-bold rounded-lg text-amber-800 bg-amber-50 hover:bg-amber-100 transition"
                            >
                              Review Variances →
                            </Link>
                          ) : (
                            <Link
                              href={`/inventory/count/${c.id}`}
                              className="text-gray-600 hover:text-gray-900 text-xs font-semibold underline"
                            >
                              View History
                            </Link>
                          )}
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="px-6 py-4 bg-gray-50 border-t border-gray-200 flex items-center justify-between">
              <span className="text-xs text-gray-500">
                Showing page <span className="font-semibold">{page}</span> of{' '}
                <span className="font-semibold">{totalPages}</span> ({totalCount} total sessions)
              </span>
              <div className="flex space-x-2">
                <button
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  disabled={page === 1}
                  className="px-3 py-1.5 border border-gray-300 text-xs rounded-md bg-white hover:bg-gray-50 disabled:opacity-50 transition"
                >
                  Previous
                </button>
                <button
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                  className="px-3 py-1.5 border border-gray-300 text-xs rounded-md bg-white hover:bg-gray-50 disabled:opacity-50 transition"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Start Count Modal */}
      {isStartModalOpen && (
        <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-gray-200 max-w-lg w-full p-6 space-y-6">
            <div className="flex items-center justify-between border-b pb-4">
              <div className="flex items-center space-x-2">
                
                <h3 className="text-lg font-bold text-gray-900">Start New Inventory Count</h3>
              </div>
              <button
                onClick={() => setIsStartModalOpen(false)}
                className="text-gray-400 hover:text-gray-600 text-xl font-bold"
              >
                ✕
              </button>
            </div>

            {modalError && (
              <div className="p-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-xs font-semibold">
                {modalError}
              </div>
            )}

            <form onSubmit={handleStartCount} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-gray-600 mb-1">
                  Target Branch <span className="text-red-500">*</span>
                </label>
                <select
                  value={modalBranchId}
                  onChange={(e) => {
                    setModalBranchId(e.target.value);
                    setModalLocationId('');
                  }}
                  className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-[#123B32] focus:ring-[#123B32] bg-gray-50 p-2.5 border"
                  required
                >
                  <option value="">Select Branch...</option>
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.code} - {b.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-gray-600 mb-1">
                  Stock Location to Verify <span className="text-red-500">*</span>
                </label>
                <select
                  value={modalLocationId}
                  onChange={(e) => setModalLocationId(e.target.value)}
                  className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-[#123B32] focus:ring-[#123B32] bg-gray-50 p-2.5 border"
                  disabled={!modalBranchId}
                  required
                >
                  <option value="">Select Location...</option>
                  {modalLocations.map((loc) => (
                    <option key={loc.id} value={loc.id}>
                      {loc.code} - {loc.name}
                    </option>
                  ))}
                </select>
              </div>

              {/* Active Count Warning Banner */}
              {isCheckingActive ? (
                <div className="text-xs text-gray-500 italic py-1">Verifying location count status...</div>
              ) : activeCheck?.hasActiveCount ? (
                <div className="p-3 bg-red-50 border border-red-300 rounded-lg text-red-800 text-xs space-y-1">
                  <div className="font-bold flex items-center">
                     Active Count In Progress
                  </div>
                  <p>
                    An inventory count is already active for this stock location (Session #{activeCheck.activeCountId?.substring(0, 8).toUpperCase()}, Status: {activeCheck.statusName}).
                  </p>
                  <p className="font-semibold">
                    Saranga enforces exactly one active count per location at a time.
                  </p>
                </div>
              ) : modalLocationId ? (
                <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-lg text-emerald-800 text-xs">
                  <span className="font-bold">Location Available:</span> No active count session in progress. Ready to capture frozen baseline.
                </div>
              ) : null}

              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-gray-600 mb-1">
                  Count Notes / Session Goal (Optional)
                </label>
                <textarea
                  value={modalNotes}
                  onChange={(e) => setModalNotes(e.target.value)}
                  rows={2}
                  placeholder="Record observations from the physical stock count"
                  className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-[#123B32] focus:ring-[#123B32] bg-gray-50 p-2.5 border"
                />
              </div>

              {/* Baseline Guarantee Callout */}
              <div className="p-3.5 bg-blue-50 border border-blue-200 rounded-xl text-blue-900 text-xs space-y-1.5">
                <div className="font-bold text-blue-950 flex items-center">
                   Frozen Baseline Guarantee
                </div>
                <p>
                  Starting this session will freeze a permanent snapshot of all physical jewellery items currently recorded in this location (excluding SOLD items).
                </p>
                <p className="text-[11px] text-blue-700">
                  Subsequent live system transactions will not alter this baseline, guaranteeing audit precision.
                </p>
              </div>

              <div className="flex items-center justify-end space-x-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setIsStartModalOpen(false)}
                  className="px-4 py-2 text-sm font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isStarting || isCheckingActive || activeCheck?.hasActiveCount || !modalLocationId}
                  className="px-5 py-2 text-sm font-bold text-white bg-[#123B32] hover:bg-[#1B6543] rounded-lg shadow disabled:opacity-50 transition"
                >
                  {isStarting ? 'Capturing Baseline...' : 'Start Session & Freeze Baseline'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
