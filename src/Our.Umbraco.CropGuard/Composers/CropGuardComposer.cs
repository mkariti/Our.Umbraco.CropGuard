using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Our.Umbraco.CropGuard.Configuration;
using Our.Umbraco.CropGuard.Middleware;
using Our.Umbraco.CropGuard.Notifications;
using Our.Umbraco.CropGuard.Services;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Our.Umbraco.CropGuard.Composers;

/// <summary>
/// Registers all CropGuard services and hooks the middleware into
/// the ASP.NET Core pipeline automatically — no Program.cs changes needed.
/// </summary>
public sealed class CropGuardComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Bind options from appsettings.json "CropGuard" section
        builder.Services
            .AddOptions<CropGuardOptions>()
            .BindConfiguration(CropGuardOptions.SectionName);

        // Core service — singleton because it owns the in-memory crop cache
        builder.Services.AddSingleton<IAllowedCropService, AllowedCropService>();

        // Invalidate cache when an ImageCropper data type is saved or deleted.
        // Media type changes don't affect crop definitions (those live on data types).
        builder.AddNotificationHandler<DataTypeSavedNotification, CropCacheInvalidator>();
        builder.AddNotificationHandler<DataTypeDeletedNotification, CropCacheInvalidator>();

        // Register the IStartupFilter that inserts our middleware into the pipeline
        // at startup — before ImageSharp.Web processes image requests.
        builder.Services.AddTransient<IStartupFilter, CropGuardStartupFilter>();
    }
}

/// <summary>
/// Inserts <see cref="CropGuardMiddleware"/> early in the pipeline,
/// scoped only to requests that match the configured media path prefix.
/// </summary>
internal sealed class CropGuardStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        => app =>
        {
            app.UseMiddleware<CropGuardMiddleware>();
            next(app);
        };
}
