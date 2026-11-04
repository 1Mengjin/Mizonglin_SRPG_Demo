using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zilong.Demo;
using Zilong.Forest;

// Pure data checks shared by the installed-Unity standalone runner and the Unity Editor entry point.
public static class ForestDataChecks
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Invalid(Action<ScenarioDefinition> edit, string expected)
    {
        var definition = ForestDefaults.Create(); edit(definition);
        var error = definition.Validate();
        Require(error != null && error.Contains(expected), "Invalid configuration not rejected: " + expected + "; actual=" + error);
        bool threw = false;
        try { new ForestSession(definition); } catch (InvalidDataException) { threw = true; }
        Require(threw, "Invalid configuration initialized: " + expected);
    }

    public static string Run(ScenarioDefinition supplied = null)
    {
        var d = supplied ?? ForestDefaults.Create(); d.ValidateOrThrow();
        Require(d.unitTemplates.Length == 8 && d.spawns.Length == 11, "A01 template/instance counts");
        var expected = new[] {
            "C01|符生|咒师|Ally|Fire|90|75|8|3|3|True|FuSheng", "C02|素铃|祝由|Ally|Light|80|28|6|3|2|True|SuLing",
            "C03|青羽|御风|Ally|Thunder|85|65|10|4|1|True|QingYu", "C04|阿双|铁卫|Ally|Ice|140|45|18|3|1|True|AShuang",
            "M01|树精|雾源|Enemy|Dark|150|0|4|0|0|False|Tree", "M02|吞噬怪|携雾巡逻|Enemy|Dark|100|0|8|1|0|False|Devourer",
            "M03|千面|阶段资源|Enemy|Dark|120|0|5|0|0|False|MaskBoss", "M04|腐卒|追击占位|Enemy|None|60|30|0|2|1|True|Corpse"
        };
        foreach (var row in expected)
        {
            var t = d.unitTemplates.Single(v => v.templateId == row.Split('|')[0]);
            Require($"{t.templateId}|{t.name}|{t.profession}|{t.faction}|{t.element}|{t.maxHp}|{t.attack}|{t.defense}|{t.move}|{t.basicRange}|{t.hasBasicAttack}|{t.behaviorKind}" == row, "A01 " + t.templateId);
        }
        var ts = d.unitTemplates.ToDictionary(t => t.templateId);
        Require(ts["C01"].talisman.castRange == 3 && ts["C01"].talisman.squareSize == 3 && ts["C01"].talisman.maxUses == 1, "Talisman parameters");
        var support = ts["C02"].support;
        Require(support.healRange == 2 && support.healAmount == 30 && support.cleanseRange == 2 && support.cleanseCooldownTurns == 2, "Support parameters");
        Require(ts["C03"].flight.switchCooldownTurns == 1, "Stance skill cooldown");
        var dual = ts["C04"].dualForm;
        Require(dual.cycle == 4 && dual.guardTurns == 2 && dual.guardDefense == 36 && dual.attackMultiplier == 2
            && dual.attackDefense == 0 && dual.attackRange == 2 && dual.selfDamagePercent == 5, "Dual-form parameters");
        var tree = ts["M01"].tree; var devourer = ts["M02"].devourer; var boss = ts["M03"].maskBoss;
        Require(tree.firstBurstRound == 6 && tree.burstInterval == 3 && tree.maxFruitLevel == 3, "Tree parameters");
        Require(devourer.absorbRadius == 2 && devourer.absorbLimit == 6 && !devourer.growthEnabled
            && devourer.growthFogPerTier == 8 && devourer.growthTierLimit == 3, "Devourer parameters");
        Require(boss.phaseCount == 4 && boss.phaseHp == 120 && boss.dropSearchRadius == 3, "Boss parameters");
        var rules = d.rules;
        Require(rules.roundLimit == 99 && rules.poisonMaxStacks == 3 && rules.poisonAttackNumerator == 7 && rules.poisonAttackDenominator == 10
            && rules.poisonMovePenalty == 1 && rules.poisonDamagePercent == 10 && rules.nearDeathPercent == 20
            && rules.spiritCarryLimit == 1 && rules.spiritThrowRange == 4 && rules.spiritClearRadius == 2 && rules.spiritsPerGroundCell == 1
            && rules.purificationCellsPerSpirit == 12 && rules.purificationCellsPerScar == 4 && rules.scarHpPenalty == 10 && rules.rootEntryCost == 2, "Rule parameters");

        var positions = new[] { "C01|C01|3|11", "C02|C02|2|12", "C03|C03|2|11", "C04|C04|4|11",
            "M01A|M01|8|2", "M01B|M01|14|10", "M02-1|M02|15|6", "M03|M03|14|3",
            "M04-1|M04|10|1", "M04-2|M04|10|6", "M04-3|M04|13|12" };
        foreach (var row in positions)
        {
            var s = d.spawns.Single(v => v.instanceId == row.Split('|')[0]);
            Require($"{s.instanceId}|{s.templateId}|{s.cell.x}|{s.cell.y}" == row, "A02 " + s.instanceId);
        }
        var map = d.map; var oldGeometry = LevelMap.Forest();
        Require(map.width == 18 && map.height == 14 && map.terrain.Count(t => t == 0) == 149
            && map.initialFog.Count(f => f) == 15 && map.topLeftCoordinates, "A02 map counts");
        Require(map.terrain.SequenceEqual(oldGeometry.tiles) && map.regions.SequenceEqual(oldGeometry.regions)
            && map.initialFog.SequenceEqual(oldGeometry.initialFog), "A02 geometry preserved");
        Require(map.rootCells.SequenceEqual(new[] { new Vector2Int(9, 1), new Vector2Int(9, 2) }) && d.rootSourceInstanceId == "M01A", "A02 roots");
        Require(map.patrolPoints.SequenceEqual(new[] { new Vector2Int(15, 6), new Vector2Int(16, 6), new Vector2Int(16, 7), new Vector2Int(15, 7) }), "A02 patrol");
        var reached = new HashSet<Vector2Int> { d.spawns[0].cell }; var queue = new Queue<Vector2Int>(reached);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var step in new[] { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) })
                if (map.Walkable(p + step) && reached.Add(p + step)) queue.Enqueue(p + step);
        }
        Require(reached.Count == 149, "A02 connectivity");

        var source = d.Copy(); var session = new ForestSession(source); var other = new ForestSession(source);
        var state = session.State; CheckInitial(state);
        Require(!ReferenceEquals(state.Map, source.map) && !ReferenceEquals(state.Rules, source.rules), "A03 config snapshot");
        state.unitsById["C01"].currentHp = 1; state.unitsById["C01"].acted = true;
        state.unitsById["C01"].cell = new Vector2Int(1, 10); state.unitsById["C01"].poisonStacks = 3;
        state.unitsById["C01"].scarStacks = 3; state.unitsById["C01"].everNearDeath = true;
        state.unitsById["C01"].carriedSpiritId = "test"; state.unitsById["C01"].abilityStates[0].remainingUses = 0;
        state.unitsById["C01"].purificationCharges.Add(new PurificationCharge { sourceSpiritId = "spent", clearedCount = 8 });
        state.unitsById["C01"].statistics.actions = 5;
        state.unitsById["C02"].abilityStates[1].availableRound = 14;
        state.unitsById["C03"].flight.stance = FlightStance.Flying;
        state.unitsById["C03"].flight.pendingStance = FlightStance.Grounded;
        state.unitsById["C03"].flight.switchRound = 14;
        state.unitsById["C03"].abilityStates[0].availableRound = 14;
        state.unitsById["C04"].dualForm.form = DualForm.Attack;
        state.unitsById["C04"].dualForm.protectedUnitId = "C01";
        state.unitsById["M01A"].tree.fruitLevel = 3;
        Require(state.unitsById["M01B"].tree.fruitLevel == 1, "A03 two trees share no state");
        state.unitsById["M02-1"].lifeState = LifeState.Dead; state.unitsById["M02-1"].devourer.carriedFogCount = 7;
        Require(state.CarriedFogCount == 7 && state.At(new Vector2Int(15, 6)) == null, "Dead carried-fog record retained without occupancy");
        state.unitsById["M03"].maskBoss.phaseIndex = 4; state.unitsById["M03"].maskBoss.resolvedPhaseMask = 7;
        state.groundFog.Clear(); state.Map.terrain[0] = 0; state.Map.regions[0] = 1;
        state.Map.initialFog[0] = true; state.Map.rootCells[0] = new Vector2Int(1, 1);
        state.Map.patrolPoints[0] = new Vector2Int(2, 2); state.Rules.roundLimit = 7;
        state.round = 14; state.phase = BattlePhase.Finished; state.result = BattleResult.Defeat; state.revision = 99;
        state.spiritsById.Add("test", new SpiritState()); state.pendingDrops.Add(new PendingDrop()); state.eventLog.Add(new BattleEvent());
        state.statistics.everNearDeath = state.statistics.everAllyDied = true;
        state.statistics.generatedFogCount = 7; state.statistics.clearedFogCount = 5; state.statistics.elapsedSeconds = 400;
        state.statistics.sourceDefeatedRounds.Add("M01A", 6);
        CheckInitial(other.State);
        Require(source.map.terrain[0] == 1 && source.rules.roundLimit == 99 && source.unitTemplates[0].maxHp == 90, "A03 no writeback");
        var detached = other.State.GetTemplate("M01"); detached.maxHp = 1; detached.tree.firstBurstRound = 99;
        Require(other.State.GetTemplate("M01").maxHp == 150 && other.State.GetTemplate("M01").tree.firstBurstRound == 6, "A03 template lookup detached");
        // Editing configuration after session creation does not change this session's restart snapshot.
        source.unitTemplates[0].maxHp = 12; source.rules.roundLimit = 8; source.map.terrain[0] = 0;
        source.spawns[0].cell = new Vector2Int(1, 10); source.unitTemplates[4].tree.firstBurstRound = 3;
        for (int i = 0; i < 3; i++) { session.Restart(); CheckInitial(session.State); }
        Require(!ReferenceEquals(state, session.State), "A03 restart replaces old state");
        CheckInitial(new ForestSession(ForestDefaults.Create()).State);

        Invalid(v => v.schemaVersion = 0, "版本"); Invalid(v => v.rulesetId = "legacy", "版本");
        Invalid(v => v.map = null, "主体"); Invalid(v => v.rules = null, "主体");
        Invalid(v => v.unitTemplates[1].templateId = "C01", "重复");
        Invalid(v => v.spawns[1].instanceId = "C01", "重复");
        Invalid(v => v.spawns[0].templateId = "unknown", "未知模板");
        Invalid(v => v.spawns[0].cell = new Vector2Int(18, 0), "出生点");
        Invalid(v => v.spawns[0].cell = new Vector2Int(0, 0), "出生点");
        Invalid(v => v.spawns[1].cell = v.spawns[0].cell, "出生点");
        Invalid(v => v.map.initialFog = new bool[1], "图层");
        Invalid(v => v.map.terrain = null, "图层"); Invalid(v => v.map.regions = new int[1], "图层");
        Invalid(v => v.map.initialFog[0] = true, "障碍"); Invalid(v => v.map.regions[0] = 100, "区域");
        Invalid(v => v.map.terrain[0] = 2, "地形"); Invalid(v => v.map.width = int.MaxValue, "尺寸");
        Invalid(v => v.map.rootCells[1] = v.map.rootCells[0], "根须");
        Invalid(v => v.rootSourceInstanceId = "M03", "根须来源");
        Invalid(v => v.rootSourceInstanceId = "missing", "根须来源");
        Invalid(v => v.map.patrolPoints[0] = new Vector2Int(1, 1), "巡逻");
        Invalid(v => v.map.patrolPoints = Array.Empty<Vector2Int>(), "巡逻");
        Invalid(v => v.unitTemplates[0].maxHp = 0, "属性"); Invalid(v => v.unitTemplates[0].attack = -1, "属性");
        Invalid(v => v.unitTemplates[0].basicRange = 0, "射程");
        Invalid(v => v.unitTemplates[0].element = (Element)999, "属相");
        Invalid(v => v.unitTemplates[0].faction = Faction.Enemy, "阵营");
        Invalid(v => v.unitTemplates[0].talisman = null, "符箓");
        Invalid(v => v.unitTemplates[2].flight.flying.canAttack = true, "权限");
        Invalid(v => v.unitTemplates[2].flight.switchCooldownTurns = 0, "冷却");
        Invalid(v => v.unitTemplates[4].hasBasicAttack = true, "攻击");
        Invalid(v => v.unitTemplates[5].devourer.growthEnabled = true, "增长");
        Invalid(v => v.unitTemplates[6].maskBoss.phaseHp = 119, "阶段");
        Invalid(v => v.rules.poisonAttackDenominator = 0, "瘴毒");
        Invalid(v => v.rules.spiritsPerGroundCell = 2, "元神");
        Invalid(v => v.unitTemplates[0] = null, "模板为空"); Invalid(v => v.spawns[0] = null, "实例编号");

        // Existing code paths are deliberately not migrated to C/M templates in milestone 1.
        FighterBook.Defaults().Validate();
        foreach (var legacyMap in new[] { LevelMap.Default(), LevelMap.Forest() })
        {
            Require(legacyMap.Validate() == null, "A04 legacy map validation");
        }
        var legacyState = new BattleState(); FighterBook.Defaults().Apply(legacyState, LevelMap.Default());
        Require(legacyState.units.Count == 7 && legacyState.units.All(u => u.stats != null), "A04 legacy initialization");
        return "PASS A01: 8 templates / 11 instances and all preset parameters.\n"
            + "PASS A02: 18x14 / 149 connected walkable cells / 15 fog / official spawns / roots / patrol.\n"
            + "PASS A03: deep isolation, detached templates, independent trees, dead fog retained, full restart (3 times).\n"
            + "PASS A04: invalid schemas, attributes, references, map layers and placements rejected; legacy factories validate and original units initialize.\n"
            + "Scope: data and initialization only; no skill, phase advancement, AI or victory implementation.";
    }

    static void CheckInitial(ForestBattleState state)
    {
        Require(state.round == 1 && state.phase == BattlePhase.PlayerStart && state.result == BattleResult.InProgress
            && state.revision == 0 && state.unitsById.Count == 11, "A03 initial battle state");
        foreach (var unit in state.unitsById.Values)
        {
            var t = state.GetTemplate(unit.templateId);
            Require(unit.currentHp == t.maxHp && !unit.acted && unit.lifeState == LifeState.Alive
                && unit.poisonStacks == 0 && unit.scarStacks == 0 && !unit.everNearDeath && unit.carriedSpiritId == null
                && unit.purificationCharges.Count == 0 && unit.statistics.actions == 0, "A03 initial unit " + unit.instanceId);
            Require(state.At(unit.cell) == unit, "A03 occupancy " + unit.instanceId);
        }
        Require(state.unitsById["C01"].currentHp == 90 && state.unitsById["C01"].cell == new Vector2Int(3, 11)
            && state.unitsById["C01"].abilityStates.Single(a => a.abilityId == "talisman").remainingUses == 1, "A03 talisman reset");
        Require(state.unitsById["C02"].abilityStates.All(a => a.availableRound == 1 && a.remainingUses == -1), "A03 support ready");
        Require(state.unitsById["C03"].flight.stance == FlightStance.Grounded && state.unitsById["C03"].flight.stanceLockedRound == 0
            && state.unitsById["C03"].flight.switchRound == 0 && state.unitsById["C03"].abilityStates.Single(a => a.abilityId == "stance").availableRound == 1, "A03 initial stance and free skill");
        Require(state.unitsById["C04"].dualForm.form == DualForm.Guard && state.unitsById["C04"].dualForm.protectedUnitId == null, "A03 initial form");
        Require(state.unitsById["M01A"].tree.fruitLevel == 1 && state.unitsById["M01A"].tree.nextBurstRound == 6
            && state.unitsById["M01B"].tree.fruitLevel == 1, "A03 initial trees");
        Require(state.unitsById["M02-1"].devourer.nextPatrolIndex == 1 && state.Map.patrolPoints[1] == new Vector2Int(16, 6)
            && state.CarriedFogCount == 0 && state.LivingSourceCount == 2, "A03 initial pollution records");
        Require(state.unitsById["M03"].maskBoss.phaseIndex == 1 && state.unitsById["M03"].maskBoss.resolvedPhaseMask == 0, "A03 initial boss");
        Require(state.groundFog.Count == 15 && state.Map.terrain[0] == 1 && state.Map.regions[0] == 0 && !state.Map.initialFog[0]
            && state.Map.rootCells[0] == new Vector2Int(9, 1) && state.Map.patrolPoints[0] == new Vector2Int(15, 6)
            && state.Rules.roundLimit == 99, "A03 independent map/rules");
        Require(state.spiritsById.Count == 0 && state.pendingDrops.Count == 0 && state.eventLog.Count == 0
            && !state.statistics.everNearDeath && !state.statistics.everAllyDied && state.statistics.initialFogCount == 15
            && state.statistics.generatedFogCount == 0 && state.statistics.clearedFogCount == 0
            && state.statistics.elapsedSeconds == 0 && state.statistics.sourceDefeatedRounds.Count == 0, "A03 empty history");
    }
}
