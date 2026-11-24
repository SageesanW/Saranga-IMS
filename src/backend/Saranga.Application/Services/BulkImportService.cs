using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Exceptions;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.DTOs.Inventory;
using Saranga.Domain.Constants;
using Saranga.Domain.Entities;
using Saranga.Domain.Enums;

namespace Saranga.Application.Services;

public class BulkImportService : IBulkImportService
{
    private readonly IAppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBranchAuthorizationService _branchAuthService;
    private readonly ITagGeneratorService _tagGeneratorService;
    private readonly IInventoryValidationService _inventoryValidationService;
    private readonly ILogger<BulkImportService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public BulkImportService(
        IAppDbContext context,
        ICurrentUserService currentUserService,
        IBranchAuthorizationService branchAuthService,
        ITagGeneratorService tagGeneratorService,
        IInventoryValidationService inventoryValidationService,
        ILogger<BulkImportService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _branchAuthService = branchAuthService;
        _tagGeneratorService = tagGeneratorService;
        _inventoryValidationService = inventoryValidationService;
        _logger = logger;
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> GenerateTemplateAsync(string format, CancellationToken cancellationToken = default)
    {
        format = (format ?? "xlsx").Trim().ToLowerInvariant();

        if (format == "csv")
        {
            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Item Name,Item Code,Material Code,Category Code,Purity Code,Gross Weight,Stone Weight,Piece Count,Stock Location Code,Making Charge Method,Making Charge Value,Description");
            csvBuilder.AppendLine("Gold Bangle 22K Plain,BGL-001,GOLD,BANGLES,22K,15.5000,0.5000,1,DISPLAY-01,PER_GRAM,2500.00,Handcrafted traditional single bangle");
            csvBuilder.AppendLine("Gold Ring Diamond Cut,RNG-002,GOLD,RINGS,22K,6.2000,0.0000,1,VAULT-01,FIXED,4500.00,Classic diamond cut wedding band");

            var bytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            return (bytes, "text/csv", "Saranga_Inventory_Import_Template.csv");
        }

        using var workbook = new XLWorkbook();
        
        // Sheet 1: Inventory Items
        var ws = workbook.Worksheets.Add("Inventory Items");
        var headers = new[]
        {
            "Item Name", "Item Code", "Material Code", "Category Code", "Purity Code",
            "Gross Weight", "Stone Weight", "Piece Count", "Stock Location Code",
            "Making Charge Method", "Making Charge Value", "Description"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.FromHtml("#D4AF37"); // Gold
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0D3826"); // Royal Green
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Sample Row 1
        ws.Cell(2, 1).Value = "Gold Bangle 22K Plain";
        ws.Cell(2, 2).Value = "BGL-001";
        ws.Cell(2, 3).Value = "GOLD";
        ws.Cell(2, 4).Value = "BANGLES";
        ws.Cell(2, 5).Value = "22K";
        ws.Cell(2, 6).Value = 15.5000;
        ws.Cell(2, 7).Value = 0.5000;
        ws.Cell(2, 8).Value = 1;
        ws.Cell(2, 9).Value = "DISPLAY-01";
        ws.Cell(2, 10).Value = "PER_GRAM";
        ws.Cell(2, 11).Value = 2500.00;
        ws.Cell(2, 12).Value = "Handcrafted traditional single bangle";

        // Sample Row 2
        ws.Cell(3, 1).Value = "Gold Ring Diamond Cut";
        ws.Cell(3, 2).Value = "RNG-002";
        ws.Cell(3, 3).Value = "GOLD";
        ws.Cell(3, 4).Value = "RINGS";
        ws.Cell(3, 5).Value = "22K";
        ws.Cell(3, 6).Value = 6.2000;
        ws.Cell(3, 7).Value = 0.0000;
        ws.Cell(3, 8).Value = 1;
        ws.Cell(3, 9).Value = "VAULT-01";
        ws.Cell(3, 10).Value = "FIXED";
        ws.Cell(3, 11).Value = 4500.00;
        ws.Cell(3, 12).Value = "Classic diamond cut wedding band";

        ws.Columns().AdjustToContents();

        // Sheet 2: Reference Guide
        var guideWs = workbook.Worksheets.Add("Instructions");
        guideWs.Cell(1, 1).Value = "Saranga Bulk Inventory Import Reference Guide";
        guideWs.Cell(1, 1).Style.Font.Bold = true;
        guideWs.Cell(1, 1).Style.Font.FontSize = 14;

        guideWs.Cell(3, 1).Value = "Field";
        guideWs.Cell(3, 2).Value = "Required";
        guideWs.Cell(3, 3).Value = "Rules & Allowed Values";
        guideWs.Row(3).Style.Font.Bold = true;

        var guideRows = new[]
        {
            ("Item Name", "Yes", "Non-empty string describing the jewellery piece."),
            ("Item Code", "No", "Optional custom SKU or manufacturer stock code."),
            ("Material Code", "Yes", "Active material code (e.g. GOLD, SILVER, PLATINUM). Case-insensitive."),
            ("Category Code", "Yes", "Active category code (e.g. BANGLES, NECKLACE, RINGS, CHAINS)."),
            ("Purity Code", "Yes", "Active purity code (e.g. 22K, 24K, 18K, 925)."),
            ("Gross Weight", "Yes", "Decimal in grams, must be strictly greater than 0."),
            ("Stone Weight", "No", "Decimal in grams, >= 0 and <= Gross Weight. Defaults to 0 if blank."),
            ("Piece Count", "Yes", "Integer, must be at least 1. Defaults to 1 if blank."),
            ("Stock Location Code", "Yes", "Active stock location belonging to the selected branch (e.g. DISPLAY-01, VAULT-01)."),
            ("Making Charge Method", "Yes", "Calculation method: FIXED, PER_GRAM, or PERCENTAGE."),
            ("Making Charge Value", "Yes", "Decimal value >= 0 (e.g. fixed amount, rate per gram, or percent)."),
            ("Description", "No", "Optional additional notes.")
        };

        for (int i = 0; i < guideRows.Length; i++)
        {
            guideWs.Cell(4 + i, 1).Value = guideRows[i].Item1;
            guideWs.Cell(4 + i, 2).Value = guideRows[i].Item2;
            guideWs.Cell(4 + i, 3).Value = guideRows[i].Item3;
        }

        guideWs.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return (memoryStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Saranga_Inventory_Import_Template.xlsx");
    }

    public async Task<BulkImportValidationResultDto> ValidateImportFileAsync(Guid branchId, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        if (stream == null || stream.Length == 0)
        {
            throw new BusinessRuleException("The uploaded file is empty.");
        }

        if (stream.Length > BulkImportConstants.MaxFileSizeBytes)
        {
            throw new BusinessRuleException($"File size exceeds maximum permitted limit of {BulkImportConstants.MaxFileSizeMb} MB.");
        }

        // Branch verification
        await _branchAuthService.EnsureBranchAccessAsync(branchId, cancellationToken);
        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken);
        if (branch == null || !branch.IsActive)
        {
            throw new BusinessRuleException("Selected branch does not exist or is inactive.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".csv")
        {
            throw new BusinessRuleException("Unsupported file type. Only Excel (.xlsx) and CSV (.csv) files are supported.");
        }

        string fileType = ext == ".xlsx" ? "XLSX" : "CSV";

        // Parse raw rows from file
        List<RawImportRow> rawRows;
        if (ext == ".xlsx")
        {
            rawRows = ParseExcel(stream);
        }
        else
        {
            rawRows = ParseCsv(stream);
        }

        if (rawRows.Count == 0)
        {
            throw new BusinessRuleException("The uploaded file contains no data rows.");
        }

        if (rawRows.Count > BulkImportConstants.MaxAllowedRows)
        {
            throw new BusinessRuleException($"File contains {rawRows.Count} rows, which exceeds the maximum allowed limit of {BulkImportConstants.MaxAllowedRows} rows per import batch.");
        }

        // Efficient batch resolution: Load all branch stock locations, materials, categories, purities into lookup dictionaries
        var locations = await _context.StockLocations
            .AsNoTracking()
            .Where(l => l.BranchId == branchId)
            .ToListAsync(cancellationToken);

        var allLocations = await _context.StockLocations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var materials = await _context.Materials
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var categories = await _context.Categories
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var purities = await _context.Purities
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var locationByCode = locations.ToDictionary(l => l.Code.Trim().ToUpperInvariant(), l => l);
        var allLocationByCode = allLocations.ToDictionary(l => l.Code.Trim().ToUpperInvariant(), l => l);
        var materialByCode = materials.ToDictionary(m => m.Code.Trim().ToUpperInvariant(), m => m);
        var categoryByCode = categories.ToDictionary(c => c.Code.Trim().ToUpperInvariant(), c => c);
        var purityByCode = purities.ToDictionary(p => p.Code.Trim().ToUpperInvariant(), p => p);

        var validatedRows = new List<BulkImportRowDto>();
        var globalErrors = new List<BulkImportErrorDto>();

        for (int i = 0; i < rawRows.Count; i++)
        {
            var raw = rawRows[i];
            int rowNumber = raw.RowNumber;
            var errors = new List<string>();

            // 1. Item Name
            var nameErr = _inventoryValidationService.ValidateItemName(raw.ItemName);
            if (nameErr != null)
            {
                errors.Add(nameErr);
                globalErrors.Add(new BulkImportErrorDto(rowNumber, "ItemName", nameErr));
            }

            // 2. Material Code
            Guid? materialId = null;
            if (string.IsNullOrWhiteSpace(raw.MaterialCode))
            {
                var err = "Material Code is required.";
                errors.Add(err);
                globalErrors.Add(new BulkImportErrorDto(rowNumber, "MaterialCode", err));
            }
            else
            {
                var norm = raw.MaterialCode.Trim().ToUpperInvariant();
                if (!materialByCode.TryGetValue(norm, out var mat))
                {
                    var err = $"Material code \"{raw.MaterialCode}\" was not found.";
                    errors.Add(err);
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, "MaterialCode", err));
                }
                else
                {
                    var activeErr = _inventoryValidationService.ValidateEntityActive("Material", $"\"{mat.Name}\" ({norm})", mat.IsActive);
                    if (activeErr != null)
                    {
                        errors.Add(activeErr);
                        globalErrors.Add(new BulkImportErrorDto(rowNumber, "MaterialCode", activeErr));
                    }
                    else
                    {
                        materialId = mat.Id;
                    }
                }
            }

            // 3. Category Code
            Guid? categoryId = null;
            if (string.IsNullOrWhiteSpace(raw.CategoryCode))
            {
                var err = "Category Code is required.";
                errors.Add(err);
                globalErrors.Add(new BulkImportErrorDto(rowNumber, "CategoryCode", err));
            }
            else
            {
                var norm = raw.CategoryCode.Trim().ToUpperInvariant();
                if (!categoryByCode.TryGetValue(norm, out var cat))
                {
                    var err = $"Category code \"{raw.CategoryCode}\" was not found.";
                    errors.Add(err);
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, "CategoryCode", err));
                }
                else
                {
                    var activeErr = _inventoryValidationService.ValidateEntityActive("Category", $"\"{cat.Name}\" ({norm})", cat.IsActive);
                    if (activeErr != null)
                    {
                        errors.Add(activeErr);
                        globalErrors.Add(new BulkImportErrorDto(rowNumber, "CategoryCode", activeErr));
                    }
                    else
                    {
                        categoryId = cat.Id;
                    }
                }
            }

            // 4. Purity Code
            Guid? purityId = null;
            if (string.IsNullOrWhiteSpace(raw.PurityCode))
            {
                var err = "Purity Code is required.";
                errors.Add(err);
                globalErrors.Add(new BulkImportErrorDto(rowNumber, "PurityCode", err));
            }
            else
            {
                var norm = raw.PurityCode.Trim().ToUpperInvariant();
                if (!purityByCode.TryGetValue(norm, out var pur))
                {
                    var err = $"Purity code \"{raw.PurityCode}\" was not found.";
                    errors.Add(err);
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, "PurityCode", err));
                }
                else
                {
                    var activeErr = _inventoryValidationService.ValidateEntityActive("Purity", $"\"{pur.Name}\" ({norm})", pur.IsActive);
                    if (activeErr != null)
                    {
                        errors.Add(activeErr);
                        globalErrors.Add(new BulkImportErrorDto(rowNumber, "PurityCode", activeErr));
                    }
                    else
                    {
                        purityId = pur.Id;
                    }
                }
            }

