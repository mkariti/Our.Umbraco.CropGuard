# Publishing CropGuard to NuGet and Umbraco Marketplace

This guide walks through publishing the package to NuGet.org and listing it on the Umbraco Marketplace.

## Prerequisites

1. **NuGet.org Account**: Create one at https://www.nuget.org (free)
2. **API Key**: Generate at https://www.nuget.org/account/apikeys
3. **Umbraco Account**: Register at https://our.umbraco.com
4. **Package Icon**: 128x128 PNG (see below)

## Step 1: Create Package Icon

Create a 128x128 pixel PNG icon at `src/Our.Umbraco.CropGuard/icon.png`:

**Design tips**:
- Use Umbraco's blue (#3544B1) as primary color
- Simple, recognizable symbol (shield, crop marks, or lock)
- Works well at small sizes
- No text (package name shows separately)

**Quick options**:
- Use a design tool like Figma, Canva, or Photoshop
- Generate via AI: "Create a simple shield icon with crop marks, blue gradient, 128x128 PNG"
- Find free icons: https://www.flaticon.com (attribution required)

## Step 2: Build the Package

```bash
# Clean previous builds
dotnet clean

# Create the NuGet package
dotnet pack src/Our.Umbraco.CropGuard/Our.Umbraco.CropGuard.csproj -c Release

# Output: bin/Release/Our.Umbraco.CropGuard.1.0.0.nupkg
```

**Verify the package**:
```bash
# Install NuGet Package Explorer (optional but helpful)
dotnet tool install -g NuGetPackageExplorer

# Open and inspect
NuGetPackageExplorer bin/Release/Our.Umbraco.CropGuard.1.0.0.nupkg
```

Check that it includes:
- ✅ `lib/net10.0/Our.Umbraco.CropGuard.dll`
- ✅ `contentFiles/any/any/App_Plugins/CropGuard/`
- ✅ `README.md`
- ✅ `icon.png`

## Step 3: Test Locally (Optional but Recommended)

Create a local NuGet source to test installation:

```bash
# Create local package source
mkdir ~/nuget-local
cp bin/Release/Our.Umbraco.CropGuard.1.0.0.nupkg ~/nuget-local/

# Add local source
dotnet nuget add source ~/nuget-local -n "Local"

# Test install in a fresh project
cd /path/to/test-umbraco-site
dotnet add package Our.Umbraco.CropGuard --version 1.0.0 --source "Local"

# Run and verify middleware works
dotnet run
```

## Step 4: Publish to NuGet.org

```bash
# Set your API key (get from nuget.org/account/apikeys)
export NUGET_API_KEY="your-api-key-here"

# Push to NuGet.org
dotnet nuget push bin/Release/Our.Umbraco.CropGuard.1.0.0.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json

# Or use nuget.exe (Windows)
nuget push bin\Release\Our.Umbraco.CropGuard.1.0.0.nupkg -ApiKey $NUGET_API_KEY -Source https://api.nuget.org/v3/index.json
```

**Validation time**:
- Package appears immediately at nuget.org/packages/Our.Umbraco.CropGuard
- Takes ~5-10 minutes to be searchable and installable
- Monitor status at: https://www.nuget.org/packages/Our.Umbraco.CropGuard/1.0.0

## Step 5: Submit to Umbraco Marketplace

1. **Go to the Marketplace**: https://marketplace.umbraco.com
2. **Click "List a Package"** (requires login)
3. **Fill out the form**:

### Basic Information
- **Package Name**: CropGuard
- **Package ID**: `Our.Umbraco.CropGuard` (must match NuGet ID)
- **Category**: Security, Developer Tools
- **License**: MIT
- **Current Version**: 1.0.0

### Description
Use the README.md content, highlighting:
- **Problem**: Arbitrary image resize parameters can exhaust server resources
- **Solution**: Middleware that validates crops against allowed list
- **Features**: Dynamic crops, static crops, custom dashboard
- **Benefits**: Prevents abuse, reduces CPU/memory load, protects cache

### Media
- **Icon**: Upload the same 128x128 PNG
- **Screenshots** (highly recommended):
  1. Custom crops dashboard in action
  2. Blocked request log entry
  3. Configuration in appsettings.json

### Links
- **Package URL**: https://www.nuget.org/packages/Our.Umbraco.CropGuard
- **Documentation**: https://github.com/mkariti/Our.Umbraco.CropGuard#readme
- **Source Code**: https://github.com/mkariti/Our.Umbraco.CropGuard
- **Issue Tracker**: https://github.com/mkariti/Our.Umbraco.CropGuard/issues

### Compatibility
- **Umbraco Version**: 17.0+
- **DotNet Version**: 10.0

### Installation Instructions
```bash
dotnet add package Our.Umbraco.CropGuard
```

No additional setup needed - package auto-registers via IComposer.

4. **Submit for Review**
   - Umbraco team reviews within 1-2 business days
   - You'll receive email notification when approved

## Step 6: Post-Publish Checklist

After marketplace approval:

- [ ] Add "Umbraco Marketplace" badge to README:
  ```markdown
  [![Umbraco Marketplace](https://img.shields.io/badge/Umbraco-Marketplace-blue)](https://marketplace.umbraco.com/package/our.umbraco.cropguard)
  ```

- [ ] Update GitHub repo description with "Umbraco Marketplace" topic

- [ ] Create GitHub release matching the NuGet version:
  ```bash
  gh release create v1.0.0 \
    --title "v1.0.0 - Initial Release" \
    --notes "See CHANGELOG.md for details" \
    bin/Release/Our.Umbraco.CropGuard.1.0.0.nupkg
  ```

- [ ] Announce on:
  - Umbraco Community Discord (#packages channel)
  - Twitter/X with #umbraco hashtag
  - Your blog/website

## Updating the Package

For future releases:

1. Update `<Version>` in the .csproj (follow SemVer)
2. Update CHANGELOG.md with release notes
3. Update `<PackageReleaseNotes>` in .csproj
4. Build and push new version to NuGet
5. Marketplace auto-detects new versions from NuGet

## Troubleshooting

### "Package already exists" error
You cannot overwrite published versions. Increment the version number.

### Icon not showing
- Ensure icon.png is exactly 128x128
- PNG format only
- File size < 1MB
- Rebuild package after adding icon

### Marketplace "Package not found"
Wait 10-15 minutes after NuGet publish for indexing to complete.

### Installation fails in target project
Check:
- Target framework compatibility (net10.0)
- Umbraco.Cms version (17.0+)
- Run `dotnet restore --force` to clear cache

## Best Practices

- **SemVer**: Use semantic versioning (MAJOR.MINOR.PATCH)
- **Changelog**: Keep CHANGELOG.md up to date
- **Testing**: Test installations before publishing
- **Documentation**: Keep README current with features
- **Support**: Respond to GitHub issues promptly
- **Breaking changes**: Clearly document in release notes

## Useful Links

- NuGet Package Explorer: https://github.com/NuGetPackageExplorer/NuGetPackageExplorer
- Umbraco Package Docs: https://docs.umbraco.com/umbraco-cms/extending/packages
- SemVer: https://semver.org
- Marketplace Guidelines: https://umbraco.com/products/add-ons/marketplace/submission-guidelines/
