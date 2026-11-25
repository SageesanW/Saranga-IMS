using Saranga.Application.DTOs.Common;
using Saranga.Application.DTOs.Pos;

namespace Saranga.Application.Common.Interfaces;

public interface IPosService
{
    Task<PosItemLookupResultDto> LookupItemForSaleAsync(Guid shiftId, string barcodeOrTag, CancellationToken cancellationToken = default);
    Task<PosInvoiceDto> CreateDraftInvoiceAsync(CreatePosDraftInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<PosInvoiceDto> QuickCreateCustomerAndDraftAsync(QuickCreateCustomerAndDraftRequest request, CancellationToken cancellationToken = default);
    Task<PosInvoiceDto> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<PagedResult<PosInvoiceDto>> GetInvoicesAsync(PosInvoiceFilter filter, CancellationToken cancellationToken = default);
    Task<PosInvoiceDto> AddLineToDraftAsync(Guid invoiceId, AddPosInvoiceLineRequest request, CancellationToken cancellationToken = default);
    Task<PosInvoiceDto> UpdateLineInDraftAsync(Guid invoiceId, Guid lineId, UpdatePosInvoiceLineRequest request, CancellationToken cancellationToken = default);
    Task<PosInvoiceDto> RemoveLineFromDraftAsync(Guid invoiceId, Guid lineId, CancellationToken cancellationToken = default);
    Task<PosInvoiceDto> FinalizeInvoiceAsync(Guid invoiceId, FinalizePosInvoiceRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<PosInvoiceDto> CancelDraftInvoiceAsync(Guid invoiceId, string reason, CancellationToken cancellationToken = default);
}
