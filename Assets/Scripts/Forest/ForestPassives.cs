using System;
using System.Linq;
namespace Mizonglin.Forest
{
    public static partial class ForestRules
    {
        static bool IsReactiveDamage(string cause) => cause == "Reflection" || cause == "CurseDamage" || cause == "FormCounterDamage";
        static UnitState FindProtector(ForestBattleState state, UnitState target)
        {
            if (state.Template(target.templateId).faction != Faction.Ally) return null;
            return state.unitsById.Values.Where(u => u != target && Alive(u) && u.dualForm != null
                && state.Template(u.templateId).faction == Faction.Ally && Distance(u.cell, target.cell) <= 2)
                .OrderBy(u => Distance(u.cell, target.cell)).ThenBy(u => u.instanceId, StringComparer.Ordinal).FirstOrDefault();
        }
        static void ApplyAwakening(ForestBattleState state, UnitState source, UnitState target)
        {
            if (!Alive(target) || target.awakeningSources.Count >= 3) return;
            target.awakeningSources.Add(source.instanceId); Record(state, "AwakeningAdded", source, target, 1);
        }
        static void GrantMovementCurses(ForestBattleState state, UnitState mover, int distance)
        {
            if (!Alive(mover) || distance <= 0 || state.Template(mover.templateId).faction != Faction.Ally) return;
            foreach (var caster in state.unitsById.Values.Where(u => u != mover && Alive(u)
                && state.Template(u.templateId).behaviorKind == BehaviorKind.FuSheng).OrderBy(u => u.instanceId, StringComparer.Ordinal))
            {
                int added = Math.Min(distance, 8 - mover.curseSources.Count);
                for (int i = 0; i < added; i++) mover.curseSources.Add(caster.instanceId);
                if (added > 0) Record(state, "CurseAdded", caster, mover, added);
            }
        }
        // Called for actual HP loss, including costs and environmental damage. Clear before reactions.
        static void HandleHpLoss(ForestBattleState state, UnitState damageSource, UnitState target, int actual)
        {
            if (actual <= 0) return;
            if (target.currentHp <= NearDeathLine(state, target) && state.Template(target.templateId).faction == Faction.Ally)
                state.statistics.everNearDeath = target.everNearDeath = true;
            var awakening = target.awakeningSources.ToArray(); target.awakeningSources.Clear();
            var curses = target.curseSources.ToArray(); target.curseSources.Clear();
            int healing = 0;
            foreach (var group in awakening.GroupBy(id => id))
                if (state.unitsById.TryGetValue(group.Key, out var healer)) healing += Stats(state, healer).attack * group.Count() * 50 / 100;
            if (awakening.Length > 0)
            {
                int restored = Math.Min(healing, Math.Max(0, Stats(state, target).maxHp - target.currentHp));
                target.currentHp += restored;
                Record(state, "AwakeningHeal", target: target, amount: restored);
            }
            if (curses.Length > 0) Record(state, "CurseDetonated", target, damageSource, curses.Length, target.cell);
            if (damageSource == null || damageSource == target || !Alive(damageSource)) return;
            foreach (var group in curses.GroupBy(id => id))
                if (state.unitsById.TryGetValue(group.Key, out var caster) && Alive(damageSource))
                {
                    int damage = Stats(state, caster).attack * group.Count() * 4 / 100;
                    if (damage > 0) Hurt(state, caster, damageSource, damage, "CurseDamage");
                }
        }
    }
}
