# LxyDemo architecture

The project has one hot-update gameplay assembly: `Game.Logic`.

```text
                         AOT bootstrap shell

Game.Main ──────────────> Game.Contracts <──────────── Game.Resource
    │
    └── loads Game.Logic through HybridCLR

                          hot-update gameplay

Game.Logic ─────────────> Game.Contracts / Game.Resource / XLua.Runtime
    ├── UI and Lua UI runtime
    ├── scene service and scene-flow logic
    ├── deterministic battle core, client and Unity view
    ├── shared utilities and state controls
    └── future event and gameplay systems
```

`Game.Main`, `Game.Contracts` and `Game.Resource` remain in the Player/AOT
bootstrap so the app can download resources and load `Game.Logic` before any
gameplay code exists. The bootstrap creates both the scene service and the
first-scene UI runtime by contract/reflection after `Game.Logic` is loaded; it
does not compile-reference concrete game types.

## Code layout

All gameplay runtime source is under `Packages/LxyGame/LxyGame.Logic/Runtime`:

- `Battle`: deterministic `Core`/`Math`/`Protocol`, frame client and view.
- `UI`: C# UI, Lua UI runtime and UI state logic.
- `Scene`: gameplay scene service and transition lifecycle.
- `Lua`: project Lua bindings and Game.Logic-owned generated wrappers.
- `Common`: shared game utilities and state-control components.

The `Battle/Core`, `Battle/Math` and `Battle/Protocol` folders remain
deterministic boundaries: they must not depend on UnityEngine, floating-point
simulation, system time, Unity Physics or non-deterministic random sources.

Editor tooling can remain in embedded `com.lxy.*` packages. It references
`Game.Logic` but does not create extra Player or HybridCLR gameplay DLLs.

`XLua.Runtime` is vendor runtime code in `Assets/XLua` and remains AOT. Its
generated wrapper for `GameSceneManager` is registered by Game.Logic before
the first LuaEnv is created, preventing an AOT-to-hot-update reference cycle.

Existing script `.meta` files move with their scripts so serialized
MonoBehaviour references retain their GUIDs.
