using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Our.Umbraco.CropGuard.Configuration;
using Our.Umbraco.CropGuard.Models;
using Our.Umbraco.CropGuard.Services;
using System.Text.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Our.Umbraco.CropGuard.Tests.Services;

public sealed class AllowedCropServiceTests : IDisposable
{
    private readonly IDataTypeService _dataTypeService = Substitute.For<IDataTypeService>();
    private readonly IKeyValueService _keyValueService = Substitute.For<IKeyValueService>();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly IOptionsMonitor<CropGuardOptions> _options = Substitute.For<IOptionsMonitor<CropGuardOptions>>();

    private AllowedCropService BuildService(CropGuardOptions? opts = null)
    {
        _options.CurrentValue.Returns(opts ?? DefaultOptions());
        return new AllowedCropService(
            _dataTypeService,
            _keyValueService,
            _cache,
            _options,
            NullLogger<AllowedCropService>.Instance);
    }

    private static CropGuardOptions DefaultOptions() => new()
    {
        CacheDuration = TimeSpan.FromMinutes(5),
        StaticCrops = []
    };

    public void Dispose() => _cache.Dispose();

    // ── Static crops ──────────────────────────────────────────────────────────

    [Fact]
    public async Task StaticCrops_AreIncludedInAllowedList()
    {
        var opts = DefaultOptions();
        opts.StaticCrops.Add(new StaticCropConfig { Width = 800, Height = 600, Alias = "landscape" });

        _dataTypeService.GetByEditorAliasAsync(Arg.Any<string>()).Returns([]);
        _keyValueService.GetValue(Arg.Any<string>()).Returns((string?)null);

        var svc = BuildService(opts);
        var allowed = await svc.GetAllAsync();

        Assert.Single(allowed);
        Assert.Equal(800, allowed[0].Width);
        Assert.Equal(600, allowed[0].Height);
        Assert.Equal(CropSource.Static, allowed[0].Source);
    }

    [Fact]
    public async Task StaticCrop_IsAllowed()
    {
        var opts = DefaultOptions();
        opts.StaticCrops.Add(new StaticCropConfig { Width = 400, Height = 400 });

        _dataTypeService.GetByEditorAliasAsync(Arg.Any<string>()).Returns([]);
        _keyValueService.GetValue(Arg.Any<string>()).Returns((string?)null);

        var svc = BuildService(opts);

        Assert.True(await svc.IsAllowedAsync(400, 400));
        Assert.False(await svc.IsAllowedAsync(401, 400));
    }

    // ── Custom crops ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CustomCrops_StoredInKeyValueService_AreLoaded()
    {
        var json = JsonSerializer.Serialize(new[] { new { Width = 300, Height = 200, Alias = "thumb" } });
        _keyValueService.GetValue("CropGuard_CustomCrops").Returns(json);
        _dataTypeService.GetByEditorAliasAsync(Arg.Any<string>()).Returns([]);

        var svc = BuildService();
        var allowed = await svc.GetAllAsync();

        Assert.Single(allowed);
        Assert.Equal(300, allowed[0].Width);
        Assert.Equal(CropSource.Custom, allowed[0].Source);
    }

    [Fact]
    public async Task AddCustomCrop_PersistsAndIsAllowed()
    {
        _dataTypeService.GetByEditorAliasAsync(Arg.Any<string>()).Returns([]);
        _keyValueService.GetValue(Arg.Any<string>()).Returns((string?)null);

        var svc = BuildService();

        // Not allowed before adding
        Assert.False(await svc.IsAllowedAsync(500, 500));

        await svc.AddCustomCropAsync(500, 500, "custom");
        _keyValueService.Received().SetValue("CropGuard_CustomCrops", Arg.Any<string>());

        // Simulate the persisted value being returned on next load
        var json = JsonSerializer.Serialize(new[] { new { Width = 500, Height = 500, Alias = "custom" } });
        _keyValueService.GetValue("CropGuard_CustomCrops").Returns(json);

        Assert.True(await svc.IsAllowedAsync(500, 500));
    }

    [Fact]
    public async Task AddCustomCrop_DuplicateIsIgnored()
    {
        _dataTypeService.GetByEditorAliasAsync(Arg.Any<string>()).Returns([]);
        var json = JsonSerializer.Serialize(new[] { new { Width = 100, Height = 100, Alias = (string?)null } });
        _keyValueService.GetValue(Arg.Any<string>()).Returns(json);

        var svc = BuildService();
        await svc.AddCustomCropAsync(100, 100);

        // SetValue should NOT be called — crop already exists
        _keyValueService.DidNotReceive().SetValue(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RemoveCustomCrop_RemovesAndInvalidatesCache()
    {
        _dataTypeService.GetByEditorAliasAsync(Arg.Any<string>()).Returns([]);
        var json = JsonSerializer.Serialize(new[] { new { Width = 200, Height = 150, Alias = (string?)null } });
        _keyValueService.GetValue(Arg.Any<string>()).Returns(json);

        var svc = BuildService();

        // Confirm it was loaded
        Assert.True(await svc.IsAllowedAsync(200, 150));

        // Remove it — let RemoveAsync read the existing crop from the mock, remove it, and
        // invalidate the cache. Then update the mock to reflect the now-empty persisted state.
        await svc.RemoveCustomCropAsync(200, 150);
        _keyValueService.GetValue(Arg.Any<string>()).Returns("[]");

        // Cache was invalidated; next call reloads from mock which now returns []
        Assert.False(await svc.IsAllowedAsync(200, 150));
    }

    // ── Cache ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidateCache_ForcesReloadOnNextCall()
    {
        _dataTypeService.GetByEditorAliasAsync(Arg.Any<string>()).Returns([]);
        _keyValueService.GetValue(Arg.Any<string>()).Returns((string?)null);

        var svc = BuildService();
        _ = await svc.GetAllAsync(); // populate cache

        // GetByEditorAliasAsync should have been called once
        await _dataTypeService.Received(1).GetByEditorAliasAsync(Arg.Any<string>());

        svc.InvalidateCache();
        _ = await svc.GetAllAsync(); // should re-load

        await _dataTypeService.Received(2).GetByEditorAliasAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task SecondCall_UsesCacheWithoutReloading()
    {
        _dataTypeService.GetByEditorAliasAsync(Arg.Any<string>()).Returns([]);
        _keyValueService.GetValue(Arg.Any<string>()).Returns((string?)null);

        var svc = BuildService();
        _ = await svc.GetAllAsync();
        _ = await svc.GetAllAsync();

        // Should only have called through once
        await _dataTypeService.Received(1).GetByEditorAliasAsync(Arg.Any<string>());
    }
}
