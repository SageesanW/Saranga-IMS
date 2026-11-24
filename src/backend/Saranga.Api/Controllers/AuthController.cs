using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saranga.Application.DTOs.Auth;
using Saranga.Application.Services;

namespace Saranga.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "saranga_refresh_token";
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger,
        IWebHostEnvironment env,
        IConfiguration configuration)
    {
        _authService = authService;
        _logger = logger;
        _env = env;
        _configuration = configuration;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var (response, rawRefreshToken) = await _authService.LoginAsync(request, cancellationToken);
            SetRefreshTokenCookie(rawRefreshToken, response.ExpiresAt);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Failed login attempt for username: {Username}. Reason: {Reason}", request.Username, ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenBody? body, CancellationToken cancellationToken)
    {
        // Obtain token from secure HttpOnly cookie or fallback body
        var rawRefreshToken = Request.Cookies[RefreshTokenCookieName] ?? body?.RefreshToken;

        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Unauthorized(new { message = "No refresh token provided." });
        }

        try
        {
            var (response, newRawRefreshToken) = await _authService.RefreshTokenAsync(rawRefreshToken, cancellationToken);
            SetRefreshTokenCookie(newRawRefreshToken, response.ExpiresAt);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            ClearRefreshTokenCookie();
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenBody? body, CancellationToken cancellationToken)
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookieName] ?? body?.RefreshToken;

        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            await _authService.RevokeTokenAsync(rawRefreshToken, cancellationToken);
        }

        ClearRefreshTokenCookie();
        return NoContent();
    }

    /// <summary>
    /// Safe initialization endpoint for bootstrapping the initial Owner / Super Admin.
    /// Only accessible when zero users exist in the database.
    /// </summary>
    [HttpPost("bootstrap")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BootstrapInitialOwner([FromBody] BootstrapOwnerRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _authService.BootstrapInitialOwnerAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, user);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var user = await _authService.GetCurrentUserAsync(cancellationToken);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(user);
    }

    private void SetRefreshTokenCookie(string token, DateTimeOffset expiresAt)
    {
        var cookieOptions = BuildDeploymentAwareCookieOptions(expiresAt);
        Response.Cookies.Append(RefreshTokenCookieName, token, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        var cookieOptions = BuildDeploymentAwareCookieOptions(DateTimeOffset.UtcNow.AddDays(-1));
        Response.Cookies.Delete(RefreshTokenCookieName, cookieOptions);
    }

    private CookieOptions BuildDeploymentAwareCookieOptions(DateTimeOffset expiresAt)
    {
        var sameSiteConfig = _configuration["Auth:Cookie:SameSite"];
        var sameSite = sameSiteConfig?.ToLowerInvariant() switch
        {
            "strict" => SameSiteMode.Strict,
            "none" => SameSiteMode.None,
            _ => SameSiteMode.Lax // Default for app.<domain> and api.<domain> subdomains or local
        };

        var secureConfig = _configuration["Auth:Cookie:Secure"];
        bool isSecure = bool.TryParse(secureConfig, out var secureVal)
            ? secureVal
            : (sameSite == SameSiteMode.None || Request.IsHttps || !_env.IsDevelopment());

        var domain = _configuration["Auth:Cookie:Domain"];

        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = sameSite,
            Expires = expiresAt,
            Path = "/api/auth"
        };

        if (!string.IsNullOrWhiteSpace(domain))
        {
            options.Domain = domain;
        }

        return options;
    }
}

public record RefreshTokenBody(string? RefreshToken);
