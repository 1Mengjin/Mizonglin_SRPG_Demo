using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zilong.Demo;

namespace Zilong.Forest
{
    public static class ForestAuthoring
    {
        public static string Paint(ScenarioDefinition definition, Vector2Int cell, bool blocked)
        {
            var map = definition.map;
            if (!map.Inside(cell)) return "格子越界";
            if (blocked && definition.spawns.Any(s => s.cell == cell)) return "请先移动或删除此格出生点";
            int i = cell.y * map.width + cell.x; map.terrain[i] = blocked ? 1 : 0;
            if (blocked)
            {
                map.regions[i] = 0; map.initialFog[i] = false;
                map.rewardCells = (map.rewardCells ?? new Vector2Int[0]).Where(p => p != cell).ToArray();
                map.rootCells = map.rootCells.Where(p => p != cell).ToArray();
                map.patrolPoints = map.patrolPoints.Where(p => p != cell).ToArray();
            }
            else if (map.regions[i] == 0) map.regions[i] = (int)MapRegion.中庭;
            return null;
        }
        public static string Place(ScenarioDefinition definition, string templateId, Vector2Int cell, string movingId = null)
        {
            var t = Array.Find(definition.unitTemplates, v => v.templateId == templateId);
            if (t == null) return "模板不存在";
            if (!definition.map.Walkable(cell)) return "出生点必须位于可走格";
            if (definition.spawns.Any(s => s.cell == cell && s.instanceId != movingId)) return "该格已有单位出生点";
            if (t.behaviorKind == BehaviorKind.Devourer && Array.IndexOf(definition.map.patrolPoints, cell) < 0)
                return "吞噬怪必须出生在巡逻点上，请先设置巡逻点";
            var spawn = movingId == null ? null : Array.Find(definition.spawns, s => s.instanceId == movingId);
            if (movingId != null && spawn == null) return "要移动的出生点不存在";
            if (spawn == null)
            {
                string id = templateId; int suffix = 1;
                while (definition.spawns.Any(s => s.instanceId == id)) id = templateId + "-" + (suffix++).ToString("00");
                spawn = new SpawnDefinition { instanceId = id, templateId = templateId };
                var all = new List<SpawnDefinition>(definition.spawns) { spawn }; definition.spawns = all.ToArray();
            }
            spawn.cell = cell; return null;
        }
        public static string DeleteSpawn(ScenarioDefinition definition, string id)
        {
            if (id == definition.rootSourceInstanceId && definition.map.rootCells.Length > 0)
                return "此实例是根须来源；请先更换根须来源或移除根须标记";
            definition.spawns = definition.spawns.Where(s => s.instanceId != id).ToArray(); return null;
        }

        // Rendering-only DTO for the existing board. Never validate/save this as the legacy seven-template scenario.
        public static LevelMap ViewMap(ScenarioDefinition definition) => new LevelMap {
            version = 2, width = definition.map.width, height = definition.map.height,
            tiles = (int[])definition.map.terrain.Clone(), regions = (int[])definition.map.regions.Clone(),
            initialFog = (bool[])definition.map.initialFog.Clone(), topLeftCoordinates = true, hasGoal = false, hazardPercent = 0,
            rootCandidates = new List<Vector2Int>(definition.map.rootCells), patrol = new List<Vector2Int>(definition.map.patrolPoints),
            spawns = definition.spawns.Select(s => new SpawnPoint { instanceId = s.instanceId, templateId = s.templateId,
                x = s.cell.x, y = s.cell.y, label = Array.Find(definition.unitTemplates, t => t.templateId == s.templateId)?.name }).ToList()
        };
    }
}
