using Our.Umbraco.CropGuard.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Our.Umbraco.CropGuard.Notifications;

/// <summary>
/// Invalidates the crop cache when an ImageCropper data type is saved or deleted.
/// Other data type changes (rich text, content picker, etc.) are ignored.
/// Media type notifications are not needed — crop definitions live on data types, not media types.
/// </summary>
public sealed class CropCacheInvalidator :
    INotificationHandler<DataTypeSavedNotification>,
    INotificationHandler<DataTypeDeletedNotification>
{
    private const string ImageCropperAlias = "Umbraco.ImageCropper";

    private readonly IAllowedCropService _cropService;

    public CropCacheInvalidator(IAllowedCropService cropService)
        => _cropService = cropService;

    public void Handle(DataTypeSavedNotification notification)
    {
        if (notification.SavedEntities.Any(dt => dt.EditorAlias == ImageCropperAlias))
            _cropService.InvalidateCache();
    }

    public void Handle(DataTypeDeletedNotification notification)
    {
        if (notification.DeletedEntities.Any(dt => dt.EditorAlias == ImageCropperAlias))
            _cropService.InvalidateCache();
    }
}
