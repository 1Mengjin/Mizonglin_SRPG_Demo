using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zilong.Forest;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
        public const string ForestPreviewMarker = "Forest rules v5.5 character passives";
        public ForestScenario forestScenario;
        public ForestSession ForestSession => forestSession;
        ForestSession forestSession;
        ActionKind? forestTargetAction;
        string forestTeleportTargetId;
        readonly List<Button> forestSkillButtons = new List<Button>();
        Text forestHint, forestForecast;
        RectTransform forestForecastPanel;
        bool ForestEnabled => forestScenario != null && !VerifyCombatMode;

        void InitializeForest(bool beginPhase)
        {
            ClearBuffPulses();
            ResetForestTutorials();
            forestMovementPreview = null;
            forestSession = forestScenario.CreateSession();
            if (beginPhase) ForestRules.BeginPlayerPhase(forestSession.State);
            State.BindForest(forestSession.State, forestSession.InitialDefinition);
        }
        void BeginForestCombat()
        {
            if (forestSession != null && forestSession.State.result == BattleResult.InProgress && forestSession.State.eventLog.Any(e => e.kind == "ActionEnd")) SaveForestRun("手动重开");
            lastRunReport = null; ledgerRevision = -1;
            if (ledgerPanel != null) ledgerPanel.gameObject.SetActive(false);
            forestMovementPreview = null;
            StopAllCoroutines(); combatEpoch++; busy = battleEnded = editing = false;
            ClearBuffPulses();
            ResetForestTutorials();
            HideStatusDetails();
            ClearAnnouncements(); ClearRewardPopup();
            forestTargetAction = null; forestTeleportTargetId = null; resultReason = null; hover = null; walkingPath.Clear();
            if (forestSession == null) forestSession = forestScenario.CreateSession(); else forestSession.Restart();
            State.BindForest(forestSession.State, forestSession.InitialDefinition);
            SyncMapVisuals(); ResetFigures(); ResetCamera(); Refresh(); StartCoroutine(BattleOpening());
        }
        void EnterForestPhase()
        {
            ForestRules.BeginPlayerPhase(forestSession.State); State.SyncForest(); Refresh();
        }
        ForestCommand ForestCommandFor(ActionKind kind, DemoUnit target = null, Vector2Int? cell = null)
        {
            var unit = State.selected;
            return new ForestCommand { actorId = unit?.id, targetId = kind == ActionKind.Echo ? forestTeleportTargetId ?? target?.id : target?.id, kind = kind,
                targetCell = cell ?? default,
                spiritId = kind == ActionKind.CarrySpirit || kind == ActionKind.SwallowSpirit ? ForestGroundSpirit()?.spiritId : null,
                path = unit == null ? null : State.Path(State.phase == SelectionPhase.Preview ? State.preview : unit.cell) };
        }
        bool ForestClickSkillTarget(Vector2Int cell)
        {
            if (!forestTargetAction.HasValue) return false;
            if (forestTargetAction == ActionKind.Echo && forestTeleportTargetId == null)
            {
                var target = ForestTargetAt(cell);
                var targetError = ForestRules.ValidateTeleportTarget(ForestDisplayState, State.selected?.id, target?.id);
                if (targetError != null) { Notify(targetError); return true; }
                forestTeleportTargetId = target.id; hover = null; Refresh();
                Notify("已选 " + target.name + "，请选择符生身边上下左右的可走空格；右键或 Esc 取消");
                return true;
            }
            var command = ForestCommandFor(forestTargetAction.Value, ForestTargetAt(cell), cell);
            var error = ForestRules.ValidateCommand(forestSession.State, command);
            if (error != null) { Notify(error); return true; }
            StartCoroutine(ForestPlayerAction(command)); return true;
        }
        void SelectForestSkill(ActionKind kind)
        {
            if (busy || State.selected == null || State.selected.acted && !ForestRules.IsFreeAction(kind)) return;
            if (kind == ActionKind.SwitchStance || kind == ActionKind.Sprint)
            {
                StartCoroutine(ForestFreeSkill(kind)); return;
            }
            if (kind == ActionKind.CarrySpirit || kind == ActionKind.SwallowSpirit || kind == ActionKind.Retreat && State.selected.forestUnit.dualForm.form == DualForm.Guard) { StartCoroutine(ForestPlayerAction(ForestCommandFor(kind))); return; }
            forestTeleportTargetId = null; forestTargetAction = kind; hover = null; Refresh();
            Notify(kind == ActionKind.Talisman ? "选择符箓中心 范围伤害并晕眩敌人 右键或 Esc 取消"
                : kind == ActionKind.ThrowSpirit ? "选择元神落点 射程4，落点不能有其他单位 右键或 Esc 取消"
                : kind == ActionKind.Retreat ? "选择退去的敌方目标" : kind == ActionKind.Echo ? "选择范围内要传送的队友，再指定符生身边的落点；右键或 Esc 取消" : "选择友方目标（可选自身） 右键或 Esc 取消");
        }

        IEnumerator ForestFreeSkill(ActionKind kind)
        {
            var command = new ForestCommand { actorId = State.selected.id, kind = kind };
            var error = ForestRules.ValidateCommand(forestSession.State, command);
            if (error != null) { Notify(error); yield break; }
            busy = true; forestTargetAction = null; forestTeleportTargetId = null; hover = null; Refresh();
            yield return CheckForestTutorials(command);
            yield return ShowSkillAnnouncement(command);
            bool undoPoint = forestSession.RecordUndoPoint(command.actorId);
            if (!ForestRules.TryExecute(forestSession.State, command, out error))
            { if (undoPoint) forestSession.DiscardUndoPoint(); busy = false; Notify(error); Refresh(); yield break; }
            // Free skills retain both the selected unit and uncommitted movement preview.
            State.SyncForest();
            if (State.phase != SelectionPhase.Preview) State.Select(State.selected);
            busy = false; Refresh();
            Notify(kind == ActionKind.Sprint ? "疾行已生效 本回合增加一次行动" : "已安排下回合切换姿态 不消耗行动次数");
        }

        IEnumerator ForestPlayerAction(ForestCommand command)
        {
            if (busy) yield break;
            string error = ForestRules.ValidateCommand(forestSession.State, command);
            if (error != null) { Notify(error); yield break; }
            busy = true; Refresh();
            yield return CheckForestTutorials(command);
            while (walkingIndex < walkingPath.Count) yield return null;
            yield return ShowSkillAnnouncement(command);
            yield return AnimateActionLeadIn(command);
            int eventStart = forestSession.State.eventLog.Count;
            bool undoPoint = forestSession.RecordUndoPoint(command.actorId);
            if (!ForestRules.TryExecute(forestSession.State, command, out error)) { if (undoPoint) forestSession.DiscardUndoPoint(); busy = false; Notify(error); Refresh(); yield break; }
            RefreshRewardMarkers();
            yield return ShowCollectedReward(eventStart);
            yield return PlayForestImpacts(eventStart);
            State.Clear(); forestTargetAction = null; forestTeleportTargetId = null; hover = null; walkingPath.Clear(); walkingIndex = 0;
            SyncForestPresentation();
            yield return CheckForestTutorials();
            if (ForestFinishIfEnded()) yield break;
            if (ForestRules.PlayerPhaseComplete(forestSession.State)) { yield return ForestRound(); yield break; }
            if (ForestRules.IsSpiritAction(command.kind))
            {
                State.Select(State.units.FirstOrDefault(u => u.id == command.actorId));
                busy = false; Refresh();
                Notify("元神操作完成 不消耗行动次数" + (State.selected != null && !State.selected.acted ? "，仍可进行主要行动" : ""));
                yield break;
            }
            busy = false; Refresh();
        }
        IEnumerator ForestRound()
        {
            busy = true; State.Clear(); forestTargetAction = null; forestTeleportTargetId = null; hover = null; walkingPath.Clear();
            int skippedActionStart = forestSession.State.eventLog.Count;
            ForestRules.EndPlayerPhase(forestSession.State);
            yield return PlayForestImpacts(skippedActionStart); SyncForestPresentation();
            if (ForestFinishIfEnded()) yield break;
            yield return ShowTurnTransition(true, forestSession.State.round);
            foreach (var enemy in forestSession.State.unitsById.Values.Where(u => ForestRules.Alive(u)
                && forestSession.State.GetTemplate(u.templateId).faction == Faction.Enemy).OrderBy(u => u.instanceId, StringComparer.Ordinal).ToArray())
            {
                if (!ForestRules.Alive(enemy)) continue;
                int start = forestSession.State.eventLog.Count;
                var command = ForestRules.ChooseEnemyAction(forestSession.State, enemy.instanceId);
                if (command == null) continue;
                yield return AnimateEnemyTravel(command);
                yield return ShowSkillAnnouncement(command);
                yield return AnimateActionLeadIn(command);
                if (!ForestRules.TryExecute(forestSession.State, command, out var error))
                { figurePositionOverrides.Clear(); Debug.LogError("敌方行动失败：" + enemy.instanceId + " " + error); busy = false; Refresh(); yield break; }
                yield return PlayForestImpacts(start);
                SyncForestPresentation(); figurePositionOverrides.Clear();
                if (ForestFinishIfEnded()) yield break;
            }
            int roundEndStart = forestSession.State.eventLog.Count;
            yield return ShowRoundEndEnemySkills();
            ForestRules.EndEnemyPhase(forestSession.State);
            yield return PlayForestImpacts(roundEndStart); SyncForestPresentation();
            phaseBanner.text = "";
            if (ForestFinishIfEnded()) yield break;
            yield return CheckForestTutorials();
            yield return ShowTurnTransition(false, forestSession.State.round);
            EnterForestPhase(); busy = false; Refresh();
        }
        bool ForestFinishIfEnded()
        {
            var battle = forestSession.State;
            if (battle.result == BattleResult.InProgress) return false;
            bool won = battle.result == BattleResult.Victory;
            SaveForestRun(won ? "战斗胜利" : "战斗失败");
            FinishBattle(won, won ? "当前场上的瘴气已全部清除"
                : battle.endReason == BattleEndReason.Timeout ? "第 " + battle.Rules.roundLimit + " 回合结束，未完成除源清雾"
                : "我方单位全部阵亡"); return true;
        }
        void SyncForestPresentation()
        {
            State.SyncForest(); RemoveGoneFigures();
            SyncMapVisuals(); Refresh();
        }
        void RefreshForestHud()
        {
            var battle = ForestDisplayState;
            if (forestHint != null) forestHint.text = ForestPhaseCaption;
            var viewUnit = State.selected; if (viewUnit?.forestUnit == null) return;
            var unit = battle.unitsById[viewUnit.id]; var template = battle.GetTemplate(unit.templateId); var effective = ForestRules.Stats(battle, unit);
            selectedHealth.text = unit.currentHp + " / " + effective.maxHp;
            healthFill.rectTransform.sizeDelta = new Vector2(354 * Mathf.Clamp01(unit.currentHp / (float)effective.maxHp), 30);
            selectedStats.text = $"攻击 {effective.attack}    防御 {effective.defense}\n移动 {effective.move}    射程 {(template.hasBasicAttack ? effective.range.ToString() : "无")}";
            // The effects bar contains committed effects only, not changes predicted by movement preview.
            RefreshStatusBar(forestSession.State, forestSession.State.unitsById[viewUnit.id]);
            RefreshSpiritButtons(battle, unit, viewUnit.enemy);
            RefreshSkillSlots(battle, unit, template);
        }
        void PaintForestTargeting()
        {
            if (forestHint != null) forestHint.text = ForestPhaseCaption;
            SetForestForecast("");
            PaintSelectedThreat();
            PaintMaskWarnings();
            if (State.selected == null || forestSession.State.phase != BattlePhase.Player) return;
            if (forestTargetAction == ActionKind.Echo && forestTeleportTargetId == null)
            {
                foreach (var unit in State.units)
                    if (ForestRules.ValidateTeleportTarget(ForestDisplayState, State.selected.id, unit.id) == null && tiles.TryGetValue(unit.cell, out var tile))
                        tile.sharedMaterial.color = jade;
                SetForestForecast("符箓·引：先选择范围内的另一名队友，再选择身边四邻空格");
                return;
            }
            if (forestTargetAction.HasValue)
            {
                foreach (var pair in tiles)
                {
                    var command = ForestCommandFor(forestTargetAction.Value, ForestTargetAt(pair.Key), pair.Key);
                    if (ForestRules.ValidateFromMovementPreview(ForestDisplayState, command) == null) pair.Value.sharedMaterial.color = new Color(.43f, .72f, .53f);
                }
            }
            if (!hover.HasValue) return;
            ActionKind kind = forestTargetAction ?? ActionKind.BasicAttack;
            var trial = ForestRules.Preview(forestSession.State, ForestCommandFor(kind, ForestTargetAt(hover.Value), hover), out _);
            if (trial == null) return;
            var events = trial.eventLog.Skip(forestSession.State.eventLog.Count).ToArray();
            if (kind == ActionKind.ThrowSpirit)
            {
                foreach (var p in ForestDisplayState.groundFog.Where(p => !trial.groundFog.Contains(p))) tiles[p].sharedMaterial.color = gold;
                SetForestForecast("元神投掷预览 实际清雾 " + events.Single(e => e.kind == "ThrowSpirit").amount + " 格 不消耗行动次数");
            }
            else
            {
                if (kind == ActionKind.Talisman)
                    foreach (var pair in tiles)
                        if (Math.Abs(pair.Key.x - hover.Value.x) <= 1 && Math.Abs(pair.Key.y - hover.Value.y) <= 1) pair.Value.sharedMaterial.color = gold;
                SetForestForecast(ForestFeedback.PreviewSummary(forestSession.State, trial));
            }
        }

        void UndoForestStep()
        {
            if (!ForestEnabled || buildingPreview || busy || battleEnded || editing || forestSession == null) return;
            if (forestSession.State.phase != BattlePhase.Player || forestSession.UndoCount == 0) return;
            forestMovementPreview = null; forestTargetAction = null; forestTeleportTargetId = null;
            State.Clear(); walkingPath.Clear(); walkingIndex = 0; hover = null; rightHeld = rightDragging = false;
            HideStatusDetails(); HideSkillDescription(); ClearRewardPopup(); ClearAnnouncements(); ClearBuffPulses();
            if (!forestSession.TryUndo(out string actorId, out int remaining)) { Notify("没有可回退的步骤"); Refresh(); return; }
            State.SyncForest();
            ResetFigures();
            SyncMapVisuals();
            // Re-select the restored character so the player can immediately retry the same decision.
            var view = actorId == null ? null : State.units.FirstOrDefault(u => u.id == actorId);
            if (view != null) { State.Select(view); FocusCamera(view.cell); }
            Refresh();
            RefreshForestLedger(true);
            Notify("已撤回" + (actorId == null ? "上一步" : "该角色本回合的全部操作") + "，仍可回退 " + remaining + " 步");
        }
    }
}
