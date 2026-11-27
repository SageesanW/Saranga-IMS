'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  LayoutDashboard,
  Receipt,
  Users,
  Gem,
  Layers,
  UploadCloud,
  ClipboardCheck,
  Sliders,
  Wallet,
  Coins,
  TrendingUp,
  Percent,
  Building2,
  UserCheck,
  ShieldCheck,
  LogOut,
  ChevronDown,
  Menu,
  X,
  CreditCard,
  Store,
  Clock,
} from 'lucide-react';
import { shiftApi, Shift } from '@/lib/api';

interface AppLayoutProps {
  children: React.ReactNode;
}

interface NavSection {
  title: string;
  items: {
    name: string;
    href: string;
    icon: React.ElementType;
    permission?: string;
  }[];
}

export default function AppLayout({ children }: AppLayoutProps) {
  const { user, isAuthenticated, isLoading: authLoading, logout } = useAuth();
  const { branches, activeBranch, activeBranchId, setActiveBranch } = useBranch();
  const pathname = usePathname();
  const router = useRouter();

  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [activeShift, setActiveShift] = useState<Shift | null>(null);
  const [shiftLoading, setShiftLoading] = useState(false);

  useEffect(() => {
    if (!authLoading && !isAuthenticated) {
      router.push('/login');
    }
  }, [authLoading, isAuthenticated, router]);

  // Load active shift status for top bar context
  useEffect(() => {
    if (isAuthenticated) {
      setShiftLoading(true);
      shiftApi
        .getActiveCashierShift()
        .then((shift: Shift | null) => setActiveShift(shift && shift.id ? shift : null))
        .catch(() => setActiveShift(null))
        .finally(() => setShiftLoading(false));
    }
  }, [isAuthenticated, pathname]);

  if (authLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-[#F8F7F4]">
        <div className="flex flex-col items-center space-y-3.5">
          <div className="w-10 h-10 border-3 border-[#123B32] border-t-[#B89A60] rounded-full animate-spin" />
          <p className="text-xs font-semibold text-[#123B32] tracking-wider uppercase">
            Loading Saranga Jewellery...
          </p>
        </div>
      </div>
    );
  }

  if (!isAuthenticated || !user) {
    return null;
  }

  const primaryRole = user.roles[0] || (user.isGlobalAdmin ? 'Owner / Super Admin' : 'Staff');

  const navSections: NavSection[] = [
    {
      title: 'Overview',
      items: [
        { name: 'Dashboard', href: '/dashboard', icon: LayoutDashboard },
      ],
    },
    {
      title: 'Sales & Customers',
      items: [
        { name: 'POS Terminal', href: '/pos', icon: Receipt },
        { name: 'Customers', href: '/customers', icon: Users },
      ],
    },
    {
      title: 'Inventory',
      items: [
        { name: 'Jewellery Items', href: '/inventory', icon: Gem },
        { name: 'Assemblies & Sets', href: '/inventory/compositions/new', icon: Layers },
        { name: 'Physical Count', href: '/inventory/count', icon: ClipboardCheck },
        { name: 'Attributes Master', href: '/attributes', icon: Sliders },
      ],
    },
    {
      title: 'Cash Operations',
      items: [
        { name: 'Shifts & Drawers', href: '/shifts', icon: Wallet },
        { name: 'Cash Ledger', href: '/cash-ledger', icon: Coins },
      ],
    },
    {
      title: 'Configuration',
      items: [
        { name: 'Metal Rates', href: '/rates', icon: TrendingUp },
        { name: 'VAT Settings', href: '/vat', icon: Percent },
        { name: 'Branches', href: '/branches', icon: Building2 },
        { name: 'Staff Users', href: '/users', icon: UserCheck },
        { name: 'Roles & Permissions', href: '/roles', icon: ShieldCheck },
      ],
    },
  ];

  return (
    <div className="min-h-screen bg-[#F8F7F4] flex">
      {/* Mobile Sidebar Overlay */}
      {sidebarOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/50 backdrop-blur-sm lg:hidden transition-opacity"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      {/* Sidebar Navigation */}
      <aside
        className={`
          fixed top-0 bottom-0 left-0 z-50 w-64 bg-[#0B2822] text-white flex flex-col border-r border-[#123B32] transition-transform duration-200 ease-in-out
          lg:translate-x-0 ${sidebarOpen ? 'translate-x-0' : '-translate-x-full'}
        `}
      >
        {/* Brand Header */}
        <div className="h-16 flex items-center justify-between px-5 border-b border-[#123B32]/80 bg-[#061713]">
          <Link href="/dashboard" className="flex items-center space-x-2.5">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-[#B89A60] to-[#7D6336] flex items-center justify-center text-white font-bold text-sm shadow-sm">
              S
            </div>
            <div>
              <div className="flex items-center space-x-1.5">
                <span className="text-sm font-bold tracking-wider text-white">SARANGA</span>
                <span className="text-[10px] px-1.5 py-0.2 rounded bg-[#B89A60]/20 text-[#B89A60] font-semibold border border-[#B89A60]/30">
                  JEWELLERY
                </span>
              </div>
              <p className="text-[9px] text-gray-400 tracking-wider uppercase">Enterprise Management</p>
            </div>
          </Link>

          <button
            onClick={() => setSidebarOpen(false)}
            className="lg:hidden p-1 text-gray-400 hover:text-white rounded-md"
            aria-label="Close menu"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Navigation Sections */}
        <div className="flex-1 overflow-y-auto px-3.5 py-4 space-y-5">
          {navSections.map((section, idx) => (
            <div key={idx}>
              <p className="px-2.5 mb-1.5 text-[10px] font-semibold tracking-wider text-[#B89A60] uppercase">
                {section.title}
              </p>
              <nav className="space-y-0.5">
                {section.items.map((item) => {
                  const Icon = item.icon;
                  const isActive =
                    pathname === item.href ||
                    (item.href !== '/dashboard' && pathname.startsWith(item.href));

                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      onClick={() => setSidebarOpen(false)}
                      className={`
                        flex items-center gap-2.5 px-2.5 py-2 rounded-lg text-xs font-medium transition-colors select-none
                        ${
                          isActive
                            ? 'bg-[#123B32] text-white shadow-sm border border-[#B89A60]/30 font-semibold'
                            : 'text-gray-300 hover:bg-white/5 hover:text-white'
                        }
                      `}
                    >
                      <Icon
                        className={`w-4 h-4 flex-shrink-0 ${
                          isActive ? 'text-[#B89A60]' : 'text-gray-400'
                        }`}
                      />
                      <span className="truncate">{item.name}</span>
                    </Link>
                  );
                })}
              </nav>
            </div>
          ))}
        </div>

        {/* Sidebar Footer: Active User & Logout */}
        <div className="p-3.5 border-t border-[#123B32] bg-[#061713]/80">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-2.5 min-w-0">
              <div className="w-8 h-8 rounded-full bg-[#123B32] border border-[#B89A60]/40 flex items-center justify-center text-xs font-bold text-[#B89A60] flex-shrink-0">
                {user.fullName.charAt(0)}
              </div>
              <div className="min-w-0">
                <p className="text-xs font-semibold text-white truncate">{user.fullName}</p>
                <p className="text-[10px] text-gray-400 truncate">{primaryRole}</p>
              </div>
            </div>

            <button
              onClick={() => logout()}
              title="Sign Out"
              className="p-1.5 rounded-md text-gray-400 hover:text-rose-400 hover:bg-rose-950/30 transition-colors"
            >
              <LogOut className="w-4 h-4" />
            </button>
          </div>
        </div>
      </aside>

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col lg:pl-64 min-w-0">
        {/* Top Header Bar */}
        <header className="h-16 bg-white border-b border-[#E2E6E3] sticky top-0 z-30 flex items-center justify-between px-4 sm:px-6 shadow-xs">
          {/* Left: Mobile Menu Toggle & Branch Context */}
          <div className="flex items-center space-x-3 sm:space-x-4">
            <button
              onClick={() => setSidebarOpen(true)}
              className="lg:hidden p-2 rounded-lg text-gray-600 hover:bg-gray-100 focus:outline-none"
              aria-label="Open menu"
            >
              <Menu className="w-5 h-5" />
            </button>

            {/* Active Branch Selector */}
            <div className="flex items-center space-x-2 bg-[#F8F7F4] px-3 py-1.5 rounded-lg border border-[#E2E6E3]">
              <Store className="w-4 h-4 text-[#123B32] flex-shrink-0" />
              <div className="flex flex-col">
                <span className="text-[9px] uppercase font-bold text-[#69736E] tracking-wider leading-none">
                  Branch
                </span>
                <select
                  value={activeBranchId || ''}
                  onChange={(e) => setActiveBranch(e.target.value || null)}
                  className="bg-transparent text-xs text-[#202824] font-semibold focus:outline-none cursor-pointer pr-1"
                >
                  {branches.length === 0 && <option value="">No branches</option>}
                  {branches.map((b) => (
                    <option key={b.id} value={b.id}>
                      {b.code} - {b.name} ({b.currencyCode})
                    </option>
                  ))}
                </select>
              </div>
              <span className="text-[10px] font-bold px-1.5 py-0.5 rounded bg-[#123B32]/10 text-[#123B32] border border-[#123B32]/20">
                {activeBranch?.currencyCode || 'LKR'}
              </span>
            </div>
          </div>

          {/* Right: Active Shift Badge & Staff Profile */}
          <div className="flex items-center space-x-3 sm:space-x-4">
            {/* Contextual Active Shift Indicator */}
            {activeShift && activeShift.id ? (
              <Link
                href="/shifts"
                className="flex items-center space-x-2 px-3 py-1.5 rounded-lg bg-emerald-50 border border-emerald-200 text-emerald-800 hover:bg-emerald-100 transition-colors"
                title={`Active Shift #${activeShift.shiftNumber} in ${activeShift.cashDrawerName}`}
              >
                <span className="w-2 h-2 rounded-full bg-emerald-600 animate-pulse" />
                <div className="text-left hidden sm:block">
                  <p className="text-[10px] font-bold uppercase tracking-wider text-emerald-900 leading-none">
                    Shift Active
                  </p>
                  <p className="text-xs font-semibold text-emerald-700 leading-tight">
                    {activeShift.cashDrawerCode || activeShift.cashDrawerName}
                  </p>
                </div>
              </Link>
            ) : (
              <Link
                href="/shifts"
                className="flex items-center space-x-1.5 px-2.5 py-1.5 rounded-lg bg-amber-50 border border-amber-200 text-amber-800 hover:bg-amber-100 transition-colors text-xs font-medium"
              >
                <Clock className="w-3.5 h-3.5 text-amber-700" />
                <span className="hidden sm:inline">No Active Shift</span>
              </Link>
            )}

            {/* Quick POS Action */}
            <Link
              href="/pos"
              className="hidden md:inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-[#123B32] hover:bg-[#0B2822] text-white text-xs font-semibold shadow-xs transition-colors"
            >
              <CreditCard className="w-3.5 h-3.5 text-[#B89A60]" />
              <span>Open POS</span>
            </Link>

            {/* User Profile Chip */}
            <div className="flex items-center space-x-2 pl-2 border-l border-[#E2E6E3]">
              <div className="w-8 h-8 rounded-full bg-[#123B32] text-white flex items-center justify-center text-xs font-bold border border-[#B89A60]/30">
                {user.fullName.charAt(0)}
              </div>
              <div className="hidden sm:block text-left">
                <p className="text-xs font-semibold text-[#202824] leading-tight">{user.fullName}</p>
                <p className="text-[10px] text-[#69736E] leading-tight">{primaryRole}</p>
              </div>
            </div>
          </div>
        </header>

        {/* Content Body Container */}
        <main className="flex-1 p-4 sm:p-6 lg:p-8 max-w-7xl w-full mx-auto">
          {children}
        </main>
      </div>
    </div>
  );
}
