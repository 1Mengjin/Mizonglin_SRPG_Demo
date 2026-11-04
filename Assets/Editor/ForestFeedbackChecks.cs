using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zilong.Forest;

public static class ForestFeedbackChecks
{
    static int checks;
    static Vector2Int P(int x, int y) => new Vector2Int(x, y);
    static UnitState U(ForestBattleState s, string id) => s.unitsById[id];
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception("M5: " + label); }
    static ForestBattleState Fresh(int round = 1, bool cloud = false)
    {
        var d = ForestDefaults.Create();
        for (int i = 0; i < d.map.terrain.Length; i++) { d.map.terrain[i] = 0; d.map.regions[i] = 3; d.map.initialFog[i] = false; }
        d.map.initialFog[13 * d.map.width] = true;
        d.map.rootCells = new Vector2Int[0];
        d.spawns[0].cell = P(2, 2); d.spawns[1].cell = P(4, 2); d.spawns[2].cell = P(2, 4); d.spawns[3].cell = P(4, 4);
        if (cloud) { d.map.initialFog[6 * 18 + 15] = true; d.map.initialFog[6 * 18 + 16] = true; }
        // Fixed seed keeps the phase-change summons out of the hard-coded carry paths below.
        var s = new ForestSession(d).State; s.round = round; s.randomState = 42u; ForestRules.BeginPlayerPhase(s); return s;
    }
    static void Act(ForestBattleState s, ForestCommand c) => Check(ForestRules.TryExecute(s, c, out var error), error ?? c.kind.ToString());
    static void Next(ForestBattleState s) { ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s); ForestRules.BeginPlayerPhase(s); }
    public static string Run()
    {
        checks = 0;
        var s = Fresh(); Check(ForestAudit.Check(s).Count == 0, "initial audit balanced");
        U(s, "M04-1").cell = P(3, 2); var command = new ForestCommand { actorId = "C01", targetId = "M04-1", kind = ActionKind.BasicAttack };
        var preview = ForestRules.Preview(s, command, out _); var hit = preview.eventLog.Last(e => e.kind == "Attack");
        Check(hit.amount == 60 && hit.requestedAmount == 75, "overkill preview distinguishes actual and requested damage");
        Check(ForestFeedback.PreviewSummary(s, preview).Contains("-60") && U(s, "M04-1").currentHp == 60, "preview text matches real HP delta without source mutation");
        Act(s, command); Check(U(s, "C01").statistics.damageDealt == 60 && s.Copy().eventLog.Last(e => e.kind == "Attack").requestedAmount == 75, "actual damage statistics and copied events agree");
        s = Fresh(); U(s, "M04-1").cell = P(8, 6);
        var threat = ForestFeedback.ThreatCells(s, U(s, "M04-1"));
        Check(threat.Contains(P(10, 7)) && !threat.Contains(P(12, 6)), "corpse threat uses move plus attack range");
        s.Map.rootCells = new[] { P(8, 5), P(9, 6), P(8, 7), P(7, 6) }; s.Rules.rootEntryCost = 3;
        Check(!ForestFeedback.ThreatCells(s, U(s, "M04-1")).Contains(P(10, 6)), "threat obeys root budget");
        var boss = U(s, "M03"); boss.cell = P(5, 5); boss.maskBoss.releaseRound = 2; boss.maskBoss.warningCenter = P(7, 5);
        Check(ForestFeedback.ThreatCells(s, boss).SetEquals(ForestRules.MaskWarningCells(s, boss)), "locked warning and threat share cells");
        Check(ForestFeedback.UnitSummary(s, boss).Contains("第2回合释放60伤害"), "boss phase/release readable");
        boss.stunnedUntilRound = s.round; Check(ForestFeedback.ThreatCaption(s, boss).Contains("晕眩"), "stun prevents misleading threat timing");
        Check(ForestFeedback.UnitSummary(s, U(s, "M01A")).Contains("果级 1/3") && ForestFeedback.UnitSummary(s, U(s, "M02-1")).Contains("下个巡逻点"), "fruit and patrol summaries readable");
        U(s, "C03").bonusActions = 1; Check(ForestFeedback.UnitSummary(s, U(s, "C03")).Contains("主要行动余 2"), "extra actions shown accurately");
        ForestRules.Ability(U(s, "C03"), "sprint").availableRound = 3; Check(ForestFeedback.UnitSummary(s, U(s, "C03")).Contains("疾行CD2"), "cooldown uses same available round");
        U(s, "C04").lifeState = LifeState.Dead; U(s, "C04").currentHp = 0;
        Check(ForestFeedback.UnitSummary(s, U(s, "C04")).StartsWith("已阵亡", StringComparison.Ordinal), "dead records do not advertise usable actions");

        s = Fresh(cloud: true); Next(s);
        Check(s.groundFog.Count == 1 && s.CarriedFogCount == 2 && ForestAudit.Check(s).Count == 0, "absorption has matching transfer ledger");
        U(s, "C01").cell = P(12, 3); U(s, "M03").currentHp = 1;
        Act(s, new ForestCommand { actorId = "C01", targetId = "M03", kind = ActionKind.BasicAttack });
        Check(ForestAudit.Check(s).Count == 0 && s.spiritsById.Count == 1, "one resource per resolved phase audited"); Next(s);
        string spirit = s.spiritsById.Keys.Single();
        Act(s, new ForestCommand { actorId = "C01", kind = ActionKind.CarrySpirit, spiritId = spirit, path = new[] { P(12, 3), P(13, 3), P(13, 2), P(14, 2) }.ToList() });
        Check(ForestAudit.Check(s).Count == 0 && ForestAudit.Summary(s).Contains("携带1"), "carry ledger and summary consistent");
        Act(s, new ForestCommand { actorId = "C01", kind = ActionKind.SwallowSpirit }); Check(ForestAudit.Check(s).Count == 0, "consumption has one owner and no ground/carrier residue");
        Next(s); U(s, "M02-1").currentHp = 1;
        Act(s, new ForestCommand { actorId = "C01", targetId = "M02-1", kind = ActionKind.BasicAttack, path = new[] { P(14, 2), P(15, 2), P(15, 3) }.ToList() });
        Check(s.groundFog.Count == 3 && s.CarriedFogCount == 0 && ForestAudit.Check(s).Count == 0, "death spill transfers without generating or clearing"); Next(s);
        Act(s, new ForestCommand { actorId = "C01", kind = ActionKind.Wait, path = new[] { P(15, 3), P(15, 4), P(15, 5), P(15, 6) }.ToList() });
        Check(s.groundFog.Count == 1 && s.statistics.clearedFogCount == 2 && s.eventLog.Count(e => e.kind == "FogCleared") == 2 && ForestAudit.Check(s).Count == 0, "purification records each actual removed cell");
        var damaged = s.Copy(); damaged.statistics.generatedFogCount++;
        Check(ForestAudit.Check(damaged).Any(e => e.Contains("不守恒")) && damaged.statistics.generatedFogCount == 1, "audit reports corruption without repairing state");
        damaged = s.Copy(); damaged.spiritsById[spirit].locationKind = SpiritLocation.Pending;
        Check(ForestAudit.Check(damaged).Any(e => e.Contains("待掉落")), "pending mismatch detected");
        damaged = s.Copy(); damaged.spiritsById.Add("duplicate", new SpiritState { spiritId = "duplicate", sourceInstanceId = "M03", sourcePhase = 1, locationKind = SpiritLocation.Ground, cell = P(1, 1) });
        Check(ForestAudit.Check(damaged).Any(e => e.Contains("元神数量")), "duplicate phase resource detected");
        var timed = s.Copy(); ForestFeedback.Tick(timed, 75.5); ForestFeedback.Tick(timed, -1); ForestFeedback.Tick(timed, double.NaN); ForestFeedback.Tick(timed, double.PositiveInfinity);
        Check(ForestFeedback.Clock(timed.statistics.elapsedSeconds) == "01:15" && s.statistics.elapsedSeconds == 0, "clock is explicit, isolated and rejects invalid deltas");
        timed.result = BattleResult.Victory; ForestFeedback.Tick(timed, 20); Check(timed.statistics.elapsedSeconds == 75.5, "timer stops at finished result");
        var split = s.Copy(); ForestFeedback.Tick(split, 100);
        ForestFeedback.TickBlocking(split, 70, true, false); ForestFeedback.TickBlocking(split, 30, true, true);
        ForestFeedback.TickBlocking(split, 25, false, false); ForestFeedback.TickBlocking(split, -5, true, false);
        Check(split.statistics.elapsedSeconds == 100 && split.statistics.presentationSeconds == 100 && split.statistics.enemyPhaseSeconds == 30
            && ForestFeedback.DurationBreakdown(split).Contains("决策与操作 0.0s"), "blocking split sums to the focused clock and ignores non-blocking frames");
        var unblocked = s.Copy(); ForestFeedback.Tick(unblocked, 100); ForestFeedback.TickBlocking(unblocked, 40, true, false);
        Check(ForestFeedback.DurationBreakdown(unblocked).Contains("决策与操作 60.0s")
            && ForestFeedback.DurationBreakdown(unblocked).Contains("我方演出与结算 40.0s"), "duration breakdown separates player control from blocking presentation");
        string md = ForestAudit.Markdown(s, "逻辑检查样例", "未进行实际试玩");
        Check(md.Contains("主要行动") && md.Contains("元神记录") && md.Contains("除源回合") && md.Contains("实际累计秒数：0.00") && md.Contains("时长构成"), "report covers A20 fields without invented elapsed time");
        Check(ForestAudit.Markdown(s, "逻辑样例", "未试玩", ForestDefaults.Create(), 42).Contains("初始随机种子：42"), "report can attach initial setup and random seed");
        string report = ForestAudit.Export(s, "Artifacts/ForestFeedbackChecks", "逻辑检查样例", "未进行实际试玩");
        Check(File.Exists(report) && File.ReadAllText(Path.ChangeExtension(report, ".csv")).Contains("requestedAmount"), "markdown and complete CSV export");
        File.Copy(report, "Artifacts/forest-m5-example-report.md", true);
        File.Delete(report); File.Delete(Path.ChangeExtension(report, ".csv"));
        s = Fresh(6); ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s);
        Check(s.statistics.generatedFogCount > 0 && ForestAudit.Check(s).Count == 0, "burst generation tracked per cell and conserved");
        Check(s.GetTemplate("M02").devourer.growthEnabled == false && ForestRules.Stats(s, U(s, "M02-1")).maxHp == 100, "growth remains disabled");
        return "PASS M5 engineering (" + checks + " assertions): A18 feedback/threat/actual damage/CD; A19 pollution/resource audit and event export; A20 timer/result/strategy report infrastructure. Actual two-strategy gameplay and real 3-8 minute duration remain unexecuted per no-Play constraint.";
    }
}
