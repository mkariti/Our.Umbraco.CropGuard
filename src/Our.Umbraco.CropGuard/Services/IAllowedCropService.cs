using Our.Umbraco.CropGuard.Models;

namespace Our.Umbraco.CropGuard.Services;

public interface IAllowedCropService
{
    /// <summary>Returns true if the given width+height combination is in the allowed set.</summary>
    ValueTask<bool> IsAllowedAsync(int width, int height);

    /// <summary>Returns all currently allowed crops across all sources.</summary>
    Task<IReadOnlyList<AllowedCrop>> GetAllAsync();

    /// <summary>Persists a custom crop to the database and invalidates the cache.</summary>
    Task AddCustomCropAsync(int width, int height, string? alias = null);

    /// <summary>Removes a custom crop from the database and invalidates the cache.</summary>
    Task RemoveCustomCropAsync(int width, int height);

    /// <summary>Clears the in-memory crop cache, forcing a reload on the next request.</summary>
    void InvalidateCache();
}
