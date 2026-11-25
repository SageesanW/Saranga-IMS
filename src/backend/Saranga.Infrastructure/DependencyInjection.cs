using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.Services;
using Saranga.Infrastructure.Authorization;
using Saranga.Infrastructure.External;
using Saranga.Infrastructure.Persistence;
using Saranga.Infrastructure.Services;

namespace Saranga.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? configuration["ConnectionStrings__DefaultConnection"];

        services.AddDbContext<AppDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                options.UseNpgsql(connectionString, b =>
                {
                    b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                });
            }
        });

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IBranchAuthorizationService, BranchAuthorizationService>();
        services.AddScoped<IDataSeeder, DataSeeder>();

        // Application Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBranchService, BranchService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAttributesService, AttributesService>();
        services.AddScoped<ITagGeneratorService, TagGeneratorService>();
        services.AddScoped<IInventoryValidationService, InventoryValidationService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ICompositionService, CompositionService>();
        services.AddScoped<IBulkImportService, BulkImportService>();
        services.AddScoped<IInventoryCountService, InventoryCountService>();

        // Module 9: Rates, VAT & Reference Pricing
        services.AddHttpClient<IMetalRateProvider, GoldApiRateProvider>();
        services.AddScoped<IMetalRateService, MetalRateService>();
        services.AddScoped<IVatConfigurationService, VatConfigurationService>();
        services.AddScoped<IReferencePricingService, ReferencePricingService>();

        // Module 10: Customer Management
        services.AddSingleton<IPhoneNormalizationService, PhoneNormalizationService>();
        services.AddScoped<ICustomerService, CustomerService>();

        // Module 11: Shift Opening & Closing
        services.AddScoped<IShiftExpectedBalanceService, ShiftExpectedBalanceService>();
        services.AddScoped<IShiftService, ShiftService>();

        // Module 12: Cash Ledger & Cash Drawer Movements
        services.AddScoped<ICashLedgerService, CashLedgerService>();

        // Module 13: POS Normal Jewellery Sales
        services.AddScoped<IPosService, PosService>();

        // Authorization Handler & Policy Provider
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
