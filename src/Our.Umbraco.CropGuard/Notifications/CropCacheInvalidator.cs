using Our.Umbraco.CropGuard.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Our.Umbraco.CropGuard.Notifications;

/// <summary>
/// Listens for Umbraco data-type and media-type save/delete events and
/// invalidates the crop cache so the next request picks up the latest definitions.
/// </summary>
public sealed class CropCacheInvalidator :
    INotificationHandler<DataTypeSavedNotification>,
    INotificationHandler<DataTypeDeletedNotification>,
    INotificationHandler<MediaTypeSavedNotification>,
    INotificationHandler<MediaTypeDeletedNotification>
{
    private readonly IAllowedCropService _cropService;

    public CropCacheInvalidator(IAllowedCropService cropService)
        => _cropService = cropService;

    public void Handle(DataTypeSavedNotification notification) => _cropService.InvalidateCache();
    public void Handle(DataTypeDeletedNotification notification) => _cropService.InvalidateCache();
    public void Handle(MediaTypeSavedNotification notification) => _cropService.InvalidateCache();
    public void Handle(MediaTypeDeletedNotification notification) => _cropService.InvalidateCache();
}
