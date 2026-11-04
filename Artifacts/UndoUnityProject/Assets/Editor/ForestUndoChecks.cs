using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zilong.Forest;

// One step is every committed operation a character makes in one round; the newest three steps can be undone.
public static class ForestUndoChecks
{
    static int checks;
    static Vector2Int P(int x, int y) => new Vector2Int(x, y);
    static UnitState U(ForestBattleState s, string id) => s.unitsById[id];
    static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception("Undo: " + text); }

    static ForestSession Fresh(out ForestBattleState s)
    {
        var d = ForestDefaults.Create();
        for (int i = 0; i < d.map.terrain.Length; i++) { d.map.terrain[i] = 0; d.map.regions[i] = 3; d.map.initialFog[i] = false; }
        d.map.rootCells = new Vector2Int[0];
        d.spawns[0].cell = P(2, 2); d.spawns[1].cell = P(4, 2); d.spawns[2].cell = P(2, 4); d.spawns[3].cell = P(4, 4);
        var session = new ForestSession(d);
        s = session.State;
        ForestRules.BeginPlayerPhase(s);
        return session;
    }
    static ForestCommand Wait(string actor) => new ForestCommand { actorId = actor, kind = ActionKind.Wait };
    // Mirrors the UI commit path: record before execution and drop the record when the rules reject the command.
    static void PlayerAct(ForestSession session, ForestCommand command)
    {
        bool recorded = session.RecordUndoPoint(command.actorId);
        if (!ForestRules.TryExecute(session.State, command, out var error))
        {
            if (recorded) session.DiscardUndoPoint();
            throw new Exception("Undo: " + (error ?? command.kind.ToString()));
        }
    }

    public static string Run()
    {
        checks = 0;

        // A character's whole round is one step.
        var session = Fresh(out var s);
        Check(session.UndoCount == 0, "new session has no undo points");
        Check(!session.TryUndo(out _, out _), "undo with no history fails");
        Check(!session.RecordUndoPoint(null), "empty actor is not recorded");
        var baseline = s.Copy();
        PlayerAct(session, new ForestCommand { actorId = "C03", kind = ActionKind.Sprint });
        Check(session.UndoCount == 1, "first operation records one step");
        Check(!session.RecordUndoPoint("C03"), "later operation of the same character adds no step");
        PlayerAct(session, Wait("C03"));
        PlayerAct(session, Wait("C03"));
        Check(U(s, "C03").statistics.actions == 2 && U(s, "C03").acted, "two committed operations executed");
        Check(session.TryUndo(out string actorId, out int remaining), "undo the character step");
        Check(actorId == "C03" && remaining == 0, "undo reports the actor and remaining steps");
        Check(SameState(s, baseline), "undo reverts every operation of the character this round");
        Check(session.UndoCount == 0, "history is consumed");

        // Different characters are separate steps, undone in reverse order.
        session = Fresh(out s);
        var start = s.Copy();
        PlayerAct(session, Wait("C03"));
        var afterC03 = s.Copy();
        PlayerAct(session, Wait("C01"));
        var afterC01 = s.Copy();
        PlayerAct(session, Wait("C02"));
        Check(session.UndoCount == 3, "three characters produce three steps");
        Check(session.TryUndo(out actorId, out remaining) && actorId == "C02" && remaining == 2, "last character undone first");
        Check(SameState(s, afterC01), "undo restores the state before the last character's first operation");
        Check(session.TryUndo(out actorId, out remaining) && actorId == "C01" && remaining == 1, "middle character undone second");
        Check(SameState(s, afterC03), "middle undo keeps the earlier character's operations");
        Check(session.TryUndo(out actorId, out remaining) && actorId == "C03" && remaining == 0, "first character undone last");
        Check(SameState(s, start), "all character steps undone");

        // Only the latest three character steps survive.
        session = Fresh(out s);
        session.RecordUndoPoint("C01");
        U(s, "C02").poisonStacks = 1; session.RecordUndoPoint("C02");
        U(s, "C03").poisonStacks = 2; session.RecordUndoPoint("C03");
        U(s, "C04").poisonStacks = 3; session.RecordUndoPoint("C04");
        Check(session.UndoCount == ForestSession.MaxUndoSteps, "undo history caps at three steps");
        Check(session.TryUndo(out _, out remaining) && U(s, "C04").poisonStacks == 3, "newest kept step undone first");
        Check(session.TryUndo(out _, out remaining) && U(s, "C03").poisonStacks == 2, "second kept step undone");
        Check(session.TryUndo(out _, out remaining) && U(s, "C02").poisonStacks == 1, "third kept step undone");
        Check(session.UndoCount == 0 && !session.TryUndo(out _, out _), "oldest step was dropped");
        Check(U(s, "C02").poisonStacks == 1, "dropped step cannot be recovered");

        // A rejected command must not consume a step.
        session = Fresh(out s);
        int historyBefore = session.UndoCount;
        bool recorded = session.RecordUndoPoint("C01");
        var invalid = new ForestCommand { actorId = "C01", kind = ActionKind.Talisman, targetCell = P(99, 99) };
        Check(!ForestRules.TryExecute(s, invalid, out _), "invalid command is rejected");
        if (recorded) session.DiscardUndoPoint();
        Check(session.UndoCount == historyBefore, "rejected command keeps the history unchanged");

        // The run clock never moves backwards.
        session = Fresh(out s);
        s.statistics.elapsedSeconds = 10d;
        session.RecordUndoPoint("C01");
        s.statistics.elapsedSeconds = 25d;
        Check(session.TryUndo(out _, out _) && Math.Abs(s.statistics.elapsedSeconds - 25d) < 0.001, "elapsed time is preserved on undo");

        // The snapshot detaches unit and random state.
        session = Fresh(out s);
        s.randomState = 424242u;
        session.RecordUndoPoint("C01");
        s.randomState = 7u;
        U(s, "C01").currentHp = 1;
        Check(session.TryUndo(out _, out _) && s.randomState == 424242u, "random state is restored");
        Check(U(s, "C01").currentHp == 90, "unit state is restored");

        // Units summoned after the snapshot must not survive the undo.
        session = Fresh(out s);
        session.RecordUndoPoint("C01");
        s.unitsById.Add("M04-9", new UnitState { instanceId = "M04-9", templateId = "M04", cell = P(1, 1), currentHp = 60, lifeState = LifeState.Alive });
        Check(session.TryUndo(out _, out _) && !s.unitsById.ContainsKey("M04-9"), "undo removes units that did not exist in the snapshot");

        // The event log is truncated and numbering continues from the restored prefix.
        session = Fresh(out s);
        int logBefore = s.eventLog.Count;
        session.RecordUndoPoint("C01");
        PlayerAct(session, Wait("C01"));
        Check(s.eventLog.Count > logBefore, "action writes events");
        Check(session.TryUndo(out _, out _), "undo the action");
        Check(s.eventLog.Count == logBefore, "undo truncates the event log back to the snapshot");
        PlayerAct(session, Wait("C02"));
        Check(s.eventLog.Last().sequence == s.eventLog.Count, "new events continue numbering after undo");

        // A new round gives the same character a new step; the old step is still undoable.
        session = Fresh(out s);
        var roundOne = s.Copy();
        PlayerAct(session, Wait("C03"));
        ForestRules.EndPlayerPhase(s);
        ForestRules.EndEnemyPhase(s);
        ForestRules.BeginPlayerPhase(s);
        Check(s.round == 2, "round advanced");
        var roundTwo = s.Copy();
        PlayerAct(session, Wait("C03"));
        Check(session.UndoCount == 2, "same character records a new step in a new round");
        Check(session.TryUndo(out actorId, out remaining) && actorId == "C03" && remaining == 1, "round-two step undone first");
        Check(SameState(s, roundTwo), "undo keeps the round-two start");
        Check(session.TryUndo(out actorId, out remaining) && actorId == "C03" && remaining == 0, "round-one step follows");
        Check(SameState(s, roundOne), "undo crosses the round boundary and restores round one");

        // The restored state still satisfies the pollution and resource audit.
        session = Fresh(out s);
        PlayerAct(session, Wait("C01"));
        Check(session.TryUndo(out _, out _), "undo for the audit check");
        Check(ForestAudit.Check(s).Count == 0, "restored state passes the battle audit");

        // Restart clears the history.
        session = Fresh(out s);
        PlayerAct(session, Wait("C01"));
        session.Restart();
        Check(session.UndoCount == 0, "restart clears the undo history");
        Check(session.State.round == 1 && session.State.phase == BattlePhase.PlayerStart, "restart rebuilds the initial state");

        return "PASS: " + checks + " undo assertions; one character's round operations form one step; three-step history; "
            + "cross-round restore; reinforcement removal; rejected commands; log truncation; elapsed and random rules.";
    }

    static bool SameState(ForestBattleState a, ForestBattleState b)
    {
        if (a.round != b.round || a.phase != b.phase || a.result != b.result || a.endReason != b.endReason
            || a.randomState != b.randomState || a.revision != b.revision) return false;
        if (a.unitsById.Count != b.unitsById.Count) return false;
        foreach (var pair in a.unitsById)
        {
            if (!b.unitsById.TryGetValue(pair.Key, out var other) || !SameUnit(pair.Value, other)) return false;
        }
        if (!a.groundFog.SetEquals(b.groundFog) || !a.remainingRewards.SetEquals(b.remainingRewards)) return false;
        if (a.spiritsById.Count != b.spiritsById.Count) return false;
        foreach (var pair in a.spiritsById)
        {
            if (!b.spiritsById.TryGetValue(pair.Key, out var other)) return false;
            var mine = pair.Value;
            if (other.sourceInstanceId != mine.sourceInstanceId || other.sourcePhase != mine.sourcePhase
                || other.carrierId != mine.carrierId || other.consumedBy != mine.consumedBy
                || other.locationKind != mine.locationKind || other.cell != mine.cell) return false;
        }
        if (a.pendingDrops.Count != b.pendingDrops.Count) return false;
        for (int i = 0; i < a.pendingDrops.Count; i++)
        {
            var mine = a.pendingDrops[i]; var other = b.pendingDrops[i];
            if (mine.spiritId != other.spiritId || mine.sourceInstanceId != other.sourceInstanceId
                || mine.origin != other.origin || mine.atDeathCell != other.atDeathCell) return false;
        }
        if (a.eventLog.Count != b.eventLog.Count) return false;
        for (int i = 0; i < a.eventLog.Count; i++)
        {
            var mine = a.eventLog[i]; var other = b.eventLog[i];
            if (mine.sequence != other.sequence || mine.revision != other.revision || mine.round != other.round
                || mine.kind != other.kind || mine.sourceId != other.sourceId || mine.targetId != other.targetId
                || mine.amount != other.amount || mine.requestedAmount != other.requestedAmount || mine.cell != other.cell) return false;
        }
        var sa = a.statistics; var sb = b.statistics;
        return sa.everNearDeath == sb.everNearDeath && sa.everAllyDied == sb.everAllyDied
            && sa.initialFogCount == sb.initialFogCount && sa.generatedFogCount == sb.generatedFogCount
            && sa.clearedFogCount == sb.clearedFogCount && Math.Abs(sa.elapsedSeconds - sb.elapsedSeconds) < 0.0001
            && sa.sourceDefeatedRounds.Count == sb.sourceDefeatedRounds.Count
            && sa.sourceDefeatedRounds.All(pair => sb.sourceDefeatedRounds.TryGetValue(pair.Key, out int round) && round == pair.Value);
    }

    static bool SameUnit(UnitState a, UnitState b)
    {
        if (a.instanceId != b.instanceId || a.templateId != b.templateId || a.cell != b.cell
            || a.currentHp != b.currentHp || a.poisonStacks != b.poisonStacks || a.scarStacks != b.scarStacks
            || a.lifeState != b.lifeState || a.acted != b.acted || a.everNearDeath != b.everNearDeath
            || a.movementCommitted != b.movementCommitted || a.bonusActions != b.bonusActions
            || a.stunnedUntilRound != b.stunnedUntilRound || a.reflectPercent != b.reflectPercent
            || a.carriedSpiritId != b.carriedSpiritId
            || !a.awakeningSources.SequenceEqual(b.awakeningSources)
            || !a.curseSources.SequenceEqual(b.curseSources)) return false;
        if (a.abilityStates.Count != b.abilityStates.Count || a.purificationCharges.Count != b.purificationCharges.Count) return false;
        for (int i = 0; i < a.abilityStates.Count; i++)
            if (a.abilityStates[i].abilityId != b.abilityStates[i].abilityId
                || a.abilityStates[i].remainingUses != b.abilityStates[i].remainingUses
                || a.abilityStates[i].availableRound != b.abilityStates[i].availableRound) return false;
        for (int i = 0; i < a.purificationCharges.Count; i++)
            if (a.purificationCharges[i].sourceSpiritId != b.purificationCharges[i].sourceSpiritId
                || a.purificationCharges[i].clearedCount != b.purificationCharges[i].clearedCount) return false;
        if (!SameFlight(a.flight, b.flight) || !SameForm(a.dualForm, b.dualForm) || !SameTree(a.tree, b.tree)
            || !SameDevourer(a.devourer, b.devourer) || !SameBoss(a.maskBoss, b.maskBoss)) return false;
        var x = a.statistics; var y = b.statistics;
        return x.actions == y.actions && x.damageDealt == y.damageDealt && x.damageTaken == y.damageTaken && x.fogCleared == y.fogCleared;
    }
    static bool SameFlight(FlightState a, FlightState b) => a == null ? b == null : b != null
        && a.stance == b.stance && a.pendingStance == b.pendingStance
        && a.stanceLockedRound == b.stanceLockedRound && a.switchRound == b.switchRound;
    static bool SameForm(DualFormState a, DualFormState b) => a == null ? b == null : b != null
        && a.form == b.form && a.protectedUnitId == b.protectedUnitId && a.protectionRound == b.protectionRound;
    static bool SameTree(TreeState a, TreeState b) => a == null ? b == null : b != null
        && a.fruitLevel == b.fruitLevel && a.nextBurstRound == b.nextBurstRound;
    static bool SameDevourer(DevourerState a, DevourerState b) => a == null ? b == null : b != null
        && a.nextPatrolIndex == b.nextPatrolIndex && a.carriedFogCount == b.carriedFogCount;
    static bool SameBoss(MaskBossState a, MaskBossState b) => a == null ? b == null : b != null
        && a.phaseIndex == b.phaseIndex && a.resolvedPhaseMask == b.resolvedPhaseMask && a.releaseRound == b.releaseRound
        && a.warningCenter == b.warningCenter && a.phaseChangedRevision == b.phaseChangedRevision;
}
