---
name: unity-frame-sync
description: 实现、审查、调试或扩展 LxyDemo 的确定性帧同步战斗运行时。适用于 Packages/com.lxy.battle、FP 或 FPVector2 数学、BattleWorld 系统、FrameCommand/FrameData/FrameBuffer 流程、确定性碰撞或随机数、状态哈希、回放、服务端帧接入，以及帧同步战斗中的逻辑/表现分离。
---

# Unity 帧同步

保证在配置、随机种子和有序帧输入相同的情况下，战斗结果完全一致。修改战斗行为前先阅读 [references/deterministic-battle.md](references/deterministic-battle.md)。

## 判断改动归属

- 权威模拟放入 `Runtime/Core`、`Runtime/Math` 或 `Runtime/Protocol`，且不得依赖 `UnityEngine`。
- 帧接入、缓冲、回放记录和客户端编排放入 `Runtime/Client`。
- `Runtime/View` 只能读取状态并消费 `BattleEvent`；表现层不得决定伤害、命中、Buff、移动结果、死亡或随机结果。
- 编辑工具和确定性自检放在 Editor 程序集中。

## 确定性实现规则

1. 模拟只能通过连续的 `BattleWorld.Tick(FrameData)` 调用推进。
2. 使用整数、`FP` 和 `FPVector2` 表示模拟量；在边界使用原始定点数协议字段。
3. 使用带明确共享种子的 `BattleRandom`。权威结果中禁止使用 `UnityEngine.Random`、`System.Random`、系统时间、帧时间、Unity Physics 或浮点数学。
4. 为所有会影响结果的集合和命令定义稳定顺序。不得依赖 `Dictionary` 或哈希集合的枚举顺序。
5. 保持命令按 `PlayerId`、再按 `Sequence` 排序；新增任何可能产生歧义的操作前，先定义明确的最终比较规则。
6. 明确系统执行顺序。插入或调整系统前，审查所有下游行为。
7. 将每个新增的权威状态字段和确定性随机状态按稳定顺序加入 `CalculateStateHash`。
8. 逻辑决定结果后，通过 `BattleEvent` 数据发出表现效果。
9. 保持 `FrameCommand -> FrameData -> FrameBuffer -> BattleWorld` 边界。接入网络时替换本地帧源，不要绕过帧流水线。

## 验证

- 为新行为和边界情况扩展双世界确定性自检，使用相同种子和克隆输入。
- Unity Editor 可执行时运行 `工具/战斗/运行确定性自检`。
- 按需验证边界值、溢出行为、命令顺序、缺失/迟到帧和重复运行状态哈希。
- 搜索 `Runtime/Core`、`Runtime/Math` 和 `Runtime/Protocol`，检查是否误引入 `UnityEngine`、`Time`、Physics、随机数、`float` 或 `double`。
- 修改编辑器代码或配置时，同时编译 `Game.Battle` 和 `Game.Battle.Editor`。
- 如果没有运行 Editor 自检或多运行时/多设备比较，必须明确说明。

报告结果时，将权威逻辑改动与客户端、表现层、编辑器改动分开说明，并附上确定性验证证据。
