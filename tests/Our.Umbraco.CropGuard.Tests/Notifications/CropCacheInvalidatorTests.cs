using NSubstitute;
using Our.Umbraco.CropGuard.Notifications;
using Our.Umbraco.CropGuard.Services;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;

namespace Our.Umbraco.CropGuard.Tests.Notifications;

public sealed class CropCacheInvalidatorTests
{
    private const string ImageCropperAlias = "Umbraco.ImageCropper";
    private const string OtherAlias = "Umbraco.RichText";

    private readonly IAllowedCropService _cropService = Substitute.For<IAllowedCropService>();

    private CropCacheInvalidator BuildSut() => new(_cropService);

    private static IDataType MakeDataType(string editorAlias)
    {
        var dt = Substitute.For<IDataType>();
        dt.EditorAlias.Returns(editorAlias);
        return dt;
    }

    private static EventMessages Messages() => new();

    [Fact]
    public void Saved_ImageCropper_InvalidatesCache()
    {
        var notification = new DataTypeSavedNotification(MakeDataType(ImageCropperAlias), Messages());

        BuildSut().Handle(notification);

        _cropService.Received(1).InvalidateCache();
    }

    [Fact]
    public void Saved_OnlyOtherDataTypes_DoesNotInvalidate()
    {
        var notification = new DataTypeSavedNotification(MakeDataType(OtherAlias), Messages());

        BuildSut().Handle(notification);

        _cropService.DidNotReceive().InvalidateCache();
    }

    [Fact]
    public void Saved_MixedBatch_InvalidatesOnce()
    {
        var notification = new DataTypeSavedNotification(
            [MakeDataType(OtherAlias), MakeDataType(ImageCropperAlias)],
            Messages());

        BuildSut().Handle(notification);

        _cropService.Received(1).InvalidateCache();
    }

    [Fact]
    public void Deleted_ImageCropper_InvalidatesCache()
    {
        var notification = new DataTypeDeletedNotification(MakeDataType(ImageCropperAlias), Messages());

        BuildSut().Handle(notification);

        _cropService.Received(1).InvalidateCache();
    }

    [Fact]
    public void Deleted_OnlyOtherDataTypes_DoesNotInvalidate()
    {
        var notification = new DataTypeDeletedNotification(MakeDataType(OtherAlias), Messages());

        BuildSut().Handle(notification);

        _cropService.DidNotReceive().InvalidateCache();
    }
}
