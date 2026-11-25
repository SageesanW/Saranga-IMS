'use client';

import React, { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
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
  Shift,
} from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Button } from '@/components/ui/Button';
import { Input } from '@/components/ui/Input';
import { Select } from '@/components/ui/Select';
import { Modal } from '@/components/ui/Modal';
import { Badge } from '@/components/ui/Badge';
import { Table } from '@/components/ui/Table';
import {
  CreditCard,
  Banknote,
  Building,
  UserPlus,
  Trash2,
  Edit2,
  Search,
  AlertTriangle,
  Receipt,
  CheckCircle2,
  Scan,
  Printer,
  RotateCcw,
  Plus,
  Wallet,
  Clock,
  ArrowRight,
  Info,
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
  const barcodeInputRef = useRef<HTMLInputElement>(null);

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
    notes: '',
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
    { method: 1, amount: 0, cashTendered: 0 },
  ]);
  const [discountAmount, setDiscountAmount] = useState<number>(0);
  const [invoiceNotes, setInvoiceNotes] = useState('');
  const [isFinalizing, setIsFinalizing] = useState(false);
  const [finalizedSuccess, setFinalizedSuccess] = useState<PosInvoice | null>(null);

  // Messages
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [infoMsg, setInfoMsg] = useState<string | null>(null);

  const currency = activeBranch?.currencyCode || 'LKR';

  // Load Active Shift for the current user and branch
  useEffect(() => {
    async function loadActiveShift() {
      if (!activeBranch) return;
      setIsLoadingShift(true);
      setErrorMsg(null);
      try {
        const shiftsRes = await shiftsApi.getShifts({
          branchId: activeBranch.id,
          status: 1,
        });
        const shifts = shiftsRes.items || [];
        const myShift = shifts.find((s) => s.cashierId === user?.id && s.status === 1) || shifts[0] || null;
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

  // Focus barcode input on mount
  useEffect(() => {
    if (activeShift && !finalizedSuccess) {
      barcodeInputRef.current?.focus();
    }
  }, [activeShift, finalizedSuccess]);

  // Item Lookup
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
      setNegotiatedPriceInput(item.referencePrice.toFixed(2));
    } catch (err: any) {
      setErrorMsg(err.message || 'Item lookup failed. Verify barcode or tag number.');
    } finally {
      setIsLookingUp(false);
    }
  };

  // Ensure Draft Exists with Selected Customer
  const ensureDraftInvoice = async (customer: Customer): Promise<PosInvoice> => {
    if (currentInvoice && currentInvoice.customerId === customer.id) {
      return currentInvoice;
    }

    if (!activeShift) {
      throw new Error('No active shift found. Open a shift before creating transactions.');
    }

    setIsLoadingInvoice(true);
    try {
      const invoice = await posApi.createDraft({
        customerId: customer.id,
        shiftId: activeShift.id,
        notes: 'POS Walk-in Sale',
      });
      setCurrentInvoice(invoice);
      return invoice;
    } finally {
      setIsLoadingInvoice(false);
    }
  };

  // Add Item to Cart
  const handleAddToCart = async () => {
    if (!scannedItem) return;
    if (!selectedCustomer) {
      setErrorMsg('Please select or create a customer before adding items to cart.');
      return;
    }

    const price = parseFloat(negotiatedPriceInput);
    if (isNaN(price) || price < 0) {
      setErrorMsg('Please specify a valid non-negative negotiated selling price.');
      return;
    }

    setErrorMsg(null);
    try {
      const invoice = await ensureDraftInvoice(selectedCustomer);
      const updated = await posApi.addLine(invoice.id, {
        itemBarcodeOrTag: scannedItem.tagNumber,
        negotiatedUnitPrice: price,
      });

      setCurrentInvoice(updated);
      setScannedItem(null);
      setScanQuery('');
      setNegotiatedPriceInput('');
      setInfoMsg(`Added ${scannedItem.itemName} (${scannedItem.tagNumber}) to cart.`);
      barcodeInputRef.current?.focus();
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to add item to invoice.');
    }
  };

  // Quick Customer Creation
  const handleQuickCreateCustomer = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!quickCustomer.fullName.trim() || !quickCustomer.primaryPhone.trim()) {
      setErrorMsg('Full name and primary phone number are required.');
      return;
    }

    setErrorMsg(null);
    try {
      const newCust = await customerApi.createCustomer({
        fullName: quickCustomer.fullName.trim(),
        primaryPhone: quickCustomer.primaryPhone.trim(),
        email: quickCustomer.email.trim() || undefined,
        city: quickCustomer.city.trim() || undefined,
        notes: quickCustomer.notes.trim() || undefined,
      });

      setSelectedCustomer(newCust);
      setShowQuickAddModal(false);
      setQuickCustomer({ fullName: '', primaryPhone: '', email: '', city: '', notes: '' });
      setInfoMsg(`Customer "${newCust.fullName}" selected.`);
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to create customer.');
    }
  };

  // Cart Line Management
  const handleRemoveLine = async (lineId: string) => {
    if (!currentInvoice) return;
    try {
      const updated = await posApi.removeLine(currentInvoice.id, lineId);
      setCurrentInvoice(updated);
      setInfoMsg('Item removed from cart.');
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to remove item.');
    }
  };

  const handleUpdateLinePrice = async () => {
    if (!currentInvoice || !editingLineId) return;
    const price = parseFloat(editPriceValue);
    if (isNaN(price) || price < 0) {
      setErrorMsg('Please specify a valid price.');
      return;
    }

    try {
      const updated = await posApi.updateLine(currentInvoice.id, editingLineId, {
        negotiatedUnitPrice: price,
      });
      setCurrentInvoice(updated);
      setEditingLineId(null);
      setEditPriceValue('');
      setInfoMsg('Negotiated price updated.');
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to update line price.');
    }
  };

  // Financial Calculations
  const linesTotal = currentInvoice?.lines.reduce((sum, l) => sum + l.lineTotal, 0) || 0;
  const netPayable = Math.max(0, linesTotal - discountAmount);

  // Auto-sync initial cash payment amount
  useEffect(() => {
    if (netPayable > 0 && payments.length === 1 && payments[0].amount === 0) {
      setPayments([{ method: 1, amount: netPayable, cashTendered: netPayable }]);
    }
  }, [netPayable, payments.length]);

  const totalTendered = payments.reduce((sum, p) => {
    if (p.method === 1) {
      return sum + (p.cashTendered !== undefined ? p.cashTendered : p.amount);
    }
    return sum + (p.amount || 0);
  }, 0);

  const totalPaidAllocation = payments.reduce((sum, p) => sum + (p.amount || 0), 0);
  const cashRow = payments.find((p) => p.method === 1);
  const changeDue =
    cashRow && (cashRow.cashTendered || 0) > cashRow.amount
      ? (cashRow.cashTendered || 0) - cashRow.amount
      : 0;

  // Checkout & Finalize Invoice
  const handleCheckout = async () => {
    if (!currentInvoice || currentInvoice.lines.length === 0) {
      setErrorMsg('Cart is empty. Add items before checking out.');
      return;
    }
    if (!activeShift) {
      setErrorMsg('Active shift required to complete checkout.');
      return;
    }
    if (totalPaidAllocation < netPayable) {
      setErrorMsg(
        `Insufficient payment allocated. Allocated: ${currency} ${totalPaidAllocation.toFixed(
          2
        )}, Total Payable: ${currency} ${netPayable.toFixed(2)}`
      );
      return;
    }

    setIsFinalizing(true);
    setErrorMsg(null);
    try {
      const checkoutPayments = payments
        .filter((p) => p.amount > 0)
        .map((p) => ({
          method: p.method,
          amount: p.amount,
          cashTendered: p.method === 1 ? p.cashTendered || p.amount : undefined,
          cardLast4: p.method === 2 ? p.cardLast4 : undefined,
          cardType: p.method === 2 ? p.cardType : undefined,
          referenceNumber: p.referenceNumber,
          bankName: p.method === 3 ? p.bankName : undefined,
          notes: p.notes,
        }));

      const finalized = await posApi.finalizeInvoice(currentInvoice.id, {
        payments: checkoutPayments,
        discountAmount,
        notes: invoiceNotes,
      });

      setFinalizedSuccess(finalized);
      setCurrentInvoice(null);
      setSelectedCustomer(null);
      setPayments([{ method: 1, amount: 0, cashTendered: 0 }]);
      setDiscountAmount(0);
      setInvoiceNotes('');
    } catch (err: any) {
      setErrorMsg(err.message || 'Checkout failed. Please verify payments.');
    } finally {
      setIsFinalizing(false);
    }
  };

  return (
    <AppLayout>
      <PageHeader
        title="POS Sales Workspace"
        description="Authoritative jewellery retail checkout with reference price verification, price negotiation, and physical cash ledger posting."
        badge={
          activeShift ? (
            <Badge variant="success" size="sm">
              Shift #{activeShift.shiftNumber} • {activeShift.cashDrawerName}
            </Badge>
          ) : (
            <Badge variant="warning" size="sm">
              No Active Shift
            </Badge>
          )
        }
        actions={
          activeShift && (
            <div className="flex items-center gap-2">
              <Link href="/shifts">
                <Button variant="secondary" size="sm" leftIcon={<Wallet className="w-4 h-4" />}>
                  Shift Details
                </Button>
              </Link>
            </div>
          )
        }
      />

      {/* No Active Shift Warning Banner */}
      {!isLoadingShift && !activeShift && (
        <div className="mb-6 p-4 rounded-xl bg-amber-50 border border-amber-200 text-amber-900 flex items-start justify-between shadow-xs">
          <div className="flex items-start gap-3">
            <Clock className="w-5 h-5 text-amber-700 flex-shrink-0 mt-0.5" />
            <div>
              <h3 className="text-sm font-bold">Shift Opening Required</h3>
              <p className="text-xs text-amber-800 mt-0.5">
                POS transactions require an authoritative active shift to post cash receipts into physical drawers.
              </p>
            </div>
          </div>
          <Link href="/shifts">
            <Button variant="primary" size="sm">
              Open Shift Now &rarr;
            </Button>
          </Link>
        </div>
      )}

      {/* Notifications */}
      {errorMsg && (
        <div className="mb-4 p-3 rounded-lg bg-rose-50 border border-rose-200 text-rose-800 text-xs flex items-center justify-between">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 text-rose-600 flex-shrink-0" />
            <span>{errorMsg}</span>
          </div>
          <button onClick={() => setErrorMsg(null)} className="text-rose-700 hover:text-rose-900">
            ✕
          </button>
        </div>
      )}

      {infoMsg && (
        <div className="mb-4 p-3 rounded-lg bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs flex items-center justify-between">
          <div className="flex items-center gap-2">
            <CheckCircle2 className="w-4 h-4 text-emerald-600 flex-shrink-0" />
            <span>{infoMsg}</span>
          </div>
          <button onClick={() => setInfoMsg(null)} className="text-emerald-700 hover:text-emerald-900">
            ✕
          </button>
        </div>
      )}

      {/* Transaction Finished Receipt Screen */}
      {finalizedSuccess ? (
        <div className="max-w-2xl mx-auto bg-white rounded-2xl border border-[#E2E6E3] p-8 shadow-md text-center">
          <div className="w-14 h-14 rounded-full bg-emerald-50 border border-emerald-200 text-emerald-700 flex items-center justify-center mx-auto mb-4">
            <CheckCircle2 className="w-8 h-8" />
          </div>
          <span className="text-xs font-bold text-[#B89A60] uppercase tracking-wider">
            Sale Finalized Successfully
          </span>
          <h2 className="text-2xl font-bold text-[#123B32] mt-1">
            Invoice {finalizedSuccess.invoiceNumber}
          </h2>
          <p className="text-xs text-[#69736E] mt-1">
            Customer: <strong className="text-[#202824]">{finalizedSuccess.customerName}</strong> • Cashier: {user?.fullName || 'Cashier'}
          </p>

          <div className="my-6 p-4 rounded-xl bg-[#F8F7F4] border border-[#E2E6E3] text-left text-xs space-y-2">
            <div className="flex justify-between py-1 border-b border-[#E2E6E3]">
              <span className="text-[#69736E]">Subtotal</span>
              <span className="font-mono font-medium text-[#202824]">
                {currency} {finalizedSuccess.subTotal.toFixed(2)}
              </span>
            </div>
            {finalizedSuccess.discountAmount > 0 && (
              <div className="flex justify-between py-1 border-b border-[#E2E6E3] text-amber-800">
                <span>Discount Applied</span>
                <span className="font-mono font-medium">
                  - {currency} {finalizedSuccess.discountAmount.toFixed(2)}
                </span>
              </div>
            )}
            <div className="flex justify-between py-1.5 font-bold text-sm text-[#123B32] border-t border-[#E2E6E3]">
              <span>Total Paid</span>
              <span className="font-mono tabular-nums">
                {currency} {finalizedSuccess.totalAmount.toFixed(2)}
              </span>
            </div>
            {finalizedSuccess.totalCashChange > 0 && (
              <div className="flex justify-between py-1 text-emerald-800 font-semibold border-t border-[#E2E6E3]">
                <span>Change Returned</span>
                <span className="font-mono tabular-nums">
                  {currency} {finalizedSuccess.totalCashChange.toFixed(2)}
                </span>
              </div>
            )}
          </div>

          <div className="flex items-center justify-center gap-3">
            <Button
              variant="secondary"
              size="md"
              leftIcon={<Printer className="w-4 h-4" />}
              onClick={() => window.print()}
            >
              Print Receipt
            </Button>
            <Button
              variant="primary"
              size="md"
              leftIcon={<Plus className="w-4 h-4" />}
              onClick={() => {
                setFinalizedSuccess(null);
                setScannedItem(null);
              }}
            >
              Start New Sale
            </Button>
          </div>
        </div>
      ) : (
        /* POS Interactive Workspace */
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Left Column: Customer, Barcode Scanning, Scanned Item & Cart (7 cols) */}
          <div className="lg:col-span-7 space-y-5">
            {/* Customer Selection Card */}
            <div className="bg-white p-4.5 rounded-xl border border-[#E2E6E3] shadow-xs">
              <div className="flex items-center justify-between mb-3">
                <span className="text-xs font-bold text-[#123B32] uppercase tracking-wider flex items-center gap-1.5">
                  <span>Customer Selection</span>
                  <span className="text-rose-600">*</span>
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowQuickAddModal(true)}
                  leftIcon={<UserPlus className="w-3.5 h-3.5 text-[#123B32]" />}
                >
                  Quick Add Walk-in
                </Button>
              </div>

              {selectedCustomer ? (
                <div className="flex items-center justify-between p-3 rounded-lg bg-[#F8F7F4] border border-[#E2E6E3]">
                  <div>
                    <p className="text-xs font-bold text-[#123B32]">{selectedCustomer.fullName}</p>
                    <p className="text-[11px] font-mono text-[#69736E] mt-0.5">
                      {selectedCustomer.customerCode} • {selectedCustomer.primaryPhone}
                    </p>
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      setSelectedCustomer(null);
                      setCurrentInvoice(null);
                    }}
                  >
                    Change
                  </Button>
                </div>
              ) : (
                <div className="relative">
                  <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-[#69736E]">
                    <Search className="w-4 h-4" />
                  </div>
                  <input
                    type="text"
                    value={customerSearchQuery}
                    onChange={(e) => setCustomerSearchQuery(e.target.value)}
                    placeholder="Search by customer name, phone or customer code"
                    className="w-full pl-9 pr-3.5 py-2 text-xs rounded-lg border border-[#E2E6E3] focus:outline-none focus:ring-1 focus:ring-[#123B32] focus:border-[#123B32] text-[#202824] placeholder-gray-400"
                  />

                  {/* Search Results Dropdown */}
                  {customerSearchResults.length > 0 && (
                    <div className="absolute top-full left-0 right-0 mt-1 bg-white border border-[#E2E6E3] rounded-lg shadow-lg z-20 max-h-48 overflow-y-auto divide-y divide-[#E2E6E3]">
                      {customerSearchResults.map((c) => (
                        <button
                          key={c.id}
                          type="button"
                          onClick={() => {
                            setSelectedCustomer(c);
                            setCustomerSearchQuery('');
                            setCustomerSearchResults([]);
                          }}
                          className="w-full text-left p-2.5 hover:bg-[#F8F7F4] text-xs flex justify-between items-center transition-colors"
                        >
                          <div>
                            <p className="font-bold text-[#123B32]">{c.fullName}</p>
                            <p className="font-mono text-[11px] text-[#69736E]">{c.primaryPhone}</p>
                          </div>
                          <span className="font-mono text-[10px] text-gray-400">{c.customerCode}</span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>

            {/* Barcode Scanning Input */}
            <div className="bg-white p-4.5 rounded-xl border border-[#E2E6E3] shadow-xs">
              <form onSubmit={handleItemLookup} className="flex gap-2">
                <div className="relative flex-1">
                  <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-[#69736E]">
                    <Scan className="w-4 h-4" />
                  </div>
                  <input
                    ref={barcodeInputRef}
                    type="text"
                    value={scanQuery}
                    onChange={(e) => setScanQuery(e.target.value)}
                    placeholder="Scan or enter jewellery tag or barcode"
                    disabled={!activeShift}
                    className="w-full pl-9 pr-3.5 py-2 text-xs font-mono rounded-lg border border-[#E2E6E3] focus:outline-none focus:ring-1 focus:ring-[#123B32] focus:border-[#123B32] text-[#202824] placeholder-gray-400 disabled:bg-gray-100 disabled:cursor-not-allowed"
                  />
                </div>
                <Button
                  variant="primary"
                  size="sm"
                  type="submit"
                  isLoading={isLookingUp}
                  disabled={!activeShift || !scanQuery.trim()}
                  leftIcon={<Search className="w-3.5 h-3.5" />}
                >
                  Lookup
                </Button>
              </form>

              {/* Scanned Item Detail & Price Negotiation Card */}
              {scannedItem && (
                <div className="mt-4 p-4 rounded-xl bg-[#F8F7F4] border border-[#B89A60]/40 shadow-xs space-y-3">
                  <div className="flex justify-between items-start">
                    <div>
                      <span className="text-[10px] font-mono font-bold text-[#B89A60] uppercase tracking-wider">
                        {scannedItem.tagNumber} • {scannedItem.purityName} {scannedItem.materialName}
                      </span>
                      <h4 className="text-sm font-bold text-[#123B32]">{scannedItem.itemName}</h4>
                      <p className="text-xs text-[#69736E] mt-0.5">
                        Weight: Gross <strong className="text-[#202824]">{scannedItem.grossWeight.toFixed(4)}g</strong> | Net{' '}
                        <strong className="text-[#202824]">{scannedItem.netWeight.toFixed(4)}g</strong>
                      </p>
                    </div>
                    <Badge variant={scannedItem.isPartOfComposition ? 'forest' : 'neutral'} size="sm">
                      {scannedItem.isPartOfComposition ? 'Composite Set' : 'Individual Piece'}
                    </Badge>
                  </div>

                  {/* Reference Breakdown */}
                  <div className="p-3 bg-white rounded-lg border border-[#E2E6E3] text-xs grid grid-cols-3 gap-2">
                    <div>
                      <span className="text-[10px] text-[#69736E] block">Gold Value</span>
                      <span className="font-mono font-medium text-[#202824]">
                        {currency} {scannedItem.referenceMaterialValue.toFixed(2)}
                      </span>
                    </div>
                    <div>
                      <span className="text-[10px] text-[#69736E] block">Making Charge</span>
                      <span className="font-mono font-medium text-[#202824]">
                        {currency} {scannedItem.referenceMakingCharge.toFixed(2)}
                      </span>
                    </div>
                    <div>
                      <span className="text-[10px] text-[#69736E] block">Ref Total</span>
                      <span className="font-mono font-bold text-[#123B32]">
                        {currency} {scannedItem.referencePrice.toFixed(2)}
                      </span>
                    </div>
                  </div>

                  {/* Price Negotiation & Add to Cart */}
                  <div className="flex items-center gap-3 pt-1">
                    <div className="w-48">
                      <Input
                        label="Negotiated Price"
                        unit={currency}
                        type="number"
                        step="0.01"
                        placeholder="Enter agreed selling price"
                        value={negotiatedPriceInput}
                        onChange={(e) => setNegotiatedPriceInput(e.target.value)}
                      />
                    </div>
                    <div className="pt-5">
                      <Button
                        variant="primary"
                        size="md"
                        onClick={handleAddToCart}
                        leftIcon={<Plus className="w-4 h-4" />}
                      >
                        Add to Invoice
                      </Button>
                    </div>
                  </div>
                </div>
              )}
            </div>

            {/* Cart Items Table */}
            <div className="bg-white rounded-xl border border-[#E2E6E3] shadow-xs overflow-hidden">
              <div className="p-4 border-b border-[#E2E6E3] bg-[#F8F7F4]/60 flex items-center justify-between">
                <div>
                  <h3 className="text-xs font-bold text-[#123B32] uppercase tracking-wider">
                    Invoice Items ({currentInvoice?.lines.length || 0})
                  </h3>
                  {currentInvoice && (
                    <p className="text-[11px] font-mono text-[#69736E] mt-0.5">
                      Draft #{currentInvoice.invoiceNumber}
                    </p>
                  )}
                </div>
              </div>

              {!currentInvoice || currentInvoice.lines.length === 0 ? (
                <div className="p-8 text-center text-xs text-[#69736E]">
                  Cart is currently empty. Scan a barcode above to add jewellery items.
                </div>
              ) : (
                <table className="w-full text-xs text-left">
                  <thead>
                    <tr className="border-b border-[#E2E6E3] bg-[#F8F7F4] text-[#69736E] uppercase tracking-wider font-semibold">
                      <th className="py-2.5 px-3.5">Item</th>
                      <th className="py-2.5 px-3.5">Weight</th>
                      <th className="py-2.5 px-3.5 text-right">Ref Price</th>
                      <th className="py-2.5 px-3.5 text-right">Negotiated</th>
                      <th className="py-2.5 px-3.5 text-center">Action</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#E2E6E3]">
                    {currentInvoice.lines.map((l) => (
                      <tr key={l.id} className="hover:bg-[#F8F7F4]/60 transition-colors">
                        <td className="py-3 px-3.5">
                          <p className="font-bold text-[#123B32]">{l.itemName}</p>
                          <p className="font-mono text-[10px] text-[#69736E]">{l.tagNumber}</p>
                        </td>
                        <td className="py-3 px-3.5 font-mono text-[#202824]">
                          {l.grossWeight.toFixed(3)}g
                        </td>
                        <td className="py-3 px-3.5 text-right font-mono text-[#69736E] tabular-nums">
                          {currency} {l.referencePrice.toFixed(2)}
                        </td>
                        <td className="py-3 px-3.5 text-right font-mono font-bold text-[#123B32] tabular-nums">
                          {currency} {l.unitPrice.toFixed(2)}
                        </td>
                        <td className="py-3 px-3.5 text-center">
                          <div className="flex items-center justify-center gap-1">
                            <button
                              onClick={() => {
                                setEditingLineId(l.id);
                                setEditPriceValue(l.unitPrice.toFixed(2));
                              }}
                              className="p-1 rounded text-[#69736E] hover:text-[#123B32] hover:bg-gray-100"
                              title="Edit Negotiated Price"
                            >
                              <Edit2 className="w-3.5 h-3.5" />
                            </button>
                            <button
                              onClick={() => handleRemoveLine(l.id)}
                              className="p-1 rounded text-rose-600 hover:bg-rose-50"
                              title="Remove Line"
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>

          {/* Right Column: Checkout & Split Tender Panel (5 cols) */}
          <div className="lg:col-span-5 space-y-5">
            <div className="bg-white rounded-xl border border-[#E2E6E3] p-5 shadow-xs space-y-4">
              <h3 className="text-xs font-bold text-[#123B32] uppercase tracking-wider pb-3 border-b border-[#E2E6E3]">
                Payment Allocation & Checkout
              </h3>

              {/* Summary Totals */}
              <div className="space-y-2 text-xs">
                <div className="flex justify-between text-[#69736E]">
                  <span>Items Subtotal</span>
                  <span className="font-mono font-semibold text-[#202824] tabular-nums">
                    {currency} {linesTotal.toFixed(2)}
                  </span>
                </div>

                <div className="flex justify-between items-center text-[#69736E]">
                  <span>Overall Discount</span>
                  <div className="w-28">
                    <input
                      type="number"
                      step="0.01"
                      placeholder="e.g. 0.00"
                      value={discountAmount || ''}
                      onChange={(e) => setDiscountAmount(parseFloat(e.target.value) || 0)}
                      className="w-full text-right py-1 px-2 text-xs rounded border border-[#E2E6E3] font-mono focus:outline-none focus:border-[#123B32]"
                    />
                  </div>
                </div>

                {/* Prominent Payable Box */}
                <div className="mt-3 p-4 rounded-xl bg-[#0B2822] text-white flex justify-between items-center shadow-xs">
                  <div>
                    <span className="text-[10px] font-bold text-[#B89A60] uppercase tracking-widest block">
                      Total Payable
                    </span>
                    <span className="text-xs text-gray-300">Net Amount Due</span>
                  </div>
                  <span className="text-2xl font-bold font-mono text-[#B89A60] tabular-nums">
                    {currency} {netPayable.toFixed(2)}
                  </span>
                </div>
              </div>

              {/* Tender Allocation Inputs */}
              <div className="pt-2 border-t border-[#E2E6E3] space-y-3">
                <span className="text-xs font-bold text-[#202824] uppercase tracking-wider block">
                  Payment Methods (Split Tender)
                </span>

                {/* 1. Cash Tender */}
                <div className="p-3 rounded-lg border border-[#E2E6E3] bg-[#F8F7F4]/50 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-[#123B32] flex items-center gap-1.5">
                      <Banknote className="w-4 h-4 text-emerald-700" />
                      <span>Physical Cash</span>
                    </span>
                    <div className="w-32">
                      <input
                        type="number"
                        step="0.01"
                        placeholder="Amount to pay"
                        value={cashRow?.amount || ''}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value) || 0;
                          const updated = [...payments];
                          const idx = updated.findIndex((p) => p.method === 1);
                          if (idx >= 0) {
                            updated[idx].amount = val;
                            if (!updated[idx].cashTendered || updated[idx].cashTendered < val) {
                              updated[idx].cashTendered = val;
                            }
                          }
                          setPayments(updated);
                        }}
                        className="w-full text-right py-1 px-2 text-xs font-mono rounded border border-[#E2E6E3] focus:outline-none focus:border-[#123B32]"
                      />
                    </div>
                  </div>

                  {(cashRow?.amount || 0) > 0 && (
                    <div className="flex items-center justify-between pt-2 border-t border-[#E2E6E3]/60 text-xs">
                      <span className="text-[#69736E]">Cash Received from Customer:</span>
                      <div className="w-32">
                        <input
                          type="number"
                          step="0.01"
                          placeholder="e.g. 50000.00"
                          value={cashRow?.cashTendered || ''}
                          onChange={(e) => {
                            const val = parseFloat(e.target.value) || 0;
                            const updated = [...payments];
                            const idx = updated.findIndex((p) => p.method === 1);
                            if (idx >= 0) {
                              updated[idx].cashTendered = val;
                            }
                            setPayments(updated);
                          }}
                          className="w-full text-right py-1 px-2 text-xs font-mono rounded border border-[#E2E6E3] text-[#123B32] font-semibold focus:outline-none focus:border-[#123B32]"
                        />
                      </div>
                    </div>
                  )}

                  {changeDue > 0 && (
                    <div className="p-2 rounded bg-emerald-50 border border-emerald-200 text-xs flex justify-between font-bold text-emerald-800">
                      <span>Change to Return:</span>
                      <span className="font-mono tabular-nums">
                        {currency} {changeDue.toFixed(2)}
                      </span>
                    </div>
                  )}
                </div>

                {/* 2. Card Payment */}
                <div className="p-3 rounded-lg border border-[#E2E6E3] bg-[#F8F7F4]/50 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-[#123B32] flex items-center gap-1.5">
                      <CreditCard className="w-4 h-4 text-blue-700" />
                      <span>Card Payment</span>
                    </span>
                    <div className="w-32">
                      <input
                        type="number"
                        step="0.01"
                        placeholder="Amount to pay"
                        value={payments.find((p) => p.method === 2)?.amount || ''}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value) || 0;
                          const updated = [...payments];
                          const idx = updated.findIndex((p) => p.method === 2);
                          if (idx >= 0) {
                            updated[idx].amount = val;
                          } else if (val > 0) {
                            updated.push({ method: 2, amount: val });
                          }
                          setPayments(updated);
                        }}
                        className="w-full text-right py-1 px-2 text-xs font-mono rounded border border-[#E2E6E3] focus:outline-none focus:border-[#123B32]"
                      />
                    </div>
                  </div>

                  {(payments.find((p) => p.method === 2)?.amount || 0) > 0 && (
                    <div className="grid grid-cols-2 gap-2 pt-2 border-t border-[#E2E6E3]/60">
                      <input
                        type="text"
                        maxLength={4}
                        placeholder="Last 4 (e.g. 4821)"
                        value={payments.find((p) => p.method === 2)?.cardLast4 || ''}
                        onChange={(e) => {
                          const val = e.target.value;
                          const updated = [...payments];
                          const idx = updated.findIndex((p) => p.method === 2);
                          if (idx >= 0) updated[idx].cardLast4 = val;
                          setPayments(updated);
                        }}
                        className="py-1 px-2 text-xs font-mono rounded border border-[#E2E6E3]"
                      />
                      <input
                        type="text"
                        placeholder="Approval Code"
                        value={payments.find((p) => p.method === 2)?.referenceNumber || ''}
                        onChange={(e) => {
                          const val = e.target.value;
                          const updated = [...payments];
                          const idx = updated.findIndex((p) => p.method === 2);
                          if (idx >= 0) updated[idx].referenceNumber = val;
                          setPayments(updated);
                        }}
                        className="py-1 px-2 text-xs font-mono rounded border border-[#E2E6E3]"
                      />
                    </div>
                  )}
                </div>

                {/* 3. Bank Transfer */}
                <div className="p-3 rounded-lg border border-[#E2E6E3] bg-[#F8F7F4]/50 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-semibold text-[#123B32] flex items-center gap-1.5">
                      <Building className="w-4 h-4 text-purple-700" />
                      <span>Bank Transfer</span>
                    </span>
                    <div className="w-32">
                      <input
                        type="number"
                        step="0.01"
                        placeholder="Amount to pay"
                        value={payments.find((p) => p.method === 3)?.amount || ''}
                        onChange={(e) => {
                          const val = parseFloat(e.target.value) || 0;
                          const updated = [...payments];
                          const idx = updated.findIndex((p) => p.method === 3);
                          if (idx >= 0) {
                            updated[idx].amount = val;
                          } else if (val > 0) {
                            updated.push({ method: 3, amount: val });
                          }
                          setPayments(updated);
                        }}
                        className="w-full text-right py-1 px-2 text-xs font-mono rounded border border-[#E2E6E3] focus:outline-none focus:border-[#123B32]"
                      />
                    </div>
                  </div>

                  {(payments.find((p) => p.method === 3)?.amount || 0) > 0 && (
                    <div className="grid grid-cols-2 gap-2 pt-2 border-t border-[#E2E6E3]/60">
                      <input
                        type="text"
                        placeholder="Bank Name (e.g. BOC)"
                        value={payments.find((p) => p.method === 3)?.bankName || ''}
                        onChange={(e) => {
                          const val = e.target.value;
                          const updated = [...payments];
                          const idx = updated.findIndex((p) => p.method === 3);
                          if (idx >= 0) updated[idx].bankName = val;
                          setPayments(updated);
                        }}
                        className="py-1 px-2 text-xs rounded border border-[#E2E6E3]"
                      />
                      <input
                        type="text"
                        placeholder="Transfer Ref #"
                        value={payments.find((p) => p.method === 3)?.referenceNumber || ''}
                        onChange={(e) => {
                          const val = e.target.value;
                          const updated = [...payments];
                          const idx = updated.findIndex((p) => p.method === 3);
                          if (idx >= 0) updated[idx].referenceNumber = val;
                          setPayments(updated);
                        }}
                        className="py-1 px-2 text-xs font-mono rounded border border-[#E2E6E3]"
                      />
                    </div>
                  )}
                </div>
              </div>

              {/* Checkout Action Button */}
              <div className="pt-3">
                <Button
                  variant="primary"
                  size="lg"
                  className="w-full"
                  disabled={
                    !currentInvoice ||
                    currentInvoice.lines.length === 0 ||
                    !activeShift ||
                    totalPaidAllocation < netPayable ||
                    isFinalizing
                  }
                  isLoading={isFinalizing}
                  onClick={handleCheckout}
                >
                  Confirm & Finalize Invoice
                </Button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Quick Add Customer Modal */}
      <Modal
        isOpen={showQuickAddModal}
        onClose={() => setShowQuickAddModal(false)}
        title="Quick Walk-in Customer"
        description="Register a walk-in customer with canonical phone normalization."
        footer={
          <>
            <Button variant="secondary" size="sm" onClick={() => setShowQuickAddModal(false)}>
              Cancel
            </Button>
            <Button variant="primary" size="sm" onClick={handleQuickCreateCustomer}>
              Register Customer
            </Button>
          </>
        }
      >
        <form onSubmit={handleQuickCreateCustomer} className="space-y-3">
          <Input
            label="Full Name"
            requiredIndicator
            placeholder="e.g. Arjun Rajan"
            value={quickCustomer.fullName}
            onChange={(e) => setQuickCustomer({ ...quickCustomer, fullName: e.target.value })}
            required
          />
          <Input
            label="Primary Phone"
            requiredIndicator
            placeholder="e.g. 077 123 4567"
            value={quickCustomer.primaryPhone}
            onChange={(e) => setQuickCustomer({ ...quickCustomer, primaryPhone: e.target.value })}
            helperText="Normalized to canonical E.164 (+94...)"
            required
          />
          <Input
            label="City"
            placeholder="e.g. Jaffna"
            value={quickCustomer.city}
            onChange={(e) => setQuickCustomer({ ...quickCustomer, city: e.target.value })}
          />
          <Input
            label="Customer Notes"
            placeholder="Relevant customer preferences or additional information"
            value={quickCustomer.notes}
            onChange={(e) => setQuickCustomer({ ...quickCustomer, notes: e.target.value })}
          />
        </form>
      </Modal>

      {/* Edit Line Negotiated Price Modal */}
      <Modal
        isOpen={editingLineId !== null}
        onClose={() => setEditingLineId(null)}
        title="Adjust Negotiated Selling Price"
        description="Modify the agreed selling price for this jewellery piece."
        footer={
          <>
            <Button variant="secondary" size="sm" onClick={() => setEditingLineId(null)}>
              Cancel
            </Button>
            <Button variant="primary" size="sm" onClick={handleUpdateLinePrice}>
              Update Price
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <Input
            label="Negotiated Selling Price"
            unit={currency}
            type="number"
            step="0.01"
            placeholder="Enter the agreed selling price"
            value={editPriceValue}
            onChange={(e) => setEditPriceValue(e.target.value)}
            requiredIndicator
            required
          />
        </div>
      </Modal>
    </AppLayout>
  );
}
