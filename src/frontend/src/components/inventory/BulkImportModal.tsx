'use client';

import React, { useState, useEffect, useRef } from 'react';
import { useBranch } from '@/lib/branch-context';
import {
  bulkImportApi,
  BulkImportValidationResult,
  BulkImportResult,
  Branch,
  branchApi,
} from '@/lib/api';
import { Modal } from '@/components/ui/Modal';
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

interface BulkImportModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

type ModalView = 'upload' | 'review' | 'complete';

export const BulkImportModal: React.FC<BulkImportModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
}) => {
  const { activeBranchId } = useBranch();

  const [view, setView] = useState<ModalView>('upload');
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
    if (!isOpen) return;
    branchApi
      .getBranches(true)
      .then((data) => {
        setBranches(data);
        if (activeBranchId) {
          setSelectedBranchId(activeBranchId);
        } else if (data.length > 0) {
          setSelectedBranchId(data[0].id);
        }
      })
      .catch((err: any) => setError(err.message || 'Failed to load branches.'));
  }, [isOpen, activeBranchId]);

  const resetState = () => {
    setSelectedFile(null);
    setValidationResult(null);
    setImportResult(null);
    setError(null);
    setView('upload');
  };

  const handleClose = () => {
    if (isValidating || isConfirming) return;
    resetState();
    onClose();
  };

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
      setView('review');
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
      setView('complete');
      onSuccess?.();
    } catch (err: any) {
      setError(err.message || 'Bulk import confirmation failed.');
    } finally {
      setIsConfirming(false);
    }
  };

  const title =
    view === 'upload'
      ? 'Bulk Import Jewellery'
      : view === 'review'
      ? 'Review Import Batch'
      : 'Import Complete';

  const description =
    view === 'upload'
      ? 'Upload an Excel (.xlsx) or CSV (.csv) spreadsheet to add stock in one batch.'
      : view === 'review'
      ? `${validationResult?.originalFileName ?? ''} • ${validationResult?.branchName ?? ''}`
      : `${importResult?.totalImported ?? 0} items added with generated tags and barcodes.`;

  const footer =
    view === 'upload' ? (
      <>
        <Button variant="ghost" size="sm" onClick={handleClose} disabled={isValidating}>
          Cancel
        </Button>
        <Button
          variant="primary"
          size="md"
          isLoading={isValidating}
          disabled={!selectedBranchId || !selectedFile}
          onClick={handleValidateFile}
          rightIcon={<ArrowRight className="w-4 h-4" />}
        >
          Validate File
        </Button>
      </>
    ) : view === 'review' && validationResult ? (
      <>
        <Button
          variant="secondary"
          size="sm"
          onClick={resetState}
          disabled={isConfirming}
          leftIcon={<ArrowLeft className="w-3.5 h-3.5" />}
        >
          Choose Another File
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
            ? `Import ${validationResult.validRows} Items`
            : 'Fix Errors to Import'}
        </Button>
      </>
    ) : (
      <>
        <Button variant="secondary" size="sm" onClick={resetState}>
          Import Another
        </Button>
        <Button variant="primary" size="md" onClick={handleClose}>
          Done
        </Button>
      </>
    );

  return (
    <Modal
      isOpen={isOpen}
      onClose={handleClose}
      title={title}
      description={description}
      maxWidth={view === 'upload' ? 'xl' : '3xl'}
      footer={footer}
    >
      {error && (
        <div className="bg-rose-50 border border-rose-200 text-rose-800 px-3.5 py-2.5 rounded-lg text-xs flex items-center justify-between">
          <div className="flex items-center gap-2">
            <AlertTriangle className="w-4 h-4 text-rose-600 flex-shrink-0" />
            <span>{error}</span>
          </div>
          <button onClick={() => setError(null)} className="text-rose-500 hover:text-rose-800">
            <X className="w-4 h-4" />
          </button>
        </div>
      )}

      {view === 'upload' && (
        <div className="space-y-4">
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 items-end">
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

            <div>
              <span className="block text-xs font-semibold text-[#202824] uppercase tracking-wider mb-1.5">
                Template
              </span>
              <div className="flex items-center gap-2">
                <div className="inline-flex rounded-lg border border-[#E2E6E3] bg-[#F8F7F4] p-0.5">
                  {(['xlsx', 'csv'] as const).map((fmt) => (
                    <button
                      key={fmt}
                      type="button"
                      onClick={() => setTemplateFormat(fmt)}
                      className={`px-2 py-1.5 text-[11px] font-semibold rounded-md uppercase transition ${
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
                  size="sm"
                  isLoading={isDownloading}
                  onClick={handleDownloadTemplate}
                  leftIcon={<Download className="w-3.5 h-3.5" />}
                  className="flex-1"
                >
                  Download (.{templateFormat})
                </Button>
              </div>
            </div>
          </div>

          <div
            onClick={() => fileInputRef.current?.click()}
            onDragOver={(e) => {
              e.preventDefault();
              setIsDragging(true);
            }}
            onDragLeave={() => setIsDragging(false)}
            onDrop={handleDrop}
            className={`border-2 border-dashed rounded-xl p-6 text-center cursor-pointer transition-colors ${
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
              <div className="space-y-1">
                <FileSpreadsheet className="w-7 h-7 text-[#123B32] mx-auto" />
                <p className="text-sm font-bold text-[#123B32]">{selectedFile.name}</p>
                <p className="text-xs text-[#69736E]">
                  {(selectedFile.size / 1024).toFixed(1)} KB •{' '}
                  {selectedFile.name.toLowerCase().endsWith('.xlsx') ? 'Excel (.xlsx)' : 'CSV (.csv)'}
                </p>
                <p className="text-[11px] font-medium text-[#B89A60]">Click or drop another file to replace</p>
              </div>
            ) : (
              <div className="space-y-1">
                <UploadCloud className="w-7 h-7 text-[#69736E] mx-auto" />
                <p className="text-sm font-semibold text-[#202824]">
                  Click to choose or drag and drop a file
                </p>
                <p className="text-xs text-[#69736E]">
                  Supports <span className="font-mono">.xlsx</span> and <span className="font-mono">.csv</span> up to 5 MB
                </p>
              </div>
            )}
          </div>

          <p className="text-[11px] text-[#69736E]">
            Tags and barcodes are generated automatically. All rows are validated before import.
          </p>
        </div>
      )}

      {view === 'review' && validationResult && (
        <div className="space-y-3">
          <div
            className={`border rounded-lg p-3.5 flex flex-wrap justify-between items-center gap-2 ${
              validationResult.isAllValid
                ? 'bg-emerald-50 border-emerald-200 text-emerald-950'
                : 'bg-rose-50 border-rose-200 text-rose-950'
            }`}
          >
            <div className="flex items-center gap-2 text-xs font-semibold">
              {validationResult.isAllValid ? (
                <CheckCircle2 className="w-4 h-4 text-emerald-700 flex-shrink-0" />
              ) : (
                <AlertTriangle className="w-4 h-4 text-rose-700 flex-shrink-0" />
              )}
              <span>
                {validationResult.isAllValid
                  ? `All ${validationResult.validRows} rows are valid and ready to import.`
                  : `${validationResult.invalidRows} row(s) have errors. Fix them and re-upload.`}
              </span>
            </div>

            <div className="flex items-center gap-1.5">
              <Badge variant="neutral" size="sm">Total: {validationResult.totalRows}</Badge>
              <Badge variant="success" size="sm">Valid: {validationResult.validRows}</Badge>
              {validationResult.invalidRows > 0 && (
                <Badge variant="error" size="sm">Errors: {validationResult.invalidRows}</Badge>
              )}
            </div>
          </div>

          <div className="border border-[#E2E6E3] rounded-lg overflow-hidden">
            <div className="flex border-b border-[#E2E6E3] bg-[#F8F7F4] px-3 pt-2 gap-1">
              {validationResult.invalidRows > 0 && (
                <button
                  onClick={() => setActiveTab('errors')}
                  className={`text-xs font-semibold px-3 py-1.5 border-b-2 transition ${
                    activeTab === 'errors'
                      ? 'border-rose-600 text-rose-700 bg-white rounded-t-md'
                      : 'border-transparent text-[#69736E] hover:text-[#202824]'
                  }`}
                >
                  Errors ({validationResult.invalidRows})
                </button>
              )}
              <button
                onClick={() => setActiveTab('valid')}
                className={`text-xs font-semibold px-3 py-1.5 border-b-2 transition ${
                  activeTab === 'valid'
                    ? 'border-[#123B32] text-[#123B32] bg-white rounded-t-md'
                    : 'border-transparent text-[#69736E] hover:text-[#202824]'
                }`}
              >
                Valid Rows ({validationResult.validRows})
              </button>
            </div>

            {activeTab === 'errors' && (
              <div className="overflow-x-auto max-h-64">
                <table className="w-full text-left text-xs">
                  <thead className="sticky top-0 bg-rose-50 text-rose-900 border-b border-rose-100 font-semibold">
                    <tr>
                      <th className="py-2 px-3 w-16">Row</th>
                      <th className="py-2 px-3 w-32">Field</th>
                      <th className="py-2 px-3">Error</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#E2E6E3]">
                    {validationResult.errors.map((err, idx) => (
                      <tr key={idx} className="hover:bg-rose-50/30">
                        <td className="py-2 px-3 font-mono font-semibold text-rose-700">#{err.rowNumber}</td>
                        <td className="py-2 px-3 font-mono text-[#202824]">{err.field}</td>
                        <td className="py-2 px-3 text-rose-700">{err.message}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {activeTab === 'valid' && (
              <div className="overflow-x-auto max-h-64">
                <table className="w-full text-left text-xs">
                  <thead className="sticky top-0 bg-[#F8F7F4] text-[#69736E] border-b border-[#E2E6E3] font-semibold">
                    <tr>
                      <th className="py-2 px-3 w-12">Row</th>
                      <th className="py-2 px-3">Item</th>
                      <th className="py-2 px-3">Material</th>
                      <th className="py-2 px-3">Category</th>
                      <th className="py-2 px-3">Purity</th>
                      <th className="py-2 px-3 text-right">Net (g)</th>
                      <th className="py-2 px-3">Location</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#E2E6E3]">
                    {validationResult.rows
                      .filter((r) => r.isValid)
                      .map((row) => (
                        <tr key={row.rowNumber} className="hover:bg-[#F8F7F4]/60">
                          <td className="py-2 px-3 font-mono text-[#69736E]">{row.rowNumber}</td>
                          <td className="py-2 px-3 font-medium text-[#202824]">{row.itemName}</td>
                          <td className="py-2 px-3">{row.materialCode}</td>
                          <td className="py-2 px-3">{row.categoryCode}</td>
                          <td className="py-2 px-3">{row.purityCode}</td>
                          <td className="py-2 px-3 text-right font-mono font-semibold text-[#123B32] tabular-nums">
                            {row.netWeight.toFixed(4)}
                          </td>
                          <td className="py-2 px-3 font-mono text-[11px]">{row.stockLocationCode}</td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}

      {view === 'complete' && importResult && (
        <div className="space-y-3">
          <div className="bg-emerald-50 border border-emerald-200 rounded-lg p-4 flex items-center gap-3">
            <CheckCircle2 className="w-6 h-6 text-[#123B32] flex-shrink-0" />
            <div className="text-xs text-[#202824]">
              <p className="font-bold text-[#123B32]">
                Successfully imported {importResult.totalImported} jewellery items
              </p>
              <p className="text-[#69736E] mt-0.5">
                Unique tags and barcodes have been assigned to branch stock.
              </p>
            </div>
          </div>

          <div className="border border-[#E2E6E3] rounded-lg overflow-hidden max-h-64 overflow-y-auto">
            <table className="w-full text-left text-xs">
              <thead className="sticky top-0 bg-[#F8F7F4] border-b border-[#E2E6E3] font-semibold text-[#69736E]">
                <tr>
                  <th className="py-2 px-3">Item Name</th>
                  <th className="py-2 px-3">Tag Number</th>
                  <th className="py-2 px-3">Barcode</th>
                  <th className="py-2 px-3 text-right">Net (g)</th>
                  <th className="py-2 px-3">Location</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#E2E6E3]">
                {importResult.createdItems.map((item) => (
                  <tr key={item.id} className="hover:bg-[#F8F7F4]/60">
                    <td className="py-2 px-3 font-medium text-[#202824]">{item.itemName}</td>
                    <td className="py-2 px-3 font-mono font-bold text-[#123B32]">{item.tagNumber}</td>
                    <td className="py-2 px-3 font-mono text-[#69736E]">{item.barcode}</td>
                    <td className="py-2 px-3 text-right font-mono font-semibold text-[#123B32] tabular-nums">
                      {item.netWeight.toFixed(4)}
                    </td>
                    <td className="py-2 px-3 font-mono text-[11px]">{item.stockLocationCode}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </Modal>
  );
};
