using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zilong.Forest
{
    public enum ActionKind { Wait, BasicAttack, Talisman, Heal, Cleanse, SwitchStance, ThrowSpirit, SwallowSpirit, CarrySpirit, Retreat, Sprint, Echo, MaskCharge, MaskBlast }
    public sealed class ForestCommand
    {
        public string actorId, targetId, spiritId;
        public ActionKind kind;
        public Vector2Int targetCell;
        public List<Vector2Int> path;
    }
    public struct EffectiveStats
    {
        public int maxHp, attack, defense, move, range;
        public bool canAttack;
    }

    public static partial class ForestRules
    {
        public const int DevourerSelfDestructRound = 8;
        public const int DevourerSelfDestructDamage = 50;
        public const int DevourerSelfDestructRadius = 2;
        public static bool IsSpiritAction(ActionKind kind) => kind == ActionKind.CarrySpirit || kind == ActionKind.ThrowSpirit || kind == ActionKind.SwallowSpirit;
        public static bool IsFreeAction(ActionKind kind) => kind == ActionKind.SwitchStance || kind == ActionKind.Sprint || IsSpiritAction(kind);
        public static bool PlayerPhaseComplete(ForestBattleState state) => state.phase == BattlePhase.Player
            && state.result == BattleResult.InProgress
            && state.unitsById.Values.Any(u => Alive(u) && state.Template(u.templateId).faction == Faction.Ally)
            && state.unitsById.Values.Where(u => Alive(u) && state.Template(u.templateId).faction == Faction.Ally).All(u => u.acted);
        public static int Distance(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
        public static bool Alive(UnitState unit) => unit != null && unit.lifeState == LifeState.Alive && unit.currentHp > 0;
        public static bool Flying(UnitState unit) => unit.flight?.stance == FlightStance.Flying;
        public static EffectiveStats Stats(ForestBattleState state, UnitState unit)
        {
            var t = state.Template(unit.templateId); var r = state.Rules;
            bool ally = t.faction == Faction.Ally, attackForm = unit.dualForm?.form == DualForm.Attack;
            int multiplier = attackForm ? t.dualForm.attackMultiplier : 1;
            int numerator = ally && unit.poisonStacks > 0 ? r.poisonAttackNumerator : r.poisonAttackDenominator;
            return new EffectiveStats {
                maxHp = (int)(Math.Max(1L, (long)t.maxHp - (ally ? (long)r.scarHpPenalty * unit.scarStacks : 0)) * (attackForm ? 150 : 100) / 100),
                attack = (int)((long)t.attack * multiplier * (attackForm ? 120 : 100) * numerator / (100L * r.poisonAttackDenominator)),
                defense = unit.dualForm == null ? t.defense : attackForm ? t.dualForm.attackDefense : t.dualForm.guardDefense * 150 / 100,
                range = unit.dualForm == null ? t.basicRange : attackForm ? t.dualForm.attackRange : 1,
                move = Math.Max(0, t.move + (Flying(unit) ? 1 : 0) - (unit.dualForm?.form == DualForm.Guard ? 1 : 0) - (ally && unit.poisonStacks >= 2 ? r.poisonMovePenalty : 0)),
                canAttack = Alive(unit) && t.hasBasicAttack && !Flying(unit)
            };
        }
        public static int Damage(ForestBattleState state, UnitState attacker, UnitState target) =>
            Math.Max(1, Stats(state, attacker).attack * (attacker.dualForm?.form == DualForm.Guard ? 50 : 100) / 100 - Stats(state, target).defense);
        public static int NearDeathLine(ForestBattleState state, UnitState unit) => Math.Max(1, (int)((long)Stats(state, unit).maxHp * state.Rules.nearDeathPercent / 100));
        public static int PoisonDamage(ForestBattleState state, UnitState unit) => (int)(((long)Stats(state, unit).maxHp * state.Rules.poisonDamagePercent + 99) / 100);
        public static bool CanHit(ForestBattleState state, UnitState attacker, Vector2Int origin, UnitState target)
        {
            if (!Alive(attacker) || !Alive(target) || !Stats(state, attacker).canAttack
                || state.Template(attacker.templateId).faction == state.Template(target.templateId).faction) return false;
            int distance = Distance(origin, target.cell);
            return distance >= 1 && distance <= Stats(state, attacker).range;
        }
        public static AbilityState Ability(UnitState unit, string id) => unit.abilityStates.Find(a => a.abilityId == id);

        internal static void Record(ForestBattleState state, string kind, UnitState source = null, UnitState target = null, int amount = 0, Vector2Int? cell = null)
        {
            state.eventLog.Add(new BattleEvent { sequence = state.eventLog.Count + 1L, revision = state.revision,
                round = state.round, kind = kind, sourceId = source?.instanceId, targetId = target?.instanceId,
                amount = amount, requestedAmount = amount, cell = cell ?? target?.cell ?? source?.cell ?? default });
        }
        static void ObserveHealth(ForestBattleState state, UnitState unit)
        {
            if (state.Template(unit.templateId).faction != Faction.Ally) return;
            if (unit.currentHp <= NearDeathLine(state, unit)) { unit.everNearDeath = true; state.statistics.everNearDeath = true; }
            if (unit.currentHp <= 0) { state.statistics.everAllyDied = state.statistics.everNearDeath = unit.everNearDeath = true; }
        }
        static void RemoveDead(ForestBattleState state, UnitState unit)
        {
            if (unit.currentHp > 0 || unit.lifeState == LifeState.Dead) return;
            if (unit.maskBoss != null && ResolveMaskPhase(state, unit)) return;
            unit.currentHp = 0; unit.lifeState = LifeState.Dead;
            DropCarriedSpirit(state, unit);
            unit.purificationCharges.Clear(); unit.awakeningSources.Clear(); unit.curseSources.Clear(); unit.reflectPercent = 0;
            if (unit.maskBoss != null) unit.maskBoss.releaseRound = 0;
            if (unit.flight != null) unit.flight.switchRound = 0;
            if (unit.tree != null) { unit.tree.nextBurstRound = 0; state.statistics.sourceDefeatedRounds[unit.instanceId] = state.round; }
            if (unit.devourer != null)
            {
                if (IsStunned(state, unit))
                {
                    if (unit.devourer.carriedFogCount > 0) Record(state, "DeathrattleSilenced", unit, amount: unit.devourer.carriedFogCount);
                }
                else SpillFog(state, unit);
            }
            foreach (var other in state.unitsById.Values)
            {
                if (other.dualForm != null && (other == unit || other.dualForm.protectedUnitId == unit.instanceId)) other.dualForm.protectedUnitId = null;
            }
            Record(state, "Death", unit);
            if (unit.tree != null && state.unitsById.Values.Where(u => u.tree != null).All(u => !Alive(u))) Record(state, "SourcesCleared", unit);
        }
        static void Hurt(ForestBattleState state, UnitState source, UnitState target, int damage, string cause)
        {
            if (target.maskBoss != null && target.maskBoss.phaseChangedRevision == state.revision) return;
            if (source != null && source != target && !IsReactiveDamage(cause)
                && state.Template(source.templateId).faction != state.Template(target.templateId).faction)
            {
                var protector = FindProtector(state, target);
                if (protector != null) { Record(state, "Protect", protector, target, damage); target = protector; }
            }
            int reflection = cause != "Reflection" && source != null && source != target ? target.reflectPercent : 0;
            if (reflection > 0) target.reflectPercent = 0;
            if (target.dualForm?.form == DualForm.Attack && damage > 0)
                damage = (int)Math.Min(int.MaxValue, ((long)damage * 130 + 99) / 100);
            int actual = Math.Min(target.currentHp, damage);
            target.currentHp = Math.Max(0, target.currentHp - damage); target.statistics.damageTaken += actual;
            if (source != null && source != target) source.statistics.damageDealt += actual;
            Record(state, cause, source, target, actual); state.eventLog[state.eventLog.Count - 1].requestedAmount = damage;
            HandleHpLoss(state, source, target, actual);
            ObserveHealth(state, target); RemoveDead(state, target);
            if (actual > 0 && Alive(target) && Alive(source) && source != target
                && target.dualForm?.form == DualForm.Attack && !IsReactiveDamage(cause) && cause != "Counter"
                && state.Template(source.templateId).faction != state.Template(target.templateId).faction)
                Hurt(state, target, source, Math.Max(1, Stats(state, target).attack * 120 / 100 - Stats(state, source).defense), "FormCounterDamage");
            if (reflection > 0 && Alive(source) && actual * reflection / 100 > 0) Hurt(state, target, source, actual * reflection / 100, "Reflection");
        }
        static void DirectHit(ForestBattleState state, UnitState attacker, UnitState originalTarget, string kind)
        {
            int damage = Damage(state, attacker, originalTarget);
            Hurt(state, attacker, originalTarget, damage, kind);
        }
        static void Attack(ForestBattleState state, UnitState attacker, UnitState defender)
        {
            DirectHit(state, attacker, defender, "Attack");
            // A surviving, non-stunned tree drops its current fruit when hit by an ally's basic attack.
            if (Alive(defender) && defender.tree != null && state.Template(attacker.templateId).faction == Faction.Ally && !IsStunned(state, defender))
                ReleaseFruit(state, defender, true);
            if (Alive(attacker) && Alive(defender) && Stats(state, defender).canAttack
                && defender.dualForm == null && FindProtector(state, defender) == null && Stats(state, attacker).range == 1 && Stats(state, defender).range == 1
                && Distance(attacker.cell, defender.cell) == 1) DirectHit(state, defender, attacker, "Counter");
        }
        // Time-based growth; attacks no longer add layers, they drop the current fruit instead.
        static void GrowFruit(ForestBattleState state, UnitState tree)
        {
            if (!Alive(tree) || tree.tree == null) return;
            tree.tree.fruitLevel = Math.Min(state.Template(tree.templateId).tree.maxFruitLevel, tree.tree.fruitLevel + 1);
            Record(state, "FruitGrowth", tree, amount: tree.tree.fruitLevel);
        }

        public static void BeginPlayerPhase(ForestBattleState state)
        {
            if (state.phase != BattlePhase.PlayerStart || state.result != BattleResult.InProgress) throw new InvalidOperationException("当前不是我方阶段开始");
            state.revision++;
            foreach (var unit in state.unitsById.Values.OrderBy(u => u.instanceId, StringComparer.Ordinal))
            {
                if (unit.dualForm != null) unit.dualForm.protectedUnitId = null;
                if (Alive(unit)) { unit.acted = false; unit.movementCommitted = false; unit.bonusActions = 0; }
                if (!Alive(unit) || state.Template(unit.templateId).faction != Faction.Ally) continue;
                if (unit.dualForm != null)
                {
                    var parameters = state.Template(unit.templateId).dualForm;
                    int previousMaxHp = Stats(state, unit).maxHp;
                    var nextForm = (state.round - 1) % parameters.cycle < parameters.guardTurns ? DualForm.Guard : DualForm.Attack;
                    if (unit.dualForm.form != nextForm)
                    {
                        int previousHp = unit.currentHp;
                        unit.dualForm.form = nextForm;
                        int nextMaxHp = Stats(state, unit).maxHp;
                        unit.currentHp = (int)Math.Max(1L, Math.Min(nextMaxHp, (long)previousHp * nextMaxHp / previousMaxHp));
                        // A proportional form conversion is not damage, healing, or a paid HP cost.
                        Record(state, "FormHealthAdjusted", unit, amount: unit.currentHp - previousHp);
                    }
                    unit.dualForm.protectionRound = state.round;
                    Record(state, unit.dualForm.form == DualForm.Guard ? "GuardForm" : "AttackForm", unit);
                }
                if (unit.flight != null)
                {
                    if (unit.flight.switchRound > 0 && unit.flight.switchRound <= state.round)
                    {
                        unit.flight.stance = unit.flight.pendingStance; unit.flight.switchRound = 0;
                        Record(state, "Stance", unit, amount: (int)unit.flight.stance);
                    }
                    unit.flight.stanceLockedRound = state.round;
                }
            }
            state.phase = BattlePhase.Player; Record(state, "PlayerPhase");
        }

        static string ValidateActor(ForestBattleState state, ForestCommand command)
        {
            if (command == null || command.actorId == null || !state.unitsById.TryGetValue(command.actorId, out var actor) || !Alive(actor)) return "行动者不存在或已死亡";
            if (state.result != BattleResult.InProgress || actor.acted && !IsFreeAction(command.kind)) return "当前无法再次行动";
            bool ally = state.Template(actor.templateId).faction == Faction.Ally;
            if (state.phase != (ally ? BattlePhase.Player : BattlePhase.Enemy)) return "当前阶段不允许该单位行动";
            if (!Enum.IsDefined(typeof(ActionKind), command.kind)) return "未知行动";
            if (IsStunned(state, actor))
            {
                if (ally) { if (IsSkill(command.kind)) return "晕眩中无法释放技能"; }
                else if (command.kind != ActionKind.Wait || command.path != null && command.path.Count > 1) return "晕眩中无法行动";
            }
            if (!ally && command.kind != ActionKind.BasicAttack && command.kind != ActionKind.Wait && command.kind != ActionKind.MaskCharge && command.kind != ActionKind.MaskBlast) return "敌方不能使用角色技能";
            var t = state.Template(actor.templateId);
            if (command.kind == ActionKind.SwitchStance)
            {
                var stanceAbility = Ability(actor, "stance");
                if (t.behaviorKind != BehaviorKind.QingYu || actor.flight == null || stanceAbility == null) return "该单位没有姿态切换技能";
                if (state.round < stanceAbility.availableRound) return "姿态切换技能仍在冷却";
                if (actor.flight.switchRound > 0) return "已安排下回合切换姿态";
                return null;
            }
            return null;
        }

        static string ValidateAction(ForestBattleState state, ForestCommand command)
        {
            var actor = state.unitsById[command.actorId]; var t = state.Template(actor.templateId);
            var destination = actor.cell;
            state.unitsById.TryGetValue(command.targetId ?? "", out var target);
            switch (command.kind)
            {
                case ActionKind.BasicAttack:
                    if (!CanHit(state, actor, destination, target)) return "当前姿态不能攻击，或目标不在射程内";
                    break;
                case ActionKind.Talisman:
                    if (t.behaviorKind != BehaviorKind.FuSheng || Ability(actor, "talisman")?.remainingUses < 1
                        || Ability(actor, "talisman") == null) return "符箓次数不足或单位没有该技能";
                    if (!state.Map.Inside(command.targetCell) || Distance(destination, command.targetCell) > t.talisman.castRange) return "符箓中心超出施法距离";
                    break;
                case ActionKind.Heal: case ActionKind.Cleanse:
                    if (t.behaviorKind != BehaviorKind.SuLing || !Alive(target) || state.Template(target.templateId).faction != Faction.Ally) return "请选择存活友方，可包含自身";
                    int range = command.kind == ActionKind.Heal ? t.support.healRange : t.support.cleanseRange;
                    var targetCell = target == actor ? destination : target.cell;
                    if (Distance(destination, targetCell) > range) return "友方不在技能距离内";
                    var ability = Ability(actor, command.kind == ActionKind.Heal ? "heal" : "cleanse");
                    if (ability == null || state.round < ability.availableRound) return "技能仍在冷却";
                    break;
                case ActionKind.Retreat: case ActionKind.Sprint: case ActionKind.Echo: case ActionKind.MaskCharge: case ActionKind.MaskBlast:
                    return ValidateNewSkill(state, command);
                case ActionKind.CarrySpirit: case ActionKind.ThrowSpirit: case ActionKind.SwallowSpirit:
                    if (Flying(actor)) return "飞行时不能携带、投掷或吞下元神";
                    var spiritId = command.spiritId ?? actor.carriedSpiritId;
                    if (spiritId == null || !state.spiritsById.TryGetValue(spiritId, out var spirit)) return "请选择可接触的元神";
                    bool carried = spirit.locationKind == SpiritLocation.Carried && spirit.carrierId == actor.instanceId && actor.carriedSpiritId == spiritId;
                    bool onPath = spirit.locationKind == SpiritLocation.Ground && (spirit.cell == actor.cell || command.path != null && command.path.Contains(spirit.cell));
                    if (command.kind == ActionKind.CarrySpirit && (actor.carriedSpiritId != null || !onPath)) return "最多携带一颗元神，请选择脚下或本次路径上的元神";
                    if (command.kind == ActionKind.SwallowSpirit && !carried && !onPath) return "请选择携带、脚下或本次路径上的元神";
                    if (command.kind == ActionKind.ThrowSpirit)
                    {
                        if (!carried) return "请先选择携带元神";
                        if (!state.Map.Walkable(command.targetCell) || state.At(command.targetCell) != null && state.At(command.targetCell) != actor)
                            return "投掷落点须可走且没有其他单位";
                        if (Distance(actor.cell, command.targetCell) > state.Rules.spiritThrowRange) return "超出元神投掷距离";
                    }
                    break;
            }
            return null;
        }

        public static ForestBattleState PreviewMovement(ForestBattleState state, string actorId, List<Vector2Int> path, out string error)
        {
            var command = new ForestCommand { actorId = actorId, kind = ActionKind.Wait, path = path };
            error = ValidateActor(state, command); if (error != null) return null;
            error = ForestPathfinder.ValidatePath(state, state.unitsById[actorId], path, out _); if (error != null) return null;
            var moved = state.Copy(); moved.revision++;
            ResolveMovement(moved, moved.unitsById[actorId], path); return moved;
        }

        // The supplied state has already simulated movement, purification and scars.
        public static string ValidateFromMovementPreview(ForestBattleState moved, ForestCommand command) => ValidateActor(moved, command) ?? ValidateAction(moved, command);
        static ForestBattleState PrepareCommand(ForestBattleState state, ForestCommand command, out string error)
        {
            error = ValidateActor(state, command); if (error != null) return null;
            var actor = state.unitsById[command.actorId];
            if (actor.acted && command.path != null && command.path.Count > 1) { error = "已行动单位不能再次移动"; return null; }
            error = ForestPathfinder.ValidatePath(state, actor, command.path, out _); if (error != null) return null;
            var moved = state.Copy(); moved.revision++;
            ResolveMovement(moved, moved.unitsById[command.actorId], command.path);
            error = ValidateAction(moved, command); return error == null ? moved : null;
        }
        public static string ValidateCommand(ForestBattleState state, ForestCommand command)
        {
            var error = ValidateActor(state, command); if (error != null || command.kind == ActionKind.SwitchStance) return error;
            PrepareCommand(state, command, out error); return error;
        }
        public static bool TryExecute(ForestBattleState state, ForestCommand command, out string error)
        {
            error = ValidateActor(state, command); if (error != null) return false;
            var actor = state.unitsById[command.actorId]; var t = state.Template(actor.templateId);
            if (command.kind == ActionKind.SwitchStance)
            {
                state.revision++;
                actor.flight.pendingStance = Flying(actor) ? FlightStance.Grounded : FlightStance.Flying;
                actor.flight.switchRound = state.round + 1;
                Ability(actor, "stance").availableRound = state.round + t.flight.switchCooldownTurns;
                Record(state, "StanceQueued", actor, amount: (int)actor.flight.pendingStance);
                return true; // Free skill: do not commit a preview path or consume the major action.
            }
            var prepared = PrepareCommand(state, command, out error); if (prepared == null) return false;
            var movedActor = prepared.unitsById[command.actorId];
            CollectReward(prepared, movedActor);
            ExecutePrepared(prepared, command); state.CommitFrom(prepared); return true;
        }
        static void CollectReward(ForestBattleState state, UnitState actor)
        {
            if (Alive(actor) && state.Template(actor.templateId).faction == Faction.Ally && state.remainingRewards.Remove(actor.cell))
                Record(state, "RewardCollected", actor);
        }
        static void ExecutePrepared(ForestBattleState state, ForestCommand command)
        {
            var actor = state.unitsById[command.actorId]; var t = state.Template(actor.templateId);
            state.unitsById.TryGetValue(command.targetId ?? "", out var target);
            if (actor.devourer != null && command.path != null && command.path.Count > 1
                && actor.cell == state.Map.patrolPoints[actor.devourer.nextPatrolIndex])
            {
                actor.devourer.nextPatrolIndex = (actor.devourer.nextPatrolIndex + 1) % state.Map.patrolPoints.Length;
                Record(state, "Patrol", actor, amount: actor.devourer.nextPatrolIndex);
            }
            switch (command.kind)
            {
                case ActionKind.BasicAttack: Attack(state, actor, target); break;
                case ActionKind.Talisman:
                    ApplyTalisman(state, actor, command.targetCell, Stats(state, actor).attack * 150 / 100);
                    Ability(actor, "talisman").remainingUses--; break;
                case ActionKind.Heal:
                    int restored = Math.Min(t.support.healAmount, Math.Max(0, Stats(state, target).maxHp - target.currentHp));
                    target.currentHp += restored; Record(state, "Heal", actor, target, restored); ApplyAwakening(state, actor, target); break;
                case ActionKind.Cleanse:
                    int layers = target.poisonStacks; target.poisonStacks = 0;
                    Ability(actor, "cleanse").availableRound = state.round + t.support.cleanseCooldownTurns;
                    Record(state, "Cleanse", actor, target, layers); ApplyAwakening(state, actor, target); break;
                case ActionKind.Retreat: case ActionKind.Sprint: case ActionKind.Echo: case ActionKind.MaskCharge: case ActionKind.MaskBlast:
                    ExecuteNewSkill(state, actor, command); break;
                case ActionKind.CarrySpirit: CarrySpirit(state, actor, command.spiritId); break;
                case ActionKind.ThrowSpirit: ThrowSpirit(state, actor, command.targetCell); break;
                case ActionKind.SwallowSpirit: SwallowSpirit(state, actor, command.spiritId); break;
            }
            if (IsFreeAction(command.kind))
            {
                Record(state, IsSpiritAction(command.kind) ? "FreeSpiritAction" : "FreeSkillAction", actor); RetryPendingDrops(state); CheckOutcome(state); return;
            }
            if (Alive(actor) && actor.dualForm?.form == DualForm.Attack)
            {
                int selfDamage = (int)(((long)t.maxHp * t.dualForm.selfDamagePercent + 99) / 100);
                int actual = Math.Min(selfDamage, actor.currentHp - 1);
                actor.currentHp -= actual; actor.statistics.damageTaken += actual; Record(state, "SelfDamage", actor, actor, actual); HandleHpLoss(state, null, actor, actual); ObserveHealth(state, actor);
            }
            if (Alive(actor) && actor.bonusActions > 0) { actor.bonusActions--; actor.acted = false; actor.movementCommitted = false; }
            else actor.acted = true;
            actor.statistics.actions++; Record(state, "ActionEnd", actor);
            RetryPendingDrops(state); CheckOutcome(state);
        }

        public static ForestBattleState Preview(ForestBattleState state, ForestCommand command, out string error)
        {
            if (command?.kind == ActionKind.SwitchStance)
            {
                var copy = state.Copy(); return TryExecute(copy, command, out error) ? copy : null;
            }
            var prepared = PrepareCommand(state, command, out error); if (prepared == null) return null;
            ExecutePrepared(prepared, command); return prepared;
        }

        public static void EndPlayerPhase(ForestBattleState state)
        {
            if (state.phase != BattlePhase.Player || state.result != BattleResult.InProgress) throw new InvalidOperationException("当前不是我方行动阶段");
            foreach (var unit in state.unitsById.Values.Where(u => Alive(u) && state.Template(u.templateId).faction == Faction.Ally && !u.acted)
                .OrderBy(u => u.instanceId, StringComparer.Ordinal).ToArray())
            {
                while (Alive(unit) && !unit.acted)
                {
                    if (!TryExecute(state, new ForestCommand { actorId = unit.instanceId, kind = ActionKind.Wait }, out var error)) throw new InvalidOperationException(error);
                    if (state.result != BattleResult.InProgress) return;
                }
            }
            state.phase = BattlePhase.Enemy; state.revision++;
            foreach (var enemy in state.unitsById.Values.Where(u => Alive(u) && state.Template(u.templateId).faction == Faction.Enemy)) enemy.acted = false;
            Record(state, "EnemyPhase");
        }

        public static void EndEnemyPhase(ForestBattleState state)
        {
            if (state.phase != BattlePhase.Enemy || state.result != BattleResult.InProgress) throw new InvalidOperationException("当前不是敌方阶段");
            state.phase = BattlePhase.RoundEnd; state.revision++; Record(state, "RoundEnd");
            ResolveFruitBursts(state);
            AbsorbFog(state);
            ResolveDevourerSelfDestructs(state);
            var players = state.unitsById.Values.Where(u => Alive(u) && state.Template(u.templateId).faction == Faction.Ally)
                .OrderBy(u => u.instanceId, StringComparer.Ordinal).ToArray();
            foreach (var unit in players)
            {
                if (!Flying(unit)) unit.poisonStacks = Math.Max(0, Math.Min(state.Rules.poisonMaxStacks,
                    unit.poisonStacks + (state.groundFog.Contains(unit.cell) ? 1 : -1)));
                Record(state, "PoisonLayers", target: unit, amount: unit.poisonStacks);
            }
            var damage = players.Where(u => u.poisonStacks == state.Rules.poisonMaxStacks)
                .Select(u => new { unit = u, amount = PoisonDamage(state, u) }).ToArray();
            foreach (var hit in damage) Hurt(state, null, hit.unit, hit.amount, "PoisonDamage");
            RetryPendingDrops(state);
            CheckOutcome(state, true);
            if (state.result == BattleResult.InProgress) { ResolvePostBossReinforcements(state); state.round++; state.phase = BattlePhase.PlayerStart; }
        }
        public static void CheckOutcome(ForestBattleState state, bool roundEnd = false)
        {
            if (state.result != BattleResult.InProgress) return;
            if (!state.unitsById.Values.Any(u => Alive(u) && state.Template(u.templateId).faction == Faction.Ally))
            { state.result = BattleResult.Defeat; state.endReason = BattleEndReason.Eliminated; Record(state, "Elimination"); }
            else if (state.groundFog.Count == 0 && state.unitsById.Values.All(u => !Alive(u) || u.devourer == null || u.devourer.carriedFogCount == 0))
            { state.result = BattleResult.Victory; state.endReason = BattleEndReason.Cleared; Record(state, "Victory"); }
            else if (roundEnd && state.round >= state.Rules.roundLimit)
            { state.result = BattleResult.Defeat; state.endReason = BattleEndReason.Timeout; Record(state, "Timeout"); }
            if (state.result != BattleResult.InProgress) state.phase = BattlePhase.Finished;
        }
    }
}
