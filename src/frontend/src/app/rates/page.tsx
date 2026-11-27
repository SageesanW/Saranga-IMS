'use client';

import React, { useEffect, useState, useMemo } from 'react';
import AppLayout from '@/components/AppLayout';
import {
  metalRateApi,
  pricingApi,
  branchApi,
  materialApi,
  purityApi,
  MetalRate,
  ExternalRateProviderStatus,
  ReferencePriceBreakdown,
  MetalRateFeedPreview,
  Branch,
  Material,
  Purity,
} from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';

export default function RatesManagementPage() {
  const { user } = useAuth();
  const { activeBranchId, setActiveBranch } = useBranch();

  const [branches, setBranches] = useState<Branch[]>([]);
  const [materials, setMaterials] = useState<Material[]>([]);
  const [purities, setPurities] = useState<Purity[]>([]);
  const [rates, setRates] = useState<MetalRate[]>([]);
  const [providerStatus, setProviderStatus] = useState<ExternalRateProviderStatus | null>(null);

  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Source Switching Modal
  const [isSwitchModalOpen, setIsSwitchModalOpen] = useState(false);
  const [targetSwitchMode, setTargetSwitchMode] = useState<'MARKET_API' | 'CBSL_API' | 'MANUAL' | null>(null);
  const [feedPreview, setFeedPreview] = useState<MetalRateFeedPreview | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [switchReason, setSwitchReason] = useState('');
  const [switchSubmitting, setSwitchSubmitting] = useState(false);

  // Manual Rate Setup during Switch to Manual (if missing historical rates)
  const [missingPuritiesToConfigure, setMissingPuritiesToConfigure] = useState<Purity[]>([]);
  const [initialManualInputs, setInitialManualInputs] = useState<Record<string, string>>({});

  // Modals
  const [isManualModalOpen, setIsManualModalOpen] = useState(false);
  const [isOverrideModalOpen, setIsOverrideModalOpen] = useState(false);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState(false);
  const [isCalcModalOpen, setIsCalcModalOpen] = useState(false);

  // Manual Rate Form
  const [manualMaterialId, setManualMaterialId] = useState('');
  const [manualPurityId, setManualPurityId] = useState('');
  const [manualRate, setManualRate] = useState('');
  const [manualNotes, setManualNotes] = useState('');
  const [manualSubmitting, setManualSubmitting] = useState(false);

  // Override Form
  const [overrideRateTarget, setOverrideRateTarget] = useState<MetalRate | null>(null);
  const [overrideRateValue, setOverrideRateValue] = useState('');
  const [overrideReason, setOverrideReason] = useState('');
  const [overrideSubmitting, setOverrideSubmitting] = useState(false);

  // History State
  const [historyItems, setHistoryItems] = useState<MetalRate[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyFilterMaterial, setHistoryFilterMaterial] = useState('');
  const [historyFilterPurity, setHistoryFilterPurity] = useState('');

  // Calculator State
  const [calcMaterialId, setCalcMaterialId] = useState('');
  const [calcPurityId, setCalcPurityId] = useState('');
  const [calcNetWeight, setCalcNetWeight] = useState('10.0000');
  const [calcPieceCount, setCalcPieceCount] = useState('1');
  const [calcMakingMethod, setCalcMakingMethod] = useState('1'); // 1 = PER_GRAM, 2 = PER_ITEM
  const [calcMakingValue, setCalcMakingValue] = useState('1500');
  const [calcResult, setCalcResult] = useState<ReferencePriceBreakdown | null>(null);
  const [calcLoading, setCalcLoading] = useState(false);

  const canManageRates = user?.roles.some((r) => r === 'Super Admin' || r === 'Owner' || r === 'Branch Manager') || user?.isGlobalAdmin;

  const currentBranch = useMemo(() => {
    return branches.find((b) => b.id === activeBranchId) || branches[0] || null;
  }, [branches, activeBranchId]);

  const currencyCode = currentBranch?.currencyCode || 'LKR';

  const activeMode = useMemo(() => {
    return providerStatus?.activePricingMode || currentBranch?.ratePricingMode || 'MANUAL';
  }, [providerStatus?.activePricingMode, currentBranch?.ratePricingMode]);

  // Load initial branch and master data
  useEffect(() => {
    async function loadMasters() {
      try {
        const [bRes, mRes, pRes] = await Promise.all([
          branchApi.getBranches(),
          materialApi.getMaterials(true),
          purityApi.getPurities(true),
        ]);
        setBranches(bRes);
        setMaterials(mRes.filter((m: Material) => m.isActive));
        setPurities(pRes.filter((p: Purity) => p.isActive));

        if (!activeBranchId && bRes.length > 0) {
          setActiveBranch(bRes[0].id);
        }
      } catch (err: any) {
        setError(err.message || 'Failed to load master metadata');
      }
    }
    loadMasters();
  }, []);

  // Fetch active rates and provider status whenever branch changes
  useEffect(() => {
    if (activeBranchId) {
      loadRatesAndStatus(activeBranchId);
    }
  }, [activeBranchId]);

  async function loadRatesAndStatus(branchId: string) {
    setLoading(true);
    setError(null);
    try {
      const [ratesRes, statusRes] = await Promise.all([
        metalRateApi.getActiveRates(branchId),
        metalRateApi.getProviderStatus(branchId),
      ]);
      setRates(ratesRes);
      setProviderStatus(statusRes);
    } catch (err: any) {
      setError(err.message || 'Failed to load active rates');
    } finally {
      setLoading(false);
    }
  }

  async function handleRefreshExternal() {
    if (!activeBranchId) return;
    setRefreshing(true);
    setError(null);
    setSuccessMsg(null);
    try {
      const updatedRates = await metalRateApi.refreshExternalRates({ branchId: activeBranchId });
      setRates(updatedRates);
      const updatedStatus = await metalRateApi.getProviderStatus(activeBranchId);
      setProviderStatus(updatedStatus);
      setSuccessMsg('Rates updated successfully from external provider.');
    } catch (err: any) {
      setError(err.message || 'External refresh encountered an issue. Existing store rates retained.');
      const updatedStatus = await metalRateApi.getProviderStatus(activeBranchId);
      setProviderStatus(updatedStatus);
    } finally {
      setRefreshing(false);
    }
  }

  // Handle clicking one of the 3 Source Buttons
  async function handleInitiateSwitch(targetMode: 'MARKET_API' | 'CBSL_API' | 'MANUAL') {
    if (targetMode === activeMode) return;
    if (!activeBranchId) return;

    setTargetSwitchMode(targetMode);
    setSwitchReason('');
    setMissingPuritiesToConfigure([]);
    setInitialManualInputs({});
    setIsSwitchModalOpen(true);

    if (targetMode === 'MARKET_API' || targetMode === 'CBSL_API') {
      setPreviewLoading(true);
      try {
        const preview = await metalRateApi.getFeedPreview();
        setFeedPreview(preview);
      } catch (err: any) {
        setError(err.message || 'Failed to fetch live rate preview.');
      } finally {
        setPreviewLoading(false);
      }
    } else {
      setFeedPreview(null);
    }
  }

  // Confirm and Execute Source Switch
  async function handleConfirmSwitch() {
    if (!activeBranchId || !targetSwitchMode) return;

    setSwitchSubmitting(true);
    setError(null);
    setSuccessMsg(null);

    try {
      let initialManualPayload: { materialId: string; purityId: string; ratePerGram: number }[] | undefined = undefined;

      if (targetSwitchMode === 'MANUAL' && missingPuritiesToConfigure.length > 0) {
        const goldMat = materials.find((m) => m.code.includes('GOLD') || m.name.toLowerCase().includes('gold')) || materials[0];
        initialManualPayload = missingPuritiesToConfigure.map((p) => ({
          materialId: goldMat?.id || '',
          purityId: p.id,
          ratePerGram: parseFloat(initialManualInputs[p.id] || '0'),
        }));

        const invalid = initialManualPayload.find((x) => isNaN(x.ratePerGram) || x.ratePerGram <= 0);
        if (invalid) {
          setError('Please provide a valid positive rate per gram for all required purities.');
          setSwitchSubmitting(false);
          return;
        }
      }

      const updatedRates = await metalRateApi.switchPricingMode({
        branchId: activeBranchId,
        pricingMode: targetSwitchMode,
        reason: switchReason.trim() || undefined,
        initialManualRates: initialManualPayload,
      });

      setRates(updatedRates);
      const updatedStatus = await metalRateApi.getProviderStatus(activeBranchId);
      setProviderStatus(updatedStatus);

      // Update current branch in list
      setBranches((prev) =>
        prev.map((b) => (b.id === activeBranchId ? { ...b, ratePricingMode: targetSwitchMode } : b))
      );

      setIsSwitchModalOpen(false);
      setSuccessMsg(
        `Successfully switched operational pricing mode to ${
          targetSwitchMode === 'MARKET_API' ? 'Market Rate API' : targetSwitchMode === 'CBSL_API' ? 'CBSL Rate API' : 'Manual Rate'
        }.`
      );
    } catch (err: any) {
      const msg = err.message || 'Failed to switch pricing mode.';
      // Check if server indicated missing previous manual rates
      if (targetSwitchMode === 'MANUAL' && msg.includes('No previous manual rates found')) {
        // Collect missing purities so user can configure them in modal
        const goldPurities = purities.filter((p) => p.isActive);
        setMissingPuritiesToConfigure(goldPurities);
        setError('No previous manual rate exists. Please enter manual rates below to activate Manual mode.');
      } else {
        setError(msg);
      }
    } finally {
      setSwitchSubmitting(false);
    }
  }

  async function handleCreateManual(e: React.FormEvent) {
    e.preventDefault();
    if (!activeBranchId || !manualMaterialId || !manualPurityId || !manualRate) return;
    setManualSubmitting(true);
    setError(null);
    setSuccessMsg(null);
    try {
      await metalRateApi.createManualRate({
        branchId: activeBranchId,
        materialId: manualMaterialId,
        purityId: manualPurityId,
        ratePerGram: parseFloat(manualRate),
        currencyCode: currencyCode,
        notes: manualNotes,
      });
      setIsManualModalOpen(false);
      setManualRate('');
      setManualNotes('');
      setSuccessMsg('Manual rate set successfully.');
      await loadRatesAndStatus(activeBranchId);
    } catch (err: any) {
      setError(err.message || 'Failed to set manual rate.');
    } finally {
      setManualSubmitting(false);
    }
  }

  async function handleAdminOverride(e: React.FormEvent) {
    e.preventDefault();
    if (!activeBranchId || !overrideRateTarget || !overrideRateValue || !overrideReason.trim()) return;
    setOverrideSubmitting(true);
    setError(null);
    setSuccessMsg(null);
    try {
      await metalRateApi.adminOverrideRate({
        branchId: activeBranchId,
        materialId: overrideRateTarget.materialId,
        purityId: overrideRateTarget.purityId,
        overrideRatePerGram: parseFloat(overrideRateValue),
        overrideReason: overrideReason.trim(),
        currencyCode: currencyCode,
      });
      setIsOverrideModalOpen(false);
      setOverrideRateTarget(null);
      setOverrideRateValue('');
      setOverrideReason('');
      setSuccessMsg('Admin rate override recorded successfully.');
      await loadRatesAndStatus(activeBranchId);
    } catch (err: any) {
      setError(err.message || 'Failed to record admin override.');
    } finally {
      setOverrideSubmitting(false);
    }
  }

  async function openHistoryModal() {
    setIsHistoryModalOpen(true);
    if (!activeBranchId) return;
    setHistoryLoading(true);
    try {
      const res = await metalRateApi.getRateHistory({
        branchId: activeBranchId,
        materialId: historyFilterMaterial || undefined,
        purityId: historyFilterPurity || undefined,
        pageSize: 100,
      });
      setHistoryItems(res.items);
    } catch (err: any) {
      setError(err.message || 'Failed to load rate history.');
    } finally {
      setHistoryLoading(false);
    }
  }

  async function handleCalculateReference(e: React.FormEvent) {
    e.preventDefault();
    if (!activeBranchId || !calcMaterialId || !calcPurityId) return;
    setCalcLoading(true);
    setCalcResult(null);
    try {
      const res = await pricingApi.calculateReferencePrice({
        branchId: activeBranchId,
        materialId: calcMaterialId,
        purityId: calcPurityId,
        netWeight: parseFloat(calcNetWeight),
        pieceCount: parseInt(calcPieceCount, 10),
        makingChargeMethod: parseInt(calcMakingMethod, 10),
        makingChargeValue: parseFloat(calcMakingValue),
        currencyCode: currencyCode,
      });
      setCalcResult(res);
    } catch (err: any) {
      setError(err.message || 'Failed to calculate reference price.');
    } finally {
      setCalcLoading(false);
    }
  }

  function getSourceBadge(source: string, isOverride: boolean) {
    if (isOverride) {
      return (
        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-900 border border-amber-300">
          Admin Override
        </span>
      );
    }
    switch (source) {
      case 'MARKET_API':
      case 'EXTERNAL_API':
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-emerald-100 text-emerald-800 border border-emerald-300">
            Market Rate API
          </span>
        );
      case 'CBSL_API':
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-purple-100 text-purple-800 border border-purple-300">
            CBSL Rate API
          </span>
        );
      case 'MANUAL':
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-blue-100 text-blue-800 border border-blue-300">
            Manual Rate
          </span>
        );
      default:
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-800">
            {source}
          </span>
        );
    }
  }

  return (
    <AppLayout>
      <div className="space-y-6 max-w-7xl mx-auto">
        {/* Header Bar */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div className="flex items-center space-x-3">
              <h1 className="text-2xl font-black text-[#123B32] tracking-tight">Metal Rates Management</h1>
            </div>
            <p className="text-sm text-gray-500 mt-1">
              Authoritative daily pricing reference board for precious metals.
              <span className="text-xs text-amber-700 bg-amber-50 px-2 py-0.5 rounded ml-2 border border-amber-200">
                Note: Rates serve as valuation reference; final customer sale price is negotiated at POS.
              </span>
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            <button
              onClick={() => {
                if (materials.length > 0) setCalcMaterialId(materials[0].id);
                if (purities.length > 0) setCalcPurityId(purities[0].id);
                setIsCalcModalOpen(true);
              }}
              className="inline-flex items-center px-3 py-2 border border-emerald-700 text-emerald-800 bg-emerald-50 hover:bg-emerald-100 rounded-lg text-sm font-semibold transition"
            >
              Reference Calculator
            </button>
            <button
              onClick={openHistoryModal}
              className="inline-flex items-center px-3 py-2 border border-gray-300 text-gray-700 bg-white hover:bg-gray-50 rounded-lg text-sm font-semibold transition"
            >
              Rate History
            </button>
            {canManageRates && (
              <>
                <button
                  onClick={() => {
                    if (materials.length > 0) setManualMaterialId(materials[0].id);
                    if (purities.length > 0) setManualPurityId(purities[0].id);
                    setIsManualModalOpen(true);
                  }}
                  className="inline-flex items-center px-4 py-2 border border-transparent rounded-lg shadow-sm text-sm font-bold text-white bg-[#123B32] hover:bg-[#1B6543] transition"
                >
                  + Add Manual Rate
                </button>
                <button
                  onClick={handleRefreshExternal}
                  disabled={refreshing}
                  className="inline-flex items-center px-4 py-2 border border-[#B89A60] rounded-lg shadow-sm text-sm font-bold text-[#123B32] bg-[#B89A60]/20 hover:bg-[#B89A60]/30 transition disabled:opacity-50"
                >
                  {refreshing ? 'Refreshing...' : 'Refresh External API'}
                </button>
              </>
            )}
          </div>
        </div>

        {/* Three Rate Source Selector Buttons Card */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-5">
          <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
            <div>
              <div className="text-xs uppercase font-bold tracking-wider text-gray-500">Authoritative Pricing Source Mode</div>
              <div className="flex flex-wrap items-center gap-2 mt-1">
                <h2 className="text-xl font-black text-[#123B32]">
                  {activeMode === 'MARKET_API' && 'Market Rate API'}
                  {activeMode === 'CBSL_API' && 'CBSL Rate API'}
                  {activeMode === 'MANUAL' && 'Manual Rate Mode'}
                </h2>
                <span
                  className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold ${
                    activeMode === 'MARKET_API'
                      ? 'bg-emerald-100 text-emerald-800 border border-emerald-300'
                      : activeMode === 'CBSL_API'
                      ? 'bg-purple-100 text-purple-800 border border-purple-300'
                      : 'bg-blue-100 text-blue-800 border border-blue-300'
                  }`}
                >
                  Active Operational Mode
                </span>
                {providerStatus?.isStale && activeMode !== 'MANUAL' && (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-900 border border-amber-300">
                    Stale Rates (&gt;15 min)
                  </span>
                )}
              </div>
              <p className="text-xs text-gray-500 mt-1">
                {activeMode === 'MARKET_API' && 'Live international spot rates converted to LKR. Auto-refreshed every 15 minutes.'}
                {activeMode === 'CBSL_API' && 'Daily official Central Bank of Sri Lanka reference rates. Auto-refreshed every 15 minutes.'}
                {activeMode === 'MANUAL' && 'Operational rates are manually governed by store administrators. External refreshes will not alter store rates.'}
              </p>
            </div>

            {/* Three Source Buttons */}
            <div className="flex flex-wrap items-center gap-2 p-1.5 bg-gray-100 rounded-xl border border-gray-200">
              <button
                onClick={() => handleInitiateSwitch('MARKET_API')}
                disabled={!canManageRates || switchSubmitting}
                className={`px-4 py-2.5 rounded-lg text-xs font-bold transition flex items-center space-x-2 ${
                  activeMode === 'MARKET_API'
                    ? 'bg-[#123B32] text-white shadow-md'
                    : 'text-gray-700 hover:text-gray-900 hover:bg-white/80'
                }`}
              >
                <span>Market Rate API</span>
                {activeMode === 'MARKET_API' && (
                  <span className="w-2 h-2 rounded-full bg-emerald-400"></span>
                )}
              </button>

              <button
                onClick={() => handleInitiateSwitch('CBSL_API')}
                disabled={!canManageRates || switchSubmitting}
                className={`px-4 py-2.5 rounded-lg text-xs font-bold transition flex items-center space-x-2 ${
                  activeMode === 'CBSL_API'
                    ? 'bg-purple-900 text-white shadow-md'
                    : 'text-gray-700 hover:text-gray-900 hover:bg-white/80'
                }`}
              >
                <span>CBSL Rate API</span>
                {activeMode === 'CBSL_API' && (
                  <span className="w-2 h-2 rounded-full bg-purple-300"></span>
                )}
              </button>

              <button
                onClick={() => handleInitiateSwitch('MANUAL')}
                disabled={!canManageRates || switchSubmitting}
                className={`px-4 py-2.5 rounded-lg text-xs font-bold transition flex items-center space-x-2 ${
                  activeMode === 'MANUAL'
                    ? 'bg-[#B89A60] text-gray-950 shadow-md font-extrabold'
                    : 'text-gray-700 hover:text-gray-900 hover:bg-white/80'
                }`}
              >
                <span>Manual Rate</span>
                {activeMode === 'MANUAL' && (
                  <span className="w-2 h-2 rounded-full bg-[#123B32]"></span>
                )}
              </button>
            </div>
          </div>
        </div>

        {/* Alerts */}
        {error && (
          <div className="bg-red-50 border-l-4 border-red-500 p-4 rounded-md flex justify-between items-center">
            <div className="text-sm text-red-700">{error}</div>
            <button onClick={() => setError(null)} className="text-red-500 font-bold hover:text-red-700">✕</button>
          </div>
        )}
        {successMsg && (
          <div className="bg-emerald-50 border-l-4 border-emerald-500 p-4 rounded-md flex justify-between items-center">
            <div className="text-sm text-emerald-800 font-medium">{successMsg}</div>
            <button onClick={() => setSuccessMsg(null)} className="text-emerald-500 font-bold hover:text-emerald-700">✕</button>
          </div>
        )}

        {/* External Provider Status Bar */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-4 flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
          <div className="flex items-center space-x-3">
            <div>
              <div className="text-xs uppercase font-bold tracking-wider text-gray-500">Live Provider Status</div>
              <div className="flex items-center space-x-2 mt-0.5">
                <span className="text-sm font-bold text-gray-900">{providerStatus?.providerName || 'GoldPriceSriLanka.com'}</span>
                {providerStatus?.isAvailable ? (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-emerald-100 text-emerald-800">
                    Operational (LKR Feed)
                  </span>
                ) : (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-amber-100 text-amber-800">
                    Degraded / Fallback
                  </span>
                )}
              </div>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-6 text-xs text-gray-600">
            {providerStatus?.providerEffectiveTime && (
              <div>
                <span className="text-gray-400">Market Timestamp: </span>
                <span className="font-semibold text-gray-800">
                  {new Date(providerStatus.providerEffectiveTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </span>
              </div>
            )}
            {providerStatus?.cbslReferenceDate && (
              <div>
                <span className="text-gray-400">CBSL Date: </span>
                <span className="font-semibold text-gray-800">{providerStatus.cbslReferenceDate}</span>
              </div>
            )}
            <div>
              <span className="text-gray-400">Last Refresh: </span>
              <span className="font-semibold text-gray-800">
                {providerStatus?.lastSuccessfulRefreshAt
                  ? new Date(providerStatus.lastSuccessfulRefreshAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
                  : 'Manual Mode'}
              </span>
            </div>
            <div>
              <span className="text-gray-400">Branch Currency: </span>
              <span className="font-bold text-[#123B32] bg-[#123B32]/10 px-2 py-0.5 rounded">
                {currencyCode}
              </span>
            </div>
          </div>
        </div>

        {/* Active Rates Table */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-200 bg-gray-50 flex items-center justify-between">
            <h2 className="text-base font-bold text-gray-900">Current Operational Rates for {currentBranch?.name || 'Branch'}</h2>
            <span className="text-xs text-gray-500">Values in {currencyCode} (per gram &amp; per sovereign)</span>
          </div>

          {loading ? (
            <div className="p-12 text-center text-gray-500">Loading active rates...</div>
          ) : rates.length === 0 ? (
            <div className="p-12 text-center">
              <p className="text-sm font-semibold text-gray-700">No active metal rates configured for this branch.</p>
              <p className="text-xs text-gray-500 mt-1">Use the source buttons above or &ldquo;+ Add Manual Rate&rdquo; to establish current rates.</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-bold text-gray-500 uppercase tracking-wider">Material</th>
                    <th className="px-6 py-3 text-left text-xs font-bold text-gray-500 uppercase tracking-wider">Purity</th>
                    <th className="px-6 py-3 text-right text-xs font-bold text-gray-500 uppercase tracking-wider">Operational Rate</th>
                    <th className="px-6 py-3 text-right text-xs font-bold text-gray-500 uppercase tracking-wider">24K Reference</th>
                    <th className="px-6 py-3 text-left text-xs font-bold text-gray-500 uppercase tracking-wider">Source</th>
                    <th className="px-6 py-3 text-left text-xs font-bold text-gray-500 uppercase tracking-wider">Effective From</th>
                    {canManageRates && (
                      <th className="px-6 py-3 text-right text-xs font-bold text-gray-500 uppercase tracking-wider">Action</th>
                    )}
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {rates.map((rate) => (
                    <tr key={rate.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="font-bold text-gray-900">{rate.materialName}</div>
                        <div className="text-xs text-gray-400">{rate.materialCode}</div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <span className="font-bold text-[#123B32] bg-[#123B32]/10 px-2 py-0.5 rounded text-xs">
                          {rate.purityName}
                        </span>
                        <div className="text-[11px] text-gray-500 mt-0.5">
                          {rate.purityKarat}K ({rate.purityPercentage}%)
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-right">
                        <div className="text-lg font-black text-[#123B32]">
                          {currencyCode} {rate.ratePerGram.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 4 })}
                          <span className="text-xs text-gray-400 font-normal ml-1">/g</span>
                        </div>
                        <div className="text-xs font-semibold text-[#B89A60] mt-0.5">
                          {currencyCode} {(rate.ratePerGram * 8).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                          <span className="text-[10px] text-gray-400 font-normal ml-1">/ sovereign (8g)</span>
                        </div>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-right text-xs text-gray-600">
                        {rate.externalReferenceRate ? (
                          <span>{currencyCode} {rate.externalReferenceRate.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</span>
                        ) : (
                          <span className="text-gray-400">—</span>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        {getSourceBadge(rate.source, rate.isOverride)}
                        {rate.overrideReason && (
                          <div className="text-[11px] text-amber-800 italic mt-0.5 max-w-xs truncate" title={rate.overrideReason}>
                            &ldquo;{rate.overrideReason}&rdquo;
                          </div>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-xs text-gray-500">
                        {new Date(rate.effectiveFrom).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' })}
                      </td>
                      {canManageRates && (
                        <td className="px-6 py-4 whitespace-nowrap text-right text-xs font-medium">
                          <button
                            onClick={() => {
                              setOverrideRateTarget(rate);
                              setOverrideRateValue(rate.ratePerGram.toString());
                              setOverrideReason('');
                              setIsOverrideModalOpen(true);
                            }}
                            className="text-[#B89A60] hover:text-amber-800 font-bold bg-[#B89A60]/10 hover:bg-[#B89A60]/20 px-3 py-1 rounded transition border border-[#B89A60]/30"
                          >
                            Override
                          </button>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Provider Attribution Banner */}
        <div className="bg-gray-50 border border-gray-200 rounded-xl p-4 flex flex-col sm:flex-row items-center justify-between text-xs text-gray-500 gap-3">
          <div className="flex items-center space-x-2">
            <span>Gold prices by</span>
            <a
              href="https://goldpricesrilanka.com/"
              target="_blank"
              rel="noopener noreferrer"
              className="font-bold text-[#123B32] hover:text-[#1B6543] underline transition"
            >
              GoldPriceSriLanka.com
            </a>
            <span className="text-gray-400">•</span>
            <span>Sri Lanka Market &amp; CBSL Reference Data</span>
          </div>
          <div className="text-gray-400">
            Automatic 15-minute background refresh active for Market &amp; CBSL API modes.
          </div>
        </div>

        {/* Modal: Switch Source Confirmation */}
        {isSwitchModalOpen && targetSwitchMode && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 flex items-center justify-center p-4">
            <div className="bg-white rounded-xl max-w-lg w-full p-6 shadow-2xl border border-gray-200">
              <div className="flex justify-between items-center pb-3 border-b">
                <div>
                  <h3 className="text-lg font-bold text-[#123B32]">
                    Switch Pricing Mode to{' '}
                    {targetSwitchMode === 'MARKET_API' ? 'Market Rate API' : targetSwitchMode === 'CBSL_API' ? 'CBSL Rate API' : 'Manual Rate'}
                  </h3>
                  <p className="text-xs text-gray-500 mt-0.5">
                    Branch: <strong>{currentBranch?.name}</strong> ({currentBranch?.code})
                  </p>
                </div>
                <button onClick={() => setIsSwitchModalOpen(false)} className="text-gray-400 hover:text-gray-600 font-bold">
                  ✕
                </button>
              </div>

              <div className="py-4 space-y-4">
                {targetSwitchMode === 'MARKET_API' && (
                  <div className="space-y-3">
                    <p className="text-xs text-gray-600">
                      Activating <strong>Market Rate API</strong> mode will synchronize operational gold rates with live international market gold spot prices converted to LKR. Rates will automatically update every 15 minutes.
                    </p>

                    {previewLoading ? (
                      <div className="p-6 text-center text-xs text-gray-500 bg-gray-50 rounded-lg border">
                        Fetching live rates from GoldPriceSriLanka.com...
                      </div>
                    ) : feedPreview && feedPreview.isSuccess ? (
                      <div className="bg-emerald-50/60 border border-emerald-200 rounded-lg p-3 space-y-2">
                        <div className="flex justify-between text-xs font-bold text-emerald-950">
                          <span>Live Market Rates to be Applied:</span>
                          <span className="text-[11px] text-gray-500 font-normal">
                            {feedPreview.marketTime ? new Date(feedPreview.marketTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''}
                          </span>
                        </div>
                        <div className="grid grid-cols-2 gap-2 text-xs">
                          {feedPreview.marketRates.map((r) => (
                            <div key={r.purityCode} className="bg-white p-2 rounded border border-emerald-100 flex justify-between items-center">
                              <span className="font-bold text-gray-800">{r.purityCode}:</span>
                              <div className="text-right">
                                <div className="font-extrabold text-[#123B32]">Rs. {r.ratePerGram.toLocaleString()} /g</div>
                                <div className="text-[10px] text-gray-500">Rs. {r.ratePerPawn.toLocaleString()} /pawn</div>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    ) : (
                      <div className="p-3 text-xs text-red-600 bg-red-50 rounded border border-red-200">
                        {feedPreview?.errorMessage || 'Unable to connect to live provider at this time.'}
                      </div>
                    )}
                  </div>
                )}

                {targetSwitchMode === 'CBSL_API' && (
                  <div className="space-y-3">
                    <p className="text-xs text-gray-600">
                      Activating <strong>CBSL Rate API</strong> mode will synchronize operational gold rates with the official daily Central Bank of Sri Lanka gold reference rates. Note: CBSL rates represent daily reference valuations.
                    </p>

                    {previewLoading ? (
                      <div className="p-6 text-center text-xs text-gray-500 bg-gray-50 rounded-lg border">
                        Fetching CBSL reference rates from GoldPriceSriLanka.com...
                      </div>
                    ) : feedPreview && feedPreview.isSuccess ? (
                      <div className="bg-purple-50/60 border border-purple-200 rounded-lg p-3 space-y-2">
                        <div className="flex justify-between text-xs font-bold text-purple-950">
                          <span>CBSL Reference Rates to be Applied:</span>
                          <span className="text-[11px] text-gray-500 font-normal">
                            Date: {feedPreview.cbslDate || 'Today'}
                          </span>
                        </div>
                        <div className="grid grid-cols-2 gap-2 text-xs">
                          {feedPreview.cbslRates.map((r) => (
                            <div key={r.purityCode} className="bg-white p-2 rounded border border-purple-100 flex justify-between items-center">
                              <span className="font-bold text-gray-800">{r.purityCode}:</span>
                              <div className="text-right">
                                <div className="font-extrabold text-purple-900">Rs. {r.ratePerGram.toLocaleString()} /g</div>
                                <div className="text-[10px] text-gray-500">Rs. {r.ratePerPawn.toLocaleString()} /pawn</div>
                              </div>
                            </div>
                          ))}
                        </div>
                      </div>
                    ) : (
                      <div className="p-3 text-xs text-red-600 bg-red-50 rounded border border-red-200">
                        {feedPreview?.errorMessage || 'Unable to connect to live provider at this time.'}
                      </div>
                    )}
                  </div>
                )}

                {targetSwitchMode === 'MANUAL' && (
                  <div className="space-y-3">
                    <p className="text-xs text-gray-600">
                      Activating <strong>Manual Rate Mode</strong> stops automatic external updates. The system will restore your previously configured manual rates where available.
                    </p>

                    {missingPuritiesToConfigure.length > 0 && (
                      <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 space-y-2">
                        <div className="text-xs font-bold text-amber-900">Configure Required Manual Rates:</div>
                        <div className="space-y-2 text-xs">
                          {missingPuritiesToConfigure.map((p) => (
                            <div key={p.id} className="flex items-center justify-between gap-2">
                              <span className="font-semibold text-gray-700">{p.name} ({p.karatValue}K):</span>
                              <input
                                type="number"
                                step="0.0001"
                                placeholder="Rate / gram (Rs.)"
                                value={initialManualInputs[p.id] || ''}
                                onChange={(e) =>
                                  setInitialManualInputs({ ...initialManualInputs, [p.id]: e.target.value })
                                }
                                className="border border-gray-300 rounded px-2 py-1 text-xs w-36 text-right font-bold"
                              />
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                )}

                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">
                    Audit Note / Rationale (Optional)
                  </label>
                  <input
                    type="text"
                    value={switchReason}
                    onChange={(e) => setSwitchReason(e.target.value)}
                    placeholder="e.g. Switched to Market Rate API for morning trading"
                    className="w-full border border-gray-300 rounded-md p-2 text-xs focus:ring-[#123B32] focus:border-[#123B32]"
                  />
                </div>
              </div>

              <div className="flex justify-end space-x-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setIsSwitchModalOpen(false)}
                  className="px-4 py-2 border border-gray-300 rounded-md text-xs font-semibold text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleConfirmSwitch}
                  disabled={switchSubmitting || (targetSwitchMode !== 'MANUAL' && (!feedPreview || !feedPreview.isSuccess))}
                  className="px-4 py-2 bg-[#123B32] text-white rounded-md text-xs font-bold hover:bg-[#1B6543] disabled:opacity-50"
                >
                  {switchSubmitting ? 'Activating Mode...' : 'Confirm & Activate Mode'}
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Modal: Add Manual Rate */}
        {isManualModalOpen && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 flex items-center justify-center p-4">
            <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-2xl border border-gray-200">
              <h3 className="text-lg font-bold text-[#123B32] mb-4">Set Manual Metal Rate</h3>
              <form onSubmit={handleCreateManual} className="space-y-4">
                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Material</label>
                  <select
                    value={manualMaterialId}
                    onChange={(e) => setManualMaterialId(e.target.value)}
                    className="w-full border border-gray-300 rounded-md p-2 text-sm focus:ring-[#123B32] focus:border-[#123B32]"
                    required
                  >
                    {materials.map((m) => (
                      <option key={m.id} value={m.id}>{m.name} ({m.code})</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Purity</label>
                  <select
                    value={manualPurityId}
                    onChange={(e) => setManualPurityId(e.target.value)}
                    className="w-full border border-gray-300 rounded-md p-2 text-sm focus:ring-[#123B32] focus:border-[#123B32]"
                    required
                  >
                    {purities.map((p) => (
                      <option key={p.id} value={p.id}>{p.name} ({p.karatValue}K - {p.purityPercentage}%)</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">
                    Rate Per Gram ({currencyCode})
                  </label>
                  <input
                    type="number"
                    step="0.0001"
                    min="0.0001"
                    value={manualRate}
                    onChange={(e) => setManualRate(e.target.value)}
                    placeholder="e.g. 35,000.00"
                    className="w-full border border-gray-300 rounded-md p-2 text-sm font-semibold focus:ring-[#123B32] focus:border-[#123B32]"
                    required
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Notes / Authority</label>
                  <input
                    type="text"
                    value={manualNotes}
                    onChange={(e) => setManualNotes(e.target.value)}
                    placeholder="e.g. Daily morning board rate update"
                    className="w-full border border-gray-300 rounded-md p-2 text-sm focus:ring-[#123B32] focus:border-[#123B32]"
                  />
                </div>

                <div className="flex justify-end space-x-3 pt-3 border-t">
                  <button
                    type="button"
                    onClick={() => setIsManualModalOpen(false)}
                    className="px-4 py-2 border border-gray-300 rounded-md text-sm text-gray-700 hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={manualSubmitting}
                    className="px-4 py-2 bg-[#123B32] text-white rounded-md text-sm font-bold hover:bg-[#1B6543] disabled:opacity-50"
                  >
                    {manualSubmitting ? 'Saving...' : 'Set Manual Rate'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Modal: Admin Override */}
        {isOverrideModalOpen && overrideRateTarget && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 flex items-center justify-center p-4">
            <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-2xl border border-gray-200">
              <div className="flex items-center space-x-2 text-amber-700 mb-2">
                <h3 className="text-lg font-bold">Admin Rate Override</h3>
              </div>
              <p className="text-xs text-gray-600 mb-4">
                Override active rate for <strong>{overrideRateTarget.materialName} ({overrideRateTarget.purityName})</strong>.
                The previous reference value is preserved in history alongside this override.
              </p>

              <form onSubmit={handleAdminOverride} className="space-y-4">
                <div className="bg-gray-50 p-3 rounded-lg border border-gray-200 text-xs flex justify-between">
                  <span className="text-gray-500">Current Reference Rate:</span>
                  <span className="font-bold text-gray-800">
                    {currencyCode} {overrideRateTarget.ratePerGram.toFixed(4)}
                  </span>
                </div>

                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">
                    New Override Rate / gram ({currencyCode})
                  </label>
                  <input
                    type="number"
                    step="0.0001"
                    min="0.0001"
                    value={overrideRateValue}
                    onChange={(e) => setOverrideRateValue(e.target.value)}
                    className="w-full border border-amber-300 rounded-md p-2 text-sm font-bold text-[#123B32] focus:ring-amber-500 focus:border-amber-500"
                    required
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">
                    Mandatory Override Reason *
                  </label>
                  <textarea
                    value={overrideReason}
                    onChange={(e) => setOverrideReason(e.target.value)}
                    placeholder="Explain the operational rationale for overriding external/reference board rate..."
                    rows={3}
                    className="w-full border border-gray-300 rounded-md p-2 text-sm focus:ring-[#123B32] focus:border-[#123B32]"
                    required
                  />
                </div>

                <div className="flex justify-end space-x-3 pt-3 border-t">
                  <button
                    type="button"
                    onClick={() => setIsOverrideModalOpen(false)}
                    className="px-4 py-2 border border-gray-300 rounded-md text-sm text-gray-700 hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={overrideSubmitting}
                    className="px-4 py-2 bg-amber-700 text-white rounded-md text-sm font-bold hover:bg-amber-800 disabled:opacity-50"
                  >
                    {overrideSubmitting ? 'Overriding...' : 'Apply Admin Override'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Modal: Rate History */}
        {isHistoryModalOpen && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 flex items-center justify-center p-4">
            <div className="bg-white rounded-xl max-w-4xl w-full p-6 shadow-2xl border border-gray-200 max-h-[85vh] flex flex-col">
              <div className="flex justify-between items-center pb-4 border-b">
                <div>
                  <h3 className="text-lg font-bold text-[#123B32]">Historical Metal Rates</h3>
                  <p className="text-xs text-gray-500">Unbroken audit timeline of market, CBSL, manual, and overridden rates.</p>
                </div>
                <button
                  onClick={() => setIsHistoryModalOpen(false)}
                  className="text-gray-400 hover:text-gray-600 font-bold text-lg"
                >
                  ✕
                </button>
              </div>

              <div className="flex-1 overflow-y-auto mt-4">
                {historyLoading ? (
                  <div className="text-center py-8 text-gray-500">Loading historical rates...</div>
                ) : historyItems.length === 0 ? (
                  <div className="text-center py-8 text-gray-500">No historical rates found.</div>
                ) : (
                  <table className="min-w-full divide-y divide-gray-200 text-xs">
                    <thead className="bg-gray-50">
                      <tr>
                        <th className="px-4 py-2 text-left font-bold text-gray-500">Purity</th>
                        <th className="px-4 py-2 text-right font-bold text-gray-500">Rate / gram</th>
                        <th className="px-4 py-2 text-right font-bold text-gray-500">Rate / sovereign</th>
                        <th className="px-4 py-2 text-left font-bold text-gray-500">Source</th>
                        <th className="px-4 py-2 text-left font-bold text-gray-500">Effective Window</th>
                        <th className="px-4 py-2 text-left font-bold text-gray-500">Notes / Reason</th>
                        <th className="px-4 py-2 text-left font-bold text-gray-500">Created By</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-200">
                      {historyItems.map((h) => (
                        <tr key={h.id} className={h.isActive ? 'bg-emerald-50/50' : ''}>
                          <td className="px-4 py-2 font-medium text-gray-900">
                            {h.materialName} - {h.purityName}
                            {h.isActive && <span className="ml-2 text-[10px] text-emerald-700 font-bold">[Active]</span>}
                          </td>
                          <td className="px-4 py-2 text-right font-bold text-[#123B32]">
                            {h.currencyCode} {h.ratePerGram.toFixed(4)}
                          </td>
                          <td className="px-4 py-2 text-right font-semibold text-[#B89A60]">
                            {h.currencyCode} {(h.ratePerGram * 8).toFixed(2)}
                          </td>
                          <td className="px-4 py-2">{getSourceBadge(h.source, h.isOverride)}</td>
                          <td className="px-4 py-2 text-gray-500">
                            {new Date(h.effectiveFrom).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', month: 'short', day: 'numeric' })}
                            {h.effectiveTo ? (
                              <span> → {new Date(h.effectiveTo).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', month: 'short', day: 'numeric' })}</span>
                            ) : (
                              <span className="text-emerald-700 font-bold"> → Present</span>
                            )}
                          </td>
                          <td className="px-4 py-2 text-gray-600 italic">{h.overrideReason || '—'}</td>
                          <td className="px-4 py-2 text-gray-500">{h.createdByName || 'System'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>
          </div>
        )}

        {/* Modal: Pricing Reference Calculator */}
        {isCalcModalOpen && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 flex items-center justify-center p-4">
            <div className="bg-white rounded-xl max-w-lg w-full p-6 shadow-2xl border border-gray-200">
              <div className="flex justify-between items-center mb-4">
                <div>
                  <h3 className="text-lg font-bold text-[#123B32]">Reference Pricing Calculator</h3>
                  <p className="text-xs text-gray-500">Test gold valuation + making charge + VAT reference breakdown.</p>
                </div>
                <button
                  onClick={() => setIsCalcModalOpen(false)}
                  className="text-gray-400 hover:text-gray-600 font-bold"
                >
                  ✕
                </button>
              </div>

              <form onSubmit={handleCalculateReference} className="space-y-3">
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Material</label>
                    <select
                      value={calcMaterialId}
                      onChange={(e) => setCalcMaterialId(e.target.value)}
                      className="w-full border border-gray-300 rounded p-1.5 text-xs"
                      required
                    >
                      {materials.map((m) => (
                        <option key={m.id} value={m.id}>{m.name}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Purity</label>
                    <select
                      value={calcPurityId}
                      onChange={(e) => setCalcPurityId(e.target.value)}
                      className="w-full border border-gray-300 rounded p-1.5 text-xs"
                      required
                    >
                      {purities.map((p) => (
                        <option key={p.id} value={p.id}>{p.name} ({p.karatValue}K)</option>
                      ))}
                    </select>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Net Weight (g)</label>
                    <input
                      type="number"
                      step="0.0001"
                      min="0.0001"
                      value={calcNetWeight}
                      onChange={(e) => setCalcNetWeight(e.target.value)}
                      className="w-full border border-gray-300 rounded p-1.5 text-xs font-semibold"
                      required
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Piece Count</label>
                    <input
                      type="number"
                      min="1"
                      value={calcPieceCount}
                      onChange={(e) => setCalcPieceCount(e.target.value)}
                      className="w-full border border-gray-300 rounded p-1.5 text-xs font-semibold"
                      required
                    />
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Making Charge Method</label>
                    <select
                      value={calcMakingMethod}
                      onChange={(e) => setCalcMakingMethod(e.target.value)}
                      className="w-full border border-gray-300 rounded p-1.5 text-xs"
                    >
                      <option value="1">PER_GRAM (Rate × Net Weight)</option>
                      <option value="2">PER_ITEM (Rate × Piece Count)</option>
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Making Charge Value</label>
                    <input
                      type="number"
                      step="0.01"
                      min="0"
                      value={calcMakingValue}
                      onChange={(e) => setCalcMakingValue(e.target.value)}
                      className="w-full border border-gray-300 rounded p-1.5 text-xs font-semibold"
                      required
                    />
                  </div>
                </div>

                <div className="pt-2">
                  <button
                    type="submit"
                    disabled={calcLoading}
                    className="w-full py-2 bg-[#123B32] text-white rounded text-sm font-bold hover:bg-[#1B6543] disabled:opacity-50"
                  >
                    {calcLoading ? 'Calculating...' : 'Compute Reference Breakdown'}
                  </button>
                </div>
              </form>

              {calcResult && (
                <div className="mt-4 p-4 bg-emerald-50/70 border border-emerald-200 rounded-lg text-xs space-y-2">
                  <div className="flex justify-between font-bold text-gray-700">
                    <span>Active Rate Applied:</span>
                    <span>{calcResult.currencyCode} {calcResult.ratePerGramUsed.toFixed(4)} /g</span>
                  </div>
                  <div className="flex justify-between text-gray-600">
                    <span>Reference Material Value:</span>
                    <span>{calcResult.currencyCode} {calcResult.referenceMaterialValue.toFixed(2)}</span>
                  </div>
                  <div className="flex justify-between text-gray-600">
                    <span>Making Charge ({calcResult.makingChargeMethodName}):</span>
                    <span>{calcResult.currencyCode} {calcResult.makingChargeAmount.toFixed(2)}</span>
                  </div>
                  <div className="flex justify-between font-semibold text-gray-800 pt-1 border-t border-emerald-200">
                    <span>Subtotal before Tax:</span>
                    <span>{calcResult.currencyCode} {calcResult.subtotalBeforeTax.toFixed(2)}</span>
                  </div>
                  <div className="flex justify-between text-gray-600">
                    <span>VAT ({calcResult.vatEnabled ? `${calcResult.vatRatePercentage}%` : 'Disabled'}):</span>
                    <span>{calcResult.currencyCode} {calcResult.vatAmount.toFixed(2)}</span>
                  </div>
                  <div className="flex justify-between font-black text-sm text-[#123B32] pt-1 border-t border-emerald-300">
                    <span>Reference Total:</span>
                    <span>{calcResult.currencyCode} {calcResult.referenceTotal.toFixed(2)}</span>
                  </div>
                  <p className="text-[10px] text-gray-500 italic mt-2">
                    * Final selling price will be agreed and recorded at POS checkout.
                  </p>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
