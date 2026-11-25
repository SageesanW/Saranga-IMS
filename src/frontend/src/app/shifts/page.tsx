'use client';

import { useState, useEffect, useCallback } from 'react';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  shiftApi,
  Shift,
  CashDrawer,
  ShiftOpeningDefaults,
  PagedResult
} from '@/lib/api';

export default function ShiftsPage() {
  const { user } = useAuth();
  const { activeBranchId } = useBranch();

  // Active shift for current user
  const [activeShift, setActiveShift] = useState<Shift | null>(null);
  const [loadingActiveShift, setLoadingActiveShift] = useState<boolean>(true);

  // Cash drawers for current branch
  const [drawers, setDrawers] = useState<CashDrawer[]>([]);
  const [loadingDrawers, setLoadingDrawers] = useState<boolean>(false);

  // Shift history
  const [shifts, setShifts] = useState<PagedResult<Shift> | null>(null);
  const [historyPage, setHistoryPage] = useState<number>(1);
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [loadingHistory, setLoadingHistory] = useState<boolean>(false);

  // Modals
  const [showOpenModal, setShowOpenModal] = useState<boolean>(false);
  const [showCloseModal, setShowCloseModal] = useState<boolean>(false);
  const [showAddDrawerModal, setShowAddDrawerModal] = useState<boolean>(false);
  const [selectedShiftForDetail, setSelectedShiftForDetail] = useState<Shift | null>(null);

  // Open Shift Form State
  const [selectedDrawerId, setSelectedDrawerId] = useState<string>('');
  const [openingDefaults, setOpeningDefaults] = useState<ShiftOpeningDefaults | null>(null);
  const [loadingDefaults, setLoadingDefaults] = useState<boolean>(false);
  const [openingFloat, setOpeningFloat] = useState<string>('');
  const [openingDifferenceReason, setOpeningDifferenceReason] = useState<string>('');
  const [openingNotes, setOpeningNotes] = useState<string>('');
  const [openSubmitting, setOpenSubmitting] = useState<boolean>(false);
  const [openError, setOpenError] = useState<string | null>(null);

  // Close Shift Form State
  const [actualClosingCash, setActualClosingCash] = useState<string>('');
  const [closingVarianceReason, setClosingVarianceReason] = useState<string>('');
  const [closingNotes, setClosingNotes] = useState<string>('');
  const [closeConfirm, setCloseConfirm] = useState<boolean>(false);
  const [closeSubmitting, setCloseSubmitting] = useState<boolean>(false);
  const [closeError, setCloseError] = useState<string | null>(null);

  // Add Drawer Form State
  const [newDrawerCode, setNewDrawerCode] = useState<string>('');
  const [newDrawerName, setNewDrawerName] = useState<string>('');
  const [newDrawerDesc, setNewDrawerDesc] = useState<string>('');
  const [drawerSubmitting, setDrawerSubmitting] = useState<boolean>(false);
  const [drawerError, setDrawerError] = useState<string | null>(null);

  // Notification / Alert
  const [alertMessage, setAlertMessage] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const canStartShift = user?.permissions.includes('SHIFT_START') || user?.isGlobalAdmin;
  const canEndShift = user?.permissions.includes('SHIFT_END') || user?.isGlobalAdmin;

  // Load Active Shift
  const fetchActiveShift = useCallback(async () => {
    try {
      setLoadingActiveShift(true);
      const shift = await shiftApi.getActiveCashierShift();
      setActiveShift(shift);
    } catch {
      setActiveShift(null);
    } finally {
      setLoadingActiveShift(false);
    }
  }, []);

  // Load Drawers
  const fetchDrawers = useCallback(async () => {
    if (!activeBranchId) return;
    try {
      setLoadingDrawers(true);
      const list = await shiftApi.getCashDrawers(activeBranchId);
      setDrawers(list);
    } catch (err: unknown) {
      console.error('Failed to load cash drawers', err);
    } finally {
      setLoadingDrawers(false);
    }
  }, [activeBranchId]);

  // Load Shift History
  const fetchHistory = useCallback(async () => {
    if (!activeBranchId) return;
    try {
      setLoadingHistory(true);
      const statusNum = statusFilter === 'open' ? 0 : statusFilter === 'closed' ? 1 : undefined;
      const res = await shiftApi.getShifts({
        branchId: activeBranchId,
        status: statusNum,
        page: historyPage,
        pageSize: 15
      });
      setShifts(res);
    } catch (err: unknown) {
      console.error('Failed to load shift history', err);
    } finally {
      setLoadingHistory(false);
    }
  }, [activeBranchId, historyPage, statusFilter]);

  useEffect(() => {
    fetchActiveShift();
    fetchDrawers();
    fetchHistory();
  }, [fetchActiveShift, fetchDrawers, fetchHistory]);

  // Handle Drawer Selection in Open Modal
  const handleDrawerSelect = async (drawerId: string) => {
    setSelectedDrawerId(drawerId);
    setOpenError(null);
    if (!drawerId) {
      setOpeningDefaults(null);
      setOpeningFloat('');
      return;
    }

    try {
      setLoadingDefaults(true);
      const defaults = await shiftApi.getOpeningDefaults(drawerId);
      setOpeningDefaults(defaults);
      setOpeningFloat(defaults.systemExpectedOpeningBalance.toFixed(2));
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to fetch opening float defaults';
      setOpenError(msg);
    } finally {
      setLoadingDefaults(false);
    }
  };

  // Submit Open Shift
  const handleOpenShift = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeBranchId || !selectedDrawerId) {
      setOpenError('Please select an active cash drawer.');
      return;
    }

    const floatNum = parseFloat(openingFloat);
    if (isNaN(floatNum) || floatNum < 0) {
      setOpenError('Please enter a valid positive opening cash amount.');
      return;
    }

    const expectedFloat = openingDefaults?.systemExpectedOpeningBalance ?? 0;
    const diff = floatNum - expectedFloat;
    if (diff !== 0 && !openingDifferenceReason.trim()) {
      setOpenError(`An explanation reason is mandatory for the opening balance difference of Rs. ${Math.abs(diff).toFixed(2)}.`);
      return;
    }

    try {
      setOpenSubmitting(true);
      setOpenError(null);
      await shiftApi.openShift({
        branchId: activeBranchId,
        cashDrawerId: selectedDrawerId,
        openingBalance: floatNum,
        openingDifferenceReason: diff !== 0 ? openingDifferenceReason.trim() : undefined,
        openingNotes: openingNotes.trim() || undefined
      });

      setAlertMessage({ type: 'success', message: 'Cashier shift opened successfully!' });
      setShowOpenModal(false);
      resetOpenForm();
      await Promise.all([fetchActiveShift(), fetchDrawers(), fetchHistory()]);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to open shift.';
      setOpenError(msg);
    } finally {
      setOpenSubmitting(false);
    }
  };

  const resetOpenForm = () => {
    setSelectedDrawerId('');
    setOpeningDefaults(null);
    setOpeningFloat('');
    setOpeningDifferenceReason('');
    setOpeningNotes('');
    setOpenError(null);
  };

  // Submit Close Shift
  const handleCloseShift = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeShift) return;

    const actualCashNum = parseFloat(actualClosingCash);
    if (isNaN(actualCashNum) || actualCashNum < 0) {
      setCloseError('Please enter a valid non-negative physical cash amount.');
      return;
    }

    const expectedCash = activeShift.expectedClosingBalance ?? activeShift.openingBalance;
    const variance = actualCashNum - expectedCash;
    if (variance !== 0 && !closingVarianceReason.trim()) {
      const vType = variance < 0 ? 'Shortage' : 'Surplus';
      setCloseError(`A mandatory explanation reason is required for the closing cash ${vType} of Rs. ${Math.abs(variance).toFixed(2)}.`);
      return;
    }

    if (!closeConfirm) {
      setCloseError('Please confirm physical count verification before closing the shift.');
      return;
    }

    try {
      setCloseSubmitting(true);
      setCloseError(null);
      await shiftApi.closeShift(activeShift.id, {
        actualClosingBalance: actualCashNum,
        closingVarianceReason: variance !== 0 ? closingVarianceReason.trim() : undefined,
        closingNotes: closingNotes.trim() || undefined,
        rowVersion: activeShift.version
      });

      setAlertMessage({ type: 'success', message: `Shift ${activeShift.shiftNumber} closed and reconciled successfully.` });
      setShowCloseModal(false);
      resetCloseForm();
      await Promise.all([fetchActiveShift(), fetchDrawers(), fetchHistory()]);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to close shift.';
      setCloseError(msg);
    } finally {
      setCloseSubmitting(false);
    }
  };

  const resetCloseForm = () => {
    setActualClosingCash('');
    setClosingVarianceReason('');
    setClosingNotes('');
    setCloseConfirm(false);
    setCloseError(null);
  };

  // Submit Add Cash Drawer
  const handleAddDrawer = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeBranchId) return;

    if (!newDrawerCode.trim() || !newDrawerName.trim()) {
      setDrawerError('Drawer code and drawer name are required.');
      return;
    }

    try {
      setDrawerSubmitting(true);
      setDrawerError(null);
      await shiftApi.createCashDrawer({
        branchId: activeBranchId,
        drawerCode: newDrawerCode.trim().toUpperCase(),
        name: newDrawerName.trim(),
        description: newDrawerDesc.trim() || undefined
      });

      setAlertMessage({ type: 'success', message: 'New cash drawer registered successfully.' });
      setShowAddDrawerModal(false);
      setNewDrawerCode('');
      setNewDrawerName('');
      setNewDrawerDesc('');
      await fetchDrawers();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to create cash drawer.';
      setDrawerError(msg);
    } finally {
      setDrawerSubmitting(false);
    }
  };

  // Derived calculations for open modal
  const openFloatNum = parseFloat(openingFloat) || 0;
  const sysExpectedFloat = openingDefaults?.systemExpectedOpeningBalance ?? 0;
  const openDiscrepancy = openFloatNum - sysExpectedFloat;

  // Derived calculations for close modal
  const closeActualNum = parseFloat(actualClosingCash) || 0;
  const closeExpectedNum = activeShift ? (activeShift.expectedClosingBalance ?? activeShift.openingBalance) : 0;
  const closeVariance = closeActualNum - closeExpectedNum;

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Toast / Alert Notification */}
        {alertMessage && (
          <div
            className={`p-4 rounded-xl border flex items-center justify-between shadow-sm animate-fade-in ${
              alertMessage.type === 'success'
                ? 'bg-emerald-50 border-emerald-300 text-emerald-900'
                : 'bg-rose-50 border-rose-300 text-rose-900'
            }`}
          >
            <div className="flex items-center space-x-3">
              <span className="text-xl">{alertMessage.type === 'success' ? '✅' : '⚠️'}</span>
              <span className="text-sm font-medium">{alertMessage.message}</span>
            </div>
            <button
              onClick={() => setAlertMessage(null)}
              className="text-gray-400 hover:text-gray-600 font-bold ml-4"
            >
              ✕
            </button>
          </div>
        )}

        {/* Page Header */}
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 bg-white p-6 rounded-2xl shadow-sm border border-gray-100">
          <div>
            <div className="flex items-center space-x-3">
              <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-[#0D3826] to-[#1B6543] flex items-center justify-center text-white text-xl shadow">
                💵
              </div>
              <div>
                <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Shift Opening & Closing</h1>
                <p className="text-sm text-gray-500">
                  Manage physical cash drawer shifts, opening floats, active cashier sessions, and end-of-shift reconciliation.
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center space-x-3">
            <button
              onClick={() => setShowAddDrawerModal(true)}
              className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-xl hover:bg-gray-50 shadow-sm transition-colors flex items-center space-x-1.5"
            >
              <span>➕</span>
              <span>New Drawer</span>
            </button>
            {!activeShift && canStartShift && (
              <button
                onClick={() => {
                  setShowOpenModal(true);
                  if (drawers.length > 0 && !selectedDrawerId) {
                    const firstAvail = drawers.find((d) => !d.hasActiveShift);
                    if (firstAvail) handleDrawerSelect(firstAvail.id);
                  }
                }}
                className="px-5 py-2.5 text-sm font-semibold text-white bg-gradient-to-r from-[#0D3826] to-[#1B6543] hover:from-[#155438] hover:to-[#238055] rounded-xl shadow-md transition-all flex items-center space-x-2 border border-[#D4AF37]/40"
              >
                <span>🔑</span>
                <span>Open Shift</span>
              </button>
            )}
            {activeShift && canEndShift && (
              <button
                onClick={() => {
                  setActualClosingCash((activeShift.expectedClosingBalance ?? activeShift.openingBalance).toFixed(2));
                  setShowCloseModal(true);
                }}
                className="px-5 py-2.5 text-sm font-semibold text-white bg-gradient-to-r from-[#991b1b] to-[#b91c1c] hover:from-[#7f1d1d] hover:to-[#991b1b] rounded-xl shadow-md transition-all flex items-center space-x-2"
              >
                <span>🔒</span>
                <span>Close Active Shift</span>
              </button>
            )}
          </div>
        </div>

        {/* Active Cashier Shift Banner */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-base font-bold text-gray-900 flex items-center space-x-2">
                <span>👤</span>
                <span>Your Active Shift Status</span>
              </h2>
              {loadingActiveShift ? (
                <span className="text-xs text-gray-400 animate-pulse">Checking shift status...</span>
              ) : activeShift ? (
                <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-bold bg-emerald-100 text-emerald-800 border border-emerald-300">
                  <span className="w-2 h-2 rounded-full bg-emerald-500 mr-1.5 animate-ping"></span>
                  Active Shift #{activeShift.shiftNumber}
                </span>
              ) : (
                <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-amber-100 text-amber-800 border border-amber-300">
                  No Active Shift
                </span>
              )}
            </div>

            {loadingActiveShift ? (
              <div className="py-8 text-center text-gray-400 text-sm">Loading cashier session...</div>
            ) : activeShift ? (
              <div className="grid grid-cols-1 md:grid-cols-4 gap-4 p-4 rounded-xl bg-gradient-to-r from-[#0D3826]/5 to-[#D4AF37]/10 border border-[#D4AF37]/30">
                <div>
                  <span className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Cash Drawer</span>
                  <p className="text-base font-bold text-gray-900 mt-0.5">
                    {activeShift.cashDrawerName} <span className="text-xs font-normal text-gray-500">({activeShift.cashDrawerCode})</span>
                  </p>
                </div>
                <div>
                  <span className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Opened At</span>
                  <p className="text-sm font-medium text-gray-900 mt-0.5">
                    {new Date(activeShift.openedAt).toLocaleString('en-GB', {
                      day: '2-digit',
                      month: 'short',
                      year: 'numeric',
                      hour: '2-digit',
                      minute: '2-digit'
                    })}
                  </p>
                </div>
                <div>
                  <span className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Opening Float</span>
                  <p className="text-lg font-extrabold text-[#0D3826] mt-0.5">
                    Rs. {activeShift.openingBalance.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                  </p>
                </div>
                <div className="flex items-center justify-end">
                  {canEndShift && (
                    <button
                      onClick={() => {
                        setActualClosingCash((activeShift.expectedClosingBalance ?? activeShift.openingBalance).toFixed(2));
                        setShowCloseModal(true);
                      }}
                      className="w-full md:w-auto px-4 py-2 text-sm font-bold text-white bg-rose-700 hover:bg-rose-800 rounded-xl shadow transition"
                    >
                      Reconcile & Close
                    </button>
                  )}
                </div>
              </div>
            ) : (
              <div className="p-6 rounded-xl bg-amber-50/60 border border-amber-200/80 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="flex items-start space-x-3">
                  <span className="text-2xl mt-0.5">⚠️</span>
                  <div>
                    <h3 className="text-sm font-bold text-amber-900">Shift opening is required before POS sales</h3>
                    <p className="text-xs text-amber-700 mt-0.5">
                      You do not have an active cash drawer shift open. Count physical opening float cash and open a shift to begin cashier operations.
                    </p>
                  </div>
                </div>
                {canStartShift && (
                  <button
                    onClick={() => {
                      setShowOpenModal(true);
                      if (drawers.length > 0 && !selectedDrawerId) {
                        const firstAvail = drawers.find((d) => !d.hasActiveShift);
                        if (firstAvail) handleDrawerSelect(firstAvail.id);
                      }
                    }}
                    className="whitespace-nowrap px-4 py-2 text-xs font-bold text-white bg-[#0D3826] hover:bg-[#1B6543] rounded-lg shadow transition"
                  >
                    Open Shift Now
                  </button>
                )}
              </div>
            )}
          </div>
        </div>

        {/* Cash Drawers Grid */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 p-6">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-base font-bold text-gray-900 flex items-center space-x-2">
                <span>🗄️</span>
                <span>Branch Cash Drawers</span>
              </h2>
              <p className="text-xs text-gray-500 mt-0.5">
                One active cashier shift per drawer is strictly enforced by PostgreSQL database constraints.
              </p>
            </div>
            <span className="text-xs font-semibold text-gray-500 bg-gray-100 px-2.5 py-1 rounded-lg">
              {drawers.length} {drawers.length === 1 ? 'Drawer' : 'Drawers'} Configured
            </span>
          </div>

          {loadingDrawers ? (
            <div className="py-6 text-center text-gray-400 text-sm">Loading cash drawers...</div>
          ) : drawers.length === 0 ? (
            <div className="py-8 text-center bg-gray-50 rounded-xl border border-dashed border-gray-200">
              <span className="text-3xl">📭</span>
              <p className="text-sm font-semibold text-gray-700 mt-2">No cash drawers configured for this branch</p>
              <p className="text-xs text-gray-500 mt-1">Register your first physical cash drawer to enable shift management.</p>
              <button
                onClick={() => setShowAddDrawerModal(true)}
                className="mt-3 px-3.5 py-1.5 text-xs font-semibold text-white bg-[#0D3826] rounded-lg hover:bg-[#1B6543] transition"
              >
                Register Drawer
              </button>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {drawers.map((d) => (
                <div
                  key={d.id}
                  className={`p-4 rounded-xl border transition-all ${
                    d.hasActiveShift
                      ? 'bg-emerald-50/40 border-emerald-200 shadow-sm'
                      : 'bg-white border-gray-200 hover:border-gray-300'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-mono font-bold text-gray-500 bg-gray-100 px-2 py-0.5 rounded">
                      {d.drawerCode}
                    </span>
                    {d.hasActiveShift ? (
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-200">
                        In Use
                      </span>
                    ) : (
                      <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold bg-gray-100 text-gray-600">
                        Available
                      </span>
                    )}
                  </div>
                  <h3 className="text-sm font-bold text-gray-900 mt-2">{d.name}</h3>
                  {d.description && <p className="text-xs text-gray-500 mt-0.5 line-clamp-1">{d.description}</p>}

                  <div className="mt-4 pt-3 border-t border-gray-100 flex items-center justify-between text-xs text-gray-500">
                    <span>
                      {d.hasActiveShift ? (
                        <span className="text-emerald-800 font-medium">👤 {d.activeCashierName || 'Cashier in session'}</span>
                      ) : (
                        <span>Ready for shift</span>
                      )}
                    </span>
                    {!d.hasActiveShift && !activeShift && canStartShift && (
                      <button
                        onClick={() => {
                          handleDrawerSelect(d.id);
                          setShowOpenModal(true);
                        }}
                        className="text-xs font-bold text-[#0D3826] hover:underline"
                      >
                        Open Here →
                      </button>
                    )}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Shift History & Audit Reconciliation Table */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="p-6 border-b border-gray-100 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h2 className="text-base font-bold text-gray-900 flex items-center space-x-2">
                <span>📋</span>
                <span>Shift History & Reconciliation Board</span>
              </h2>
              <p className="text-xs text-gray-500 mt-0.5">
                Immutable financial shift records. Closing variances equal physical cash minus backend expected cash.
              </p>
            </div>

            {/* Status Filter */}
            <div className="flex items-center space-x-2">
              <span className="text-xs text-gray-500 font-medium">Filter:</span>
              <select
                value={statusFilter}
                onChange={(e) => {
                  setStatusFilter(e.target.value);
                  setHistoryPage(1);
                }}
                className="text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-700 focus:outline-none focus:ring-1 focus:ring-[#0D3826]"
              >
                <option value="all">All Shifts</option>
                <option value="open">Active Open</option>
                <option value="closed">Closed & Reconciled</option>
              </select>
            </div>
          </div>

          {/* Table */}
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs text-gray-600">
              <thead className="bg-gray-50 text-gray-700 uppercase tracking-wider font-semibold border-b border-gray-100">
                <tr>
                  <th className="px-6 py-3.5">Shift Number</th>
                  <th className="px-6 py-3.5">Drawer</th>
                  <th className="px-6 py-3.5">Cashier</th>
                  <th className="px-6 py-3.5">Status</th>
                  <th className="px-6 py-3.5">Opened At</th>
                  <th className="px-6 py-3.5">Closed At</th>
                  <th className="px-6 py-3.5 text-right">Opening Float</th>
                  <th className="px-6 py-3.5 text-right">Actual Cash</th>
                  <th className="px-6 py-3.5 text-right">Variance</th>
                  <th className="px-6 py-3.5 text-center">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {loadingHistory ? (
                  <tr>
                    <td colSpan={10} className="px-6 py-12 text-center text-gray-400">
                      Loading shift records...
                    </td>
                  </tr>
                ) : !shifts || shifts.items.length === 0 ? (
                  <tr>
                    <td colSpan={10} className="px-6 py-12 text-center text-gray-400">
                      No shift records found for this branch.
                    </td>
                  </tr>
                ) : (
                  shifts.items.map((s) => {
                    const variance = s.closingVariance ?? 0;
                    return (
                      <tr key={s.id} className="hover:bg-gray-50/80 transition-colors">
                        <td className="px-6 py-4 font-mono font-bold text-gray-900">
                          {s.shiftNumber}
                        </td>
                        <td className="px-6 py-4 text-gray-800 font-medium">
                          {s.cashDrawerName}
                        </td>
                        <td className="px-6 py-4 text-gray-800">
                          {s.cashierName || 'Cashier'}
                        </td>
                        <td className="px-6 py-4">
                          {s.status === 0 ? (
                            <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-200">
                              Open
                            </span>
                          ) : (
                            <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-medium bg-gray-100 text-gray-700">
                              Closed
                            </span>
                          )}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-gray-600">
                          {new Date(s.openedAt).toLocaleString('en-GB', {
                            day: '2-digit',
                            month: 'short',
                            hour: '2-digit',
                            minute: '2-digit'
                          })}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-gray-600">
                          {s.closedAt
                            ? new Date(s.closedAt).toLocaleString('en-GB', {
                                day: '2-digit',
                                month: 'short',
                                hour: '2-digit',
                                minute: '2-digit'
                              })
                            : '—'}
                        </td>
                        <td className="px-6 py-4 text-right font-mono text-gray-900 font-medium">
                          Rs. {s.openingBalance.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                        </td>
                        <td className="px-6 py-4 text-right font-mono text-gray-900 font-medium">
                          {s.actualClosingBalance != null
                            ? `Rs. ${s.actualClosingBalance.toLocaleString('en-LK', { minimumFractionDigits: 2 })}`
                            : '—'}
                        </td>
                        <td className="px-6 py-4 text-right font-mono font-bold">
                          {s.status === 0 ? (
                            <span className="text-gray-400 font-normal">Pending</span>
                          ) : variance === 0 ? (
                            <span className="text-emerald-700">Rs. 0.00</span>
                          ) : variance < 0 ? (
                            <span className="text-rose-700 font-bold" title={s.closingVarianceReason || 'Shortage'}>
                              Rs. {variance.toLocaleString('en-LK', { minimumFractionDigits: 2 })} ⚠️
                            </span>
                          ) : (
                            <span className="text-blue-700 font-bold" title={s.closingVarianceReason || 'Surplus'}>
                              +Rs. {variance.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                            </span>
                          )}
                        </td>
                        <td className="px-6 py-4 text-center">
                          <button
                            onClick={() => setSelectedShiftForDetail(s)}
                            className="text-xs font-semibold text-[#0D3826] hover:text-[#1B6543] underline"
                          >
                            Details
                          </button>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          {shifts && shifts.totalPages > 1 && (
            <div className="p-4 border-t border-gray-100 flex items-center justify-between text-xs text-gray-500">
              <span>
                Showing {((shifts.page - 1) * shifts.pageSize) + 1} to{' '}
                {Math.min(shifts.page * shifts.pageSize, shifts.totalCount)} of {shifts.totalCount} shifts
              </span>
              <div className="flex items-center space-x-2">
                <button
                  disabled={!shifts.hasPreviousPage}
                  onClick={() => setHistoryPage((p) => Math.max(p - 1, 1))}
                  className="px-3 py-1.5 border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition"
                >
                  Previous
                </button>
                <span className="font-semibold text-gray-800">
                  Page {shifts.page} of {shifts.totalPages}
                </span>
                <button
                  disabled={!shifts.hasNextPage}
                  onClick={() => setHistoryPage((p) => p + 1)}
                  className="px-3 py-1.5 border border-gray-200 rounded-lg disabled:opacity-40 hover:bg-gray-50 transition"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* ========================================================================= */}
      {/* MODAL 1: OPEN SHIFT                                                      */}
      {/* ========================================================================= */}
      {showOpenModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4 overflow-y-auto animate-fade-in">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-gray-100 relative">
            <button
              onClick={() => {
                setShowOpenModal(false);
                resetOpenForm();
              }}
              className="absolute top-4 right-4 text-gray-400 hover:text-gray-600 text-lg font-bold"
            >
              ✕
            </button>

            <div className="flex items-center space-x-3 mb-5">
              <div className="w-10 h-10 rounded-xl bg-emerald-100 text-emerald-800 flex items-center justify-center text-lg font-bold">
                🔑
              </div>
              <div>
                <h3 className="text-lg font-bold text-gray-900">Open Cashier Shift</h3>
                <p className="text-xs text-gray-500">Assign drawer and verify initial opening physical float cash.</p>
              </div>
            </div>

            {openError && (
              <div className="mb-4 p-3 bg-rose-50 border border-rose-200 rounded-xl text-xs text-rose-800 flex items-start space-x-2">
                <span>⚠️</span>
                <span>{openError}</span>
              </div>
            )}

            <form onSubmit={handleOpenShift} className="space-y-4">
              {/* Drawer Selector */}
              <div>
                <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-1">
                  Cash Drawer <span className="text-rose-500">*</span>
                </label>
                <select
                  value={selectedDrawerId}
                  onChange={(e) => handleDrawerSelect(e.target.value)}
                  className="w-full text-sm border border-gray-300 rounded-xl px-3 py-2.5 bg-white text-gray-800 focus:outline-none focus:ring-2 focus:ring-[#0D3826]"
                  required
                >
                  <option value="">-- Select Cash Drawer --</option>
                  {drawers.map((d) => (
                    <option key={d.id} value={d.id} disabled={d.hasActiveShift}>
                      {d.drawerCode} - {d.name} {d.hasActiveShift ? '(Already In Use)' : '(Available)'}
                    </option>
                  ))}
                </select>
              </div>

              {/* System Expected Float Alert */}
              {loadingDefaults ? (
                <div className="p-3 bg-gray-50 rounded-xl text-xs text-gray-400 text-center animate-pulse">
                  Retrieving previous shift closing cash baseline...
                </div>
              ) : openingDefaults && (
                <div className="p-3.5 bg-blue-50/70 border border-blue-200 rounded-xl">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-blue-900">System Expected Opening Float:</span>
                    <span className="text-sm font-extrabold text-blue-900 font-mono">
                      Rs. {openingDefaults.systemExpectedOpeningBalance.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                  <p className="text-[11px] text-blue-700 mt-1">
                    {openingDefaults.previousShiftNumber ? (
                      <>Derived from previous closed shift #{openingDefaults.previousShiftNumber} actual closing cash.</>
                    ) : (
                      <>First shift on this drawer (default float Rs. 0.00).</>
                    )}
                  </p>
                </div>
              )}

              {/* Physical Counted Float Input */}
              <div>
                <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-1">
                  Physical Counted Opening Cash (Rs.) <span className="text-rose-500">*</span>
                </label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={openingFloat}
                  onChange={(e) => setOpeningFloat(e.target.value)}
                  placeholder="0.00"
                  className="w-full text-base font-mono font-bold border border-gray-300 rounded-xl px-3 py-2.5 text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0D3826]"
                  required
                />
              </div>

              {/* Opening Difference Alert & Reason */}
              {openDiscrepancy !== 0 && selectedDrawerId && (
                <div className="p-3.5 bg-amber-50 border border-amber-300 rounded-xl space-y-2">
                  <div className="flex items-center justify-between text-xs font-bold text-amber-900">
                    <span>Discrepancy Detected:</span>
                    <span className="font-mono">
                      {openDiscrepancy > 0 ? '+' : ''}Rs. {openDiscrepancy.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                  <p className="text-[11px] text-amber-800">
                    Physical counted cash does not match system expected float. A mandatory reason is required by store policy.
                  </p>
                  <div>
                    <label className="block text-[11px] font-bold text-amber-900 uppercase tracking-wider mb-1">
                      Mandatory Discrepancy Reason <span className="text-rose-600">*</span>
                    </label>
                    <textarea
                      rows={2}
                      value={openingDifferenceReason}
                      onChange={(e) => setOpeningDifferenceReason(e.target.value)}
                      placeholder="Explain reason for opening balance difference..."
                      className="w-full text-xs border border-amber-300 rounded-lg p-2 bg-white text-gray-800 focus:outline-none focus:ring-1 focus:ring-amber-500"
                      required
                    />
                  </div>
                </div>
              )}

              {/* Opening Notes */}
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Opening Notes (Optional)</label>
                <textarea
                  rows={2}
                  value={openingNotes}
                  onChange={(e) => setOpeningNotes(e.target.value)}
                  placeholder="Add any optional notes about drawer condition or coins..."
                  className="w-full text-xs border border-gray-300 rounded-xl p-2.5 text-gray-800 focus:outline-none focus:ring-1 focus:ring-[#0D3826]"
                />
              </div>

              {/* Action Buttons */}
              <div className="pt-2 flex items-center justify-end space-x-3">
                <button
                  type="button"
                  onClick={() => {
                    setShowOpenModal(false);
                    resetOpenForm();
                  }}
                  className="px-4 py-2 text-xs font-semibold text-gray-600 hover:text-gray-800"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={openSubmitting || !selectedDrawerId}
                  className="px-5 py-2.5 text-xs font-bold text-white bg-gradient-to-r from-[#0D3826] to-[#1B6543] hover:from-[#155438] hover:to-[#238055] rounded-xl shadow disabled:opacity-50 transition"
                >
                  {openSubmitting ? 'Opening Shift...' : 'Confirm & Open Shift'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 2: CLOSE SHIFT & RECONCILIATION                                     */}
      {/* ========================================================================= */}
      {showCloseModal && activeShift && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4 overflow-y-auto animate-fade-in">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-gray-100 relative">
            <button
              onClick={() => {
                setShowCloseModal(false);
                resetCloseForm();
              }}
              className="absolute top-4 right-4 text-gray-400 hover:text-gray-600 text-lg font-bold"
            >
              ✕
            </button>

            <div className="flex items-center space-x-3 mb-4">
              <div className="w-10 h-10 rounded-xl bg-rose-100 text-rose-800 flex items-center justify-center text-lg font-bold">
                🔒
              </div>
              <div>
                <h3 className="text-lg font-bold text-gray-900">Close Shift Reconciliation</h3>
                <p className="text-xs text-gray-500">Shift #{activeShift.shiftNumber} • {activeShift.cashDrawerName}</p>
              </div>
            </div>

            {closeError && (
              <div className="mb-4 p-3 bg-rose-50 border border-rose-200 rounded-xl text-xs text-rose-800 flex items-start space-x-2">
                <span>⚠️</span>
                <span>{closeError}</span>
              </div>
            )}

            <form onSubmit={handleCloseShift} className="space-y-4">
              {/* Balances Summary Box */}
              <div className="p-4 bg-gray-50 rounded-xl border border-gray-200 space-y-2">
                <div className="flex items-center justify-between text-xs text-gray-600">
                  <span>Opening Float:</span>
                  <span className="font-mono font-semibold text-gray-800">
                    Rs. {activeShift.openingBalance.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                  </span>
                </div>
                <div className="flex items-center justify-between text-xs text-gray-600 border-t border-gray-200 pt-2">
                  <span className="font-bold text-gray-900">Backend Expected Closing Cash:</span>
                  <span className="font-mono font-extrabold text-[#0D3826] text-sm">
                    Rs. {closeExpectedNum.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                  </span>
                </div>
                <p className="text-[10px] text-gray-400">
                  *Calculated by the backend authoritative ledger engine (no artificial balancing movements).
                </p>
              </div>

              {/* Physical Cash Counted Input */}
              <div>
                <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-1">
                  Physical Cash Counted (Rs.) <span className="text-rose-500">*</span>
                </label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={actualClosingCash}
                  onChange={(e) => setActualClosingCash(e.target.value)}
                  placeholder="0.00"
                  className="w-full text-lg font-mono font-extrabold border border-gray-300 rounded-xl px-3 py-2.5 text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0D3826]"
                  required
                />
              </div>

              {/* Variance Indicator */}
              <div
                className={`p-3.5 rounded-xl border ${
                  closeVariance === 0
                    ? 'bg-emerald-50 border-emerald-300 text-emerald-900'
                    : closeVariance < 0
                    ? 'bg-rose-50 border-rose-300 text-rose-900'
                    : 'bg-blue-50 border-blue-300 text-blue-900'
                }`}
              >
                <div className="flex items-center justify-between text-xs font-bold">
                  <span>Reconciliation Status:</span>
                  <span className="font-mono text-sm">
                    {closeVariance === 0 ? (
                      '✅ Exact Match (Rs. 0.00)'
                    ) : closeVariance < 0 ? (
                      `⚠️ Shortage: Rs. ${Math.abs(closeVariance).toLocaleString('en-LK', { minimumFractionDigits: 2 })}`
                    ) : (
                      `💡 Surplus: +Rs. ${closeVariance.toLocaleString('en-LK', { minimumFractionDigits: 2 })}`
                    )}
                  </span>
                </div>

                {closeVariance !== 0 && (
                  <div className="mt-3 pt-2 border-t border-current/20">
                    <label className="block text-[11px] font-bold uppercase tracking-wider mb-1">
                      Mandatory Reason for Variance <span className="text-rose-600">*</span>
                    </label>
                    <textarea
                      rows={2}
                      value={closingVarianceReason}
                      onChange={(e) => setClosingVarianceReason(e.target.value)}
                      placeholder="State explanation for variance..."
                      className="w-full text-xs border rounded-lg p-2 bg-white text-gray-900 focus:outline-none"
                      required
                    />
                  </div>
                )}
              </div>

              {/* Closing Notes */}
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Closing Notes (Optional)</label>
                <textarea
                  rows={2}
                  value={closingNotes}
                  onChange={(e) => setClosingNotes(e.target.value)}
                  placeholder="Add optional handover or shift notes..."
                  className="w-full text-xs border border-gray-300 rounded-xl p-2.5 text-gray-800 focus:outline-none focus:ring-1 focus:ring-[#0D3826]"
                />
              </div>

              {/* Confirmation Checkbox */}
              <div className="flex items-start space-x-2 pt-1">
                <input
                  type="checkbox"
                  id="closeConfirm"
                  checked={closeConfirm}
                  onChange={(e) => setCloseConfirm(e.target.checked)}
                  className="mt-0.5 rounded border-gray-300 text-[#0D3826] focus:ring-[#0D3826]"
                  required
                />
                <label htmlFor="closeConfirm" className="text-xs text-gray-700 font-medium">
                  I confirm that physical cash was counted and verified. Closed shifts are immutable historical audit records.
                </label>
              </div>

              {/* Action Buttons */}
              <div className="pt-2 flex items-center justify-end space-x-3">
                <button
                  type="button"
                  onClick={() => {
                    setShowCloseModal(false);
                    resetCloseForm();
                  }}
                  className="px-4 py-2 text-xs font-semibold text-gray-600 hover:text-gray-800"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={closeSubmitting || !closeConfirm}
                  className="px-5 py-2.5 text-xs font-bold text-white bg-rose-700 hover:bg-rose-800 rounded-xl shadow disabled:opacity-50 transition"
                >
                  {closeSubmitting ? 'Closing & Reconciling...' : 'Confirm Shift Close'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 3: ADD CASH DRAWER                                                 */}
      {/* ========================================================================= */}
      {showAddDrawerModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4 overflow-y-auto animate-fade-in">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-gray-100 relative">
            <button
              onClick={() => {
                setShowAddDrawerModal(false);
                setDrawerError(null);
              }}
              className="absolute top-4 right-4 text-gray-400 hover:text-gray-600 text-lg font-bold"
            >
              ✕
            </button>

            <div className="flex items-center space-x-3 mb-4">
              <div className="w-10 h-10 rounded-xl bg-[#0D3826]/10 text-[#0D3826] flex items-center justify-center text-lg font-bold">
                🗄️
              </div>
              <div>
                <h3 className="text-lg font-bold text-gray-900">Register Cash Drawer</h3>
                <p className="text-xs text-gray-500">Add physical cash drawer to the active branch.</p>
              </div>
            </div>

            {drawerError && (
              <div className="mb-4 p-3 bg-rose-50 border border-rose-200 rounded-xl text-xs text-rose-800 flex items-start space-x-2">
                <span>⚠️</span>
                <span>{drawerError}</span>
              </div>
            )}

            <form onSubmit={handleAddDrawer} className="space-y-4">
              <div>
                <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-1">
                  Drawer Code <span className="text-rose-500">*</span>
                </label>
                <input
                  type="text"
                  placeholder="e.g. DRW-01, TILL-1"
                  value={newDrawerCode}
                  onChange={(e) => setNewDrawerCode(e.target.value)}
                  className="w-full text-sm font-mono border border-gray-300 rounded-xl px-3 py-2 text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0D3826]"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-1">
                  Drawer Name <span className="text-rose-500">*</span>
                </label>
                <input
                  type="text"
                  placeholder="e.g. Counter 1 Main Cash Drawer"
                  value={newDrawerName}
                  onChange={(e) => setNewDrawerName(e.target.value)}
                  className="w-full text-sm border border-gray-300 rounded-xl px-3 py-2 text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#0D3826]"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Description (Optional)</label>
                <textarea
                  rows={2}
                  placeholder="Physical location or hardware details..."
                  value={newDrawerDesc}
                  onChange={(e) => setNewDrawerDesc(e.target.value)}
                  className="w-full text-xs border border-gray-300 rounded-xl p-2.5 text-gray-800 focus:outline-none focus:ring-1 focus:ring-[#0D3826]"
                />
              </div>

              <div className="pt-2 flex items-center justify-end space-x-3">
                <button
                  type="button"
                  onClick={() => setShowAddDrawerModal(false)}
                  className="px-4 py-2 text-xs font-semibold text-gray-600 hover:text-gray-800"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={drawerSubmitting}
                  className="px-5 py-2.5 text-xs font-bold text-white bg-[#0D3826] hover:bg-[#1B6543] rounded-xl shadow transition"
                >
                  {drawerSubmitting ? 'Registering...' : 'Register Drawer'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 4: SHIFT AUDIT DETAILS                                              */}
      {/* ========================================================================= */}
      {selectedShiftForDetail && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4 overflow-y-auto animate-fade-in">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-gray-100 relative">
            <button
              onClick={() => setSelectedShiftForDetail(null)}
              className="absolute top-4 right-4 text-gray-400 hover:text-gray-600 text-lg font-bold"
            >
              ✕
            </button>

            <div className="flex items-center space-x-3 mb-4">
              <div className="w-10 h-10 rounded-xl bg-gray-100 text-gray-800 flex items-center justify-center text-lg font-bold">
                📜
              </div>
              <div>
                <h3 className="text-lg font-bold text-gray-900">Shift Reconciliation Audit</h3>
                <p className="text-xs font-mono text-gray-500">{selectedShiftForDetail.shiftNumber}</p>
              </div>
            </div>

            <div className="space-y-3 text-xs">
              <div className="grid grid-cols-2 gap-3 p-3 bg-gray-50 rounded-xl">
                <div>
                  <span className="text-gray-500">Branch:</span>
                  <p className="font-semibold text-gray-800">{selectedShiftForDetail.branchName || '—'}</p>
                </div>
                <div>
                  <span className="text-gray-500">Cash Drawer:</span>
                  <p className="font-semibold text-gray-800">{selectedShiftForDetail.cashDrawerName} ({selectedShiftForDetail.cashDrawerCode})</p>
                </div>
                <div>
                  <span className="text-gray-500">Cashier:</span>
                  <p className="font-semibold text-gray-800">{selectedShiftForDetail.cashierName || 'Cashier'}</p>
                </div>
                <div>
                  <span className="text-gray-500">Shift Status:</span>
                  <p className="font-bold text-gray-800">
                    {selectedShiftForDetail.status === 0 ? '🟢 Open' : '🔒 Closed & Reconciled'}
                  </p>
                </div>
              </div>

              <div className="p-3 bg-gray-50 rounded-xl space-y-1.5 font-mono">
                <div className="flex justify-between">
                  <span className="text-gray-600">Opening Balance:</span>
                  <span className="font-bold text-gray-900">
                    Rs. {selectedShiftForDetail.openingBalance.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                  </span>
                </div>
                <div className="flex justify-between text-gray-500">
                  <span>System Expected Float:</span>
                  <span>Rs. {selectedShiftForDetail.systemExpectedOpeningBalance.toLocaleString('en-LK', { minimumFractionDigits: 2 })}</span>
                </div>
                {selectedShiftForDetail.openingBalanceDifference !== 0 && (
                  <div className="flex justify-between text-amber-700">
                    <span>Opening Discrepancy:</span>
                    <span>Rs. {selectedShiftForDetail.openingBalanceDifference.toLocaleString('en-LK', { minimumFractionDigits: 2 })}</span>
                  </div>
                )}
                {selectedShiftForDetail.openingDifferenceReason && (
                  <p className="text-[11px] text-gray-600 font-sans italic bg-white p-2 rounded border border-gray-200 mt-1">
                    Opening Note: {selectedShiftForDetail.openingDifferenceReason}
                  </p>
                )}
              </div>

              {selectedShiftForDetail.status === 1 && (
                <div className="p-3 bg-gray-50 rounded-xl space-y-1.5 font-mono">
                  <div className="flex justify-between">
                    <span className="text-gray-600">Expected Closing Cash:</span>
                    <span className="font-bold text-gray-900">
                      Rs. {selectedShiftForDetail.expectedClosingBalance?.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-600">Actual Closing Cash:</span>
                    <span className="font-bold text-gray-900">
                      Rs. {selectedShiftForDetail.actualClosingBalance?.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                  <div className="flex justify-between border-t border-gray-200 pt-1.5 font-bold">
                    <span>Reconciliation Variance:</span>
                    <span className={selectedShiftForDetail.closingVariance === 0 ? 'text-emerald-700' : selectedShiftForDetail.closingVariance! < 0 ? 'text-rose-700' : 'text-blue-700'}>
                      Rs. {selectedShiftForDetail.closingVariance?.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                  {selectedShiftForDetail.closingVarianceReason && (
                    <p className="text-[11px] text-gray-700 font-sans italic bg-white p-2 rounded border border-gray-200 mt-1">
                      Variance Explanation: {selectedShiftForDetail.closingVarianceReason}
                    </p>
                  )}
                  {selectedShiftForDetail.closedByName && (
                    <div className="text-[11px] font-sans text-gray-500 pt-1">
                      Closed by {selectedShiftForDetail.closedByName} at {selectedShiftForDetail.closedAt ? new Date(selectedShiftForDetail.closedAt).toLocaleString() : ''}
                    </div>
                  )}
                </div>
              )}
            </div>

            <div className="pt-4 flex justify-end">
              <button
                onClick={() => setSelectedShiftForDetail(null)}
                className="px-4 py-2 text-xs font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-xl transition"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
