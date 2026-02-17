using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Our.Umbraco.CropGuard.Configuration;
using Our.Umbraco.CropGuard.Models;
using System.Text.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Our.Umbraco.CropGuard.Services;

public sealed class AllowedCropService : IAllowedCropService
{
    private const string CacheKey = "CropGuard_AllowedCrops";
    private const string DbKey = "CropGuard_CustomCrops";

    // Constants.PropertyEditors.Aliases.ImageCropper
    private const string ImageCropperAlias = "Umbraco.ImageCropper";

    private readonly IDataTypeService _dataTypeService;
    private readonly IKeyValueService _keyValueService;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<CropGuardOptions> _options;
    private readonly ILogger<AllowedCropService> _logger;

    public AllowedCropService(
        IDataTypeService dataTypeService,
        IKeyValueService keyValueService,
        IMemoryCache cache,
        IOptionsMonitor<CropGuardOptions> options,
        ILogger<AllowedCropService> logger)
    {
        _dataTypeService = dataTypeService;
        _keyValueService = keyValueService;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> IsAllowedAsync(int width, int height)
    {
        var (_, lookup) = await GetOrBuildCacheAsync();
        return lookup.Contains(new CropKey(width, height));
    }

    public async Task<IReadOnlyList<AllowedCrop>> GetAllAsync()
    {
        var (crops, _) = await GetOrBuildCacheAsync();
        return crops;
    }

    public async Task AddCustomCropAsync(int width, int height, string? alias = null)
    {
        var existing = LoadCustomCrops();
        if (existing.Any(c => c.Width == width && c.Height == height))
            return;

        existing.Add(new AllowedCrop { Width = width, Height = height, Alias = alias, Source = CropSource.Custom });
        PersistCustomCrops(existing);
        InvalidateCache();
        await Task.CompletedTask;
    }

    public async Task RemoveCustomCropAsync(int width, int height)
    {
        var existing = LoadCustomCrops();
        var removed = existing.RemoveAll(c => c.Width == width && c.Height == height);
        if (removed > 0)
        {
            PersistCustomCrops(existing);
            InvalidateCache();
        }
        await Task.CompletedTask;
    }

    public void InvalidateCache() => _cache.Remove(CacheKey);

    // -------------------------------------------------------------------------

    private async Task<(IReadOnlyList<AllowedCrop> crops, HashSet<CropKey> lookup)> GetOrBuildCacheAsync()
    {
        if (_cache.TryGetValue(CacheKey, out (IReadOnlyList<AllowedCrop>, HashSet<CropKey>) cached))
            return cached;

        var opts = _options.CurrentValue;
        var crops = new List<AllowedCrop>();

        // 1. Dynamic: read from Umbraco Image Cropper data type configurations
        await foreach (var crop in ResolveDynamicCropsAsync())
            crops.Add(crop);

        // 2. Static: from appsettings.json
        foreach (var s in opts.StaticCrops)
            crops.Add(new AllowedCrop { Width = s.Width, Height = s.Height, Alias = s.Alias, Source = CropSource.Static });

        // 3. Custom: persisted via the dashboard
        crops.AddRange(LoadCustomCrops());

        var lookup = new HashSet<CropKey>(crops.Select(c => c.Key));

        _logger.LogDebug("ImageProcessProtector: loaded {Count} allowed crops", lookup.Count);

        var entry = (crops as IReadOnlyList<AllowedCrop>, lookup);
        _cache.Set(CacheKey, entry, opts.CacheDuration);
        return entry;
    }

    private async IAsyncEnumerable<AllowedCrop> ResolveDynamicCropsAsync()
    {
        IEnumerable<IDataType> dataTypes;
        try
        {
            dataTypes = await _dataTypeService.GetByEditorAliasAsync(ImageCropperAlias);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ImageProcessProtector: failed to read data types from Umbraco");
            yield break;
        }

        foreach (var dataType in dataTypes)
        {
            // ConfigurationObject returns the typed config — we extract crops via JSON
            // to avoid a hard dependency on Umbraco.Cms.Infrastructure.
            var configObj = dataType.ConfigurationObject;
            if (configObj is null)
                continue;

            List<AllowedCrop>? extracted = null;
            try
            {
                extracted = ExtractCropsFromConfig(configObj);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ImageProcessProtector: failed to extract crops from data type '{Name}'", dataType.Name);
            }

            if (extracted is null)
                continue;

            foreach (var crop in extracted)
                yield return crop;
        }
    }

    /// <summary>
    /// Extracts crop definitions from an ImageCropper configuration object by serialising to JSON.
    /// This avoids a direct reference to Umbraco.Cms.Infrastructure.
    /// Expected shape: { "crops": [ { "alias": "...", "width": 200, "height": 300 } ] }
    /// </summary>
    private static List<AllowedCrop> ExtractCropsFromConfig(object configObj)
    {
        var result = new List<AllowedCrop>();
        var json = JsonSerializer.SerializeToElement(configObj, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (!json.TryGetProperty("crops", out var cropsEl) || cropsEl.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var cropEl in cropsEl.EnumerateArray())
        {
            if (!cropEl.TryGetProperty("width", out var wEl) || !cropEl.TryGetProperty("height", out var hEl))
                continue;

            var width = wEl.GetInt32();
            var height = hEl.GetInt32();
            var alias = cropEl.TryGetProperty("alias", out var aEl) ? aEl.GetString() : null;

            if (width > 0 && height > 0)
                result.Add(new AllowedCrop { Width = width, Height = height, Alias = alias, Source = CropSource.Dynamic });
        }

        return result;
    }

    private List<AllowedCrop> LoadCustomCrops()
    {
        var json = _keyValueService.GetValue(DbKey);
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<SerializableCrop>>(json)
                ?.Select(c => new AllowedCrop { Width = c.Width, Height = c.Height, Alias = c.Alias, Source = CropSource.Custom })
                .ToList() ?? [];
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "ImageProcessProtector: failed to deserialize custom crops from database");
            return [];
        }
    }

    private void PersistCustomCrops(List<AllowedCrop> crops)
    {
        var serializable = crops.Select(c => new SerializableCrop(c.Width, c.Height, c.Alias)).ToList();
        _keyValueService.SetValue(DbKey, JsonSerializer.Serialize(serializable));
    }

    private sealed record SerializableCrop(int Width, int Height, string? Alias);
}
