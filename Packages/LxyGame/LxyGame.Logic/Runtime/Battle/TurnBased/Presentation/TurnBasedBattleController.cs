using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle.TurnBased.Application;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.Core.Commands;
using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.Core.Session;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using LxyDemo.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Battle.TurnBased.Presentation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LxyDemo/Battle/Turn Based Battle Controller")]
    public sealed class TurnBasedBattleController : MonoBehaviour
    {
        [Header("战斗定义")]
        [SerializeField] private TurnBasedBattleDefinition definition;
        [SerializeField] private bool initializeOnStart = true;

        [Header("场景表现")]
        [SerializeField] private TurnBasedUnitView[] unitViews =
            Array.Empty<TurnBasedUnitView>();

        [Header("战斗 HUD")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text battleLogText;
        [SerializeField] private Transform skillButtonRoot;
        [SerializeField] private Button skillButtonTemplate;
        [SerializeField] private Button exitButton;
        [SerializeField] private CanvasGroup resultPanel;
        [SerializeField] private TMP_Text resultText;

        private readonly Dictionary<int, TurnBasedUnitView> views =
            new Dictionary<int, TurnBasedUnitView>();
        private readonly List<Button> skillButtons = new List<Button>();
        private readonly Queue<string> logLines = new Queue<string>();
        private readonly List<UnitId> targetBuffer = new List<UnitId>(8);

        private BattleSession session;
        private long nextCommandId = 1;
        private bool busy;
        private bool leavingScene;
        private Coroutine activePresentation;
        private TurnBasedExpressionPlayer expressionPlayer;

        public BattleSession Session => session;
        public bool IsBusy => busy;

        private void Awake()
        {
            EnsureEventSystem();

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(ExitToLogin);
            }

            if (skillButtonTemplate != null)
            {
                skillButtonTemplate.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 正式流程复用常驻 UI 根的 EventSystem；直接 Play Battle 时再补建。
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null ||
                FindObjectOfType<EventSystem>(true) != null)
            {
                return;
            }

            new GameObject(
                "[EventSystem]",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
        }

        private void Start()
        {
            if (initializeOnStart)
            {
                StartBattle();
            }
        }

        public void StartBattle()
        {
            StopBattle();
            if (definition == null)
            {
                ShowFatalError("Battle 场景缺少回合制战斗定义。");
                return;
            }

            try
            {
                TurnBasedBattleRuntimeBuild build = definition.Compile();
                session = new BattleApplication(build.Database).CreateSession(
                    build.Units,
                    definition.RandomSeed);
                expressionPlayer = new TurnBasedExpressionPlayer(this, unitViews);
                nextCommandId = 1;
                BindViews();
                ResetHud();
                ExecuteCommand(
                    new StartBattleCommand(nextCommandId++),
                    false);
            }
            catch (Exception exception)
            {
                ShowFatalError("创建战斗失败：" + exception.Message);
                Debug.LogException(exception, this);
            }
        }

        public void StopBattle()
        {
            if (activePresentation != null)
            {
                StopCoroutine(activePresentation);
                activePresentation = null;
            }

            for (int index = 0; index < unitViews.Length; index++)
            {
                unitViews[index]?.ReleaseExpressionPresentation();
            }

            session?.Dispose();
            session = null;
            busy = false;
            nextCommandId = 1;
            views.Clear();
            ClearSkillButtons();
            expressionPlayer = null;
        }

        private void BindViews()
        {
            views.Clear();
            for (int index = 0; index < unitViews.Length; index++)
            {
                TurnBasedUnitView view = unitViews[index];
                if (view == null)
                {
                    continue;
                }

                BattleUnitAuthoring unitDefinition =
                    definition.FindUnit(view.UnitId);
                if (unitDefinition == null ||
                    !session.Context.World.TryGetUnit(
                        new UnitId(view.UnitId),
                        out BattleUnit unit))
                {
                    throw new InvalidOperationException(
                        $"场景单位表现 {view.name} 没有对应的单位配置。");
                }

                view.Bind(unit, unitDefinition);
                views.Add(view.UnitId, view);
            }

            IReadOnlyList<UnitId> unitIds = session.Context.World.UnitIds;
            if (views.Count != unitIds.Count)
            {
                throw new InvalidOperationException(
                    $"场景单位表现数量不完整：配置 {unitIds.Count}，场景 {views.Count}。" +
                    "请检查 Battle 场景中的 unitViews 与战斗定义是否一致。");
            }
        }

        private void ResetHud()
        {
            leavingScene = false;
            logLines.Clear();
            if (titleText != null)
            {
                titleText.text = definition.DisplayName;
            }

            if (roundText != null)
            {
                roundText.text = "准备战斗";
            }

            if (statusText != null)
            {
                statusText.text = "正在进入战斗…";
            }

            if (battleLogText != null)
            {
                battleLogText.text = string.Empty;
            }

            SetResultVisible(false, string.Empty);
            RefreshAllViews();
        }

        private void ExecuteCommand(
            IBattleCommand command,
            bool endTurnAfterPresentation)
        {
            if (session == null || busy || leavingScene)
            {
                return;
            }

            busy = true;
            SetSkillButtonsInteractable(false);
            BattleStepResult result = session.Step(command);
            activePresentation = StartCoroutine(
                PresentStep(result, endTurnAfterPresentation));
        }

        private IEnumerator PresentStep(
            BattleStepResult result,
            bool endTurnAfterPresentation)
        {
            if (!result.Success)
            {
                activePresentation = null;
                busy = false;
                ShowFatalError(
                    $"战斗指令失败：{result.AbortReason} / {result.Error}");
                yield break;
            }

            var expressionHitTargets = new HashSet<int>();
            var expressionDeathTargets = new HashSet<int>();
            for (int index = 0; index < result.Events.Count; index++)
            {
                BattleEventBase battleEvent = result.Events[index];
                if (battleEvent is SkillCastEvent cast)
                {
                    yield return PresentSkillCast(
                        cast,
                        result.Events,
                        expressionHitTargets,
                        expressionDeathTargets);
                    continue;
                }

                yield return PresentEvent(
                    battleEvent,
                    expressionHitTargets.Contains(battleEvent.Target.Value),
                    expressionDeathTargets.Contains(battleEvent.Target.Value));
            }

            RefreshAllViews();
            activePresentation = null;

            if (session == null || leavingScene)
            {
                busy = false;
                yield break;
            }

            if (session.IsFinished)
            {
                busy = false;
                ShowBattleResult(session.Context.Flow.Result);
                yield break;
            }

            if (endTurnAfterPresentation)
            {
                yield return WaitUnscaled(
                    definition.Presentation.commandGapDuration);
                UnitId actor = session.Context.Flow.CurrentActor;
                busy = false;
                ExecuteCommand(
                    new EndTurnCommand(nextCommandId++, actor),
                    false);
                yield break;
            }

            busy = false;
            ContinueCurrentTurn();
        }

        private IEnumerator PresentSkillCast(
            SkillCastEvent cast,
            IReadOnlyList<BattleEventBase> stepEvents,
            HashSet<int> handledHitTargets,
            HashSet<int> handledDeathTargets)
        {
            BattleSkillAuthoring skill = definition.FindSkill(cast.Skill.Value);
            BattleUnitAuthoring casterDefinition =
                definition.FindUnit(cast.Caster.Value);
            AppendLog(
                $"{GetUnitName(casterDefinition, cast.Caster)} 使用 " +
                (skill?.displayName ?? $"技能 {cast.Skill.Value}"));

            if (!views.TryGetValue(cast.Caster.Value, out TurnBasedUnitView casterView))
            {
                yield break;
            }

            TurnBasedUnitView targetView = null;
            UnitId targetId = UnitId.None;
            if (cast.SelectedTargets.Length > 0)
            {
                targetId = cast.SelectedTargets[0];
                views.TryGetValue(targetId.Value, out targetView);
            }

            DamageResolvedEvent damage = FindDamageEvent(stepEvents, cast, targetId);
            if (session.Context.Database.TryGetSkill(cast.Skill, out CompiledSkill compiledSkill) &&
                session.Context.Database.TryGetExpression(
                    compiledSkill.ExpressionKey,
                    out CompiledBattleExpression expression) &&
                expressionPlayer != null)
            {
                MarkExpressionHandledTargets(
                    expression,
                    targetId,
                    damage,
                    handledHitTargets,
                    handledDeathTargets);
                yield return expressionPlayer.Play(
                    expression,
                    casterView,
                    targetView,
                    damage);
                yield break;
            }

            yield return casterView.PlaySkill(
                targetView,
                skill?.animationName,
                skill?.presentationDuration ?? 0.6f);
        }

        private IEnumerator PresentEvent(
            BattleEventBase battleEvent,
            bool expressionHandledHit,
            bool expressionHandledDeath)
        {
            BattlePresentationAuthoring presentation = definition.Presentation;
            switch (battleEvent)
            {
                case BattleStartedEvent _:
                    AppendLog("战斗开始");
                    if (statusText != null)
                    {
                        statusText.text = "战斗开始";
                    }
                    yield return WaitUnscaled(presentation.battleStartDuration);
                    break;

                case RoundStartedEvent round:
                    if (roundText != null)
                    {
                        roundText.text = $"第 {round.Round} 回合";
                    }
                    AppendLog($"— 第 {round.Round} 回合 —");
                    break;

                case TurnStartedEvent turn:
                    SetCurrentActor(turn.Actor);
                    BattleUnitAuthoring actor =
                        definition.FindUnit(turn.Actor.Value);
                    AppendLog($"{GetUnitName(actor, turn.Actor)} 的行动");
                    yield return WaitUnscaled(presentation.turnBannerDuration);
                    break;

                case DamageResolvedEvent damage:
                    if (damage.Miss)
                    {
                        AppendLog($"{GetUnitName(damage.Target)} 闪避了攻击");
                    }
                    else
                    {
                        AppendLog(
                            $"{GetUnitName(damage.Target)} 受到 {damage.Damage} 伤害" +
                            (damage.Critical ? "（暴击）" : string.Empty));
                    }

                    if (!expressionHandledHit && views.TryGetValue(
                            damage.Target.Value,
                            out TurnBasedUnitView damagedView))
                    {
                        yield return damagedView.PlayHit(
                            damage.Damage,
                            damage.Critical,
                            damage.Miss,
                            presentation.hitDuration);
                    }
                    break;

                case HpChangedEvent hpChanged:
                    if (views.TryGetValue(
                            hpChanged.Target.Value,
                            out TurnBasedUnitView healthView))
                    {
                        healthView.Refresh();
                    }
                    break;

                case HealResolvedEvent heal:
                    AppendLog(
                        $"{GetUnitName(heal.Target)} 恢复 {heal.Healed} 生命");
                    break;

                case BuffChangedEvent buff:
                    AppendLog(
                        $"{GetUnitName(buff.Target)} " +
                        (buff.Added ? "获得" : "移除") +
                        $" Buff {buff.Buff.Value}");
                    break;

                case UnitLifeEvent life when !life.Revived:
                    AppendLog($"{GetUnitName(life.Target)} 被击败");
                    if (!expressionHandledDeath && views.TryGetValue(
                            life.Target.Value,
                            out TurnBasedUnitView deadView))
                    {
                        yield return deadView.PlayDeath(
                            presentation.deathDuration);
                    }
                    break;

                case UnitLifeEvent life:
                    AppendLog($"{GetUnitName(life.Target)} 复活");
                    break;

                case TurnEndedEvent turnEnded:
                    AppendLog($"{GetUnitName(turnEnded.Actor)} 行动结束");
                    break;

                case BattleEndedEvent ended:
                    AppendLog("战斗结束：" + GetResultText(ended.Result));
                    break;
            }
        }

        private static DamageResolvedEvent FindDamageEvent(
            IReadOnlyList<BattleEventBase> events,
            SkillCastEvent cast,
            UnitId preferredTarget)
        {
            for (int index = 0; index < events.Count; index++)
            {
                if (events[index] is DamageResolvedEvent damage &&
                    damage.Source == cast.Caster &&
                    (!preferredTarget.IsValid || damage.Target == preferredTarget))
                {
                    return damage;
                }
            }
            return null;
        }

        private static void MarkExpressionHandledTargets(
            CompiledBattleExpression expression,
            UnitId target,
            DamageResolvedEvent damage,
            HashSet<int> hitTargets,
            HashSet<int> deathTargets)
        {
            if (!target.IsValid)
            {
                return;
            }

            for (int index = 0; index < expression.Clips.Length; index++)
            {
                CompiledBattleExpressionClip clip = expression.Clips[index];
                if (clip.Subject != BattleExpressionSubject.PrimaryTarget)
                {
                    continue;
                }

                if (clip.Type == BattleExpressionClipType.Hit && damage != null)
                {
                    hitTargets.Add(target.Value);
                }
                else if (clip.Type == BattleExpressionClipType.CheckDead &&
                    damage != null && damage.TargetDead)
                {
                    deathTargets.Add(target.Value);
                }
            }
        }

        private void ContinueCurrentTurn()
        {
            if (session == null || session.IsFinished)
            {
                return;
            }

            UnitId actorId = session.Context.Flow.CurrentActor;
            BattleUnitAuthoring actor = definition.FindUnit(actorId.Value);
            if (actor == null)
            {
                ShowFatalError($"找不到当前行动单位配置：{actorId}");
                return;
            }

            SetCurrentActor(actorId);
            if (actor.control == BattleUnitControl.Player)
            {
                if (statusText != null)
                {
                    statusText.text = $"请选择 {actor.displayName} 的技能";
                }
                BuildSkillButtons(actor);
                return;
            }

            if (statusText != null)
            {
                statusText.text = $"{actor.displayName} 正在思考…";
            }
            busy = true;
            activePresentation = StartCoroutine(AutoAct(actor));
        }

        private IEnumerator AutoAct(BattleUnitAuthoring actor)
        {
            yield return WaitUnscaled(definition.Presentation.aiThinkingDuration);
            activePresentation = null;
            if (session == null || session.IsFinished || leavingScene)
            {
                busy = false;
                yield break;
            }

            BattleSkillAuthoring selectedSkill = null;
            for (int index = 0; index < actor.skillIds.Count; index++)
            {
                BattleSkillAuthoring candidate =
                    definition.FindSkill(actor.skillIds[index]);
                BattleUnit unit = session.Context.World.GetUnit(
                    new UnitId(actor.unitId));
                if (candidate != null && candidate.activeCommand &&
                    unit.Skills.TryGet(
                        new SkillId(candidate.id),
                        out SkillInstance instance) &&
                    !instance.Disabled && instance.Cooldown <= 0 &&
                    unit.Attributes.Get(candidate.costAttribute) >= candidate.cost)
                {
                    selectedSkill = candidate;
                    break;
                }
            }

            busy = false;
            if (selectedSkill == null ||
                !TryBuildTargets(actor, selectedSkill, out UnitId[] targets))
            {
                ExecuteCommand(
                    new EndTurnCommand(
                        nextCommandId++,
                        new UnitId(actor.unitId)),
                    false);
                yield break;
            }

            UseSkill(actor, selectedSkill, targets);
        }

        private void BuildSkillButtons(BattleUnitAuthoring actor)
        {
            ClearSkillButtons();
            if (skillButtonTemplate == null || skillButtonRoot == null)
            {
                ShowFatalError("Battle HUD 缺少技能按钮模板。");
                return;
            }

            for (int index = 0; index < actor.skillIds.Count; index++)
            {
                BattleSkillAuthoring skill =
                    definition.FindSkill(actor.skillIds[index]);
                if (skill == null)
                {
                    continue;
                }

                if (!skill.activeCommand)
                {
                    continue;
                }

                Button button = Instantiate(skillButtonTemplate, skillButtonRoot);
                button.name = $"Skill_{skill.id}";
                button.gameObject.SetActive(true);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = skill.displayName;
                }

                Image image = button.targetGraphic as Image;
                if (image != null)
                {
                    image.color = skill.buttonColor;
                }

                int skillId = skill.id;
                button.onClick.AddListener(() => OnSkillClicked(skillId));
                skillButtons.Add(button);
            }

            SetSkillButtonsInteractable(true);
        }

        private void OnSkillClicked(int skillId)
        {
            if (busy || session == null || session.IsFinished)
            {
                return;
            }

            UnitId actorId = session.Context.Flow.CurrentActor;
            BattleUnitAuthoring actor = definition.FindUnit(actorId.Value);
            BattleSkillAuthoring skill = definition.FindSkill(skillId);
            if (actor == null || skill == null ||
                !skill.activeCommand ||
                actor.control != BattleUnitControl.Player)
            {
                return;
            }

            if (!TryBuildTargets(actor, skill, out UnitId[] targets))
            {
                ShowFatalError($"技能 {skill.displayName} 没有可用目标。");
                return;
            }

            UseSkill(actor, skill, targets);
        }

        private void UseSkill(
            BattleUnitAuthoring actor,
            BattleSkillAuthoring skill,
            UnitId[] targets)
        {
            ExecuteCommand(
                new UseSkillCommand(
                    nextCommandId++,
                    new UnitId(actor.unitId),
                    new SkillId(skill.id),
                    targets),
                true);
        }

        private bool TryBuildTargets(
            BattleUnitAuthoring actor,
            BattleSkillAuthoring explicitSkill,
            out UnitId[] targets)
        {
            BattleSkillAuthoring skill = explicitSkill;
            if (skill == null && actor.skillIds.Count > 0)
            {
                for (int index = 0; index < actor.skillIds.Count; index++)
                {
                    BattleSkillAuthoring candidate =
                        definition.FindSkill(actor.skillIds[index]);
                    if (candidate != null && candidate.activeCommand)
                    {
                        skill = candidate;
                        break;
                    }
                }
            }

            targetBuffer.Clear();
            if (skill == null)
            {
                targets = Array.Empty<UnitId>();
                return false;
            }

            BattleWorld world = session.Context.World;
            UnitId actorId = new UnitId(actor.unitId);
            if (skill.targetMode == BattleSkillTargetMode.Self)
            {
                targetBuffer.Add(actorId);
            }
            else
            {
                IReadOnlyList<UnitId> ids = world.UnitIds;
                UnitId lowestHpAlly = UnitId.None;
                long lowestHp = long.MaxValue;
                for (int index = 0; index < ids.Count; index++)
                {
                    BattleUnit candidate = world.GetUnit(ids[index]);
                    if (candidate.IsDead)
                    {
                        continue;
                    }

                    bool ally = candidate.Camp == actor.camp;
                    if (skill.targetMode == BattleSkillTargetMode.LowestHpAlly)
                    {
                        if (ally && candidate.Attributes.Get(AttributeType.Hp) < lowestHp)
                        {
                            lowestHp = candidate.Attributes.Get(AttributeType.Hp);
                            lowestHpAlly = candidate.Id;
                        }
                        continue;
                    }

                    if (ally || candidate.Camp == BattleCamp.Neutral)
                    {
                        continue;
                    }

                    targetBuffer.Add(candidate.Id);
                    if (skill.targetMode == BattleSkillTargetMode.SingleEnemy)
                    {
                        break;
                    }
                }

                if (lowestHpAlly.IsValid)
                {
                    targetBuffer.Add(lowestHpAlly);
                }
            }

            targets = targetBuffer.ToArray();
            return targets.Length > 0;
        }

        private void SetCurrentActor(UnitId actor)
        {
            BattleUnitAuthoring definitionUnit = definition.FindUnit(actor.Value);
            Color accent = definitionUnit != null &&
                           definitionUnit.camp == BattleCamp.Defender
                ? definition.Presentation.defenderAccent
                : definition.Presentation.attackerAccent;
            foreach (KeyValuePair<int, TurnBasedUnitView> pair in views)
            {
                pair.Value.SetCurrentTurn(pair.Key == actor.Value, accent);
            }
        }

        private void RefreshAllViews()
        {
            foreach (KeyValuePair<int, TurnBasedUnitView> pair in views)
            {
                pair.Value.Refresh();
            }
        }

        private void ShowBattleResult(BattleResult result)
        {
            string message = GetResultText(result);
            if (statusText != null)
            {
                statusText.text = message;
            }
            SetResultVisible(true, message);

            BattleCamp winner = result == BattleResult.AttackerVictory
                ? BattleCamp.Attacker
                : result == BattleResult.DefenderVictory
                    ? BattleCamp.Defender
                    : BattleCamp.Neutral;
            if (winner != BattleCamp.Neutral)
            {
                foreach (KeyValuePair<int, TurnBasedUnitView> pair in views)
                {
                    if (pair.Value.Unit != null &&
                        pair.Value.Unit.Camp == winner &&
                        !pair.Value.Unit.IsDead)
                    {
                        pair.Value.PlayVictory();
                    }
                }
            }

            ClearSkillButtons();
        }

        private void ShowFatalError(string message)
        {
            busy = false;
            Debug.LogError("[TurnBasedBattle] " + message, this);
            if (statusText != null)
            {
                statusText.text = message;
            }
            AppendLog(message);
            SetResultVisible(true, "战斗中断\n" + message);
            ClearSkillButtons();
        }

        private void SetResultVisible(bool visible, string message)
        {
            if (resultPanel != null)
            {
                resultPanel.alpha = visible ? 1f : 0f;
                resultPanel.interactable = visible;
                resultPanel.blocksRaycasts = visible;
            }

            if (resultText != null)
            {
                resultText.text = message;
            }
        }

        private void AppendLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            logLines.Enqueue(line);
            while (logLines.Count > 8)
            {
                logLines.Dequeue();
            }

            if (battleLogText != null)
            {
                battleLogText.text = string.Join("\n", logLines);
            }
        }

        private string GetUnitName(UnitId id) =>
            GetUnitName(definition.FindUnit(id.Value), id);

        private static string GetUnitName(
            BattleUnitAuthoring unit,
            UnitId fallback)
        {
            return unit == null || string.IsNullOrWhiteSpace(unit.displayName)
                ? $"单位 {fallback.Value}"
                : unit.displayName;
        }

        private static string GetResultText(BattleResult result)
        {
            switch (result)
            {
                case BattleResult.AttackerVictory:
                    return "胜利";
                case BattleResult.DefenderVictory:
                    return "战败";
                case BattleResult.Draw:
                    return "平局";
                case BattleResult.Exited:
                    return "已退出战斗";
                case BattleResult.Aborted:
                    return "战斗异常中止";
                default:
                    return "战斗结束";
            }
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SetSkillButtonsInteractable(bool interactable)
        {
            for (int index = 0; index < skillButtons.Count; index++)
            {
                if (skillButtons[index] != null)
                {
                    skillButtons[index].interactable = interactable;
                }
            }
        }

        private void ClearSkillButtons()
        {
            for (int index = 0; index < skillButtons.Count; index++)
            {
                if (skillButtons[index] != null)
                {
                    Destroy(skillButtons[index].gameObject);
                }
            }
            skillButtons.Clear();
        }

        public void ExitToLogin()
        {
            if (leavingScene)
            {
                return;
            }

            leavingScene = true;
            busy = true;
            SetSkillButtonsInteractable(false);
            if (statusText != null)
            {
                statusText.text = "正在返回 Login…";
            }
            GameSceneManager.Instance.LoadSceneAsync("Login");
        }

        private void OnDestroy()
        {
            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(ExitToLogin);
            }
            StopBattle();
        }
    }
}
