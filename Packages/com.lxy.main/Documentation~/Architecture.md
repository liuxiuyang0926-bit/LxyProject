# LxyDemo modular architecture

The project keeps game content in `Assets` and reusable first-party code in embedded UPM packages.

## Assembly dependency direction

Arrows point from a consumer to its compile-time dependency.

```text
                         AOT shell

Game.Main ──────────────> Game.Contracts <──────────── Game.Resource
    ├──> Game.Resource
    └──> Game.Scene

                      hot-update runtime

Game.UI ──────────────> Game.Contracts
    ├─────────────────> Game.Resource (AOT)
    └─────────────────> Game.Lua ─────> Game.Common
                                      └> XLua.Runtime (AOT)

Assembly-CSharp ───────> hot-update framework assemblies as needed
```

`Game.Contracts`, `Game.Resource`, `Game.Scene`, and `Game.Main` form the
first-party AOT bootstrap. The actual HybridCLR DLL list comes only from
`HybridCLRSettings`; the generated runtime manifest preserves that order and
also supports an empty list. `Game.Main` loads the configured DLLs before the
first business scene, then initializes its UI through `IFirstSceneRuntime`, so
the AOT shell never references `Game.UI` directly.

`GameRuntimeConfig` is the stable bootstrap configuration bridge. The player
contains one seed configuration URL because the first remote request happens
before any hot-update DLL can be downloaded. Runtime URL overrides can be
applied through `GameRuntimeConfig.TryApplyBootstrapOverride` by any future
hot-update assembly.

## Ownership

- `com.lxy.contracts`: cross-boundary startup contracts.
- `com.lxy.core`: common utilities and state-control components.
- `com.lxy.lua`: project-specific XLua bindings and authoring tools.
- `com.lxy.resource`: YooAsset initialization and update lifecycle.
- `com.lxy.scene`: scene loading and transition lifecycle.
- `com.lxy.ui`: reusable UI runtime, Lua UI integration, and editor tools.
- `com.lxy.battle`: one runtime DLL containing deterministic battle logic,
  local frame client, and Unity views; editor tools live in an Editor-only DLL.
- `com.lxy.main`: the AOT bootstrap and HybridCLR loader.

Resources, scenes, prefabs, configuration assets, generated HybridCLR files, and generated project code remain in `Assets`. The vendor XLua distribution also remains under `Assets/XLua` because its generator, examples, resources, and generated partial classes share that layout; dedicated `XLua.Runtime` and Editor asmdefs isolate it from `Assembly-CSharp`.

Existing script `.meta` files move with their scripts, preserving every serialized MonoBehaviour reference.
