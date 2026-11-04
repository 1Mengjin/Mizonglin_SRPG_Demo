using UnityEngine;
using Mizonglin.Demo;

namespace Mizonglin.Forest
{
    public static class ForestDefaults
    {
        static UnitTemplate Unit(string id, string name, string profession, Faction faction, Element element,
            BehaviorKind behavior, int hp, int attack, int defense, int move, int range) => new UnitTemplate {
            templateId = id, name = name, profession = profession, faction = faction, element = element,
            behaviorKind = behavior, maxHp = hp, attack = attack, defense = defense, move = move,
            basicRange = range, hasBasicAttack = range > 0
        };
        static SpawnDefinition Spawn(string id, string template, int x, int y) => new SpawnDefinition {
            instanceId = id, templateId = template, cell = new Vector2Int(x, y)
        };

        public static ScenarioDefinition Create()
        {
            // Reuse geometry only. The legacy map's U/E/F spawns and its goal/hazard rules stay in the old Demo.
            var geometry = LevelMap.Forest();
            var templates = new[] {
                Unit("C01", "符生", "咒师", Faction.Ally, Element.Fire, BehaviorKind.FuSheng, 90, 75, 8, 3, 3),
                Unit("C02", "素铃", "祝由", Faction.Ally, Element.Light, BehaviorKind.SuLing, 80, 28, 6, 3, 2),
                Unit("C03", "青羽", "御风", Faction.Ally, Element.Thunder, BehaviorKind.QingYu, 85, 65, 10, 4, 1),
                Unit("C04", "阿双", "铁卫", Faction.Ally, Element.Ice, BehaviorKind.AShuang, 140, 45, 18, 3, 1),
                Unit("M01", "树精", "雾源", Faction.Enemy, Element.Dark, BehaviorKind.Tree, 150, 0, 4, 0, 0),
                Unit("M02", "吞噬怪", "携雾巡逻", Faction.Enemy, Element.Dark, BehaviorKind.Devourer, 100, 0, 8, 1, 0),
                Unit("M03", "千面", "阶段资源", Faction.Enemy, Element.Dark, BehaviorKind.MaskBoss, 120, 0, 5, 0, 0),
                Unit("M04", "腐卒", "追击占位", Faction.Enemy, Element.None, BehaviorKind.Corpse, 60, 30, 0, 2, 1)
            };
            templates[0].talisman = new TalismanParameters { castRange = 3, squareSize = 3, maxUses = 1 };
            templates[1].support = new SupportParameters { healRange = 2, healAmount = 30, cleanseRange = 2, cleanseCooldownTurns = 2 };
            templates[2].flight = new FlightParameters {
                switchCooldownTurns = 1,
                grounded = new FlightPermissions { canAttack = true, canCounter = true, canPickUp = true,
                    canThrow = true, canSwallow = true, canPurify = true, poisonLayersChange = true },
                flying = new FlightPermissions { crossBlockedTerrain = true, crossUnits = true, ignoreRootExtraCost = true }
            };
            templates[3].dualForm = new DualFormParameters { cycle = 4, guardTurns = 2, guardDefense = 36,
                attackMultiplier = 2, attackDefense = 0, attackRange = 2, selfDamagePercent = 5 };
            templates[4].tree = new TreeParameters { firstBurstRound = 6, burstInterval = 3, maxFruitLevel = 3 };
            templates[5].devourer = new DevourerParameters { absorbRadius = 2, absorbLimit = 6,
                growthEnabled = false, growthFogPerTier = 8, growthTierLimit = 3 };
            templates[6].maskBoss = new MaskBossParameters { phaseCount = 4, phaseHp = 120, dropSearchRadius = 3 };
            return new ScenarioDefinition {
                schemaVersion = ScenarioDefinition.CurrentSchemaVersion, scenarioId = "mist-forest",
                rulesetId = ScenarioDefinition.ForestRulesetId, rootSourceInstanceId = "M01A",
                map = new MapDefinition { width = geometry.width, height = geometry.height, topLeftCoordinates = true,
                    terrain = (int[])geometry.tiles.Clone(), regions = (int[])geometry.regions.Clone(),
                    initialFog = (bool[])geometry.initialFog.Clone(), rootCells = geometry.rootCandidates.ToArray(),
                    rewardCells = new[] { new Vector2Int(4, 10) },
                    patrolPoints = geometry.patrol.ToArray() },
                unitTemplates = templates,
                spawns = new[] {
                    Spawn("C01", "C01", 3, 11), Spawn("C02", "C02", 2, 12),
                    Spawn("C03", "C03", 2, 11), Spawn("C04", "C04", 4, 11),
                    Spawn("M01A", "M01", 8, 2), Spawn("M01B", "M01", 14, 10),
                    Spawn("M02-1", "M02", 15, 6), Spawn("M03", "M03", 14, 3),
                    Spawn("M04-1", "M04", 10, 1), Spawn("M04-2", "M04", 10, 6), Spawn("M04-3", "M04", 13, 12)
                },
                rules = new BattleRules { roundLimit = 99, poisonMaxStacks = 3, poisonAttackNumerator = 7,
                    poisonAttackDenominator = 10, poisonMovePenalty = 1, poisonDamagePercent = 10,
                    nearDeathPercent = 20, spiritCarryLimit = 1, spiritThrowRange = 4, spiritClearRadius = 2,
                    spiritsPerGroundCell = 1, purificationCellsPerSpirit = 12, purificationCellsPerScar = 4,
                    scarHpPenalty = 10, rootEntryCost = 2 }
            };
        }
    }
}
