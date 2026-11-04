using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zilong.Forest
{
    public static partial class ForestRules
    {
        public static bool IsStunned(ForestBattleState state, UnitState unit) => unit.stunnedUntilRound >= state.round;
        public static bool IsSkill(ActionKind kind) => kind == ActionKind.Talisman || kind == ActionKind.Heal
            || kind == ActionKind.Cleanse || kind == ActionKind.SwitchStance || kind == ActionKind.Retreat
            || kind == ActionKind.Sprint || kind == ActionKind.Echo || kind == ActionKind.MaskCharge || kind == ActionKind.MaskBlast;
        static string ValidateNewSkill(ForestBattleState state, ForestCommand command)
        {
            var actor = state.unitsById[command.actorId];
            state.unitsById.TryGetValue(command.targetId ?? "", out var target);
            switch (command.kind)
            {
                case ActionKind.Sprint:
                    if (actor.flight == null || Ability(actor, "sprint") == null) return "该角色没有疾行";
                    if (state.round < Ability(actor, "sprint").availableRound) return "疾行仍在冷却";
                    break;
                case ActionKind.Echo:
                    if (Ability(actor, "echo") == null || state.round < Ability(actor, "echo").availableRound) return "符箓引不可用或仍在冷却";
                    var targetError = ValidateTeleportTarget(state, actor.instanceId, command.targetId);
                    if (targetError != null) return targetError;
                    if (Distance(actor.cell, command.targetCell) != 1) return "请选择符生身边上下左右四格之一";
                    if (!state.Map.Walkable(command.targetCell) || state.At(command.targetCell) != null) return "传送落点必须可走且没有单位";
                    break;
                case ActionKind.Retreat:
                    if (actor.dualForm == null) return "该角色没有退去";
                    if (actor.dualForm.form == DualForm.Attack)
                    {
                        if (actor.currentHp <= RetreatCost(state, actor)) return "生命不足以支付30%最大生命并保留1点生命";
                        if (!CanHit(state, actor, actor.cell, target)) return "请选择攻击射程内的敌人";
                    }
                    break;
                case ActionKind.MaskCharge:
                    if (actor.maskBoss == null || actor.maskBoss.releaseRound > 0) return "当前不能蓄力";
                    if (!state.unitsById.Values.Any(u => Alive(u) && state.Template(u.templateId).faction == Faction.Ally)) return "没有存活的玩家角色";
                    break;
                case ActionKind.MaskBlast:
                    if (actor.maskBoss == null || actor.maskBoss.releaseRound <= 0 || state.round < actor.maskBoss.releaseRound) return "蓄力尚未完成";
                    break;
            }
            return null;
        }
        public static string ValidateTeleportTarget(ForestBattleState state, string actorId, string targetId)
        {
            if (actorId == null || !state.unitsById.TryGetValue(actorId, out var actor) || !Alive(actor)
                || state.Template(actor.templateId).behaviorKind != BehaviorKind.FuSheng) return "该角色没有符箓·引";
            if (targetId == null || !state.unitsById.TryGetValue(targetId, out var target) || !Alive(target)
                || target == actor || state.Template(target.templateId).faction != Faction.Ally) return "请选择另一名存活队友";
            if (Distance(actor.cell, target.cell) > state.Template(actor.templateId).talisman.echoRange) return "队友超出符箓·引的传送范围";
            return null;
        }
        static IEnumerable<Vector2Int> Square(ForestBattleState state, Vector2Int center, int size)
        {
            int radius = size / 2;
            for (int y = center.y - radius; y <= center.y + radius; y++)
            for (int x = center.x - radius; x <= center.x + radius; x++)
            { var p = new Vector2Int(x, y); if (state.Map.Inside(p)) yield return p; }
        }
        public static IEnumerable<Vector2Int> MaskWarningCells(ForestBattleState state, UnitState boss) =>
            boss.maskBoss?.releaseRound > 0 && Alive(boss) ? Square(state, boss.maskBoss.warningCenter, 3) : Enumerable.Empty<Vector2Int>();
        static void ApplyTalisman(ForestBattleState state, UnitState caster, Vector2Int center, int attack)
        {
            var cells = new HashSet<Vector2Int>(Square(state, center, state.Template(caster.templateId).talisman.squareSize));
            foreach (var target in state.unitsById.Values.Where(u => Alive(u) && state.Template(u.templateId).faction == Faction.Enemy && cells.Contains(u.cell))
                .OrderBy(u => u.instanceId, StringComparer.Ordinal).ToArray())
            {
                target.stunnedUntilRound = Math.Max(target.stunnedUntilRound, state.round);
                Record(state, "Stun", caster, target, 1);
                Hurt(state, caster, target, Math.Max(1, attack - Stats(state, target).defense), "TalismanDamage");
            }
            Record(state, "Talisman", caster, cell: center);
        }
        static void GrantAction(ForestBattleState state, UnitState source, UnitState target)
        {
            if (target.acted) { target.acted = false; target.movementCommitted = false; }
            else target.bonusActions++;
            Record(state, "ExtraAction", source, target, 1);
        }
        public static int RetreatCost(ForestBattleState state, UnitState caster) => (int)(((long)Stats(state, caster).maxHp * 30 + 99) / 100);
        static void ApplyRetreatAttack(ForestBattleState state, UnitState caster, UnitState target)
        {
            int cost = RetreatCost(state, caster);
            if (!Alive(target) || caster.currentHp <= cost) { Record(state, "RetreatNoEffect", caster, target); return; }
            caster.currentHp -= cost; caster.statistics.damageTaken += cost; Record(state, "SkillCost", caster, caster, cost); HandleHpLoss(state, null, caster, cost); ObserveHealth(state, caster);
            Hurt(state, caster, target, Math.Max(1, cost * 120 / 100), "RetreatDamage");
        }
        static void ExecuteNewSkill(ForestBattleState state, UnitState actor, ForestCommand command)
        {
            state.unitsById.TryGetValue(command.targetId ?? "", out var target);
            switch (command.kind)
            {
                case ActionKind.Sprint:
                    GrantAction(state, actor, actor); Ability(actor, "sprint").availableRound = state.round + 2; break;
                case ActionKind.Echo:
                    int teleportDistance = Distance(target.cell, command.targetCell);
                    target.cell = command.targetCell;
                    GrantMovementCurses(state, target, teleportDistance);
                    Ability(actor, "echo").availableRound = state.round + 3;
                    Record(state, "AllyTeleported", actor, target, cell: target.cell);
                    CollectReward(state, target); break;
                case ActionKind.Retreat:
                    if (actor.dualForm.form == DualForm.Guard) { actor.reflectPercent = 90; Record(state, "ReflectReady", actor, actor, 90); }
                    else ApplyRetreatAttack(state, actor, target);
                    break;
                case ActionKind.MaskCharge:
                    var candidates = state.unitsById.Values.Where(u => Alive(u) && state.Template(u.templateId).faction == Faction.Ally)
                        .OrderBy(u => u.instanceId, StringComparer.Ordinal).ToArray();
                    uint rng = NextRandom(state);
                    target = candidates[rng % (uint)candidates.Length];
                    int dx = target.cell.x - actor.cell.x, dy = target.cell.y - actor.cell.y;
                    var direction = Math.Abs(dx) >= Math.Abs(dy) && dx != 0 ? new Vector2Int(Math.Sign(dx), 0) : new Vector2Int(0, dy == 0 ? -1 : Math.Sign(dy));
                    actor.maskBoss.warningCenter = actor.cell + direction * 2; actor.maskBoss.releaseRound = state.round + 1;
                    Record(state, "MaskCharge", actor, target, cell: actor.maskBoss.warningCenter); break;
                case ActionKind.MaskBlast:
                    var area = new HashSet<Vector2Int>(MaskWarningCells(state, actor));
                    actor.maskBoss.releaseRound = 0; Record(state, "MaskBlast", actor, amount: 60);
                    foreach (var victim in state.unitsById.Values.Where(u => Alive(u) && u != actor && area.Contains(u.cell)).OrderBy(u => u.instanceId, StringComparer.Ordinal).ToArray())
                        if (Alive(victim)) Hurt(state, actor, victim, 60, "MaskBlastDamage");
                    break;
            }
        }
    }
}
