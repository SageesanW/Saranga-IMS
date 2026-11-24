using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using Saranga.Api.Middleware;
using Saranga.Application.Common.Interfaces;
using Saranga.Infrastructure;
using Saranga.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddOpenApi();

// Register Infrastructure & Persistence (includes auth handlers, policy provider, and application services)
builder.Services.AddInfrastructure(builder.Configuration);

// Configure JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SarangaJewellerySuperSecretKeyForJwtTokenGeneration2026!#$";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SarangaJewellery";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SarangaClients";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure CORS for Next.js frontend (deployment-aware)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:3001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

// Global exception handling (RFC 7807 / unified JSON)
app.UseMiddleware<GlobalExceptionMiddleware>();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Saranga Jewellery Management System API";
        options.Theme = ScalarTheme.Solarized;
        options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseSerilogRequestLogging();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseMiddleware<BranchContextMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    System = "Saranga Jewellery Management System",
    Version = "1.0.0",
    Timestamp = DateTimeOffset.UtcNow
})).AllowAnonymous();

// Run idempotent data seeding on startup (Permissions, System Roles, System Mappings)
try
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    await seeder.SeedAsync();
}
catch (Exception ex)
{
    Log.Error(ex, "An error occurred during startup data seeding.");
}

try
{
    Log.Information("Starting Saranga Jewellery API backend...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Saranga API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
