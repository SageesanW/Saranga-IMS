using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Saranga.Api;
using Saranga.Application.Common.Interfaces;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Infrastructure;

public class DatabaseSchemaVerificationTests
{
    private readonly ITestOutputHelper _output;

    public DatabaseSchemaVerificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task VerifySchemaTablesAndConstraints_InSupabase()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
                               ?? config["ConnectionStrings__DefaultConnection"];

        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var context = new AppDbContext(options, new DummyUserService());
        var conn = context.Database.GetDbConnection();
        await conn.OpenAsync();

        // 1. Verify all 8 domain tables exist
        var expectedTables = new[]
        {
            "AuditLogs", "Branches", "Permissions", "Roles", "Users",
            "RolePermissions", "RefreshTokens", "UserBranches", "UserRoles"
        };

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public';";
            var tables = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            foreach (var expected in expectedTables)
            {
                Assert.Contains(expected, tables);
                _output.WriteLine($"Verified table: {expected}");
            }
        }

        // 2. Verify AuditLogs jsonb columns
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT column_name, data_type 
                FROM information_schema.columns 
                WHERE table_name = 'AuditLogs' AND column_name IN ('OldValues', 'NewValues');";

            await using var reader = await cmd.ExecuteReaderAsync();
            var jsonbCols = new Dictionary<string, string>();
            while (await reader.ReadAsync())
            {
                jsonbCols[reader.GetString(0)] = reader.GetString(1);
            }

            Assert.Equal("jsonb", jsonbCols["OldValues"]);
            Assert.Equal("jsonb", jsonbCols["NewValues"]);
            _output.WriteLine("Verified AuditLogs OldValues and NewValues are PostgreSQL jsonb.");
        }

        // 3. Verify Foreign Keys and Delete Rules
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT
                    tc.table_name, 
                    kcu.column_name, 
                    ccu.table_name AS foreign_table_name,
                    rc.delete_rule
                FROM information_schema.table_constraints AS tc 
                JOIN information_schema.key_column_usage AS kcu
                    ON tc.constraint_name = kcu.constraint_name
                JOIN information_schema.referential_constraints AS rc
                    ON tc.constraint_name = rc.constraint_name
                JOIN information_schema.constraint_column_usage AS ccu
                    ON ccu.constraint_name = tc.constraint_name
                WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = 'public';";

            await using var reader = await cmd.ExecuteReaderAsync();
            var fks = new List<(string Table, string Col, string ForeignTable, string DeleteRule)>();
            while (await reader.ReadAsync())
            {
                fks.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            }

            // Verify Users.DefaultBranchId SET NULL
            Assert.Contains(fks, f => f.Table == "Users" && f.Col == "DefaultBranchId" && f.ForeignTable == "Branches" && f.DeleteRule == "SET NULL");
            // Verify RefreshTokens.UserId CASCADE
            Assert.Contains(fks, f => f.Table == "RefreshTokens" && f.Col == "UserId" && f.ForeignTable == "Users" && f.DeleteRule == "CASCADE");
            // Verify UserBranches.BranchId CASCADE
            Assert.Contains(fks, f => f.Table == "UserBranches" && f.Col == "BranchId" && f.ForeignTable == "Branches" && f.DeleteRule == "CASCADE");

            _output.WriteLine($"Verified {fks.Count} Foreign Keys with correct CASCADE and SET NULL rules.");
        }
    }

    private class DummyUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Username => null;
        public Guid? BranchId => null;
        public string? Role => null;
        public bool IsGlobalAdmin => false;
        public IReadOnlyList<Guid> AccessibleBranchIds => Array.Empty<Guid>();
        public IReadOnlyList<string> Roles => Array.Empty<string>();
        public IReadOnlyList<string> Permissions => Array.Empty<string>();
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}
