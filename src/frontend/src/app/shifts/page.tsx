'use client';

import { useState, useEffect, useCallback } from 'react';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  shiftApi,
  cashLedgerApi,
  Shift,
  CashDrawer,
  ShiftOpeningDefaults,
  ShiftCashLedgerSummary,
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
  const [closingShiftTarget, setClosingShiftTarget] = useState<Shift | null>(null);
  const [closingShiftSummary, setClosingShiftSummary] = useState<ShiftCashLedgerSummary | null>(null);
  const [showAddDrawerModal, setShowAddDrawerModal] = useState<boolean>(false);
  const [editingDrawer, setEditingDrawer] = useState<CashDrawer | null>(null);
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
  const [overrideReason, setOverrideReason] = useState<string>('');
  const [closeConfirm, setCloseConfirm] = useState<boolean>(false);
  const [closeSubmitting, setCloseSubmitting] = useState<boolean>(false);
  const [closeError, setCloseError] = useState<string | null>(null);

  // Add / Edit Drawer Form State
  const [drawerCodeInput, setDrawerCodeInput] = useState<string>('');
  const [drawerNameInput, setDrawerNameInput] = useState<string>('');
  const [drawerDescInput, setDrawerDescInput] = useState<string>('');
  const [drawerSubmitting, setDrawerSubmitting] = useState<boolean>(false);
  const [drawerError, setDrawerError] = useState<string | null>(null);

  // Notification / Alert
  const [alertMessage, setAlertMessage] = useState<{ type: 'success' | 'error'; message: string } | null>(null);

  const canStartShift = user?.permissions.includes('SHIFT_START') || user?.isGlobalAdmin;
  const canEndShift = user?.permissions.includes('SHIFT_END') || user?.isGlobalAdmin;
  const canOverrideClose = user?.permissions.includes('SHIFT_OVERRIDE_CLOSE') || user?.isGlobalAdmin;
  const canManageDrawers = user?.permissions.includes('CASH_DRAWER_MANAGE') || user?.isGlobalAdmin;

  // Load Active Shift
  const fetchActiveShift = useCallback(async () => {
    try {
      setLoadingActiveShift(true);
      const shift = await shiftApi.getActiveCashierShift();
      setActiveShift(shift && shift.id ? shift : null);
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
      setOpenError('Please enter a valid non-negative opening cash amount.');
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

  // Prepare Close Modal
  const initiateCloseShift = (target: Shift) => {
    setClosingShiftTarget(target);
    setClosingShiftSummary(null);
    cashLedgerApi.getShiftSummary(target.id).then(setClosingShiftSummary).catch(() => {});
    const exp = target.expectedClosingBalance ?? target.openingBalance;
    setActualClosingCash(exp.toFixed(2));
    setClosingVarianceReason('');
    setClosingNotes('');
    setOverrideReason('');
    setCloseConfirm(false);
    setCloseError(null);
    setShowCloseModal(true);
  };

  // Submit Close Shift
  const handleCloseShift = async (e: React.FormEvent) => {
    e.preventDefault();
    const target = closingShiftTarget || activeShift;
    if (!target) return;

    const actualCashNum = parseFloat(actualClosingCash);
    if (isNaN(actualCashNum) || actualCashNum < 0) {
      setCloseError('Please enter a valid non-negative physical cash amount.');
      return;
    }

    const isOverride = target.cashierId !== user?.id;
    if (isOverride && !canOverrideClose) {
      setCloseError('You can only close your own active shift unless authorized with management override permission.');
      return;
    }

    if (isOverride && !overrideReason.trim()) {
      setCloseError('A mandatory management explanation reason is required when closing another cashier\'s shift.');
      return;
    }

    const expectedCash = target.expectedClosingBalance ?? target.openingBalance;
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
      await shiftApi.closeShift(target.id, {
        actualClosingBalance: actualCashNum,
        closingVarianceReason: variance !== 0 ? closingVarianceReason.trim() : undefined,
        closingNotes: closingNotes.trim() || undefined,
        overrideReason: isOverride ? overrideReason.trim() : undefined,
        rowVersion: target.version
      });

      setAlertMessage({
        type: 'success',
        message: `Shift ${target.shiftNumber} closed and reconciled successfully${isOverride ? ' (Management Override)' : ''}.`
      });
      setShowCloseModal(false);
      setClosingShiftTarget(null);
      await Promise.all([fetchActiveShift(), fetchDrawers(), fetchHistory()]);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to close shift.';
      setCloseError(msg);
    } finally {
      setCloseSubmitting(false);
    }
  };

  // Submit Create or Edit Cash Drawer
  const handleSaveDrawer = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeBranchId) return;

    if (!drawerCodeInput.trim() || !drawerNameInput.trim()) {
      setDrawerError('Drawer code and drawer name are required.');
      return;
    }

    try {
      setDrawerSubmitting(true);
      setDrawerError(null);

      if (editingDrawer) {
        await shiftApi.updateCashDrawer(editingDrawer.id, {
          drawerCode: drawerCodeInput.trim().toUpperCase(),
          name: drawerNameInput.trim(),
          description: drawerDescInput.trim() || undefined,
          branchId: activeBranchId
        });
        setAlertMessage({ type: 'success', message: `Cash drawer ${drawerCodeInput} updated successfully.` });
      } else {
        await shiftApi.createCashDrawer({
          branchId: activeBranchId,
          drawerCode: drawerCodeInput.trim().toUpperCase(),
          name: drawerNameInput.trim(),
          description: drawerDescInput.trim() || undefined
        });
        setAlertMessage({ type: 'success', message: 'New cash drawer registered successfully.' });
      }

      setShowAddDrawerModal(false);
      setEditingDrawer(null);
      setDrawerCodeInput('');
      setDrawerNameInput('');
      setDrawerDescInput('');
      await fetchDrawers();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to save cash drawer.';
      setDrawerError(msg);
    } finally {
      setDrawerSubmitting(false);
    }
  };

  // Toggle Drawer Active Status
  const handleToggleDrawerStatus = async (drawer: CashDrawer) => {
    if (!canManageDrawers) return;
    try {
      await shiftApi.updateCashDrawerStatus(drawer.id, {
        isActive: !drawer.isActive,
        reason: drawer.isActive ? 'Deactivated by manager' : 'Reactivated by manager'
      });
      setAlertMessage({
        type: 'success',
        message: `Cash drawer ${drawer.drawerCode} ${drawer.isActive ? 'deactivated' : 'activated'} successfully.`
      });
      await fetchDrawers();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to update drawer status.';
      setAlertMessage({ type: 'error', message: msg });
    }
  };

  // Delete Cash Drawer
  const handleDeleteDrawer = async (drawer: CashDrawer) => {
    if (!canManageDrawers) return;
    if (!confirm(`Are you sure you want to delete cash drawer '${drawer.name}' (${drawer.drawerCode})? Drawers with past shift history cannot be deleted.`)) {
      return;
    }
    try {
      await shiftApi.deleteCashDrawer(drawer.id);
      setAlertMessage({ type: 'success', message: `Cash drawer ${drawer.drawerCode} deleted successfully.` });
      await fetchDrawers();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to delete drawer.';
      setAlertMessage({ type: 'error', message: msg });
    }
  };

  // Calculations for open modal
  const openFloatNum = parseFloat(openingFloat) || 0;
  const sysExpectedFloat = openingDefaults?.systemExpectedOpeningBalance ?? 0;
  const openDiscrepancy = openFloatNum - sysExpectedFloat;

  // Calculations for close modal
  const targetShift = closingShiftTarget || activeShift;
  const closeActualNum = parseFloat(actualClosingCash) || 0;
  const closeExpectedNum = targetShift ? (targetShift.expectedClosingBalance ?? targetShift.openingBalance) : 0;
  const closeVariance = closeActualNum - closeExpectedNum;
  const isTargetOverride = targetShift ? targetShift.cashierId !== user?.id : false;

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
              <span className="text-xl">{alertMessage.type === 'success' ? '' : ''}</span>
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
              <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-[#123B32] to-[#1B6543] flex items-center justify-center text-white text-xl shadow">
                
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
            {canManageDrawers && (
              <button
                onClick={() => {
                  setEditingDrawer(null);
                  setDrawerCodeInput('');
                  setDrawerNameInput('');
                  setDrawerDescInput('');
                  setShowAddDrawerModal(true);
                }}
                className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-xl hover:bg-gray-50 shadow-sm transition-colors flex items-center space-x-1.5"
              >
                <span></span>
                <span>New Drawer</span>
              </button>
            )}
            {(!activeShift || !activeShift.id) && canStartShift && (
              <button
                onClick={() => {
                  setShowOpenModal(true);
                  if (drawers.length > 0 && !selectedDrawerId) {
                    const firstAvail = drawers.find((d) => !d.hasActiveShift && d.isActive);
                    if (firstAvail) handleDrawerSelect(firstAvail.id);
                  }
                }}
                className="px-5 py-2.5 text-sm font-semibold text-white bg-gradient-to-r from-[#123B32] to-[#1B6543] hover:from-[#155438] hover:to-[#238055] rounded-xl shadow-md transition-all flex items-center space-x-2 border border-[#B89A60]/40"
              >
                <span></span>
                <span>Open Shift</span>
              </button>
            )}
            {activeShift && activeShift.id && canEndShift && (
              <button
                onClick={() => initiateCloseShift(activeShift)}
                className="px-5 py-2.5 text-sm font-semibold text-white bg-gradient-to-r from-[#991b1b] to-[#b91c1c] hover:from-[#7f1d1d] hover:to-[#991b1b] rounded-xl shadow-md transition-all flex items-center space-x-2"
              >
                <span></span>
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
                <span></span>
                <span>Your Active Shift Status</span>
              </h2>
              {loadingActiveShift ? (
                <span className="text-xs text-gray-400 animate-pulse">Checking shift status...</span>
              ) : activeShift && activeShift.id ? (
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
            ) : activeShift && activeShift.id ? (
              <div className="grid grid-cols-1 md:grid-cols-4 gap-4 p-4 rounded-xl bg-gradient-to-r from-[#123B32]/5 to-[#B89A60]/10 border border-[#B89A60]/30">
                <div>
                  <span className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Cash Drawer</span>
                  <p className="text-base font-bold text-gray-900 mt-0.5">
                    {activeShift.cashDrawerName} <span className="text-xs font-normal text-gray-500">({activeShift.cashDrawerCode})</span>
                  </p>
                </div>
                <div>
                  <span className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Opened At</span>
                  <p className="text-sm font-medium text-gray-900 mt-0.5">
                    {activeShift.openedAt
                      ? new Date(activeShift.openedAt).toLocaleString('en-GB', {
                          day: '2-digit',
                          month: 'short',
                          year: 'numeric',
                          hour: '2-digit',
                          minute: '2-digit',
                        })
                      : '—'}
                  </p>
                </div>
                <div>
                  <span className="text-xs text-gray-500 uppercase tracking-wider font-semibold">Opening Float ({activeShift.currencyCode || 'LKR'})</span>
                  <p className="text-lg font-extrabold text-[#123B32] mt-0.5">
                    Rs. {(activeShift.openingBalance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                  </p>
                </div>
                <div className="flex items-center justify-end">
                  {canEndShift && (
                    <button
                      onClick={() => initiateCloseShift(activeShift)}
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
                  <span className="text-2xl mt-0.5"></span>
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
                        const firstAvail = drawers.find((d) => !d.hasActiveShift && d.isActive);
                        if (firstAvail) handleDrawerSelect(firstAvail.id);
                      }
                    }}
                    className="whitespace-nowrap px-4 py-2 text-xs font-bold text-white bg-[#123B32] hover:bg-[#1B6543] rounded-lg shadow transition"
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
                <span></span>
                <span>Branch Cash Drawers</span>
              </h2>
              <p className="text-xs text-gray-500 mt-0.5">
                Physical drawer hardware registry. One active shift per drawer is strictly enforced by PostgreSQL uniqueness.
              </p>
            </div>
            <span className="text-xs font-semibold text-gray-500 bg-gray-100 px-2.5 py-1 rounded-lg">
              {drawers.length} {drawers.length === 1 ? 'Drawer' : 'Drawers'} Registered
            </span>
          </div>

          {loadingDrawers ? (
            <div className="py-6 text-center text-gray-400 text-sm">Loading cash drawers...</div>
          ) : drawers.length === 0 ? (
            <div className="py-8 text-center bg-gray-50 rounded-xl border border-dashed border-gray-200">
              <span className="text-3xl"></span>
              <p className="text-sm font-semibold text-gray-700 mt-2">No cash drawers configured for this branch</p>
              <p className="text-xs text-gray-500 mt-1">Register your first physical cash drawer to enable shift management.</p>
              {canManageDrawers && (
                <button
                  onClick={() => {
                    setEditingDrawer(null);
                    setDrawerCodeInput('');
                    setDrawerNameInput('');
                    setDrawerDescInput('');
                    setShowAddDrawerModal(true);
                  }}
                  className="mt-3 px-3.5 py-1.5 text-xs font-semibold text-white bg-[#123B32] rounded-lg hover:bg-[#1B6543] transition"
                >
                  Register Drawer
                </button>
              )}
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              {drawers.map((d) => (
                <div
                  key={d.id}
                  className={`p-4 rounded-xl border transition-all ${
                    !d.isActive
                      ? 'bg-gray-100/70 border-gray-200 opacity-70'
                      : d.hasActiveShift
                      ? 'bg-emerald-50/40 border-emerald-200 shadow-sm'
                      : 'bg-white border-gray-200 hover:border-gray-300'
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-mono font-bold text-gray-700 bg-gray-100 px-2 py-0.5 rounded">
                      {d.drawerCode}
                    </span>
                    <div className="flex items-center space-x-1.5">
                      {!d.isActive ? (
                        <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-gray-200 text-gray-600">
                          Inactive
                        </span>
                      ) : d.hasActiveShift ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-200">
                          In Use
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-semibold bg-gray-100 text-gray-600">
                          Available
                        </span>
                      )}
                    </div>
                  </div>
                  <h3 className="text-sm font-bold text-gray-900 mt-2">{d.name}</h3>
                  {d.description && <p className="text-xs text-gray-500 mt-0.5 line-clamp-1">{d.description}</p>}

                  <div className="mt-4 pt-3 border-t border-gray-100 flex items-center justify-between text-xs">
                    <span className="text-gray-500">
                      {d.hasActiveShift ? (
                        <span className="text-emerald-800 font-medium"> {d.activeCashierName || 'Cashier in session'}</span>
                      ) : d.isActive ? (
                        <span>Ready for shift</span>
                      ) : (
                        <span className="text-gray-400">Drawer disabled</span>
                      )}
                    </span>
                    <div className="flex items-center space-x-2">
                      {d.isActive && !d.hasActiveShift && (!activeShift || !activeShift.id) && canStartShift && (
                        <button
                          onClick={() => {
                            handleDrawerSelect(d.id);
                            setShowOpenModal(true);
                          }}
                          className="text-xs font-bold text-[#123B32] hover:underline"
                        >
                          Open Here →
                        </button>
                      )}
                      {canManageDrawers && (
                        <div className="flex items-center space-x-1 ml-1 text-gray-400">
                          <button
                            onClick={() => {
                              setEditingDrawer(d);
                              setDrawerCodeInput(d.drawerCode);
                              setDrawerNameInput(d.name);
                              setDrawerDescInput(d.description || '');
                              setShowAddDrawerModal(true);
                            }}
                            className="p-1 hover:text-gray-700 hover:bg-gray-100 rounded text-xs"
                            title="Edit Drawer"
                          >
                            
                          </button>
                          <button
                            onClick={() => handleToggleDrawerStatus(d)}
                            className="p-1 hover:text-gray-700 hover:bg-gray-100 rounded text-xs"
                            title={d.isActive ? 'Deactivate Drawer' : 'Activate Drawer'}
                          >
                            {d.isActive ? '⏸️' : '▶️'}
                          </button>
                          <button
                            onClick={() => handleDeleteDrawer(d)}
                            className="p-1 hover:text-rose-600 hover:bg-rose-50 rounded text-xs"
                            title="Delete Drawer"
                          >
                            
                          </button>
                        </div>
                      )}
                    </div>
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
                <span></span>
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
                className="text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-700 focus:outline-none focus:ring-1 focus:ring-[#123B32]"
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
                          {s.isOverrideClose && (
                            <span className="ml-1.5 text-[10px] font-sans px-1.5 py-0.5 rounded bg-purple-100 text-purple-800 font-semibold" title={s.overrideReason || 'Override Close'}>
                              Override
                            </span>
                          )}
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
                          {s.openedAt
                            ? new Date(s.openedAt).toLocaleString('en-GB', {
                                day: '2-digit',
                                month: 'short',
                                hour: '2-digit',
                                minute: '2-digit'
                              })
                            : '—'}
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
                          Rs. {(s.openingBalance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                        </td>
                        <td className="px-6 py-4 text-right font-mono text-gray-900 font-medium">
                          {s.actualClosingBalance != null
                            ? `Rs. ${(s.actualClosingBalance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}`
                            : '—'}
                        </td>
                        <td className="px-6 py-4 text-right font-mono font-bold">
                          {s.status === 0 ? (
                            <span className="text-gray-400 font-normal">Pending</span>
                          ) : variance === 0 ? (
                            <span className="text-emerald-700">Rs. 0.00</span>
                          ) : variance < 0 ? (
                            <span className="text-rose-700 font-bold" title={s.closingVarianceReason || 'Shortage'}>
                              Rs. {(variance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })} 
                            </span>
                          ) : (
                            <span className="text-blue-700 font-bold" title={s.closingVarianceReason || 'Surplus'}>
                              +Rs. {(variance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                            </span>
                          )}
                        </td>
                        <td className="px-6 py-4 text-center space-x-2">
                          <button
                            onClick={() => setSelectedShiftForDetail(s)}
                            className="text-xs font-semibold text-[#123B32] hover:text-[#1B6543] underline"
                          >
                            Details
                          </button>
                          {s.status === 0 && canOverrideClose && s.cashierId !== user?.id && (
                            <button
                              onClick={() => initiateCloseShift(s)}
                              className="text-[11px] font-bold text-rose-700 hover:text-rose-900 underline"
                            >
                              Override Close
                            </button>
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
                
              </div>
              <div>
                <h3 className="text-lg font-bold text-gray-900">Open Cashier Shift</h3>
                <p className="text-xs text-gray-500">Assign drawer and verify initial opening physical float cash.</p>
              </div>
            </div>

            {openError && (
              <div className="mb-4 p-3 bg-rose-50 border border-rose-200 rounded-xl text-xs text-rose-800 flex items-start space-x-2">
                <span></span>
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
                  className="w-full text-sm border border-gray-300 rounded-xl px-3 py-2.5 bg-white text-gray-800 focus:outline-none focus:ring-2 focus:ring-[#123B32]"
                  required
                >
                  <option value="">-- Select Cash Drawer --</option>
                  {drawers.map((d) => (
                    <option key={d.id} value={d.id} disabled={d.hasActiveShift || !d.isActive}>
                      {d.drawerCode} - {d.name} {!d.isActive ? '(Disabled)' : d.hasActiveShift ? '(Already In Use)' : '(Available)'}
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
                      Rs. {(openingDefaults.systemExpectedOpeningBalance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
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
                  placeholder="e.g. 25,000.00"
                  className="w-full text-base font-mono font-bold border border-gray-300 rounded-xl px-3 py-2.5 text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#123B32]"
                  required
                />
              </div>

              {/* Opening Difference Alert & Reason */}
              {openDiscrepancy !== 0 && selectedDrawerId && (
                <div className="p-3.5 bg-amber-50 border border-amber-300 rounded-xl space-y-2">
                  <div className="flex items-center justify-between text-xs font-bold text-amber-900">
                    <span>Discrepancy Detected:</span>
                    <span className="font-mono">
                      {openDiscrepancy > 0 ? '+' : ''}Rs. {(openDiscrepancy ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
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
                      placeholder="Explain why the opening cash differs from the previous closing balance"
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
                  placeholder="Optional remarks regarding physical cash float or coins"
                  className="w-full text-xs border border-gray-300 rounded-xl p-2.5 text-gray-800 focus:outline-none focus:ring-1 focus:ring-[#123B32]"
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
                  className="px-5 py-2.5 text-xs font-bold text-white bg-gradient-to-r from-[#123B32] to-[#1B6543] hover:from-[#155438] hover:to-[#238055] rounded-xl shadow disabled:opacity-50 transition"
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
      {showCloseModal && targetShift && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4 overflow-y-auto animate-fade-in">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl border border-gray-100 relative">
            <button
              onClick={() => {
                setShowCloseModal(false);
                setClosingShiftTarget(null);
              }}
              className="absolute top-4 right-4 text-gray-400 hover:text-gray-600 text-lg font-bold"
            >
              ✕
            </button>

            <div className="flex items-center space-x-3 mb-4">
              <div className="w-10 h-10 rounded-xl bg-rose-100 text-rose-800 flex items-center justify-center text-lg font-bold">
                
              </div>
              <div>
                <h3 className="text-lg font-bold text-gray-900">
                  {isTargetOverride ? 'Management Override Close' : 'Close Shift Reconciliation'}
                </h3>
                <p className="text-xs text-gray-500">
                  Shift #{targetShift.shiftNumber} • {targetShift.cashDrawerName}
                  {isTargetOverride && ` (Cashier: ${targetShift.cashierName})`}
                </p>
              </div>
            </div>

            {isTargetOverride && (
              <div className="mb-4 p-3 bg-purple-50 border border-purple-200 rounded-xl text-xs text-purple-900">
                <span className="font-bold">Management Override Notice:</span> You are closing a shift owned by{' '}
                <span className="font-bold">{targetShift.cashierName}</span>. Your user identity will be audited as the closing authority.
              </div>
            )}

            {closeError && (
              <div className="mb-4 p-3 bg-rose-50 border border-rose-200 rounded-xl text-xs text-rose-800 flex items-start space-x-2">
                <span></span>
                <span>{closeError}</span>
              </div>
            )}

            <form onSubmit={handleCloseShift} className="space-y-4">
              {/* Balances Summary Box */}
              <div className="p-4 bg-gray-50 rounded-xl border border-gray-200 space-y-2">
                <div className="flex items-center justify-between text-xs text-gray-600">
                  <span>Opening Float ({targetShift?.currencyCode || 'LKR'}):</span>
                  <span className="font-mono font-semibold text-gray-800">
                    Rs. {(targetShift?.openingBalance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                  </span>
                </div>
                {closingShiftSummary && (
                  <>
                    <div className="flex items-center justify-between text-xs text-emerald-700">
                      <span>Total Posted Cash In (+):</span>
                      <span className="font-mono font-semibold">
                        + Rs. {(closingShiftSummary.totalCashIn ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                      </span>
                    </div>
                    <div className="flex items-center justify-between text-xs text-rose-700">
                      <span>Total Posted Cash Out (-):</span>
                      <span className="font-mono font-semibold">
                        - Rs. {(closingShiftSummary.totalCashOut ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                      </span>
                    </div>
                  </>
                )}
                <div className="flex items-center justify-between text-xs text-gray-600 border-t border-gray-200 pt-2">
                  <span className="font-bold text-gray-900">Backend Expected Closing Cash:</span>
                  <span className="font-mono font-extrabold text-[#123B32] text-sm">
                    Rs. {(closeExpectedNum ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                  </span>
                </div>
                <p className="text-[10px] text-gray-400">
                  *Authoritative backend calculation (Opening + Total In − Total Out).
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
                  placeholder="e.g. 25,000.00"
                  className="w-full text-lg font-mono font-extrabold border border-gray-300 rounded-xl px-3 py-2.5 text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#123B32]"
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
                      'Balanced (0.00)'
                    ) : closeVariance < 0 ? (
                      ` Shortage: Rs. ${Math.abs(closeVariance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}`
                    ) : (
                      ` Surplus: +Rs. ${(closeVariance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}`
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
                      placeholder="Explain the reason for the cash shortage or surplus"
                      className="w-full text-xs border rounded-lg p-2 bg-white text-gray-900 focus:outline-none"
                      required
                    />
                  </div>
                )}
              </div>

              {/* Override Reason (Mandatory when closing another cashier's shift) */}
              {isTargetOverride && (
                <div>
                  <label className="block text-xs font-bold text-purple-900 uppercase tracking-wider mb-1">
                    Mandatory Management Override Reason <span className="text-rose-600">*</span>
                  </label>
                  <textarea
                    rows={2}
                    value={overrideReason}
                    onChange={(e) => setOverrideReason(e.target.value)}
                    placeholder="Explain why management is closing this shift on behalf of the cashier..."
                    className="w-full text-xs border border-purple-300 rounded-xl p-2.5 text-gray-900 focus:outline-none focus:ring-1 focus:ring-purple-500"
                    required
                  />
                </div>
              )}

              {/* Closing Notes */}
              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Closing Notes (Optional)</label>
                <textarea
                  rows={2}
                  value={closingNotes}
                  onChange={(e) => setClosingNotes(e.target.value)}
                  placeholder="Optional remarks regarding handover or physical envelope seal"
                  className="w-full text-xs border border-gray-300 rounded-xl p-2.5 text-gray-800 focus:outline-none focus:ring-1 focus:ring-[#123B32]"
                />
              </div>

              {/* Confirmation Checkbox */}
              <div className="flex items-start space-x-2 pt-1">
                <input
                  type="checkbox"
                  id="closeConfirm"
                  checked={closeConfirm}
                  onChange={(e) => setCloseConfirm(e.target.checked)}
                  className="mt-0.5 rounded border-gray-300 text-[#123B32] focus:ring-[#123B32]"
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
                    setClosingShiftTarget(null);
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
      {/* MODAL 3: ADD / EDIT CASH DRAWER                                           */}
      {/* ========================================================================= */}
      {showAddDrawerModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4 overflow-y-auto animate-fade-in">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-gray-100 relative">
            <button
              onClick={() => {
                setShowAddDrawerModal(false);
                setEditingDrawer(null);
                setDrawerError(null);
              }}
              className="absolute top-4 right-4 text-gray-400 hover:text-gray-600 text-lg font-bold"
            >
              ✕
            </button>

            <div className="flex items-center space-x-3 mb-4">
              <div className="w-10 h-10 rounded-xl bg-[#123B32]/10 text-[#123B32] flex items-center justify-center text-lg font-bold">
                
              </div>
              <div>
                <h3 className="text-lg font-bold text-gray-900">
                  {editingDrawer ? 'Edit Cash Drawer' : 'Register Cash Drawer'}
                </h3>
                <p className="text-xs text-gray-500">
                  {editingDrawer ? `Update details for ${editingDrawer.drawerCode}` : 'Add physical cash drawer to the active branch.'}
                </p>
              </div>
            </div>

            {drawerError && (
              <div className="mb-4 p-3 bg-rose-50 border border-rose-200 rounded-xl text-xs text-rose-800 flex items-start space-x-2">
                <span></span>
                <span>{drawerError}</span>
              </div>
            )}

            <form onSubmit={handleSaveDrawer} className="space-y-4">
              <div>
                <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-1">
                  Drawer Code <span className="text-rose-500">*</span>
                </label>
                <input
                  type="text"
                  placeholder="e.g. TILL-01"
                  value={drawerCodeInput}
                  onChange={(e) => setDrawerCodeInput(e.target.value)}
                  className="w-full text-sm font-mono border border-gray-300 rounded-xl px-3 py-2 text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#123B32]"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-bold text-gray-700 uppercase tracking-wider mb-1">
                  Drawer Name <span className="text-rose-500">*</span>
                </label>
                <input
                  type="text"
                  placeholder="e.g. Main Sales Counter"
                  value={drawerNameInput}
                  onChange={(e) => setDrawerNameInput(e.target.value)}
                  className="w-full text-sm border border-gray-300 rounded-xl px-3 py-2 text-gray-900 focus:outline-none focus:ring-2 focus:ring-[#123B32]"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-600 mb-1">Description (Optional)</label>
                <textarea
                  rows={2}
                  placeholder="Describe the drawer's physical location or purpose"
                  value={drawerDescInput}
                  onChange={(e) => setDrawerDescInput(e.target.value)}
                  className="w-full text-xs border border-gray-300 rounded-xl p-2.5 text-gray-800 focus:outline-none focus:ring-1 focus:ring-[#123B32]"
                />
              </div>

              <div className="pt-2 flex items-center justify-end space-x-3">
                <button
                  type="button"
                  onClick={() => {
                    setShowAddDrawerModal(false);
                    setEditingDrawer(null);
                  }}
                  className="px-4 py-2 text-xs font-semibold text-gray-600 hover:text-gray-800"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={drawerSubmitting}
                  className="px-5 py-2.5 text-xs font-bold text-white bg-[#123B32] hover:bg-[#1B6543] rounded-xl shadow transition"
                >
                  {drawerSubmitting ? 'Saving...' : editingDrawer ? 'Update Drawer' : 'Register Drawer'}
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
                  <span className="text-gray-500">Currency / Status:</span>
                  <p className="font-bold text-gray-800">
                    {selectedShiftForDetail.currencyCode || 'LKR'} • {selectedShiftForDetail.status === 0 ? ' Open' : ' Closed'}
                  </p>
                </div>
              </div>

              {selectedShiftForDetail.isOverrideClose && (
                <div className="p-3 bg-purple-50 rounded-xl border border-purple-200">
                  <span className="font-bold text-purple-900"> Management Override Close:</span>
                  <p className="text-purple-800 mt-0.5">{selectedShiftForDetail.overrideReason || 'No reason specified'}</p>
                  <p className="text-[10px] text-purple-600 mt-1">Closed by: {selectedShiftForDetail.closedByName || 'Manager'}</p>
                </div>
              )}

              <div className="p-3 bg-gray-50 rounded-xl space-y-1.5 font-mono">
                <div className="flex justify-between">
                  <span className="text-gray-600">Opening Balance:</span>
                  <span className="font-bold text-gray-900">
                    Rs. {(selectedShiftForDetail.openingBalance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                  </span>
                </div>
                <div className="flex justify-between text-gray-500">
                  <span>System Expected Float:</span>
                  <span>Rs. {(selectedShiftForDetail.systemExpectedOpeningBalance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}</span>
                </div>
                {selectedShiftForDetail.openingBalanceDifference !== 0 && (
                  <div className="flex justify-between text-amber-700">
                    <span>Opening Discrepancy:</span>
                    <span>Rs. {(selectedShiftForDetail.openingBalanceDifference ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}</span>
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
                      Rs. {(selectedShiftForDetail.expectedClosingBalance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-600">Actual Closing Cash:</span>
                    <span className="font-bold text-gray-900">
                      Rs. {(selectedShiftForDetail.actualClosingBalance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                  <div className="flex justify-between border-t border-gray-200 pt-1.5 font-bold">
                    <span>Reconciliation Variance:</span>
                    <span className={selectedShiftForDetail.closingVariance === 0 ? 'text-emerald-700' : selectedShiftForDetail.closingVariance! < 0 ? 'text-rose-700' : 'text-blue-700'}>
                      Rs. {(selectedShiftForDetail.closingVariance ?? 0).toLocaleString('en-LK', { minimumFractionDigits: 2 })}
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