            // 5. Stock Location Code (Must exist, belong to branch, and be active)
            Guid? stockLocationId = null;
            if (string.IsNullOrWhiteSpace(raw.StockLocationCode))
            {
                var err = "Stock Location Code is required.";
                errors.Add(err);
                globalErrors.Add(new BulkImportErrorDto(rowNumber, "StockLocationCode", err));
            }
            else
            {
                var norm = raw.StockLocationCode.Trim().ToUpperInvariant();
                if (locationByCode.TryGetValue(norm, out var loc))
                {
                    var activeErr = _inventoryValidationService.ValidateEntityActive("Stock location", $"\"{loc.Name}\" ({norm})", loc.IsActive);
                    if (activeErr != null)
                    {
                        errors.Add(activeErr);
                        globalErrors.Add(new BulkImportErrorDto(rowNumber, "StockLocationCode", activeErr));
                    }
                    else
                    {
                        stockLocationId = loc.Id;
                    }
                }
                else if (allLocationByCode.TryGetValue(norm, out var crossLoc))
                {
                    var branchErr = _inventoryValidationService.ValidateStockLocationBranch(crossLoc.BranchId, branchId, crossLoc.Code, branch.Code);
                    var err = branchErr ?? $"Stock location \"{crossLoc.Code}\" does not belong to selected branch.";
                    errors.Add(err);
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, "StockLocationCode", err));
                }
                else
                {
                    var err = $"Stock location code \"{raw.StockLocationCode}\" was not found.";
                    errors.Add(err);
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, "StockLocationCode", err));
                }
            }

            // 6 & 7. Weights (Gross > 0, Stone >= 0, Stone <= Gross, Net = Gross - Stone >= 0)
            decimal grossWeight = 0;
            bool grossParsed = decimal.TryParse(raw.GrossWeightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out grossWeight);

            decimal stoneWeight = 0;
            bool stoneParsed = true;
            if (!string.IsNullOrWhiteSpace(raw.StoneWeightStr))
            {
                stoneParsed = decimal.TryParse(raw.StoneWeightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out stoneWeight);
            }

            decimal netWeight = 0;
            if (!grossParsed || !stoneParsed)
            {
                if (!grossParsed)
                {
                    var err = "Gross weight must be greater than zero.";
                    errors.Add(err);
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, "GrossWeight", err));
                }
                if (!stoneParsed)
                {
                    var err = "Stone weight must be greater than or equal to zero.";
                    errors.Add(err);
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, "StoneWeight", err));
                }
            }
            else
            {
                var weightCheck = _inventoryValidationService.ValidateWeights(grossWeight, stoneWeight);
                if (!weightCheck.IsValid)
                {
                    errors.Add(weightCheck.Error!);
                    var field = grossWeight <= 0 ? "GrossWeight" : "StoneWeight";
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, field, weightCheck.Error!));
                }
                else
                {
                    netWeight = weightCheck.NetWeight;
                }
            }

            // 8. Piece Count (>= 1)
            int pieceCount = 1;
            if (!string.IsNullOrWhiteSpace(raw.PieceCountStr))
            {
                if (!int.TryParse(raw.PieceCountStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out pieceCount))
                {
                    var err = "Piece count must be at least 1.";
                    errors.Add(err);
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, "PieceCount", err));
                }
                else
                {
                    var pcErr = _inventoryValidationService.ValidatePieceCount(pieceCount);
                    if (pcErr != null)
                    {
                        errors.Add(pcErr);
                        globalErrors.Add(new BulkImportErrorDto(rowNumber, "PieceCount", pcErr));
                    }
                }
            }

            // 9. Making Charge Method
            var mcParsed = _inventoryValidationService.ParseAndValidateMakingChargeMethod(raw.MakingChargeMethod);
            int methodVal = (int)mcParsed.Method;
            if (!mcParsed.Success)
            {
                errors.Add(mcParsed.Error!);
                globalErrors.Add(new BulkImportErrorDto(rowNumber, "MakingChargeMethod", mcParsed.Error!));
            }

            // 10. Making Charge Value (>= 0)
            decimal mcValue = 0;
            if (!string.IsNullOrWhiteSpace(raw.MakingChargeValueStr))
            {
                if (!decimal.TryParse(raw.MakingChargeValueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out mcValue))
                {
                    var err = "Making charge value must be greater than or equal to zero.";
                    errors.Add(err);
                    globalErrors.Add(new BulkImportErrorDto(rowNumber, "MakingChargeValue", err));
                }
                else
                {
                    var mcErr = _inventoryValidationService.ValidateMakingCharge(mcParsed.Method, mcValue);
                    if (mcErr != null)
                    {
                        errors.Add(mcErr);
                        globalErrors.Add(new BulkImportErrorDto(rowNumber, "MakingChargeValue", mcErr));
                    }
                }
            }

            bool isValid = errors.Count == 0;

            validatedRows.Add(new BulkImportRowDto(
                rowNumber,
                raw.ItemName?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(raw.ItemCode) ? null : raw.ItemCode.Trim(),
                raw.MaterialCode?.Trim().ToUpperInvariant() ?? string.Empty,
                raw.CategoryCode?.Trim().ToUpperInvariant() ?? string.Empty,
                raw.PurityCode?.Trim().ToUpperInvariant() ?? string.Empty,
                grossWeight,
                stoneWeight,
                netWeight,
                pieceCount,
                raw.StockLocationCode?.Trim().ToUpperInvariant() ?? string.Empty,
                ((MakingChargeMethod)methodVal).ToString().ToUpperInvariant(),
                mcValue,
                string.IsNullOrWhiteSpace(raw.Description) ? null : raw.Description.Trim(),
                isValid,
                errors,
                materialId,
                categoryId,
                purityId,
                stockLocationId,
                methodVal
            ));
        }

        int totalRows = validatedRows.Count;
        int validRows = validatedRows.Count(r => r.IsValid);
        int invalidRows = totalRows - validRows;
        bool isAllValid = invalidRows == 0 && totalRows > 0;

        // Create and persist import batch record
        var batch = new InventoryImportBatch
        {
            BranchId = branchId,
            OriginalFileName = Path.GetFileName(fileName),
            FileType = fileType,
            TotalRows = totalRows,
            ValidRows = validRows,
            InvalidRows = invalidRows,
            Status = isAllValid ? ImportBatchStatus.Validated : ImportBatchStatus.ValidationFailed,
            ImportedBy = _currentUserService.UserId ?? Guid.Empty,
            PayloadJson = JsonSerializer.Serialize(validatedRows, JsonOptions),
            ValidationErrorsJson = globalErrors.Count > 0 ? JsonSerializer.Serialize(globalErrors, JsonOptions) : null,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        _context.InventoryImportBatches.Add(batch);

        RecordAudit("InventoryImportBatch", batch.Id, isAllValid ? "BATCH_VALIDATED" : "BATCH_VALIDATION_FAILED",
            null,
            new { batch.BranchId, batch.OriginalFileName, batch.TotalRows, batch.ValidRows, batch.InvalidRows, Status = batch.Status.ToString() },
            branchId);

        await _context.SaveChangesAsync(cancellationToken);

        return new BulkImportValidationResultDto(
            batch.Id,
            branch.Id,
            branch.Code,
            branch.Name,
            batch.OriginalFileName,
            batch.FileType,
            totalRows,
            validRows,
            invalidRows,
            isAllValid,
            validatedRows,
            globalErrors
        );
    }

    public async Task<BulkImportValidationResultDto> GetBatchValidationAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _context.InventoryImportBatches
            .Include(b => b.Branch)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch == null)
        {
            throw new NotFoundException("InventoryImportBatch", batchId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(batch.BranchId, cancellationToken);

        var rows = !string.IsNullOrEmpty(batch.PayloadJson)
            ? JsonSerializer.Deserialize<List<BulkImportRowDto>>(batch.PayloadJson, JsonOptions) ?? new()
            : new List<BulkImportRowDto>();

        var errors = !string.IsNullOrEmpty(batch.ValidationErrorsJson)
            ? JsonSerializer.Deserialize<List<BulkImportErrorDto>>(batch.ValidationErrorsJson, JsonOptions) ?? new()
            : new List<BulkImportErrorDto>();

        return new BulkImportValidationResultDto(
            batch.Id,
            batch.BranchId,
            batch.Branch?.Code ?? string.Empty,
            batch.Branch?.Name ?? string.Empty,
            batch.OriginalFileName,
            batch.FileType,
            batch.TotalRows,
            batch.ValidRows,
            batch.InvalidRows,
            batch.InvalidRows == 0 && batch.TotalRows > 0,
            rows,
            errors
        );
    }

    public async Task<BulkImportResultDto> ConfirmImportAsync(Guid batchId, string? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        var batch = await _context.InventoryImportBatches
            .Include(b => b.Branch)
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);

        if (batch == null)
        {
            throw new NotFoundException("InventoryImportBatch", batchId);
        }

        await _branchAuthService.EnsureBranchAccessAsync(batch.BranchId, cancellationToken);

        // Check Idempotency
        string? normalizedKey = idempotencyKey?.Trim();
        string requestPayload = $"ConfirmBatch:{batchId}:{batch.BranchId}:{batch.TotalRows}";
        string requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestPayload)));

        if (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            var existingRecord = await _context.IdempotencyRecords
                .FirstOrDefaultAsync(r => r.UserId == _currentUserService.UserId && r.Key == normalizedKey, cancellationToken);

            if (existingRecord != null)
            {
                if (existingRecord.RequestHash != requestHash)
                {
                    throw new BusinessRuleException("Idempotency key has already been used with a different request payload.");
                }

                var cachedResult = JsonSerializer.Deserialize<BulkImportResultDto>(existingRecord.ResponseBody, JsonOptions);
                if (cachedResult != null)
                {
                    return cachedResult;
                }
            }
        }

        // If batch was already completed, return existing saved result
        if (batch.Status == ImportBatchStatus.Completed && !string.IsNullOrEmpty(batch.ResultSummaryJson))
        {
            var existingResult = JsonSerializer.Deserialize<BulkImportResultDto>(batch.ResultSummaryJson, JsonOptions);
            if (existingResult != null)
            {
                return existingResult;
            }
        }

        // Atomicity requirement: Must be in Validated state with 0 invalid rows
        if (batch.Status != ImportBatchStatus.Validated || batch.InvalidRows > 0)
        {
            throw new BusinessRuleException($"Cannot confirm import. The batch is in '{batch.Status}' state and contains {batch.InvalidRows} invalid row(s). All rows must be valid before confirming import.");
        }

        if (string.IsNullOrWhiteSpace(batch.PayloadJson))
        {
            throw new BusinessRuleException("Import payload data is missing or corrupted.");
        }

        var rows = JsonSerializer.Deserialize<List<BulkImportRowDto>>(batch.PayloadJson, JsonOptions)
                   ?? throw new BusinessRuleException("Failed to read validated import payload.");

        if (rows.Count == 0 || rows.Any(r => !r.IsValid))
        {
            throw new BusinessRuleException("Batch cannot be imported because one or more rows are invalid.");
        }

        var branch = batch.Branch ?? await _context.Branches.FirstOrDefaultAsync(b => b.Id == batch.BranchId, cancellationToken)
                     ?? throw new BusinessRuleException("Associated branch not found.");

        var currentUserId = _currentUserService.UserId;
        var now = DateTimeOffset.UtcNow;
        var createdItemsList = new List<ImportedItemSummaryDto>();

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            batch.Status = ImportBatchStatus.Importing;
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var row in rows)
            {
                // Generate permanent collision-proof Tag and Barcode via atomic sequence
                var (tagNumber, barcode) = await _tagGeneratorService.GenerateTagAndBarcodeAsync(branch.Code, row.CategoryCode, cancellationToken);

                var item = new InventoryItem
                {
                    BranchId = batch.BranchId,
                    StockLocationId = row.StockLocationId!.Value,
                    Name = row.ItemName,
                    ItemCode = row.ItemCode ?? string.Empty,
                    TagNumber = tagNumber,
                    Barcode = barcode,
                    MaterialId = row.MaterialId!.Value,
                    CategoryId = row.CategoryId!.Value,
                    PurityId = row.PurityId!.Value,
                    GrossWeight = row.GrossWeight,
                    StoneWeight = row.StoneWeight,
                    NetWeight = row.NetWeight,
                    PieceCount = row.PieceCount,
                    MakingChargeMethod = (MakingChargeMethod)row.MakingChargeMethodValue,
                    MakingChargeValue = row.MakingChargeValue,
                    Description = row.Description ?? string.Empty,
                    Status = InventoryStatus.Available,
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = currentUserId
                };

                _context.InventoryItems.Add(item);

                // Audit log per item referencing the ImportBatch
                _context.AuditLogs.Add(new AuditLog
                {
                    EntityName = "InventoryItem",
                    EntityId = item.Id.ToString(),
                    Action = "CREATE",
                    UserId = currentUserId,
                    Username = _currentUserService.Username,
                    BranchId = batch.BranchId,
                    Timestamp = now,
                    NewValues = JsonSerializer.Serialize(new
                    {
                        item.Name,
                        item.ItemCode,
                        item.TagNumber,
                        item.Barcode,
                        item.GrossWeight,
                        item.StoneWeight,
                        item.NetWeight,
                        item.PieceCount,
                        ImportBatchId = batch.Id
                    }),
                    IpAddress = _currentUserService.IpAddress,
                    UserAgent = _currentUserService.UserAgent
                });

                createdItemsList.Add(new ImportedItemSummaryDto(
                    item.Id,
                    item.Name,
                    item.TagNumber,
                    item.Barcode,
                    item.GrossWeight,
                    item.NetWeight,
                    row.StockLocationCode
                ));
            }

            batch.Status = ImportBatchStatus.Completed;
            batch.ConfirmedAt = now;
            batch.IdempotencyKey = normalizedKey;

            var resultDto = new BulkImportResultDto(
                batch.Id,
                batch.BranchId,
                batch.Status.ToString().ToUpperInvariant(),
                createdItemsList.Count,
                now,
                createdItemsList
            );

            batch.ResultSummaryJson = JsonSerializer.Serialize(resultDto, JsonOptions);

            // Audit log for confirmed batch
            _context.AuditLogs.Add(new AuditLog
            {
                EntityName = "InventoryImportBatch",
                EntityId = batch.Id.ToString(),
                Action = "BATCH_CONFIRMED",
                UserId = currentUserId,
                Username = _currentUserService.Username,
                BranchId = batch.BranchId,
                Timestamp = now,
                NewValues = JsonSerializer.Serialize(new
                {
                    batch.Id,
                    batch.BranchId,
                    batch.TotalRows,
                    batch.ValidRows,
                    CreatedItemsCount = createdItemsList.Count,
                    IdempotencyKey = normalizedKey
                }),
                IpAddress = _currentUserService.IpAddress,
                UserAgent = _currentUserService.UserAgent
            });

            // Persist idempotency record
            if (!string.IsNullOrWhiteSpace(normalizedKey))
            {
                _context.IdempotencyRecords.Add(new IdempotencyRecord
                {
                    Key = normalizedKey,
                    UserId = currentUserId,
                    Endpoint = "/api/inventory/import/confirm",
                    RequestHash = requestHash,
                    ResponseBody = JsonSerializer.Serialize(resultDto, JsonOptions),
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Successfully completed bulk import batch {BatchId} with {Count} items for branch {BranchCode}",
                batch.Id, createdItemsList.Count, branch.Code);

            return resultDto;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Bulk import batch {BatchId} failed during execution. Rolled back all changes.", batch.Id);

            batch.Status = ImportBatchStatus.Failed;
            await _context.SaveChangesAsync(CancellationToken.None);

            throw;
        }
    }

    public async Task<List<InventoryImportBatchDto>> GetBatchesAsync(Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryImportBatches
            .Include(b => b.Branch)
            .AsNoTracking()
            .AsQueryable();

        if (_currentUserService.IsGlobalAdmin)
        {
            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                query = query.Where(b => b.BranchId == branchId.Value);
            }
        }
        else
        {
            var targetBranchId = branchId ?? _currentUserService.BranchId;
            if (targetBranchId.HasValue && targetBranchId != Guid.Empty)
            {
                await _branchAuthService.EnsureBranchAccessAsync(targetBranchId.Value, cancellationToken);
                query = query.Where(b => b.BranchId == targetBranchId.Value);
            }
            else
            {
                var accessible = _currentUserService.AccessibleBranchIds;
                query = query.Where(b => accessible.Contains(b.BranchId));
            }
        }

        var batches = await query
            .OrderByDescending(b => b.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return batches.Select(b => new InventoryImportBatchDto(
            b.Id,
            b.BranchId,
            b.Branch?.Name ?? string.Empty,
            b.OriginalFileName,
            b.FileType,
            b.TotalRows,
            b.ValidRows,
            b.InvalidRows,
            (int)b.Status,
            b.Status.ToString().ToUpperInvariant(),
            b.ImportedBy,
            b.ConfirmedAt,
            b.CreatedAt
        )).ToList();
    }

    private static List<RawImportRow> ParseExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault(w => w.Name == "Inventory Items") ?? workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
        {
            throw new BusinessRuleException("The Excel workbook contains no readable worksheets.");
        }

        var headerRow = worksheet.Row(1);
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= worksheet.ColumnsUsed().Count(); col++)
        {
            var headerVal = worksheet.Cell(1, col).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(headerVal))
            {
                headerMap[headerVal] = col;
            }
        }

        ValidateHeaders(headerMap);

        var list = new List<RawImportRow>();
        int lastRow = worksheet.RowsUsed().Count();

        for (int r = 2; r <= lastRow; r++)
        {
            var row = worksheet.Row(r);
            if (row.IsEmpty()) continue;

            string itemName = GetCellString(worksheet, r, headerMap, "Item Name");
            if (string.IsNullOrWhiteSpace(itemName)) continue; // ignore blank rows

            list.Add(new RawImportRow(
                r,
                itemName,
                GetCellString(worksheet, r, headerMap, "Item Code"),
                GetCellString(worksheet, r, headerMap, "Material Code"),
                GetCellString(worksheet, r, headerMap, "Category Code"),
                GetCellString(worksheet, r, headerMap, "Purity Code"),
                GetCellString(worksheet, r, headerMap, "Gross Weight"),
                GetCellString(worksheet, r, headerMap, "Stone Weight"),
                GetCellString(worksheet, r, headerMap, "Piece Count"),
                GetCellString(worksheet, r, headerMap, "Stock Location Code"),
                GetCellString(worksheet, r, headerMap, "Making Charge Method"),
                GetCellString(worksheet, r, headerMap, "Making Charge Value"),
                GetCellString(worksheet, r, headerMap, "Description")
            ));
        }

        return list;
    }

    private static List<RawImportRow> ParseCsv(Stream stream)
    {
        var records = ParseCsvRecords(stream);
        if (records.Count == 0)
        {
            throw new BusinessRuleException("The CSV file is empty or missing headers.");
        }

        var headers = records[0];
        if (headers.Count == 0)
        {
            throw new BusinessRuleException("The CSV file is missing header columns.");
        }

        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(headers[i]))
            {
                headerMap[headers[i].Trim()] = i;
            }
        }

        ValidateHeaders(headerMap);

        var list = new List<RawImportRow>();
        for (int r = 1; r < records.Count; r++)
        {
            var row = records[r];
            string itemName = GetCsvRowField(row, headerMap, "Item Name");
            if (string.IsNullOrWhiteSpace(itemName)) continue;

            list.Add(new RawImportRow(
                r + 1,
                itemName,
                GetCsvRowField(row, headerMap, "Item Code"),
                GetCsvRowField(row, headerMap, "Material Code"),
                GetCsvRowField(row, headerMap, "Category Code"),
                GetCsvRowField(row, headerMap, "Purity Code"),
                GetCsvRowField(row, headerMap, "Gross Weight"),
                GetCsvRowField(row, headerMap, "Stone Weight"),
                GetCsvRowField(row, headerMap, "Piece Count"),
                GetCsvRowField(row, headerMap, "Stock Location Code"),
                GetCsvRowField(row, headerMap, "Making Charge Method"),
                GetCsvRowField(row, headerMap, "Making Charge Value"),
                GetCsvRowField(row, headerMap, "Description")
            ));
        }

        return list;
    }

    private static List<List<string>> ParseCsvRecords(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        var records = new List<List<string>>();
        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (!inQuotes && string.IsNullOrWhiteSpace(line)) continue;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        currentRecord.Add(currentField.ToString().Trim());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            if (!inQuotes)
            {
                currentRecord.Add(currentField.ToString().Trim());
                currentField.Clear();
                if (currentRecord.Any(f => !string.IsNullOrWhiteSpace(f)))
                {
                    records.Add(new List<string>(currentRecord));
                }
                currentRecord.Clear();
            }
            else
            {
                currentField.AppendLine();
            }
        }

        if (currentRecord.Count > 0 || currentField.Length > 0)
        {
            currentRecord.Add(currentField.ToString().Trim());
            if (currentRecord.Any(f => !string.IsNullOrWhiteSpace(f)))
            {
                records.Add(currentRecord);
            }
        }

        return records;
    }

    private static string GetCsvRowField(List<string> row, Dictionary<string, int> map, string header)
    {
        if (!map.TryGetValue(header, out int col) || col >= row.Count) return string.Empty;
        return row[col]?.Trim() ?? string.Empty;
    }

    private static void ValidateHeaders(Dictionary<string, int> map)
    {
        var requiredHeaders = new[]
        {
            "Item Name", "Material Code", "Category Code", "Purity Code",
            "Gross Weight", "Stock Location Code", "Making Charge Method"
        };

        var missing = requiredHeaders.Where(h => !map.ContainsKey(h)).ToList();
        if (missing.Count > 0)
        {
            throw new BusinessRuleException($"Missing required column(s): {string.Join(", ", missing)}.");
        }
    }

    private static string GetCellString(IXLWorksheet ws, int row, Dictionary<string, int> map, string header)
    {
        if (!map.TryGetValue(header, out int col)) return string.Empty;
        var cell = ws.Cell(row, col);
        if (cell.IsEmpty()) return string.Empty;

        return cell.GetString().Trim();
    }

    private void RecordAudit(string entityName, Guid entityId, string action, object? oldValues, object? newValues, Guid? branchId = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId.ToString(),
            Action = action,
            UserId = _currentUserService.UserId,
            Username = _currentUserService.Username,
            BranchId = branchId ?? _currentUserService.BranchId,
            Timestamp = DateTimeOffset.UtcNow,
            OldValues = oldValues != null ? JsonSerializer.Serialize(oldValues, JsonOptions) : null,
            NewValues = newValues != null ? JsonSerializer.Serialize(newValues, JsonOptions) : null,
            IpAddress = _currentUserService.IpAddress,
            UserAgent = _currentUserService.UserAgent
        });
    }

    private record RawImportRow(
        int RowNumber,
        string ItemName,
        string ItemCode,
        string MaterialCode,
        string CategoryCode,
        string PurityCode,
        string GrossWeightStr,
        string StoneWeightStr,
        string PieceCountStr,
        string StockLocationCode,
        string MakingChargeMethod,
        string MakingChargeValueStr,
        string Description
    );
}
