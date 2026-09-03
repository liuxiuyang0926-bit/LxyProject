# Launcher icon source catalog

Put launcher-icon source PNGs in this directory. The file name is the logical ID used by game code and remote configuration:

```text
Assets/XPackageUse/AppIcons/
├─ Common/
│  ├─ icon_default.png
│  └─ icon_spring.png
├─ Android/
│  └─ icon_spring.png       # Overrides Common/icon_spring.png on Android only.
├─ iOS/
│  └─ icon_ios_special.png
└─ OpenHarmony/
   └─ icon_harmony_special.png
```

Rules:

- Only direct-child `icon_<id>.png` files are included. `<id>` must match `[a-z][a-z0-9_]*`.
- `Common` is merged first and the target platform directory overrides the same ID.
- If a platform has any icon, its merged set must contain `icon_default.png`.
- Source PNGs should be square, at least 1024×1024, and composed for each operating system's safe area. They are source art, not copied unchanged into every final package.

Call from game code or remote configuration:

```csharp
LauncherIconManager.SetIcon("spring");
LauncherIconManager.SetIcon(LauncherIconId.Default);
```

The iOS callback runs automatically for every Xcode-project build. The Android callback runs automatically when Unity exports the Gradle project (including the project's export-based Android pipeline); Unity 2022.3 does not invoke `IPostGenerateGradleAndroidProject` for its opaque Internal build path. Use **Tools/Lxy/Launcher Icons/Validate Source Catalog** before making a release build.

## OpenHarmony export integration

Invoke the following after the existing Unity-to-OpenHarmony exporter has created the target `AppScope` directory, and before `hvigor` packages the HAP:

```csharp
LauncherIconBuildProcessor.ExportOpenHarmonyIcons(appScopeDirectory);
```

It copies the merged `Common + OpenHarmony` PNGs to `AppScope/resources/base/media`, sets `app.icon`, and replaces `app.alternateIcons` in `AppScope/app.json5`.

At runtime, the project's installed OpenHarmony Unity bridge must register the platform call once during startup:

```csharp
LauncherIconManager.SetOpenHarmonyAdapter(myOpenHarmonyAdapter);
```

`myOpenHarmonyAdapter.SetIcon(iconId)` must call the dynamic-icon API offered by the specific OpenHarmony Unity integration in use. This project currently has no OpenHarmony exporter/runtime bridge to bind directly; keeping that adapter at the integration boundary prevents game or hot-update code from depending on a vendor-specific API.
