using System;
using System.IO;
using System.Linq;
using Zilong.Forest;

static class StatusBarChecks
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static ForestStatusEntry Entry(ForestBattleState s, UnitState u, string id) => ForestStatusDisplay.Build(s, u).Single(e => e.id == id);
    static void Main()
    {
        var s = new ForestSession(ForestDefaults.Create()).State; ForestRules.BeginPlayerPhase(s);
        var u = s.unitsById["C04"];
        Check(Entry(s, u, "form").title == "守形态" && Entry(s, u, "form").count == "2", "Initial guard duration");
        Check(!ForestStatusDisplay.Build(s, u).Any(e => e.id == "guard-radius"), "Permanent protection ability description omitted");
        Check(!ForestStatusDisplay.Build(s, u).Any(e => e.id == "poison" || e.id == "scar" || e.id == "purify" || e.id == "spirit"), "Inactive effects omitted");
        u.poisonStacks = 2; u.scarStacks = 3; u.reflectPercent = 60; u.stunnedUntilRound = 3; u.bonusActions = 2;
        u.awakeningSources.Add("C02"); u.curseSources.Add("C01");
        u.purificationCharges.Add(new PurificationCharge { sourceSpiritId = "display-check", clearedCount = 4 });
        u.carriedSpiritId = "display-check";
        Check(Entry(s, u, "poison").count == "2" && Entry(s, u, "scar").count == "3", "Layer badges reflect actual counts");
        Check(Entry(s, u, "reflect").count == "60%", "Applied reflection percentage retained");
        Check(Entry(s, u, "stun").count == "3" && Entry(s, u, "awakening").count == "1" && Entry(s, u, "curse").count == "1", "Applied stun, awakening and curse stacks retained");
        Check(Entry(s, u, "purify").count == "8" && Entry(s, u, "spirit").count == "1", "Remaining purification and carried spirit");
        u.acted = true; Check(!ForestStatusDisplay.Build(s, u).Any(e => e.id == "actions"), "Action counters omitted regardless of action rights");
        u.currentHp = 1; Check(Entry(s, u, "near-death").tone == StatusTone.Negative, "Near-death current health warning");
        s.Rules.scarHpPenalty = 7; s.Rules.purificationCellsPerScar = 3;
        Check(Entry(s, u, "scar").description.Contains("减少 7") && Entry(s, u, "scar").description.Contains("满 3 格"), "Effect details use authored parameters");
        var flyer = s.unitsById["C03"]; flyer.flight.pendingStance = FlightStance.Flying; flyer.flight.switchRound = 2;
        Check(!ForestStatusDisplay.Build(s, flyer).Any(e => e.id == "stance") && Entry(s, flyer, "pending-stance").count == "1", "Ordinary grounded state omitted; applied pending switch retained");
        flyer.flight.stance = FlightStance.Flying;
        Check(Entry(s, flyer, "stance").title == "飞行", "Active flying effect retained");
        ForestRules.Ability(flyer, "sprint").availableRound = 3;
        Check(!ForestStatusDisplay.Build(s, flyer).Any(e => e.id.StartsWith("cd-")), "Cooldown information omitted from effects bar");
        var caster = s.unitsById["C01"];
        Check(ForestStatusDisplay.Build(s, caster).Count == 0, "Character with no applied effects has no permanent skill or passive entries");
        ForestRules.Ability(caster, "talisman").remainingUses = 0;
        Check(!ForestStatusDisplay.Build(s, caster).Any(e => e.id.StartsWith("uses-")), "Skill uses omitted even when depleted");
        var boss = s.unitsById["M03"]; boss.maskBoss.releaseRound = 2;
        Check(!ForestStatusDisplay.Build(s, boss).Any(e => e.id == "mask") && Entry(s, boss, "charge").count == "1", "Boss phase description omitted; active charge retained");
        var tree = s.unitsById["M01A"]; var devourer = s.unitsById["M02-1"];
        Check(ForestStatusDisplay.Build(s, tree).Count == 0 && ForestStatusDisplay.Build(s, devourer).Count == 0, "Baseline fruit and zero carried fog omitted");
        tree.tree.fruitLevel = 2; devourer.devourer.carriedFogCount = 3;
        Check(Entry(s, tree, "fruit").count == "2" && Entry(s, devourer, "fog").count == "3", "Gained fruit stacks and nonzero carried fog retained");
        long revision = s.revision; uint random = s.randomState; int log = s.eventLog.Count, hp = u.currentHp;
        var list = ForestStatusDisplay.Build(s, u);
        Check(list.Select(e => e.id).Distinct().Count() == list.Count && list.All(e => !string.IsNullOrEmpty(e.description)), "Unique badges have effects");
        Check(s.revision == revision && s.randomState == random && s.eventLog.Count == log && u.currentHp == hp && u.poisonStacks == 2, "Rendering statuses does not mutate battle");
        var preview = s.Copy(); preview.unitsById[u.instanceId].poisonStacks = 1;
        Check(Entry(preview, preview.unitsById[u.instanceId], "poison").count == "1" && Entry(s, u, "poison").count == "2", "Preview display isolated from live state");
        u.lifeState = LifeState.Dead; u.currentHp = 0;
        Check(ForestStatusDisplay.Build(s, u).Count == 0, "Dead units do not advertise active buffs");
        string report = $"PASS effects-only status display ({checks} assertions): applied buffs/debuffs/marks, no actions/uses/cooldowns/permanent descriptions, active flight/forms, zero-state filtering, authored values and read-only state.\n";
        File.WriteAllText("Artifacts/status-bar-verification.txt", report); Console.Write(report);
    }
}
