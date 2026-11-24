using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Saranga.Domain.Entities;
using Saranga.Infrastructure.Services;
using Xunit;

namespace Saranga.Tests.Services;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _tokenService;

    public JwtTokenServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:Secret", "TestSecretKeyThatIsAtLeast32BytesLongForSecurity2026!"},
            {"Jwt:Issuer", "SarangaTestIssuer"},
            {"Jwt:Audience", "SarangaTestAudience"},
            {"Jwt:ExpiryMinutes", "15"},
            {"Jwt:RefreshTokenExpiryDays", "7"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _tokenService = new JwtTokenService(configuration);
    }

    [Fact]
    public void GenerateAccessToken_ShouldIncludeRolesPermissionsAndBranch()
    {
        var branchId = Guid.NewGuid();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "saranga_cashier",
            Email = "cashier@sarangajewellery.lk",
            FullName = "Saranga Cashier 1",
            DefaultBranchId = branchId,
            IsGlobalAdmin = false
        };

        var roles = new[] { "Cashier" };
        var permissions = new[] { "POS_VIEW", "POS_CREATE_SALE" };
        var branches = new[] { branchId };

        var tokenString = _tokenService.GenerateAccessToken(user, roles, permissions, branches);
        Assert.NotNull(tokenString);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        Assert.Equal("SarangaTestIssuer", jwt.Issuer);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == user.Username);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Cashier");
        Assert.Contains(jwt.Claims, c => c.Type == "branch_id" && c.Value == branchId.ToString());
        Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "POS_VIEW");
        Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "POS_CREATE_SALE");
    }

    [Fact]
    public void GenerateRefreshToken_ShouldHashTokenAndNeverStoreRawTokenInEntity()
    {
        var userId = Guid.NewGuid();
        var (rawToken, entity) = _tokenService.GenerateRefreshToken(userId);

        Assert.NotNull(rawToken);
        Assert.NotNull(entity.TokenHash);
        Assert.NotEqual(rawToken, entity.TokenHash); // Raw token is NOT stored in TokenHash
        Assert.Equal(64, entity.TokenHash.Length);   // SHA-256 hex string length
        Assert.Equal(userId, entity.UserId);
        Assert.True(entity.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.True(entity.IsActive);
        Assert.False(entity.IsExpired);
        Assert.False(entity.IsRevoked);
    }

    [Fact]
    public void HashToken_ShouldBeDeterministic()
    {
        var sampleToken = "TestRawTokenValue1234567890";
        var hash1 = _tokenService.HashToken(sampleToken);
        var hash2 = _tokenService.HashToken(sampleToken);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }
}
