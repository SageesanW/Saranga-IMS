'use client';

import React, { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { useAuth } from '@/lib/auth-context';
import { Gem, LogOut, CheckCircle2, Shield, Database, FileText, KeyRound } from 'lucide-react';

export default function DashboardPage() {
  const { user, isAuthenticated, isLoading, logout } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.replace('/login');
    }
  }, [isAuthenticated, isLoading, router]);

  if (isLoading || !user) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50">
        <div className="w-8 h-8 border-4 border-saranga-green-700 border-t-transparent rounded-full animate-spin"></div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 text-slate-800">
      {/* Top Navbar */}
      <header className="bg-saranga-green-950 border-b border-saranga-gold-500/40 text-white shadow-md">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-saranga-gold-500/20 border border-saranga-gold-500/50 flex items-center justify-center text-saranga-gold-400">
              <Gem className="w-6 h-6" />
            </div>
            <div>
              <span className="text-xl font-black tracking-wider text-saranga-gold-400 uppercase">
                SARANGA
              </span>
              <span className="hidden sm:inline-block ml-3 px-2 py-0.5 text-xs font-semibold rounded bg-saranga-gold-500/20 text-saranga-gold-300 border border-saranga-gold-500/30">
                LKR / Rs.
              </span>
            </div>
          </div>

          <div className="flex items-center gap-4">
            <div className="text-right">
              <div className="text-sm font-semibold text-white">{user.fullName}</div>
              <div className="text-xs text-saranga-gold-300/90 font-medium">
                Roles: {user.roles?.length ? user.roles.join(', ') : 'User'} • {user.isGlobalAdmin ? 'Global Admin' : (user.defaultBranchId ? `Branch ${user.defaultBranchId.slice(0, 8)}...` : 'Assigned Branch')}
              </div>
            </div>

            <button
              onClick={() => {
                logout();
                router.push('/login');
              }}
              className="p-2 rounded-lg bg-saranga-green-800/80 hover:bg-saranga-green-700 text-saranga-gold-300 border border-saranga-gold-500/30 transition-colors"
              title="Sign Out"
            >
              <LogOut className="w-5 h-5" />
            </button>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="mb-8">
          <h1 className="text-2xl font-bold text-saranga-green-950">
            Module 1: Foundation & Authentication Active
          </h1>
          <p className="text-sm text-gray-500 mt-1">
            Modular monolith backend (.NET 10 LTS + PostgreSQL) & frontend (Next.js 16 + Tailwind CSS) verified.
          </p>
        </div>

        {/* Verification Status Cards */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <div className="bg-white p-6 rounded-2xl border border-gray-200 shadow-sm">
            <div className="flex items-center gap-3 text-saranga-green-700 mb-2">
              <KeyRound className="w-5 h-5" />
              <h3 className="font-semibold text-gray-900">Secure Tokens</h3>
            </div>
            <p className="text-xs text-gray-500 mb-3">
              HttpOnly SameSite cookies with SHA-256 hashed refresh tokens & in-memory access tokens.
            </p>
            <div className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 bg-emerald-50 px-2.5 py-1 rounded-full">
              <CheckCircle2 className="w-3.5 h-3.5" /> Hardened
            </div>
          </div>

          <div className="bg-white p-6 rounded-2xl border border-gray-200 shadow-sm">
            <div className="flex items-center gap-3 text-saranga-green-700 mb-2">
              <Shield className="w-5 h-5" />
              <h3 className="font-semibold text-gray-900">RBAC Architecture</h3>
            </div>
            <p className="text-xs text-gray-500 mb-3">
              User ↔ Role ↔ Permission & multi-branch scoping ready for Module 2.
            </p>
            <div className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 bg-emerald-50 px-2.5 py-1 rounded-full">
              <CheckCircle2 className="w-3.5 h-3.5" /> Prepared
            </div>
          </div>

          <div className="bg-white p-6 rounded-2xl border border-gray-200 shadow-sm">
            <div className="flex items-center gap-3 text-saranga-green-700 mb-2">
              <Database className="w-5 h-5" />
              <h3 className="font-semibold text-gray-900">PostgreSQL Precision</h3>
            </div>
            <p className="text-xs text-gray-500 mb-3">
              Explicit domain precision: money (18,2), gold weights/rates (18,4), taxes (5,2).
            </p>
            <div className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 bg-emerald-50 px-2.5 py-1 rounded-full">
              <CheckCircle2 className="w-3.5 h-3.5" /> Configured
            </div>
          </div>

          <div className="bg-white p-6 rounded-2xl border border-gray-200 shadow-sm">
            <div className="flex items-center gap-3 text-saranga-green-700 mb-2">
              <FileText className="w-5 h-5" />
              <h3 className="font-semibold text-gray-900">Immutable Audit</h3>
            </div>
            <p className="text-xs text-gray-500 mb-3">
              Protected audit trail: immutable logs, password/token hash sanitization.
            </p>
            <div className="inline-flex items-center gap-1.5 text-xs font-semibold text-emerald-700 bg-emerald-50 px-2.5 py-1 rounded-full">
              <CheckCircle2 className="w-3.5 h-3.5" /> Immutable
            </div>
          </div>
        </div>

        {/* User Session Details */}
        <div className="bg-white rounded-2xl border border-gray-200 p-6 shadow-sm">
          <h2 className="text-base font-bold text-saranga-green-950 mb-4">
            Active User & Security Context
          </h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4 text-sm">
            <div className="p-3 rounded-xl bg-gray-50">
              <span className="text-xs text-gray-500 uppercase font-semibold block">User ID</span>
              <span className="font-mono text-xs text-gray-800">{user.id}</span>
            </div>
            <div className="p-3 rounded-xl bg-gray-50">
              <span className="text-xs text-gray-500 uppercase font-semibold block">Username</span>
              <span className="font-medium text-gray-900">{user.username}</span>
            </div>
            <div className="p-3 rounded-xl bg-gray-50">
              <span className="text-xs text-gray-500 uppercase font-semibold block">Assigned Roles</span>
              <span className="font-medium text-saranga-green-800">{user.roles?.join(', ') || 'None'}</span>
            </div>
            <div className="p-3 rounded-xl bg-gray-50">
              <span className="text-xs text-gray-500 uppercase font-semibold block">Email</span>
              <span className="text-gray-800">{user.email}</span>
            </div>
            <div className="p-3 rounded-xl bg-gray-50">
              <span className="text-xs text-gray-500 uppercase font-semibold block">Branch Scope</span>
              <span className="text-gray-800">
                {user.isGlobalAdmin ? 'All Branches (Owner / Super Admin)' : (user.branchIds?.length ? `${user.branchIds.length} Assigned Branch(es)` : 'Default Branch')}
              </span>
            </div>
            <div className="p-3 rounded-xl bg-gray-50">
              <span className="text-xs text-gray-500 uppercase font-semibold block">Status</span>
              <span className="text-emerald-700 font-semibold">{user.isActive ? 'Active' : 'Inactive'}</span>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
