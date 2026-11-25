'use client';

import React, { useState, useEffect } from 'react';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  posApi,
  shiftsApi,
  customerApi,
  PosInvoice,
  PosItemLookupResult,
  Customer,
  Shift
} from '@/lib/api';
import {
  ShoppingCart,
  Scan,
  UserPlus,
  CreditCard,
  Banknote,
  Building,
  CheckCircle2,
  Trash2,
  Edit2,
  Search,
  AlertTriangle,
  Receipt,
  RotateCcw
} from 'lucide-react';

interface PaymentRow {
  method: number; // 1 = Cash, 2 = Card, 3 = BankTransfer
  amount: number;
  cashTendered?: number;
  referenceNumber?: string;
  cardType?: string;
  cardLast4?: string;
  bankName?: string;
  notes?: string;
}

export default function PosTerminalPage() {
  const { user } = useAuth();
  const { activeBranch } = useBranch();

  // Shift & Cashier State
  const [activeShift, setActiveShift] = useState<Shift | null>(null);
  const [isLoadingShift, setIsLoadingShift] = useState(true);

  // Customer Search & Quick Add
  const [customerSearchQuery, setCustomerSearchQuery] = useState('');
  const [customerSearchResults, setCustomerSearchResults] = useState<Customer[]>([]);
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [isSearchingCustomers, setIsSearchingCustomers] = useState(false);
  const [showQuickAddModal, setShowQuickAddModal] = useState(false);
  const [quickCustomer, setQuickCustomer] = useState({
    fullName: '',
    primaryPhone: '',
    email: '',
    city: '',
    notes: ''
  });

  // Current POS Invoice State
  const [currentInvoice, setCurrentInvoice] = useState<PosInvoice | null>(null);
  const [isLoadingInvoice, setIsLoadingInvoice] = useState(false);

  // Scan & Item Lookup
  const [scanQuery, setScanQuery] = useState('');
  const [isLookingUp, setIsLookingUp] = useState(false);
  const [scannedItem, setScannedItem] = useState<PosItemLookupResult | null>(null);
  const [negotiatedPriceInput, setNegotiatedPriceInput] = useState<string>('');

  // Editing line price modal
  const [editingLineId, setEditingLineId] = useState<string | null>(null);
  const [editPriceValue, setEditPriceValue] = useState<string>('');

  // Payments & Finalization
  const [payments, setPayments] = useState<PaymentRow[]>([
    { method: 1, amount: 0, cashTendered: 0 }
  ]);
  const [discountAmount, setDiscountAmount] = useState<number>(0);
  const [invoiceNotes, setInvoiceNotes] = useState('');
  const [isFinalizing, setIsFinalizing] = useState(false);
  const [finalizedSuccess, setFinalizedSuccess] = useState<PosInvoice | null>(null);

  // General Messages
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [infoMsg, setInfoMsg] = useState<string | null>(null);

  // Load Active Shift for the current user and branch
  useEffect(() => {
    async function loadActiveShift() {
      if (!activeBranch) return;
      setIsLoadingShift(true);
      setErrorMsg(null);
      try {
        const shiftsRes = await shiftsApi.getShifts({
          branchId: activeBranch.id,
          status: 1
        });
        const shifts = shiftsRes.items || [];
        // Find shift for current user or first open shift
        const myShift = shifts.find(s => s.cashierId === user?.id && s.status === 1) || shifts[0] || null;
        setActiveShift(myShift);
      } catch (err: any) {
        console.error('Failed to load active shift:', err);
      } finally {
        setIsLoadingShift(false);
      }
    }

    loadActiveShift();
  }, [activeBranch, user]);

  // Handle Customer Search
  useEffect(() => {
    if (!customerSearchQuery.trim() || customerSearchQuery.length < 2) {
      setCustomerSearchResults([]);
      return;
    }

    const timer = setTimeout(async () => {
      setIsSearchingCustomers(true);
      try {
        const res = await customerApi.getCustomers({ search: customerSearchQuery, pageSize: 5 });
        setCustomerSearchResults(res.items || []);
      } catch (err) {
        console.error('Customer search failed:', err);
      } finally {
        setIsSearchingCustomers(false);
      }
    }, 300);

    return () => clearTimeout(timer);
  }, [customerSearchQuery]);

  // Item Lookup (Enter key or button)
  const handleItemLookup = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    if (!activeShift) {
      setErrorMsg('An active open shift is required to lookup items and initiate sales.');
      return;
    }
    if (!scanQuery.trim()) return;

    setIsLookingUp(true);
    setErrorMsg(null);
    setScannedItem(null);
    try {
      const item = await posApi.lookupItem(activeShift.id, scanQuery.trim());
      setScannedItem(item);
      setNegotiatedPriceInput(String(item.referencePrice));
    } catch (err: any) {
      setErrorMsg(err.message || 'Item lookup failed.');
    } finally {
      setIsLookingUp(false);
    }
  };

  // Add Item to Draft Invoice
  const handleAddItemToInvoice = async () => {
    if (!scannedItem || !activeShift) return;

    const price = parseFloat(negotiatedPriceInput);
    if (isNaN(price) || price < 0) {
      setErrorMsg('Please enter a valid negotiated selling price.');
      return;
    }

    setErrorMsg(null);
    setIsLoadingInvoice(true);
    try {
      let draft = currentInvoice;

      // Create draft if not exists
      if (!draft) {
        if (!selectedCustomer) {
          setErrorMsg('Please select or quick-create a customer before adding items to sale.');
          setIsLoadingInvoice(false);
          return;
        }

        draft = await posApi.createDraft({
          shiftId: activeShift.id,
          customerId: selectedCustomer.id,
          notes: invoiceNotes
        });
        setCurrentInvoice(draft);
      }

      // Add line to draft
      const updated = await posApi.addLine(draft.id, {
        itemBarcodeOrTag: scannedItem.tagNumber,
        negotiatedUnitPrice: price
      });

      setCurrentInvoice(updated);
      setScannedItem(null);
      setScanQuery('');
      setNegotiatedPriceInput('');

      // Auto-update first payment row with total if single payment
      if (payments.length === 1 && payments[0].amount === 0) {
        setPayments([{ ...payments[0], amount: updated.totalAmount, cashTendered: updated.totalAmount }]);
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to add item to invoice.');
    } finally {
      setIsLoadingInvoice(false);
    }
  };

  // Quick Customer Create
  const handleQuickCustomerSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!activeShift) {
      setErrorMsg('Active shift required.');
      return;
    }
    if (!quickCustomer.fullName.trim() || !quickCustomer.primaryPhone.trim()) {
      setErrorMsg('Customer Name and Primary Phone are mandatory.');
      return;
    }

    setErrorMsg(null);
    setIsLoadingInvoice(true);
    try {
      const inv = await posApi.quickCustomerDraft({
        shiftId: activeShift.id,
        fullName: quickCustomer.fullName.trim(),
        primaryPhone: quickCustomer.primaryPhone.trim(),
        email: quickCustomer.email.trim() || undefined,
        city: quickCustomer.city.trim() || undefined,
        notes: quickCustomer.notes.trim() || undefined
      });

      setCurrentInvoice(inv);
      setSelectedCustomer({
        id: inv.customerId,
        customerCode: inv.customerCode || 'NEW',
        fullName: inv.customerName,
        primaryPhone: inv.customerPhone,
        normalizedPrimaryPhone: inv.customerPhone,
        country: 'LK',
        isActive: true,
        rowVersion: '',
        createdAt: inv.createdAt
      });
      setShowQuickAddModal(false);
      setQuickCustomer({ fullName: '', primaryPhone: '', email: '', city: '', notes: '' });
      setInfoMsg(`Walk-in customer ${inv.customerName} registered and draft invoice created.`);
    } catch (err: any) {
      setErrorMsg(err.message || 'Quick customer registration failed.');
    } finally {
      setIsLoadingInvoice(false);
    }
  };

  // Line Price Update
  const handleUpdateLinePrice = async () => {
    if (!currentInvoice || !editingLineId) return;
    const price = parseFloat(editPriceValue);
    if (isNaN(price) || price < 0) {
      setErrorMsg('Price must be greater than or equal to 0.');
      return;
    }

    setIsLoadingInvoice(true);
    try {
      const updated = await posApi.updateLine(currentInvoice.id, editingLineId, {
        negotiatedUnitPrice: price
      });
      setCurrentInvoice(updated);
      setEditingLineId(null);
      setEditPriceValue('');
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to update line price.');
    } finally {
      setIsLoadingInvoice(false);
    }
  };

  // Line Removal
  const handleRemoveLine = async (lineId: string) => {
    if (!currentInvoice) return;
    setIsLoadingInvoice(true);
    try {
      const updated = await posApi.removeLine(currentInvoice.id, lineId);
      setCurrentInvoice(updated);
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to remove line.');
    } finally {
      setIsLoadingInvoice(false);
    }
  };

  // Payment Rows Management
  const addPaymentRow = () => {
    const currentTotalPaid = payments.reduce((acc, p) => acc + (p.amount || 0), 0);
    const remaining = Math.max(0, (currentInvoice?.totalAmount || 0) - currentTotalPaid);
    setPayments([...payments, { method: 2, amount: remaining }]);
  };

  const removePaymentRow = (index: number) => {
    if (payments.length === 1) return;
    setPayments(payments.filter((_, i) => i !== index));
  };

  const updatePaymentRow = (index: number, updates: Partial<PaymentRow>) => {
    const next = [...payments];
    next[index] = { ...next[index], ...updates };
    setPayments(next);
  };

  // Finalize Invoice
  const handleFinalize = async () => {
    if (!currentInvoice) return;
    if (currentInvoice.lines.length === 0) {
      setErrorMsg('Cannot finalize an empty invoice.');
      return;
    }

    const totalPaid = payments.reduce((acc, p) => acc + (p.amount || 0), 0);
    const invoiceTotal = (currentInvoice.subTotal - discountAmount);

    if (totalPaid !== invoiceTotal) {
      setErrorMsg(`Accepted payments (Rs. ${totalPaid.toLocaleString()}) must equal the final invoice total (Rs. ${invoiceTotal.toLocaleString()}).`);
      return;
    }

    setIsFinalizing(true);
    setErrorMsg(null);
    try {
      const finalized = await posApi.finalizeInvoice(currentInvoice.id, {
        payments,
        discountAmount: discountAmount > 0 ? discountAmount : undefined,
        notes: invoiceNotes || undefined
      });

      setFinalizedSuccess(finalized);
      setCurrentInvoice(null);
      setSelectedCustomer(null);
      setPayments([{ method: 1, amount: 0, cashTendered: 0 }]);
      setDiscountAmount(0);
      setInvoiceNotes('');
      setInfoMsg(`Invoice ${finalized.invoiceNumber} finalized successfully!`);
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to finalize invoice.');
    } finally {
      setIsFinalizing(false);
    }
  };

  // Calculations
  const invoiceSubtotal = currentInvoice?.subTotal || 0;
  const invoiceTotal = Math.max(0, invoiceSubtotal - discountAmount);
  const totalPaid = payments.reduce((sum, p) => sum + (Number(p.amount) || 0), 0);
  const balanceDue = Math.max(0, invoiceTotal - totalPaid);
  const isFullyPaid = invoiceTotal > 0 && totalPaid === invoiceTotal;

  return (
    <AppLayout>
      <div className="space-y-6">
        {/* Terminal Header */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white p-5 rounded-2xl border border-gray-200 shadow-sm">
          <div>
            <div className="flex items-center space-x-2">
              <span className="p-2 rounded-xl bg-[#0D3826] text-[#D4AF37]">
                <Receipt className="w-5 h-5" />
              </span>
              <h1 className="text-xl font-black text-[#0D3826] tracking-tight">Point of Sale Terminal</h1>
              <span className="px-2 py-0.5 rounded-full text-[11px] font-bold bg-[#D4AF37]/20 text-[#0D3826] border border-[#D4AF37]/40">
                Normal Sales
              </span>
            </div>
            <p className="text-xs text-gray-500 mt-1">
              Authoritative Jewellery POS • Dynamic Reference Pricing • Split Payments • Cash Ledger Linked
            </p>
          </div>

          {/* Shift & Branch Status Badge */}
          <div className="flex items-center gap-3">
            {isLoadingShift ? (
              <div className="text-xs text-gray-400">Verifying cashier shift...</div>
            ) : activeShift ? (
              <div className="flex items-center gap-2 bg-emerald-50 border border-emerald-200 text-emerald-800 px-3 py-1.5 rounded-xl text-xs font-bold">
                <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
                <span>Shift {activeShift.shiftNumber} (Open)</span>
                <span className="text-gray-400">|</span>
                <span>Drawer: {activeShift.cashDrawerName || 'Drawer'}</span>
              </div>
            ) : (
              <div className="flex items-center gap-2 bg-rose-50 border border-rose-200 text-rose-800 px-3 py-1.5 rounded-xl text-xs font-bold">
                <AlertTriangle className="w-4 h-4 text-rose-600" />
                <span>No Active Open Shift</span>
                <a href="/shifts" className="underline ml-1 text-rose-900">Open Shift</a>
              </div>
            )}
          </div>
        </div>

        {/* Global Notifications */}
        {errorMsg && (
          <div className="bg-rose-50 border border-rose-200 text-rose-800 p-4 rounded-xl text-xs flex items-start justify-between shadow-sm">
            <div className="flex items-center space-x-2">
              <AlertTriangle className="w-4 h-4 text-rose-600 flex-shrink-0" />
              <span>{errorMsg}</span>
            </div>
            <button onClick={() => setErrorMsg(null)} className="text-rose-500 hover:text-rose-700 font-bold">✕</button>
          </div>
        )}

        {infoMsg && (
          <div className="bg-emerald-50 border border-emerald-200 text-emerald-800 p-4 rounded-xl text-xs flex items-start justify-between shadow-sm">
            <div className="flex items-center space-x-2">
              <CheckCircle2 className="w-4 h-4 text-emerald-600 flex-shrink-0" />
              <span>{infoMsg}</span>
            </div>
            <button onClick={() => setInfoMsg(null)} className="text-emerald-500 hover:text-emerald-700 font-bold">✕</button>
          </div>
        )}

        {/* Finalized Success Banner */}
        {finalizedSuccess && (
          <div className="bg-gradient-to-r from-[#0D3826] to-[#1B6543] text-white p-6 rounded-2xl shadow-lg border border-[#D4AF37]/40 flex flex-col md:flex-row items-center justify-between gap-4">
            <div className="flex items-center space-x-4">
              <div className="w-12 h-12 rounded-full bg-[#D4AF37] text-[#0D3826] flex items-center justify-center font-black text-xl">
                ✓
              </div>
              <div>
                <span className="text-[10px] uppercase font-bold tracking-widest text-[#D4AF37]">Invoice Finalized</span>
                <h3 className="text-xl font-black">{finalizedSuccess.invoiceNumber}</h3>
                <p className="text-xs text-gray-200 mt-0.5">
                  Customer: <strong>{finalizedSuccess.customerName}</strong> • Total Paid: <strong>Rs. {finalizedSuccess.totalAmount.toLocaleString()}</strong> ({finalizedSuccess.payments.length} payment records)
                </p>
              </div>
            </div>
            <button
              onClick={() => setFinalizedSuccess(null)}
              className="px-4 py-2 bg-[#D4AF37] hover:bg-[#BA9225] text-[#0D3826] font-bold text-xs rounded-xl transition"
            >
              Start New Sale
            </button>
          </div>
        )}

        {/* Main POS Interface Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Left Column (7 cols): Customer + Barcode Scan + Cart Lines */}
          <div className="lg:col-span-7 space-y-5">
            {/* 1. Customer Selection Bar */}
            <div className="bg-white p-4 rounded-2xl border border-gray-200 shadow-sm space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-gray-500 uppercase tracking-wider flex items-center gap-1.5">
                  <Search className="w-3.5 h-3.5" /> Customer Identification
                </span>
                <button
                  type="button"
                  onClick={() => setShowQuickAddModal(true)}
                  className="inline-flex items-center gap-1 text-xs font-bold text-[#0D3826] hover:text-emerald-700 hover:underline"
                >
                  <UserPlus className="w-3.5 h-3.5" /> Quick Walk-in Customer
                </button>
              </div>

              {selectedCustomer ? (
                <div className="flex items-center justify-between bg-emerald-50/70 border border-emerald-200 p-3 rounded-xl">
                  <div className="flex items-center space-x-3">
                    <div className="w-8 h-8 rounded-full bg-[#0D3826] text-white flex items-center justify-center font-bold text-xs">
                      {selectedCustomer.fullName.charAt(0)}
                    </div>
                    <div>
                      <p className="text-xs font-bold text-gray-900">{selectedCustomer.fullName}</p>
                      <p className="text-[11px] text-gray-500 font-mono">
                        {selectedCustomer.primaryPhone} {selectedCustomer.customerCode ? `• ${selectedCustomer.customerCode}` : ''}
                      </p>
                    </div>
                  </div>
                  {!currentInvoice && (
                    <button
                      type="button"
                      onClick={() => setSelectedCustomer(null)}
                      className="text-gray-400 hover:text-rose-600 text-xs font-bold"
                    >
                      Change
                    </button>
                  )}
                </div>
              ) : (
                <div className="relative">
                  <input
                    type="text"
                    value={customerSearchQuery}
                    onChange={(e) => setCustomerSearchQuery(e.target.value)}
                    placeholder="Search customer by name or phone (e.g. 077 123 4567)..."
                    className="w-full text-xs px-3.5 py-2.5 rounded-xl border border-gray-300 focus:outline-none focus:ring-2 focus:ring-[#0D3826]/30 font-medium"
                  />
                  {isSearchingCustomers && (
                    <span className="absolute right-3 top-2.5 text-xs text-gray-400 animate-pulse">Searching...</span>
                  )}
                  {customerSearchResults.length > 0 && (
                    <div className="absolute left-0 right-0 mt-1 bg-white border border-gray-200 rounded-xl shadow-lg z-20 max-h-48 overflow-y-auto">
                      {customerSearchResults.map((c) => (
                        <div
                          key={c.id}
                          onClick={() => {
                            setSelectedCustomer(c);
                            setCustomerSearchQuery('');
                            setCustomerSearchResults([]);
                          }}
                          className="p-2.5 hover:bg-gray-50 cursor-pointer border-b border-gray-100 flex items-center justify-between text-xs"
                        >
                          <div>
                            <p className="font-bold text-gray-900">{c.fullName}</p>
                            <p className="text-[11px] text-gray-500 font-mono">{c.primaryPhone}</p>
                          </div>
                          <span className="text-[10px] font-mono text-gray-400">{c.customerCode}</span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* 2. Item Scan & Dynamic Pricing Card */}
            <div className="bg-white p-4 rounded-2xl border border-gray-200 shadow-sm space-y-4">
              <span className="text-[11px] font-bold text-gray-500 uppercase tracking-wider flex items-center gap-1.5">
                <Scan className="w-3.5 h-3.5" /> Barcode & Tag Scan
              </span>

              <form onSubmit={handleItemLookup} className="flex gap-2">
                <input
                  type="text"
                  value={scanQuery}
                  onChange={(e) => setScanQuery(e.target.value)}
                  placeholder="Scan item barcode or enter tag number (e.g. TAG-COL-2026-0001)..."
                  disabled={!activeShift || isLookingUp}
                  className="flex-1 text-xs px-3.5 py-2.5 rounded-xl border border-gray-300 focus:outline-none focus:ring-2 focus:ring-[#0D3826]/30 font-mono"
                />
                <button
                  type="submit"
                  disabled={!activeShift || isLookingUp || !scanQuery.trim()}
                  className="px-4 py-2.5 bg-[#0D3826] hover:bg-[#1B6543] text-white rounded-xl text-xs font-bold transition disabled:opacity-50"
                >
                  {isLookingUp ? 'Scanning...' : 'Lookup Item'}
                </button>
              </form>

              {/* Scanned Item Reference Price Breakdown */}
              {scannedItem && (
                <div className="bg-slate-50 border border-slate-200 rounded-xl p-4 space-y-3 animate-fadeIn">
                  <div className="flex items-start justify-between">
                    <div>
                      <span className="text-[10px] font-bold uppercase tracking-wider text-[#D4AF37] bg-[#0D3826] px-2 py-0.5 rounded">
                        {scannedItem.purityName} {scannedItem.materialName}
                      </span>
                      <h4 className="text-sm font-black text-gray-900 mt-1">{scannedItem.itemName}</h4>
                      <p className="text-[11px] font-mono text-gray-500">
                        Tag: {scannedItem.tagNumber} {scannedItem.barcode ? `• Barcode: ${scannedItem.barcode}` : ''}
                      </p>
                    </div>
                    <div className="text-right">
                      <span className="text-[10px] font-bold text-gray-400 uppercase">System Reference</span>
                      <p className="text-base font-black text-emerald-700">
                        Rs. {scannedItem.referencePrice.toLocaleString()}
                      </p>
                    </div>
                  </div>

                  {/* Weights & Breakdown Grid */}
                  <div className="grid grid-cols-4 gap-2 pt-2 border-t border-gray-200 text-[11px]">
                    <div>
                      <span className="text-gray-400 block">Net Wt</span>
                      <strong className="font-mono text-gray-800">{scannedItem.netWeight.toFixed(3)}g</strong>
                    </div>
                    <div>
                      <span className="text-gray-400 block">Rate/g</span>
                      <strong className="font-mono text-gray-800">Rs. {scannedItem.activeRatePerGram.toLocaleString()}</strong>
                    </div>
                    <div>
                      <span className="text-gray-400 block">Gold Value</span>
                      <strong className="font-mono text-gray-800">Rs. {scannedItem.referenceMaterialValue.toLocaleString()}</strong>
                    </div>
                    <div>
                      <span className="text-gray-400 block">Making Chg</span>
                      <strong className="font-mono text-gray-800">Rs. {scannedItem.referenceMakingCharge.toLocaleString()}</strong>
                    </div>
                  </div>

                  {/* Composition Alert */}
                  {scannedItem.isPartOfComposition && (
                    <div className="bg-amber-50 border border-amber-200 text-amber-800 px-3 py-1.5 rounded-lg text-[11px] flex items-center gap-1.5 font-medium">
                      <AlertTriangle className="w-3.5 h-3.5 text-amber-600 flex-shrink-0" />
                      <span>
                        Part of <strong>{scannedItem.compositionType}</strong> ({scannedItem.compositionTagNumber}). Selling this item will automatically detach it from the composition.
                      </span>
                    </div>
                  )}

                  {/* Negotiated Selling Price Input */}
                  <div className="pt-2 border-t border-gray-200 flex items-center justify-between gap-3">
                    <div className="flex-1">
                      <label className="block text-[11px] font-bold text-gray-700 mb-1">
                        Actual Negotiated Selling Price (Rs.)
                      </label>
                      <input
                        type="number"
                        step="0.01"
                        value={negotiatedPriceInput}
                        onChange={(e) => setNegotiatedPriceInput(e.target.value)}
                        placeholder="Negotiated Price"
                        className="w-full text-sm font-black px-3 py-1.5 rounded-lg border border-gray-300 focus:outline-none focus:ring-2 focus:ring-[#0D3826]/30"
                      />
                    </div>
                    <button
                      type="button"
                      onClick={handleAddItemToInvoice}
                      disabled={isLoadingInvoice}
                      className="mt-5 px-5 py-2 bg-[#D4AF37] hover:bg-[#BA9225] text-[#0D3826] font-black text-xs rounded-xl transition shadow-sm"
                    >
                      Add To Sale
                    </button>
                  </div>
                </div>
              )}
            </div>

            {/* 3. Invoice Items Cart Table */}
            <div className="bg-white rounded-2xl border border-gray-200 shadow-sm overflow-hidden">
              <div className="p-4 border-b border-gray-100 flex items-center justify-between">
                <span className="text-[11px] font-bold text-gray-500 uppercase tracking-wider flex items-center gap-1.5">
                  <ShoppingCart className="w-3.5 h-3.5" /> Cart Items ({currentInvoice?.lines.length || 0})
                </span>
                {currentInvoice && (
                  <span className="text-xs font-mono font-bold text-gray-600">
                    Draft: {currentInvoice.invoiceNumber}
                  </span>
                )}
              </div>

              {!currentInvoice || currentInvoice.lines.length === 0 ? (
                <div className="p-8 text-center text-gray-400 text-xs">
                  No items added to current sale yet. Scan a barcode or lookup a tag to begin.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs">
                    <thead className="bg-gray-50 text-[10px] font-bold uppercase text-gray-500 border-b border-gray-200">
                      <tr>
                        <th className="p-3">Item / Tag</th>
                        <th className="p-3">Net Wt</th>
                        <th className="p-3 text-right">Reference</th>
                        <th className="p-3 text-right">Negotiated Price</th>
                        <th className="p-3 text-center">Action</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {currentInvoice.lines.map((l) => (
                        <tr key={l.id} className="hover:bg-gray-50/50">
                          <td className="p-3">
                            <p className="font-bold text-gray-900">{l.itemName}</p>
                            <p className="text-[11px] font-mono text-gray-400">{l.tagNumber}</p>
                          </td>
                          <td className="p-3 font-mono text-gray-600">{l.netWeight.toFixed(3)}g</td>
                          <td className="p-3 text-right font-mono text-gray-400">
                            Rs. {l.referencePrice.toLocaleString()}
                          </td>
                          <td className="p-3 text-right font-mono font-black text-gray-900">
                            Rs. {l.unitPrice.toLocaleString()}
                          </td>
                          <td className="p-3 text-center space-x-2">
                            <button
                              type="button"
                              onClick={() => {
                                setEditingLineId(l.id);
                                setEditPriceValue(String(l.unitPrice));
                              }}
                              className="text-gray-400 hover:text-emerald-700 font-bold"
                              title="Edit Price"
                            >
                              <Edit2 className="w-3.5 h-3.5 inline" />
                            </button>
                            <button
                              type="button"
                              onClick={() => handleRemoveLine(l.id)}
                              className="text-gray-400 hover:text-rose-600 font-bold"
                              title="Remove Item"
                            >
                              <Trash2 className="w-3.5 h-3.5 inline" />
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>

          {/* Right Column (5 cols): Invoice Summary & Split Payments */}
          <div className="lg:col-span-5 space-y-5">
            <div className="bg-white p-5 rounded-2xl border border-gray-200 shadow-sm space-y-5">
              <h3 className="text-xs font-bold text-gray-500 uppercase tracking-wider">
                Invoice Settlement & Split Payments
              </h3>

              {/* Summary Financial Figures */}
              <div className="space-y-2 bg-slate-50 p-4 rounded-xl border border-slate-200 text-xs">
                <div className="flex justify-between text-gray-600">
                  <span>Subtotal ({currentInvoice?.lines.length || 0} items)</span>
                  <span className="font-mono font-bold">Rs. {invoiceSubtotal.toLocaleString()}</span>
                </div>

                <div className="flex justify-between items-center text-gray-600">
                  <span>Overall Discount (Rs.)</span>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={discountAmount || ''}
                    onChange={(e) => setDiscountAmount(Math.max(0, parseFloat(e.target.value) || 0))}
                    placeholder="0.00"
                    className="w-28 text-right font-mono text-xs px-2 py-1 rounded border border-gray-300"
                  />
                </div>

                <div className="flex justify-between text-gray-400 text-[11px]">
                  <span>VAT Rate (Flat %)</span>
                  <span>0% (Exempt/Disabled)</span>
                </div>

                <div className="pt-2 border-t border-gray-200 flex justify-between text-sm font-black text-[#0D3826]">
                  <span>Total Amount Due</span>
                  <span className="font-mono text-base text-emerald-800">
                    Rs. {invoiceTotal.toLocaleString()}
                  </span>
                </div>
              </div>

              {/* Split Payments Container */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-[11px] font-bold text-gray-500 uppercase tracking-wider">
                    Accepted Payments
                  </span>
                  <button
                    type="button"
                    onClick={addPaymentRow}
                    className="text-xs font-bold text-[#0D3826] hover:underline"
                  >
                    + Add Payment Method
                  </button>
                </div>

                {payments.map((p, idx) => (
                  <div key={idx} className="p-3 bg-gray-50 border border-gray-200 rounded-xl space-y-2">
                    <div className="flex items-center justify-between gap-2">
                      <select
                        value={p.method}
                        onChange={(e) => updatePaymentRow(idx, { method: Number(e.target.value) })}
                        className="text-xs font-bold bg-white px-2.5 py-1.5 rounded-lg border border-gray-300 focus:outline-none"
                      >
                        <option value={1}>💵 Cash</option>
                        <option value={2}>💳 Card</option>
                        <option value={3}>🏦 Bank Transfer</option>
                      </select>

                      <div className="flex-1">
                        <input
                          type="number"
                          step="0.01"
                          value={p.amount || ''}
                          onChange={(e) => updatePaymentRow(idx, { amount: parseFloat(e.target.value) || 0 })}
                          placeholder="Amount"
                          className="w-full text-right font-mono font-black text-xs px-2.5 py-1.5 rounded-lg border border-gray-300"
                        />
                      </div>

                      {payments.length > 1 && (
                        <button
                          type="button"
                          onClick={() => removePaymentRow(idx)}
                          className="text-gray-400 hover:text-rose-600 font-bold text-xs"
                        >
                          ✕
                        </button>
                      )}
                    </div>

                    {/* Method-Specific Inputs */}
                    {p.method === 1 && (
                      <div className="flex items-center justify-between text-[11px] bg-white p-2 rounded-lg border border-gray-100">
                        <span className="text-gray-500 font-medium">Cash Tendered:</span>
                        <input
                          type="number"
                          step="0.01"
                          value={p.cashTendered || ''}
                          onChange={(e) => updatePaymentRow(idx, { cashTendered: parseFloat(e.target.value) || 0 })}
                          placeholder="Tendered"
                          className="w-24 text-right font-mono text-xs px-1.5 py-0.5 rounded border border-gray-300"
                        />
                        <span className="text-gray-500 ml-2">
                          Change Due: <strong className="text-emerald-800 font-mono">Rs. {Math.max(0, (p.cashTendered || 0) - (p.amount || 0)).toLocaleString()}</strong>
                        </span>
                      </div>
                    )}

                    {p.method === 2 && (
                      <div className="grid grid-cols-3 gap-2 text-[11px]">
                        <input
                          type="text"
                          value={p.cardType || ''}
                          onChange={(e) => updatePaymentRow(idx, { cardType: e.target.value })}
                          placeholder="Card Type (Visa)"
                          className="px-2 py-1 rounded border border-gray-300 text-xs"
                        />
                        <input
                          type="text"
                          maxLength={4}
                          value={p.cardLast4 || ''}
                          onChange={(e) => updatePaymentRow(idx, { cardLast4: e.target.value })}
                          placeholder="Last 4 Digits"
                          className="px-2 py-1 rounded border border-gray-300 text-xs font-mono"
                        />
                        <input
                          type="text"
                          value={p.referenceNumber || ''}
                          onChange={(e) => updatePaymentRow(idx, { referenceNumber: e.target.value })}
                          placeholder="Approval Code"
                          className="px-2 py-1 rounded border border-gray-300 text-xs font-mono"
                        />
                      </div>
                    )}

                    {p.method === 3 && (
                      <div className="grid grid-cols-2 gap-2 text-[11px]">
                        <input
                          type="text"
                          value={p.bankName || ''}
                          onChange={(e) => updatePaymentRow(idx, { bankName: e.target.value })}
                          placeholder="Bank Name (BOC/Commercial)"
                          className="px-2 py-1 rounded border border-gray-300 text-xs"
                        />
                        <input
                          type="text"
                          value={p.referenceNumber || ''}
                          onChange={(e) => updatePaymentRow(idx, { referenceNumber: e.target.value })}
                          placeholder="Transaction Ref #"
                          className="px-2 py-1 rounded border border-gray-300 text-xs font-mono"
                        />
                      </div>
                    )}
                  </div>
                ))}

                {/* Balance Status */}
                <div className="pt-2 flex items-center justify-between text-xs">
                  <span className="text-gray-500 font-medium">Total Payments Accepted:</span>
                  <span className="font-mono font-bold text-gray-800">Rs. {totalPaid.toLocaleString()}</span>
                </div>

                <div className="flex items-center justify-between text-xs">
                  <span className="text-gray-500 font-medium">Remaining Balance Due:</span>
                  <span className={`font-mono font-black ${balanceDue > 0 ? 'text-rose-600' : 'text-emerald-700'}`}>
                    Rs. {balanceDue.toLocaleString()}
                  </span>
                </div>
              </div>

              {/* Notes Input */}
              <div>
                <label className="block text-[11px] font-bold text-gray-600 mb-1">Invoice Notes</label>
                <input
                  type="text"
                  value={invoiceNotes}
                  onChange={(e) => setInvoiceNotes(e.target.value)}
                  placeholder="Optional notes or remarks..."
                  className="w-full text-xs px-3 py-2 rounded-xl border border-gray-300"
                />
              </div>

              {/* Finalize Button */}
              <button
                type="button"
                onClick={handleFinalize}
                disabled={!isFullyPaid || isFinalizing || !currentInvoice || currentInvoice.lines.length === 0}
                className="w-full py-3.5 bg-gradient-to-r from-[#0D3826] to-[#1B6543] hover:from-[#114630] hover:to-[#227a52] text-white font-black text-sm rounded-xl transition shadow-md disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center space-x-2"
              >
                {isFinalizing ? (
                  <span>Finalizing & Posting Cash Ledger...</span>
                ) : (
                  <>
                    <CheckCircle2 className="w-4 h-4 text-[#D4AF37]" />
                    <span>Complete Sale & Issue Invoice</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>

        {/* Quick Add Walk-in Customer Modal */}
        {showQuickAddModal && (
          <div className="fixed inset-0 bg-black/50 backdrop-blur-xs flex items-center justify-center z-50 p-4">
            <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-xl border border-gray-200 animate-scaleIn">
              <div className="flex items-center justify-between pb-3 border-b border-gray-100">
                <h3 className="text-sm font-bold text-[#0D3826]">Quick Register Walk-in Customer</h3>
                <button
                  type="button"
                  onClick={() => setShowQuickAddModal(false)}
                  className="text-gray-400 hover:text-gray-600 font-bold"
                >
                  ✕
                </button>
              </div>

              <form onSubmit={handleQuickCustomerSubmit} className="space-y-4 pt-4 text-xs">
                <div>
                  <label className="block font-bold text-gray-700 mb-1">Full Name *</label>
                  <input
                    type="text"
                    required
                    value={quickCustomer.fullName}
                    onChange={(e) => setQuickCustomer({ ...quickCustomer, fullName: e.target.value })}
                    placeholder="e.g. Kasun Perera"
                    className="w-full px-3 py-2 rounded-xl border border-gray-300"
                  />
                </div>

                <div>
                  <label className="block font-bold text-gray-700 mb-1">Primary Phone *</label>
                  <input
                    type="text"
                    required
                    value={quickCustomer.primaryPhone}
                    onChange={(e) => setQuickCustomer({ ...quickCustomer, primaryPhone: e.target.value })}
                    placeholder="e.g. 077 123 4567"
                    className="w-full px-3 py-2 rounded-xl border border-gray-300 font-mono"
                  />
                </div>

                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block font-bold text-gray-700 mb-1">Email (Optional)</label>
                    <input
                      type="email"
                      value={quickCustomer.email}
                      onChange={(e) => setQuickCustomer({ ...quickCustomer, email: e.target.value })}
                      placeholder="kasun@example.com"
                      className="w-full px-3 py-2 rounded-xl border border-gray-300"
                    />
                  </div>
                  <div>
                    <label className="block font-bold text-gray-700 mb-1">City (Optional)</label>
                    <input
                      type="text"
                      value={quickCustomer.city}
                      onChange={(e) => setQuickCustomer({ ...quickCustomer, city: e.target.value })}
                      placeholder="Colombo"
                      className="w-full px-3 py-2 rounded-xl border border-gray-300"
                    />
                  </div>
                </div>

                <div className="pt-3 border-t border-gray-100 flex justify-end space-x-2">
                  <button
                    type="button"
                    onClick={() => setShowQuickAddModal(false)}
                    className="px-4 py-2 border border-gray-300 rounded-xl text-gray-700 font-bold"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    className="px-5 py-2 bg-[#0D3826] hover:bg-[#1B6543] text-white font-bold rounded-xl"
                  >
                    Register & Start Sale
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}

        {/* Edit Price Modal */}
        {editingLineId && (
          <div className="fixed inset-0 bg-black/50 backdrop-blur-xs flex items-center justify-center z-50 p-4">
            <div className="bg-white rounded-2xl max-w-sm w-full p-5 shadow-xl border border-gray-200 space-y-4">
              <h4 className="text-xs font-bold text-gray-700 uppercase tracking-wider">
                Adjust Negotiated Price
              </h4>
              <input
                type="number"
                step="0.01"
                value={editPriceValue}
                onChange={(e) => setEditPriceValue(e.target.value)}
                className="w-full text-base font-black px-3 py-2 rounded-xl border border-gray-300 font-mono"
              />
              <div className="flex justify-end space-x-2 text-xs">
                <button
                  type="button"
                  onClick={() => setEditingLineId(null)}
                  className="px-3 py-1.5 border border-gray-300 rounded-lg font-bold text-gray-600"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleUpdateLinePrice}
                  className="px-4 py-1.5 bg-[#0D3826] text-white rounded-lg font-bold"
                >
                  Save Price
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
