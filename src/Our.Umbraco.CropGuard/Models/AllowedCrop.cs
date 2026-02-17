namespace Our.Umbraco.CropGuard.Models;

public sealed class AllowedCrop
{
    public int Width { get; init; }
    public int Height { get; init; }
    public string? Alias { get; init; }
    public CropSource Source { get; init; }

    public CropKey Key => new(Width, Height);
}

/// <summary>Lookup key for O(1) HashSet matching on width+height.</summary>
public readonly record struct CropKey(int Width, int Height);

public enum CropSource
{
    /// <summary>Read from an Umbraco Image Cropper data type definition.</summary>
    Dynamic,
    /// <summary>Specified in appsettings.json.</summary>
    Static,
    /// <summary>Added via the backoffice dashboard and persisted in the database.</summary>
    Custom
}
