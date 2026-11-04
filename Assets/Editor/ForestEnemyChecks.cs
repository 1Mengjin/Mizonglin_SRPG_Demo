using System;
using System.Linq;
using UnityEngine;
using Mizonglin.Forest;

public static class ForestEnemyChecks
{
    static int checks;
    static Vector2Int P(int x, int y) => new Vector2Int(x, y);
    static UnitState U(ForestBattleState s, string id) => s.unitsById[id];
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception("M4: " + label); }
    static ForestBattleState Fresh(int round = 1)
    {
        var d = ForestDefaults.Create();
        for (int i = 0; i < d.map.terrain.Length; i++) { d.map.terrain[i] = 0; d.map.regions[i] = 3; d.map.initialFog[i] = false; }
        d.map.initialFog[13 * d.map.width] = true;
        d.map.rootCells = new Vector2Int[0];
        d.spawns[0].cell = P(2, 2); d.spawns[1].cell = P(4, 2); d.spawns[2].cell = P(2, 4); d.spawns[3].cell = P(4, 4);
        var s = new ForestSession(d).State; s.round = round; ForestRules.BeginPlayerPhase(s); return s;
    }
    static void Act(ForestBattleState s, ForestCommand c) => Check(ForestRules.TryExecute(s, c, out var error), error ?? c.kind.ToString());
    static ForestCommand Attack(string actor, string target) => new ForestCommand { actorId = actor, targetId = target, kind = ActionKind.BasicAttack };
    static void KillSources(ForestBattleState s)
    { foreach (var u in s.unitsById.Values.Where(u => u.tree != null)) { u.lifeState = LifeState.Dead; u.currentHp = 0; u.tree.nextBurstRound = 0; } }
    static void Fog(ForestBattleState s, params Vector2Int[] cells)
    { foreach (var p in cells) if (s.groundFog.Add(p)) s.statistics.initialFogCount++; }
    static void NoFog(ForestBattleState s)
    { s.groundFog.Clear(); s.statistics.initialFogCount = 0; s.statistics.generatedFogCount = 0; s.statistics.clearedFogCount = 0; }
    static void Conserved(ForestBattleState s) => Check(s.groundFog.Count + s.CarriedFogCount == s.statistics.initialFogCount + s.statistics.generatedFogCount - s.statistics.clearedFogCount, "pollution conservation");
    static void End(ForestBattleState s) { s.phase = BattlePhase.Enemy; ForestRules.EndEnemyPhase(s); Conserved(s); }
    static void EnemyReady(ForestBattleState s, string id)
    { s.phase = BattlePhase.Enemy; U(s, id).acted = U(s, id).movementCommitted = false; }
    static void PursuitFixture(ForestBattleState s)
    { U(s, "M04-1").cell = P(4, 4); U(s, "C01").cell = P(6, 4); U(s, "C02").cell = P(4, 6); U(s, "C03").cell = P(1, 10); U(s, "C04").cell = P(2, 12); EnemyReady(s, "M04-1"); }
    public static string Run()
    {
        checks = 0;
        // A14 time growth: first release keeps layer1, then +1 layer per round; attacks drop the fruit immediately.
        var s = Fresh(); U(s, "M01A").cell = P(5, 2);
        int burstStart = s.eventLog.Count; s.round = 6; End(s);
        Check(s.eventLog.Skip(burstStart).Any(e => e.kind == "TreeBurst" && e.sourceId == "M01A")
            && U(s, "M01A").tree.fruitLevel == 1 && U(s, "M01A").tree.lastBurstRound == 6 && U(s, "M01A").tree.nextBurstRound == 9,
            "A14 first release keeps layer1 and schedules round9");
        int growthStart = s.eventLog.Count; s.round = 7; End(s);
        Check(U(s, "M01A").tree.fruitLevel == 2 && s.eventLog.Skip(growthStart).Any(e => e.kind == "FruitGrowth" && e.sourceId == "M01A"),
            "A14 one automatic layer per round");
        s.round = 8; End(s); Check(U(s, "M01A").tree.fruitLevel == 3, "A14 growth caps at3");
        int secondBurstStart = s.eventLog.Count; s.round = 9; End(s);
        Check(s.eventLog.Skip(secondBurstStart).Any(e => e.kind == "TreeBurst" && e.sourceId == "M01A")
            && U(s, "M01A").tree.fruitLevel == 1 && U(s, "M01A").tree.lastBurstRound == 9 && U(s, "M01A").tree.nextBurstRound == 12,
            "A14 periodic release uses the grown layer and resets to1");
        s = Fresh(); U(s, "M01A").cell = P(5, 2); s.round = 6; End(s);
        U(s, "M01A").stunnedUntilRound = 7;
        s.round = 7; int stunGrowStart = s.eventLog.Count; End(s);
        Check(U(s, "M01A").tree.fruitLevel == 1
            && !s.eventLog.Skip(stunGrowStart).Any(e => e.kind == "FruitGrowth" && e.sourceId == "M01A")
            && !s.eventLog.Skip(stunGrowStart).Any(e => e.kind == "TreeBurst" && e.sourceId == "M01A"),
            "A14 stun pauses growth and automatic release");
        s.round = 8; End(s);
        Check(U(s, "M01A").tree.fruitLevel == 2, "A14 growth resumes after stun ends");
        s = Fresh(4); U(s, "M01A").cell = P(5, 2); U(s, "C02").cell = P(4, 2); U(s, "C02").acted = false;
        int attackBurstStart = s.eventLog.Count; Act(s, Attack("C02", "M01A"));
        Check(s.eventLog.Skip(attackBurstStart).Any(e => e.kind == "TreeBurst" && e.sourceId == "M01A")
            && U(s, "M01A").tree.fruitLevel == 1 && U(s, "M01A").tree.lastBurstRound == 4 && U(s, "M01A").tree.nextBurstRound == 7,
            "A14 ally basic attack releases the fruit immediately and reschedules");
        s = Fresh(4); U(s, "M01A").cell = P(5, 2); U(s, "C02").cell = P(4, 2); U(s, "C02").acted = false;
        U(s, "M01A").stunnedUntilRound = 4;
        int stunnedAttackStart = s.eventLog.Count; Act(s, Attack("C02", "M01A"));
        Check(!s.eventLog.Skip(stunnedAttackStart).Any(e => e.kind == "TreeBurst" && e.sourceId == "M01A")
            && U(s, "M01A").tree.fruitLevel == 1, "A14 stun pauses the attack-triggered fall");
        s.round = 5; U(s, "C02").acted = false;
        int resumedAttackStart = s.eventLog.Count; Act(s, Attack("C02", "M01A"));
        Check(s.eventLog.Skip(resumedAttackStart).Any(e => e.kind == "TreeBurst" && e.sourceId == "M01A"), "A14 attack fall resumes after stun ends");
        s = Fresh(4); U(s, "M01A").cell = P(5, 2); U(s, "M01A").currentHp = 1; U(s, "C02").cell = P(4, 2); U(s, "C02").acted = false;
        s.Map.rootCells = new[] { P(3, 3) };
        int lethalStart = s.eventLog.Count; Act(s, Attack("C02", "M01A"));
        Check(U(s, "M01A").lifeState == LifeState.Dead && !s.eventLog.Skip(lethalStart).Any(e => e.kind == "TreeBurst")
            && U(s, "M01A").tree.nextBurstRound == 0 && s.statistics.sourceDefeatedRounds["M01A"] == s.round
            && ForestPathfinder.EntryCost(s, U(s, "C01"), P(3, 3)) == 1, "A14 killing the tree cancels the fall and root cost");
        s.round = 6; End(s); Check(!s.eventLog.Any(e => e.kind == "TreeBurst" && e.sourceId == "M01A"), "A14 dead source never bursts");
        s = new ForestSession(ForestDefaults.Create()).State; s.round = 6; ForestRules.BeginPlayerPhase(s);
        U(s, "M01A").tree.fruitLevel = U(s, "M01B").tree.fruitLevel = 3;
        End(s);
        Check(s.eventLog.Where(e => e.kind == "TreeBurst").Select(e => e.sourceId).SequenceEqual(new[] { "M01A", "M01B" }), "A14 tree order A then B");
        Check(s.eventLog.Where(e => e.kind == "TreeBurst").Select(e => e.amount).SequenceEqual(new[] { 16, 23 }), "A14 official clipped radius3 coverage16/23");
        Check(U(s, "M01A").tree.fruitLevel == 1 && U(s, "M01A").tree.nextBurstRound == 9, "A14 reset fruit and schedule next burst");
        s.round = 9; End(s); s.round = 12; End(s);
        Check(s.eventLog.Where(e => e.kind == "TreeBurst" && e.sourceId == "M01A").Select(e => e.round).SequenceEqual(new[] { 6, 9, 12 }), "A14 schedule6/9/12");
        s = Fresh(6); U(s, "M01A").cell = P(7, 7); U(s, "M01A").tree.fruitLevel = 3;
        U(s, "M02-1").cell = P(7, 9); U(s, "C01").cell = P(7, 8); U(s, "C01").poisonStacks = 2;
        Fog(s, P(7, 7)); End(s);
        Check(s.eventLog.First(e => e.kind == "TreeBurst").amount == 25 && s.eventLog.Count(e => e.kind == "FogGenerated" && e.sourceId == "M01A") == 24, "A14 full coverage includes center; overlap only counts new fog");
        Check(U(s, "C01").currentHp == 90 && U(s, "C01").poisonStacks == 1 && !s.groundFog.Contains(P(7, 8)), "A17 burst then absorb under ally then poison; no direct burst damage");
        Check(s.eventLog.FindLastIndex(e => e.kind == "TreeBurst") < s.eventLog.FindIndex(e => e.kind == "FogAbsorbed")
            && s.eventLog.FindLastIndex(e => e.kind == "FogAbsorbed") < s.eventLog.FindIndex(e => e.kind == "PoisonLayers"), "A17 round-end event order");

        s = Fresh(); var dev = U(s, "M02-1"); EnemyReady(s, dev.instanceId);
        Act(s, ForestRules.ChooseEnemyAction(s, dev.instanceId));
        Check(dev.cell == P(16, 6) && s.Map.patrolPoints[dev.devourer.nextPatrolIndex] == P(16, 7), "A15 patrol advances to next ordered point");
        U(s, "C01").cell = P(16, 7); int next = dev.devourer.nextPatrolIndex; EnemyReady(s, dev.instanceId);
        Act(s, ForestRules.ChooseEnemyAction(s, dev.instanceId)); Check(dev.cell == P(16, 6) && dev.devourer.nextPatrolIndex == next, "A15 occupied patrol point waits without advancing");
        U(s, "C01").cell = P(2, 2);
        foreach (var end in new[] { P(16, 7), P(15, 7), P(15, 6) })
        { EnemyReady(s, dev.instanceId); Act(s, ForestRules.ChooseEnemyAction(s, dev.instanceId)); Check(dev.cell == end, "A15 fixed patrol loop"); }
        s = Fresh(); NoFog(s); dev = U(s, "M02-1");
        var cloud = (from y in Enumerable.Range(4, 5) from x in Enumerable.Range(13, 5) let p = P(x, y) where ForestRules.Distance(p, dev.cell) <= 2 select p).ToArray();
        Fog(s, cloud); var expected = cloud.OrderBy(p => ForestRules.Distance(p, dev.cell)).ThenBy(p => p.y).ThenBy(p => p.x).Take(6).ToArray(); End(s);
        Check(s.eventLog.Where(e => e.kind == "FogAbsorbed").Select(e => e.cell).SequenceEqual(expected) && dev.devourer.carriedFogCount == 6 && s.groundFog.Count == 7 && s.statistics.clearedFogCount == 0, "A15 absorption nearest/y/x, cap6, transfer not clearing");
        End(s); Check(dev.devourer.carriedFogCount == 12 && dev.currentHp == 100 && ForestRules.Stats(s, dev).attack == 0, "A15 growth stays disabled with large carried fog");
        U(s, "C01").cell = P(12, 6); dev.currentHp = 1; s.phase = BattlePhase.Player;
        var empty = (from y in Enumerable.Range(0, s.Map.height) from x in Enumerable.Range(0, s.Map.width) let p = P(x, y) where s.Map.Walkable(p) && !s.groundFog.Contains(p) orderby ForestRules.Distance(p, dev.cell), p.y, p.x select p).Take(12).ToArray();
        int generated = s.statistics.generatedFogCount; Act(s, Attack("C01", dev.instanceId)); Conserved(s);
        Check(s.eventLog.Where(e => e.kind == "FogSpilled").Select(e => e.cell).SequenceEqual(empty) && dev.devourer.carriedFogCount == 0 && s.statistics.generatedFogCount == generated, "A15 death spill ordered and conserved, not new generation");
        s = Fresh(); NoFog(s); dev = U(s, "M02-1"); dev.devourer.carriedFogCount = 3; s.statistics.initialFogCount = 3;
        for (int y = 0; y < s.Map.height; y++) for (int x = 0; x < s.Map.width; x++) Fog(s, P(x, y));
        dev.currentHp = 1; U(s, "C01").cell = P(12, 6); Act(s, Attack("C01", dev.instanceId)); Conserved(s);
        Check(dev.lifeState == LifeState.Dead && dev.devourer.carriedFogCount == 3 && s.eventLog.Any(e => e.kind == "SpillOverflow"), "A15 overflow retained on dead carrier");
        NoFog(s); KillSources(s); ForestRules.CheckOutcome(s);
        Check(s.result == BattleResult.Victory && s.CarriedFogCount == 3, "A17 carried fog on a dead record does not block an empty field");

        s = Fresh(); PursuitFixture(s); var command = ForestRules.ChooseEnemyAction(s, "M04-1");
        Check(command.targetId == "C01" && command.kind == ActionKind.BasicAttack && command.path.Last() == P(5, 4), "A16 equal route costs choose C ID; move then attack");
        Act(s, command); Check(U(s, "C01").currentHp == 68 && U(s, "M04-1").acted, "A16 attack uses ordinary damage and consumes once");
        s = Fresh(); PursuitFixture(s); U(s, "C01").cell = P(4, 2); U(s, "C02").cell = P(7, 4);
        s.Map.rootCells = new[] { P(4, 3), P(3, 2), P(5, 2), P(4, 1) }; s.Rules.rootEntryCost = 3;
        Check(ForestRules.ChooseEnemyAction(s, "M04-1").targetId == "C02", "A16 weighted route beats physically nearer target");
        U(s, "M01A").lifeState = LifeState.Dead; U(s, "M01A").currentHp = 0;
        Check(ForestRules.ChooseEnemyAction(s, "M04-1").targetId == "C01", "A16 dead root source immediately changes pursuit cost");
        s = Fresh(); PursuitFixture(s);
        foreach (var p in new[] { P(5, 4), P(7, 4), P(6, 3), P(6, 5) }) s.Map.terrain[p.y * s.Map.width + p.x] = 1;
        Check(ForestRules.ChooseEnemyAction(s, "M04-1").targetId == "C02", "A16 unreachable close target skipped");
        s = Fresh(); PursuitFixture(s); U(s, "C01").cell = P(6, 6); U(s, "C02").cell = P(15, 12);
        command = ForestRules.ChooseEnemyAction(s, "M04-1");
        Check(command.targetId == "C01" && command.kind == ActionKind.Wait && command.path.Last() == P(6, 4), "A16 destination y/x tie, affordable path prefix, no out-of-range attack");
        s = Fresh(); PursuitFixture(s); U(s, "C03").cell = P(5, 4); U(s, "C03").flight.stance = FlightStance.Flying;
        command = ForestRules.ChooseEnemyAction(s, "M04-1"); Act(s, command);
        Check(command.targetId == "C03" && U(s, "C03").currentHp == 65 && !s.eventLog.Any(e => e.kind == "Counter"), "A16 flying ally can be targeted but cannot counter");

        // A18 devourer self-destruct at round8: fixed50 in radius2, tree growth, death spill.
        s = Fresh(7); var earlyDev = U(s, "M02-1"); earlyDev.cell = P(5, 5); U(s, "C01").cell = P(5, 6);
        int earlyStart = s.eventLog.Count; End(s);
        Check(!s.eventLog.Skip(earlyStart).Any(e => e.kind == "SelfDestruct"), "A18 self-destruct waits for round8");
        s = Fresh(8); var deadDev = U(s, "M02-1"); deadDev.cell = P(5, 5); deadDev.lifeState = LifeState.Dead; deadDev.currentHp = 0;
        int deadStart = s.eventLog.Count; End(s);
        Check(!s.eventLog.Skip(deadStart).Any(e => e.kind == "SelfDestruct"), "A18 a dead devourer cannot explode");
        s = Fresh(8); var bomber = U(s, "M02-1"); bomber.cell = P(5, 5); bomber.devourer.carriedFogCount = 3; s.statistics.initialFogCount += 3;
        foreach (var tree in s.unitsById.Values.Where(u => u.tree != null)) tree.tree.nextBurstRound = 99;
        U(s, "C01").cell = P(5, 6); U(s, "C02").cell = P(6, 5); U(s, "C03").cell = P(7, 5); U(s, "C04").cell = P(12, 10);
        U(s, "M01A").cell = P(5, 7); U(s, "M01B").cell = P(17, 12); U(s, "M03").cell = P(16, 12);
        U(s, "M04-1").cell = P(1, 12); U(s, "M04-2").cell = P(2, 12); U(s, "M04-3").cell = P(3, 12);
        int c04Before = U(s, "C04").currentHp;
        int blastStart = s.eventLog.Count; End(s);
        var blast = s.eventLog.Skip(blastStart).ToArray();
        Check(bomber.lifeState == LifeState.Dead && blast.Any(e => e.kind == "SelfDestruct" && e.sourceId == "M02-1"), "A18 self-destruct kills the devourer");
        Check(U(s, "C01").currentHp == 40 && U(s, "C02").currentHp == 30 && U(s, "C03").currentHp == 35, "A18 fixed50 damage within radius2");
        Check(U(s, "C04").currentHp == c04Before, "A18 units outside radius2 untouched");
        Check(U(s, "M01A").tree.fruitLevel == 1, "A18 explosion no longer grows the tree");
        Check(blast.Count(e => e.kind == "SelfDestructDamage") == 4 && blast.Count(e => e.kind == "FogSpilled") == 3, "A18 blast and spill events");
        Check(bomber.devourer.carriedFogCount == 0, "A18 explosion releases carried fog");
        Conserved(s);
        s = Fresh(8); var stunnedDev = U(s, "M02-1"); stunnedDev.cell = P(5, 5); stunnedDev.stunnedUntilRound = 8; U(s, "C01").cell = P(5, 6);
        int stunStart = s.eventLog.Count; End(s);
        Check(!s.eventLog.Skip(stunStart).Any(e => e.kind == "SelfDestruct") && stunnedDev.lifeState == LifeState.Alive, "A18 stun delays self-destruct");
        int resumeStart = s.eventLog.Count; End(s);
        Check(s.eventLog.Skip(resumeStart).Any(e => e.kind == "SelfDestruct") && stunnedDev.lifeState == LifeState.Dead, "A18 self-destruct resumes after stun");
        s = Fresh(4); var silencedDev = U(s, "M02-1"); silencedDev.cell = P(5, 5); silencedDev.devourer.carriedFogCount = 3;
        s.statistics.initialFogCount += 3; silencedDev.stunnedUntilRound = 4; silencedDev.currentHp = 1; U(s, "C01").cell = P(5, 6);
        int silenceStart = s.eventLog.Count; Act(s, Attack("C01", silencedDev.instanceId));
        Check(silencedDev.lifeState == LifeState.Dead && silencedDev.devourer.carriedFogCount == 3
            && !s.eventLog.Skip(silenceStart).Any(e => e.kind == "FogSpilled")
            && s.eventLog.Skip(silenceStart).Any(e => e.kind == "DeathrattleSilenced"), "A18 stunned death silences the fog spill");
        Conserved(s);
        s = Fresh(3); var stunnedCorpse = U(s, "M04-1"); stunnedCorpse.stunnedUntilRound = s.round; EnemyReady(s, "M04-1");
        U(s, "C01").cell = P(5, 4);
        Check(ForestRules.ChooseEnemyAction(s, "M04-1").kind == ActionKind.Wait, "A18 stun blocks the basic attack AI");
        Check(!ForestRules.TryExecute(s, new ForestCommand { actorId = "M04-1", targetId = "C01", kind = ActionKind.BasicAttack }, out _), "A18 stun blocks enemy commands");

        s = Fresh(); NoFog(s); ForestRules.CheckOutcome(s);
        Check(s.result == BattleResult.Victory, "A17 live sources do not block victory without ground fog");
        s = Fresh(); NoFog(s); Fog(s, P(2, 2));
        s.spiritsById.Add("a", new SpiritState { spiritId = "a", sourceInstanceId = "M03", sourcePhase = 1, cell = P(2, 2), locationKind = SpiritLocation.Ground });
        Act(s, new ForestCommand { actorId = "C01", spiritId = "a", kind = ActionKind.CarrySpirit });
        Check(s.result == BattleResult.InProgress, "A17 residual ground fog blocks victory");
        Act(s, new ForestCommand { actorId = "C01", kind = ActionKind.ThrowSpirit, targetCell = P(2, 2) });
        Check(s.result == BattleResult.Victory && s.phase == BattlePhase.Finished && s.endReason == BattleEndReason.Cleared && !U(s, "C01").acted, "A17 free resource clear checks victory without consuming action");
        s = Fresh(); NoFog(s); KillSources(s); dev = U(s, "M02-1"); dev.devourer.carriedFogCount = 3; s.statistics.initialFogCount = 3; dev.currentHp = 1; U(s, "C01").cell = P(12, 6);
        Act(s, Attack("C01", dev.instanceId)); Conserved(s);
        Check(s.result == BattleResult.InProgress && s.groundFog.Count == 3, "A17 no premature victory before death spill completes");
        s = Fresh(14); Act(s, new ForestCommand { actorId = "C01", kind = ActionKind.Wait }); End(s);
        Check(s.result == BattleResult.InProgress && s.round == 15, "test limit99 permits playing beyond round14");
        s = Fresh(99); Act(s, new ForestCommand { actorId = "C01", kind = ActionKind.Wait });
        Check(s.result == BattleResult.InProgress, "A17 round99 action does not time out early"); End(s);
        Check(s.result == BattleResult.Defeat && s.endReason == BattleEndReason.Timeout && s.round == 99, "A17 timeout only after full round99 end");
        s = Fresh(99); NoFog(s); KillSources(s); End(s);
        Check(s.result == BattleResult.Victory && s.endReason == BattleEndReason.Cleared, "A17 round99 victory before timeout");
        s = Fresh(99); NoFog(s); KillSources(s);
        foreach (var u in s.unitsById.Values.Where(u => s.GetTemplate(u.templateId).faction == Faction.Ally && u.instanceId != "C03")) { u.lifeState = LifeState.Dead; u.currentHp = 0; }
        U(s, "C03").currentHp = 1; U(s, "C03").flight.stance = FlightStance.Flying; U(s, "C03").poisonStacks = 3; End(s);
        Check(s.result == BattleResult.Defeat && s.endReason == BattleEndReason.Eliminated && s.statistics.everAllyDied, "A17 poison batch and elimination beat simultaneous clean-field victory");
        s = new ForestSession(ForestDefaults.Create()).State; ForestRules.BeginPlayerPhase(s); ForestRules.EndPlayerPhase(s);
        foreach (var id in s.unitsById.Values.Where(u => s.GetTemplate(u.templateId).faction == Faction.Enemy).Select(u => u.instanceId).OrderBy(id => id, StringComparer.Ordinal).ToArray())
            Act(s, ForestRules.ChooseEnemyAction(s, id));
        ForestRules.EndEnemyPhase(s); Conserved(s);
        Check(s.round == 2 && s.phase == BattlePhase.PlayerStart, "A14-A17 formal default enemy phase and round end connect");
        return "PASS M4 (" + checks + " focused assertions): A14 time growth/bursts/attack fall/stun attack-fall pause/death; A15 patrol/absorb/spill/overflow; A16 weighted pursuit/targeting; A17 order/conservation/victory/timeout; A18 devourer self-destruct delay/stun silence/deathrattle suppression; actual default enemy command chain. Logic only, no Play or computer-use.";
    }
}
