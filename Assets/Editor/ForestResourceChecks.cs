using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mizonglin.Demo;
using Mizonglin.Forest;

// Focused checks for the revised free spirit interactions; no Play or native UI automation.
public static class ForestResourceChecks
{
    static int checks;
    static Vector2Int P(int x, int y) => new Vector2Int(x, y);
    static UnitState U(ForestBattleState s, string id = "C01") => s.unitsById[id];
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception("Spirit rules: " + label); }
    static ForestBattleState Fresh(int round = 1)
    {
        var d = ForestDefaults.Create();
        for (int i = 0; i < d.map.terrain.Length; i++) { d.map.terrain[i] = 0; d.map.regions[i] = 3; d.map.initialFog[i] = false; }
        d.map.initialFog[13 * d.map.width] = true;
        d.map.rootCells = new Vector2Int[0];
        d.spawns[0].cell = P(2, 2); d.spawns[1].cell = P(4, 2); d.spawns[2].cell = P(2, 4); d.spawns[3].cell = P(4, 4);
        var s = new ForestSession(d).State; s.round = round; ForestRules.BeginPlayerPhase(s); return s;
    }
    static ForestCommand C(ActionKind kind, string spirit = null, string actor = "C01", params Vector2Int[] path) =>
        new ForestCommand { actorId = actor, kind = kind, spiritId = spirit, path = path.Length == 0 ? null : path.ToList() };
    static void Ground(ForestBattleState s, string id, Vector2Int cell) => s.spiritsById.Add(id, new SpiritState {
        spiritId = id, sourceInstanceId = "M03", sourcePhase = 1, cell = cell, locationKind = SpiritLocation.Ground });
    static void Act(ForestBattleState s, ForestCommand c) => Check(ForestRules.TryExecute(s, c, out var error), error ?? c.kind.ToString());
    public static string Run()
    {
        checks = 0;
        var s = Fresh(); Ground(s, "a", P(3, 2));
        var carry = C(ActionKind.CarrySpirit, "a", "C01", P(2, 2), P(3, 2));
        var moved = ForestRules.PreviewMovement(s, "C01", carry.path, out _);
        Check(U(moved).carriedSpiritId == null && ForestRules.SpiritAt(moved, P(3, 2)) != null, "movement no longer automatically picks up spirits");
        var preview = ForestRules.Preview(s, carry, out _);
        Check(preview != null && U(preview).carriedSpiritId == "a" && !U(preview).acted && U(preview).movementCommitted, "explicit carry previews movement without consuming action");
        Check(U(s).cell == P(2, 2) && U(s).carriedSpiritId == null && !U(s).movementCommitted && U(s).statistics.actions == 0, "cancel/discard preview leaves real state intact");
        Act(s, carry);
        Check(U(s).cell == P(3, 2) && U(s).carriedSpiritId == "a" && !U(s).acted && U(s).statistics.actions == 0, "carry confirms movement while preserving major action");
        Check(ForestPathfinder.Reachable(s, U(s)).costs.Count == 1, "free operation does not grant another movement");
        Check(!ForestRules.TryExecute(s, C(ActionKind.Wait, null, "C01", P(3, 2), P(3, 3)), out _), "second movement rejected");
        Ground(s, "b", P(3, 2));
        Check(!ForestRules.TryExecute(s, C(ActionKind.CarrySpirit, "b"), out _), "carry cap stays one");
        Act(s, C(ActionKind.SwallowSpirit, "b"));
        Check(U(s).carriedSpiritId == "a" && U(s).purificationCharges.Count == 1 && !U(s).acted && U(s).statistics.actions == 0, "ground swallow preserves carried spirit and action");
        Act(s, C(ActionKind.Wait));
        Check(U(s).acted && U(s).statistics.actions == 1, "major action still consumes once");
        var toss = C(ActionKind.ThrowSpirit); toss.targetCell = P(6, 2);
        s.groundFog.Add(P(6, 2)); s.statistics.initialFogCount++;
        preview = ForestRules.Preview(s, toss, out _);
        Check(preview != null && s.groundFog.Contains(P(6, 2)) && U(s).carriedSpiritId == "a", "throw preview after action is isolated");
        Act(s, toss);
        Check(U(s).acted && U(s).statistics.actions == 1 && U(s).carriedSpiritId == null && !s.groundFog.Contains(P(6, 2)), "throw allowed after action without consuming or restoring it");
        Check(!ForestRules.TryExecute(s, C(ActionKind.Wait), out _), "throw does not restore the spent action");
        Ground(s, "c", U(s).cell); Act(s, C(ActionKind.CarrySpirit, "c")); Act(s, C(ActionKind.SwallowSpirit));
        Check(U(s).acted && U(s).statistics.actions == 1 && U(s).purificationCharges.Count == 2, "carry and swallow remain free after major action; quotas queue independently");
        ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s); ForestRules.BeginPlayerPhase(s);
        Check(!U(s).movementCommitted && !U(s).acted && ForestPathfinder.Reachable(s, U(s)).costs.Count > 1, "next phase restores movement and major action normally");
        s.groundFog.Clear(); s.statistics.initialFogCount = 0; s.statistics.clearedFogCount = 0;
        s.groundFog.Add(P(3, 2)); s.groundFog.Add(P(3, 3));
        Act(s, C(ActionKind.Wait, null, "C01", P(3, 2), P(3, 3)));
        Check(U(s).purificationCharges[0].clearedCount == 2 && s.groundFog.Count == 0, "queued purification still clears start and endpoint on actual movement");
        s = Fresh(); Ground(s, "far", P(8, 8));
        Check(!ForestRules.TryExecute(s, C(ActionKind.SwallowSpirit, "far"), out _) && s.spiritsById["far"].locationKind == SpiritLocation.Ground && s.eventLog.All(e => e.kind != "SwallowSpirit"), "remote ground spirit cannot be consumed");
        s = Fresh(); Ground(s, "a", U(s, "C03").cell); U(s, "C03").flight.stance = FlightStance.Flying;
        Check(!ForestRules.TryExecute(s, C(ActionKind.CarrySpirit, "a", "C03"), out _) && !ForestRules.TryExecute(s, C(ActionKind.SwallowSpirit, "a", "C03"), out _), "flying restrictions preserved");
        s = Fresh(3); Ground(s, "a", U(s, "C04").cell); Act(s, C(ActionKind.SwallowSpirit, "a", "C04"));
        Check(U(s, "C04").currentHp == 210 && !U(s, "C04").acted && U(s, "C04").statistics.actions == 0 && !s.eventLog.Any(e => e.kind == "SelfDamage"), "free spirit operation does not trigger AShuang action-end self damage");
        Act(s, C(ActionKind.Wait, null, "C04")); Check(U(s, "C04").currentHp == 203, "major action still triggers exactly one self damage");
        s = Fresh(); U(s, "M03").cell = P(3, 2);
        for (int phase = 1; phase <= 4; phase++)
        {
            U(s).acted = false; U(s, "M03").currentHp = 1;
            var attack = C(ActionKind.BasicAttack); attack.targetId = "M03"; Act(s, attack);
            Check(s.spiritsById.Count == phase && (phase == 4 ? !ForestRules.Alive(U(s, "M03")) : U(s, "M03").currentHp == 120), "boss phase/resource regression " + phase);
        }
        Check(s.spiritsById.Values.All(a => a.locationKind == SpiritLocation.Ground) && s.spiritsById.Values.Select(a => a.cell).Distinct().Count() == 4, "boss drops stay distinct ground resources awaiting player choice");
        var view = new BattleState(); view.BindForest(s, ForestDefaults.Create());
        Check(view.units.Single(a => a.id == "C01").forestUnit == U(s), "formal state still reaches presentation adapter");
        s = Fresh(); Ground(s, "held", U(s).cell); Act(s, C(ActionKind.CarrySpirit, "held"));
        Check(!ForestRules.PlayerPhaseComplete(s), "free carry alone does not complete major actions");
        foreach (var u in s.unitsById.Values.Where(u => s.GetTemplate(u.templateId).faction == Faction.Ally)) u.acted = true;
        Check(ForestRules.PlayerPhaseComplete(s) && U(s).carriedSpiritId == "held", "unused carried spirit does not prevent automatic phase completion");
        ForestRules.EndPlayerPhase(s);
        Check(s.phase == BattlePhase.Enemy && U(s).carriedSpiritId == "held", "phase advances with spirit kept");
        ForestRules.EndEnemyPhase(s); ForestRules.BeginPlayerPhase(s);
        Check(!ForestRules.PlayerPhaseComplete(s) && !U(s).acted && U(s).carriedSpiritId == "held", "next round restores action and keeps unused spirit");
        return "PASS revised spirit rules (" + checks + " focused assertions): explicit carry/swallow; free carry/throw/swallow before and after major action; single movement; isolated preview; flight/carry limits; purification; boss drops; presentation adapter. No Play or computer-use verification.";
    }
}
