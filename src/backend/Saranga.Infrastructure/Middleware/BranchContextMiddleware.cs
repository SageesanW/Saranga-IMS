using Microsoft.AspNetCore.Http;

namespace Saranga.Infrastructure.Middleware;

public class BranchContextMiddleware
{
    private readonly RequestDelegate _next;
    public const string HeaderName = "X-Branch-Id";
    public const string ContextItemKey = "ActiveBranchId";

    public BranchContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            var headerValue = headerValues.ToString();
            if (Guid.TryParse(headerValue, out var branchId) && branchId != Guid.Empty)
            {
                context.Items[ContextItemKey] = branchId;
            }
        }

        await _next(context);
    }
}
