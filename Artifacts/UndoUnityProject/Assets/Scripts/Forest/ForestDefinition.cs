using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zilong.Forest
{
    public enum Faction { Ally, Enemy }
    public enum Element { None, Ice, Fire, Thunder, Light, Dark }
    public enum BehaviorKind { FuSheng, SuLing, QingYu, AShuang, Tree, Devourer, MaskBoss, Corpse }

    [Serializable]
    public sealed class BattleRules
    {
        public int roundLimit, poisonMaxStacks, poisonAttackNumerator, poisonAttackDenominator;
        public int poisonMovePenalty, poisonDamagePercent, nearDeathPercent;
        public int spiritCarryLimit, spiritThrowRange, spiritClearRadius, spiritsPerGroundCell;
        public int purificationCellsPerSpirit, purificationCellsPerScar, scarHpPenalty, rootEntryCost;
        public BattleRules Copy() => (BattleRules)MemberwiseClone();

        public string Validate()
        {
            if (roundLimit < 1 || poisonMaxStacks < 1 || poisonAttackDenominator < 1
                || poisonAttackNumerator < 0 || poisonAttackNumerator > poisonAttackDenominator
                || poisonMovePenalty < 0 || poisonDamagePercent < 1 || poisonDamagePercent > 100
                || nearDeathPercent < 1 || nearDeathPercent > 100) return "回合或瘴毒参数无效";
            if (spiritCarryLimit != 1 || spiritsPerGroundCell != 1 || spiritThrowRange < 1
                || spiritClearRadius < 0 || purificationCellsPerSpirit < 1 || purificationCellsPerScar < 1
                || purificationCellsPerSpirit % purificationCellsPerScar != 0 || scarHpPenalty < 1
                || rootEntryCost < 1) return "元神、净行或根须参数无效";
            return null;
        }
    }

    [Serializable] public sealed class TalismanParameters
    {
        public int castRange, squareSize, maxUses;
        public int echoRange = 6;
        public TalismanParameters Copy() => (TalismanParameters)MemberwiseClone();
    }
    [Serializable] public sealed class SupportParameters
    {
        public int healRange, healAmount, cleanseRange, cleanseCooldownTurns;
        public SupportParameters Copy() => (SupportParameters)MemberwiseClone();
    }
    [Serializable] public sealed class FlightPermissions
    {
        public bool crossBlockedTerrain, crossUnits, ignoreRootExtraCost;
        public bool canAttack, canCounter, canPickUp, canThrow, canSwallow, canPurify, poisonLayersChange;
        public FlightPermissions Copy() => (FlightPermissions)MemberwiseClone();
    }
    [Serializable] public sealed class FlightParameters
    {
        public int switchCooldownTurns = 1;
        public FlightPermissions grounded, flying;
        public FlightParameters Copy() => new FlightParameters { switchCooldownTurns = switchCooldownTurns,
            grounded = grounded?.Copy(), flying = flying?.Copy() };
    }
    [Serializable] public sealed class DualFormParameters
    {
        public int cycle, guardTurns, guardDefense, attackMultiplier, attackDefense, attackRange, selfDamagePercent;
        public DualFormParameters Copy() => (DualFormParameters)MemberwiseClone();
    }
    [Serializable] public sealed class TreeParameters
    {
        public int firstBurstRound, burstInterval, maxFruitLevel;
        public TreeParameters Copy() => (TreeParameters)MemberwiseClone();
    }
    [Serializable] public sealed class DevourerParameters
    {
        public int absorbRadius, absorbLimit, growthFogPerTier, growthTierLimit;
        public bool growthEnabled;
        public DevourerParameters Copy() => (DevourerParameters)MemberwiseClone();
    }
    [Serializable] public sealed class MaskBossParameters
    {
        public int phaseCount, phaseHp, dropSearchRadius;
        public MaskBossParameters Copy() => (MaskBossParameters)MemberwiseClone();
    }

    [Serializable]
    public sealed class UnitTemplate
    {
        public string templateId, name, profession;
        public Sprite portrait;
        public Faction faction;
        public Element element;
        public BehaviorKind behaviorKind;
        public int maxHp, attack, defense, move, basicRange;
        public bool hasBasicAttack;
        public TalismanParameters talisman;
        public SupportParameters support;
        public FlightParameters flight;
        public DualFormParameters dualForm;
        public TreeParameters tree;
        public DevourerParameters devourer;
        public MaskBossParameters maskBoss;

        public UnitTemplate Copy()
        {
            var copy = (UnitTemplate)MemberwiseClone();
            // Portraits are shared read-only Unity assets; all mutable parameter objects are copied.
            copy.talisman = talisman?.Copy(); copy.support = support?.Copy(); copy.flight = flight?.Copy();
            copy.dualForm = dualForm?.Copy(); copy.tree = tree?.Copy();
            copy.devourer = devourer?.Copy(); copy.maskBoss = maskBoss?.Copy();
            return copy;
        }

        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(templateId) || string.IsNullOrWhiteSpace(name)
                || string.IsNullOrWhiteSpace(profession)) return "模板编号、名称和职业不能为空";
            if (!Enum.IsDefined(typeof(Faction), faction) || !Enum.IsDefined(typeof(Element), element)
                || !Enum.IsDefined(typeof(BehaviorKind), behaviorKind)) return "阵营、属相或行为无效";
            bool player = behaviorKind <= BehaviorKind.AShuang;
            if (player != (faction == Faction.Ally) || !templateId.StartsWith(player ? "C" : "M", StringComparison.Ordinal))
                return "模板编号、行为与阵营不一致";
            if (maxHp < 1 || attack < 0 || defense < 0 || move < 0 || basicRange < 0) return "基础属性无效";
            bool noAttack = behaviorKind == BehaviorKind.Tree || behaviorKind == BehaviorKind.Devourer || behaviorKind == BehaviorKind.MaskBoss;
            if (hasBasicAttack == noAttack || (hasBasicAttack ? basicRange < 1 : attack != 0 || basicRange != 0))
                return "无攻击动作必须明确关闭且攻击/射程为0；有攻击动作的射程必须为正";
            switch (behaviorKind)
            {
                case BehaviorKind.FuSheng:
                    if (talisman == null || talisman.castRange < 1 || talisman.squareSize < 1
                        || talisman.squareSize % 2 == 0 || talisman.maxUses < 1 || talisman.echoRange < 1) return "符箓参数无效";
                    break;
                case BehaviorKind.SuLing:
                    if (support == null || support.healRange < 0 || support.healAmount < 1
                        || support.cleanseRange < 0 || support.cleanseCooldownTurns < 1) return "治疗或涤尘参数无效";
                    break;
                case BehaviorKind.QingYu:
                    if (flight?.grounded == null || flight.flying == null) return "飞行/落地权限配置缺失";
                    if (flight.switchCooldownTurns < 1) return "姿态切换冷却必须至少1回合";
                    var air = flight.flying; var ground = flight.grounded;
                    if (!air.crossBlockedTerrain || !air.crossUnits || !air.ignoreRootExtraCost
                        || air.canAttack || air.canCounter || air.canPickUp || air.canThrow || air.canSwallow
                        || air.canPurify || air.poisonLayersChange || ground.crossBlockedTerrain || ground.crossUnits
                        || ground.ignoreRootExtraCost || !ground.canAttack || !ground.canCounter || !ground.canPickUp
                        || !ground.canThrow || !ground.canSwallow || !ground.canPurify || !ground.poisonLayersChange)
                        return "v1.0飞行/落地权限与定案不一致";
                    break;
                case BehaviorKind.AShuang:
                    if (dualForm == null || dualForm.cycle < 2 || dualForm.guardTurns < 1
                        || dualForm.guardTurns >= dualForm.cycle || dualForm.guardDefense < 0
                        || dualForm.attackMultiplier < 1 || dualForm.attackDefense < 0
                        || dualForm.attackRange < 1 || dualForm.selfDamagePercent < 1
                        || dualForm.selfDamagePercent > 100) return "攻守形态参数无效";
                    break;
                case BehaviorKind.Tree:
                    if (tree == null || tree.firstBurstRound < 1 || tree.burstInterval < 1
                        || tree.maxFruitLevel < 1 || move != 0) return "树精参数无效";
                    break;
                case BehaviorKind.Devourer:
                    if (devourer == null || devourer.absorbRadius < 0 || devourer.absorbLimit < 1
                        || devourer.growthFogPerTier < 1 || devourer.growthTierLimit < 1 || move != 1)
                        return "吞噬怪参数无效";
                    if (devourer.growthEnabled) return "v1.0未定义增长增量，不支持启用吞噬怪增长";
                    break;
                case BehaviorKind.MaskBoss:
                    if (maskBoss == null || maskBoss.phaseCount < 1 || maskBoss.phaseCount > 31
                        || maskBoss.phaseHp != maxHp || maskBoss.dropSearchRadius < 1 || move != 0)
                        return "千面阶段参数无效（阶段生命须等于模板最大生命）";
                    break;
            }
            return null;
        }
    }

    [Serializable]
    public sealed class MapDefinition
    {
        public int width, height;
        public bool topLeftCoordinates;
        public int[] terrain, regions;
        public bool[] initialFog;
        public Vector2Int[] rootCells, patrolPoints;
        public Vector2Int[] rewardCells = new Vector2Int[0];
        public bool Inside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
        public bool Walkable(Vector2Int p) => Inside(p) && terrain[p.y * width + p.x] == 0;
        public MapDefinition Copy() => new MapDefinition {
            width = width, height = height, topLeftCoordinates = topLeftCoordinates,
            terrain = (int[])terrain?.Clone(), regions = (int[])regions?.Clone(), initialFog = (bool[])initialFog?.Clone(),
            rewardCells = (Vector2Int[])rewardCells?.Clone(),
            rootCells = (Vector2Int[])rootCells?.Clone(), patrolPoints = (Vector2Int[])patrolPoints?.Clone()
        };

        public string Validate()
        {
            if (width < 1 || height < 1 || width > 64 || height > 64 || !topLeftCoordinates)
                return "地图尺寸应为1至64，坐标原点须为左上角";
            int size = width * height;
            if (terrain == null || regions == null || initialFog == null || terrain.Length != size
                || regions.Length != size || initialFog.Length != size) return "地形、区域或初始雾图层尺寸不匹配";
            bool anyWalkable = false;
            for (int i = 0; i < size; i++)
            {
                if (terrain[i] != 0 && terrain[i] != 1) return "迷踪林仅支持普通/障碍地形，不沿用旧危险格伤害";
                if (regions[i] < 0 || regions[i] > 10) return "地图区域编号无效";
                if (terrain[i] == 1 && (initialFog[i] || regions[i] != 0)) return "障碍格不能有初始雾或可走区域归属";
                if (terrain[i] == 0 && regions[i] == 0) return "可走格必须有区域归属";
                anyWalkable |= terrain[i] == 0;
            }
            if (!anyWalkable) return "地图没有可走格";
            if (rootCells == null || patrolPoints == null) return "根须或巡逻配置缺失";
            var unique = new HashSet<Vector2Int>();
            foreach (var cell in rewardCells ?? new Vector2Int[0])
                if (!Walkable(cell) || !unique.Add(cell)) return "奖励地块越界、重复或不可走";
            unique.Clear();
            foreach (var cell in rootCells)
                if (!Walkable(cell) || !unique.Add(cell)) return "根须格越界、重复或不可走";
            unique.Clear();
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                var cell = patrolPoints[i];
                if (!Walkable(cell) || !unique.Add(cell)) return "巡逻格越界、重复或不可走";
                var next = patrolPoints[(i + 1) % patrolPoints.Length];
                if (Math.Abs(cell.x - next.x) + Math.Abs(cell.y - next.y) != 1) return "巡逻点必须按顺序组成四邻接闭环";
            }
            return null;
        }
    }

    [Serializable]
    public sealed class SpawnDefinition
    {
        public string instanceId, templateId;
        public Vector2Int cell;
        public SpawnDefinition Copy() => (SpawnDefinition)MemberwiseClone();
    }

    [Serializable]
    public sealed class ScenarioDefinition
    {
        public const int CurrentSchemaVersion = 1;
        public const string ForestRulesetId = "forest-v1.0";
        public int schemaVersion;
        public string scenarioId, rulesetId, rootSourceInstanceId;
        public MapDefinition map;
        public UnitTemplate[] unitTemplates;
        public SpawnDefinition[] spawns;
        public BattleRules rules;

        public ScenarioDefinition Copy()
        {
            var copy = (ScenarioDefinition)MemberwiseClone();
            copy.map = map?.Copy(); copy.rules = rules?.Copy();
            copy.unitTemplates = unitTemplates == null ? null : Array.ConvertAll(unitTemplates, t => t?.Copy());
            copy.spawns = spawns == null ? null : Array.ConvertAll(spawns, s => s?.Copy());
            return copy;
        }

        public string Validate()
        {
            if (schemaVersion != CurrentSchemaVersion || rulesetId != ForestRulesetId
                || string.IsNullOrWhiteSpace(scenarioId)) return "配置版本、规则版本或关卡编号无效";
            if (map == null || rules == null || unitTemplates == null || spawns == null) return "配置主体缺失";
            var error = map.Validate() ?? rules.Validate();
            if (error != null) return error;
            if (unitTemplates.Length == 0 || spawns.Length == 0) return "单位模板或出生点为空";
            var templates = new Dictionary<string, UnitTemplate>(StringComparer.Ordinal);
            foreach (var template in unitTemplates)
            {
                if (template == null) return "单位模板为空";
                error = template.Validate();
                if (error != null) return template.templateId + ": " + error;
                if (templates.ContainsKey(template.templateId)) return "模板编号重复：" + template.templateId;
                templates.Add(template.templateId, template);
            }
            var ids = new HashSet<string>(StringComparer.Ordinal); var cells = new HashSet<Vector2Int>();
            int allies = 0, enemies = 0; bool rootSourceFound = false;
            foreach (var spawn in spawns)
            {
                if (spawn == null || string.IsNullOrWhiteSpace(spawn.instanceId) || !ids.Add(spawn.instanceId))
                    return "实例编号为空或重复";
                if (spawn.templateId == null || !templates.TryGetValue(spawn.templateId, out var template))
                    return "出生点引用未知模板：" + spawn.instanceId;
                if (!spawn.instanceId.StartsWith(template.faction == Faction.Ally ? "C" : "M", StringComparison.Ordinal))
                    return "实例编号与阵营不一致：" + spawn.instanceId;
                if (!map.Walkable(spawn.cell) || !cells.Add(spawn.cell)) return "出生点越界、重叠或位于障碍：" + spawn.instanceId;
                if (template.faction == Faction.Ally) allies++; else enemies++;
                if (spawn.instanceId == rootSourceInstanceId)
                {
                    if (template.behaviorKind != BehaviorKind.Tree) return "根须来源必须是树精实例";
                    rootSourceFound = true;
                }
                if (template.behaviorKind == BehaviorKind.Devourer
                    && (map.patrolPoints.Length < 2 || Array.IndexOf(map.patrolPoints, spawn.cell) < 0))
                    return "吞噬怪出生点必须位于巡逻闭环";
            }
            if (allies == 0 || enemies == 0) return "需要至少一个我方和一个敌方实例";
            if (map.rootCells.Length > 0 && !rootSourceFound) return "根须来源实例不存在";
            return null;
        }

        public void ValidateOrThrow()
        {
            var error = Validate();
            if (error != null) throw new InvalidDataException(error);
        }
    }
}
