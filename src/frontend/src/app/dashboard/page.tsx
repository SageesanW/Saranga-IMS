'use client';

import React from 'react';
import Link from 'next/link';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import { KeyRound, Shield, Building2, Users2, Landmark } from 'lucide-react';

export default function DashboardPage() {
  const { user } = useAuth();
  const { activeBranch, branches } = useBranch();

  if (!user) return null;

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Welcome Banner */}
        <div className="bg-gradient-to-r from-[#0D3826] to-[#1B6543] rounded-2xl p-6 text-white shadow-md border border-[#D4AF37]/30">
          <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4">
            <div>
              <span className="text-[11px] font-bold uppercase tracking-wider text-[#D4AF37]">
                Enterprise Operations Portal
              </span>
              <h1 className="text-2xl font-bold mt-1">Welcome, {user.fullName}</h1>
              <p className="text-sm text-gray-200 mt-1">
                Active Branch: <strong className="text-[#D4AF37]">{activeBranch ? `${activeBranch.code} - ${activeBranch.name}` : 'All Branches (Global Admin)'}</strong>
                {' '}• Currency: <strong className="text-white">{activeBranch?.currencyCode || 'LKR (Rs.)'}</strong>
              </p>
            </div>
            <div className="flex gap-2">
              <Link
                href="/branches"
                className="px-4 py-2 bg-white/10 hover:bg-white/20 text-white rounded-lg text-xs font-semibold border border-white/20 transition"
              >
                Manage Branches
              </Link>
              <Link
                href="/users"
                className="px-4 py-2 bg-[#D4AF37] hover:bg-[#BA9225] text-[#0D3826] rounded-lg text-xs font-bold transition shadow-sm"
              >
                Manage Staff
              </Link>
            </div>
          </div>
        </div>

        {/* Quick Stats Grid */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-bold text-gray-500 uppercase tracking-wider">Total Branches</p>
              <p className="text-2xl font-black text-[#0D3826] mt-1">{branches.length}</p>
              <p className="text-[11px] text-gray-400 mt-0.5">Active across Sri Lanka & International</p>
            </div>
            <div className="w-12 h-12 rounded-xl bg-emerald-50 text-emerald-800 flex items-center justify-center">
              <Building2 className="w-6 h-6" />
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-bold text-gray-500 uppercase tracking-wider">Your Assigned Scope</p>
              <p className="text-2xl font-black text-[#0D3826] mt-1">
                {user.isGlobalAdmin ? 'Full Business' : `${user.branchIds.length} Branch(es)`}
              </p>
              <p className="text-[11px] text-gray-400 mt-0.5">
                {user.isGlobalAdmin ? 'Unrestricted Global Oversight' : 'Branch-scoped operational access'}
              </p>
            </div>
            <div className="w-12 h-12 rounded-xl bg-amber-50 text-amber-800 flex items-center justify-center">
              <Shield className="w-6 h-6" />
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-bold text-gray-500 uppercase tracking-wider">Default Currency</p>
              <p className="text-2xl font-black text-[#0D3826] mt-1">
                {activeBranch?.currencyCode || 'LKR'}
              </p>
              <p className="text-[11px] text-gray-400 mt-0.5">
                Sri Lankan Rupees (Rs.) • Precision 18,2
              </p>
            </div>
            <div className="w-12 h-12 rounded-xl bg-yellow-50 text-yellow-800 flex items-center justify-center">
              <Landmark className="w-6 h-6" />
            </div>
          </div>
        </div>

        {/* Module Status & Architecture Overview */}
        <div className="bg-white rounded-xl border border-gray-200 p-6 shadow-sm">
          <h2 className="text-base font-bold text-gray-900 mb-4">
            Module 2 Architecture & Status Summary
          </h2>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="border border-gray-100 p-4 rounded-xl bg-gray-50/50">
              <div className="flex items-center space-x-2 text-sm font-bold text-[#0D3826] mb-1">
                <Building2 className="w-4 h-4" />
                <span>Multi-Branch Isolation & Global Admin</span>
              </div>
              <p className="text-xs text-gray-600 leading-relaxed">
                Branch CRUD with unique code constraint, active/inactive enforcement, and multi-country readiness (Sri Lanka LKR / Canada CAD). Regular users are restricted strictly to their assigned branches via <code className="text-xs bg-gray-200 px-1 py-0.5 rounded">UserBranches</code>, while Global Admins have business-wide oversight.
              </p>
            </div>

            <div className="border border-gray-100 p-4 rounded-xl bg-gray-50/50">
              <div className="flex items-center space-x-2 text-sm font-bold text-[#0D3826] mb-1">
                <Shield className="w-4 h-4" />
                <span>Granular RBAC & Security</span>
              </div>
              <p className="text-xs text-gray-600 leading-relaxed">
                Fine-grained permissions mapped across 9 system modules. System roles (<code className="text-xs bg-gray-200 px-1 py-0.5 rounded">Owner / Super Admin</code>, <code className="text-xs bg-gray-200 px-1 py-0.5 rounded">Manager</code>, <code className="text-xs bg-gray-200 px-1 py-0.5 rounded">Cashier</code>, <code className="text-xs bg-gray-200 px-1 py-0.5 rounded">Auditor</code>) protected against deletion. Instant token revocation on user deactivation.
              </p>
            </div>
          </div>
        </div>
      </div>
    </AppLayout>
  );
}
