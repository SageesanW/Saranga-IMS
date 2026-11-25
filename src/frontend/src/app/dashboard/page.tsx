'use client';

import React, { useEffect, useState } from 'react';
import Link from 'next/link';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  inventoryApi,
  customerApi,
  shiftApi,
  metalRateApi,
  cashLedgerApi,
  Shift,
  MetalRate,
  CashLedgerTransaction,
} from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Badge } from '@/components/ui/Badge';
import { Button } from '@/components/ui/Button';
import {
  Receipt,
  Gem,
  Users,
  Wallet,
  Coins,
  TrendingUp,
  Clock,
  ArrowUpRight,
  ShieldCheck,
  Building2,
  Calendar,
} from 'lucide-react';

export default function DashboardPage() {
  const { user } = useAuth();
  const { activeBranch, branches } = useBranch();

  const [loading, setLoading] = useState(true);
  const [totalItems, setTotalItems] = useState<number>(0);
  const [totalCustomers, setTotalCustomers] = useState<number>(0);
  const [activeShift, setActiveShift] = useState<Shift | null>(null);
  const [metalRates, setMetalRates] = useState<MetalRate[]>([]);
  const [recentCashMoves, setRecentCashMoves] = useState<CashLedgerTransaction[]>([]);

  useEffect(() => {
    let isMounted = true;
    async function loadDashboardData() {
      setLoading(true);
      try {
        const [invRes, custRes, shiftRes, rateRes, cashRes] = await Promise.allSettled([
          inventoryApi.getInventory({ pageSize: 1 }),
          customerApi.getCustomers({ pageSize: 1 }),
          shiftApi.getActiveCashierShift(),
          metalRateApi.getActiveRates(activeBranch?.id),
          cashLedgerApi.getTransactions({ pageSize: 5 }),
        ]);

        if (isMounted) {
          if (invRes.status === 'fulfilled') setTotalItems(invRes.value.totalCount);
          if (custRes.status === 'fulfilled') setTotalCustomers(custRes.value.totalCount);
          if (shiftRes.status === 'fulfilled') setActiveShift(shiftRes.value);
          if (rateRes.status === 'fulfilled') setMetalRates(rateRes.value);
          if (cashRes.status === 'fulfilled') setRecentCashMoves(cashRes.value.items);
        }
      } finally {
        if (isMounted) setLoading(false);
      }
    }

    loadDashboardData();
    return () => {
      isMounted = false;
    };
  }, [activeBranch?.id]);

  if (!user) return null;

  const currency = activeBranch?.currencyCode || 'LKR';

  return (
    <AppLayout>
      <PageHeader
        title="Operations Overview"
        description={`Real-time status for ${activeBranch ? activeBranch.name : 'All Branches'}`}
        actions={
          <div className="flex items-center gap-2">
            <Link href="/pos">
              <Button variant="primary" size="sm" leftIcon={<Receipt className="w-4 h-4" />}>
                Open POS Terminal
              </Button>
            </Link>
            <Link href="/inventory">
              <Button variant="secondary" size="sm" leftIcon={<Gem className="w-4 h-4" />}>
                Manage Stock
              </Button>
            </Link>
          </div>
        }
      />

      {/* Operational Summary Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
        {/* Active Shift Card */}
        <div className="bg-white p-5 rounded-xl border border-[#E2E6E3] shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-2">
              <span className="text-[11px] font-semibold text-[#69736E] uppercase tracking-wider">
                Active Shift
              </span>
              {activeShift ? (
                <Badge variant="success" size="sm">
                  Open
                </Badge>
              ) : (
                <Badge variant="neutral" size="sm">
                  Closed
                </Badge>
              )}
            </div>
            {activeShift ? (
              <div>
                <p className="text-lg font-bold text-[#123B32]">
                  {activeShift.cashDrawerName}
                </p>
                <p className="text-xs text-[#69736E] mt-0.5">
                  Shift #{activeShift.shiftNumber} • Started by {activeShift.cashierName}
                </p>
              </div>
            ) : (
              <div>
                <p className="text-sm font-semibold text-[#69736E]">No shift open</p>
                <p className="text-xs text-gray-400 mt-0.5">Start shift to begin POS operations</p>
              </div>
            )}
          </div>
          <div className="mt-4 pt-3 border-t border-[#E2E6E3] flex justify-between items-center text-xs">
            <span className="text-[#69736E]">Cash Drawer</span>
            <Link href="/shifts" className="font-semibold text-[#123B32] hover:underline">
              {activeShift ? 'Manage Shift' : 'Open Shift'} &rarr;
            </Link>
          </div>
        </div>

        {/* Live Gold Rates Card */}
        <div className="bg-white p-5 rounded-xl border border-[#E2E6E3] shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-2">
              <span className="text-[11px] font-semibold text-[#69736E] uppercase tracking-wider">
                Gold Rate (24K / 22K)
              </span>
              <TrendingUp className="w-4 h-4 text-[#B89A60]" />
            </div>
            {metalRates.length > 0 ? (
              <div>
                <div className="flex items-baseline gap-2">
                  <span className="text-lg font-bold text-[#123B32] tabular-nums">
                    {currency} {metalRates[0]?.ratePerGram?.toLocaleString('en-US', { minimumFractionDigits: 2 }) ?? '0.00'}
                  </span>
                  <span className="text-xs text-[#69736E]">/g (24K)</span>
                </div>
                <p className="text-xs text-[#69736E] mt-0.5 tabular-nums">
                  22K: {currency} {((metalRates[0]?.ratePerGram ?? 0) * (22 / 24)).toLocaleString('en-US', { minimumFractionDigits: 2 })} /g
                </p>
              </div>
            ) : (
              <div>
                <p className="text-sm font-semibold text-[#69736E]">Rates Not Configured</p>
                <p className="text-xs text-gray-400 mt-0.5">Set daily rates for reference pricing</p>
              </div>
            )}
          </div>
          <div className="mt-4 pt-3 border-t border-[#E2E6E3] flex justify-between items-center text-xs">
            <span className="text-[#69736E]">Daily Board</span>
            <Link href="/rates" className="font-semibold text-[#123B32] hover:underline">
              View Rates &rarr;
            </Link>
          </div>
        </div>

        {/* Total Available Inventory */}
        <div className="bg-white p-5 rounded-xl border border-[#E2E6E3] shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-2">
              <span className="text-[11px] font-semibold text-[#69736E] uppercase tracking-wider">
                Stock Catalog
              </span>
              <Gem className="w-4 h-4 text-[#123B32]" />
            </div>
            <p className="text-2xl font-bold text-[#123B32] tabular-nums">
              {loading ? '...' : totalItems.toLocaleString()}
            </p>
            <p className="text-xs text-[#69736E] mt-0.5">
              Jewellery pieces registered across catalog
            </p>
          </div>
          <div className="mt-4 pt-3 border-t border-[#E2E6E3] flex justify-between items-center text-xs">
            <span className="text-[#69736E]">Stock Inventory</span>
            <Link href="/inventory" className="font-semibold text-[#123B32] hover:underline">
              Inventory List &rarr;
            </Link>
          </div>
        </div>

        {/* Customer Base */}
        <div className="bg-white p-5 rounded-xl border border-[#E2E6E3] shadow-xs flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between mb-2">
              <span className="text-[11px] font-semibold text-[#69736E] uppercase tracking-wider">
                Customer Registry
              </span>
              <Users className="w-4 h-4 text-[#123B32]" />
            </div>
            <p className="text-2xl font-bold text-[#123B32] tabular-nums">
              {loading ? '...' : totalCustomers.toLocaleString()}
            </p>
            <p className="text-xs text-[#69736E] mt-0.5">
              Profiles with E.164 canonical normalization
            </p>
          </div>
          <div className="mt-4 pt-3 border-t border-[#E2E6E3] flex justify-between items-center text-xs">
            <span className="text-[#69736E]">Profiles</span>
            <Link href="/customers" className="font-semibold text-[#123B32] hover:underline">
              Customer Directory &rarr;
            </Link>
          </div>
        </div>
      </div>

      {/* Main Two-Column Operational Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left 2 Columns: Recent Physical Cash Ledger Movements */}
        <div className="lg:col-span-2 bg-white rounded-xl border border-[#E2E6E3] p-5 shadow-xs">
          <div className="flex items-center justify-between mb-4">
            <div>
              <h2 className="text-sm font-bold text-[#123B32]">Recent Cash Movements</h2>
              <p className="text-xs text-[#69736E] mt-0.5">
                Authoritative physical cash ledger transactions
              </p>
            </div>
            <Link href="/cash-ledger" className="text-xs font-semibold text-[#123B32] hover:underline">
              View All &rarr;
            </Link>
          </div>

          {recentCashMoves.length === 0 ? (
            <div className="text-center py-8 text-xs text-[#69736E] border border-dashed border-[#E2E6E3] rounded-lg">
              No recent physical cash movements recorded in this branch.
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-xs text-left">
                <thead>
                  <tr className="border-b border-[#E2E6E3] text-[#69736E] uppercase tracking-wider font-semibold">
                    <th className="py-2 px-3">Reference</th>
                    <th className="py-2 px-3">Type</th>
                    <th className="py-2 px-3">Drawer</th>
                    <th className="py-2 px-3">Category</th>
                    <th className="py-2 px-3 text-right">Amount</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#E2E6E3]">
                  {recentCashMoves.map((tx) => (
                    <tr key={tx.id} className="hover:bg-[#F8F7F4]/60">
                      <td className="py-2.5 px-3 font-mono font-medium text-[#202824]">
                        {tx.transactionReference || tx.id.substring(0, 8)}
                      </td>
                      <td className="py-2.5 px-3">
                        <Badge
                          size="sm"
                          variant={
                            tx.transactionType === 1
                              ? 'success'
                              : tx.transactionType === 2
                              ? 'warning'
                              : 'info'
                          }
                        >
                          {tx.transactionType === 1 ? 'Cash In' : tx.transactionType === 2 ? 'Cash Out' : tx.transactionType === 3 ? 'Transfer' : 'Reversal'}
                        </Badge>
                      </td>
                      <td className="py-2.5 px-3 text-[#202824]">{tx.categoryName || 'General'}</td>
                      <td className="py-2.5 px-3 text-[#69736E]">{tx.description || tx.reason || '—'}</td>
                      <td className="py-2.5 px-3 text-right font-mono font-semibold text-[#202824] tabular-nums">
                        {currency} {tx.amount.toLocaleString('en-US', { minimumFractionDigits: 2 })}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        {/* Right 1 Column: Quick Operational Actions & Enterprise Identity */}
        <div className="space-y-5">
          {/* Quick Actions Panel */}
          <div className="bg-white rounded-xl border border-[#E2E6E3] p-5 shadow-xs">
            <h2 className="text-sm font-bold text-[#123B32] mb-3">Quick Actions</h2>
            <div className="space-y-2">
              <Link
                href="/pos"
                className="flex items-center justify-between p-3 rounded-lg border border-[#E2E6E3] hover:border-[#123B32] hover:bg-[#F8F7F4] transition-colors text-xs font-semibold text-[#202824]"
              >
                <div className="flex items-center gap-2.5">
                  <Receipt className="w-4 h-4 text-[#123B32]" />
                  <span>Start Retail POS Sale</span>
                </div>
                <ArrowUpRight className="w-3.5 h-3.5 text-gray-400" />
              </Link>

              <Link
                href="/inventory/new"
                className="flex items-center justify-between p-3 rounded-lg border border-[#E2E6E3] hover:border-[#123B32] hover:bg-[#F8F7F4] transition-colors text-xs font-semibold text-[#202824]"
              >
                <div className="flex items-center gap-2.5">
                  <Gem className="w-4 h-4 text-[#123B32]" />
                  <span>Add New Jewellery Piece</span>
                </div>
                <ArrowUpRight className="w-3.5 h-3.5 text-gray-400" />
              </Link>

              <Link
                href="/customers"
                className="flex items-center justify-between p-3 rounded-lg border border-[#E2E6E3] hover:border-[#123B32] hover:bg-[#F8F7F4] transition-colors text-xs font-semibold text-[#202824]"
              >
                <div className="flex items-center gap-2.5">
                  <Users className="w-4 h-4 text-[#123B32]" />
                  <span>Register Customer</span>
                </div>
                <ArrowUpRight className="w-3.5 h-3.5 text-gray-400" />
              </Link>

              <Link
                href="/shifts"
                className="flex items-center justify-between p-3 rounded-lg border border-[#E2E6E3] hover:border-[#123B32] hover:bg-[#F8F7F4] transition-colors text-xs font-semibold text-[#202824]"
              >
                <div className="flex items-center gap-2.5">
                  <Wallet className="w-4 h-4 text-[#123B32]" />
                  <span>Cash Drawer & Shifts</span>
                </div>
                <ArrowUpRight className="w-3.5 h-3.5 text-gray-400" />
              </Link>
            </div>
          </div>

          {/* Operational Scope */}
          <div className="bg-[#0B2822] text-white rounded-xl p-5 border border-[#123B32] shadow-xs">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[10px] font-bold uppercase tracking-wider text-[#B89A60]">
                Security & Compliance
              </span>
              <ShieldCheck className="w-4 h-4 text-[#B89A60]" />
            </div>
            <p className="text-xs font-semibold text-white">
              {user.isGlobalAdmin ? 'Full Enterprise Oversight' : `Branch: ${activeBranch?.name}`}
            </p>
            <p className="text-[11px] text-gray-300 mt-1 leading-relaxed">
              All financial actions, price negotiations, shift balances, and cash ledger postings are audited with immutable timestamps and user signatures.
            </p>
            <div className="mt-4 pt-3 border-t border-white/10 flex items-center justify-between text-[11px] text-gray-300">
              <span>Operating Currency:</span>
              <span className="font-semibold text-[#B89A60]">{currency}</span>
            </div>
          </div>
        </div>
      </div>
    </AppLayout>
  );
}
