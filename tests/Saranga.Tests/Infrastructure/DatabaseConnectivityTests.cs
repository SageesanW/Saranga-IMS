using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Saranga.Api;
using Saranga.Application.Common.Interfaces;
using Saranga.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Saranga.Tests.Infrastructure;

public class DatabaseConnectivityTests
{
    private readonly ITestOutputHelper _output;

    public DatabaseConnectivityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task VerifyDatabaseConnectivity_WithoutModifyingSchema()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
                               ?? config["ConnectionStrings__DefaultConnection"];

        Assert.False(string.IsNullOrWhiteSpace(connectionString), "Connection string was not found in User Secrets or environment variables.");
        Assert.DoesNotContain("PLACEHOLDER", connectionString, StringComparison.OrdinalIgnoreCase);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // Use mock current user service for read check
        var mockUserService = new MockCurrentUserService();
        await using var context = new AppDbContext(options, mockUserService);

        var canConnect = await context.Database.CanConnectAsync();
        Assert.True(canConnect, "Unable to establish connection to PostgreSQL database.");

        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version();";
        var version = await command.ExecuteScalarAsync();

        Assert.NotNull(version);
        _output.WriteLine($"Connected successfully. Database Version: {version}");
    }

    private class MockCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Username => "test_runner";
        public Guid? BranchId => null;
        public string? Role => null;
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}
