# Our.Umbraco.CropGuard

An Umbraco 17 package that protects the ImageSharp image processing pipeline from abuse by only allowing pre-defined crop dimensions to proceed.

## The Problem

Umbraco uses [SixLabors.ImageSharp.Web](https://docs.sixlabors.com/articles/imagesharp.web/) to process images on-the-fly. Anyone who knows your media URL can append arbitrary resize parameters:

```
/media/abc123/image.jpg?width=9999&height=9999&quality=100
```

This triggers expensive CPU and memory work on every unique combination, and can be used to exhaust server resources or fill your disk cache.

## The Solution

CropGuard sits in the ASP.NET Core middleware pipeline and intercepts all requests to `/media` that carry query string parameters. It checks the requested dimensions against a list of **allowed crops** and rejects anything not on the list with a `400 Bad Request`.

```
Request → CropGuard → Is it /media with query params?
                           ↓ yes
                      Are width+height in the allowed list?
                           ↓ no  → 400 Bad Request
                           ↓ yes → ImageSharp processes normally
```

Allowed crops come from three sources — all merged automatically:

| Source | How |
|---|---|
| **Dynamic** | Read from Umbraco Image Cropper data type definitions |
| **Static** | Defined in `appsettings.json` |
| **Custom** | Added at runtime via the backoffice dashboard |

## Requirements

- Umbraco 17+
- .NET 10

## Installation

```bash
dotnet add package Our.Umbraco.CropGuard
```

No `Program.cs` changes needed. The package registers itself automatically via Umbraco's `IComposer`.

## Configuration

Add to `appsettings.json` (all settings are optional — defaults work out of the box):

```json
{
  "CropGuard": {
    "Enabled": true,
    "MediaPathPrefix": "/media",
    "AllowOriginalImage": true,
    "LogBlockedRequests": true,
    "CacheDuration": "00:05:00",
    "StaticCrops": [
      { "Width": 1200, "Height": 630, "Alias": "ogImage" },
      { "Width": 1920, "Height": 1080, "Alias": "hero" }
    ]
  }
}
```

| Option | Default | Description |
|---|---|---|
| `Enabled` | `true` | Toggle the middleware on/off without removing the package |
| `MediaPathPrefix` | `/media` | URL prefix that triggers inspection |
| `AllowOriginalImage` | `true` | Allow requests with no resize parameters (serve original) |
| `LogBlockedRequests` | `true` | Write a warning log entry for each blocked request |
| `CacheDuration` | `5 minutes` | How long to cache the resolved crop list |
| `StaticCrops` | `[]` | Fixed crop sizes always allowed, regardless of Umbraco config |

## How Crops Are Resolved

### Dynamic crops (automatic)

CropGuard reads all **Image Cropper** data types defined in your Umbraco media types and extracts their crop configurations. These are loaded automatically — no manual configuration needed.

The crop list is cached in memory and automatically invalidated whenever a data type or media type is saved or deleted in the backoffice.

### Static crops (`appsettings.json`)

Use `StaticCrops` for sizes that aren't defined as Umbraco crops but should always be allowed — for example, Open Graph images generated in code.

### Custom crops (dashboard)

Navigate to **Settings → CropGuard** in the Umbraco backoffice. From here you can:

- View all currently allowed crops and their source
- Add custom crops (persisted to the database)
- Remove custom crops
- Force a cache refresh

## Behaviour

| Request | Result |
|---|---|
| `/media/image.jpg` | Allowed (no params) |
| `/media/image.jpg?width=200&height=300` | Allowed if `200×300` is a known crop |
| `/media/image.jpg?width=9999&height=9999` | **Blocked — 400** |
| `/media/image.jpg?width=200&height=300&quality=80` | Allowed if `200×300` is known (`quality` is not checked) |
| `/css/site.css` | Passed through (not a `/media` request) |

> Requests that hit the cache are served directly from disk — ImageSharp only processes each unique combination **once**, so replay of known valid URLs is not a concern.

## Monitoring & Logs

CropGuard logs all important events using ASP.NET Core's standard `ILogger`. Enable detailed logging in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Our.Umbraco.CropGuard": "Debug"
    }
  }
}
```

### What Gets Logged

| Level | Event | Example |
|---|---|---|
| **Warning** | Blocked request | `CropGuard: blocked request /media/image.jpg?width=999&height=999` |
| **Warning** | Configuration errors | `CropGuard: failed to read data types from Umbraco` |
| **Debug** | Startup info | `CropGuard: loaded 15 allowed crops` |

### Viewing Logs

**In Umbraco Log Viewer** (recommended):
1. Go to **Settings → Log Viewer** in the backoffice
2. Search for "CropGuard" to filter relevant entries
3. Set date range to see historical blocked requests

**Via log files**:
```bash
# View recent CropGuard activity
tail -f umbraco/Logs/*.json | grep -i cropguard

# Count blocked requests today
grep "CropGuard: blocked" umbraco/Logs/UmbracoTraceLog.*.json | wc -l
```

**Example log entry**:
```json
{
  "@t": "2026-02-19T10:30:00.123Z",
  "@mt": "CropGuard: blocked request {Path}{QueryString}",
  "@l": "Warning",
  "Path": "/media/abc123/image.jpg",
  "QueryString": "?width=9999&height=9999",
  "SourceContext": "Our.Umbraco.CropGuard.Middleware.CropGuardMiddleware"
}
```

## Development

### Prerequisites

- .NET 10 SDK
- Umbraco 17

### Build

```bash
dotnet build
```

### Solution structure

```
src/
  Our.Umbraco.CropGuard/         ← package source
    Configuration/               ← CropGuardOptions
    Models/                      ← AllowedCrop, CropKey, CropSource
    Services/                    ← IAllowedCropService, AllowedCropService
    Middleware/                  ← CropGuardMiddleware
    Notifications/               ← cache invalidation on type changes
    Controllers/                 ← Management API (dashboard backend)
    Composers/                   ← auto-registration via IComposer
    App_Plugins/CropGuard/       ← backoffice dashboard (Lit element)
```

## Versioning & Compatibility

This package follows [Semantic Versioning](https://semver.org/):

- **MAJOR** — breaking changes to public API or configuration schema
- **MINOR** — new features, backwards compatible
- **PATCH** — bug fixes

### Umbraco compatibility

| CropGuard | Umbraco | .NET |
|---|---|---|
| 1.x | 17.x | 10 |

> New Umbraco major versions will get a matching new CropGuard major version if breaking changes are needed, or a minor release if fully compatible.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for the full release history.

## Contributing

Issues and pull requests are welcome. Please open an issue before starting work on a significant change.

## License

MIT
