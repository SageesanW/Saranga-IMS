using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Saranga.Application.Common.Interfaces;
using Saranga.Application.Services;
using Saranga.Infrastructure.Authorization;
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
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ICompositionService, CompositionService>();
        services.AddScoped<IBulkImportService, BulkImportService>();

        // Authorization Handler & Policy Provider
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
