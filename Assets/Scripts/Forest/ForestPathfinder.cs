using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zilong.Forest
{
    public sealed class MovementArea
    {
        public readonly Dictionary<Vector2Int, int> costs = new Dictionary<Vector2Int, int>();
        public readonly Dictionary<Vector2Int, Vector2Int> parents = new Dictionary<Vector2Int, Vector2Int>();
    }
    public static class ForestPathfinder
    {
        static readonly Vector2Int[] Directions = { new Vector2Int(0, -1), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0) };
        sealed class Node { public Vector2Int cell; public int cost; public long sequence; }
        public static int EntryCost(ForestBattleState state, UnitState actor, Vector2Int cell)
        {
            bool rootsAlive = state.unitsById.TryGetValue(state.RootSourceInstanceId ?? "", out var source) && ForestRules.Alive(source);
            return !ForestRules.Flying(actor) && rootsAlive && Array.IndexOf(state.Map.rootCells, cell) >= 0 ? state.Rules.rootEntryCost : 1;
        }
        static bool CanPass(ForestBattleState state, UnitState actor, Vector2Int cell) => state.Map.Inside(cell)
            && (ForestRules.Flying(actor) || state.Map.Walkable(cell) && (state.At(cell) == null || state.At(cell) == actor));
        static bool CanStop(ForestBattleState state, UnitState actor, Vector2Int cell) => state.Map.Walkable(cell) && (state.At(cell) == null || state.At(cell) == actor);

        public static MovementArea Reachable(ForestBattleState state, UnitState actor)
            => Search(state, actor, actor.movementCommitted ? 0 : ForestRules.Stats(state, actor).move);

        // Used by pursuit to compare complete routes, then take only the affordable prefix.
        public static MovementArea Search(ForestBattleState state, UnitState actor, int budget)
        {
            var result = new MovementArea(); if (!ForestRules.Alive(actor)) return result;
            long sequence = 0;
            var costs = new Dictionary<Vector2Int, int> { [actor.cell] = 0 };
            var frontier = new SortedSet<Node>(Comparer<Node>.Create((a, b) => a.cost != b.cost ? a.cost.CompareTo(b.cost) : a.sequence.CompareTo(b.sequence)));
            frontier.Add(new Node { cell = actor.cell, cost = 0, sequence = sequence++ });
            while (frontier.Count > 0)
            {
                var node = frontier.Min; frontier.Remove(node);
                if (costs[node.cell] != node.cost) continue;
                if (CanStop(state, actor, node.cell)) result.costs[node.cell] = node.cost;
                foreach (var direction in Directions)
                {
                    var next = node.cell + direction;
                    if (!CanPass(state, actor, next)) continue;
                    long cost = (long)node.cost + EntryCost(state, actor, next);
                    if (cost > budget || costs.TryGetValue(next, out int existing) && existing <= cost) continue;
                    costs[next] = (int)cost; result.parents[next] = node.cell;
                    frontier.Add(new Node { cell = next, cost = (int)cost, sequence = sequence++ });
                }
            }
            return result;
        }
        public static List<Vector2Int> Path(MovementArea area, Vector2Int origin, Vector2Int destination)
        {
            var path = new List<Vector2Int>();
            if (!area.costs.ContainsKey(destination)) return path;
            for (var p = destination; ; p = area.parents[p]) { path.Add(p); if (p == origin) break; }
            path.Reverse(); return path;
        }
        public static string ValidatePath(ForestBattleState state, UnitState actor, List<Vector2Int> path, out Vector2Int destination)
        {
            destination = actor.cell;
            if (path == null || path.Count == 0) return null;
            if (actor.movementCommitted && path.Count > 1) return "本回合已确认移动，仍可进行主要行动和免费元神操作";
            if (path[0] != actor.cell) return "移动路径起点已变化，请重新选择";
            long cost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                if (ForestRules.Distance(path[i - 1], path[i]) != 1 || !CanPass(state, actor, path[i])) return "路径越界、经过障碍或被单位阻挡";
                cost += EntryCost(state, actor, path[i]);
            }
            if (!CanStop(state, actor, path[path.Count - 1])) return "终点须可走且没有其他单位";
            if (cost > ForestRules.Stats(state, actor).move) return "移动费用超出当前移动力";
            destination = path[path.Count - 1]; return null;
        }
    }
}
