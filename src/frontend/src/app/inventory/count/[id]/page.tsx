'use client';

import React, { useState, useEffect, useRef, useCallback } from 'react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import {
  inventoryCountApi,
  InventoryCount,
  InventoryCountLine,
  InventoryCountUnexpectedItem,
  ScanResult,
  CompositionScanPreview,
  CountVariances
} from '@/lib/api';

interface RecentScanEntry {
  id: string;
  timestamp: Date;
  outcome: string;
  message: string;
  tagNumber?: string;
  itemName?: string;
  grossWeight?: number;
}

export default function InventoryCountWorkspacePage() {
  const params = useParams();
  const router = useRouter();
  const { user } = useAuth();
  const countId = params?.id as string;

  const [count, setCount] = useState<InventoryCount | null>(null);
  const [lines, setLines] = useState<InventoryCountLine[]>([]);
  const [unexpectedItems, setUnexpectedItems] = useState<InventoryCountUnexpectedItem[]>([]);
  const [variances, setVariances] = useState<CountVariances | null>(null);

  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Scanner workspace state
  const [scanInput, setScanInput] = useState('');
  const [isScanning, setIsScanning] = useState(false);
  const [scanFeedback, setScanFeedback] = useState<ScanResult | null>(null);
  const [recentScans, setRecentScans] = useState<RecentScanEntry[]>([]);
  const scannerInputRef = useRef<HTMLInputElement>(null);

  // Composition Modal state
  const [compositionPreview, setCompositionPreview] = useState<CompositionScanPreview | null>(null);
  const [selectedMemberIds, setSelectedMemberIds] = useState<string[]>([]);
  const [isConfirmingComposition, setIsConfirmingComposition] = useState(false);

  // Finish Count Modal state
  const [isFinishModalOpen, setIsFinishModalOpen] = useState(false);
  const [isFinishing, setIsFinishing] = useState(false);

  // Variance Review Tab state
  const [varianceTab, setVarianceTab] = useState<'missing' | 'unexpected'>('missing');

  // Variance Resolution Modal state
  const [resolvingVariance, setResolvingVariance] = useState<{
    type: 'Missing' | 'Unexpected';
    id: string;
    tagNumber: string;
    itemName: string;
  } | null>(null);
  const [reasonCode, setReasonCode] = useState('ITEM_FOUND');
  const [resolutionNotes, setResolutionNotes] = useState('');
  const [isResolving, setIsResolving] = useState(false);
  const [resolveError, setResolveError] = useState<string | null>(null);

  // Complete Count Modal state
  const [isCompleteModalOpen, setIsCompleteModalOpen] = useState(false);
  const [completionNotes, setCompletionNotes] = useState('');
  const [isCompleting, setIsCompleting] = useState(false);

  // Cancel Count Modal state
  const [isCancelModalOpen, setIsCancelModalOpen] = useState(false);
  const [cancellationReason, setCancellationReason] = useState('');
  const [isCancelling, setIsCancelling] = useState(false);

  // Check permission for variance resolve
  const canResolveVariance = user?.isGlobalAdmin || user?.permissions?.includes('INVENTORY_VARIANCE_RESOLVE');

  // Load Count Data
  const loadCountData = useCallback(async () => {
    if (!countId) return;
    setIsLoading(true);
    setError(null);
    try {
      const countData = await inventoryCountApi.getCountById(countId);
      setCount(countData);

      if (countData.status === 1) {
        // In Progress: load lines and unexpected items
        const [linesRes, unexpRes] = await Promise.all([
          inventoryCountApi.getCountLines(countId, { page: 1, pageSize: 200 }),
          inventoryCountApi.getUnexpectedItems(countId),
        ]);
        setLines(linesRes.items);
        setUnexpectedItems(unexpRes);
      } else if (countData.status === 2 || countData.status === 3) {
        // Requires Review or Completed: load variances
        const varRes = await inventoryCountApi.getVariances(countId);
        setVariances(varRes);
      }
    } catch (err: any) {
      setError(err.message || 'Failed to load inventory count details.');
    } finally {
      setIsLoading(false);
    }
  }, [countId]);

  useEffect(() => {
    loadCountData();
  }, [loadCountData]);

  // Keep scanner focused
  useEffect(() => {
    if (count?.status === 1 && !isFinishModalOpen && !compositionPreview) {
      scannerInputRef.current?.focus();
    }
  }, [count?.status, isFinishModalOpen, compositionPreview]);

  // Handle Scan Submit
  const handleScanSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const identifier = scanInput.trim();
    if (!identifier) return;

    setIsScanning(true);
    setScanInput('');
    try {
      const res = await inventoryCountApi.scanItem(countId, identifier);
      setScanFeedback(res);
      setCount(res.countSummary);

      // Handle Composition Parent Detection
      if (res.scanOutcome === 'COMPOSITION_PARENT' && res.compositionPreview) {
        setCompositionPreview(res.compositionPreview);
        // Pre-select members that are not already counted
        const uncounted = res.compositionPreview.members
          .filter((m) => !m.isAlreadyCounted)
          .map((m) => m.inventoryItemId);
        setSelectedMemberIds(uncounted);
      } else if (res.scanOutcome === 'MATCHED' && res.expectedLine) {
        // Update local lines state
        setLines((prev) =>
          prev.map((l) => (l.id === res.expectedLine!.id ? res.expectedLine! : l))
        );
      } else if (res.scanOutcome === 'UNEXPECTED' && res.unexpectedItem) {
        setUnexpectedItems((prev) => [res.unexpectedItem!, ...prev]);
      }

      // Add to recent activity stream
      setRecentScans((prev) => [
        {
          id: crypto.randomUUID(),
          timestamp: new Date(),
          outcome: res.scanOutcome,
          message: res.message,
          tagNumber: res.expectedLine?.tagNumberSnapshot || res.unexpectedItem?.tagNumberSnapshot,
          itemName: res.expectedLine?.itemNameSnapshot || res.unexpectedItem?.itemNameSnapshot,
          grossWeight: res.expectedLine?.grossWeightSnapshot || res.unexpectedItem?.grossWeightSnapshot,
        },
        ...prev.slice(0, 9),
      ]);
    } catch (err: any) {
      setScanFeedback({
        scanOutcome: 'NOT_FOUND',
        message: err.message || 'Scan processing failed.',
        countSummary: count!,
      });
    } finally {
      setIsScanning(false);
      // Auto-refocus scanner field
      setTimeout(() => {
        scannerInputRef.current?.focus();
      }, 50);
    }
  };

  // Confirm Composition Members
  const handleConfirmComposition = async () => {
    if (!compositionPreview) return;
    setIsConfirmingComposition(true);
    try {
      const res = await inventoryCountApi.confirmCompositionScan(
        countId,
        compositionPreview.compositionId,
        selectedMemberIds
      );
      setCount(res.countSummary);
      setCompositionPreview(null);
      await loadCountData();

      setRecentScans((prev) => [
        {
          id: crypto.randomUUID(),
          timestamp: new Date(),
          outcome: 'MATCHED',
          message: res.message,
        },
        ...prev.slice(0, 9),
      ]);
    } catch (err: any) {
      alert(err.message || 'Failed to confirm composition scan.');
    } finally {
      setIsConfirmingComposition(false);
      setTimeout(() => {
        scannerInputRef.current?.focus();
      }, 50);
    }
  };

  // Finish Count Action
  const handleFinishCount = async () => {
    setIsFinishing(true);
    try {
      const updated = await inventoryCountApi.finishCount(countId);
      setCount(updated);
      setIsFinishModalOpen(false);
      await loadCountData();
    } catch (err: any) {
      alert(err.message || 'Failed to finish count session.');
    } finally {
      setIsFinishing(false);
    }
  };

  // Resolve Variance Submit
  const handleResolveVarianceSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!resolvingVariance) return;

    if (reasonCode === 'OTHER' && !resolutionNotes.trim()) {
      setResolveError('Notes are required when selecting "OTHER" as the reason code.');
      return;
    }

    setIsResolving(true);
    setResolveError(null);
    try {
      await inventoryCountApi.resolveVariance(countId, {
        varianceType: resolvingVariance.type,
        varianceId: resolvingVariance.id,
        reasonCode,
        notes: resolutionNotes.trim() || undefined,
      });
      setResolvingVariance(null);
      setResolutionNotes('');
      await loadCountData();
    } catch (err: any) {
      setResolveError(err.message || 'Failed to resolve variance.');
    } finally {
      setIsResolving(false);
    }
  };

  // Complete Count
  const handleCompleteCount = async () => {
    setIsCompleting(true);
    try {
      const updated = await inventoryCountApi.completeCount(countId, completionNotes.trim() || undefined);
      setCount(updated);
      setIsCompleteModalOpen(false);
      await loadCountData();
    } catch (err: any) {
      alert(err.message || 'Failed to complete count.');
    } finally {
      setIsCompleting(false);
    }
  };

  // Cancel Count
  const handleCancelCount = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!cancellationReason.trim()) {
      alert('Cancellation reason is required.');
      return;
    }
    setIsCancelling(true);
    try {
      const updated = await inventoryCountApi.cancelCount(countId, cancellationReason.trim());
      setCount(updated);
      setIsCancelModalOpen(false);
      await loadCountData();
    } catch (err: any) {
      alert(err.message || 'Failed to cancel count session.');
    } finally {
      setIsCancelling(false);
    }
  };

  if (isLoading && !count) {
    return (
      <AppLayout>
        <div className="flex items-center justify-center min-h-[400px]">
          <div className="text-center space-y-2">
            <div className="inline-block animate-spin text-3xl">⌛</div>
            <p className="text-sm font-semibold text-gray-700">Loading Inventory Count Session...</p>
          </div>
        </div>
      </AppLayout>
    );
  }

  if (error || !count) {
    return (
      <AppLayout>
        <div className="bg-red-50 p-6 rounded-xl border border-red-200 text-center space-y-3">
          <p className="text-red-700 font-semibold">{error || 'Inventory count session not found.'}</p>
          <Link href="/inventory/count" className="text-sm text-[#123B32] font-bold underline">
            ← Return to Inventory Counts
          </Link>
        </div>
      </AppLayout>
    );
  }

  const uncountedBaselineCount = Math.max(0, count.expectedItemCount - count.countedExpectedItemCount);
  const totalVariances = count.missingCount + count.unexpectedCount;
  const unresolvedMissing = variances?.missingItems.filter((m) => m.resolutionStatus === 0).length || 0;
  const unresolvedUnexpected = variances?.unexpectedItems.filter((u) => u.resolutionStatus === 0).length || 0;
  const allVariancesResolved = unresolvedMissing === 0 && unresolvedUnexpected === 0 && totalVariances > 0;

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Top Control Bar */}
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 bg-white p-6 rounded-xl shadow-sm border border-gray-200">
          <div>
            <div className="flex items-center space-x-3">
              
              <div>
                <div className="flex items-center space-x-2">
                  <h1 className="text-2xl font-bold text-gray-900 tracking-tight">
                    Count #{count.id.substring(0, 8).toUpperCase()}
                  </h1>
                  <span
                    className={`text-xs px-2.5 py-0.5 rounded-full font-bold uppercase tracking-wider ${
                      count.status === 1
                        ? 'bg-blue-100 text-blue-800 border border-blue-200'
                        : count.status === 2
                        ? 'bg-amber-100 text-amber-800 border border-amber-200'
                        : count.status === 3
                        ? 'bg-emerald-100 text-emerald-800 border border-emerald-200'
                        : 'bg-gray-100 text-gray-700'
                    }`}
                  >
                    {count.statusName}
                  </span>
                </div>
                <p className="text-xs text-gray-500 mt-1">
                  Branch: <span className="font-semibold text-gray-800">{count.branchCode} - {count.branchName}</span> &nbsp;|&nbsp; Location: <span className="font-semibold text-gray-800">{count.stockLocationCode} ({count.stockLocationName})</span> &nbsp;|&nbsp; Started: {new Date(count.startedAt).toLocaleString('en-GB')}
                </p>
              </div>
            </div>
          </div>

          <div className="flex items-center space-x-3">
            <Link
              href="/inventory/count"
              className="px-3.5 py-2 text-xs font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition"
            >
              ← All Sessions
            </Link>

            {count.status === 1 && (
              <>
                <button
                  onClick={() => setIsFinishModalOpen(true)}
                  className="px-4 py-2 text-xs font-bold text-white bg-emerald-700 hover:bg-emerald-800 rounded-lg shadow transition flex items-center space-x-1.5"
                >
                  ✓
                  <span>Finish & Reconcile Count</span>
                </button>
                <button
                  onClick={() => setIsCancelModalOpen(true)}
                  className="px-3 py-2 text-xs font-semibold text-red-700 bg-red-50 hover:bg-red-100 border border-red-200 rounded-lg transition"
                >
                  Cancel
                </button>
              </>
            )}

            {count.status === 2 && canResolveVariance && (
              <button
                onClick={() => setIsCompleteModalOpen(true)}
                disabled={!allVariancesResolved}
                className="px-4 py-2 text-xs font-bold text-white bg-[#123B32] hover:bg-[#1B6543] rounded-lg shadow disabled:opacity-40 transition"
                title={!allVariancesResolved ? 'All variances must be resolved before completing' : ''}
              >
                Complete Count Session →
              </button>
            )}
          </div>
        </div>

        {/* Metrics Banner */}
        <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-[11px] font-bold uppercase tracking-wider text-gray-500">Expected Baseline</span>
            <div className="text-2xl font-black text-gray-900 mt-1">{count.expectedItemCount}</div>
            <span className="text-[11px] text-gray-400">Frozen at session start</span>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-[11px] font-bold uppercase tracking-wider text-emerald-600">Physical Counted</span>
            <div className="text-2xl font-black text-emerald-700 mt-1">{count.countedExpectedItemCount}</div>
            <span className="text-[11px] text-gray-400">Matched baseline items</span>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-[11px] font-bold uppercase tracking-wider text-[#B89A60]">Progress</span>
            <div className="text-2xl font-black text-gray-900 mt-1">{count.progressPercentage.toFixed(1)}%</div>
            <div className="w-full bg-gray-200 rounded-full h-1.5 mt-1.5 overflow-hidden">
              <div
                className="bg-[#B89A60] h-1.5 rounded-full transition-all duration-300"
                style={{ width: `${Math.min(100, count.progressPercentage)}%` }}
              ></div>
            </div>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-[11px] font-bold uppercase tracking-wider text-amber-600">Unexpected Items</span>
            <div className="text-2xl font-black text-amber-700 mt-1">{count.unexpectedCount}</div>
            <span className="text-[11px] text-gray-400">Different recorded location</span>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-[11px] font-bold uppercase tracking-wider text-gray-500">
              {count.status === 1 ? 'Pending Uncounted' : 'Missing Items'}
            </span>
            <div className={`text-2xl font-black mt-1 ${count.status === 1 ? 'text-gray-700' : 'text-red-600'}`}>
              {count.status === 1 ? uncountedBaselineCount : count.missingCount}
            </div>
            <span className="text-[11px] text-gray-400">
              {count.status === 1 ? 'Not yet scanned' : 'Declared missing'}
            </span>
          </div>
        </div>

        {/* ======================================================== */}
        {/* PHASE 1: IN_PROGRESS SCANNER WORKSPACE                   */}
        {/* ======================================================== */}
        {count.status === 1 && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Left 2 Cols: High-Speed Repetitive Scanner & Activity */}
            <div className="lg:col-span-2 space-y-6">
              {/* Scanner Form */}
              <div className="bg-white p-6 rounded-2xl shadow-sm border border-[#123B32]/30 bg-gradient-to-br from-white via-white to-emerald-50/20">
                <form onSubmit={handleScanSubmit} className="space-y-4">
                  <div className="flex items-center justify-between">
                    <label
                      htmlFor="barcodeInput"
                      className="block text-sm font-bold text-gray-900 tracking-tight flex items-center space-x-2"
                    >
                      
                      <span>Scan Barcode or Tag Number</span>
                    </label>
                    <span className="text-xs bg-emerald-100 text-emerald-800 font-bold px-2 py-0.5 rounded border border-emerald-300">
                      Scanner Ready
                    </span>
                  </div>

                  <div className="relative">
                    <input
                      ref={scannerInputRef}
                      id="barcodeInput"
                      type="text"
                      value={scanInput}
                      onChange={(e) => setScanInput(e.target.value)}
                      placeholder="Scan or enter the item barcode"
                      disabled={isScanning}
                      autoComplete="off"
                      className="w-full text-lg font-mono tracking-wider p-4 pl-12 pr-28 rounded-xl border-2 border-[#123B32] focus:outline-none focus:ring-4 focus:ring-[#123B32]/20 bg-white shadow-inner"
                    />
                    <span className="absolute left-4 top-1/2 -translate-y-1/2 text-2xl text-gray-400">
                      
                    </span>
                    <button
                      type="submit"
                      disabled={isScanning || !scanInput.trim()}
                      className="absolute right-2.5 top-1/2 -translate-y-1/2 px-5 py-2.5 bg-[#123B32] hover:bg-[#1B6543] text-white text-xs font-bold rounded-lg shadow transition disabled:opacity-50"
                    >
                      {isScanning ? 'Scanning...' : 'Scan / Enter'}
                    </button>
                  </div>

                  <p className="text-xs text-gray-500">
                    <span className="font-semibold">Hardware Scanner Support:</span> Handheld USB/Bluetooth barcode scanners behaving as keyboard input submit automatically. The field refocuses after every scan.
                  </p>
                </form>

                {/* Scan Feedback Banner */}
                {scanFeedback && (
                  <div
                    className={`mt-4 p-4 rounded-xl border flex items-start space-x-3 text-sm transition-all animate-fadeIn ${
                      scanFeedback.scanOutcome === 'MATCHED'
                        ? 'bg-emerald-50 border-emerald-300 text-emerald-900'
                        : scanFeedback.scanOutcome === 'UNEXPECTED'
                        ? 'bg-amber-50 border-amber-300 text-amber-900'
                        : scanFeedback.scanOutcome === 'ALREADY_COUNTED'
                        ? 'bg-blue-50 border-blue-300 text-blue-900'
                        : scanFeedback.scanOutcome === 'COMPOSITION_PARENT'
                        ? 'bg-purple-50 border-purple-300 text-purple-900'
                        : 'bg-red-50 border-red-300 text-red-900'
                    }`}
                  >
                    <span className="text-2xl flex-shrink-0">
                      {scanFeedback.scanOutcome === 'MATCHED'
                        ? 'Match'
                        : scanFeedback.scanOutcome === 'UNEXPECTED'
                        ? 'Review'
                        : scanFeedback.scanOutcome === 'ALREADY_COUNTED'
                        ? 'Reconciled'
                        : scanFeedback.scanOutcome === 'COMPOSITION_PARENT'
                        ? 'Counted'
                        : 'Missing'}
                    </span>
                    <div className="flex-1">
                      <div className="font-bold tracking-tight uppercase text-xs">
                        {scanFeedback.scanOutcome.replace('_', ' ')}
                      </div>
                      <div className="mt-0.5">{scanFeedback.message}</div>
                    </div>
                  </div>
                )}
              </div>

              {/* Baseline Summary Table Preview */}
              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
                <div className="flex items-center justify-between pb-3 border-b">
                  <h3 className="text-sm font-bold text-gray-900">
                    Expected Physical Stock Snapshot ({lines.length} items in baseline)
                  </h3>
                  <span className="text-xs text-gray-500">
                    {count.countedExpectedItemCount} of {count.expectedItemCount} items physically counted
                  </span>
                </div>

                <div className="max-h-80 overflow-y-auto mt-3">
                  <table className="min-w-full text-xs text-left divide-y divide-gray-100">
                    <thead className="bg-gray-50 text-gray-500 uppercase sticky top-0">
                      <tr>
                        <th className="px-3 py-2">Status</th>
                        <th className="px-3 py-2">Tag & Barcode</th>
                        <th className="px-3 py-2">Item Name</th>
                        <th className="px-3 py-2">Category</th>
                        <th className="px-3 py-2 text-right">Gross Wt.</th>
                        <th className="px-3 py-2 text-right">Counted At</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {lines.map((l) => (
                        <tr
                          key={l.id}
                          className={l.isCounted ? 'bg-emerald-50/40 font-medium' : 'hover:bg-gray-50'}
                        >
                          <td className="px-3 py-2 whitespace-nowrap">
                            {l.isCounted ? (
                              <span className="text-[10px] font-bold px-2 py-0.5 rounded bg-emerald-100 text-emerald-800">
                                Counted
                              </span>
                            ) : (
                              <span className="text-[10px] font-semibold px-2 py-0.5 rounded bg-gray-100 text-gray-600">
                                Pending
                              </span>
                            )}
                          </td>
                          <td className="px-3 py-2 whitespace-nowrap font-mono font-semibold">
                            {l.tagNumberSnapshot}
                            <span className="block text-[10px] text-gray-400 font-sans">{l.barcodeSnapshot}</span>
                          </td>
                          <td className="px-3 py-2 font-medium text-gray-800">{l.itemNameSnapshot}</td>
                          <td className="px-3 py-2 text-gray-500">{l.categoryNameSnapshot}</td>
                          <td className="px-3 py-2 text-right font-mono">{l.grossWeightSnapshot.toFixed(3)}g</td>
                          <td className="px-3 py-2 text-right text-gray-400">
                            {l.countedAt ? new Date(l.countedAt).toLocaleTimeString('en-GB') : '—'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

            {/* Right Col: Recent Scans Stream & Unexpected Items */}
            <div className="space-y-6">
              {/* Recent Activity Stream */}
              <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
                <h3 className="text-sm font-bold text-gray-900 pb-2 border-b flex items-center justify-between">
                  <span>Recent Scan Activity</span>
                  <span className="text-xs text-gray-400">{recentScans.length} scans</span>
                </h3>

                <div className="mt-3 space-y-2 max-h-80 overflow-y-auto">
                  {recentScans.length === 0 ? (
                    <div className="text-center py-8 text-xs text-gray-400">
                      No scans yet in this session. Start scanning items.
                    </div>
                  ) : (
                    recentScans.map((scan) => (
                      <div
                        key={scan.id}
                        className="p-2.5 rounded-lg border text-xs flex items-center justify-between bg-gray-50 border-gray-100"
                      >
                        <div className="flex items-center space-x-2">
                          <span className="text-sm">
                            {scan.outcome === 'MATCHED'
                              ? 'Match'
                              : scan.outcome === 'UNEXPECTED'
                              ? 'Review'
                              : scan.outcome === 'ALREADY_COUNTED'
                              ? 'Reconciled'
                              : 'Missing'}
                          </span>
                          <div>
                            <div className="font-semibold text-gray-900 font-mono">
                              {scan.tagNumber || scan.outcome}
                            </div>
                            <div className="text-[11px] text-gray-500 truncate max-w-[180px]">
                              {scan.itemName || scan.message}
                            </div>
                          </div>
                        </div>
                        <span className="text-[10px] text-gray-400 font-mono">
                          {scan.timestamp.toLocaleTimeString('en-GB')}
                        </span>
                      </div>
                    ))
                  )}
                </div>
              </div>

              {/* Unexpected Items Observed so far */}
              {unexpectedItems.length > 0 && (
                <div className="bg-amber-50 rounded-xl shadow-sm border border-amber-200 p-4">
                  <h3 className="text-sm font-bold text-amber-900 pb-2 border-b border-amber-200 flex items-center justify-between">
                    <span>Unexpected Items ({unexpectedItems.length})</span>
                  </h3>
                  <p className="text-xs text-amber-700 mt-1 mb-3">
                    These items belong to this branch but are currently assigned to a different physical location in the system.
                  </p>
                  <div className="space-y-2 max-h-48 overflow-y-auto">
                    {unexpectedItems.map((u) => (
                      <div
                        key={u.id}
                        className="p-2 bg-white rounded border border-amber-200 text-xs flex items-center justify-between"
                      >
                        <div>
                          <div className="font-bold text-gray-900 font-mono">{u.tagNumberSnapshot}</div>
                          <div className="text-[11px] text-gray-600">{u.itemNameSnapshot}</div>
                          <div className="text-[10px] text-amber-800">
                            Recorded Location: {u.currentSystemLocationNameSnapshot}
                          </div>
                        </div>
                        <span className="text-[10px] text-gray-400">
                          {new Date(u.scannedAt).toLocaleTimeString('en-GB')}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* ======================================================== */}
        {/* PHASE 2 & 3: VARIANCE REVIEW & HISTORICAL DASHBOARD      */}
        {/* ======================================================== */}
        {(count.status === 2 || count.status === 3) && variances && (
          <div className="space-y-6">
            {/* Variance Header Alert */}
            {count.status === 2 && (
              <div className="p-4 bg-amber-50 border border-amber-300 rounded-xl text-amber-900 flex items-start space-x-3">
                <span className="text-2xl"></span>
                <div className="flex-1">
                  <h3 className="font-bold text-sm text-amber-950">
                    Stock Variances Detected — Investigation & Review Required
                  </h3>
                  <p className="text-xs text-amber-800 mt-0.5">
                    This inventory count requires review. Reconciled at {new Date(count.reconciledAt!).toLocaleString('en-GB')} by {count.reconciledByName || 'Staff'}.
                  </p>
                  <p className="text-xs text-amber-900 font-semibold mt-1">
                    Rule: Every Missing and Unexpected variance must be formally investigated and resolved before this count can be marked Completed.
                  </p>
                </div>
              </div>
            )}

            {count.status === 3 && (
              <div className="p-4 bg-emerald-50 border border-emerald-300 rounded-xl text-emerald-900 flex items-start space-x-3">
                <span className="text-2xl">✓</span>
                <div className="flex-1">
                  <h3 className="font-bold text-sm text-emerald-950">
                    Stock Verification Completed & Permanently Archived
                  </h3>
                  <p className="text-xs text-emerald-800 mt-0.5">
                    Completed at {new Date(count.completedAt!).toLocaleString('en-GB')} by {count.completedByName || 'Admin'}. All baseline snapshots and investigation findings are permanently preserved.
                  </p>
                </div>
              </div>
            )}

            {/* Variance Tabs */}
            <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
              <div className="flex border-b border-gray-200 bg-gray-50">
                <button
                  onClick={() => setVarianceTab('missing')}
                  className={`px-6 py-3 text-xs font-bold uppercase tracking-wider transition border-b-2 flex items-center space-x-2 ${
                    varianceTab === 'missing'
                      ? 'border-[#123B32] text-[#123B32] bg-white'
                      : 'border-transparent text-gray-500 hover:text-gray-700'
                  }`}
                >
                  <span>Missing Items</span>
                  <span
                    className={`px-2 py-0.5 rounded-full text-[10px] ${
                      unresolvedMissing > 0 ? 'bg-red-100 text-red-800' : 'bg-gray-100 text-gray-600'
                    }`}
                  >
                    {variances.missingItems.length} ({unresolvedMissing} unresolved)
                  </span>
                </button>

                <button
                  onClick={() => setVarianceTab('unexpected')}
                  className={`px-6 py-3 text-xs font-bold uppercase tracking-wider transition border-b-2 flex items-center space-x-2 ${
                    varianceTab === 'unexpected'
                      ? 'border-[#123B32] text-[#123B32] bg-white'
                      : 'border-transparent text-gray-500 hover:text-gray-700'
                  }`}
                >
                  <span>Unexpected Items</span>
                  <span
                    className={`px-2 py-0.5 rounded-full text-[10px] ${
                      unresolvedUnexpected > 0 ? 'bg-amber-100 text-amber-800' : 'bg-gray-100 text-gray-600'
                    }`}
                  >
                    {variances.unexpectedItems.length} ({unresolvedUnexpected} unresolved)
                  </span>
                </button>
              </div>

              {/* Tab Content */}
              <div className="p-4">
                {varianceTab === 'missing' && (
                  <div className="overflow-x-auto">
                    <table className="min-w-full text-xs text-left divide-y divide-gray-200">
                      <thead className="bg-gray-50 text-gray-500 font-semibold uppercase text-[11px]">
                        <tr>
                          <th className="px-4 py-3">Tag & Barcode</th>
                          <th className="px-4 py-3">Item Details</th>
                          <th className="px-4 py-3">Category / Purity</th>
                          <th className="px-4 py-3 text-right">Gross Weight</th>
                          <th className="px-4 py-3">Expected Status</th>
                          <th className="px-4 py-3">Resolution State</th>
                          <th className="px-4 py-3">Resolution Notes</th>
                          <th className="px-4 py-3 text-right">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 bg-white">
                        {variances.missingItems.length === 0 ? (
                          <tr>
                            <td colSpan={8} className="px-4 py-8 text-center text-gray-400 font-medium">
                              No missing items recorded. All expected baseline stock was physically accounted for!
                            </td>
                          </tr>
                        ) : (
                          variances.missingItems.map((item) => (
                            <tr key={item.id} className="hover:bg-gray-50">
                              <td className="px-4 py-3 font-mono font-bold text-gray-900 whitespace-nowrap">
                                {item.tagNumberSnapshot}
                                <span className="block text-[10px] text-gray-400 font-sans">{item.barcodeSnapshot}</span>
                              </td>
                              <td className="px-4 py-3 font-semibold text-gray-900">{item.itemNameSnapshot}</td>
                              <td className="px-4 py-3 text-gray-600">
                                {item.categoryNameSnapshot} • {item.purityNameSnapshot}
                              </td>
                              <td className="px-4 py-3 text-right font-mono font-medium">
                                {item.grossWeightSnapshot.toFixed(3)}g
                              </td>
                              <td className="px-4 py-3 whitespace-nowrap">
                                <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-gray-100 text-gray-700">
                                  {item.expectedStatusSnapshot}
                                </span>
                              </td>
                              <td className="px-4 py-3 whitespace-nowrap">
                                {item.resolutionStatus === 1 ? (
                                  <span className="px-2.5 py-1 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800">
                                    Resolved ({item.resolutionReasonCode})
                                  </span>
                                ) : (
                                  <span className="px-2.5 py-1 rounded-full text-[10px] font-bold bg-red-100 text-red-800">
                                    Unresolved
                                  </span>
                                )}
                              </td>
                              <td className="px-4 py-3 text-gray-600 max-w-xs truncate">
                                {item.resolutionNotes || '—'}
                              </td>
                              <td className="px-4 py-3 text-right whitespace-nowrap">
                                {count.status === 2 && canResolveVariance && (
                                  <button
                                    onClick={() => {
                                      setResolvingVariance({
                                        type: 'Missing',
                                        id: item.id,
                                        tagNumber: item.tagNumberSnapshot,
                                        itemName: item.itemNameSnapshot,
                                      });
                                      setReasonCode('ITEM_FOUND');
                                      setResolutionNotes(item.resolutionNotes || '');
                                    }}
                                    className="px-2.5 py-1 text-xs font-bold rounded text-white bg-[#123B32] hover:bg-[#1B6543] transition"
                                  >
                                    {item.resolutionStatus === 1 ? 'Edit Resolution' : 'Resolve'}
                                  </button>
                                )}
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>
                )}

                {varianceTab === 'unexpected' && (
                  <div className="overflow-x-auto">
                    <table className="min-w-full text-xs text-left divide-y divide-gray-200">
                      <thead className="bg-gray-50 text-gray-500 font-semibold uppercase text-[11px]">
                        <tr>
                          <th className="px-4 py-3">Tag & Barcode</th>
                          <th className="px-4 py-3">Item Details</th>
                          <th className="px-4 py-3">Recorded Location</th>
                          <th className="px-4 py-3 text-right">Gross Weight</th>
                          <th className="px-4 py-3">System Status</th>
                          <th className="px-4 py-3">Resolution State</th>
                          <th className="px-4 py-3">Resolution Notes</th>
                          <th className="px-4 py-3 text-right">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100 bg-white">
                        {variances.unexpectedItems.length === 0 ? (
                          <tr>
                            <td colSpan={8} className="px-4 py-8 text-center text-gray-400 font-medium">
                              No unexpected items found. No items from other locations were scanned here.
                            </td>
                          </tr>
                        ) : (
                          variances.unexpectedItems.map((item) => (
                            <tr key={item.id} className="hover:bg-gray-50">
                              <td className="px-4 py-3 font-mono font-bold text-gray-900 whitespace-nowrap">
                                {item.tagNumberSnapshot}
                                <span className="block text-[10px] text-gray-400 font-sans">{item.barcodeSnapshot}</span>
                              </td>
                              <td className="px-4 py-3 font-semibold text-gray-900">{item.itemNameSnapshot}</td>
                              <td className="px-4 py-3 text-amber-800 font-semibold">
                                {item.currentSystemLocationNameSnapshot}
                              </td>
                              <td className="px-4 py-3 text-right font-mono font-medium">
                                {item.grossWeightSnapshot.toFixed(3)}g
                              </td>
                              <td className="px-4 py-3 whitespace-nowrap">
                                <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-gray-100 text-gray-700">
                                  {item.currentSystemStatusSnapshot}
                                </span>
                              </td>
                              <td className="px-4 py-3 whitespace-nowrap">
                                {item.resolutionStatus === 1 ? (
                                  <span className="px-2.5 py-1 rounded-full text-[10px] font-bold bg-emerald-100 text-emerald-800">
                                    Resolved ({item.resolutionReasonCode})
                                  </span>
                                ) : (
                                  <span className="px-2.5 py-1 rounded-full text-[10px] font-bold bg-amber-100 text-amber-800">
                                    Unresolved
                                  </span>
                                )}
                              </td>
                              <td className="px-4 py-3 text-gray-600 max-w-xs truncate">
                                {item.resolutionNotes || '—'}
                              </td>
                              <td className="px-4 py-3 text-right whitespace-nowrap">
                                {count.status === 2 && canResolveVariance && (
                                  <button
                                    onClick={() => {
                                      setResolvingVariance({
                                        type: 'Unexpected',
                                        id: item.id,
                                        tagNumber: item.tagNumberSnapshot,
                                        itemName: item.itemNameSnapshot,
                                      });
                                      setReasonCode('WRONG_RECORDED_LOCATION');
                                      setResolutionNotes(item.resolutionNotes || '');
                                    }}
                                    className="px-2.5 py-1 text-xs font-bold rounded text-white bg-[#123B32] hover:bg-[#1B6543] transition"
                                  >
                                    {item.resolutionStatus === 1 ? 'Edit Resolution' : 'Resolve'}
                                  </button>
                                )}
                              </td>
                            </tr>
                          ))
                        )}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}
      </div>

      {/* ======================================================== */}
      {/* MODAL 1: COMPOSITION PARENT SCAN CONFIRMATION            */}
      {/* ======================================================== */}
      {compositionPreview && (
        <div className="fixed inset-0 z-50 overflow-y-auto bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-gray-200 max-w-xl w-full p-6 space-y-6">
            <div className="border-b pb-4">
              <div className="flex items-center space-x-2">
                
                <div>
                  <h3 className="text-lg font-bold text-gray-900">
                    {compositionPreview.type}: {compositionPreview.name}
                  </h3>
                  <p className="text-xs text-gray-500 font-mono">
                    Parent Tag: {compositionPreview.tagNumber} | Barcode: {compositionPreview.barcode}
                  </p>
                </div>
              </div>
            </div>

            {/* Core Invariant Callout */}
            <div className="p-3.5 bg-purple-50 border border-purple-200 rounded-xl text-xs text-purple-900 space-y-1">
              <div className="font-bold flex items-center">
                 Core Accounting Invariant
              </div>
              <p>
                The parent composition itself is an organizational/selling grouping and is{' '}
                <span className="font-bold underline">NOT</span> counted as an additional stock unit.
              </p>
              <p className="text-[11px] text-purple-700">
                Please verify the physical presence of each individual member item listed below.
              </p>
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between text-xs font-semibold text-gray-600">
                <span>Select physically present members ({selectedMemberIds.length} of {compositionPreview.members.length} selected):</span>
                <button
                  type="button"
                  onClick={() => {
                    if (selectedMemberIds.length === compositionPreview.members.length) {
                      setSelectedMemberIds([]);
                    } else {
                      setSelectedMemberIds(compositionPreview.members.map((m) => m.inventoryItemId));
                    }
                  }}
                  className="text-[#123B32] font-bold underline"
                >
                  {selectedMemberIds.length === compositionPreview.members.length ? 'Deselect All' : 'Select All'}
                </button>
              </div>

              <div className="divide-y divide-gray-100 max-h-60 overflow-y-auto border rounded-xl p-2 bg-gray-50">
                {compositionPreview.members.map((member) => (
                  <label
                    key={member.inventoryItemId}
                    className="flex items-center justify-between p-2.5 hover:bg-white rounded-lg cursor-pointer transition"
                  >
                    <div className="flex items-center space-x-3">
                      <input
                        type="checkbox"
                        checked={selectedMemberIds.includes(member.inventoryItemId)}
                        onChange={(e) => {
                          if (e.target.checked) {
                            setSelectedMemberIds((prev) => [...prev, member.inventoryItemId]);
                          } else {
                            setSelectedMemberIds((prev) => prev.filter((id) => id !== member.inventoryItemId));
                          }
                        }}
                        className="rounded border-gray-300 text-[#123B32] focus:ring-[#123B32] w-4 h-4"
                      />
                      <div>
                        <div className="font-bold text-gray-900 font-mono text-xs">{member.tagNumber}</div>
                        <div className="text-[11px] text-gray-600">{member.itemName} ({member.categoryName})</div>
                      </div>
                    </div>
                    <div className="text-right text-xs font-mono">
                      <span>{member.grossWeight.toFixed(3)}g</span>
                      {member.isAlreadyCounted && (
                        <span className="block text-[10px] text-emerald-600 font-semibold font-sans">
                          Already counted
                        </span>
                      )}
                    </div>
                  </label>
                ))}
              </div>
            </div>

            <div className="flex items-center justify-end space-x-3 pt-4 border-t">
              <button
                type="button"
                onClick={() => setCompositionPreview(null)}
                className="px-4 py-2 text-xs font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleConfirmComposition}
                disabled={isConfirmingComposition || selectedMemberIds.length === 0}
                className="px-5 py-2 text-xs font-bold text-white bg-[#123B32] hover:bg-[#1B6543] rounded-lg shadow disabled:opacity-50 transition"
              >
                {isConfirmingComposition
                  ? 'Confirming...'
                  : `Confirm ${selectedMemberIds.length} Selected Member(s)`}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ======================================================== */}
      {/* MODAL 2: FINISH COUNT CONFIRMATION                       */}
      {/* ======================================================== */}
      {isFinishModalOpen && (
        <div className="fixed inset-0 z-50 overflow-y-auto bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-gray-200 max-w-md w-full p-6 space-y-5">
            <div className="flex items-center space-x-3 border-b pb-4">
              
              <div>
                <h3 className="text-lg font-bold text-gray-900">Finish & Reconcile Count</h3>
                <p className="text-xs text-gray-500">Stock Location: {count.stockLocationName}</p>
              </div>
            </div>

            <div className="space-y-3 bg-gray-50 p-4 rounded-xl border border-gray-200 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-600">Expected Baseline Items:</span>
                <span className="font-bold text-gray-900">{count.expectedItemCount}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-emerald-700 font-medium">Physically Counted & Matched:</span>
                <span className="font-bold text-emerald-700">{count.countedExpectedItemCount}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-red-600 font-medium">Uncounted Items (Will be marked MISSING):</span>
                <span className="font-bold text-red-600">{uncountedBaselineCount}</span>
              </div>
              <div className="flex justify-between border-t pt-2">
                <span className="text-amber-700 font-medium">Unexpected Items Found:</span>
                <span className="font-bold text-amber-700">{count.unexpectedCount}</span>
              </div>
            </div>

            {uncountedBaselineCount === 0 && count.unexpectedCount === 0 ? (
              <div className="p-3 bg-emerald-50 border border-emerald-200 rounded-lg text-emerald-800 text-xs">
                <span className="font-bold">Stock Perfectly Matches</span> All expected items were physically accounted for with zero unexpected observations. This session will be automatically marked COMPLETED.
              </div>
            ) : (
              <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-amber-800 text-xs">
                <span className="font-bold">Variances Detected:</span> Finishing will transition this session to REQUIRES_REVIEW for formal investigation before final completion.
              </div>
            )}

            <div className="flex items-center justify-end space-x-3 pt-3 border-t">
              <button
                type="button"
                onClick={() => setIsFinishModalOpen(false)}
                className="px-4 py-2 text-xs font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition"
              >
                Keep Counting
              </button>
              <button
                type="button"
                onClick={handleFinishCount}
                disabled={isFinishing}
                className="px-5 py-2 text-xs font-bold text-white bg-emerald-700 hover:bg-emerald-800 rounded-lg shadow disabled:opacity-50 transition"
              >
                {isFinishing ? 'Reconciling...' : 'Confirm & Reconcile'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ======================================================== */}
      {/* MODAL 3: RESOLVE VARIANCE                                */}
      {/* ======================================================== */}
      {resolvingVariance && (
        <div className="fixed inset-0 z-50 overflow-y-auto bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-gray-200 max-w-lg w-full p-6 space-y-5">
            <div className="flex items-center justify-between border-b pb-4">
              <div className="flex items-center space-x-2">
                <span className="text-2xl">{resolvingVariance.type === 'Missing' ? '' : ''}</span>
                <div>
                  <h3 className="text-lg font-bold text-gray-900">
                    Resolve {resolvingVariance.type} Variance
                  </h3>
                  <p className="text-xs text-gray-500 font-mono">
                    {resolvingVariance.tagNumber} - {resolvingVariance.itemName}
                  </p>
                </div>
              </div>
              <button
                onClick={() => setResolvingVariance(null)}
                className="text-gray-400 hover:text-gray-600 text-xl font-bold"
              >
                ✕
              </button>
            </div>

            {resolveError && (
              <div className="p-3 bg-red-50 border border-red-200 rounded-lg text-red-700 text-xs font-semibold">
                {resolveError}
              </div>
            )}

            <form onSubmit={handleResolveVarianceSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-gray-600 mb-1">
                  Investigation Reason Code <span className="text-red-500">*</span>
                </label>
                <select
                  value={reasonCode}
                  onChange={(e) => setReasonCode(e.target.value)}
                  className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-[#123B32] focus:ring-[#123B32] bg-gray-50 p-2.5 border"
                  required
                >
                  <option value="ITEM_FOUND">ITEM_FOUND — Item located physically during investigation</option>
                  <option value="WRONG_RECORDED_LOCATION">WRONG_RECORDED_LOCATION — Physical location mismatch in system</option>
                  <option value="CONFIRMED_MISSING">CONFIRMED_MISSING — Stock shortage confirmed after full audit</option>
                  <option value="INVENTORY_RECORD_CORRECTION">INVENTORY_RECORD_CORRECTION — Human data entry clerical error</option>
                  <option value="OTHER">OTHER — Special case (mandatory explanatory notes)</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-gray-600 mb-1">
                  Investigation & Resolution Notes {reasonCode === 'OTHER' && <span className="text-red-500">*</span>}
                </label>
                <textarea
                  value={resolutionNotes}
                  onChange={(e) => setResolutionNotes(e.target.value)}
                  rows={3}
                  placeholder="Explain why the physical stock differs from the system record"
                  className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-[#123B32] focus:ring-[#123B32] bg-gray-50 p-2.5 border"
                  required={reasonCode === 'OTHER'}
                />
              </div>

              {/* No Auto Adjustment Guarantee Callout */}
              <div className="p-3 bg-blue-50 border border-blue-200 rounded-lg text-xs text-blue-900 space-y-1">
                <div className="font-bold flex items-center">
                   Investigation Outcome Guarantee
                </div>
                <p>
                  Resolving this variance records an authoritative audit finding. It will{' '}
                  <span className="font-bold underline">never silently modify</span> the live inventory item&apos;s location, branch, status, or weight.
                </p>
              </div>

              <div className="flex items-center justify-end space-x-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setResolvingVariance(null)}
                  className="px-4 py-2 text-xs font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={isResolving}
                  className="px-5 py-2 text-xs font-bold text-white bg-[#123B32] hover:bg-[#1B6543] rounded-lg shadow disabled:opacity-50 transition"
                >
                  {isResolving ? 'Saving Resolution...' : 'Save Resolution'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ======================================================== */}
      {/* MODAL 4: COMPLETE COUNT CONFIRMATION                     */}
      {/* ======================================================== */}
      {isCompleteModalOpen && (
        <div className="fixed inset-0 z-50 overflow-y-auto bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-gray-200 max-w-md w-full p-6 space-y-5">
            <div className="flex items-center space-x-3 border-b pb-4">
              
              <div>
                <h3 className="text-lg font-bold text-gray-900">Complete Inventory Count</h3>
                <p className="text-xs text-gray-500">Session #{count.id.substring(0, 8).toUpperCase()}</p>
              </div>
            </div>

            <p className="text-xs text-gray-600">
              All <span className="font-bold text-gray-900">{totalVariances}</span> detected stock variances have been investigated and resolved. Completing this session archives it permanently as an immutable audit record.
            </p>

            <div>
              <label className="block text-xs font-semibold uppercase tracking-wider text-gray-600 mb-1">
                Final Completion Notes (Optional)
              </label>
              <textarea
                value={completionNotes}
                onChange={(e) => setCompletionNotes(e.target.value)}
                rows={2}
                placeholder="Record observations from the physical stock count"
                className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-[#123B32] focus:ring-[#123B32] bg-gray-50 p-2.5 border"
              />
            </div>

            <div className="flex items-center justify-end space-x-3 pt-3 border-t">
              <button
                type="button"
                onClick={() => setIsCompleteModalOpen(false)}
                className="px-4 py-2 text-xs font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition"
              >
                Back
              </button>
              <button
                type="button"
                onClick={handleCompleteCount}
                disabled={isCompleting}
                className="px-5 py-2 text-xs font-bold text-white bg-[#123B32] hover:bg-[#1B6543] rounded-lg shadow disabled:opacity-50 transition"
              >
                {isCompleting ? 'Completing...' : 'Complete & Archive Session'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ======================================================== */}
      {/* MODAL 5: CANCEL COUNT                                    */}
      {/* ======================================================== */}
      {isCancelModalOpen && (
        <div className="fixed inset-0 z-50 overflow-y-auto bg-black/60 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl shadow-2xl border border-gray-200 max-w-md w-full p-6 space-y-5">
            <div className="flex items-center space-x-3 border-b pb-4">
              
              <div>
                <h3 className="text-lg font-bold text-gray-900">Cancel Inventory Count</h3>
                <p className="text-xs text-gray-500">Session #{count.id.substring(0, 8).toUpperCase()}</p>
              </div>
            </div>

            <p className="text-xs text-gray-600">
              Cancelling will release the stock location lock. The session and all captured count lines will be permanently retained in CANCELLED status for audit history.
            </p>

            <form onSubmit={handleCancelCount} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold uppercase tracking-wider text-gray-600 mb-1">
                  Cancellation Reason <span className="text-red-500">*</span>
                </label>
                <textarea
                  value={cancellationReason}
                  onChange={(e) => setCancellationReason(e.target.value)}
                  rows={2}
                  placeholder="Explain why this count session is being cancelled"
                  className="w-full text-sm border-gray-300 rounded-lg shadow-sm focus:border-red-500 focus:ring-red-500 bg-gray-50 p-2.5 border"
                  required
                />
              </div>

              <div className="flex items-center justify-end space-x-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setIsCancelModalOpen(false)}
                  className="px-4 py-2 text-xs font-semibold text-gray-700 bg-gray-100 hover:bg-gray-200 rounded-lg transition"
                >
                  Dismiss
                </button>
                <button
                  type="submit"
                  disabled={isCancelling || !cancellationReason.trim()}
                  className="px-5 py-2 text-xs font-bold text-white bg-red-700 hover:bg-red-800 rounded-lg shadow disabled:opacity-50 transition"
                >
                  {isCancelling ? 'Cancelling...' : 'Confirm Cancellation'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </AppLayout>
  );
}
