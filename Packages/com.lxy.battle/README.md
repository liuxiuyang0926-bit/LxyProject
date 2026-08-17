# Game.Battle

本包只产生两个程序集：

- `Game.Battle.dll`：Protocol、定点数、ECS 逻辑、本地帧源和 Unity 表现。
- `Game.Battle.Editor.dll`：技能/Buff 编辑器、Demo 场景和确定性自检。

逻辑层位于 `Runtime/Core`、`Runtime/Math`、`Runtime/Protocol`，这些目录
禁止引用 UnityEngine。表现层只读取逻辑状态和 BattleEvent，不参与伤害、
命中、Buff、死亡等战斗结果。

## 快速开始

1. 打开 `工具/战斗/技能编辑器` 和 `工具/战斗/Buff编辑器` 修改配置。
2. 执行 `工具/战斗/运行确定性自检`。
3. 执行 `工具/战斗/创建本地帧同步 Demo 场景`。
4. Play 后使用 WASD/方向键移动，Space 释放技能。

本地模式同样产生 `FrameCommand -> FrameData -> FrameBuffer -> BattleWorld`
链路。接入服务器时只替换 `LocalFrameSession` 的帧来源。

## 技能帧操作

技能不再使用单一 `hitFrame`。每个技能可以在时间轴中添加多条操作，
同一帧按编辑器列表顺序执行：

- `DamageBox`：使用 AABB 或 OBB 查询敌方碰撞盒，可同时造成伤害和附加 Buff。
- `ApplyBuffToTarget`：在指定帧直接给技能目标添加 Buff。
- `DisplaceCaster`：按施法时朝向执行确定性的本地位移。

伤害盒的偏移、半尺寸均为定点数 Raw 值，`10000 = 1`。OBB 角度同样
使用 Raw 值，`10000 = 1 度`。AABB 始终与世界轴对齐；OBB 旋转角为
“施法朝向角 + 配置角度”。目标实体默认具有 `0.5 x 0.5` 的 AABB，
也可以在 `BattleWorld.AddEntity` 时指定 OBB、半尺寸和相对旋转。

## 定点数学

`FP` 提供带溢出检查的加减乘除、`Sqrt`、`Sin`、`Cos`、`Atan2`、
`NormalizeAngle`、`Clamp` 和 `Abs`；`FPVector2` 提供 `Normalize`、
`Rotate`、`Clamp` 和 `Abs`。三角函数使用角度制整数 CORDIC，碰撞使用
纯定点数 SAT，不依赖 Unity Physics、`float` 或 `double`。

## HybridCLR

本包不会自动修改你的 HybridCLRSettings。需要让战斗逻辑热更新时，在
`Hot Update Assemblies` 手动添加 `Game.Battle`，放在可能引用它的
`Assembly-CSharp` 前面，然后执行：

`工具/HybridCLR/生成全部并同步到 YooAsset`
