using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zilong.Forest;

public static class ForestSkillChecks
{
    static int checks;
    static Vector2Int P(int x, int y) => new Vector2Int(x, y);
    static UnitState U(ForestBattleState s, string id) => s.unitsById[id];
    static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception("Skills: " + text); }
    static ForestBattleState Fresh(int round = 1, Action<ScenarioDefinition> edit = null)
    {
        var d = ForestDefaults.Create();
        for (int i = 0; i < d.map.terrain.Length; i++) { d.map.terrain[i] = 0; d.map.regions[i] = 3; d.map.initialFog[i] = false; }
        d.map.rootCells = new Vector2Int[0];
        d.spawns[0].cell = P(2, 2); d.spawns[1].cell = P(4, 2); d.spawns[2].cell = P(2, 4); d.spawns[3].cell = P(4, 4);
        edit?.Invoke(d); var s = new ForestSession(d).State; s.round = round; ForestRules.BeginPlayerPhase(s); return s;
    }
    static ForestCommand C(string actor, ActionKind kind, string target = null) => new ForestCommand { actorId = actor, kind = kind, targetId = target };
    static void Act(ForestBattleState s, ForestCommand c) => Check(ForestRules.TryExecute(s, c, out var error), error ?? c.kind.ToString());
    static void Next(ForestBattleState s) { ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s); ForestRules.BeginPlayerPhase(s); }
    public static string Run()
    {
        checks = 0;
        var s = Fresh(); U(s, "M03").cell = P(5, 4); U(s, "C01").cell = P(8, 4); s.randomState = 42;
        foreach (var u in s.unitsById.Values.Where(u => s.GetTemplate(u.templateId).faction == Faction.Ally && u.instanceId != "C01")) { u.lifeState = LifeState.Dead; u.currentHp = 0; }
        s.phase = BattlePhase.Enemy;
        Check(!ForestRules.Stats(s, U(s, "M03")).canAttack && !ForestRules.TryExecute(s, C("M03", ActionKind.BasicAttack, "C01"), out _), "boss has no basic attack");
        var charge = ForestRules.ChooseEnemyAction(s, "M03"); var preview = ForestRules.Preview(s, charge, out _);
        Check(charge.kind == ActionKind.MaskCharge && s.randomState == 42 && U(s, "M03").maskBoss.releaseRound == 0, "charge preview isolates random state and warning");
        Check(U(preview, "M03").maskBoss.warningCenter == P(7, 4) && ForestRules.MaskWarningCells(preview, U(preview, "M03")).Count() == 9, "adjacent directional3x3 warning");
        Act(s, charge); Check(s.randomState == preview.randomState && U(s, "M03").maskBoss.releaseRound == 2 && U(s, "C01").currentHp == 90, "charge consumes enemy turn with no immediate damage");
        U(s, "M03").acted = false; Check(!ForestRules.TryExecute(s, C("M03", ActionKind.MaskBlast), out _), "cannot release in charging round");
        var warning = U(s, "M03").maskBoss.warningCenter; U(s, "C01").cell = P(10, 4);
        Check(U(s, "M03").maskBoss.warningCenter == warning, "warning does not track moving target");
        U(s, "C01").cell = P(8, 4); U(s, "M04-1").cell = P(6, 3); s.round = 2;
        Act(s, ForestRules.ChooseEnemyAction(s, "M03"));
        Check(U(s, "C01").currentHp == 30 && U(s, "M04-1").lifeState == LifeState.Dead && !ForestRules.MaskWarningCells(s, U(s, "M03")).Any(), "release deals fixed60 to units in area, including enemies, then clears warning");
        s = Fresh(); U(s, "M03").cell = P(4, 3); U(s, "M04-1").cell = P(3, 2); s.groundFog.Add(P(4, 3)); s.statistics.initialFogCount = 1;
        var talisman = C("C01", ActionKind.Talisman); talisman.targetCell = P(4, 3);
        preview = ForestRules.Preview(s, talisman, out _);
        Check(U(preview, "M03").currentHp == 13 && U(s, "M03").currentHp == 120 && !U(s, "C01").acted, "talisman150% attack minus defense, preview detached");
        Act(s, talisman);
        Check(U(s, "M03").currentHp == 13 && U(s, "M04-1").lifeState == LifeState.Dead && s.groundFog.Contains(P(4, 3)) && s.statistics.clearedFogCount == 0, "talisman damages enemies without clearing fog");
        Check(ForestRules.IsStunned(s, U(s, "M03")) && U(s, "C02").currentHp == 80, "stun enemies only");
        s.phase = BattlePhase.Enemy; Check(ForestRules.ChooseEnemyAction(s, "M03").kind == ActionKind.Wait, "stun blocks boss skill");
        ForestRules.EndEnemyPhase(s); ForestRules.BeginPlayerPhase(s);
        Check(!ForestRules.IsStunned(s, U(s, "M03")), "one-round stun expires next round");
        s = Fresh(); U(s, "C03").stunnedUntilRound = 1; U(s, "M04-1").cell = P(3, 4);
        Check(!ForestRules.TryExecute(s, C("C03", ActionKind.Sprint), out _) && !ForestRules.TryExecute(s, C("C03", ActionKind.SwitchStance), out _), "stun blocks free skills");
        Act(s, C("C03", ActionKind.BasicAttack, "M04-1"));
        s = Fresh(); U(s, "M04-1").cell = P(5, 4); Act(s, C("C04", ActionKind.BasicAttack, "M04-1"));
        Check(U(s, "M04-1").currentHp == 38 && U(s, "C04").currentHp == 139, "guard adjacent attack at50%, normal enemy counter");
        s = Fresh(edit: d => d.unitTemplates[7].attack = 100); U(s, "M04-1").cell = P(5, 4);
        Act(s, C("C04", ActionKind.Retreat)); Check(U(s, "C04").reflectPercent == 90 && U(s, "C04").acted, "guard retreat arms next-hit reflection and consumes action");
        s.phase = BattlePhase.Enemy; Act(s, C("M04-1", ActionKind.BasicAttack, "C04"));
        Check(U(s, "C04").currentHp == 94 && U(s, "M04-1").currentHp == 19 && U(s, "C04").reflectPercent == 0, "reflect90% actual damage once without recursion");
        U(s, "M04-1").acted = false; Act(s, C("M04-1", ActionKind.BasicAttack, "C04"));
        Check(U(s, "M04-1").currentHp == 19, "reflection consumed after first hit");
        s = Fresh(3); U(s, "M04-1").cell = P(5, 4); Act(s, C("C04", ActionKind.Retreat, "M04-1"));
        Check(U(s, "C04").currentHp == 140 && U(s, "M04-1").currentHp == 0 && s.eventLog.Count(e => e.kind == "SkillCost") == 1, "attack retreat pays63, hits75, then existing self damage7");
        s = Fresh(); Act(s, C("C03", ActionKind.Sprint));
        Check(!U(s, "C03").acted && U(s, "C03").bonusActions == 1 && ForestRules.Ability(U(s, "C03"), "sprint").availableRound == 3, "sprint free, one bonus action, cooldown2");
        var wait = C("C03", ActionKind.Wait); wait.path = new List<Vector2Int> { P(2, 4), P(3, 4) }; Act(s, wait);
        Check(!U(s, "C03").acted && !U(s, "C03").movementCommitted && U(s, "C03").bonusActions == 0, "first action spends bonus and restores second movement");
        wait.path = new List<Vector2Int> { P(3, 4), P(3, 5) }; Act(s, wait);
        Check(U(s, "C03").acted && U(s, "C03").statistics.actions == 2 && U(s, "C03").cell == P(3, 5), "second action completes normally");
        Next(s); Check(!ForestRules.TryExecute(s, C("C03", ActionKind.Sprint), out _), "sprint unavailable N+1"); Next(s); Act(s, C("C03", ActionKind.Sprint));
        s = Fresh(3);
        var teleport = C("C01", ActionKind.Echo, "C04"); teleport.targetCell = P(3, 2);
        preview = ForestRules.Preview(s, teleport, out _);
        Check(U(preview, "C04").cell == P(3, 2) && U(s, "C04").cell == P(4, 4), "teleport preview is detached");
        Act(s, teleport);
        Check(U(s, "C04").cell == P(3, 2) && U(s, "C01").acted && !U(s, "C04").acted
            && ForestRules.Ability(U(s, "C01"), "echo").availableRound == 6, "teleport consumes caster action and CD3, preserves teammate action");
        U(s, "C01").acted = false;
        Check(!ForestRules.TryExecute(s, teleport, out _), "teleport cooldown enforced");
        s = Fresh(3); U(s, "M03").cell = P(5, 4); U(s, "M03").currentHp = 1;
        Act(s, C("C04", ActionKind.Retreat, "M03"));
        Check(U(s, "M03").maskBoss.phaseIndex == 2 && U(s, "M03").currentHp == 120 && s.spiritsById.Count == 1
            && s.eventLog.Count(e => e.kind == "CorpseSummoned") == 2, "boss revives and summons two corpses");
        var copy = s.Copy(); U(copy, "C03").bonusActions = 9; U(copy, "M03").maskBoss.releaseRound = 55;
        Check(U(s, "C03").bonusActions == 0 && U(s, "M03").maskBoss.releaseRound == 0, "new runtime fields deep-copy independently");
        return "PASS skill update (" + checks + " focused assertions): boss charge/warning/fixed blast; talisman damage/stun; guard strike/retreat reflection; attack retreat HP cost; sprint actions/CD; ally teleport targeting/CD/action; preview, state copying and phase safety. Long-press UI compiled only; no Play/computer-use.";
    }
}
