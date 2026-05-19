using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Our.Umbraco.CropGuard.Configuration;
using Our.Umbraco.CropGuard.Middleware;
using Our.Umbraco.CropGuard.Services;

namespace Our.Umbraco.CropGuard.Tests.Middleware;

public sealed class CropGuardMiddlewareTests
{
    private readonly IAllowedCropService _cropService = Substitute.For<IAllowedCropService>();
    private readonly IOptionsMonitor<CropGuardOptions> _options = Substitute.For<IOptionsMonitor<CropGuardOptions>>();

    private CropGuardMiddleware BuildMiddleware(RequestDelegate next, CropGuardOptions? opts = null)
    {
        _options.CurrentValue.Returns(opts ?? DefaultOptions());
        return new CropGuardMiddleware(next, _cropService, _options, NullLogger<CropGuardMiddleware>.Instance);
    }

    private static CropGuardOptions DefaultOptions() => new()
    {
        Enabled = true,
        MediaPathPrefix = "/media",
        AllowOriginalImage = true,
        LogBlockedRequests = false
    };

    private static HttpContext MakeContext(string path, string queryString = "")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Request.QueryString = queryString.Length > 0 ? new QueryString(queryString) : QueryString.Empty;
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    // ── Pass-through cases ────────────────────────────────────────────────────

    [Fact]
    public async Task WhenDisabled_AllRequestsPassThrough()
    {
        var opts = new CropGuardOptions { Enabled = false, MediaPathPrefix = "/media", AllowOriginalImage = true, LogBlockedRequests = false };
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next, opts);
        var ctx = MakeContext("/media/img.jpg", "?width=9999&height=9999");

        await mw.InvokeAsync(ctx);

        await next.Received(1).Invoke(ctx);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task WhenRequestNotUnderMediaPath_PassesThrough()
    {
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/css/site.css", "?v=123");

        await mw.InvokeAsync(ctx);

        await next.Received(1).Invoke(ctx);
    }

    [Fact]
    public async Task WhenNoQueryString_PassesThrough()
    {
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/media/image.jpg");

        await mw.InvokeAsync(ctx);

        await next.Received(1).Invoke(ctx);
    }

    [Fact]
    public async Task WhenNoWidthOrHeight_AndAllowOriginalTrue_PassesThrough()
    {
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/media/image.jpg", "?format=webp");

        await mw.InvokeAsync(ctx);

        await next.Received(1).Invoke(ctx);
    }

    [Fact]
    public async Task WhenCropIsAllowed_PassesThrough()
    {
        _cropService.IsAllowedAsync(800, 600).Returns(true);
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/media/image.jpg", "?width=800&height=600");

        await mw.InvokeAsync(ctx);

        await next.Received(1).Invoke(ctx);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    // ── Blocking cases ────────────────────────────────────────────────────────

    [Fact]
    public async Task WhenCropIsNotAllowed_Returns400()
    {
        _cropService.IsAllowedAsync(9999, 9999).Returns(false);
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/media/image.jpg", "?width=9999&height=9999");

        await mw.InvokeAsync(ctx);

        await next.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        Assert.Equal(400, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task WhenNoWidthOrHeight_AndAllowOriginalFalse_Returns400()
    {
        var opts = new CropGuardOptions { Enabled = true, MediaPathPrefix = "/media", AllowOriginalImage = false, LogBlockedRequests = false };
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next, opts);
        var ctx = MakeContext("/media/image.jpg", "?format=webp");

        await mw.InvokeAsync(ctx);

        Assert.Equal(400, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task PathMatchIsCaseInsensitive()
    {
        _cropService.IsAllowedAsync(200, 200).Returns(false);
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/MEDIA/image.jpg", "?width=200&height=200");

        await mw.InvokeAsync(ctx);

        Assert.Equal(400, ctx.Response.StatusCode);
    }

    // ── cc/rxy bypass regression (commit 307d761) ─────────────────────────────

    [Fact]
    public async Task WhenCcParamPresentWithoutDimensions_Returns400_EvenIfAllowOriginalTrue()
    {
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/media/image.jpg", "?cc=0,0.35,1,0.65");

        await mw.InvokeAsync(ctx);

        await next.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        Assert.Equal(400, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task WhenRxyParamPresentWithoutDimensions_Returns400()
    {
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/media/image.jpg", "?rxy=0.5,0.5");

        await mw.InvokeAsync(ctx);

        await next.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        Assert.Equal(400, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task WhenCcParamPairedWithAllowedDimensions_PassesThrough()
    {
        _cropService.IsAllowedAsync(800, 600).Returns(true);
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/media/image.jpg", "?cc=0,0,1,1&width=800&height=600");

        await mw.InvokeAsync(ctx);

        await next.Received(1).Invoke(ctx);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task WhenCcParamPairedWithDisallowedDimensions_Returns400()
    {
        _cropService.IsAllowedAsync(1234, 5678).Returns(false);
        var next = Substitute.For<RequestDelegate>();
        var mw = BuildMiddleware(next);
        var ctx = MakeContext("/media/image.jpg", "?cc=0,0,1,1&width=1234&height=5678");

        await mw.InvokeAsync(ctx);

        await next.DidNotReceive().Invoke(Arg.Any<HttpContext>());
        Assert.Equal(400, ctx.Response.StatusCode);
    }
}
