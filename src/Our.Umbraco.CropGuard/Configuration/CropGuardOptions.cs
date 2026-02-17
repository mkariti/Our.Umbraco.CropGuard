namespace Our.Umbraco.CropGuard.Configuration;

public sealed class CropGuardOptions
{
    public const string SectionName = "CropGuard";

    /// <summary>Enable or disable the protection middleware entirely.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// URL path prefix that triggers the middleware.
    /// Only requests starting with this path and containing a query string are inspected.
    /// </summary>
    public string MediaPathPrefix { get; set; } = "/media";

    /// <summary>
    /// Allow requests that have no resize parameters at all (i.e. serve the original image).
    /// Default: true.
    /// </summary>
    public bool AllowOriginalImage { get; set; } = true;

    /// <summary>
    /// Write a warning log entry whenever a request is blocked.
    /// Default: true.
    /// </summary>
    public bool LogBlockedRequests { get; set; } = true;

    /// <summary>
    /// How long to cache the resolved crop list before re-reading from Umbraco.
    /// Default: 5 minutes. Cache is also invalidated automatically on data-type / media-type saves.
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Static crops defined in appsettings.json that are always allowed,
    /// independent of what is configured in Umbraco content types.
    /// </summary>
    public List<StaticCropConfig> StaticCrops { get; set; } = [];
}

public sealed class StaticCropConfig
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Alias { get; set; }
}
