using Microsoft.EntityFrameworkCore;
using Saranga.Application.Common.Interfaces;
using Saranga.Domain.Entities;
using Saranga.Infrastructure.Persistence;
using Xunit;

namespace Saranga.Tests.Infrastructure;

public class AuditSafetyTests
{
    private class MockCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Username { get; set; } = "auditor_test";
        public Guid? BranchId { get; set; } = Guid.NewGuid();
        public string? Role { get; set; } = "Auditor";
        public string? IpAddress { get; set; } = "127.0.0.1";
        public string? UserAgent { get; set; } = "xUnit Test Runner";
    }

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new MockCurrentUserService());
    }

    [Fact]
    public async Task AuditLog_ShouldBeImmutable_AndThrowOnUpdateOrDelete()
    {
        using var context = CreateDbContext();

        var log = new AuditLog
        {
            EntityName = "User",
            EntityId = Guid.NewGuid().ToString(),
            Action = "LOGIN",
            Username = "cashier1"
        };

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        // Attempt to modify the audit log record
        log.Action = "TAMPERED_ACTION";
        var updateEx = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("immutable", updateEx.Message, StringComparison.OrdinalIgnoreCase);

        // Reset state and attempt delete
        context.Entry(log).State = EntityState.Deleted;
        var deleteEx = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("immutable", deleteEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveChangesAsync_ShouldScrubSensitiveFieldsFromAudit()
    {
        using var context = CreateDbContext();

        var user = new User
        {
            Username = "safe_user",
            Email = "safe@saranga.lk",
            PasswordHash = "$2a$11$SECRET_PASSWORD_HASH_DO_NOT_LEAK",
            FullName = "Safe User"
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var auditRecord = await context.AuditLogs
            .FirstOrDefaultAsync(a => a.EntityName == nameof(User) && a.EntityId == user.Id.ToString());

        Assert.NotNull(auditRecord);
        Assert.Equal("CREATE", auditRecord.Action);

        // Ensure PasswordHash was scrubbed from NewValues JSON
        Assert.NotNull(auditRecord.NewValues);
        Assert.DoesNotContain("SECRET_PASSWORD_HASH", auditRecord.NewValues);
        Assert.DoesNotContain("PasswordHash", auditRecord.NewValues);
        Assert.Contains("safe_user", auditRecord.NewValues);
    }

    [Fact]
    public async Task AuditLog_ShouldNotAuditItself()
    {
        using var context = CreateDbContext();

        var initialCount = await context.AuditLogs.CountAsync();

        var log = new AuditLog
        {
            EntityName = "CustomEvent",
            EntityId = "123",
            Action = "EVENT"
        };

        context.AuditLogs.Add(log);
        await context.SaveChangesAsync();

        var finalCount = await context.AuditLogs.CountAsync();
        // Only the single manually added AuditLog exists, no recursive self-audit log
        Assert.Equal(initialCount + 1, finalCount);
    }
}
