using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mizonglin.Demo;
using Mizonglin.Forest;

public static class ForestCombatChecks
{
    static int checks;
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception("M2: " + label); }
    static UnitState U(ForestBattleState s, string id) => s.unitsById[id];
    static ForestBattleState Fresh(int round = 1, FlightStance stance = FlightStance.Grounded, Action<ScenarioDefinition> edit = null, bool begin = true)
    {
        var d = ForestDefaults.Create();
        for (int i = 0; i < d.map.terrain.Length; i++) { d.map.terrain[i] = 0; d.map.regions[i] = 3; d.map.initialFog[i] = false; }
        d.map.initialFog[13 * d.map.width] = true;
        d.spawns[0].cell = new Vector2Int(2, 2); d.spawns[1].cell = new Vector2Int(4, 2);
        d.spawns[2].cell = new Vector2Int(2, 4); d.spawns[3].cell = new Vector2Int(4, 4);
        edit?.Invoke(d);
        var s = new ForestSession(d).State; s.round = round;
        U(s, "C03").flight.stance = stance;
        if (begin) ForestRules.BeginPlayerPhase(s);
        return s;
    }
    static ForestCommand Command(string actor, ActionKind kind, string target = null, params Vector2Int[] path) =>
        new ForestCommand { actorId = actor, kind = kind, targetId = target, path = path.Length == 0 ? null : path.ToList() };
    static void Act(ForestBattleState s, ForestCommand c) => Check(ForestRules.TryExecute(s, c, out var error), error ?? c.kind.ToString());
    static void Next(ForestBattleState s)
    { ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s); ForestRules.BeginPlayerPhase(s); }

    public static string Run()
    {
        checks = 0;
        var s = Fresh(); U(s, "M04-1").cell = new Vector2Int(5, 2);
        Act(s, Command("C01", ActionKind.BasicAttack, "M04-1"));
        Check(!ForestRules.TryExecute(s, Command("C01", ActionKind.Wait), out _) && U(s, "C01").statistics.actions == 1, "A05 action consumed exactly once");
        Check(!ForestRules.TryExecute(s, Command("M03", ActionKind.Wait), out _), "A05 enemy cannot act in player phase");
        s = Fresh(3); Act(s, Command("C04", ActionKind.Wait));
        Check(U(s, "C04").currentHp == 203, "A05 wait self damage");
        ForestRules.EndPlayerPhase(s); Check(U(s, "C04").currentHp == 203 && s.phase == BattlePhase.Enemy, "A05 manual end no duplicate self damage");
        Check(s.eventLog.Where(e => e.kind == "ActionEnd").Select(e => e.sourceId).SequenceEqual(new[] { "C04", "C01", "C02", "C03" }), "A05 skipped allies wait by ID");
        s = Fresh(3); ForestRules.EndPlayerPhase(s); Check(U(s, "C04").currentHp == 203 && U(s, "C04").statistics.actions == 1, "A05 skipped attack form still self hurts");
        ForestRules.EndEnemyPhase(s); Check(s.phase == BattlePhase.PlayerStart && s.round == 4, "A05 round order");
        ForestRules.BeginPlayerPhase(s);
        Check(U(s, "C03").flight.stanceLockedRound == 4 && !U(s, "C03").acted, "A05 automatic phase start costs no action");
        bool locked = false; try { ForestRules.BeginPlayerPhase(s); } catch (InvalidOperationException) { locked = true; }
        Check(locked, "A05 cannot begin the same phase twice");
        for (int round = 1; round <= 14; round++)
        { s = Fresh(round); Check(U(s, "C04").dualForm.form == (((round - 1) % 4 < 2) ? DualForm.Guard : DualForm.Attack), "A06 form round " + round); }

        s = Fresh(3); int[] poisonAttack = { 52, 19, 45, 75 };
        for (int i = 0; i < 4; i++)
        {
            var u = U(s, "C0" + (i + 1)); u.poisonStacks = 1;
            Check(ForestRules.Stats(s, u).attack == poisonAttack[i], "A06 integer attack " + u.instanceId);
            u.poisonStacks = 3; Check(ForestRules.Stats(s, u).attack == poisonAttack[i], "A06 poison not multiplied per stack");
        }
        s = Fresh(); Check(ForestRules.Stats(s, U(s, "C04")).canAttack && ForestRules.Stats(s, U(s, "C04")).defense == 54, "A06 guard can attack adjacent enemies");
        Check(!ForestRules.Stats(s, U(s, "M01A")).canAttack && !ForestRules.Stats(s, U(s, "M02-1")).canAttack, "A06 trees/devourer no attack");
        s = Fresh(edit: d => d.unitTemplates[7].maxHp = 100); U(s,"C04").cell = new Vector2Int(12,8); U(s, "M04-1").cell = new Vector2Int(3, 4);
        Act(s, Command("C03", ActionKind.BasicAttack, "M04-1"));
        Check(U(s, "C03").currentHp == 65 && U(s, "M04-1").currentHp == 35 && s.eventLog.Count(e => e.kind == "Counter") == 1, "A06 exactly one melee counter");
        s = Fresh(); U(s, "M04-1").cell = new Vector2Int(3, 4); Act(s, Command("C03", ActionKind.BasicAttack, "M04-1"));
        Check(U(s, "C03").currentHp == 85 && !s.eventLog.Any(e => e.kind == "Counter"), "A06 killed defender no counter");
        s = Fresh(); U(s, "M03").cell = new Vector2Int(3, 4); Act(s, Command("C03", ActionKind.BasicAttack, "M03"));
        Check(!s.eventLog.Any(e => e.kind == "Counter"), "A06 range2 boss no counter");
        s = Fresh(begin: false); U(s, "C04").cell = new Vector2Int(2, 2); U(s, "C01").cell = new Vector2Int(3, 2);
        U(s, "C02").cell = new Vector2Int(2, 3); U(s, "C01").currentHp = 45; U(s, "C02").currentHp = 40;
        ForestRules.BeginPlayerPhase(s);
        Check(U(s, "C04").dualForm.protectedUnitId == null, "A06 protection is dynamic, no fixed phase-start target");
        U(s, "C01").cell = new Vector2Int(12, 2); U(s, "M04-1").cell = new Vector2Int(11, 2); s.phase = BattlePhase.Enemy;
        Act(s, Command("M04-1", ActionKind.BasicAttack, "C01"));
        Check(U(s, "C01").currentHp == 23 && U(s, "C04").currentHp == 140, "A06 far target leaves protection radius");
        U(s, "M04-1").acted = false; U(s, "C04").cell = new Vector2Int(12,3); U(s, "C04").currentHp = 1; Act(s, Command("M04-1", ActionKind.BasicAttack, "C01"));
        Check(U(s, "C04").lifeState == LifeState.Dead && U(s, "C01").currentHp == 23 && U(s, "C04").dualForm.protectedUnitId == null, "A06 protector death no spillover");
        s = Fresh(begin: false); U(s, "C04").cell = new Vector2Int(2, 2); U(s, "C03").cell = new Vector2Int(3, 2);
        U(s, "C01").cell = new Vector2Int(1, 6); U(s, "C04").currentHp = 1; U(s, "M04-1").cell = new Vector2Int(4, 2);
        ForestRules.BeginPlayerPhase(s); s.phase = BattlePhase.Enemy;
        Act(s, Command("M04-1", ActionKind.BasicAttack, "C03"));
        Check(U(s, "C03").currentHp == 85 && U(s, "C04").lifeState == LifeState.Dead
            && U(s, "M04-1").lifeState == LifeState.Dead && s.eventLog.Count(e => e.kind == "Counter") == 1,
            "A06 original target counters after protector dies");
        s = Fresh(); U(s, "M04-1").cell = new Vector2Int(5, 4); s.phase = BattlePhase.Enemy;
        Act(s, Command("M04-1", ActionKind.BasicAttack, "C04"));
        Check(U(s, "C04").currentHp == 139 && !s.eventLog.Any(e => e.kind == "Counter"), "A06 guard direct hit defense36 minimum1 no counter");
        s = Fresh(stance: FlightStance.Flying); U(s,"C04").cell = new Vector2Int(12,8); U(s, "M04-1").cell = new Vector2Int(3, 4); s.phase = BattlePhase.Enemy;
        Act(s, Command("M04-1", ActionKind.BasicAttack, "C03"));
        Check(U(s, "C03").currentHp == 65 && !s.eventLog.Any(e => e.kind == "Counter"), "A06 flying can be hit but cannot counter");
        s = Fresh(3); U(s, "C04").scarStacks = 3; U(s, "C04").currentHp = 40; Act(s, Command("C04", ActionKind.Wait));
        Check(U(s, "C04").currentHp == 33 && ForestRules.Stats(s, U(s, "C04")).maxHp == 165
            && ForestRules.PoisonDamage(s, U(s, "C04")) == 17 && ForestRules.NearDeathLine(s, U(s, "C04")) == 33, "A06 scar percentage base; self damage template base");
        s = Fresh(3); U(s, "C04").currentHp = 3; Act(s, Command("C04", ActionKind.Wait));
        Check(U(s, "C04").currentHp == 1 && U(s, "C04").everNearDeath && s.statistics.everNearDeath, "A07 self damage nonlethal and latches near death");
        s = Fresh(3, edit: d => { d.unitTemplates[3].dualForm.attackRange = 1; d.unitTemplates[7].maxHp = 200; });
        U(s, "C04").currentHp = 20; U(s, "M04-1").cell = new Vector2Int(5, 4); Act(s, Command("C04", ActionKind.BasicAttack, "M04-1"));
        Check(U(s, "C04").lifeState == LifeState.Dead && !s.eventLog.Any(e => e.kind == "SelfDamage"), "A06 no self damage after lethal counter");

        s = Fresh(); int[] poisonDamage = { 9, 8, 9, 14 }; int[] maxHp = { 90, 80, 85, 140 };
        for (int i = 0; i < 4; i++) { var u = U(s, "C0" + (i + 1)); u.poisonStacks = 2; s.groundFog.Add(u.cell); }
        ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s);
        for (int i = 0; i < 4; i++) { var u = U(s, "C0" + (i + 1)); Check(u.poisonStacks == 3 && u.currentHp == maxHp[i] - poisonDamage[i], "A07 entering layer3 damage " + u.instanceId); }
        s = Fresh(); U(s, "C01").poisonStacks = 3; ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s);
        Check(U(s, "C01").poisonStacks == 2 && U(s, "C01").currentHp == 90, "A07 layer3 to2 no damage");
        s = Fresh(begin: false); U(s, "C04").cell = new Vector2Int(2, 2); U(s, "C01").cell = new Vector2Int(3, 2);
        U(s, "C01").poisonStacks = 2; s.groundFog.Add(U(s, "C01").cell);
        ForestRules.BeginPlayerPhase(s); ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s);
        Check(U(s, "C01").currentHp == 81 && U(s, "C04").currentHp == 140, "A07 poison bypasses protection");
        s = Fresh(stance: FlightStance.Flying); U(s, "C03").poisonStacks = 3; ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s);
        Check(U(s, "C03").poisonStacks == 3 && U(s, "C03").currentHp == 76, "A07 flying retains layers and layer3 damage");
        s = Fresh(); foreach (var u in s.unitsById.Values.Where(u => s.GetTemplate(u.templateId).faction == Faction.Enemy)) s.groundFog.Add(u.cell);
        ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s);
        Check(s.unitsById.Values.Where(u => s.GetTemplate(u.templateId).faction == Faction.Enemy).All(u => u.poisonStacks == 0 && u.currentHp == s.GetTemplate(u.templateId).maxHp), "A07 all monsters immune");
        s = Fresh(edit: d => d.unitTemplates[7].attack = 100); U(s,"C04").cell = new Vector2Int(12,8); U(s, "M04-1").cell = new Vector2Int(5, 2); s.phase = BattlePhase.Enemy;
        Act(s, Command("M04-1", ActionKind.BasicAttack, "C02"));
        Check(U(s, "C02").currentHp == 0 && s.statistics.everAllyDied && s.statistics.everNearDeath, "A07 full health one hit death fails rating");

        s = Fresh(); U(s, "C02").cell = new Vector2Int(3, 2); U(s, "C01").currentHp = 40;
        Act(s, Command("C02", ActionKind.Heal, "C01")); Check(U(s, "C01").currentHp == 70, "A08 heal30");
        Check(!ForestRules.TryExecute(s, Command("C02", ActionKind.Cleanse, "C01"), out _), "A05 heal and cleanse exclusive");
        s = Fresh(); U(s, "C02").currentHp = 75; U(s, "C02").poisonStacks = 2; U(s, "C02").everNearDeath = true;
        Act(s, Command("C02", ActionKind.Heal, "C02", new Vector2Int(4, 2), new Vector2Int(4, 3)));
        Check(U(s, "C02").currentHp == 80 && U(s, "C02").poisonStacks == 2 && U(s, "C02").everNearDeath, "A08 moved self heal overflow; no cleanse/history reset");
        s = Fresh(); U(s, "C02").poisonStacks = 3; U(s, "C02").scarStacks = 1; s.groundFog.Add(U(s, "C02").cell);
        Act(s, Command("C02", ActionKind.Cleanse, "C02"));
        Check(U(s, "C02").poisonStacks == 0 && U(s, "C02").scarStacks == 1 && s.groundFog.Contains(U(s, "C02").cell), "A08 cleanse self only ordinary poison");
        Next(s); Check(!ForestRules.TryExecute(s, Command("C02", ActionKind.Cleanse, "C02"), out _), "A05 cleanse N+1 unavailable");
        Next(s); Act(s, Command("C02", ActionKind.Cleanse, "C02")); Check(s.round == 3, "A05 cleanse N+2 ready");
        s = Fresh(); s.groundFog.Clear(); s.statistics.initialFogCount = 0; U(s, "C01").cell = new Vector2Int(5, 5); U(s, "C01").poisonStacks = 3;
        var center = new Vector2Int(6, 5); s.Map.terrain[5 * s.Map.width + 6] = 1; s.Map.regions[5 * s.Map.width + 6] = 0;
        for (int y = 4; y <= 6; y++) for (int x = 5; x <= 7; x++) if (s.Map.Walkable(new Vector2Int(x, y))) s.groundFog.Add(new Vector2Int(x, y));
        s.groundFog.Add(new Vector2Int(10, 2));
        var spell = Command("C01", ActionKind.Talisman); spell.targetCell = center;
        var trial = ForestRules.Preview(s, spell, out var trialError);
        Check(trialError == null && trial.groundFog.Count == 9 && s.groundFog.Count == 9 && !U(s, "C01").acted
            && ForestRules.Ability(U(s, "C01"), "talisman").remainingUses == 1, "A08 preview no consumption, blocked center legal, walkable clipping");
        Act(s, spell); Check(s.groundFog.SetEquals(trial.groundFog) && U(s, "C01").poisonStacks == 3
            && s.Map.terrain[5 * s.Map.width + 6] == 1 && s.statistics.clearedFogCount == 0, "A08 preview actual match, no status/terrain change");
        Next(s); Check(!ForestRules.TryExecute(s, spell, out _), "A08 one talisman per battle");
        s = Fresh(); U(s, "C01").cell = new Vector2Int(5, 5); U(s, "M04-1").cell = new Vector2Int(6, 5);
        spell = Command("C01", ActionKind.Talisman); spell.targetCell = U(s, "M04-1").cell; Act(s, spell);
        Check(U(s, "M04-1").currentHp == 0, "A08 occupied center legal, talisman deals damage");

        s = Fresh(); U(s, "C01").cell = new Vector2Int(8, 1); U(s, "M04-1").cell = new Vector2Int(17, 13);
        Check(ForestPathfinder.EntryCost(s, U(s, "C01"), new Vector2Int(9, 1)) == 2, "A09 live roots cost2");
        var area = ForestPathfinder.Reachable(s, U(s, "C01"));
        Check(area.costs[new Vector2Int(9, 1)] == 2 && area.costs[new Vector2Int(10, 1)] == 3, "A09 weighted reachable");
        var rootsPath = new List<Vector2Int> { new Vector2Int(8, 1), new Vector2Int(9, 1), new Vector2Int(9, 2) };
        Check(ForestPathfinder.ValidatePath(s, U(s, "C01"), rootsPath, out _) != null, "A09 weighted path budget");
        U(s, "M01A").lifeState = LifeState.Dead; U(s, "M01A").currentHp = 0;
        Check(ForestPathfinder.ValidatePath(s, U(s, "C01"), rootsPath, out _) == null, "A09 dead source removes costs");
        s = Fresh(edit: d => { d.unitTemplates[0].move = 6; d.rules.rootEntryCost = 4; });
        U(s, "C01").cell = new Vector2Int(8, 1); U(s, "M01A").cell = new Vector2Int(17, 13);
        area = ForestPathfinder.Reachable(s, U(s, "C01"));
        Check(area.costs[new Vector2Int(10, 2)] == 5, "A09 Dijkstra chooses cheaper five-step route over expensive three-step route");
        s = Fresh(stance: FlightStance.Flying); U(s, "C03").cell = new Vector2Int(2, 2); U(s, "C01").cell = new Vector2Int(4, 2); U(s, "C02").cell = new Vector2Int(4, 3);
        s.Map.terrain[2 * s.Map.width + 3] = 1;
        var airPath = new List<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(4, 2), new Vector2Int(5, 2) };
        Check(ForestPathfinder.ValidatePath(s, U(s, "C03"), airPath, out _) == null, "A09 flying crosses obstacle and unit");
        Check(ForestPathfinder.ValidatePath(s, U(s, "C03"), airPath.Take(2).ToList(), out _) != null
            && ForestPathfinder.ValidatePath(s, U(s, "C03"), airPath.Take(3).ToList(), out _) != null, "A09 flying cannot land blocked/occupied");
        Check(!ForestRules.Stats(s, U(s, "C03")).canAttack, "A06 flight no attack");
        U(s, "C03").flight.stance = FlightStance.Grounded;
        Check(ForestPathfinder.ValidatePath(s, U(s, "C03"), airPath, out _) != null, "A09 ground cannot cross obstacle");
        s.Map.terrain[2 * s.Map.width + 3] = 0;
        Check(ForestPathfinder.ValidatePath(s, U(s, "C03"), airPath, out _) != null, "A09 ground cannot cross friendly unit");
        U(s, "C03").flight.stance = FlightStance.Flying; U(s, "C03").poisonStacks = 2;
        Check(ForestRules.Stats(s, U(s, "C03")).move == 4 && ForestRules.Stats(s, U(s, "C03")).attack == 45
            && ForestPathfinder.EntryCost(s, U(s, "C03"), new Vector2Int(9, 1)) == 1, "A09 flying poison penalty and root exception");
        s = Fresh(); var originalRange = ForestPathfinder.Reachable(s, U(s, "C01")).costs;
        s.groundFog.UnionWith(originalRange.Keys);
        Check(ForestPathfinder.Reachable(s, U(s, "C01")).costs.All(p => originalRange[p.Key] == p.Value), "A09 ground fog adds no cost");

        var authored = ForestDefaults.Create(); authored.unitTemplates[0].attack = 101; authored.unitTemplates[0].move = 5;
        Check(ForestAuthoring.Place(authored, "C01", new Vector2Int(1, 10), "C01") == null, "authoring move by stable ID");
        var battle = new ForestSession(authored).State; ForestRules.BeginPlayerPhase(battle);
        var projection = new BattleState(); projection.BindForest(battle, authored);
        Check(projection.units.Count == 11 && projection.units.Single(u => u.id == "C01").stats.id == "C01"
            && projection.units.Single(u => u.id == "C01").stats.attack == 101 && projection.units.Single(u => u.id == "C01").move == 5
            && projection.units.Single(u => u.id == "C01").cell == new Vector2Int(1, 10), "formal edits reach live templates");
        Check(projection.units.Single(u => u.id == "M01A").stats.range == 0 && projection.units.Single(u => u.id == "M02-1").stats.range == 0, "formal no-attack view templates");
        Check(ForestAuthoring.Place(authored, "M04", new Vector2Int(0, 0)) != null && ForestAuthoring.Paint(authored, U(battle, "C01").cell, true) != null, "authoring illegal placement/painting rejected");
        Check(ForestAuthoring.Place(authored, "M04", new Vector2Int(1, 1)) == null && authored.spawns.Last().templateId == "M04", "authoring new monster uses formal template");
        authored.ValidateOrThrow();
        Check(ForestAuthoring.DeleteSpawn(authored, "M01A") != null, "authoring protects root source reference");

        // Free stance skill: scheduling is independent from movement and the major action.
        s = Fresh(); U(s,"C04").cell = new Vector2Int(12,8); var bird = U(s, "C03");
        var switchCommand = Command("C03", ActionKind.SwitchStance);
        var stanceTrial = ForestRules.Preview(s, switchCommand, out var stanceError);
        Check(stanceError == null && stanceTrial.unitsById["C03"].flight.switchRound == 2
            && stanceTrial.unitsById["C03"].flight.stance == FlightStance.Grounded && !stanceTrial.unitsById["C03"].acted
            && bird.flight.switchRound == 0 && ForestRules.Ability(bird, "stance").availableRound == 1, "Free stance preview fully isolated");
        Act(s, switchCommand);
        Check(!bird.acted && bird.statistics.actions == 0 && !s.eventLog.Any(e => e.kind == "ActionEnd")
            && bird.flight.stance == FlightStance.Grounded && bird.flight.pendingStance == FlightStance.Flying
            && bird.flight.switchRound == 2 && ForestRules.Ability(bird, "stance").availableRound == 2, "Free skill queues next round, CD1, consumes no action");
        Check(!ForestRules.TryExecute(s, switchCommand, out _) && bird.flight.switchRound == 2, "Same-round repeat blocked by cooldown");
        U(s, "M04-1").cell = new Vector2Int(3, 4); bird.poisonStacks = 2; s.groundFog.Add(bird.cell);
        Act(s, Command("C03", ActionKind.BasicAttack, "M04-1"));
        Check(bird.acted && bird.statistics.actions == 1 && ForestRules.Stats(s, bird).canAttack, "Free toggle leaves current ground attack available");
        ForestRules.EndPlayerPhase(s); ForestRules.EndEnemyPhase(s);
        Check(s.round == 2 && bird.flight.stance == FlightStance.Grounded && bird.poisonStacks == 3 && bird.currentHp == 56,
            "Queued flight still takes current-round grounded poison after melee counter");
        ForestRules.BeginPlayerPhase(s);
        Check(bird.flight.stance == FlightStance.Flying && bird.flight.switchRound == 0 && !bird.acted
            && !ForestRules.Stats(s, bird).canAttack && ForestRules.Ability(bird, "stance").availableRound <= s.round,
            "Next player start applies flight and restores CD1 skill");
        Act(s, switchCommand);
        Check(bird.flight.stance == FlightStance.Flying && bird.flight.pendingStance == FlightStance.Grounded
            && bird.flight.switchRound == 3 && !bird.acted, "Free landing request works while flying without immediate change");
        Next(s);
        Check(bird.flight.stance == FlightStance.Grounded && bird.currentHp == 47 && bird.poisonStacks == 3
            && ForestRules.Stats(s, bird).canAttack, "Queued landing still takes current flying layer3 damage then lands next turn");
        s.groundFog.Remove(bird.cell); Next(s);
        Check(bird.flight.stance == FlightStance.Grounded && bird.poisonStacks == 2, "No skill means current stance persists");
        s = Fresh(stance: FlightStance.Flying); Next(s);
        Check(U(s, "C03").flight.stance == FlightStance.Flying, "Flight persists without repeated phase choices");
        s = Fresh(); Act(s, Command("C03", ActionKind.Wait)); Act(s, switchCommand);
        Check(U(s, "C03").acted && U(s, "C03").statistics.actions == 1
            && !ForestRules.TryExecute(s, Command("C03", ActionKind.Wait), out _), "Free skill after major action does not restore action rights");
        Check(!ForestRules.TryExecute(s, Command("C02", ActionKind.SwitchStance), out _), "Other characters cannot use stance skill");
        s = Fresh(); U(s,"C04").cell = new Vector2Int(12,8); bird = U(s, "C03"); bird.currentHp = 1; U(s, "M04-1").cell = new Vector2Int(3, 4);
        Act(s, switchCommand); ForestRules.EndPlayerPhase(s); Act(s, Command("M04-1", ActionKind.BasicAttack, "C03"));
        Check(!ForestRules.Alive(bird) && bird.flight.switchRound == 0, "Death clears queued stance");

        s = Fresh(); U(s,"C04").cell = new Vector2Int(12,8); bird = U(s, "C03");
        projection = new BattleState(); projection.BindForest(s, ForestDefaults.Create());
        var birdView = projection.units.Single(u => u.id == "C03"); projection.Select(birdView);
        Check(!projection.CanCancelMovementPreview && projection.Preview(bird.cell) && projection.CanCancelMovementPreview,
            "Same-cell movement preview exposes cancel control");
        projection.Cancel();
        Check(projection.phase == SelectionPhase.Selected && !projection.CanCancelMovementPreview && !bird.acted,
            "Same-cell cancel restores selection without consuming action");
        Check(projection.Preview(new Vector2Int(3, 4)), "Can choose another destination after same-cell cancellation");
        var plannedPath = projection.Path(projection.preview);
        Act(s, Command("C03", ActionKind.SwitchStance, null, plannedPath.ToArray())); projection.SyncForest();
        Check(bird.cell == new Vector2Int(2, 4) && !bird.acted && bird.statistics.actions == 0
            && projection.phase == SelectionPhase.Preview && projection.preview == new Vector2Int(3, 4) && projection.CanCancelMovementPreview,
            "Free skill preserves pending movement without committing it");
        projection.Cancel();
        Check(bird.flight.switchRound == 2 && bird.cell == new Vector2Int(2, 4), "Movement cancellation preserves already-used free skill");
        Check(projection.Preview(new Vector2Int(3, 4)), "Can re-plan movement after free skill");
        Act(s, Command("C03", ActionKind.Wait, null, projection.Path(projection.preview).ToArray()));
        Check(bird.cell == new Vector2Int(3, 4) && bird.acted && bird.statistics.actions == 1, "Movement and one major action remain after free skill");
        var restart = new ForestSession(ForestDefaults.Create()); ForestRules.BeginPlayerPhase(restart.State);
        Act(restart.State, switchCommand); restart.Restart();
        Check(restart.State.unitsById["C03"].flight.stance == FlightStance.Grounded && restart.State.unitsById["C03"].flight.switchRound == 0
            && ForestRules.Ability(restart.State.unitsById["C03"], "stance").availableRound == 1, "Restart clears pending stance and cooldown");
        var legacy = new BattleState(); legacy.Select(legacy.units[0]);
        Check(legacy.Preview(legacy.selected.cell) && legacy.CanCancelMovementPreview, "Legacy self-cell preview can cancel too");
        legacy.Cancel(); Check(legacy.phase == SelectionPhase.Selected && !legacy.CanCancelMovementPreview, "Legacy self-cell cancel restores normal selection");
        return $"PASS M2 ({checks} assertions): A05 actions/phases/CD; A06 integer damage/counters/forms/protection/self damage; A07 poison/flight/death history; A08 skills/preview; A09 weighted pathfinding.\n"
            + "PASS integration model: 8 formal templates / 11 instances; authored stats and placement reach live projection; authoring validation.\n"
            + "PASS regressions: free stance skill, next-round application, CD1, stance persistence/death/restart; self-cell preview cancellation; no committed movement or consumed/restored action.\n"
            + "Scope of this suite: M2 only; resource/phase checks are in the M3 suite.";
    }
}
