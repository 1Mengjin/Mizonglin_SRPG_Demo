using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mizonglin.Forest
{
    public enum BattlePhase { PlayerStart, Player, Enemy, RoundEnd, Finished }
    public enum BattleResult { InProgress, Victory, Defeat }
    public enum BattleEndReason { None, Cleared, Eliminated, Timeout }
    public enum LifeState { Alive, Dead }
    public enum FlightStance { Grounded, Flying }
    public enum DualForm { Guard, Attack }
    public enum SpiritLocation { Ground, Carried, Consumed, Pending }

    [Serializable] public sealed class AbilityState { public string abilityId; public int remainingUses, availableRound; }
    [Serializable] public sealed class PurificationCharge { public string sourceSpiritId; public int clearedCount; }
    [Serializable] public sealed class FlightState
    {
        public FlightStance stance, pendingStance;
        public int stanceLockedRound, switchRound;
    }
    [Serializable] public sealed class DualFormState { public DualForm form; public string protectedUnitId; public int protectionRound; }
    [Serializable] public sealed class TreeState { public int fruitLevel, nextBurstRound, lastBurstRound; }
    [Serializable] public sealed class DevourerState { public int nextPatrolIndex, carriedFogCount; }
    [Serializable] public sealed class MaskBossState { public int phaseIndex, resolvedPhaseMask, releaseRound; public Vector2Int warningCenter; public long phaseChangedRevision; }
    [Serializable] public sealed class UnitStatistics { public int actions, damageDealt, damageTaken, fogCleared; }

    [Serializable]
    public sealed class UnitState
    {
        public string instanceId, templateId;
        public Vector2Int cell;
        public int currentHp, poisonStacks, scarStacks;
        public LifeState lifeState;
        public bool acted, everNearDeath, movementCommitted;
        public int bonusActions, stunnedUntilRound, reflectPercent;
        public string carriedSpiritId;
        public List<string> awakeningSources = new List<string>();
        public List<string> curseSources = new List<string>();
        public List<AbilityState> abilityStates = new List<AbilityState>();
        public List<PurificationCharge> purificationCharges = new List<PurificationCharge>();
        public FlightState flight;
        public DualFormState dualForm;
        public TreeState tree;
        public DevourerState devourer;
        public MaskBossState maskBoss;
        public UnitStatistics statistics = new UnitStatistics();

        internal void CopyFrom(UnitState u)
        {
            instanceId = u.instanceId; templateId = u.templateId; cell = u.cell; currentHp = u.currentHp;
            poisonStacks = u.poisonStacks; scarStacks = u.scarStacks; lifeState = u.lifeState;
            bonusActions = u.bonusActions; stunnedUntilRound = u.stunnedUntilRound; reflectPercent = u.reflectPercent;
            acted = u.acted; everNearDeath = u.everNearDeath; movementCommitted = u.movementCommitted; carriedSpiritId = u.carriedSpiritId;
            awakeningSources.Clear(); awakeningSources.AddRange(u.awakeningSources);
            curseSources.Clear(); curseSources.AddRange(u.curseSources);
            abilityStates.Clear();
            foreach (var a in u.abilityStates) abilityStates.Add(new AbilityState { abilityId = a.abilityId, remainingUses = a.remainingUses, availableRound = a.availableRound });
            purificationCharges.Clear();
            foreach (var c in u.purificationCharges) purificationCharges.Add(new PurificationCharge { sourceSpiritId = c.sourceSpiritId, clearedCount = c.clearedCount });
            flight = u.flight == null ? null : new FlightState { stance = u.flight.stance, pendingStance = u.flight.pendingStance, stanceLockedRound = u.flight.stanceLockedRound, switchRound = u.flight.switchRound };
            dualForm = u.dualForm == null ? null : new DualFormState { form = u.dualForm.form, protectedUnitId = u.dualForm.protectedUnitId, protectionRound = u.dualForm.protectionRound };
            tree = u.tree == null ? null : new TreeState { fruitLevel = u.tree.fruitLevel, nextBurstRound = u.tree.nextBurstRound, lastBurstRound = u.tree.lastBurstRound };
            devourer = u.devourer == null ? null : new DevourerState { nextPatrolIndex = u.devourer.nextPatrolIndex, carriedFogCount = u.devourer.carriedFogCount };
            maskBoss = u.maskBoss == null ? null : new MaskBossState { phaseIndex = u.maskBoss.phaseIndex, resolvedPhaseMask = u.maskBoss.resolvedPhaseMask, releaseRound = u.maskBoss.releaseRound, warningCenter = u.maskBoss.warningCenter, phaseChangedRevision = u.maskBoss.phaseChangedRevision };
            statistics.actions = u.statistics.actions; statistics.damageDealt = u.statistics.damageDealt;
            statistics.damageTaken = u.statistics.damageTaken; statistics.fogCleared = u.statistics.fogCleared;
        }
    }

    [Serializable] public sealed class SpiritState
    {
        public string spiritId, sourceInstanceId, carrierId, consumedBy;
        public int sourcePhase;
        public SpiritLocation locationKind;
        public Vector2Int cell;
    }
    [Serializable] public sealed class PendingDrop { public string spiritId, sourceInstanceId; public Vector2Int origin; public bool atDeathCell; }
    [Serializable] public sealed class BattleEvent
    {
        public long sequence, revision;
        public int round, amount, requestedAmount;
        public string kind, sourceId, targetId;
        public Vector2Int cell;
    }
    [Serializable] public sealed class BattleStatistics
    {
        public bool everNearDeath, everAllyDied;
        public int initialFogCount, generatedFogCount, clearedFogCount;
        public double elapsedSeconds;
        // Split of the same focused wall clock: blocking time (player cannot act) and how much of it was the enemy phase.
        // elapsedSeconds - presentationSeconds is the time the player actually had control.
        public double presentationSeconds, enemyPhaseSeconds;
        public Dictionary<string, int> sourceDefeatedRounds = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    // Authoritative battle data. Presentation adapters never write their projected attributes back here.
    public sealed class ForestBattleState
    {
        readonly Dictionary<string, UnitTemplate> templates = new Dictionary<string, UnitTemplate>(StringComparer.Ordinal);
        public string ScenarioId { get; }
        public string RulesetId { get; }
        public string RootSourceInstanceId { get; }
        public MapDefinition Map { get; }
        public BattleRules Rules { get; }
        public int round = 1;
        public uint randomState = 2463534242;
        public BattlePhase phase = BattlePhase.PlayerStart;
        public BattleEndReason endReason;
        public BattleResult result = BattleResult.InProgress;
        public long revision;
        // Dead records must remain in this dictionary for carried fog and evaluation history.
        public readonly Dictionary<string, UnitState> unitsById = new Dictionary<string, UnitState>(StringComparer.Ordinal);
        public readonly HashSet<Vector2Int> remainingRewards = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> groundFog = new HashSet<Vector2Int>();
        public readonly Dictionary<string, SpiritState> spiritsById = new Dictionary<string, SpiritState>(StringComparer.Ordinal);
        public readonly List<PendingDrop> pendingDrops = new List<PendingDrop>();
        public readonly List<BattleEvent> eventLog = new List<BattleEvent>();
        public readonly BattleStatistics statistics = new BattleStatistics();

        internal ForestBattleState(ScenarioDefinition source)
        {
            source.ValidateOrThrow();
            var definition = source.Copy();
            ScenarioId = definition.scenarioId; RulesetId = definition.rulesetId;
            RootSourceInstanceId = definition.rootSourceInstanceId;
            Map = definition.map; Rules = definition.rules;
            foreach (var template in definition.unitTemplates) templates.Add(template.templateId, template);
            foreach (var spawn in definition.spawns)
            {
                var template = templates[spawn.templateId];
                var unit = new UnitState { instanceId = spawn.instanceId, templateId = spawn.templateId,
                    cell = spawn.cell, currentHp = template.maxHp, lifeState = LifeState.Alive };
                switch (template.behaviorKind)
                {
                    case BehaviorKind.FuSheng:
                        unit.abilityStates.Add(new AbilityState { abilityId = "talisman", remainingUses = template.talisman.maxUses });
                        unit.abilityStates.Add(new AbilityState { abilityId = "echo", remainingUses = -1, availableRound = 1 });
                        break;
                    case BehaviorKind.SuLing:
                        unit.abilityStates.Add(new AbilityState { abilityId = "heal", remainingUses = -1, availableRound = 1 });
                        unit.abilityStates.Add(new AbilityState { abilityId = "cleanse", remainingUses = -1, availableRound = 1 });
                        break;
                    case BehaviorKind.QingYu:
                        unit.flight = new FlightState { stance = FlightStance.Grounded, stanceLockedRound = 0 };
                        unit.abilityStates.Add(new AbilityState { abilityId = "stance", remainingUses = -1, availableRound = 1 });
                        unit.abilityStates.Add(new AbilityState { abilityId = "sprint", remainingUses = -1, availableRound = 1 });
                        break;
                    case BehaviorKind.AShuang:
                        unit.dualForm = new DualFormState { form = DualForm.Guard };
                        break;
                    case BehaviorKind.Tree:
                        unit.tree = new TreeState { fruitLevel = 1, nextBurstRound = template.tree.firstBurstRound };
                        break;
                    case BehaviorKind.Devourer:
                        unit.devourer = new DevourerState {
                            nextPatrolIndex = (Array.IndexOf(Map.patrolPoints, spawn.cell) + 1) % Map.patrolPoints.Length };
                        break;
                    case BehaviorKind.MaskBoss:
                        unit.maskBoss = new MaskBossState { phaseIndex = 1 };
                        break;
                }
                unitsById.Add(unit.instanceId, unit);
            }
            for (int y = 0; y < Map.height; y++) for (int x = 0; x < Map.width; x++)
                if (Map.initialFog[y * Map.width + x]) groundFog.Add(new Vector2Int(x, y));
            remainingRewards.UnionWith(Map.rewardCells ?? new Vector2Int[0]);
            statistics.initialFogCount = groundFog.Count;
        }

        // Return a detached value: callers cannot accidentally modify the session's base attributes.
        public UnitTemplate GetTemplate(string templateId) => templates[templateId].Copy();
        internal UnitTemplate Template(string templateId) => templates[templateId];
        internal UnitTemplate FindTemplate(BehaviorKind behavior)
        {
            UnitTemplate match = null;
            foreach (var t in templates.Values)
                if (t.behaviorKind == behavior && (match == null || string.CompareOrdinal(t.templateId, match.templateId) < 0)) match = t;
            return match;
        }

        public ForestBattleState Copy()
        {
            var definition = new ScenarioDefinition { schemaVersion = ScenarioDefinition.CurrentSchemaVersion,
                scenarioId = ScenarioId, rulesetId = RulesetId, rootSourceInstanceId = RootSourceInstanceId,
                map = Map.Copy(), rules = Rules.Copy(), unitTemplates = new List<UnitTemplate>(templates.Values).ConvertAll(t => t.Copy()).ToArray(),
                spawns = new SpawnDefinition[0] };
            return new ForestBattleState(this, definition);
        }

        ForestBattleState(ForestBattleState source, ScenarioDefinition definition)
        {
            ScenarioId = source.ScenarioId; RulesetId = source.RulesetId; RootSourceInstanceId = source.RootSourceInstanceId;
            Map = definition.map; Rules = definition.rules;
            foreach (var t in definition.unitTemplates) templates.Add(t.templateId, t);
            round = source.round; phase = source.phase; result = source.result; endReason = source.endReason; revision = source.revision; randomState = source.randomState;
            foreach (var pair in source.unitsById)
            {
                var u = pair.Value;
                var copy = new UnitState { instanceId = u.instanceId, templateId = u.templateId, cell = u.cell,
                    currentHp = u.currentHp, poisonStacks = u.poisonStacks, scarStacks = u.scarStacks, lifeState = u.lifeState,
                    acted = u.acted, bonusActions = u.bonusActions, stunnedUntilRound = u.stunnedUntilRound, reflectPercent = u.reflectPercent, everNearDeath = u.everNearDeath, movementCommitted = u.movementCommitted, carriedSpiritId = u.carriedSpiritId,
                    statistics = new UnitStatistics { actions = u.statistics.actions, damageDealt = u.statistics.damageDealt,
                        damageTaken = u.statistics.damageTaken, fogCleared = u.statistics.fogCleared } };
                copy.awakeningSources.AddRange(u.awakeningSources); copy.curseSources.AddRange(u.curseSources);
                foreach (var a in u.abilityStates) copy.abilityStates.Add(new AbilityState {
                    abilityId = a.abilityId, remainingUses = a.remainingUses, availableRound = a.availableRound });
                foreach (var c in u.purificationCharges) copy.purificationCharges.Add(new PurificationCharge {
                    sourceSpiritId = c.sourceSpiritId, clearedCount = c.clearedCount });
                if (u.flight != null) copy.flight = new FlightState { stance = u.flight.stance, stanceLockedRound = u.flight.stanceLockedRound,
                    pendingStance = u.flight.pendingStance, switchRound = u.flight.switchRound };
                if (u.dualForm != null) copy.dualForm = new DualFormState { form = u.dualForm.form,
                    protectedUnitId = u.dualForm.protectedUnitId, protectionRound = u.dualForm.protectionRound };
                if (u.tree != null) copy.tree = new TreeState { fruitLevel = u.tree.fruitLevel, nextBurstRound = u.tree.nextBurstRound, lastBurstRound = u.tree.lastBurstRound };
                if (u.devourer != null) copy.devourer = new DevourerState { nextPatrolIndex = u.devourer.nextPatrolIndex, carriedFogCount = u.devourer.carriedFogCount };
                if (u.maskBoss != null) copy.maskBoss = new MaskBossState { phaseIndex = u.maskBoss.phaseIndex, resolvedPhaseMask = u.maskBoss.resolvedPhaseMask, releaseRound = u.maskBoss.releaseRound, warningCenter = u.maskBoss.warningCenter, phaseChangedRevision = u.maskBoss.phaseChangedRevision };
                unitsById.Add(pair.Key, copy);
            }
            remainingRewards.UnionWith(source.remainingRewards);
            groundFog.UnionWith(source.groundFog);
            foreach (var pair in source.spiritsById)
            {
                var s = pair.Value;
                spiritsById.Add(pair.Key, new SpiritState { spiritId = s.spiritId, sourceInstanceId = s.sourceInstanceId,
                    sourcePhase = s.sourcePhase, carrierId = s.carrierId, consumedBy = s.consumedBy, locationKind = s.locationKind, cell = s.cell });
            }
            foreach (var p in source.pendingDrops) pendingDrops.Add(new PendingDrop { spiritId = p.spiritId, sourceInstanceId = p.sourceInstanceId, origin = p.origin, atDeathCell = p.atDeathCell });
            foreach (var e in source.eventLog) eventLog.Add(new BattleEvent { sequence = e.sequence, revision = e.revision, round = e.round,
                amount = e.amount, requestedAmount = e.requestedAmount, kind = e.kind, sourceId = e.sourceId, targetId = e.targetId, cell = e.cell });
            statistics.everNearDeath = source.statistics.everNearDeath; statistics.everAllyDied = source.statistics.everAllyDied;
            statistics.initialFogCount = source.statistics.initialFogCount; statistics.generatedFogCount = source.statistics.generatedFogCount;
            statistics.clearedFogCount = source.statistics.clearedFogCount; statistics.elapsedSeconds = source.statistics.elapsedSeconds;
            statistics.presentationSeconds = source.statistics.presentationSeconds; statistics.enemyPhaseSeconds = source.statistics.enemyPhaseSeconds;
            foreach (var pair in source.statistics.sourceDefeatedRounds) statistics.sourceDefeatedRounds.Add(pair.Key, pair.Value);
        }

        // Commit one fully simulated action while preserving unit identities used by selection and HUD adapters.
        internal void CommitFrom(ForestBattleState source)
        {
            round = source.round; phase = source.phase; result = source.result; endReason = source.endReason; revision = source.revision; randomState = source.randomState;
            // Undo restores a snapshot from before reinforcements existed; never keep live-only units.
            var removed = new List<string>();
            foreach (var id in unitsById.Keys) if (!source.unitsById.ContainsKey(id)) removed.Add(id);
            foreach (var id in removed) unitsById.Remove(id);
            foreach (var pair in source.unitsById)
            {
                if (!unitsById.TryGetValue(pair.Key, out var unit)) { unit = new UnitState(); unitsById.Add(pair.Key, unit); }
                unit.CopyFrom(pair.Value);
            }
            remainingRewards.Clear(); remainingRewards.UnionWith(source.remainingRewards);
            groundFog.Clear();
            groundFog.UnionWith(source.groundFog);
            spiritsById.Clear();
            foreach (var pair in source.spiritsById)
            {
                var s = pair.Value;
                spiritsById.Add(pair.Key, new SpiritState { spiritId = s.spiritId, sourceInstanceId = s.sourceInstanceId, carrierId = s.carrierId,
                    consumedBy = s.consumedBy, sourcePhase = s.sourcePhase, locationKind = s.locationKind, cell = s.cell });
            }
            pendingDrops.Clear();
            foreach (var p in source.pendingDrops) pendingDrops.Add(new PendingDrop { spiritId = p.spiritId, sourceInstanceId = p.sourceInstanceId, origin = p.origin, atDeathCell = p.atDeathCell });
            eventLog.Clear(); eventLog.AddRange(source.eventLog);
            statistics.everNearDeath = source.statistics.everNearDeath; statistics.everAllyDied = source.statistics.everAllyDied;
            statistics.initialFogCount = source.statistics.initialFogCount; statistics.generatedFogCount = source.statistics.generatedFogCount;
            statistics.clearedFogCount = source.statistics.clearedFogCount; statistics.elapsedSeconds = source.statistics.elapsedSeconds;
            statistics.presentationSeconds = source.statistics.presentationSeconds; statistics.enemyPhaseSeconds = source.statistics.enemyPhaseSeconds;
            statistics.sourceDefeatedRounds.Clear();
            foreach (var pair in source.statistics.sourceDefeatedRounds) statistics.sourceDefeatedRounds.Add(pair.Key, pair.Value);
        }
        public UnitState At(Vector2Int cell)
        {
            foreach (var unit in unitsById.Values)
                if (unit.lifeState == LifeState.Alive && unit.cell == cell) return unit;
            return null;
        }
        public int CarriedFogCount
        {
            get { int count = 0; foreach (var unit in unitsById.Values) count += unit.devourer?.carriedFogCount ?? 0; return count; }
        }
        public int LivingSourceCount
        {
            get { int count = 0; foreach (var unit in unitsById.Values) if (unit.tree != null && unit.lifeState == LifeState.Alive) count++; return count; }
        }
    }

    public sealed class ForestSession
    {
        public const int MaxUndoSteps = 3;
        sealed class UndoPoint
        {
            public ForestBattleState state;
            public string actorId;
            public int round;
        }
        readonly ScenarioDefinition initialDefinition;
        readonly uint initialRandomSeed = unchecked((uint)Guid.NewGuid().GetHashCode()) | 1u;
        readonly List<UndoPoint> undoPoints = new List<UndoPoint>(MaxUndoSteps);
        public uint InitialRandomSeed => initialRandomSeed;
        public ScenarioDefinition InitialDefinition => initialDefinition.Copy();
        public ForestBattleState State { get; private set; }
        public int UndoCount => undoPoints.Count;
        public ForestSession(ScenarioDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition), "迷踪林配置为空");
            definition.ValidateOrThrow();
            initialDefinition = definition.Copy();
            Restart();
        }
        // One step covers every committed operation a character makes in one round.
        // Returns true only when this round's first operation created a new undo point.
        public bool RecordUndoPoint(string actorId)
        {
            if (string.IsNullOrEmpty(actorId) || State.result != BattleResult.InProgress) return false;
            foreach (var point in undoPoints)
                if (point.round == State.round && point.actorId == actorId) return false;
            if (undoPoints.Count >= MaxUndoSteps) undoPoints.RemoveAt(0);
            undoPoints.Add(new UndoPoint { state = State.Copy(), actorId = actorId, round = State.round });
            return true;
        }
        public void DiscardUndoPoint() { if (undoPoints.Count > 0) undoPoints.RemoveAt(undoPoints.Count - 1); }
        public bool TryUndo(out string actorId, out int remaining)
        {
            if (undoPoints.Count == 0) { actorId = null; remaining = 0; return false; }
            var point = undoPoints[undoPoints.Count - 1]; undoPoints.RemoveAt(undoPoints.Count - 1);
            double elapsed = State.statistics.elapsedSeconds;
            double presentation = State.statistics.presentationSeconds, enemyPhase = State.statistics.enemyPhaseSeconds;
            State.CommitFrom(point.state);
            // Undo rewinds the battle state, not the real elapsed time kept for the run report.
            State.statistics.elapsedSeconds = Math.Max(elapsed, State.statistics.elapsedSeconds);
            State.statistics.presentationSeconds = Math.Max(presentation, State.statistics.presentationSeconds);
            State.statistics.enemyPhaseSeconds = Math.Max(enemyPhase, State.statistics.enemyPhaseSeconds);
            actorId = point.actorId; remaining = undoPoints.Count; return true;
        }
        public void ClearUndoPoints() { undoPoints.Clear(); }
        public void Restart() { undoPoints.Clear(); State = new ForestBattleState(initialDefinition); State.randomState = initialRandomSeed; }
    }
}
