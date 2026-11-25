'use client';

import React, { useEffect } from 'react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';

interface AppLayoutProps {
  children: React.ReactNode;
}

export default function AppLayout({ children }: AppLayoutProps) {
  const { user, isAuthenticated, isLoading: authLoading, logout } = useAuth();
  const { branches, activeBranch, activeBranchId, setActiveBranch } = useBranch();
  const pathname = usePathname();
  const router = useRouter();

  useEffect(() => {
    if (!authLoading && !isAuthenticated) {
      router.push('/login');
    }
  }, [authLoading, isAuthenticated, router]);

  if (authLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50">
        <div className="flex flex-col items-center space-y-4">
          <div className="w-12 h-12 border-4 border-[#0D3826] border-t-[#D4AF37] rounded-full animate-spin"></div>
          <p className="text-sm font-semibold text-gray-700 tracking-wide uppercase">Loading Saranga Jewellery System...</p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !user) {
    return null;
  }

  const primaryRole = user.roles[0] || (user.isGlobalAdmin ? 'Owner / Super Admin' : 'User');

  const navLinks = [
    { name: 'Dashboard', href: '/dashboard', icon: '📊' },
    { name: 'Branches', href: '/branches', icon: '🏢' },
    { name: 'Users', href: '/users', icon: '👥' },
    { name: 'Roles & Permissions', href: '/roles', icon: '🛡️' },
    { name: 'Attributes Master', href: '/attributes', icon: '💎' },
    { name: 'Inventory & Barcode', href: '/inventory', icon: '📦' },
    { name: 'Inventory Count', href: '/inventory/count', icon: '📋' },
    { name: 'Metal Rates', href: '/rates', icon: '🪙' },
    { name: 'VAT Configuration', href: '/vat', icon: '🏷️' },
    { name: 'Customers', href: '/customers', icon: '👤' },
    { name: 'Shifts', href: '/shifts', icon: '💵' },
  ];

  const upcomingModules = [
    { name: 'POS & Billing', icon: '🧾' },
    { name: 'Cash Ledger', icon: '🪙' },
    { name: 'Sales Verification', icon: '🔍' },
    { name: 'Audit Logs', icon: '📜' },
  ];

  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      {/* Top Header Bar */}
      <header className="bg-[#0D3826] text-white border-b border-[#D4AF37]/30 shadow-md sticky top-0 z-40">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          {/* Logo & Brand */}
          <div className="flex items-center space-x-3">
            <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-[#D4AF37] to-[#946E1E] flex items-center justify-center text-white font-bold text-lg shadow">
              S
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <span className="text-lg font-bold tracking-tight text-white">SARANGA</span>
                <span className="text-xs px-1.5 py-0.5 rounded bg-[#D4AF37]/20 text-[#D4AF37] border border-[#D4AF37]/40 font-medium">
                  JEWELLERY
                </span>
              </div>
              <p className="text-[10px] text-gray-300 tracking-wider uppercase">Enterprise Management System</p>
            </div>
          </div>

          {/* Right Section: Branch Switcher & User Profile */}
          <div className="flex items-center space-x-4">
            {/* Active Branch Selector */}
            <div className="flex items-center space-x-2 bg-[#1B6543]/60 px-3 py-1.5 rounded-lg border border-[#D4AF37]/30">
              <span className="text-sm">📍</span>
              <div className="flex flex-col">
                <span className="text-[10px] uppercase font-bold text-[#D4AF37] tracking-wider leading-none">Branch Context</span>
                <select
                  value={activeBranchId || ''}
                  onChange={(e) => setActiveBranch(e.target.value || null)}
                  className="bg-transparent text-xs text-white font-medium focus:outline-none cursor-pointer pr-2"
                >
                  {branches.length === 0 && <option value="" className="text-gray-800">No branches</option>}
                  {branches.map((b) => (
                    <option key={b.id} value={b.id} className="text-gray-900">
                      {b.code} - {b.name} ({b.currencyCode}){!b.isActive ? ' [Inactive]' : ''}
                    </option>
                  ))}
                  {user.isGlobalAdmin && (
                    <option value="" className="text-gray-900">🌐 All Branches (Global Oversight)</option>
                  )}
                </select>
              </div>
            </div>

            {/* User Profile Pill */}
            <div className="hidden md:flex items-center space-x-3 pl-2 border-l border-white/20">
              <div className="text-right">
                <div className="text-xs font-semibold text-white">{user.fullName || user.username}</div>
                <div className="flex items-center justify-end space-x-1">
                  <span className="text-[10px] px-1.5 py-0.2 rounded font-medium bg-[#D4AF37] text-[#0D3826]">
                    {primaryRole}
                  </span>
                  {user.isGlobalAdmin && (
                    <span className="text-[10px] px-1.5 py-0.2 rounded font-medium bg-emerald-500 text-white">
                      Global
                    </span>
                  )}
                </div>
              </div>
              <div className="w-8 h-8 rounded-full bg-[#1B6543] border border-[#D4AF37]/50 flex items-center justify-center text-xs font-bold text-[#D4AF37]">
                {(user.fullName || user.username).substring(0, 2).toUpperCase()}
              </div>
            </div>

            {/* Logout Button */}
            <button
              onClick={() => {
                logout();
                router.push('/login');
              }}
              title="Sign Out"
              className="text-xs bg-red-950/60 hover:bg-red-900 text-red-200 border border-red-800/60 px-3 py-1.5 rounded-md font-medium transition"
            >
              Sign Out
            </button>
          </div>
        </div>
      </header>

      {/* Main Body with Sidebar */}
      <div className="flex-1 flex max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6 gap-6">
        {/* Sidebar */}
        <aside className="w-64 flex-shrink-0">
          <nav className="bg-white rounded-xl shadow-sm border border-gray-200 p-3 space-y-6">
            <div>
              <p className="px-3 text-[11px] font-bold uppercase tracking-wider text-gray-400 mb-2">Core Modules</p>
              <ul className="space-y-1">
                {navLinks.map((link) => {
                  const isActive = pathname === link.href || pathname.startsWith(link.href + '/');
                  return (
                    <li key={link.href}>
                      <Link
                        href={link.href}
                        className={`flex items-center space-x-3 px-3 py-2 rounded-lg text-sm font-semibold transition ${
                          isActive
                            ? 'bg-[#0D3826] text-white shadow-sm'
                            : 'text-gray-700 hover:bg-gray-100 hover:text-gray-900'
                        }`}
                      >
                        <span className="text-base">{link.icon}</span>
                        <span>{link.name}</span>
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </div>

            <div>
              <p className="px-3 text-[11px] font-bold uppercase tracking-wider text-gray-400 mb-2">Upcoming Modules</p>
              <ul className="space-y-1">
                {upcomingModules.map((mod) => (
                  <li key={mod.name}>
                    <div className="flex items-center justify-between px-3 py-1.5 rounded-lg text-xs font-medium text-gray-400 cursor-not-allowed">
                      <span className="flex items-center space-x-2">
                        <span>{mod.icon}</span>
                        <span>{mod.name}</span>
                      </span>
                      <span className="text-[9px] bg-gray-100 text-gray-500 px-1.5 py-0.5 rounded border border-gray-200">
                        Upcoming
                      </span>
                    </div>
                  </li>
                ))}
              </ul>
            </div>

            {/* Current Active Context Footer */}
            <div className="pt-3 border-t border-gray-100 px-2 text-[11px] text-gray-500">
              <div className="flex justify-between items-center mb-1">
                <span className="font-semibold text-gray-700">Currency:</span>
                <span className="font-bold text-[#0D3826]">{activeBranch?.currencyCode || 'LKR (Rs.)'}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="font-semibold text-gray-700">Time Zone:</span>
                <span className="text-gray-600">{activeBranch?.timeZone || 'Asia/Colombo'}</span>
              </div>
            </div>
          </nav>
        </aside>

        {/* Content Area */}
        <main className="flex-1 min-w-0">
          {children}
        </main>
      </div>
    </div>
  );
}
