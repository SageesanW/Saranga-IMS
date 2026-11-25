'use client';

import React, { useState, useEffect, useCallback, useId } from 'react';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  cashLedgerApi,
  shiftApi,
  CashLedgerTransaction,
  CashLedgerEntry,
  ShiftCashLedgerSummary,
  CashMovementCategory,
  Shift
} from '@/lib/api';
import {
  ArrowDownLeft,
  ArrowUpRight,
  ArrowLeftRight,
  RotateCcw,
  PlusCircle,
  Tag,
  Search,
  Filter,
  CheckCircle2,
  AlertTriangle,
  X,
  Eye,
  Calendar,
  Lock,
  Layers,
  FileText
} from 'lucide-react';

export default function CashLedgerPage() {
  const { user } = useAuth();
  const { activeBranchId, branches } = useBranch();

  // State
  const [activeShift, setActiveShift] = useState<Shift | null>(null);
  const [shiftSummary, setShiftSummary] = useState<ShiftCashLedgerSummary | null>(null);
  const [branchOpenShifts, setBranchOpenShifts] = useState<Shift[]>([]);
  const [transactions, setTransactions] = useState<CashLedgerTransaction[]>([]);
  const [categories, setCategories] = useState<CashMovementCategory[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const pageSize = 20;
  const [isLoading, setIsLoading] = useState(true);
  const [isTxLoading, setIsTxLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Filters
  const [filterType, setFilterType] = useState<string>('');
  const [filterStatus, setFilterStatus] = useState<string>('');
  const [filterCategory, setFilterCategory] = useState<string>('');
  const [filterDrawer, setFilterDrawer] = useState<string>('');
  const [filterFromDate, setFilterFromDate] = useState<string>('');
  const [filterToDate, setFilterToDate] = useState<string>('');

  // Modals
  const [showCashInModal, setShowCashInModal] = useState(false);
  const [showCashOutModal, setShowCashOutModal] = useState(false);
  const [showTransferModal, setShowTransferModal] = useState(false);
  const [showReverseModal, setShowReverseModal] = useState(false);
  const [showDetailsModal, setShowDetailsModal] = useState(false);
  const [showCategoryModal, setShowCategoryModal] = useState(false);

  // Active items for modals
  const [selectedTx, setSelectedTx] = useState<CashLedgerTransaction | null>(null);

  // Form states
  const [inAmount, setInAmount] = useState<string>('');
  const [inCategory, setInCategory] = useState<string>('');
  const [inDescription, setInDescription] = useState<string>('');
  const [inReason, setInReason] = useState<string>('');
  const [inReference, setInReference] = useState<string>('');

  const [outAmount, setOutAmount] = useState<string>('');
  const [outCategory, setOutCategory] = useState<string>('');
  const [outDescription, setOutDescription] = useState<string>('');
  const [outReason, setOutReason] = useState<string>('');
  const [outReference, setOutReference] = useState<string>('');

  const [transferDestShiftId, setTransferDestShiftId] = useState<string>('');
  const [transferAmount, setTransferAmount] = useState<string>('');
  const [transferReason, setTransferReason] = useState<string>('');
  const [transferReference, setTransferReference] = useState<string>('');

  const [reversalReason, setReversalReason] = useState<string>('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Category management form
  const [editingCategory, setEditingCategory] = useState<CashMovementCategory | null>(null);
  const [catName, setCatName] = useState('');
  const [catCode, setCatCode] = useState('');
  const [catDirection, setCatDirection] = useState('3'); // 1: In, 2: Out, 3: Both
  const [catDescription, setCatDescription] = useState('');

  // Permissions
  const canCashIn = user?.isGlobalAdmin || user?.permissions?.includes('CASH_LEDGER_CASH_IN');
  const canCashOut = user?.isGlobalAdmin || user?.permissions?.includes('CASH_LEDGER_CASH_OUT');
  const canTransfer = user?.isGlobalAdmin || user?.permissions?.includes('CASH_LEDGER_TRANSFER');
  const canReverse = user?.isGlobalAdmin || user?.permissions?.includes('CASH_LEDGER_REVERSE');
  const canManageCategories = user?.isGlobalAdmin || user?.permissions?.includes('CASH_LEDGER_CATEGORY_MANAGE');

  const generateIdempotencyKey = () => `CL-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;

  // Load shift context
  const loadShiftContext = useCallback(async () => {
    try {
      const active = await shiftApi.getActiveCashierShift();
      setActiveShift(active);

      if (active) {
        const summary = await cashLedgerApi.getShiftSummary(active.id);
        setShiftSummary(summary);
      } else {
        setShiftSummary(null);
      }

      // Load all open shifts in the active branch for transfers
      if (activeBranchId) {
        const branchShiftsRes = await shiftApi.getShifts({
          branchId: activeBranchId,
          status: 0, // Open
          pageSize: 50
        });
        setBranchOpenShifts(branchShiftsRes.items || []);
      }
    } catch (err: any) {
      console.error('Failed to load shift context', err);
    }
  }, [activeBranchId]);

  // Load categories
  const loadCategories = useCallback(async () => {
    try {
      const cats = await cashLedgerApi.getCategories(undefined, true);
      setCategories(cats || []);
    } catch (err: any) {
      console.error('Failed to load categories', err);
    }
  }, []);

  // Load transactions
  const loadTransactions = useCallback(async () => {
    setIsTxLoading(true);
    try {
      const res = await cashLedgerApi.getTransactions({
        branchId: activeBranchId || undefined,
        cashDrawerId: filterDrawer || undefined,
        transactionType: filterType ? parseInt(filterType, 10) : undefined,
        categoryId: filterCategory || undefined,
        status: filterStatus ? parseInt(filterStatus, 10) : undefined,
        fromDate: filterFromDate ? new Date(filterFromDate).toISOString() : undefined,
        toDate: filterToDate ? new Date(filterToDate).toISOString() : undefined,
        page,
        pageSize
      });
      setTransactions(res.items || []);
      setTotalCount(res.totalCount || 0);
    } catch (err: any) {
      setError(err.message || 'Failed to load ledger transactions.');
    } finally {
      setIsTxLoading(false);
    }
  }, [activeBranchId, filterDrawer, filterType, filterCategory, filterStatus, filterFromDate, filterToDate, page]);

  useEffect(() => {
    setIsLoading(true);
    Promise.all([loadShiftContext(), loadCategories(), loadTransactions()]).finally(() => {
      setIsLoading(false);
    });
  }, [loadShiftContext, loadCategories, loadTransactions]);

  // Handlers
  const handleCashIn = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeShift) return;
    const amt = parseFloat(inAmount);
    if (isNaN(amt) || amt <= 0) {
      setError('Please enter a valid positive amount.');
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      await cashLedgerApi.postCashIn(
        {
          shiftId: activeShift.id,
          amount: amt,
          categoryId: inCategory || null,
          description: inDescription || null,
          reason: inReason || null,
          externalReference: inReference || null
        },
        generateIdempotencyKey()
      );
      setSuccessMessage(`Cash In of Rs. ${amt.toLocaleString(undefined, { minimumFractionDigits: 2 })} posted successfully.`);
      setShowCashInModal(false);
      setInAmount('');
      setInCategory('');
      setInDescription('');
      setInReason('');
      setInReference('');
      await loadShiftContext();
      await loadTransactions();
    } catch (err: any) {
      setError(err.message || 'Failed to post Cash In.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCashOut = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeShift) return;
    const amt = parseFloat(outAmount);
    if (isNaN(amt) || amt <= 0) {
      setError('Please enter a valid positive amount.');
      return;
    }

    if (shiftSummary && amt > shiftSummary.currentExpectedBalance) {
      setError(`Insufficient drawer cash. Current expected cash is Rs. ${shiftSummary.currentExpectedBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}.`);
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      await cashLedgerApi.postCashOut(
        {
          shiftId: activeShift.id,
          amount: amt,
          categoryId: outCategory || null,
          description: outDescription || null,
          reason: outReason || null,
          externalReference: outReference || null
        },
        generateIdempotencyKey()
      );
      setSuccessMessage(`Cash Out of Rs. ${amt.toLocaleString(undefined, { minimumFractionDigits: 2 })} posted successfully.`);
      setShowCashOutModal(false);
      setOutAmount('');
      setOutCategory('');
      setOutDescription('');
      setOutReason('');
      setOutReference('');
      await loadShiftContext();
      await loadTransactions();
    } catch (err: any) {
      setError(err.message || 'Failed to post Cash Out.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleTransfer = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeShift) return;
    const amt = parseFloat(transferAmount);
    if (isNaN(amt) || amt <= 0) {
      setError('Please enter a valid transfer amount.');
      return;
    }

    if (!transferDestShiftId) {
      setError('Please select a destination cash drawer / shift.');
      return;
    }

    if (!transferReason.trim()) {
      setError('A transfer reason is mandatory.');
      return;
    }

    if (shiftSummary && amt > shiftSummary.currentExpectedBalance) {
      setError(`Insufficient drawer cash. Current expected balance is Rs. ${shiftSummary.currentExpectedBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}.`);
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      await cashLedgerApi.postTransfer(
        {
          sourceShiftId: activeShift.id,
          destinationShiftId: transferDestShiftId,
          amount: amt,
          reason: transferReason.trim(),
          externalReference: transferReference || null
        },
        generateIdempotencyKey()
      );
      setSuccessMessage(`Transferred Rs. ${amt.toLocaleString(undefined, { minimumFractionDigits: 2 })} successfully.`);
      setShowTransferModal(false);
      setTransferAmount('');
      setTransferDestShiftId('');
      setTransferReason('');
      setTransferReference('');
      await loadShiftContext();
      await loadTransactions();
    } catch (err: any) {
      setError(err.message || 'Failed to post transfer.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleReverse = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedTx) return;
    if (!reversalReason.trim()) {
      setError('A reversal reason is mandatory.');
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      await cashLedgerApi.reverseTransaction(
        selectedTx.id,
        { reversalReason: reversalReason.trim() },
        generateIdempotencyKey()
      );
      setSuccessMessage(`Transaction ${selectedTx.transactionReference} was reversed successfully.`);
      setShowReverseModal(false);
      setSelectedTx(null);
      setReversalReason('');
      await loadShiftContext();
      await loadTransactions();
    } catch (err: any) {
      setError(err.message || 'Failed to reverse transaction.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleSaveCategory = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!catName.trim() || !catCode.trim()) {
      setError('Category name and code are required.');
      return;
    }

    setIsSubmitting(true);
    setError(null);
    try {
      if (editingCategory) {
        await cashLedgerApi.updateCategory(editingCategory.id, {
          name: catName.trim(),
          allowedDirection: parseInt(catDirection, 10),
          description: catDescription || null
        });
        setSuccessMessage(`Category '${catName}' updated.`);
      } else {
        await cashLedgerApi.createCategory({
          name: catName.trim(),
          code: catCode.trim(),
          allowedDirection: parseInt(catDirection, 10),
          description: catDescription || null
        });
        setSuccessMessage(`Category '${catName}' created.`);
      }
      setEditingCategory(null);
      setCatName('');
      setCatCode('');
      setCatDirection('3');
      setCatDescription('');
      await loadCategories();
    } catch (err: any) {
      setError(err.message || 'Failed to save category.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleCategoryStatus = async (cat: CashMovementCategory) => {
    try {
      await cashLedgerApi.updateCategoryStatus(cat.id, !cat.isActive);
      await loadCategories();
      setSuccessMessage(`Category '${cat.name}' ${!cat.isActive ? 'activated' : 'deactivated'}.`);
    } catch (err: any) {
      setError(err.message || 'Failed to toggle category status.');
    }
  };

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Header Title */}
        <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
          <div>
            <div className="flex items-center space-x-2">
              
              <h1 className="text-2xl font-bold text-gray-900 tracking-tight">Physical Cash Ledger</h1>
              <span className="text-xs px-2 py-0.5 rounded-full font-bold bg-[#B89A60]/20 text-[#123B32] border border-[#B89A60]/40">
                Single Source of Truth
              </span>
            </div>
            <p className="text-sm text-gray-500 mt-1">
              Authoritative physical drawer tracking, cash receipts, petty payments, and transfers.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            {canManageCategories && (
              <button
                onClick={() => {
                  setEditingCategory(null);
                  setCatName('');
                  setCatCode('');
                  setCatDirection('3');
                  setCatDescription('');
                  setShowCategoryModal(true);
                }}
                className="px-3 py-2 bg-white text-gray-700 hover:bg-gray-50 rounded-lg text-xs font-semibold border border-gray-200 shadow-sm flex items-center gap-1.5 transition"
              >
                <Tag className="w-3.5 h-3.5 text-[#123B32]" />
                Categories
              </button>
            )}

            {canTransfer && activeShift && (
              <button
                onClick={() => setShowTransferModal(true)}
                className="px-3.5 py-2 bg-amber-600 hover:bg-amber-700 text-white rounded-lg text-xs font-bold shadow-sm flex items-center gap-1.5 transition"
              >
                <ArrowLeftRight className="w-3.5 h-3.5" />
                Drawer Transfer
              </button>
            )}

            {canCashOut && activeShift && (
              <button
                onClick={() => setShowCashOutModal(true)}
                className="px-3.5 py-2 bg-rose-700 hover:bg-rose-800 text-white rounded-lg text-xs font-bold shadow-sm flex items-center gap-1.5 transition"
              >
                <ArrowUpRight className="w-3.5 h-3.5" />
                Cash Out
              </button>
            )}

            {canCashIn && activeShift && (
              <button
                onClick={() => setShowCashInModal(true)}
                className="px-3.5 py-2 bg-[#123B32] hover:bg-[#1B6543] text-white rounded-lg text-xs font-bold shadow-sm flex items-center gap-1.5 transition border border-[#B89A60]/40"
              >
                <ArrowDownLeft className="w-3.5 h-3.5 text-[#B89A60]" />
                Cash In
              </button>
            )}
          </div>
        </div>

        {/* Notifications */}
        {error && (
          <div className="p-4 bg-red-50 border-l-4 border-red-500 rounded-r-lg flex items-start justify-between">
            <div className="flex items-start space-x-3">
              <AlertTriangle className="w-5 h-5 text-red-600 mt-0.5 flex-shrink-0" />
              <p className="text-sm text-red-800 font-medium">{error}</p>
            </div>
            <button onClick={() => setError(null)} className="text-red-500 hover:text-red-700">
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {successMessage && (
          <div className="p-4 bg-emerald-50 border-l-4 border-emerald-500 rounded-r-lg flex items-start justify-between">
            <div className="flex items-start space-x-3">
              <CheckCircle2 className="w-5 h-5 text-emerald-600 mt-0.5 flex-shrink-0" />
              <p className="text-sm text-emerald-800 font-medium">{successMessage}</p>
            </div>
            <button onClick={() => setSuccessMessage(null)} className="text-emerald-500 hover:text-emerald-700">
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* Active Shift Financial Card */}
        {activeShift && shiftSummary ? (
          <div className="bg-gradient-to-br from-[#123B32] to-[#124b33] rounded-2xl p-6 text-white shadow-md border border-[#B89A60]/30">
            <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-4 border-b border-white/10">
              <div>
                <div className="flex items-center space-x-2">
                  <span className="text-xs uppercase font-bold text-[#B89A60] tracking-wider">
                    Physical Drawer In-Flight Session
                  </span>
                  <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/30 text-emerald-300 border border-emerald-400/40">
                    OPEN SHIFT
                  </span>
                </div>
                <h2 className="text-xl font-bold mt-1 flex items-center gap-2">
                  <span>{shiftSummary.cashDrawerName}</span>
                  <span className="text-xs font-normal text-gray-300">({shiftSummary.cashDrawerCode})</span>
                  <span className="text-xs font-normal text-[#B89A60]">• {shiftSummary.shiftNumber}</span>
                </h2>
                <p className="text-xs text-gray-300 mt-0.5">
                  Cashier: <strong className="text-white">{shiftSummary.cashierName}</strong> • Branch: <strong className="text-white">{shiftSummary.branchName}</strong>
                </p>
              </div>

              <div className="text-right">
                <span className="text-[11px] font-bold uppercase tracking-wider text-[#B89A60]">
                  Authoritative Expected Cash
                </span>
                <div className="text-3xl font-black text-white mt-0.5">
                  Rs. {shiftSummary.currentExpectedBalance.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                </div>
                <div className="text-[10px] text-gray-300">
                  {shiftSummary.totalTransactionsCount} physical movements recorded
                </div>
              </div>
            </div>

            {/* Shift Balance Formula Line */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4 pt-4 text-center">
              <div className="bg-white/5 rounded-xl p-3 border border-white/10">
                <p className="text-[10px] text-gray-300 uppercase font-semibold">Opening Float</p>
                <p className="text-base font-bold text-white mt-0.5">
                  Rs. {shiftSummary.openingBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                </p>
              </div>

              <div className="bg-emerald-950/40 rounded-xl p-3 border border-emerald-500/30">
                <p className="text-[10px] text-emerald-300 uppercase font-semibold flex items-center justify-center gap-1">
                  <ArrowDownLeft className="w-3 h-3" /> Total Cash In
                </p>
                <p className="text-base font-bold text-emerald-300 mt-0.5">
                  + Rs. {shiftSummary.totalCashIn.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                </p>
              </div>

              <div className="bg-rose-950/40 rounded-xl p-3 border border-rose-500/30">
                <p className="text-[10px] text-rose-300 uppercase font-semibold flex items-center justify-center gap-1">
                  <ArrowUpRight className="w-3 h-3" /> Total Cash Out
                </p>
                <p className="text-base font-bold text-rose-300 mt-0.5">
                  - Rs. {shiftSummary.totalCashOut.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                </p>
              </div>

              <div className="bg-[#B89A60]/10 rounded-xl p-3 border border-[#B89A60]/30">
                <p className="text-[10px] text-[#B89A60] uppercase font-semibold">Formula Balance</p>
                <p className="text-base font-black text-[#B89A60] mt-0.5">
                  Rs. {shiftSummary.currentExpectedBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                </p>
              </div>
            </div>
          </div>
        ) : (
          <div className="bg-amber-50 border border-amber-200 rounded-xl p-5 flex items-start gap-4">
            <Lock className="w-6 h-6 text-amber-700 flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="text-sm font-bold text-amber-900">No Active Open Shift for Current Cashier</h3>
              <p className="text-xs text-amber-800 mt-1">
                You must have an authorized open shift on an active cash drawer to perform Cash In, Cash Out, and Cash Transfers.
                Open a shift on the <a href="/shifts" className="font-bold underline hover:text-amber-950">Shift Management</a> page to proceed.
              </p>
            </div>
          </div>
        )}

        {/* Transactions Section & Filters */}
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          {/* Filter Bar */}
          <div className="p-4 border-b border-gray-200 bg-gray-50/70 space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div className="flex items-center space-x-2">
                <FileText className="w-4 h-4 text-[#123B32]" />
                <h3 className="text-sm font-bold text-gray-800 uppercase tracking-wider">
                  Ledger Transactions History ({totalCount})
                </h3>
              </div>

              <div className="flex items-center gap-2">
                {(filterType || filterStatus || filterCategory || filterDrawer || filterFromDate || filterToDate) && (
                  <button
                    onClick={() => {
                      setFilterType('');
                      setFilterStatus('');
                      setFilterCategory('');
                      setFilterDrawer('');
                      setFilterFromDate('');
                      setFilterToDate('');
                      setPage(1);
                    }}
                    className="text-xs text-rose-600 hover:text-rose-800 font-semibold"
                  >
                    Reset Filters
                  </button>
                )}
              </div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-6 gap-3">
              <div>
                <label className="block text-[10px] font-bold text-gray-500 uppercase">Type</label>
                <select
                  value={filterType}
                  onChange={(e) => { setFilterType(e.target.value); setPage(1); }}
                  className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-800"
                >
                  <option value="">All Types</option>
                  <option value="1">Cash In</option>
                  <option value="2">Cash Out</option>
                  <option value="3">Transfer</option>
                  <option value="4">Reversal</option>
                </select>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-gray-500 uppercase">Category</label>
                <select
                  value={filterCategory}
                  onChange={(e) => { setFilterCategory(e.target.value); setPage(1); }}
                  className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-800"
                >
                  <option value="">All Categories</option>
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-gray-500 uppercase">Status</label>
                <select
                  value={filterStatus}
                  onChange={(e) => { setFilterStatus(e.target.value); setPage(1); }}
                  className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-800"
                >
                  <option value="">All Statuses</option>
                  <option value="1">Posted</option>
                  <option value="2">Reversed</option>
                </select>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-gray-500 uppercase">From Date</label>
                <input
                  type="date"
                  value={filterFromDate}
                  onChange={(e) => { setFilterFromDate(e.target.value); setPage(1); }}
                  className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-800"
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-gray-500 uppercase">To Date</label>
                <input
                  type="date"
                  value={filterToDate}
                  onChange={(e) => { setFilterToDate(e.target.value); setPage(1); }}
                  className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-800"
                />
              </div>

              <div className="flex items-end">
                <button
                  onClick={() => loadTransactions()}
                  className="w-full text-xs font-bold bg-gray-200 hover:bg-gray-300 text-gray-800 rounded-lg py-1.5 flex items-center justify-center gap-1 transition"
                >
                  <Filter className="w-3 h-3" /> Filter
                </button>
              </div>
            </div>
          </div>

          {/* Transactions Table */}
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
              <thead className="bg-gray-50 text-gray-500 font-bold uppercase tracking-wider text-[10px]">
                <tr>
                  <th className="px-4 py-3">Reference</th>
                  <th className="px-4 py-3">Type</th>
                  <th className="px-4 py-3">Category / Details</th>
                  <th className="px-4 py-3">Drawers & Shifts</th>
                  <th className="px-4 py-3 text-right">Cash In (+)</th>
                  <th className="px-4 py-3 text-right">Cash Out (-)</th>
                  <th className="px-4 py-3">Recorded By</th>
                  <th className="px-4 py-3">Timestamp</th>
                  <th className="px-4 py-3 text-center">Status</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200 bg-white">
                {isTxLoading ? (
                  <tr>
                    <td colSpan={10} className="px-4 py-8 text-center text-gray-500 font-medium">
                      Loading cash movements...
                    </td>
                  </tr>
                ) : transactions.length === 0 ? (
                  <tr>
                    <td colSpan={10} className="px-4 py-8 text-center text-gray-400">
                      No cash movements recorded matching the selected criteria.
                    </td>
                  </tr>
                ) : (
                  transactions.map((tx) => {
                    const isCashIn = tx.transactionType === 1;
                    const isCashOut = tx.transactionType === 2;
                    const isTransfer = tx.transactionType === 3;
                    const isReversal = tx.transactionType === 4;
                    const isPosted = tx.status === 1;

                    return (
                      <tr key={tx.id} className="hover:bg-gray-50/80 transition">
                        <td className="px-4 py-3 font-mono font-bold text-gray-900 whitespace-nowrap">
                          {tx.transactionReference}
                          {tx.reversalOfTransactionReference && (
                            <div className="text-[10px] text-gray-500 font-normal">
                              Rev of: {tx.reversalOfTransactionReference}
                            </div>
                          )}
                        </td>

                        <td className="px-4 py-3 whitespace-nowrap">
                          {isCashIn && (
                            <span className="inline-flex items-center gap-1 text-[11px] font-bold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
                              <ArrowDownLeft className="w-3 h-3" /> Cash In
                            </span>
                          )}
                          {isCashOut && (
                            <span className="inline-flex items-center gap-1 text-[11px] font-bold text-rose-700 bg-rose-50 px-2 py-0.5 rounded border border-rose-200">
                              <ArrowUpRight className="w-3 h-3" /> Cash Out
                            </span>
                          )}
                          {isTransfer && (
                            <span className="inline-flex items-center gap-1 text-[11px] font-bold text-amber-700 bg-amber-50 px-2 py-0.5 rounded border border-amber-200">
                              <ArrowLeftRight className="w-3 h-3" /> Transfer
                            </span>
                          )}
                          {isReversal && (
                            <span className="inline-flex items-center gap-1 text-[11px] font-bold text-purple-700 bg-purple-50 px-2 py-0.5 rounded border border-purple-200">
                              <RotateCcw className="w-3 h-3" /> Reversal
                            </span>
                          )}
                        </td>

                        <td className="px-4 py-3 max-w-xs">
                          <div className="font-semibold text-gray-800">
                            {tx.categoryName || (isTransfer ? 'Drawer Transfer' : isReversal ? 'Compensating Reversal' : 'General')}
                          </div>
                          {(tx.reason || tx.description) && (
                            <div className="text-[11px] text-gray-500 truncate" title={tx.reason || tx.description || ''}>
                              {tx.reason || tx.description}
                            </div>
                          )}
                        </td>

                        <td className="px-4 py-3 whitespace-nowrap text-[11px] text-gray-600">
                          {tx.entries && tx.entries.length > 0 ? (
                            <div className="space-y-0.5">
                              {tx.entries.map((e) => (
                                <div key={e.id} className="flex items-center gap-1">
                                  <span className={`font-bold ${e.direction === 1 ? 'text-emerald-700' : 'text-rose-700'}`}>
                                    {e.direction === 1 ? 'IN:' : 'OUT:'}
                                  </span>
                                  <span>{e.cashDrawerName}</span>
                                  <span className="text-gray-400">({e.shiftNumber})</span>
                                </div>
                              ))}
                            </div>
                          ) : (
                            <span className="text-gray-400">—</span>
                          )}
                        </td>

                        <td className="px-4 py-3 text-right font-mono font-bold text-emerald-700 whitespace-nowrap">
                          {isCashIn || (isReversal && tx.entries.some(e => e.direction === 1)) ? (
                            `+ ${tx.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`
                          ) : isTransfer ? (
                            <span className="text-gray-400 font-normal">± {tx.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span>
                          ) : (
                            '—'
                          )}
                        </td>

                        <td className="px-4 py-3 text-right font-mono font-bold text-rose-700 whitespace-nowrap">
                          {isCashOut || (isReversal && tx.entries.some(e => e.direction === 2)) ? (
                            `- ${tx.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}`
                          ) : (
                            '—'
                          )}
                        </td>

                        <td className="px-4 py-3 text-gray-600 whitespace-nowrap">
                          {tx.createdByName || 'System'}
                        </td>

                        <td className="px-4 py-3 text-gray-500 whitespace-nowrap">
                          {new Date(tx.transactionAt).toLocaleString(undefined, {
                            dateStyle: 'short',
                            timeStyle: 'short'
                          })}
                        </td>

                        <td className="px-4 py-3 text-center whitespace-nowrap">
                          {isPosted ? (
                            <span className="inline-block px-2 py-0.5 text-[10px] font-bold rounded-full bg-emerald-100 text-emerald-800">
                              POSTED
                            </span>
                          ) : (
                            <span className="inline-block px-2 py-0.5 text-[10px] font-bold rounded-full bg-rose-100 text-rose-800">
                              REVERSED
                            </span>
                          )}
                        </td>

                        <td className="px-4 py-3 text-right whitespace-nowrap space-x-2">
                          <button
                            onClick={() => {
                              setSelectedTx(tx);
                              setShowDetailsModal(true);
                            }}
                            className="p-1 text-gray-500 hover:text-gray-800 transition"
                            title="View Transaction Entries"
                          >
                            <Eye className="w-4 h-4" />
                          </button>

                          {canReverse && isPosted && tx.sourceType === 1 && !isReversal && (
                            <button
                              onClick={() => {
                                setSelectedTx(tx);
                                setReversalReason('');
                                setShowReverseModal(true);
                              }}
                              className="text-xs text-rose-600 hover:text-rose-800 font-bold px-2 py-1 bg-rose-50 hover:bg-rose-100 rounded border border-rose-200 transition"
                              title="Reverse this transaction"
                            >
                              Reverse
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
          {totalCount > pageSize && (
            <div className="px-4 py-3 border-t border-gray-200 flex items-center justify-between text-xs text-gray-500">
              <div>
                Showing page {page} of {Math.ceil(totalCount / pageSize)} ({totalCount} total)
              </div>
              <div className="flex gap-2">
                <button
                  disabled={page <= 1}
                  onClick={() => setPage(p => p - 1)}
                  className="px-3 py-1 bg-white border border-gray-300 rounded font-semibold disabled:opacity-50"
                >
                  Previous
                </button>
                <button
                  disabled={page >= Math.ceil(totalCount / pageSize)}
                  onClick={() => setPage(p => p + 1)}
                  className="px-3 py-1 bg-white border border-gray-300 rounded font-semibold disabled:opacity-50"
                >
                  Next
                </button>
              </div>
            </div>
          )}
        </div>

        {/* CASH IN MODAL */}
        {showCashInModal && activeShift && shiftSummary && (
          <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-xl border border-gray-200">
              <div className="flex items-center justify-between pb-4 border-b border-gray-100">
                <div className="flex items-center space-x-2">
                  <span className="w-7 h-7 rounded-lg bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold">
                    <ArrowDownLeft className="w-4 h-4" />
                  </span>
                  <h3 className="text-lg font-bold text-gray-900">Physical Cash In</h3>
                </div>
                <button onClick={() => setShowCashInModal(false)} className="text-gray-400 hover:text-gray-600">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <form onSubmit={handleCashIn} className="space-y-4 mt-4">
                <div className="bg-emerald-50/70 border border-emerald-200 rounded-xl p-3 text-xs text-emerald-900">
                  Target Drawer: <strong>{shiftSummary.cashDrawerName}</strong> ({shiftSummary.cashDrawerCode}) • Current Expected: <strong>Rs. {shiftSummary.currentExpectedBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}</strong>
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Cash Amount (Rs.) *</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0.01"
                    required
                    value={inAmount}
                    onChange={(e) => setInAmount(e.target.value)}
                    placeholder="e.g. 5,000.00"
                    className="mt-1 w-full text-base font-bold font-mono border border-gray-300 rounded-lg px-3 py-2 text-gray-900 focus:ring-2 focus:ring-[#123B32]"
                  />
                </div>

                {/* Live Preview */}
                {parseFloat(inAmount) > 0 && (
                  <div className="bg-gray-50 border border-gray-200 rounded-lg p-2.5 text-xs flex justify-between items-center font-medium">
                    <span className="text-gray-500">Expected Balance After Posting:</span>
                    <span className="font-bold text-emerald-700 font-mono">
                      Rs. {(shiftSummary.currentExpectedBalance + (parseFloat(inAmount) || 0)).toLocaleString(undefined, { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                )}

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Category</label>
                  <select
                    value={inCategory}
                    onChange={(e) => setInCategory(e.target.value)}
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-3 py-2 bg-white text-gray-900"
                  >
                    <option value="">Select Category (Optional)</option>
                    {categories.filter(c => c.allowedDirection === 1 || c.allowedDirection === 3).map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Reason / Notes</label>
                  <textarea
                    rows={2}
                    value={inReason}
                    onChange={(e) => setInReason(e.target.value)}
                    placeholder="Describe where the received cash came from"
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-3 py-2 text-gray-900"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">External Receipt Reference (Optional)</label>
                  <input
                    type="text"
                    value={inReference}
                    onChange={(e) => setInReference(e.target.value)}
                    placeholder="e.g. receipt or deposit reference"
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-3 py-2 text-gray-900"
                  />
                </div>

                <div className="pt-2 flex justify-end gap-2">
                  <button
                    type="button"
                    onClick={() => setShowCashInModal(false)}
                    className="px-4 py-2 border border-gray-300 text-gray-700 hover:bg-gray-50 rounded-lg text-xs font-semibold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmitting}
                    className="px-5 py-2 bg-[#123B32] hover:bg-[#1B6543] text-white rounded-lg text-xs font-bold shadow-sm disabled:opacity-50"
                  >
                    {isSubmitting ? 'Posting...' : 'Post Cash In'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* CASH OUT MODAL */}
        {showCashOutModal && activeShift && shiftSummary && (
          <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-xl border border-gray-200">
              <div className="flex items-center justify-between pb-4 border-b border-gray-100">
                <div className="flex items-center space-x-2">
                  <span className="w-7 h-7 rounded-lg bg-rose-100 text-rose-800 flex items-center justify-center font-bold">
                    <ArrowUpRight className="w-4 h-4" />
                  </span>
                  <h3 className="text-lg font-bold text-gray-900">Physical Cash Out</h3>
                </div>
                <button onClick={() => setShowCashOutModal(false)} className="text-gray-400 hover:text-gray-600">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <form onSubmit={handleCashOut} className="space-y-4 mt-4">
                <div className="bg-rose-50/70 border border-rose-200 rounded-xl p-3 text-xs text-rose-900">
                  Source Drawer: <strong>{shiftSummary.cashDrawerName}</strong> ({shiftSummary.cashDrawerCode}) • Current Expected: <strong>Rs. {shiftSummary.currentExpectedBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}</strong>
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Cash Amount (Rs.) *</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0.01"
                    required
                    value={outAmount}
                    onChange={(e) => setOutAmount(e.target.value)}
                    placeholder="e.g. 5,000.00"
                    className="mt-1 w-full text-base font-bold font-mono border border-gray-300 rounded-lg px-3 py-2 text-gray-900 focus:ring-2 focus:ring-rose-600"
                  />
                </div>

                {/* Live Preview and Overdraft Guard */}
                {parseFloat(outAmount) > 0 && (
                  <div className={`p-2.5 rounded-lg border text-xs flex justify-between items-center font-medium ${
                    parseFloat(outAmount) > shiftSummary.currentExpectedBalance
                      ? 'bg-rose-50 border-rose-300 text-rose-900'
                      : 'bg-gray-50 border-gray-200 text-gray-700'
                  }`}>
                    <span>
                      {parseFloat(outAmount) > shiftSummary.currentExpectedBalance ? (
                        <span className="text-rose-700 font-bold flex items-center gap-1">
                          <AlertTriangle className="w-3.5 h-3.5" /> Insufficient expected cash:
                        </span>
                      ) : (
                        'Expected Remaining Balance:'
                      )}
                    </span>
                    <span className="font-bold font-mono">
                      Rs. {(shiftSummary.currentExpectedBalance - (parseFloat(outAmount) || 0)).toLocaleString(undefined, { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                )}

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Category</label>
                  <select
                    value={outCategory}
                    onChange={(e) => setOutCategory(e.target.value)}
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-3 py-2 bg-white text-gray-900"
                  >
                    <option value="">Select Category (Optional)</option>
                    {categories.filter(c => c.allowedDirection === 2 || c.allowedDirection === 3).map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Reason / Expense Description *</label>
                  <textarea
                    rows={2}
                    required
                    value={outReason}
                    onChange={(e) => setOutReason(e.target.value)}
                    placeholder="E.g. Tea & staff refreshments, store cleaning supplies, bank cash deposit..."
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-3 py-2 text-gray-900"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">External Expense Voucher / Ref</label>
                  <input
                    type="text"
                    value={outReference}
                    onChange={(e) => setOutReference(e.target.value)}
                    placeholder="e.g. receipt or expense voucher reference"
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-3 py-2 text-gray-900"
                  />
                </div>

                <div className="pt-2 flex justify-end gap-2">
                  <button
                    type="button"
                    onClick={() => setShowCashOutModal(false)}
                    className="px-4 py-2 border border-gray-300 text-gray-700 hover:bg-gray-50 rounded-lg text-xs font-semibold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmitting || (parseFloat(outAmount) > shiftSummary.currentExpectedBalance)}
                    className="px-5 py-2 bg-rose-700 hover:bg-rose-800 text-white rounded-lg text-xs font-bold shadow-sm disabled:opacity-50"
                  >
                    {isSubmitting ? 'Posting...' : 'Post Cash Out'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* DRAWER TRANSFER MODAL */}
        {showTransferModal && activeShift && shiftSummary && (
          <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-xl border border-gray-200">
              <div className="flex items-center justify-between pb-4 border-b border-gray-100">
                <div className="flex items-center space-x-2">
                  <span className="w-7 h-7 rounded-lg bg-amber-100 text-amber-800 flex items-center justify-center font-bold">
                    <ArrowLeftRight className="w-4 h-4" />
                  </span>
                  <h3 className="text-lg font-bold text-gray-900">Physical Drawer Transfer</h3>
                </div>
                <button onClick={() => setShowTransferModal(false)} className="text-gray-400 hover:text-gray-600">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <form onSubmit={handleTransfer} className="space-y-4 mt-4">
                <div className="bg-amber-50/70 border border-amber-200 rounded-xl p-3 text-xs text-amber-900 space-y-1">
                  <div>Source: <strong>{shiftSummary.cashDrawerName}</strong> ({shiftSummary.cashDrawerCode}) • Shift: <strong>{shiftSummary.shiftNumber}</strong></div>
                  <div>Current Expected Balance: <strong>Rs. {shiftSummary.currentExpectedBalance.toLocaleString(undefined, { minimumFractionDigits: 2 })}</strong></div>
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Destination Cash Drawer / Shift *</label>
                  <select
                    required
                    value={transferDestShiftId}
                    onChange={(e) => setTransferDestShiftId(e.target.value)}
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-3 py-2 bg-white text-gray-900 font-semibold"
                  >
                    <option value="">Select Destination Open Shift...</option>
                    {branchOpenShifts
                      .filter(s => s.id !== activeShift.id && s.cashDrawerId !== activeShift.cashDrawerId)
                      .map((s) => (
                        <option key={s.id} value={s.id}>
                          {s.cashDrawerName || 'Drawer'} ({s.cashDrawerCode}) • {s.shiftNumber} (Cashier: {s.cashierName})
                        </option>
                      ))}
                  </select>
                  {branchOpenShifts.filter(s => s.id !== activeShift.id).length === 0 && (
                    <p className="text-[11px] text-rose-600 mt-1">
                      No other open shift found in this branch. Transfers require an active destination drawer session.
                    </p>
                  )}
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Transfer Amount (Rs.) *</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0.01"
                    required
                    value={transferAmount}
                    onChange={(e) => setTransferAmount(e.target.value)}
                    placeholder="e.g. 5,000.00"
                    className="mt-1 w-full text-base font-bold font-mono border border-gray-300 rounded-lg px-3 py-2 text-gray-900 focus:ring-2 focus:ring-amber-600"
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Transfer Reason *</label>
                  <textarea
                    rows={2}
                    required
                    value={transferReason}
                    onChange={(e) => setTransferReason(e.target.value)}
                    placeholder="Explain the purpose of this cash transfer"
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-3 py-2 text-gray-900"
                  />
                </div>

                <div className="p-3 bg-gray-50 border border-gray-200 rounded-lg text-[11px] text-gray-600">
                  <p className="font-semibold text-gray-800 mb-0.5">Dual-Entry Atomic Transfer Guarantee</p>
                  One physical transfer produces linked movements: <strong>OUT</strong> of source drawer and <strong>IN</strong> to destination drawer under the same transaction reference.
                </div>

                <div className="pt-2 flex justify-end gap-2">
                  <button
                    type="button"
                    onClick={() => setShowTransferModal(false)}
                    className="px-4 py-2 border border-gray-300 text-gray-700 hover:bg-gray-50 rounded-lg text-xs font-semibold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmitting || !transferDestShiftId || (parseFloat(transferAmount) > shiftSummary.currentExpectedBalance)}
                    className="px-5 py-2 bg-amber-600 hover:bg-amber-700 text-white rounded-lg text-xs font-bold shadow-sm disabled:opacity-50"
                  >
                    {isSubmitting ? 'Transferring...' : 'Execute Transfer'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* REVERSAL MODAL */}
        {showReverseModal && selectedTx && (
          <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-xl border border-gray-200">
              <div className="flex items-center justify-between pb-4 border-b border-gray-100">
                <div className="flex items-center space-x-2">
                  <span className="w-7 h-7 rounded-lg bg-purple-100 text-purple-800 flex items-center justify-center font-bold">
                    <RotateCcw className="w-4 h-4" />
                  </span>
                  <h3 className="text-lg font-bold text-gray-900">Reverse Transaction</h3>
                </div>
                <button onClick={() => setShowReverseModal(false)} className="text-gray-400 hover:text-gray-600">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <form onSubmit={handleReverse} className="space-y-4 mt-4">
                <div className="bg-purple-50/70 border border-purple-200 rounded-xl p-3 text-xs text-purple-900 space-y-1">
                  <div>Reversing: <strong className="font-mono">{selectedTx.transactionReference}</strong></div>
                  <div>Type: <strong>{selectedTx.transactionTypeName}</strong> • Amount: <strong>Rs. {selectedTx.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}</strong></div>
                  <div>Recorded: {new Date(selectedTx.transactionAt).toLocaleString()} by {selectedTx.createdByName}</div>
                </div>

                <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-xs text-amber-900">
                  <p className="font-bold">Financial Audit Protection:</p>
                  Reversal will post compensating entries to return drawer balances to their prior state while preserving original transaction history. All affected shifts must still be <strong>OPEN</strong>.
                </div>

                <div>
                  <label className="block text-xs font-bold text-gray-700 uppercase">Mandatory Reversal Reason *</label>
                  <textarea
                    rows={3}
                    required
                    value={reversalReason}
                    onChange={(e) => setReversalReason(e.target.value)}
                    placeholder="Explain why the original cash movement must be reversed"
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-3 py-2 text-gray-900 focus:ring-2 focus:ring-purple-600"
                  />
                </div>

                <div className="pt-2 flex justify-end gap-2">
                  <button
                    type="button"
                    onClick={() => setShowReverseModal(false)}
                    className="px-4 py-2 border border-gray-300 text-gray-700 hover:bg-gray-50 rounded-lg text-xs font-semibold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={isSubmitting || !reversalReason.trim()}
                    className="px-5 py-2 bg-purple-700 hover:bg-purple-800 text-white rounded-lg text-xs font-bold shadow-sm disabled:opacity-50"
                  >
                    {isSubmitting ? 'Reversing...' : 'Confirm Reversal'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* TRANSACTION DETAILS MODAL */}
        {showDetailsModal && selectedTx && (
          <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-xl border border-gray-200 max-h-[90vh] overflow-y-auto">
              <div className="flex items-center justify-between pb-4 border-b border-gray-100">
                <div>
                  <span className="text-[10px] font-bold uppercase tracking-wider text-gray-500">
                    Physical Cash Movement Details
                  </span>
                  <h3 className="text-xl font-bold font-mono text-gray-900">{selectedTx.transactionReference}</h3>
                </div>
                <button onClick={() => setShowDetailsModal(false)} className="text-gray-400 hover:text-gray-600">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <div className="space-y-4 mt-4 text-xs">
                {/* Header Information Grid */}
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 bg-gray-50 p-3 rounded-xl border border-gray-200">
                  <div>
                    <span className="text-gray-500 font-semibold block uppercase text-[10px]">Type</span>
                    <span className="font-bold text-gray-900">{selectedTx.transactionTypeName}</span>
                  </div>
                  <div>
                    <span className="text-gray-500 font-semibold block uppercase text-[10px]">Source</span>
                    <span className="font-bold text-gray-900">{selectedTx.sourceTypeName}</span>
                  </div>
                  <div>
                    <span className="text-gray-500 font-semibold block uppercase text-[10px]">Amount</span>
                    <span className="font-bold font-mono text-gray-900">
                      Rs. {selectedTx.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                  <div>
                    <span className="text-gray-500 font-semibold block uppercase text-[10px]">Status</span>
                    <span className={`inline-block px-2 py-0.5 rounded text-[10px] font-bold ${
                      selectedTx.status === 1 ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'
                    }`}>
                      {selectedTx.statusName}
                    </span>
                  </div>
                </div>

                {/* Reasons & Notes */}
                {(selectedTx.reason || selectedTx.description) && (
                  <div className="bg-gray-50 p-3 rounded-xl border border-gray-200 space-y-1">
                    {selectedTx.reason && (
                      <div><strong className="text-gray-700">Reason:</strong> {selectedTx.reason}</div>
                    )}
                    {selectedTx.description && (
                      <div><strong className="text-gray-700">Description:</strong> {selectedTx.description}</div>
                    )}
                  </div>
                )}

                {/* Reversal relationship banner */}
                {selectedTx.reversalOfTransactionReference && (
                  <div className="bg-purple-50 p-3 rounded-xl border border-purple-200 text-purple-900">
                    This transaction is a compensating reversal of <strong>{selectedTx.reversalOfTransactionReference}</strong>.
                  </div>
                )}

                {/* Ledger Entries Breakdown */}
                <div>
                  <h4 className="font-bold text-gray-800 uppercase tracking-wider text-[11px] mb-2">
                    Individual Drawer Postings ({selectedTx.entries?.length || 0})
                  </h4>
                  <div className="border border-gray-200 rounded-xl overflow-hidden">
                    <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                      <thead className="bg-gray-100/70 text-gray-600 text-[10px] uppercase font-bold">
                        <tr>
                          <th className="px-3 py-2">Drawer</th>
                          <th className="px-3 py-2">Shift</th>
                          <th className="px-3 py-2">Direction</th>
                          <th className="px-3 py-2 text-right">Amount</th>
                          <th className="px-3 py-2">Recorded At</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-200 bg-white">
                        {selectedTx.entries?.map((entry) => (
                          <tr key={entry.id}>
                            <td className="px-3 py-2 font-semibold text-gray-900">
                              {entry.cashDrawerName} <span className="text-gray-400 font-normal">({entry.cashDrawerCode})</span>
                            </td>
                            <td className="px-3 py-2 font-mono text-gray-700">{entry.shiftNumber}</td>
                            <td className="px-3 py-2">
                              <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                                entry.direction === 1 ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'
                              }`}>
                                {entry.direction === 1 ? 'IN (+)' : 'OUT (-)'}
                              </span>
                            </td>
                            <td className="px-3 py-2 text-right font-mono font-bold text-gray-900">
                              Rs. {entry.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                            </td>
                            <td className="px-3 py-2 text-gray-500">
                              {new Date(entry.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              </div>

              <div className="pt-4 flex justify-end">
                <button
                  type="button"
                  onClick={() => setShowDetailsModal(false)}
                  className="px-4 py-2 bg-gray-200 hover:bg-gray-300 text-gray-800 rounded-lg text-xs font-semibold"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        )}

        {/* CATEGORY MANAGEMENT MODAL */}
        {showCategoryModal && (
          <div className="fixed inset-0 bg-black/50 z-50 flex items-center justify-center p-4">
            <div className="bg-white rounded-2xl max-w-3xl w-full p-6 shadow-xl border border-gray-200 max-h-[90vh] overflow-y-auto">
              <div className="flex items-center justify-between pb-4 border-b border-gray-100">
                <div className="flex items-center space-x-2">
                  <span className="w-7 h-7 rounded-lg bg-emerald-100 text-emerald-800 flex items-center justify-center font-bold">
                    <Tag className="w-4 h-4" />
                  </span>
                  <h3 className="text-lg font-bold text-gray-900">Cash Movement Categories</h3>
                </div>
                <button onClick={() => setShowCategoryModal(false)} className="text-gray-400 hover:text-gray-600">
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Create/Edit Category Form */}
              <form onSubmit={handleSaveCategory} className="space-y-3 mt-4 p-4 bg-gray-50 rounded-xl border border-gray-200">
                <h4 className="text-xs font-bold uppercase text-[#123B32] tracking-wider">
                  {editingCategory ? `Edit Category: ${editingCategory.name}` : 'Add New Category'}
                </h4>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                  <div>
                    <label className="block text-[10px] font-bold text-gray-700 uppercase">Name *</label>
                    <input
                      type="text"
                      required
                      value={catName}
                      onChange={(e) => setCatName(e.target.value)}
                      placeholder="e.g. Bank Deposit"
                      className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-900"
                    />
                  </div>
                  <div>
                    <label className="block text-[10px] font-bold text-gray-700 uppercase">Code *</label>
                    <input
                      type="text"
                      required
                      disabled={!!editingCategory}
                      value={catCode}
                      onChange={(e) => setCatCode(e.target.value.toUpperCase())}
                      placeholder="e.g. BANK_DEP"
                      className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-900 uppercase disabled:bg-gray-100"
                    />
                  </div>
                  <div>
                    <label className="block text-[10px] font-bold text-gray-700 uppercase">Allowed Direction *</label>
                    <select
                      value={catDirection}
                      onChange={(e) => setCatDirection(e.target.value)}
                      className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-900 font-semibold"
                    >
                      <option value="1">Cash In Only</option>
                      <option value="2">Cash Out Only</option>
                      <option value="3">Both In and Out</option>
                    </select>
                  </div>
                </div>

                <div>
                  <label className="block text-[10px] font-bold text-gray-700 uppercase">Description</label>
                  <input
                    type="text"
                    value={catDescription}
                    onChange={(e) => setCatDescription(e.target.value)}
                    placeholder="Describe this cash movement classification"
                    className="mt-1 w-full text-xs border border-gray-300 rounded-lg px-2.5 py-1.5 bg-white text-gray-900"
                  />
                </div>

                <div className="flex justify-end gap-2 pt-1">
                  {editingCategory && (
                    <button
                      type="button"
                      onClick={() => {
                        setEditingCategory(null);
                        setCatName('');
                        setCatCode('');
                        setCatDirection('3');
                        setCatDescription('');
                      }}
                      className="px-3 py-1.5 border border-gray-300 text-gray-700 hover:bg-gray-100 rounded-lg text-xs font-semibold"
                    >
                      Cancel Edit
                    </button>
                  )}
                  <button
                    type="submit"
                    disabled={isSubmitting}
                    className="px-4 py-1.5 bg-[#123B32] hover:bg-[#1B6543] text-white rounded-lg text-xs font-bold disabled:opacity-50"
                  >
                    {isSubmitting ? 'Saving...' : editingCategory ? 'Update Category' : 'Create Category'}
                  </button>
                </div>
              </form>

              {/* Categories Table */}
              <div className="mt-4 border border-gray-200 rounded-xl overflow-hidden">
                <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                  <thead className="bg-gray-100/70 text-gray-600 text-[10px] uppercase font-bold">
                    <tr>
                      <th className="px-3 py-2">Code</th>
                      <th className="px-3 py-2">Name</th>
                      <th className="px-3 py-2">Direction</th>
                      <th className="px-3 py-2">Description</th>
                      <th className="px-3 py-2 text-center">Status</th>
                      <th className="px-3 py-2 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200 bg-white">
                    {categories.map((c) => (
                      <tr key={c.id} className="hover:bg-gray-50">
                        <td className="px-3 py-2 font-mono font-bold text-gray-900">{c.code}</td>
                        <td className="px-3 py-2 font-semibold text-gray-800">{c.name}</td>
                        <td className="px-3 py-2">
                          <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-gray-100 text-gray-700">
                            {c.allowedDirection === 1 ? 'IN ONLY' : c.allowedDirection === 2 ? 'OUT ONLY' : 'BOTH'}
                          </span>
                        </td>
                        <td className="px-3 py-2 text-gray-500 max-w-xs truncate">{c.description || '—'}</td>
                        <td className="px-3 py-2 text-center">
                          <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded-full ${
                            c.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-gray-100 text-gray-600'
                          }`}>
                            {c.isActive ? 'Active' : 'Inactive'}
                          </span>
                        </td>
                        <td className="px-3 py-2 text-right space-x-2">
                          <button
                            onClick={() => {
                              setEditingCategory(c);
                              setCatName(c.name);
                              setCatCode(c.code);
                              setCatDirection(String(c.allowedDirection));
                              setCatDescription(c.description || '');
                            }}
                            className="text-xs text-[#123B32] hover:underline font-bold"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleToggleCategoryStatus(c)}
                            className={`text-xs font-bold hover:underline ${
                              c.isActive ? 'text-amber-700' : 'text-emerald-700'
                            }`}
                          >
                            {c.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="pt-4 flex justify-end">
                <button
                  type="button"
                  onClick={() => setShowCategoryModal(false)}
                  className="px-4 py-2 bg-gray-200 hover:bg-gray-300 text-gray-800 rounded-lg text-xs font-semibold"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
