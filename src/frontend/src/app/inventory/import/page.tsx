'use client';

import React, { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  bulkImportApi,
  BulkImportValidationResult,
  BulkImportResult,
  Branch,
  branchApi,
} from '@/lib/api';
import { PageHeader } from '@/components/ui/PageHeader';
import { Button } from '@/components/ui/Button';
import { Select } from '@/components/ui/Select';
import { Badge } from '@/components/ui/Badge';
import {
  UploadCloud,
  FileSpreadsheet,
  Download,
  CheckCircle2,
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  X,
} from 'lucide-react';

type Step = 'upload' | 'review' | 'complete';

export default function BulkImportPage() {
  const { user } = useAuth();
  const { activeBranchId } = useBranch();

  const canBulkImport = user?.isGlobalAdmin || user?.permissions.includes('INVENTORY_BULK_IMPORT');

  const [step, setStep] = useState<Step>('upload');
  const [branches, setBranches] = useState<Branch[]>([]);
  const [selectedBranchId, setSelectedBranchId] = useState<string>('');
  const [templateFormat, setTemplateFormat] = useState<'xlsx' | 'csv'>('xlsx');
  const [isDownloading, setIsDownloading] = useState(false);

  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [isValidating, setIsValidating] = useState(false);
  const [isConfirming, setIsConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [validationResult, setValidationResult] = useState<BulkImportValidationResult | null>(null);
  const [importResult, setImportResult] = useState<BulkImportResult | null>(null);
  const [activeTab, setActiveTab] = useState<'errors' | 'valid'>('errors');
  const [idempotencyKey, setIdempotencyKey] = useState<string>('');

  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    async function loadBranches() {
      try {
        const data = await branchApi.getBranches(true);
        setBranches(data);
        if (activeBranchId) {
          setSelectedBranchId(activeBranchId);
        } else if (data.length > 0) {
          setSelectedBranchId(data[0].id);
        }
      } catch (err: any) {
        setError(err.message || 'Failed to load branches.');
      }
    }
    loadBranches();
  }, [activeBranchId]);

  if (!canBulkImport) {
    return (
      <AppLayout>
        <div className="max-w-xl mx-auto mt-10 bg-white border border-rose-200 rounded-xl p-6 text-center shadow-xs">
          <AlertTriangle className="w-8 h-8 text-rose-600 mx-auto mb-2" />
          <h2 className="text-base font-bold text-gray-900">Access Denied</h2>
          <p className="text-xs text-gray-600 mt-1">
            You need the <code className="font-mono text-rose-700">INVENTORY_BULK_IMPORT</code> permission to import inventory.
          </p>
          <div className="mt-4">
            <Link href="/inventory">
              <Button variant="secondary" size="sm">Return to Inventory</Button>
            </Link>
          </div>
        </div>
      </AppLayout>
    );
  }

  const handleDownloadTemplate = async () => {
    setIsDownloading(true);
    setError(null);
    try {
      await bulkImportApi.downloadTemplate(templateFormat);
    } catch (err: any) {
      setError(err.message || 'Failed to download template.');
    } finally {
      setIsDownloading(false);
    }
  };

  const acceptFile = (file: File) => {
    const ext = file.name.split('.').pop()?.toLowerCase();
    if (ext !== 'xlsx' && ext !== 'csv') {
      setError('Only .xlsx and .csv files are supported.');
      return;
    }
    if (file.size > 5 * 1024 * 1024) {
      setError('File size exceeds the 5 MB limit.');
      return;
    }
    setSelectedFile(file);
    setError(null);
  };

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      acceptFile(e.target.files[0]);
    }
  };

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      acceptFile(e.dataTransfer.files[0]);
    }
  };

  const handleValidateFile = async () => {
    if (!selectedBranchId) {
      setError('Please select a target branch.');
      return;
    }
    if (!selectedFile) {
      setError('Please choose a .xlsx or .csv file to upload.');
      return;
    }

    setIsValidating(true);
    setError(null);

    try {
      const res = await bulkImportApi.validateFile(selectedBranchId, selectedFile);
      setValidationResult(res);
      setActiveTab(res.isAllValid ? 'valid' : 'errors');
      setIdempotencyKey(crypto.randomUUID());
      setStep('review');
    } catch (err: any) {
      setError(err.message || 'File validation failed.');
    } finally {
      setIsValidating(false);
    }
  };

  const handleConfirmImport = async () => {
    if (!validationResult || !validationResult.isAllValid) return;

    setIsConfirming(true);
    setError(null);

    try {
      const res = await bulkImportApi.confirmImport(validationResult.batchId, idempotencyKey);
      setImportResult(res);
      setStep('complete');
    } catch (err: any) {
      setError(err.message || 'Bulk import confirmation failed.');
    } finally {
      setIsConfirming(false);
    }
  };

  const resetFlow = () => {
    setSelectedFile(null);
    setValidationResult(null);
    setImportResult(null);
    setError(null);
    setStep('upload');
  };

  const stepIndex = step === 'upload' ? 1 : step === 'review' ? 2 : 3;

  return (
    <AppLayout>
      <div className="max-w-5xl mx-auto space-y-5">
        <PageHeader
          title="Bulk Inventory Import"
          description="Upload an Excel (.xlsx) or CSV (.csv) spreadsheet to validate and create tagged stock in one batch."
          actions={
            <Link href="/inventory">
              <Button variant="secondary" size="sm" leftIcon={<ArrowLeft className="w-3.5 h-3.5" />}>
                Back to Inventory
              </Button>
            </Link>
          }
        />

        {/* Error Alert */}
        {error && (
          <div className="bg-rose-50 border border-rose-200 text-rose-800 px-4 py-3 rounded-xl text-xs flex items-center justify-between">
            <div className="flex items-center gap-2">
              <AlertTriangle className="w-4 h-4 text-rose-600 flex-shrink-0" />
              <span>{error}</span>
            </div>
            <button onClick={() => setError(null)} className="text-rose-500 hover:text-rose-800 p-0.5">
              <X className="w-4 h-4" />
            </button>
          </div>
        )}

        {/* STEP 1: Upload & Validate */}
        {step === 'upload' && (
          <div className="bg-white border border-[#E2E6E3] rounded-xl p-6 shadow-xs space-y-6">
            {/* Top Row: Branch & Single Template Download */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-5 items-end pb-5 border-b border-[#E2E6E3]">
              <Select
                label="Target Branch"
                requiredIndicator
                value={selectedBranchId}
                onChange={(e) => setSelectedBranchId(e.target.value)}
                options={[
                  { value: '', label: 'Select branch...' },
                  ...branches.map((b) => ({
                    value: b.id,
                    label: `${b.name} (${b.code})`,
                  })),
                ]}
              />

              <div className="flex flex-col">
                <span className="block text-xs font-semibold text-[#202824] uppercase tracking-wider mb-1.5">
                  Import Template
                </span>
                <div className="flex items-center gap-2">
                  <div className="inline-flex rounded-lg border border-[#E2E6E3] bg-[#F8F7F4] p-0.5">
                    {(['xlsx', 'csv'] as const).map((fmt) => (
                      <button
                        key={fmt}
                        type="button"
                        onClick={() => setTemplateFormat(fmt)}
                        className={`px-2.5 py-1.5 text-xs font-semibold rounded-md uppercase transition ${
                          templateFormat === fmt
                            ? 'bg-white text-[#123B32] shadow-xs'
                            : 'text-[#69736E] hover:text-[#202824]'
                        }`}
                      >
                        .{fmt}
                      </button>
                    ))}
                  </div>

                  <Button
                    variant="secondary"
                    size="md"
                    isLoading={isDownloading}
                    onClick={handleDownloadTemplate}
                    leftIcon={<Download className="w-4 h-4" />}
                    className="flex-1"
                  >
                    Download Template (.{templateFormat})
                  </Button>
                </div>
              </div>
            </div>

            {/* File Dropzone */}
            <div>
              <div
                onClick={() => fileInputRef.current?.click()}
                onDragOver={(e) => {
                  e.preventDefault();
                  setIsDragging(true);
                }}
                onDragLeave={() => setIsDragging(false)}
                onDrop={handleDrop}
                className={`border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-colors ${
                  isDragging
                    ? 'border-[#123B32] bg-[#123B32]/5'
                    : selectedFile
                    ? 'border-emerald-500 bg-emerald-50/30'
                    : 'border-[#D1D7D3] hover:border-[#123B32] hover:bg-[#F8F7F4]'
                }`}
              >
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".xlsx,.csv"
                  onChange={handleFileChange}
                  className="hidden"
                />

                {selectedFile ? (
                  <div className="space-y-1.5">
                    <FileSpreadsheet className="w-8 h-8 text-[#123B32] mx-auto" />
                    <p className="text-sm font-bold text-[#123B32]">{selectedFile.name}</p>
                    <p className="text-xs text-[#69736E]">
                      {(selectedFile.size / 1024).toFixed(1)} KB •{' '}
                      {selectedFile.name.toLowerCase().endsWith('.xlsx') ? 'Excel (.xlsx)' : 'CSV (.csv)'}
                    </p>
                    <p className="text-[11px] font-medium text-[#B89A60] pt-1">Click or drop another file to replace</p>
                  </div>
                ) : (
                  <div className="space-y-1.5">
                    <UploadCloud className="w-8 h-8 text-[#69736E] mx-auto" />
                    <p className="text-sm font-semibold text-[#202824]">
                      Click to upload or drag and drop your spreadsheet
                    </p>
                    <p className="text-xs text-[#69736E]">
                      Supports <span className="font-mono">.xlsx</span> and <span className="font-mono">.csv</span> up to 5 MB
                    </p>
                  </div>
                )}
              </div>

              <p className="text-[11px] text-[#69736E] mt-2">
                Tags and barcodes are generated automatically upon confirmation. All rows are validated before any items are created.
              </p>
            </div>

            <div className="flex justify-end pt-1">
              <Button
                variant="primary"
                size="md"
                isLoading={isValidating}
                disabled={!selectedBranchId || !selectedFile}
                onClick={handleValidateFile}
                rightIcon={<ArrowRight className="w-4 h-4" />}
              >
                Validate Spreadsheet
              </Button>
            </div>
          </div>
        )}

        {/* STEP 2: Review & Confirm */}
        {step === 'review' && validationResult && (
          <div className="space-y-4">
            {/* Status Banner */}
            <div
              className={`border rounded-xl p-4 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-3 ${
                validationResult.isAllValid
                  ? 'bg-emerald-50 border-emerald-200 text-emerald-950'
                  : 'bg-rose-50 border-rose-200 text-rose-950'
              }`}
            >
              <div className="flex items-start gap-3">
                {validationResult.isAllValid ? (
                  <CheckCircle2 className="w-5 h-5 text-emerald-700 mt-0.5 flex-shrink-0" />
                ) : (
                  <AlertTriangle className="w-5 h-5 text-rose-700 mt-0.5 flex-shrink-0" />
                )}
                <div>
                  <h3 className="text-sm font-bold">
                    {validationResult.isAllValid
                      ? `Ready to import ${validationResult.validRows} items into ${validationResult.branchName}`
                      : `Validation failed: ${validationResult.invalidRows} row(s) need attention`}
                  </h3>
                  <p className="text-xs opacity-85 mt-0.5">
                    {validationResult.isAllValid
                      ? `File: ${validationResult.originalFileName} • All rows passed validation.`
                      : 'Fix the listed errors in your spreadsheet and upload the file again.'}
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <Badge variant="neutral">Total: {validationResult.totalRows}</Badge>
                <Badge variant="success">Valid: {validationResult.validRows}</Badge>
                {validationResult.invalidRows > 0 && (
                  <Badge variant="error">Errors: {validationResult.invalidRows}</Badge>
                )}
              </div>
            </div>

            {/* Preview / Errors Table */}
            <div className="bg-white border border-[#E2E6E3] rounded-xl overflow-hidden shadow-xs">
              <div className="flex border-b border-[#E2E6E3] bg-[#F8F7F4] px-4 pt-2.5 gap-1">
                {validationResult.invalidRows > 0 && (
                  <button
                    onClick={() => setActiveTab('errors')}
                    className={`text-xs font-semibold px-3.5 py-2 border-b-2 transition ${
                      activeTab === 'errors'
                        ? 'border-rose-600 text-rose-700 bg-white rounded-t-lg'
                        : 'border-transparent text-[#69736E] hover:text-[#202824]'
                    }`}
                  >
                    Errors ({validationResult.invalidRows})
                  </button>
                )}
                <button
                  onClick={() => setActiveTab('valid')}
                  className={`text-xs font-semibold px-3.5 py-2 border-b-2 transition ${
                    activeTab === 'valid'
                      ? 'border-[#123B32] text-[#123B32] bg-white rounded-t-lg'
                      : 'border-transparent text-[#69736E] hover:text-[#202824]'
                  }`}
                >
                  Valid Rows ({validationResult.validRows})
                </button>
              </div>

              {activeTab === 'errors' && (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs">
                    <thead>
                      <tr className="bg-rose-50/70 text-rose-900 border-b border-rose-100 font-semibold">
                        <th className="py-2.5 px-4 w-24">Row</th>
                        <th className="py-2.5 px-4 w-40">Field</th>
                        <th className="py-2.5 px-4">Error</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[#E2E6E3]">
                      {validationResult.errors.map((err, idx) => (
                        <tr key={idx} className="hover:bg-rose-50/30">
                          <td className="py-2.5 px-4 font-mono font-semibold text-rose-700">#{err.rowNumber}</td>
                          <td className="py-2.5 px-4 font-mono text-[#202824]">{err.field}</td>
                          <td className="py-2.5 px-4 text-rose-700">{err.message}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {activeTab === 'valid' && (
                <div className="overflow-x-auto max-h-96">
                  <table className="w-full text-left text-xs">
                    <thead className="sticky top-0 bg-[#F8F7F4] text-[#69736E] border-b border-[#E2E6E3] font-semibold">
                      <tr>
                        <th className="py-2.5 px-3 w-14">Row</th>
                        <th className="py-2.5 px-3">Item</th>
                        <th className="py-2.5 px-3">Material</th>
                        <th className="py-2.5 px-3">Category</th>
                        <th className="py-2.5 px-3">Purity</th>
                        <th className="py-2.5 px-3 text-right">Gross (g)</th>
                        <th className="py-2.5 px-3 text-right">Net (g)</th>
                        <th className="py-2.5 px-3">Location</th>
                        <th className="py-2.5 px-3">Making Charge</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-[#E2E6E3]">
                      {validationResult.rows
                        .filter((r) => r.isValid)
                        .map((row) => (
                          <tr key={row.rowNumber} className="hover:bg-[#F8F7F4]/60">
                            <td className="py-2.5 px-3 font-mono text-[#69736E]">{row.rowNumber}</td>
                            <td className="py-2.5 px-3 font-medium text-[#202824]">
                              {row.itemName}
                              {row.itemCode && <span className="ml-1.5 text-[10px] text-[#69736E]">({row.itemCode})</span>}
                            </td>
                            <td className="py-2.5 px-3">{row.materialCode}</td>
                            <td className="py-2.5 px-3">{row.categoryCode}</td>
                            <td className="py-2.5 px-3">{row.purityCode}</td>
                            <td className="py-2.5 px-3 text-right font-mono tabular-nums">{row.grossWeight.toFixed(4)}</td>
                            <td className="py-2.5 px-3 text-right font-mono font-semibold text-[#123B32] tabular-nums">
                              {row.netWeight.toFixed(4)}
                            </td>
                            <td className="py-2.5 px-3 font-mono text-[11px]">{row.stockLocationCode}</td>
                            <td className="py-2.5 px-3 text-[11px] text-[#69736E]">
                              {row.makingChargeMethod}: Rs. {row.makingChargeValue.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                            </td>
                          </tr>
                        ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            {/* Footer Actions */}
            <div className="flex justify-between items-center bg-white border border-[#E2E6E3] rounded-xl px-5 py-3.5">
              <Button
                variant="secondary"
                size="sm"
                onClick={resetFlow}
                disabled={isConfirming}
                leftIcon={<ArrowLeft className="w-3.5 h-3.5" />}
              >
                Upload Different File
              </Button>

              <Button
                variant="primary"
                size="md"
                isLoading={isConfirming}
                disabled={!validationResult.isAllValid}
                onClick={handleConfirmImport}
                leftIcon={<CheckCircle2 className="w-4 h-4" />}
              >
                {validationResult.isAllValid
                  ? `Confirm & Import ${validationResult.validRows} Items`
                  : 'Fix Errors to Import'}
              </Button>
            </div>
          </div>
        )}

        {/* STEP 3: Complete */}
        {step === 'complete' && importResult && (
          <div className="space-y-4">
            <div className="bg-emerald-50 border border-emerald-200 rounded-xl p-6 text-center space-y-2">
              <CheckCircle2 className="w-10 h-10 text-[#123B32] mx-auto" />
              <h2 className="text-lg font-bold text-[#123B32]">
                Imported {importResult.totalImported} Jewellery Items
              </h2>
              <p className="text-xs text-[#202824]">
                Tags and barcodes have been generated and added to branch stock.
              </p>
            </div>

            <div className="bg-white border border-[#E2E6E3] rounded-xl overflow-hidden shadow-xs">
              <div className="px-4 py-3 border-b border-[#E2E6E3] flex justify-between items-center bg-[#F8F7F4]">
                <h3 className="text-xs font-bold text-[#123B32] uppercase tracking-wider">
                  Created Items ({importResult.createdItems.length})
                </h3>
                <Link href="/inventory" className="text-xs font-semibold text-[#123B32] hover:underline">
                  View in Inventory →
                </Link>
              </div>
              <div className="max-h-96 overflow-y-auto">
                <table className="w-full text-left text-xs">
                  <thead className="sticky top-0 bg-[#F8F7F4] border-b border-[#E2E6E3] font-semibold text-[#69736E]">
                    <tr>
                      <th className="py-2.5 px-4">Item Name</th>
                      <th className="py-2.5 px-4">Tag Number</th>
                      <th className="py-2.5 px-4">Barcode</th>
                      <th className="py-2.5 px-4 text-right">Gross (g)</th>
                      <th className="py-2.5 px-4 text-right">Net (g)</th>
                      <th className="py-2.5 px-4">Location</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#E2E6E3]">
                    {importResult.createdItems.map((item) => (
                      <tr key={item.id} className="hover:bg-[#F8F7F4]/60">
                        <td className="py-2.5 px-4 font-medium text-[#202824]">{item.itemName}</td>
                        <td className="py-2.5 px-4 font-mono font-bold text-[#123B32]">{item.tagNumber}</td>
                        <td className="py-2.5 px-4 font-mono text-[#69736E]">{item.barcode}</td>
                        <td className="py-2.5 px-4 text-right font-mono tabular-nums">{item.grossWeight.toFixed(4)}</td>
                        <td className="py-2.5 px-4 text-right font-mono font-semibold text-[#123B32] tabular-nums">
                          {item.netWeight.toFixed(4)}
                        </td>
                        <td className="py-2.5 px-4 font-mono text-[11px]">{item.stockLocationCode}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="flex justify-between items-center">
              <Button variant="secondary" size="sm" onClick={resetFlow}>
                + Import Another Batch
              </Button>
              <Link href="/inventory">
                <Button variant="primary" size="md">
                  Go to Inventory
                </Button>
              </Link>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
