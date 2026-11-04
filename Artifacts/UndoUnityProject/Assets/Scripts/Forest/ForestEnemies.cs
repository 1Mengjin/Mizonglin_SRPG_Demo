using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zilong.Forest
{
    public static partial class ForestRules
    {
        static IEnumerable<Vector2Int> OrderedWalkableCells(ForestBattleState state, Vector2Int origin)
        {
            var cells = new List<Vector2Int>();
            for (int y = 0; y < state.Map.height; y++) for (int x = 0; x < state.Map.width; x++)
            { var p = new Vector2Int(x, y); if (state.Map.Walkable(p)) cells.Add(p); }
            return cells.OrderBy(p => Distance(origin, p)).ThenBy(p => p.y).ThenBy(p => p.x);
        }
        static void ResolveFruitBursts(ForestBattleState state)
        {
            foreach (var tree in state.unitsById.Values.Where(u => Alive(u) && u.tree != null).OrderBy(u => u.instanceId, StringComparer.Ordinal))
            {
                if (tree.tree.nextBurstRound > state.round || IsStunned(state, tree)) continue;
                int radius = tree.tree.fruitLevel;
                var covered = OrderedWalkableCells(state, tree.cell).Where(p => Distance(tree.cell, p) <= radius).ToArray();
                Record(state, "TreeBurst", tree, amount: covered.Length);
                foreach (var cell in covered)
                    if (state.groundFog.Add(cell))
                    { state.statistics.generatedFogCount++; Record(state, "FogGenerated", tree, amount: 1, cell: cell); }
                tree.tree.fruitLevel = 1;
                tree.tree.nextBurstRound = state.round + state.Template(tree.templateId).tree.burstInterval;
            }
        }
        static void AbsorbFog(ForestBattleState state)
        {
            foreach (var unit in state.unitsById.Values.Where(u => Alive(u) && u.devourer != null).OrderBy(u => u.instanceId, StringComparer.Ordinal))
            {
                if (IsStunned(state, unit)) continue;
                var parameters = state.Template(unit.templateId).devourer;
                var cells = state.groundFog.Where(p => Distance(unit.cell, p) <= parameters.absorbRadius)
                    .OrderBy(p => Distance(unit.cell, p)).ThenBy(p => p.y).ThenBy(p => p.x).Take(parameters.absorbLimit).ToArray();
                foreach (var cell in cells)
                { state.groundFog.Remove(cell); unit.devourer.carriedFogCount++; Record(state, "FogAbsorbed", unit, amount: 1, cell: cell); }
            }
        }
        static void SpillFog(ForestBattleState state, UnitState unit)
        {
            foreach (var cell in OrderedWalkableCells(state, unit.cell))
            {
                if (unit.devourer.carriedFogCount == 0) break;
                if (!state.groundFog.Add(cell)) continue;
                unit.devourer.carriedFogCount--; Record(state, "FogSpilled", unit, amount: 1, cell: cell);
            }
            if (unit.devourer.carriedFogCount > 0) Record(state, "SpillOverflow", unit, amount: unit.devourer.carriedFogCount);
        }
        public static ForestCommand ChooseEnemyAction(ForestBattleState state, string instanceId)
        {
            if (!state.unitsById.TryGetValue(instanceId, out var unit) || !Alive(unit)
                || state.Template(unit.templateId).faction != Faction.Enemy || unit.acted || state.phase != BattlePhase.Enemy) return null;
            var wait = new ForestCommand { actorId = instanceId, kind = ActionKind.Wait };
            if (unit.maskBoss != null) return ChooseMaskBossAction(state, instanceId);
            if (unit.devourer != null)
            {
                var next = state.Map.patrolPoints[unit.devourer.nextPatrolIndex];
                var path = new List<Vector2Int> { unit.cell, next };
                if (ForestPathfinder.ValidatePath(state, unit, path, out _) == null) wait.path = path;
                return wait;
            }
            if (state.Template(unit.templateId).behaviorKind != BehaviorKind.Corpse) return wait;
            var area = ForestPathfinder.Search(state, unit, int.MaxValue);
            var best = (from target in state.unitsById.Values
                        where Alive(target) && state.Template(target.templateId).faction == Faction.Ally
                        from destination in area.costs
                        where CanHit(state, unit, destination.Key, target)
                        select new { target, destination = destination.Key, cost = destination.Value })
                .OrderBy(candidate => candidate.cost).ThenBy(candidate => candidate.target.instanceId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.destination.y).ThenBy(candidate => candidate.destination.x).FirstOrDefault();
            if (best == null) return wait;
            var route = ForestPathfinder.Path(area, unit.cell, best.destination);
            int spent = 0, budget = Stats(state, unit).move;
            var steps = new List<Vector2Int> { unit.cell };
            foreach (var cell in route.Skip(1))
            {
                int cost = ForestPathfinder.EntryCost(state, unit, cell);
                if (cost > budget - spent) break;
                spent += cost; steps.Add(cell);
            }
            var end = steps[steps.Count - 1];
            return new ForestCommand { actorId = instanceId, path = steps,
                kind = CanHit(state, unit, end, best.target) ? ActionKind.BasicAttack : ActionKind.Wait,
                targetId = best.target.instanceId };
        }
    }
}
