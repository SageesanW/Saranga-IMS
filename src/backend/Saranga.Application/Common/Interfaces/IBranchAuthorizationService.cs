namespace Saranga.Application.Common.Interfaces;

public interface IBranchAuthorizationService
{
    Task<bool> CanAccessBranchAsync(Guid? branchId, CancellationToken cancellationToken = default);
    Task EnsureBranchAccessAsync(Guid? branchId, CancellationToken cancellationToken = default);
}
