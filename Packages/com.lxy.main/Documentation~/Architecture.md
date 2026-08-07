# LxyDemo modular architecture

The project keeps game content in `Assets` and reusable first-party code in embedded UPM packages.

## Assembly dependency direction

Arrows point from a consumer to its compile-time dependency.

```text
Game.HotUpdate ───────────────> Game.Contracts <────────────── Game.Main
                                                               ├──> Game.Resource
                                                               ├──> Game.Scene
                                                               └──> Game.UI
                                                                      ├──> Game.Resource
                                                                      └──> Game.Lua
                                                                             ├──> Game.Common
                                                                             └──> XLua.Runtime
                                                                                    └──> Game.Scene
```

`Game.Main` and framework assemblies are AOT code. `Game.HotUpdate` is the HybridCLR update assembly. `Game.Contracts` is deliberately small and stable so the two sides do not depend on one another directly.

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
