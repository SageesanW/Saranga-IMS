using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Saranga.Application.Common.Interfaces;

namespace Saranga.Infrastructure.Services;

public class MetalRateBackgroundRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MetalRateBackgroundRefreshService> _logger;

    public MetalRateBackgroundRefreshService(
        IServiceScopeFactory scopeFactory,
        ILogger<MetalRateBackgroundRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MetalRateBackgroundRefreshService initialized. Periodic 15-minute rate checks will run in the background.");

        // Grace period on initial startup / container wake-up (e.g. Render spin-up)
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var rateService = scope.ServiceProvider.GetRequiredService<IMetalRateService>();

                await rateService.RefreshStaleBranchRatesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred during periodic metal rate refresh.");
            }

            try
            {
                // Check every 1 minute for branches whose rates have exceeded the 15-minute threshold
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("MetalRateBackgroundRefreshService stopped.");
    }
}
