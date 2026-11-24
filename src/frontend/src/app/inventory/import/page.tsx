'use client';

import React, { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import AppLayout from '@/components/AppLayout';
import { useAuth } from '@/lib/auth-context';
import { useBranch } from '@/lib/branch-context';
import {
  bulkImportApi,
  BulkImportValidationResult,
  BulkImportResult,
  Branch,
  branchApi
} from '@/lib/api';

type Step = 'branch_template' | 'upload' | 'validation' | 'confirm' | 'summary';

export default function BulkImportPage() {
  const router = useRouter();
  const { user } = useAuth();
  const { activeBranchId } = useBranch();

  const canBulkImport = user?.isGlobalAdmin || user?.permissions.includes('INVENTORY_BULK_IMPORT');

  const [currentStep, setCurrentStep] = useState<Step>('branch_template');
  const [branches, setBranches] = useState<Branch[]>([]);
  const [selectedBranchId, setSelectedBranchId] = useState<string>('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);

  const [isLoading, setIsLoading] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [validationResult, setValidationResult] = useState<BulkImportValidationResult | null>(null);
  const [importResult, setImportResult] = useState<BulkImportResult | null>(null);
  const [activeTab, setActiveTab] = useState<'errors' | 'valid'>('errors');

  // Idempotency key generated per batch confirmation
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
        <div className="p-8 max-w-4xl mx-auto">
          <div className="bg-red-50 border border-red-200 rounded-xl p-6 text-center">
            <h2 className="text-lg font-bold text-red-900 mb-2">Access Denied</h2>
            <p className="text-sm text-red-700">
              You do not have the required permission (<code>INVENTORY_BULK_IMPORT</code>) to access bulk inventory import.
            </p>
            <div className="mt-4">
              <Link href="/inventory" className="text-sm text-[#0D3826] font-semibold underline">
                Return to Inventory
              </Link>
            </div>
          </div>
        </div>
      </AppLayout>
    );
  }

  const selectedBranch = branches.find(b => b.id === selectedBranchId);

  // Step 1: Download templates
  const handleDownloadTemplate = (format: 'xlsx' | 'csv') => {
    const url = bulkImportApi.getTemplateDownloadUrl(format);
    window.open(url, '_blank');
  };

  // Step 2: Handle File Selection
  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      const file = e.target.files[0];
      const ext = file.name.split('.').pop()?.toLowerCase();
      if (ext !== 'xlsx' && ext !== 'csv') {
        setError('Only .xlsx and .csv files are supported.');
        return;
      }
      setSelectedFile(file);
      setError(null);
    }
  };

  // Step 2: Submit File for Validation
  const handleValidateFile = async () => {
    if (!selectedBranchId) {
      setError('Please select a branch before uploading.');
      return;
    }
    if (!selectedFile) {
      setError('Please choose a valid .xlsx or .csv file to upload.');
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      const res = await bulkImportApi.validateFile(selectedBranchId, selectedFile);
      setValidationResult(res);
      setActiveTab(res.isAllValid ? 'valid' : 'errors');
      // Prepare a unique idempotency key for this batch
      setIdempotencyKey(crypto.randomUUID());
      setCurrentStep('validation');
    } catch (err: any) {
      setError(err.message || 'File validation failed.');
    } finally {
      setIsLoading(false);
    }
  };

  // Step 5: Confirm and Import
  const handleConfirmImport = async () => {
    if (!validationResult) return;
    if (!validationResult.isAllValid) {
      setError('Cannot import a batch that contains invalid rows. Please fix the errors in your spreadsheet and re-upload.');
      return;
    }

    setIsSubmitting(true);
    setError(null);

    try {
      const res = await bulkImportApi.confirmImport(validationResult.batchId, idempotencyKey);
      setImportResult(res);
      setCurrentStep('summary');
    } catch (err: any) {
      setError(err.message || 'Bulk import confirmation failed.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <AppLayout>
      <div className="p-6 max-w-6xl mx-auto space-y-6">
        {/* Breadcrumb & Header */}
        <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 border-b border-gray-200 pb-4">
          <div>
            <div className="flex items-center space-x-2 text-xs text-gray-500 mb-1">
              <Link href="/inventory" className="hover:text-[#0D3826] transition">Inventory</Link>
              <span>/</span>
              <span className="text-gray-900 font-medium">Bulk Import</span>
            </div>
            <h1 className="text-2xl font-serif font-bold text-[#0D3826] flex items-center gap-2">
              <span>Bulk Inventory Import</span>
              <span className="text-xs bg-[#D4AF37]/20 text-[#0D3826] font-semibold px-2 py-0.5 rounded-full border border-[#D4AF37]/40">
                Excel & CSV
              </span>
            </h1>
            <p className="text-xs text-gray-600 mt-1">
              Digitize multiple physical jewellery stock records with all-or-nothing validation and automated tag generation.
            </p>
          </div>

          <div className="flex items-center gap-2">
            <Link
              href="/inventory"
              className="text-xs text-gray-600 hover:text-gray-900 bg-white border border-gray-300 rounded-lg px-3 py-2 font-medium transition shadow-sm"
            >
              Back to Inventory
            </Link>
          </div>
        </div>

        {/* Multi-step Visual Stepper */}
        <div className="bg-white border border-gray-200 rounded-xl p-4 shadow-sm">
          <div className="flex items-center justify-between max-w-3xl mx-auto">
            <div className="flex items-center space-x-2">
              <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold ${
                currentStep === 'branch_template' ? 'bg-[#0D3826] text-white ring-4 ring-[#0D3826]/10' : 'bg-green-100 text-[#0D3826]'
              }`}>
                1
              </div>
              <span className="text-xs font-semibold text-gray-800 hidden sm:inline">Branch & Template</span>
            </div>
            <div className="h-0.5 w-12 bg-gray-200 flex-1 mx-2" />

            <div className="flex items-center space-x-2">
              <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold ${
                currentStep === 'upload' ? 'bg-[#0D3826] text-white ring-4 ring-[#0D3826]/10' :
                currentStep === 'branch_template' ? 'bg-gray-100 text-gray-500' : 'bg-green-100 text-[#0D3826]'
              }`}>
                2
              </div>
              <span className="text-xs font-semibold text-gray-800 hidden sm:inline">Upload File</span>
            </div>
            <div className="h-0.5 w-12 bg-gray-200 flex-1 mx-2" />

            <div className="flex items-center space-x-2">
              <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold ${
                currentStep === 'validation' ? 'bg-[#0D3826] text-white ring-4 ring-[#0D3826]/10' :
                (currentStep === 'confirm' || currentStep === 'summary') ? 'bg-green-100 text-[#0D3826]' : 'bg-gray-100 text-gray-500'
              }`}>
                3
              </div>
              <span className="text-xs font-semibold text-gray-800 hidden sm:inline">Validation</span>
            </div>
            <div className="h-0.5 w-12 bg-gray-200 flex-1 mx-2" />

            <div className="flex items-center space-x-2">
              <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold ${
                currentStep === 'confirm' ? 'bg-[#0D3826] text-white ring-4 ring-[#0D3826]/10' :
                currentStep === 'summary' ? 'bg-green-100 text-[#0D3826]' : 'bg-gray-100 text-gray-500'
              }`}>
                4
              </div>
              <span className="text-xs font-semibold text-gray-800 hidden sm:inline">Confirm</span>
            </div>
            <div className="h-0.5 w-12 bg-gray-200 flex-1 mx-2" />

            <div className="flex items-center space-x-2">
              <div className={`w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold ${
                currentStep === 'summary' ? 'bg-[#0D3826] text-white ring-4 ring-[#0D3826]/10' : 'bg-gray-100 text-gray-500'
              }`}>
                5
              </div>
              <span className="text-xs font-semibold text-gray-800 hidden sm:inline">Summary</span>
            </div>
          </div>
        </div>

        {/* Global Error Banner */}
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-800 p-4 rounded-xl text-xs flex items-start justify-between shadow-sm">
            <div className="flex items-start gap-2">
              <span className="text-base">⚠️</span>
              <div>
                <strong className="font-semibold">Import Error: </strong>
                <span>{error}</span>
              </div>
            </div>
            <button onClick={() => setError(null)} className="text-red-500 hover:text-red-800 font-bold ml-4">✕</button>
          </div>
        )}

        {/* STEP 1: Branch Selection & Template Download */}
        {currentStep === 'branch_template' && (
          <div className="space-y-6">
            <div className="bg-white border border-gray-200 rounded-xl p-6 shadow-sm space-y-4">
              <h2 className="text-sm font-bold text-gray-900 uppercase tracking-wider flex items-center gap-2">
                <span className="w-2.5 h-2.5 rounded-full bg-[#0D3826]" />
                Step 1: Select Target Branch
              </h2>
              <p className="text-xs text-gray-600">
                All items in a single import file belong to one target branch. Stock locations specified in the file must belong to this selected branch.
              </p>

              <div className="max-w-md">
                <label className="block text-xs font-semibold text-gray-700 mb-1">Target Branch *</label>
                <select
                  value={selectedBranchId}
                  onChange={(e) => setSelectedBranchId(e.target.value)}
                  className="w-full text-xs border border-gray-300 rounded-lg p-2.5 bg-gray-50 focus:bg-white focus:ring-2 focus:ring-[#0D3826] focus:border-transparent outline-none transition"
                >
                  <option value="">-- Select Branch --</option>
                  {branches.map(b => (
                    <option key={b.id} value={b.id}>
                      {b.name} ({b.code}) — {b.country}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {/* Template Download Cards */}
            <div className="bg-white border border-gray-200 rounded-xl p-6 shadow-sm space-y-4">
              <h2 className="text-sm font-bold text-gray-900 uppercase tracking-wider flex items-center gap-2">
                <span className="w-2.5 h-2.5 rounded-full bg-[#D4AF37]" />
                Download Import Templates
              </h2>
              <p className="text-xs text-gray-600">
                Download the standardized Saranga import template. Do not modify header names.
              </p>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="border border-green-200 bg-green-50/40 rounded-xl p-4 flex flex-col justify-between hover:border-[#0D3826] transition">
                  <div>
                    <div className="flex items-center space-x-2">
                      <span className="text-xl">📊</span>
                      <h3 className="text-sm font-bold text-[#0D3826]">Excel Template (.xlsx)</h3>
                      <span className="text-[10px] bg-[#0D3826] text-white px-1.5 py-0.5 rounded font-semibold">Recommended</span>
                    </div>
                    <p className="text-xs text-gray-600 mt-2">
                      Includes formatted columns, sample jewellery items, and a dedicated Instructions sheet explaining codes and units.
                    </p>
                  </div>
                  <button
                    onClick={() => handleDownloadTemplate('xlsx')}
                    className="mt-4 w-full bg-[#0D3826] hover:bg-[#124b33] text-white text-xs font-semibold py-2.5 px-4 rounded-lg shadow-sm transition flex items-center justify-center gap-2"
                  >
                    <span>⬇</span>
                    <span>Download Saranga_Inventory_Import_Template.xlsx</span>
                  </button>
                </div>

                <div className="border border-gray-200 bg-gray-50/50 rounded-xl p-4 flex flex-col justify-between hover:border-gray-400 transition">
                  <div>
                    <div className="flex items-center space-x-2">
                      <span className="text-xl">📄</span>
                      <h3 className="text-sm font-bold text-gray-800">CSV Template (.csv)</h3>
                      <span className="text-[10px] bg-gray-200 text-gray-700 px-1.5 py-0.5 rounded font-semibold">Standard</span>
                    </div>
                    <p className="text-xs text-gray-600 mt-2">
                      Lightweight plain-text CSV template compatible with legacy spreadsheet software and automated export pipelines.
                    </p>
                  </div>
                  <button
                    onClick={() => handleDownloadTemplate('csv')}
                    className="mt-4 w-full bg-white hover:bg-gray-100 text-gray-800 border border-gray-300 text-xs font-semibold py-2.5 px-4 rounded-lg shadow-sm transition flex items-center justify-center gap-2"
                  >
                    <span>⬇</span>
                    <span>Download Saranga_Inventory_Import_Template.csv</span>
                  </button>
                </div>
              </div>

              {/* Informative Rules Box */}
              <div className="bg-amber-50/60 border border-amber-200 rounded-lg p-4 text-xs text-amber-900 space-y-1.5">
                <div className="font-bold flex items-center gap-1.5">
                  <span>ℹ️</span> Important Rules for Bulk Import:
                </div>
                <ul className="list-disc list-inside space-y-1 text-gray-700 pl-1">
                  <li><strong>Automated Identifiers:</strong> Do NOT provide Tag Number or Barcode columns. Unique tags (e.g. <code>{selectedBranch ? selectedBranch.code : 'BRANCH'}-CAT-######</code>) and barcodes (<code>SRG...</code>) are assigned atomically upon confirmation.</li>
                  <li><strong>Case-Insensitive Resolution:</strong> Codes (Material, Category, Purity, Stock Location) are matched case-insensitively and must already exist and be active.</li>
                  <li><strong>All-or-Nothing Atomicity:</strong> If even one row fails validation, no inventory items will be created. Fix all errors and re-upload.</li>
                  <li><strong>Making Charge Method:</strong> Use <code>PER_GRAM</code> or <code>PER_ITEM</code> (or <code>FIXED</code>).</li>
                </ul>
              </div>

              <div className="flex justify-end pt-2">
                <button
                  disabled={!selectedBranchId}
                  onClick={() => setCurrentStep('upload')}
                  className={`text-xs font-bold py-2.5 px-6 rounded-lg shadow-sm transition flex items-center gap-2 ${
                    selectedBranchId
                      ? 'bg-[#0D3826] hover:bg-[#124b33] text-white'
                      : 'bg-gray-300 text-gray-500 cursor-not-allowed'
                  }`}
                >
                  <span>Proceed to File Upload</span>
                  <span>→</span>
                </button>
              </div>
            </div>
          </div>
        )}

        {/* STEP 2: File Upload */}
        {currentStep === 'upload' && (
          <div className="bg-white border border-gray-200 rounded-xl p-6 shadow-sm space-y-6">
            <div className="flex justify-between items-center border-b border-gray-100 pb-3">
              <div>
                <h2 className="text-sm font-bold text-gray-900 uppercase tracking-wider flex items-center gap-2">
                  <span className="w-2.5 h-2.5 rounded-full bg-[#0D3826]" />
                  Step 2: Upload Excel or CSV File
                </h2>
                <p className="text-xs text-gray-500 mt-0.5">
                  Target Branch: <strong className="text-gray-900">{selectedBranch?.name} ({selectedBranch?.code})</strong>
                </p>
              </div>
              <button
                onClick={() => setCurrentStep('branch_template')}
                className="text-xs text-gray-500 hover:text-gray-800 underline"
              >
                Change Branch
              </button>
            </div>

            {/* Dropzone Box */}
            <div
              onClick={() => fileInputRef.current?.click()}
              className={`border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition ${
                selectedFile
                  ? 'border-[#0D3826] bg-green-50/20'
                  : 'border-gray-300 hover:border-[#0D3826] hover:bg-gray-50/50'
              }`}
            >
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx,.csv"
                onChange={handleFileChange}
                className="hidden"
              />

              <div className="text-4xl mb-3">📁</div>
              {selectedFile ? (
                <div>
                  <p className="text-sm font-bold text-[#0D3826]">{selectedFile.name}</p>
                  <p className="text-xs text-gray-500 mt-1">
                    Size: {(selectedFile.size / 1024).toFixed(1)} KB • Type: {selectedFile.name.endsWith('.xlsx') ? 'Excel' : 'CSV'}
                  </p>
                  <span className="inline-block mt-3 text-xs bg-[#0D3826] text-white px-3 py-1 rounded-full font-medium">
                    Click to replace file
                  </span>
                </div>
              ) : (
                <div>
                  <p className="text-sm font-bold text-gray-800">
                    Click to choose or drag and drop your spreadsheet
                  </p>
                  <p className="text-xs text-gray-500 mt-1">
                    Supports .xlsx (Excel) and .csv up to 5 MB (Max 1,000 rows per batch)
                  </p>
                </div>
              )}
            </div>

            <div className="flex justify-between items-center pt-2">
              <button
                onClick={() => setCurrentStep('branch_template')}
                className="text-xs font-semibold text-gray-600 hover:text-gray-900 px-4 py-2"
              >
                ← Back
              </button>

              <button
                disabled={!selectedFile || isLoading}
                onClick={handleValidateFile}
                className={`text-xs font-bold py-2.5 px-6 rounded-lg shadow-sm transition flex items-center gap-2 ${
                  selectedFile && !isLoading
                    ? 'bg-[#0D3826] hover:bg-[#124b33] text-white'
                    : 'bg-gray-300 text-gray-500 cursor-not-allowed'
                }`}
              >
                {isLoading ? (
                  <>
                    <span className="animate-spin text-sm">⟳</span>
                    <span>Validating Records...</span>
                  </>
                ) : (
                  <>
                    <span>Validate Spreadsheet</span>
                    <span>→</span>
                  </>
                )}
              </button>
            </div>
          </div>
        )}

        {/* STEP 3 & 4: Validation Results & Preview */}
        {currentStep === 'validation' && validationResult && (
          <div className="space-y-6">
            {/* Status Summary Banner */}
            <div className={`border rounded-xl p-5 shadow-sm flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4 ${
              validationResult.isAllValid
                ? 'bg-green-50 border-green-200 text-green-900'
                : 'bg-red-50 border-red-200 text-red-900'
            }`}>
              <div className="flex items-start gap-3">
                <span className="text-2xl">{validationResult.isAllValid ? '✅' : '⚠️'}</span>
                <div>
                  <h3 className="text-base font-bold">
                    {validationResult.isAllValid
                      ? 'Validation Succeeded: All Rows Are Valid'
                      : `Validation Failed: ${validationResult.invalidRows} Row(s) Contain Errors`}
                  </h3>
                  <p className="text-xs mt-0.5 opacity-90">
                    {validationResult.isAllValid
                      ? `All ${validationResult.totalRows} jewellery item records passed validation and can be digitized atomically.`
                      : 'You must fix the errors highlighted below in your spreadsheet and re-upload before confirming import.'}
                  </p>
                </div>
              </div>

              {/* Metrics Pills */}
              <div className="flex items-center space-x-2 text-xs font-semibold">
                <span className="bg-white/80 border border-gray-200 px-2.5 py-1 rounded-lg text-gray-800">
                  Total: {validationResult.totalRows}
                </span>
                <span className="bg-green-100 border border-green-300 px-2.5 py-1 rounded-lg text-green-800">
                  Valid: {validationResult.validRows}
                </span>
                <span className={`px-2.5 py-1 rounded-lg border ${
                  validationResult.invalidRows > 0
                    ? 'bg-red-100 border-red-300 text-red-800 font-bold'
                    : 'bg-gray-100 border-gray-200 text-gray-500'
                }`}>
                  Invalid: {validationResult.invalidRows}
                </span>
              </div>
            </div>

            {/* Tabs & Table Preview Container */}
            <div className="bg-white border border-gray-200 rounded-xl overflow-hidden shadow-sm">
              <div className="flex border-b border-gray-200 bg-gray-50 px-4 pt-3">
                {validationResult.invalidRows > 0 && (
                  <button
                    onClick={() => setActiveTab('errors')}
                    className={`text-xs font-bold px-4 py-2 border-b-2 transition ${
                      activeTab === 'errors'
                        ? 'border-red-600 text-red-700 bg-white rounded-t-lg'
                        : 'border-transparent text-gray-500 hover:text-gray-800'
                    }`}
                  >
                    Errors to Fix ({validationResult.invalidRows})
                  </button>
                )}

                <button
                  onClick={() => setActiveTab('valid')}
                  className={`text-xs font-bold px-4 py-2 border-b-2 transition ${
                    activeTab === 'valid'
                      ? 'border-[#0D3826] text-[#0D3826] bg-white rounded-t-lg'
                      : 'border-transparent text-gray-500 hover:text-gray-800'
                  }`}
                >
                  Valid Rows Preview ({validationResult.validRows})
                </button>
              </div>

              {/* Tab 1: Errors Table */}
              {activeTab === 'errors' && (
                <div className="p-4">
                  <div className="overflow-x-auto">
                    <table className="w-full text-left text-xs border-collapse">
                      <thead>
                        <tr className="bg-red-50 text-red-900 border-b border-red-200 font-semibold">
                          <th className="py-2.5 px-3 w-20">Row #</th>
                          <th className="py-2.5 px-3 w-36">Field</th>
                          <th className="py-2.5 px-3">Error Message</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-100">
                        {validationResult.errors.map((err, idx) => (
                          <tr key={idx} className="hover:bg-red-50/30 transition">
                            <td className="py-2.5 px-3 font-mono font-bold text-red-700">Row {err.rowNumber}</td>
                            <td className="py-2.5 px-3 font-mono text-gray-800">{err.field}</td>
                            <td className="py-2.5 px-3 text-red-700 font-medium">{err.message}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              {/* Tab 2: Valid Rows Preview Table */}
              {activeTab === 'valid' && (
                <div className="overflow-x-auto">
                  <table className="w-full text-left text-xs border-collapse">
                    <thead>
                      <tr className="bg-gray-50 text-gray-700 border-b border-gray-200 font-semibold">
                        <th className="py-2.5 px-3 w-16">Row #</th>
                        <th className="py-2.5 px-3">Item Name</th>
                        <th className="py-2.5 px-3">Material</th>
                        <th className="py-2.5 px-3">Category</th>
                        <th className="py-2.5 px-3">Purity</th>
                        <th className="py-2.5 px-3 text-right">Gross (g)</th>
                        <th className="py-2.5 px-3 text-right">Stone (g)</th>
                        <th className="py-2.5 px-3 text-right">Net (g)</th>
                        <th className="py-2.5 px-3">Location</th>
                        <th className="py-2.5 px-3">Charge</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {validationResult.rows.filter(r => r.isValid).map((row) => (
                        <tr key={row.rowNumber} className="hover:bg-gray-50/50 transition">
                          <td className="py-2.5 px-3 font-mono text-gray-500">{row.rowNumber}</td>
                          <td className="py-2.5 px-3 font-medium text-gray-900">
                            {row.itemName}
                            {row.itemCode && <span className="ml-1.5 text-[10px] text-gray-400">({row.itemCode})</span>}
                          </td>
                          <td className="py-2.5 px-3 text-gray-700">{row.materialCode}</td>
                          <td className="py-2.5 px-3 text-gray-700">{row.categoryCode}</td>
                          <td className="py-2.5 px-3 text-gray-700">{row.purityCode}</td>
                          <td className="py-2.5 px-3 text-right font-mono">{row.grossWeight.toFixed(4)}</td>
                          <td className="py-2.5 px-3 text-right font-mono text-gray-500">{row.stoneWeight.toFixed(4)}</td>
                          <td className="py-2.5 px-3 text-right font-mono font-semibold text-[#0D3826]">{row.netWeight.toFixed(4)}</td>
                          <td className="py-2.5 px-3">
                            <span className="bg-gray-100 text-gray-700 px-2 py-0.5 rounded text-[11px] font-mono">
                              {row.stockLocationCode}
                            </span>
                          </td>
                          <td className="py-2.5 px-3 text-gray-600 text-[11px]">
                            {row.makingChargeMethod}: Rs. {row.makingChargeValue.toLocaleString('en-LK', { minimumFractionDigits: 2 })}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            {/* Bottom Actions */}
            <div className="flex justify-between items-center bg-white border border-gray-200 rounded-xl p-4 shadow-sm">
              <button
                onClick={() => {
                  setSelectedFile(null);
                  setValidationResult(null);
                  setCurrentStep('upload');
                }}
                className="text-xs font-semibold text-gray-600 hover:text-gray-900 border border-gray-300 rounded-lg px-4 py-2 transition"
              >
                ← Upload Different File
              </button>

              {validationResult.isAllValid ? (
                <button
                  onClick={() => setCurrentStep('confirm')}
                  className="bg-[#0D3826] hover:bg-[#124b33] text-white text-xs font-bold py-2.5 px-6 rounded-lg shadow-sm transition flex items-center gap-2"
                >
                  <span>Proceed to Confirmation</span>
                  <span>→</span>
                </button>
              ) : (
                <div className="text-right">
                  <span className="text-xs text-red-600 font-semibold mr-3">
                    Fix {validationResult.invalidRows} errors to proceed
                  </span>
                  <button
                    disabled
                    className="bg-gray-300 text-gray-500 text-xs font-bold py-2.5 px-6 rounded-lg cursor-not-allowed"
                  >
                    Cannot Confirm with Errors
                  </button>
                </div>
              )}
            </div>
          </div>
        )}

        {/* STEP 5: Confirm Import */}
        {currentStep === 'confirm' && validationResult && (
          <div className="bg-white border border-gray-200 rounded-xl p-6 shadow-sm space-y-6">
            <h2 className="text-sm font-bold text-gray-900 uppercase tracking-wider flex items-center gap-2 border-b border-gray-100 pb-3">
              <span className="w-2.5 h-2.5 rounded-full bg-[#0D3826]" />
              Step 4: Confirm Atomic Stock Creation
            </h2>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="border border-gray-200 rounded-lg p-3 bg-gray-50">
                <span className="text-xs text-gray-500">Target Branch</span>
                <p className="text-sm font-bold text-gray-900 mt-1">{validationResult.branchName} ({validationResult.branchCode})</p>
              </div>
              <div className="border border-gray-200 rounded-lg p-3 bg-gray-50">
                <span className="text-xs text-gray-500">Source Spreadsheet</span>
                <p className="text-sm font-bold text-gray-900 mt-1">{validationResult.originalFileName} ({validationResult.fileType})</p>
              </div>
              <div className="border border-gray-200 rounded-lg p-3 bg-gray-50">
                <span className="text-xs text-gray-500">Items to Digitize</span>
                <p className="text-sm font-bold text-[#0D3826] mt-1">{validationResult.validRows} Physical Jewellery Items</p>
              </div>
            </div>

            <div className="bg-amber-50/70 border border-amber-200 rounded-lg p-4 text-xs text-amber-900 space-y-2">
              <div className="font-bold flex items-center gap-2">
                <span>🛡️</span> Transactional Safety & Idempotency:
              </div>
              <p>
                Clicking confirm will commit all <strong>{validationResult.validRows}</strong> jewellery items inside a single atomic database transaction.
                Each piece will be generated a unique sequence tag (<code>{validationResult.branchCode}-CAT-######</code>) and barcode (<code>SRG...</code>).
              </p>
              <p className="text-[11px] text-gray-600 font-mono">
                Idempotency Token: {idempotencyKey}
              </p>
            </div>

            <div className="flex justify-between items-center pt-2">
              <button
                disabled={isSubmitting}
                onClick={() => setCurrentStep('validation')}
                className="text-xs font-semibold text-gray-600 hover:text-gray-900 px-4 py-2"
              >
                ← Back to Validation Preview
              </button>

              <button
                disabled={isSubmitting}
                onClick={handleConfirmImport}
                className={`text-xs font-bold py-3 px-8 rounded-lg shadow-sm transition flex items-center gap-2 ${
                  !isSubmitting
                    ? 'bg-[#0D3826] hover:bg-[#124b33] text-white ring-2 ring-[#D4AF37]/50'
                    : 'bg-gray-400 text-white cursor-not-allowed'
                }`}
              >
                {isSubmitting ? (
                  <>
                    <span className="animate-spin text-sm">⟳</span>
                    <span>Digitizing Inventory Items...</span>
                  </>
                ) : (
                  <>
                    <span>Confirm & Digitize {validationResult.validRows} Items</span>
                    <span>✓</span>
                  </>
                )}
              </button>
            </div>
          </div>
        )}

        {/* STEP 6: Import Summary */}
        {currentStep === 'summary' && importResult && (
          <div className="space-y-6">
            <div className="bg-green-50 border border-green-200 rounded-xl p-6 text-center space-y-3 shadow-sm">
              <div className="w-12 h-12 bg-[#0D3826] text-white rounded-full flex items-center justify-center text-xl mx-auto shadow-md">
                ✓
              </div>
              <h2 className="text-xl font-serif font-bold text-[#0D3826]">
                Bulk Import Completed Successfully!
              </h2>
              <p className="text-xs text-gray-700 max-w-lg mx-auto">
                <strong>{importResult.totalImported}</strong> individual jewellery items were created, tagged, barcoded, and committed to branch stock.
              </p>
              <div className="flex justify-center items-center gap-3 pt-1">
                <span className="text-[11px] bg-white border border-gray-200 px-3 py-1 rounded-full text-gray-600 font-mono">
                  Batch ID: {importResult.batchId}
                </span>
                <span className="text-[11px] bg-white border border-gray-200 px-3 py-1 rounded-full text-gray-600">
                  Confirmed: {new Date(importResult.confirmedAt).toLocaleTimeString()}
                </span>
              </div>
            </div>

            {/* Generated Items Table */}
            <div className="bg-white border border-gray-200 rounded-xl overflow-hidden shadow-sm">
              <div className="p-4 border-b border-gray-100 flex justify-between items-center">
                <h3 className="text-xs font-bold text-gray-800 uppercase tracking-wider">
                  Generated Inventory Tags & Barcodes ({importResult.createdItems.length})
                </h3>
                <Link
                  href="/inventory"
                  className="text-xs text-[#0D3826] hover:underline font-semibold"
                >
                  View in Inventory List →
                </Link>
              </div>

              <div className="max-h-96 overflow-y-auto">
                <table className="w-full text-left text-xs border-collapse">
                  <thead className="sticky top-0 bg-gray-50 border-b border-gray-200 font-semibold text-gray-700">
                    <tr>
                      <th className="py-2.5 px-3">Item Name</th>
                      <th className="py-2.5 px-3 font-mono">Generated Tag</th>
                      <th className="py-2.5 px-3 font-mono">Barcode</th>
                      <th className="py-2.5 px-3 text-right">Gross (g)</th>
                      <th className="py-2.5 px-3 text-right">Net (g)</th>
                      <th className="py-2.5 px-3">Location</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {importResult.createdItems.map((item) => (
                      <tr key={item.id} className="hover:bg-gray-50/50 transition">
                        <td className="py-2.5 px-3 font-medium text-gray-900">{item.itemName}</td>
                        <td className="py-2.5 px-3 font-mono font-bold text-[#0D3826]">{item.tagNumber}</td>
                        <td className="py-2.5 px-3 font-mono text-gray-600">{item.barcode}</td>
                        <td className="py-2.5 px-3 text-right font-mono">{item.grossWeight.toFixed(4)}</td>
                        <td className="py-2.5 px-3 text-right font-mono font-semibold text-[#0D3826]">{item.netWeight.toFixed(4)}</td>
                        <td className="py-2.5 px-3">
                          <span className="bg-gray-100 text-gray-700 px-2 py-0.5 rounded text-[11px] font-mono">
                            {item.stockLocationCode}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

            {/* Final Actions */}
            <div className="flex justify-between items-center pt-2">
              <button
                onClick={() => {
                  setSelectedFile(null);
                  setValidationResult(null);
                  setImportResult(null);
                  setCurrentStep('branch_template');
                }}
                className="text-xs font-semibold text-gray-700 hover:text-gray-900 bg-white border border-gray-300 rounded-lg px-4 py-2.5 shadow-sm transition"
              >
                + Import Another Batch
              </button>

              <Link
                href="/inventory"
                className="bg-[#0D3826] hover:bg-[#124b33] text-white text-xs font-bold py-2.5 px-6 rounded-lg shadow-sm transition"
              >
                Go to Inventory Management
              </Link>
            </div>
          </div>
        )}
      </div>
    </AppLayout>
  );
}
