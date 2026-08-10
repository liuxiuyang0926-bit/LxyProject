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

Game.HotUpdate ────────> Game.Contracts
    └──> Game.UI ──────> Game.Resource (AOT)
              └────────> Game.Lua ─────> Game.Common
                              └────────> XLua.Runtime (AOT)

Assembly-CSharp ───────> hot-update framework assemblies as needed
```

`Game.Contracts`, `Game.Resource`, `Game.Scene`, and `Game.Main` form the
first-party AOT bootstrap. `Game.Common`, `Game.Lua`, `Game.UI`,
`Assembly-CSharp`, and `Game.HotUpdate` are loaded as HybridCLR assemblies in
that dependency order. `Game.Main` invokes the two public entry phases by
reflection, so the AOT shell never references `Game.UI` directly.

`GameRuntimeConfig` is the stable bootstrap configuration bridge. The player
contains one seed configuration URL because the first remote request happens
before any hot-update DLL can be downloaded. After `Game.HotUpdate` starts,
`HotUpdateRuntimeConfig` can replace and persist the URL for the current
session and the next launch.

## Ownership

- `com.lxy.contracts`: cross-boundary startup contracts.
- `com.lxy.core`: common utilities and state-control components.
- `com.lxy.lua`: project-specific XLua bindings and authoring tools.
- `com.lxy.resource`: YooAsset initialization and update lifecycle.
- `com.lxy.scene`: scene loading and transition lifecycle.
- `com.lxy.ui`: reusable UI runtime, Lua UI integration, and editor tools.
- `com.lxy.main`: the AOT bootstrap and HybridCLR loader.
- `com.lxy.hotupdate`: the updateable business runtime entry.

Resources, scenes, prefabs, configuration assets, generated HybridCLR files, and generated project code remain in `Assets`. The vendor XLua distribution also remains under `Assets/XLua` because its generator, examples, resources, and generated partial classes share that layout; dedicated `XLua.Runtime` and Editor asmdefs isolate it from `Assembly-CSharp`.

Existing script `.meta` files move with their scripts, preserving every serialized MonoBehaviour reference.
