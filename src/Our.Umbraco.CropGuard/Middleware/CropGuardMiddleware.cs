using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Our.Umbraco.CropGuard.Configuration;
using Our.Umbraco.CropGuard.Services;

namespace Our.Umbraco.CropGuard.Middleware;

public sealed class CropGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAllowedCropService _cropService;
    private readonly IOptionsMonitor<CropGuardOptions> _options;
    private readonly ILogger<CropGuardMiddleware> _logger;

    public CropGuardMiddleware(
        RequestDelegate next,
        IAllowedCropService cropService,
        IOptionsMonitor<CropGuardOptions> options,
        ILogger<CropGuardMiddleware> logger)
    {
        _next = next;
        _cropService = cropService;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var opts = _options.CurrentValue;

        if (opts.Enabled && ShouldInspect(context, opts))
        {
            if (!await IsAllowedAsync(context.Request.Query, opts))
            {
                if (opts.LogBlockedRequests)
                {
                    _logger.LogWarning(
                        "CropGuard: blocked request {Path}{QueryString}",
                        context.Request.Path,
                        context.Request.QueryString);
                }

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("Image processing parameters are not allowed.");
                return;
            }
        }

        await _next(context);
    }

    // Only inspect requests that target the media path AND carry a query string.
    // All other requests (HTML, CSS, JS, unparameterised images) pass straight through.
    private static bool ShouldInspect(HttpContext context, CropGuardOptions opts)
        => context.Request.Path.StartsWithSegments(opts.MediaPathPrefix, StringComparison.OrdinalIgnoreCase)
           && context.Request.QueryString.HasValue;

    private async Task<bool> IsAllowedAsync(IQueryCollection query, CropGuardOptions opts)
    {
        var hasWidth = TryParsePositiveInt(query["width"], out var width);
        var hasHeight = TryParsePositiveInt(query["height"], out var height);

        // No resize parameters — caller wants the original image.
        if (!hasWidth && !hasHeight)
            return opts.AllowOriginalImage;

        // Partial dimensions (width-only or height-only) are also checked against the crop list.
        // Treat missing dimension as 0 so it still has a unique key entry if needed.
        return await _cropService.IsAllowedAsync(
            hasWidth ? width : 0,
            hasHeight ? height : 0);
    }

    private static bool TryParsePositiveInt(string? value, out int result)
    {
        result = 0;
        return !string.IsNullOrEmpty(value)
               && int.TryParse(value, out result)
               && result > 0;
    }
}
