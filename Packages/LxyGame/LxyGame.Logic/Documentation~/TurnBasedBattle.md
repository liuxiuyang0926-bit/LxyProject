# 回合制战斗运行时

源码位于 `Runtime/Battle/TurnBased`，遵循：

```text
IBattleCommand
  -> IBattleAction
  -> BattleServices 修改 BattleWorld
  -> IBattleEvent
  -> RuleEngine
  -> 新 Action / Event
```

当前实现包含可运行闭环、基础 Service、Rule DSL Runtime、Buff、行动条、状态哈希、Replay 输入复演、Unity 表现层和独立编辑工具。战斗规则核心不读取 ScriptableObject；编辑资产会先编译为普通 C# RuntimeData，再交给核心和表现播放器。

## 最小使用流程

1. 构造 `CompiledRule`、`CompiledSkill`、`CompiledBuff`。
2. 创建 `CompiledBattleDatabase`。
3. 创建并初始化双方 `BattleUnit`。
4. 使用 `BattleApplication.CreateSession` 创建一场独立战斗。
5. 依次提交 `StartBattleCommand`、`UseSkillCommand`、`EndTurnCommand`。
6. 消费 `BattleStepResult.Events` 驱动动画、特效、音频和 UI。

编辑器菜单 `工具/战斗/运行回合制架构自检` 会执行普通攻击、死亡被动、Poison Buff 与双 Session 确定性校验。

编辑器菜单 `工具/战斗/运行技能资产闭环自检` 还会校验独立 ScriptableObject 编译、`skill_101011` 表现绑定，以及 `skill_logic_ach_tiaozhan_level_jinnang_3` 的“同一轮击杀全部 ConfigId=308”规则。

## 编辑器工作流

面向战斗策划的完整操作步骤、字段说明、示例时间轴、正式战斗接入和常见问题，请阅读：

- [回合制技能逻辑与表现编辑器策划使用手册](TurnBasedSkillAuthoringGuide.md)

1. 打开 `工具/战斗/技能逻辑编辑器`，在左侧选择逻辑资产，在右侧编辑数据契约、逻辑轨和并行组。
2. 打开 `工具/战斗/技能表现编辑器`，编辑并行组、表现轨和绝对帧操作，并绑定逻辑资产提供的数据键。
3. 可在 Project 窗口创建以下资产：
   - `回合制技能`：技能入口、目标方式、消耗，以及逻辑/表现引用；
   - `回合制技能逻辑`：Trigger、Condition、Target、Value、Action；
   - `回合制技能表现`：30 FPS 整数帧时间轴，包括动画、位移、特效、声音、命中、闪色和死亡检查。
4. 打开 `工具/战斗/18 位阵容与模型预览`，从二进制 Hero/Monster 表选择单位并保存阵容 ScriptableObject。该窗口用于资产编辑与模型预览，不负责生成或改写场景。
5. 使用两个自检菜单验证架构确定性和技能资产编译闭环。

默认定义资产位于：

`Assets/GameResources/Battle/TurnBased/DefaultTurnBasedBattle.asset`

参考资产位于：

```text
Assets/GameResources/Battle/TurnBased/Skills/
├── Definitions/skill_1001.asset
├── Definitions/skill_1002.asset
├── Definitions/skill_ach_tiaozhan_level_jinnang_3.asset
├── Logic/skill_logic_1001.asset
├── Logic/skill_logic_1002.asset
├── Logic/skill_logic_ach_tiaozhan_level_jinnang_3.asset
└── Expression/skill_101011.asset
```

权威逻辑仍只由 `BattleSession.Step` 推进。`TurnBasedBattleController` 只把输入转换为 Command，并顺序消费 `BattleStepResult.Events`；`TurnBasedUnitView` 使用 Unity 时间播放 Animator/Spine、位移、受击和飘字，不参与伤害、命中、死亡或回合判定。

Authoring 中的 Skill/Rule/Condition/Action/Value/Target/Expression 会在战斗创建时编译为 `CompiledBattleDatabase`。表现时间轴消费 `SkillCastEvent`、`DamageResolvedEvent` 和 `UnitDead`，不会写回生命、命中、死亡或回合状态；表现资源 Key 与时间轴也不参与权威状态哈希。
