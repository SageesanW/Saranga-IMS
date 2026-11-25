'use client';

import React, { useEffect, useState, useMemo } from 'react';
import AppLayout from '@/components/AppLayout';
import {
  vatConfigurationApi,
  branchApi,
  VatConfiguration,
  Branch,
} from '@/lib/api';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';

export default function VatConfigurationPage() {
  const { user } = useAuth();
  const { activeBranchId, setActiveBranch } = useBranch();

  const [branches, setBranches] = useState<Branch[]>([]);
  const [currentConfig, setCurrentConfig] = useState<VatConfiguration | null>(null);
  const [historyItems, setHistoryItems] = useState<VatConfiguration[]>([]);

  const [loading, setLoading] = useState(true);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Modal / Form state
  const [isUpdateModalOpen, setIsUpdateModalOpen] = useState(false);
  const [configName, setConfigName] = useState('Standard VAT');
  const [ratePercentage, setRatePercentage] = useState('18.00');
  const [isEnabled, setIsEnabled] = useState(false);
  const [effectiveDate, setEffectiveDate] = useState('');
  const [submitting, setSubmitting] = useState(false);

  // Toggle reason state
  const [toggleReason, setToggleReason] = useState('');
  const [isToggleModalOpen, setIsToggleModalOpen] = useState(false);
  const [pendingToggleState, setPendingToggleState] = useState(false);

  const canManageVat = user?.roles.some((r) => r === 'Super Admin' || r === 'Owner' || r === 'Branch Manager') || user?.isGlobalAdmin;

  const currentBranch = useMemo(() => {
    return branches.find((b) => b.id === activeBranchId) || branches[0] || null;
  }, [branches, activeBranchId]);

  useEffect(() => {
    async function loadBranches() {
      try {
        const bRes = await branchApi.getBranches();
        setBranches(bRes);
        if (!activeBranchId && bRes.length > 0) {
          setActiveBranch(bRes[0].id);
        }
      } catch (err: any) {
        setError(err.message || 'Failed to load branch metadata');
      }
    }
    loadBranches();
  }, []);

  useEffect(() => {
    if (activeBranchId) {
      loadVatData(activeBranchId);
    }
  }, [activeBranchId]);

  async function loadVatData(branchId: string) {
    setLoading(true);
    setHistoryLoading(true);
    setError(null);
    try {
      const [activeConfig, hist] = await Promise.all([
        vatConfigurationApi.getActiveConfiguration(branchId),
        vatConfigurationApi.getVatHistory({ branchId, pageSize: 50 }),
      ]);
      setCurrentConfig(activeConfig);
      setHistoryItems(hist.items);
      setConfigName(activeConfig.name || 'Standard VAT');
      setRatePercentage(activeConfig.ratePercentage.toFixed(2));
      setIsEnabled(activeConfig.isEnabled);
    } catch (err: any) {
      setError(err.message || 'Failed to load VAT configuration');
    } finally {
      setLoading(false);
      setHistoryLoading(false);
    }
  }

  async function handleUpdateConfig(e: React.FormEvent) {
    e.preventDefault();
    if (!activeBranchId) return;
    const rate = parseFloat(ratePercentage);
    if (isNaN(rate) || rate < 0 || rate > 100) {
      setError('VAT percentage must be between 0.00% and 100.00%.');
      return;
    }

    setSubmitting(true);
    setError(null);
    setSuccessMsg(null);
    try {
      await vatConfigurationApi.createOrUpdateConfiguration({
        branchId: activeBranchId,
        name: configName.trim(),
        isEnabled: isEnabled,
        ratePercentage: rate,
        effectiveFrom: effectiveDate ? new Date(effectiveDate).toISOString() : undefined,
        calculationMode: 'FLAT_PERCENTAGE',
      });
      setIsUpdateModalOpen(false);
      setSuccessMsg('VAT configuration updated and archived into history successfully.');
      await loadVatData(activeBranchId);
    } catch (err: any) {
      setError(err.message || 'Failed to update VAT configuration.');
    } finally {
      setSubmitting(false);
    }
  }

  async function handleConfirmToggle(e: React.FormEvent) {
    e.preventDefault();
    if (!activeBranchId) return;
    setSubmitting(true);
    setError(null);
    setSuccessMsg(null);
    try {
      await vatConfigurationApi.toggleVatStatus({
        branchId: activeBranchId,
        isEnabled: pendingToggleState,
        reason: toggleReason.trim() || undefined,
      });
      setIsToggleModalOpen(false);
      setToggleReason('');
      setSuccessMsg(`VAT successfully ${pendingToggleState ? 'activated' : 'deactivated'} for this branch.`);
      await loadVatData(activeBranchId);
    } catch (err: any) {
      setError(err.message || 'Failed to toggle VAT status.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <AppLayout>
      <div className="space-y-6 max-w-6xl mx-auto">
        {/* Header Bar */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div className="flex items-center space-x-3">
              
              <h1 className="text-2xl font-black text-[#123B32] tracking-tight">VAT Configuration</h1>
            </div>
            <p className="text-sm text-gray-500 mt-1">
              Branch-specific Value Added Tax rate and calculation configuration.
              <span className="text-xs text-amber-700 bg-amber-50 px-2 py-0.5 rounded ml-2 border border-amber-200">
                Rule: Changing tax settings archives historical configuration and never alters past sales.
              </span>
            </p>
          </div>

          {canManageVat && (
            <div className="flex items-center gap-3">
              <button
                onClick={() => {
                  setPendingToggleState(!currentConfig?.isEnabled);
                  setIsToggleModalOpen(true);
                }}
                className={`inline-flex items-center px-4 py-2 rounded-lg text-sm font-bold border transition ${
                  currentConfig?.isEnabled
                    ? 'border-amber-300 bg-amber-50 text-amber-900 hover:bg-amber-100'
                    : 'border-emerald-300 bg-emerald-50 text-emerald-900 hover:bg-emerald-100'
                }`}
              >
                {currentConfig?.isEnabled ? '⏸️ Deactivate VAT' : '▶️ Activate VAT'}
              </button>
              <button
                onClick={() => {
                  if (currentConfig) {
                    setConfigName(currentConfig.name);
                    setRatePercentage(currentConfig.ratePercentage.toFixed(2));
                    setIsEnabled(currentConfig.isEnabled);
                  }
                  setIsUpdateModalOpen(true);
                }}
                className="inline-flex items-center px-4 py-2 border border-transparent rounded-lg shadow-sm text-sm font-bold text-white bg-[#123B32] hover:bg-[#1B6543] transition"
              >
                Update VAT Rate
              </button>
            </div>
          )}
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

        {/* Current Active Configuration Card */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-200 bg-gray-50 flex items-center justify-between">
            <div className="flex items-center space-x-2">
              <span className="font-bold text-gray-900">Active VAT Policy for:</span>
              <span className="font-extrabold text-[#123B32] bg-[#123B32]/10 px-2.5 py-0.5 rounded text-sm">
                {currentBranch?.code} - {currentBranch?.name}
              </span>
            </div>
            <div>
              {currentConfig?.isEnabled ? (
                <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-black bg-emerald-100 text-emerald-800 border border-emerald-300">
                  ● VAT ACTIVE & COLLECTED
                </span>
              ) : (
                <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-black bg-gray-100 text-gray-600 border border-gray-300">
                  ○ VAT DISABLED (Rs. 0.00 TAX)
                </span>
              )}
            </div>
          </div>

          {loading ? (
            <div className="p-12 text-center text-gray-500">Loading VAT settings...</div>
          ) : (
            <div className="p-6 grid grid-cols-1 md:grid-cols-4 gap-6">
              <div className="bg-gray-50 rounded-lg p-4 border border-gray-200">
                <span className="text-xs uppercase font-bold text-gray-500">Configured Rate</span>
                <div className="text-3xl font-black text-[#123B32] mt-1">
                  {currentConfig?.ratePercentage.toFixed(2)}%
                </div>
                <div className="text-[11px] text-gray-400 mt-1">
                  {currentConfig?.isEnabled ? 'Applied to taxable bill amount' : 'Tax suspended while inactive'}
                </div>
              </div>

              <div className="bg-gray-50 rounded-lg p-4 border border-gray-200">
                <span className="text-xs uppercase font-bold text-gray-500">Calculation Mode</span>
                <div className="text-sm font-bold text-gray-800 mt-2">
                  Flat Percentage
                </div>
                <div className="text-[11px] text-amber-800 bg-amber-50 p-1 rounded mt-1 border border-amber-200">
                  MVP Foundation. Slab rules deferred until business confirmation.
                </div>
              </div>

              <div className="bg-gray-50 rounded-lg p-4 border border-gray-200">
                <span className="text-xs uppercase font-bold text-gray-500">Effective Since</span>
                <div className="text-sm font-bold text-gray-800 mt-2">
                  {currentConfig?.effectiveFrom
                    ? new Date(currentConfig.effectiveFrom).toLocaleDateString([], { year: 'numeric', month: 'short', day: 'numeric' })
                    : 'Initial Deployment'}
                </div>
                <div className="text-[11px] text-gray-400 mt-1">
                  {currentConfig?.effectiveFrom ? new Date(currentConfig.effectiveFrom).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : ''}
                </div>
              </div>

              <div className="bg-gray-50 rounded-lg p-4 border border-gray-200">
                <span className="text-xs uppercase font-bold text-gray-500">Policy Name</span>
                <div className="text-sm font-bold text-gray-800 mt-2 truncate" title={currentConfig?.name}>
                  {currentConfig?.name || 'Standard VAT'}
                </div>
                <div className="text-[11px] text-gray-400 mt-1">
                  Updated by: {currentConfig?.updatedByName || currentConfig?.createdByName || 'System'}
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Historical Configurations */}
        <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
          <div className="px-6 py-4 border-b border-gray-200 bg-gray-50 flex items-center justify-between">
            <div>
              <h2 className="text-base font-bold text-gray-900">VAT Configuration Audit Timeline</h2>
              <p className="text-xs text-gray-500">Preserved historical configurations for past transaction auditing.</p>
            </div>
            <span className="text-xs font-semibold text-gray-500">Total Versions: {historyItems.length}</span>
          </div>

          {historyLoading ? (
            <div className="p-8 text-center text-gray-500 text-sm">Loading history...</div>
          ) : historyItems.length === 0 ? (
            <div className="p-8 text-center text-gray-500 text-sm">No historical VAT configurations recorded yet.</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200 text-xs">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-6 py-3 text-left font-bold text-gray-500 uppercase">Configuration Name</th>
                    <th className="px-6 py-3 text-center font-bold text-gray-500 uppercase">Rate</th>
                    <th className="px-6 py-3 text-center font-bold text-gray-500 uppercase">Status</th>
                    <th className="px-6 py-3 text-left font-bold text-gray-500 uppercase">Effective Timeline</th>
                    <th className="px-6 py-3 text-left font-bold text-gray-500 uppercase">Created By</th>
                    <th className="px-6 py-3 text-left font-bold text-gray-500 uppercase">Created Timestamp</th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {historyItems.map((h) => (
                    <tr key={h.id} className={h.isActive ? 'bg-emerald-50/40 font-semibold' : 'text-gray-600'}>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <div className="font-bold text-gray-900">{h.name}</div>
                        {h.isActive && (
                          <span className="text-[10px] font-bold text-emerald-700">[CURRENT OPERATIONAL]</span>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-center font-bold text-[#123B32]">
                        {h.ratePercentage.toFixed(2)}%
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-center">
                        {h.isEnabled ? (
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-bold bg-emerald-100 text-emerald-800">
                            Enabled
                          </span>
                        ) : (
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-bold bg-gray-100 text-gray-600">
                            Disabled
                          </span>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-gray-500">
                        {new Date(h.effectiveFrom).toLocaleDateString([], { year: 'numeric', month: 'short', day: 'numeric' })}
                        {h.effectiveTo ? (
                          <span> → {new Date(h.effectiveTo).toLocaleDateString([], { year: 'numeric', month: 'short', day: 'numeric' })}</span>
                        ) : (
                          <span className="text-emerald-700 font-bold"> → Present</span>
                        )}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-gray-500">
                        {h.createdByName || 'System Administrator'}
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-gray-400">
                        {new Date(h.createdAt).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Modal: Update VAT Configuration */}
        {isUpdateModalOpen && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 flex items-center justify-center p-4">
            <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-2xl border border-gray-200">
              <h3 className="text-lg font-bold text-[#123B32] mb-2">Update VAT Configuration</h3>
              <p className="text-xs text-gray-500 mb-4">
                Creating an updated configuration will safely supersede the active policy while preserving full historical records.
              </p>

              <form onSubmit={handleUpdateConfig} className="space-y-4">
                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Configuration Name</label>
                  <input
                    type="text"
                    value={configName}
                    onChange={(e) => setConfigName(e.target.value)}
                    placeholder="e.g. 18.00"
                    className="w-full border border-gray-300 rounded-md p-2 text-sm focus:ring-[#123B32] focus:border-[#123B32]"
                    required
                  />
                </div>

                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">VAT Percentage (0.00% to 100.00%)</label>
                  <div className="relative">
                    <input
                      type="number"
                      step="0.01"
                      min="0"
                      max="100"
                      value={ratePercentage}
                      onChange={(e) => setRatePercentage(e.target.value)}
                      className="w-full border border-gray-300 rounded-md p-2 text-sm font-bold text-[#123B32] focus:ring-[#123B32] focus:border-[#123B32] pr-8"
                      required
                    />
                    <span className="absolute right-3 top-2 text-gray-500 font-bold">%</span>
                  </div>
                </div>

                <div className="flex items-center space-x-2 pt-1">
                  <input
                    type="checkbox"
                    id="enableVatCheck"
                    checked={isEnabled}
                    onChange={(e) => setIsEnabled(e.target.checked)}
                    className="rounded text-[#123B32] focus:ring-[#123B32] h-4 w-4"
                  />
                  <label htmlFor="enableVatCheck" className="text-sm font-semibold text-gray-800">
                    Enable VAT collection immediately for this branch
                  </label>
                </div>

                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">Effective Date (Optional - defaults to now)</label>
                  <input
                    type="datetime-local"
                    value={effectiveDate}
                    onChange={(e) => setEffectiveDate(e.target.value)}
                    className="w-full border border-gray-300 rounded-md p-2 text-xs focus:ring-[#123B32] focus:border-[#123B32]"
                  />
                </div>

                <div className="flex justify-end space-x-3 pt-3 border-t">
                  <button
                    type="button"
                    onClick={() => setIsUpdateModalOpen(false)}
                    className="px-4 py-2 border border-gray-300 rounded-md text-sm text-gray-700 hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={submitting}
                    className="px-4 py-2 bg-[#123B32] text-white rounded-md text-sm font-bold hover:bg-[#1B6543] disabled:opacity-50"
                  >
                    {submitting ? 'Saving...' : 'Save Configuration'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Modal: Toggle VAT Status */}
        {isToggleModalOpen && (
          <div className="fixed inset-0 z-50 overflow-y-auto bg-black/50 flex items-center justify-center p-4">
            <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-2xl border border-gray-200">
              <h3 className="text-lg font-bold text-gray-900 mb-2">
                {pendingToggleState ? 'Activate VAT Collection' : 'Deactivate VAT Collection'}
              </h3>
              <p className="text-xs text-gray-600 mb-4">
                {pendingToggleState
                  ? `This will begin applying the ${currentConfig?.ratePercentage || '18.00'}% VAT rate to future sales at ${currentBranch?.name}.`
                  : `This will suspend VAT collection for future sales at ${currentBranch?.name} (VAT will calculate as Rs. 0.00).`}
              </p>

              <form onSubmit={handleConfirmToggle} className="space-y-4">
                <div>
                  <label className="block text-xs font-bold uppercase text-gray-600 mb-1">
                    Operational Reason / Notes (Optional)
                  </label>
                  <textarea
                    value={toggleReason}
                    onChange={(e) => setToggleReason(e.target.value)}
                    placeholder="e.g. Government gazette implementation date or tax holiday..."
                    rows={2}
                    className="w-full border border-gray-300 rounded-md p-2 text-sm focus:ring-[#123B32] focus:border-[#123B32]"
                  />
                </div>

                <div className="flex justify-end space-x-3 pt-3 border-t">
                  <button
                    type="button"
                    onClick={() => setIsToggleModalOpen(false)}
                    className="px-4 py-2 border border-gray-300 rounded-md text-sm text-gray-700 hover:bg-gray-50"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={submitting}
                    className={`px-4 py-2 rounded-md text-sm font-bold text-white transition disabled:opacity-50 ${
                      pendingToggleState ? 'bg-emerald-700 hover:bg-emerald-800' : 'bg-amber-700 hover:bg-amber-800'
                    }`}
                  >
                    {submitting ? 'Processing...' : pendingToggleState ? 'Confirm Activation' : 'Confirm Deactivation'}
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
