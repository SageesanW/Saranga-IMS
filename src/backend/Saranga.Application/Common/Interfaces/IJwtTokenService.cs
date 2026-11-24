using Saranga.Domain.Entities;

namespace Saranga.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, IEnumerable<Guid>? branchIds = null);
    (string RawToken, RefreshToken Entity) GenerateRefreshToken(Guid userId);
    string HashToken(string rawToken);
}
