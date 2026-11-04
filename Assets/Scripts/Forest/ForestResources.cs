using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mizonglin.Forest
{
    public static partial class ForestRules
    {
        public static SpiritState SpiritAt(ForestBattleState state, Vector2Int cell) => state.spiritsById.Values
            .FirstOrDefault(s => s.locationKind == SpiritLocation.Ground && s.cell == cell);

        public static IEnumerable<Vector2Int> DropRing(Vector2Int origin, int distance)
        {
            var p = origin + new Vector2Int(0, -distance);
            foreach (var step in new[] { new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(-1, -1), new Vector2Int(1, -1) })
                for (int i = 0; i < distance; i++) { yield return p; p += step; }
        }
        static void ResourceEvent(ForestBattleState state, string kind, UnitState actor, SpiritState spirit, Vector2Int cell)
        {
            Record(state, kind, actor, amount: spirit.sourcePhase, cell: cell);
            state.eventLog[state.eventLog.Count - 1].targetId = spirit.spiritId;
        }
        static bool PlaceDrop(ForestBattleState state, PendingDrop pending)
        {
            var spirit = state.spiritsById[pending.spiritId];
            IEnumerable<Vector2Int> cells = pending.atDeathCell ? new[] { pending.origin }
                : Enumerable.Range(1, state.Template(state.unitsById[pending.sourceInstanceId].templateId).maskBoss.dropSearchRadius)
                    .SelectMany(d => DropRing(pending.origin, d));
            foreach (var cell in cells)
            {
                if (!state.Map.Walkable(cell) || SpiritAt(state, cell) != null || !pending.atDeathCell && state.At(cell) != null) continue;
                spirit.locationKind = SpiritLocation.Ground; spirit.cell = cell; spirit.carrierId = null;
                ResourceEvent(state, "SpiritDrop", state.unitsById[pending.sourceInstanceId], spirit, cell); return true;
            }
            return false;
        }
        public static void RetryPendingDrops(ForestBattleState state)
        {
            foreach (var pending in state.pendingDrops.ToArray()) if (PlaceDrop(state, pending)) state.pendingDrops.Remove(pending);
        }
        static bool ResolveMaskPhase(ForestBattleState state, UnitState boss)
        {
            var parameters = state.Template(boss.templateId).maskBoss;
            int phase = boss.maskBoss.phaseIndex, bit = 1 << (phase - 1);
            if ((boss.maskBoss.resolvedPhaseMask & bit) == 0)
            {
                boss.maskBoss.resolvedPhaseMask |= bit; Record(state, "PhaseDefeated", boss, amount: phase);
                var spirit = new SpiritState { spiritId = boss.instanceId + ":phase:" + phase, sourceInstanceId = boss.instanceId,
                    sourcePhase = phase, locationKind = SpiritLocation.Pending, cell = boss.cell };
                state.spiritsById.Add(spirit.spiritId, spirit);
                var pending = new PendingDrop { spiritId = spirit.spiritId, sourceInstanceId = boss.instanceId, origin = boss.cell };
                if (!PlaceDrop(state, pending)) { state.pendingDrops.Add(pending); ResourceEvent(state, "SpiritPending", boss, spirit, boss.cell); }
            }
            if (phase >= parameters.phaseCount) return false;
            boss.maskBoss.phaseIndex++; boss.currentHp = parameters.phaseHp; boss.maskBoss.phaseChangedRevision = state.revision;
            Record(state, "PhaseChanged", boss, amount: boss.maskBoss.phaseIndex);
            SummonRevivalCorpses(state, boss); return true;
        }
        static void SummonRevivalCorpses(ForestBattleState state, UnitState boss)
        {
            var template = state.FindTemplate(BehaviorKind.Corpse);
            var cells = Square(state, boss.cell, 3).Where(p => p != boss.cell && state.Map.Walkable(p)
                && state.At(p) == null && SpiritAt(state, p) == null).ToList();
            int count = template == null ? 0 : Math.Min(1, cells.Count);
            for (int i = 0; i < count; i++)
            {
                int index = (int)(NextRandom(state) % (uint)cells.Count);
                var cell = cells[index]; cells.RemoveAt(index);
                string id = boss.instanceId + ":summon:" + boss.maskBoss.phaseIndex + ":" + (i + 1);
                var summoned = new UnitState { instanceId = id, templateId = template.templateId, cell = cell,
                    currentHp = template.maxHp, lifeState = LifeState.Alive };
                state.unitsById.Add(id, summoned);
                Record(state, "CorpseSummoned", boss, summoned, 1);
            }
            if (count < 1) Record(state, "SummonShortfall", boss, amount: 1 - count);
        }
        static uint NextRandom(ForestBattleState state)
        {
            uint rng = state.randomState == 0 ? 2463534242u : state.randomState;
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return state.randomState = rng;
        }
        static void DropCarriedSpirit(ForestBattleState state, UnitState unit)
        {
            if (unit.carriedSpiritId == null || !state.spiritsById.TryGetValue(unit.carriedSpiritId, out var spirit)) return;
            unit.carriedSpiritId = null; spirit.carrierId = null; spirit.locationKind = SpiritLocation.Pending;
            var pending = new PendingDrop { spiritId = spirit.spiritId, sourceInstanceId = unit.instanceId, origin = unit.cell, atDeathCell = true };
            if (!PlaceDrop(state, pending)) { state.pendingDrops.Add(pending); ResourceEvent(state, "SpiritPending", unit, spirit, unit.cell); }
        }
        static void CarrySpirit(ForestBattleState state, UnitState actor, string spiritId)
        {
            var spirit = state.spiritsById[spiritId];
            actor.carriedSpiritId = spirit.spiritId; spirit.locationKind = SpiritLocation.Carried; spirit.carrierId = actor.instanceId;
            ResourceEvent(state, "SpiritPickup", actor, spirit, actor.cell);
        }
        static bool ClearFog(ForestBattleState state, UnitState actor, Vector2Int cell)
        {
            if (!state.Map.Walkable(cell) || !state.groundFog.Remove(cell)) return false;
            actor.statistics.fogCleared++; state.statistics.clearedFogCount++; Record(state, "FogCleared", actor, amount: 1, cell: cell); return true;
        }
        static void Purify(ForestBattleState state, UnitState actor)
        {
            if (Flying(actor) || state.Template(actor.templateId).faction != Faction.Ally || actor.purificationCharges.Count == 0
                || !ClearFog(state, actor, actor.cell)) return;
            var charge = actor.purificationCharges[0]; charge.clearedCount++;
            Record(state, "Purify", actor, amount: 1, cell: actor.cell);
            if (charge.clearedCount % state.Rules.purificationCellsPerScar == 0)
            {
                actor.scarStacks++; int oldHp = actor.currentHp;
                actor.currentHp = Math.Min(Stats(state, actor).maxHp, Math.Max(1, actor.currentHp - state.Rules.scarHpPenalty));
                actor.statistics.damageTaken += oldHp - actor.currentHp;
                int hpLoss = oldHp - actor.currentHp;
                Record(state, "ScarDamage", actor, actor, hpLoss);
                HandleHpLoss(state, null, actor, hpLoss);
                Record(state, "Scar", actor, amount: actor.scarStacks); ObserveHealth(state, actor);
            }
            if (charge.clearedCount == state.Rules.purificationCellsPerSpirit) actor.purificationCharges.RemoveAt(0);
        }
        static void ResolveMovement(ForestBattleState state, UnitState actor, List<Vector2Int> path)
        {
            if (path == null || path.Count <= 1) return;
            Purify(state, actor);
            foreach (var cell in path.Skip(1)) { actor.cell = cell; GrantMovementCurses(state, actor, 1); Purify(state, actor); }
            actor.movementCommitted = true;
            Record(state, "Move", actor, amount: path.Count - 1);
        }
        static SpiritState ConsumeSpirit(ForestBattleState state, UnitState actor, string spiritId = null)
        {
            var spirit = state.spiritsById[spiritId ?? actor.carriedSpiritId];
            spirit.locationKind = SpiritLocation.Consumed; spirit.carrierId = null; spirit.consumedBy = actor.instanceId;
            if (actor.carriedSpiritId == spirit.spiritId) actor.carriedSpiritId = null; return spirit;
        }
        static void ThrowSpirit(ForestBattleState state, UnitState actor, Vector2Int center)
        {
            var spirit = ConsumeSpirit(state, actor); int removed = 0, radius = state.Rules.spiritClearRadius;
            for (int y = Math.Max(0, center.y - radius); y <= Math.Min(state.Map.height - 1, center.y + radius); y++)
            for (int x = Math.Max(0, center.x - radius); x <= Math.Min(state.Map.width - 1, center.x + radius); x++)
            { var p = new Vector2Int(x, y); if (Distance(center, p) <= radius && ClearFog(state, actor, p)) removed++; }
            Record(state, "ThrowSpirit", actor, amount: removed, cell: center);
            state.eventLog[state.eventLog.Count - 1].targetId = spirit.spiritId;
        }
        static void SwallowSpirit(ForestBattleState state, UnitState actor, string spiritId)
        {
            var spirit = ConsumeSpirit(state, actor, spiritId);
            actor.purificationCharges.Add(new PurificationCharge { sourceSpiritId = spirit.spiritId, clearedCount = 0 });
            ResourceEvent(state, "SwallowSpirit", actor, spirit, actor.cell);
        }
        public static ForestCommand ChooseMaskBossAction(ForestBattleState state, string instanceId)
        {
            if (!state.unitsById.TryGetValue(instanceId, out var boss) || !Alive(boss) || boss.maskBoss == null) return null;
            return new ForestCommand { actorId = instanceId, kind = IsStunned(state, boss) ? ActionKind.Wait
                : boss.maskBoss.releaseRound > 0 ? ActionKind.MaskBlast : ActionKind.MaskCharge };
        }
    }
}
