# 确定性战斗契约

## 包目录结构

| 路径 | 职责 |
| --- | --- |
| `Runtime/Core` | 权威 ECS 风格状态、系统、碰撞、事件、随机数和哈希 |
| `Runtime/Math` | 带溢出检查的定点数标量和向量数学 |
| `Runtime/Protocol` | 序列化帧命令和帧数据 |
| `Runtime/Client` | 帧缓冲、本地/服务端帧源边界、回放快照 |
| `Runtime/Config` | Unity 编辑配置向确定性定义的转换 |
| `Runtime/View` | Unity 输入演示和视觉投影 |
| `Editor` | Skill/Buff 编辑、Demo 创建和确定性验证 |

当前运行时代码统一编译为一个 `Game.Battle` 程序集；上述分层是架构边界，不是 asmdef 边界。审查时必须执行这些边界规则。

## 当前模拟契约

- 逻辑帧率：`BattleConst.LogicFps = 20`。
- 定点数精度：`FP.Precision = 10000`；原始值 `10000` 表示 `1`。
- 角度使用定点数角度制。
- 碰撞使用确定性定点数 AABB/OBB 测试和 SAT，不使用 Unity Physics。
- 命令按 `PlayerId`、再按 `Sequence` 排序。
- 实体按排序后的实体 ID 迭代。
- 技能操作按配置帧、再按编辑器列表顺序排序。
- `BattleWorld.Tick` 当前依次运行命令、移动、技能、Buff 和死亡系统。
- `BattleRandom` 是显式种子的 xorshift 生成器，其内部状态会影响确定性。
- `BattleWorld.CalculateStateHash` 是判断不同步的依据，必须覆盖全部权威状态。

## 输入与表现边界

```text
输入或服务端广播
  -> FrameCommand
  -> FrameData
  -> FrameBuffer
  -> BattleClient
  -> BattleWorld.Tick
  -> 状态 + BattleEvent
  -> BattleViewWorld / EntityView
```

`BattleDriver` 可以使用 Unity 输入、`Time.unscaledDeltaTime`、插值和渲染类型，因为它是边界/表现组件。不得把这些值带入权威计算。

`LocalFrameSession` 模拟未来的服务端帧接口。接入网络时增加基于网络的帧提供器或适配器，不要让 `BattleWorld` 了解传输层。

## 扩展检查表

新增命令时：

1. 分配明确且稳定的枚举值和协议表示。
2. 定义校验、顺序、重复输入和缺失输入行为。
3. 只在确定性边界内把原始数值字段转换为定点数。
4. 增加回放和双世界验证用例。

新增组件或系统时：

1. 定义确定性的初始化和迭代顺序。
2. 有意识地决定系统在 Tick 中的位置。
3. 将所有影响结果的字段加入状态哈希。
4. 只通过事件发送表现层结果。

新增数学或碰撞行为时：

1. 避免平台相关浮点数和引擎 API。
2. 定义溢出、舍入、归一化和边界接触行为。
3. 在 `BattleDeterminismValidator` 中增加原始值边界断言。

## 现有验证入口

- 菜单：`工具/战斗/运行确定性自检`
- 源码：`Packages/com.lxy.battle/Editor/BattleDeterminismValidator.cs`
- Demo 创建器：`工具/战斗/创建本地帧同步 Demo 场景`
- 包指南：`Packages/com.lxy.battle/README.md`

当前自检覆盖定点数边界、确定性三角函数、AABB/OBB 碰撞用例，并让两个世界使用克隆输入运行 240 帧，同时比较状态哈希。
