using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zilong.Forest;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
        RectTransform ledgerPanel;
        Text ledgerText, runClock;
        Button ledgerButton, undoButton;
        int ledgerTab, strategyIndex, displayedSecond = -1;
        long ledgerRevision = -1;
        readonly string[] strategyNames = { "自由测试", "集中突进", "分兵除源" };
        string lastRunReport;
        void MakeForestFeedbackHud()
        {
            ledgerButton = Action(canvas.transform, "战斗记录", new Vector2(1, 1), new Vector2(-28, -94), new Vector2(130, 48), ink, ToggleForestLedger);
            undoButton = Action(canvas.transform, "悔棋", new Vector2(1, 1), new Vector2(-28, -148), new Vector2(130, 48), ink, UndoForestStep);
            runClock = Label(canvas.transform, "Run clock", "本局用时 00:00", new Vector2(1, 1), new Vector2(-28, -202), new Vector2(220, 34), 18, TextAnchor.MiddleRight);
        }
        void TickForestFeedback()
        {
            if (forestSession == null || buildingPreview) return;
            if (Application.isFocused && !editing)
            {
                double delta = Time.unscaledDeltaTime;
                ForestFeedback.Tick(forestSession.State, delta);
                // busy covers skill cut-ins, turn seals, impact settlements and the whole enemy phase.
                var phase = forestSession.State.phase;
                ForestFeedback.TickBlocking(forestSession.State, delta, busy || tutorialOverlay != null,
                    phase == BattlePhase.Enemy || phase == BattlePhase.RoundEnd);
            }
            int second = (int)forestSession.State.statistics.elapsedSeconds;
            if (runClock != null && displayedSecond != second)
            {
                displayedSecond = second; runClock.text = "本局用时 " + ForestFeedback.Clock(second);
                if (ledgerPanel != null && ledgerPanel.gameObject.activeSelf) RefreshForestLedger(true);
            }
            if (ledgerButton != null) ledgerButton.interactable = !busy || battleEnded;
            if (undoButton != null)
                undoButton.interactable = !busy && !battleEnded && !editing && forestSession != null
                    && forestSession.State.phase == BattlePhase.Player && forestSession.UndoCount > 0;
        }
        void ToggleForestLedger()
        {
            if (ledgerPanel == null)
            {
                ledgerPanel = Box(canvas.transform, "Battle ledger", new Vector2(.5f, .5f), Vector2.zero, new Vector2(820, 680), ink);
                ledgerPanel.GetComponent<Image>().raycastTarget = true;
                Label(ledgerPanel, "Title", "战斗记录", new Vector2(0, 1), new Vector2(24, -16), new Vector2(400, 36), 27, TextAnchor.MiddleLeft);
                Action(ledgerPanel, "关闭", new Vector2(1, 1), new Vector2(-16, -14), new Vector2(100, 40), ink, () => ledgerPanel.gameObject.SetActive(false));
                var tabs = new[] { "本回合", "污染与资源", "本局统计" };
                for (int i = 0; i < tabs.Length; i++)
                {
                    int index = i;
                    Action(ledgerPanel, tabs[i], new Vector2(0, 1), new Vector2(24 + 190 * i, -68), new Vector2(175, 44), new Color(.18f, .30f, .30f), () => { ledgerTab = index; RefreshForestLedger(true); });
                }
                var viewport = Box(ledgerPanel, "Ledger viewport", new Vector2(0, 1), new Vector2(24, -128), new Vector2(772, 460), new Color(.10f, .15f, .16f));
                viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
                ledgerText = Label(viewport, "Ledger entries", "", new Vector2(0, 1), new Vector2(8, -8), new Vector2(746, 440), 17, TextAnchor.UpperLeft);
                ledgerText.verticalOverflow = VerticalWrapMode.Overflow;
                var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = ledgerText.rectTransform;
                scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
                Button strategy = null;
                strategy = Action(ledgerPanel, "策略 自由测试", new Vector2(0, 0), new Vector2(24, 22), new Vector2(260, 48), new Color(.18f, .30f, .30f), () =>
                { strategyIndex = (strategyIndex + 1) % strategyNames.Length; strategy.GetComponentInChildren<Text>().text = "策略 " + strategyNames[strategyIndex]; RefreshForestLedger(true); });
                Action(ledgerPanel, "导出战报", new Vector2(1, 0), new Vector2(-24, 22), new Vector2(180, 48), new Color(.18f, .36f, .29f), () => SaveForestRun("手动导出"));
            }
            else ledgerPanel.gameObject.SetActive(!ledgerPanel.gameObject.activeSelf);
            RefreshForestLedger(true);
        }
        void ShowForestLedger()
        {
            if (ledgerPanel == null || !ledgerPanel.gameObject.activeSelf) ToggleForestLedger();
            ledgerPanel.SetAsLastSibling(); RefreshForestLedger(true);
        }
        void RefreshForestLedger(bool force = false)
        {
            if (ledgerPanel == null || !ledgerPanel.gameObject.activeSelf || forestSession == null) return;
            var s = forestSession.State;
            if (!force && ledgerRevision == s.revision) return;
            ledgerRevision = s.revision;
            var events = s.eventLog.AsEnumerable();
            if (ledgerTab == 0) events = events.Where(e => e.round == s.round);
            if (ledgerTab == 1) events = events.Where(e => e.kind.StartsWith("Fog", StringComparison.Ordinal) || e.kind.StartsWith("Spirit", StringComparison.Ordinal) || e.kind == "ThrowSpirit" || e.kind == "SwallowSpirit" || e.kind == "PhaseDefeated");
            string content = ledgerTab == 2 ? ForestFeedback.ResultSummary(s) + "\n\n时长构成\n" + ForestFeedback.DurationBreakdown(s) + "\n策略标记 " + strategyNames[strategyIndex] + "\n\n"
                + string.Join("\n\n", s.unitsById.Values.OrderBy(u => u.instanceId, StringComparer.Ordinal).Select(u => u.instanceId + " " + s.GetTemplate(u.templateId).name + "\n" + ForestFeedback.UnitSummary(s, u)))
                : "最近300笔，完整记录可导出\n" + string.Join("\n", events.Reverse().Take(300).Reverse().Select(ForestAudit.EventText));
            ledgerText.text = ForestAudit.Summary(s) + "\n\n" + content;
            Canvas.ForceUpdateCanvases(); ledgerText.rectTransform.sizeDelta = new Vector2(746, Mathf.Max(440, ledgerText.preferredHeight + 24));
        }
        void SaveForestRun(string reason)
        {
            if (forestSession == null || buildingPreview) return;
            lastRunReport = null;
            try
            {
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/ForestRuns"));
                lastRunReport = ForestAudit.Export(forestSession.State, folder, strategyNames[strategyIndex], reason, forestSession.InitialDefinition, forestSession.InitialRandomSeed);
                Notify("战报已保存到 Artifacts/ForestRuns");
            }
            catch (Exception ex) { Debug.LogError("战报保存失败：" + ex.Message); Notify("战报未能保存，请在战斗记录中重试导出"); }
        }
        string ForestFeedbackSummary() => ForestFeedback.ResultSummary(forestSession.State) + "\n策略 " + strategyNames[strategyIndex]
            + (lastRunReport == null ? "" : "\n战报已保存，可在战斗记录中导出完整事件");
        string ForestVictorySummary()
        {
            var battle = forestSession.State;
            int actions = battle.unitsById.Values.Where(u => battle.GetTemplate(u.templateId).faction == Faction.Ally).Sum(u => u.statistics.actions);
            int consumed = battle.spiritsById.Values.Count(s => s.locationKind == SpiritLocation.Consumed);
            return $"第{battle.round}回合 用时{ForestFeedback.Clock(battle.statistics.elapsedSeconds)}"
                + $"\n主要行动{actions}次 元神消耗{consumed}/{battle.spiritsById.Count}"
                + (lastRunReport == null ? "" : "\n战报已保存，可在战斗记录中导出完整事件");
        }
        void PaintSelectedThreat()
        {
            if (State.selected?.forestUnit == null || !State.selected.enemy) return;
            var unit = ForestDisplayState.unitsById[State.selected.id];
            foreach (var cell in ForestFeedback.ThreatCells(ForestDisplayState, unit))
                if (tiles.TryGetValue(cell, out var tile)) tile.sharedMaterial.color = unit.devourer == null ? new Color(.78f, .44f, .20f) : new Color(.20f, .57f, .72f);
            SetForestForecast(ForestFeedback.ThreatCaption(ForestDisplayState, unit));
        }
        void SetForestForecast(string value)
        {
            if (forestForecast == null) return;
            forestForecast.text = value;
            forestForecastPanel.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }
    }
}
