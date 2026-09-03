# Lxy Launcher Icon

This package implements a single source catalog for Android, iOS, and OpenHarmony launcher icons.

- Runtime code calls `LauncherIconManager.SetIcon(iconId)`.
- Android generates density-specific mipmaps and `activity-alias` components, then enables the selected alias through `PackageManager`.
- iOS copies primary and alternate icon PNG variants into the generated Xcode project and writes `CFBundleIcons` / `CFBundleAlternateIcons`.
- OpenHarmony is exported through `LauncherIconBuildProcessor.ExportOpenHarmonyIcons(appScopeDirectory)`, which updates `AppScope/app.json5` and its media directory.

See `Assets/XPackageUse/AppIcons/README.md` for source layout and integration instructions.
