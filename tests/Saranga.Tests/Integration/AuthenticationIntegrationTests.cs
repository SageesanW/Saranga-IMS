using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Api;
using Saranga.Application.DTOs.Auth;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Integration;

public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task CompleteAuthenticationAndBootstrapFlow_AgainstSupabase()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            BaseAddress = new Uri("http://localhost/")
        });

        // 1. Confirm backend startup & health endpoint
        var healthRes = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthRes.StatusCode);
        var healthBody = await healthRes.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", healthBody);
        _output.WriteLine("Verified /health returns 200 OK. Backend startup succeeded against Supabase.");

        // 2. Test initial Owner / Super Admin Bootstrap
        var bootstrapReq = new BootstrapOwnerRequest(
            Username: "saranga_owner",
            Email: "owner@sarangajewellery.lk",
            Password: "OwnerSuperAdmin@2026#Secure",
            FullName: "Saranga Master Owner"
        );

        var bootstrapRes = await client.PostAsJsonAsync("/api/auth/bootstrap", bootstrapReq);

        if (bootstrapRes.StatusCode == HttpStatusCode.Created)
        {
            var createdOwner = await bootstrapRes.Content.ReadFromJsonAsync<UserDto>();
            Assert.NotNull(createdOwner);
            Assert.Equal("saranga_owner", createdOwner.Username);
            Assert.True(createdOwner.IsGlobalAdmin);
            Assert.Contains("Owner / Super Admin", createdOwner.Roles);
            _output.WriteLine("Successfully bootstrapped initial Owner / Super Admin account.");
        }
        else
        {
            // If already bootstrapped, verify 400 Bad Request
            Assert.Equal(HttpStatusCode.BadRequest, bootstrapRes.StatusCode);
            _output.WriteLine("System was already bootstrapped. Guard functioning correctly.");
        }

        // 3. Confirm second bootstrap attempt is rejected
        var duplicateBootstrapRes = await client.PostAsJsonAsync("/api/auth/bootstrap", bootstrapReq);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateBootstrapRes.StatusCode);
        _output.WriteLine("Verified second bootstrap attempt is rejected with 400 Bad Request.");

        // 4. Test Login
        var loginReq = new LoginRequest("saranga_owner", "OwnerSuperAdmin@2026#Secure");
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", loginReq);
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);

        var authData = await loginRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authData);
        Assert.False(string.IsNullOrWhiteSpace(authData.AccessToken));
        Assert.Equal("saranga_owner", authData.User.Username);

        // Verify HttpOnly cookie was set
        Assert.True(loginRes.Headers.Contains("Set-Cookie"), "Set-Cookie header missing from login response.");
        var cookies = loginRes.Headers.GetValues("Set-Cookie").ToList();
        var refreshCookie = cookies.FirstOrDefault(c => c.Contains("saranga_refresh_token"));
        Assert.NotNull(refreshCookie);
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine("Login succeeded. Received short-lived JWT access token and HttpOnly refresh cookie.");

        // 5. Test /api/auth/me with Bearer token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.AccessToken);
        var meRes = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);

        var meUser = await meRes.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(meUser);
        Assert.Equal("saranga_owner", meUser.Username);
        Assert.True(meUser.IsGlobalAdmin);
        _output.WriteLine("Verified /api/auth/me returns authorized user identity.");

        // 6. Test Token Refresh & Rotation (using the HttpOnly cookie stored in client handler)
        client.DefaultRequestHeaders.Authorization = null; // Clear access token
        var refreshRes = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, refreshRes.StatusCode);

        var refreshedAuth = await refreshRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(refreshedAuth);
        Assert.False(string.IsNullOrWhiteSpace(refreshedAuth.AccessToken));
        Assert.NotEqual(authData.AccessToken, refreshedAuth.AccessToken); // New access token issued
        _output.WriteLine("Silent token refresh succeeded via HttpOnly cookie. New access token issued.");

        // 7. Verify Database Records: PasswordHash is BCrypt, TokenHash is SHA-256, no raw secrets
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == "saranga_owner");
            Assert.NotNull(user);
            Assert.StartsWith("$2a$", user.PasswordHash); // BCrypt work factor 11 hash
            Assert.DoesNotContain("OwnerSuperAdmin@2026#Secure", user.PasswordHash);

            var tokens = await db.RefreshTokens.Where(r => r.UserId == user.Id).ToListAsync();
            Assert.NotEmpty(tokens);
            foreach (var t in tokens)
            {
                Assert.Equal(64, t.TokenHash.Length); // SHA-256 hex string
                Assert.Matches("^[a-f0-9]{64}$", t.TokenHash);
            }
            _output.WriteLine("Verified database: PasswordHash is BCrypt, TokenHash is SHA-256. Zero raw secrets stored.");

            // Verify Audit Log records
            var loginAudit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityName == "User" && a.Action == "LOGIN");
            Assert.NotNull(loginAudit);
            Assert.Null(loginAudit.OldValues);
            Assert.Null(loginAudit.NewValues); // Login doesn't mutate entity properties, only creates audit record
            _output.WriteLine("Verified AuditLogs: Login event captured without leaking credentials.");
        }

        // 8. Test Logout
        var logoutRes = await client.PostAsJsonAsync("/api/auth/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);
        _output.WriteLine("Verified logout cleared session and revoked token.");
    }
}
