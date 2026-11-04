using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zilong.Forest;

static class ForestTutorialChecks
{
    static int count;
    static void Check(bool ok, string why) { count++; if (!ok) throw new Exception(why); }
    static void Main()
    {
        var s = new ForestSession(ForestDefaults.Create()).State; ForestRules.BeginPlayerPhase(s);
        var guide = new ForestTutorialGuide(); var actor = s.unitsById["C01"]; var boss = s.unitsById["M03"];
        s.groundFog.Clear(); foreach (var u in s.unitsById.Values) u.poisonStacks = 0;
        Check(guide.Pending(s) == ForestTutorialTopic.None, "No tutorial merely because far-away enemies exist");
        var fog = ForestPathfinder.Reachable(s, actor).costs.Keys.First(p => p != actor.cell && s.At(p) == null); s.groundFog.Add(fog);
        Check(guide.Pending(s, actor.instanceId, path: new[] { actor.cell, fog }) == ForestTutorialTopic.Fog, "Path contact catches fog before commit");
        Check(!guide.Pending(s).HasFlag(ForestTutorialTopic.Fog), "Hovering or distant fog does not count as contact");
        actor.cell = fog; Check(guide.Pending(s).HasFlag(ForestTutorialTopic.Fog), "Already standing in fog triggers");
        actor.cell += Vector2Int.left; actor.poisonStacks = 1;
        Check(guide.Pending(s).HasFlag(ForestTutorialTopic.Fog), "Poison from round-end fog triggers"); actor.poisonStacks = 0;
        var flyer = s.unitsById["C03"]; flyer.flight.stance = FlightStance.Flying;
        Check(!guide.Pending(s, flyer.instanceId, path: new[] { flyer.cell, fog }).HasFlag(ForestTutorialTopic.Fog), "Flying over fog does not count as grounded contact");
        guide.MarkShown(ForestTutorialTopic.Fog);
        Check(!guide.Pending(s, actor.instanceId, path: new[] { actor.cell, fog }).HasFlag(ForestTutorialTopic.Fog), "Same topic shown once per run");
        Check(guide.Pending(s, boss.instanceId) == ForestTutorialTopic.Mask, "Inspecting boss triggers encounter");
        Check(guide.Pending(s, actor.instanceId, boss.instanceId) == ForestTutorialTopic.Mask, "Targeting boss triggers encounter");
        var approach = boss.cell + Vector2Int.left * ForestTutorialGuide.EncounterDistance;
        Check(guide.Pending(s, actor.instanceId, path: new[] { actor.cell, approach }) == ForestTutorialTopic.Mask, "Movement path catches approaching boss");
        actor.cell = approach;
        Check(guide.Pending(s).HasFlag(ForestTutorialTopic.Mask), "Actual proximity catches arrival and teleport");
        guide.MarkShown(ForestTutorialTopic.Mask); Check(guide.Pending(s) == ForestTutorialTopic.None, "Seen encounters do not repeat");
        guide.Reset(); s.groundFog.Add(actor.cell);
        Check(guide.Pending(s) == (ForestTutorialTopic.Fog | ForestTutorialTopic.Mask), "Both topics can queue");
        Check(ForestTutorialGuide.Body(ForestTutorialTopic.Mask, s).Contains("携带") && !ForestTutorialGuide.Body(ForestTutorialTopic.Mask, s).Contains("自动拾取"), "Tutorial matches current manual spirit controls");
        s.Rules.scarHpPenalty = 7; s.Rules.purificationCellsPerSpirit = 18; s.Rules.purificationCellsPerScar = 6;
        string text = ForestTutorialGuide.Body(ForestTutorialTopic.Mask, s);
        Check(text.Contains("18格") && text.Contains("6 格") && text.Contains("减少 7"), "Tutorial uses authored purification and scar values");
        s.Rules.poisonDamagePercent = 12; s.Rules.poisonMovePenalty = 2;
        text = ForestTutorialGuide.Body(ForestTutorialTopic.Fog, s);
        Check(text.Contains("12%") && text.Contains("移动力减少 2"), "Tutorial uses authored poison values");
        long revision = s.revision; int log = s.eventLog.Count; uint random = s.randomState;
        guide.Pending(s); ForestTutorialGuide.Body(ForestTutorialTopic.Mask, s);
        Check(s.revision == revision && s.eventLog.Count == log && s.randomState == random, "Tutorial evaluation is read-only");
        s.result = BattleResult.Victory; Check(guide.Pending(s) == ForestTutorialTopic.None, "Finished battles do not open new tutorials");
        string report = $"PASS tutorial guide ({count} assertions): first fog/path/poison contact, flight, boss inspection/target/proximity, per-run repeat control, queued topics, authored values and read-only state.\n";
        File.WriteAllText("Artifacts/tutorial-guide-verification.txt", report); Console.Write(report);
    }
}
