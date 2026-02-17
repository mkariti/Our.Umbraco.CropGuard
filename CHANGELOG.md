# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

## [1.0.0] - 2026-02-17

### Added
- `CropGuardMiddleware` — intercepts `/media` requests and blocks any width/height combination not in the allowed list
- `AllowedCropService` — resolves allowed crops from three sources: dynamic (Umbraco Image Cropper data types), static (`appsettings.json`), and custom (dashboard-persisted)
- `CropGuardComposer` — zero-config registration via Umbraco `IComposer`; no `Program.cs` changes required
- `CropCacheInvalidator` — automatically invalidates the crop cache when data types or media types are saved/deleted
- Backoffice dashboard under **Settings → CropGuard** for viewing, adding and removing custom crops
- `CropGuardOptions` — configurable via `appsettings.json` (`Enabled`, `MediaPathPrefix`, `AllowOriginalImage`, `LogBlockedRequests`, `CacheDuration`, `StaticCrops`)
- Management API endpoint at `/umbraco/management/api/v1/cropguard/crops` powering the dashboard
- In-memory `HashSet<CropKey>` cache for O(1) per-request lookup

[Unreleased]: https://github.com/mkariti/Our.Umbraco.CropGuard/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/mkariti/Our.Umbraco.CropGuard/releases/tag/v1.0.0
