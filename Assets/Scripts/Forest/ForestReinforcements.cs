using System;
using System.Linq;
using UnityEngine;

namespace Zilong.Forest
{
    public static partial class ForestRules
    {
        public static bool IsPostBossSpawnCell(Vector2Int cell) => cell.x >= 15 && cell.x <= 16 && cell.y >= 1 && cell.y <= 2;

        static void ResolvePostBossReinforcements(ForestBattleState state)
        {
            if (state.result != BattleResult.InProgress) return;
            var boss = state.unitsById.Values.Where(u => u.maskBoss != null && u.lifeState == LifeState.Dead)
                .OrderBy(u => u.instanceId, StringComparer.Ordinal).FirstOrDefault();
            if (boss == null) return;
            // The post-boss wave is a one-off: exactly one wave per battle. A recorded wave (even one that
            // placed nobody) or a living reinforcement instance both stop every later attempt.
            if (state.eventLog.Any(e => e.kind == "PostBossWave")
                || state.unitsById.Keys.Any(id => id.Contains(":reinforcement:"))) return;
            var template = state.FindTemplate(BehaviorKind.Corpse);
            var origins = new System.Collections.Generic.List<Vector2Int> {
                new Vector2Int(15, 1), new Vector2Int(16, 1), new Vector2Int(15, 2), new Vector2Int(16, 2)
            };
            int count = 0;
            for (int i = 0; template != null && i < 2; i++)
            {
                // Draw distinct preferred tiles; resolve occupancy after each actual spawn.
                int index = (int)(NextRandom(state) % (uint)origins.Count);
                var origin = origins[index]; origins.RemoveAt(index);
                var candidates = OrderedWalkableCells(state, origin)
                    .Where(p => state.At(p) == null && SpiritAt(state, p) == null).Take(1).ToArray();
                if (candidates.Length == 0) break;
                string prefix = boss.instanceId + ":reinforcement:" + state.round + ":" + (i + 1);
                string id = prefix; int suffix = 1;
                while (state.unitsById.ContainsKey(id)) id = prefix + ":" + suffix++;
                var summoned = new UnitState { instanceId = id, templateId = template.templateId,
                    cell = candidates[0], currentHp = template.maxHp, lifeState = LifeState.Alive };
                state.unitsById.Add(id, summoned); count++;
                Record(state, "ReinforcementOrigin", boss, summoned, 1, origin);
                Record(state, "CorpseSummoned", boss, summoned, 1);
            }
            Record(state, "PostBossWave", boss, amount: count);
            if (count < 2) Record(state, "SummonShortfall", boss, amount: 2 - count);
        }
    }
}
