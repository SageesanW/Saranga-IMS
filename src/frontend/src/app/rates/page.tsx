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
        metalRateApi.getProviderStatus(),
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
      const updatedStatus = await metalRateApi.getProviderStatus();
      setProviderStatus(updatedStatus);
      setSuccessMsg('Rates updated successfully from external provider.');
    } catch (err: any) {
      setError(err.message || 'External refresh encountered an issue. Existing store rates retained.');
      // Refresh provider status anyway to show error telemetry
      const updatedStatus = await metalRateApi.getProviderStatus();
      setProviderStatus(updatedStatus);
    } finally {
      setRefreshing(false);
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
      case 'EXTERNAL_API':
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-emerald-100 text-emerald-800 border border-emerald-300">
            Live External
          </span>
        );
      case 'MANUAL':
        return (
          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-blue-100 text-blue-800 border border-blue-300">
            Manual Entry
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
              <div className="text-xs uppercase font-bold tracking-wider text-gray-500">External Provider Feed</div>
              <div className="flex items-center space-x-2 mt-0.5">
                <span className="text-sm font-bold text-gray-900">{providerStatus?.providerName || 'GoldApi'}</span>
                {providerStatus?.isAvailable ? (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-emerald-100 text-emerald-800">
                    Feed Operational
                  </span>
                ) : (
                  <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-amber-100 text-amber-800">
                    Manual Mode / Degraded
                  </span>
                )}
              </div>
            </div>
          </div>

          <div className="flex items-center space-x-6 text-xs text-gray-600">
            <div>
              <span className="text-gray-400">Last Refresh: </span>
              <span className="font-semibold text-gray-800">
                {providerStatus?.lastSuccessfulRefreshAt
                  ? new Date(providerStatus.lastSuccessfulRefreshAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
                  : 'Manual Feed Only'}
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

        {/* Active Rates Grid */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-200 bg-gray-50 flex items-center justify-between">
            <h2 className="text-base font-bold text-gray-900">Current Active Rates for {currentBranch?.name || 'Branch'}</h2>
            <span className="text-xs text-gray-500">1 g rate in {currencyCode}</span>
          </div>

          {loading ? (
            <div className="p-12 text-center text-gray-500">Loading active rates...</div>
          ) : rates.length === 0 ? (
            <div className="p-12 text-center">
              
              <p className="text-sm font-semibold text-gray-700">No active metal rates configured for this branch.</p>
              <p className="text-xs text-gray-500 mt-1">Use &ldquo;+ Add Manual Rate&rdquo; or &ldquo;Refresh External API&rdquo; to establish current rates.</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-bold text-gray-500 uppercase tracking-wider">Material</th>
                    <th className="px-6 py-3 text-left text-xs font-bold text-gray-500 uppercase tracking-wider">Purity</th>
                    <th className="px-6 py-3 text-right text-xs font-bold text-gray-500 uppercase tracking-wider">Active Rate / gram</th>
                    <th className="px-6 py-3 text-right text-xs font-bold text-gray-500 uppercase tracking-wider">Reference (24K)</th>
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
                        </div>
                        <div className="text-[10px] text-gray-400">per 1.0000 gram</div>
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
                    placeholder="e.g. 25,000.00"
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
                <span className="text-xl"></span>
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
                  <p className="text-xs text-gray-500">Unbroken audit timeline of market, manual, and overridden rates.</p>
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
