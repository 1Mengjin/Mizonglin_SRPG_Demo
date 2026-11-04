using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mizonglin.Forest;
public static class ForestReinforcementChecks
{
    static int checks;
    static Vector2Int P(int x,int y)=>new Vector2Int(x,y);
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static ForestSession Fresh()
    {
        var session=new ForestSession(ForestDefaults.Create());ForestRules.BeginPlayerPhase(session.State);session.State.randomState=42;return session;
    }
    static void KillBoss(ForestBattleState s)
    {
        s.unitsById["C01"].cell=P(13,3);s.unitsById["M03"].maskBoss.phaseIndex=4;s.unitsById["M03"].currentHp=1;
        Check(ForestRules.TryExecute(s,new ForestCommand{actorId="C01",targetId="M03",kind=ActionKind.BasicAttack},out var error),error);
        Check(s.unitsById["M03"].lifeState==LifeState.Dead,"boss actually dies from final-stage attack");
    }
    static void RoundEnd(ForestBattleState s)
    {
        if(s.phase==BattlePhase.PlayerStart)ForestRules.BeginPlayerPhase(s);
        ForestRules.EndPlayerPhase(s);
        if(s.phase==BattlePhase.Enemy)ForestRules.EndEnemyPhase(s);
    }
    static Vector2Int[] Anchors()=>new[]{P(15,1),P(16,1),P(15,2),P(16,2)};
    static void VerifyWave(ForestBattleState before,ForestBattleState after)
    {
        var events=after.eventLog.Skip(before.eventLog.Count).ToArray();
        var requests=events.Where(e=>e.kind=="ReinforcementOrigin").ToArray();
        Check(requests.Length==2,"two requests in the single wave");
        Check(requests.Select(e=>e.cell).Distinct().Count()==2 && requests.All(e=>ForestRules.IsPostBossSpawnCell(e.cell)),"distinct origins from four pictured cells");
        var occupied=new HashSet<Vector2Int>(before.unitsById.Values.Where(ForestRules.Alive).Select(u=>u.cell));
        foreach(var request in requests)
        {
            var unit=after.unitsById[request.targetId];
            var eligible=new List<Vector2Int>();
            for(int y=0;y<before.Map.height;y++)for(int x=0;x<before.Map.width;x++)
            {
                var cell=P(x,y);if(before.Map.Walkable(cell)&&!occupied.Contains(cell)&&ForestRules.SpiritAt(before,cell)==null)eligible.Add(cell);
            }
            int nearest=eligible.Min(p=>ForestRules.Distance(p,request.cell));
            Check(ForestRules.Distance(unit.cell,request.cell)==nearest,"fallback chooses nearest free cell");
            Check(before.Map.Walkable(unit.cell) && !occupied.Contains(unit.cell) && ForestRules.SpiritAt(before,unit.cell)==null,"spawn does not overwrite units, spirits, obstacles");
            Check(unit.templateId=="M04" && unit.currentHp==after.GetTemplate("M04").maxHp,"summoned corpse uses official template");
            occupied.Add(unit.cell);
        }
        Check(events.Single(e=>e.kind=="PostBossWave").amount==2,"wave logs actual count2");
    }
    public static string Run()
    {
        checks=0;var session=Fresh();var s=session.State;RoundEnd(s);Check(!s.eventLog.Any(e=>e.kind=="PostBossWave"),"no wave while boss alive");
        session=Fresh();s=session.State;KillBoss(s);Check(!s.eventLog.Any(e=>e.kind=="PostBossWave"),"death does not double-spawn immediately");
        var first= s.Copy();RoundEnd(s);VerifyWave(first,s);
        Check(s.unitsById.Keys.Count(id=>id.Contains(":reinforcement:"))==2,"the only wave adds2 uniquely identified corpses");
        var afterFirst=s.Copy();
        for(int i=0;i<3;i++)RoundEnd(s);
        Check(s.eventLog.Count(e=>e.kind=="PostBossWave")==1,"one wave per battle, never repeated on later rounds");
        Check(s.unitsById.Keys.Count(id=>id.Contains(":reinforcement:"))==2,"later rounds never add reinforcements");
        Check(!s.eventLog.Skip(afterFirst.eventLog.Count).Any(e=>e.kind=="PostBossWave"||e.kind=="SummonShortfall"||e.kind=="CorpseSummoned"),"later rounds stay silent instead of reporting a shortage");
        Check(s.unitsById.Values.Where(ForestRules.Alive).GroupBy(u=>u.cell).All(g=>g.Count()==1),"no overlapping alive units after repeated rounds");
        session=Fresh();s=session.State;KillBoss(s);
        var anchors=Anchors();int index=0;foreach(var ally in s.unitsById.Values.Where(u=>s.GetTemplate(u.templateId).faction==Faction.Ally))ally.cell=anchors[index++];
        var occupiedBefore=s.Copy();RoundEnd(s);VerifyWave(occupiedBefore,s);
        Check(s.eventLog.Where(e=>e.kind=="CorpseSummoned").All(e=>!ForestRules.IsPostBossSpawnCell(e.cell)),"all preferred cells occupied still places2 nearby");
        session=Fresh();s=session.State;KillBoss(s);foreach(var p in Anchors())s.Map.terrain[p.y*s.Map.width+p.x]=1;
        var blockedBefore=s.Copy();RoundEnd(s);VerifyWave(blockedBefore,s);
        session=Fresh();s=session.State;KillBoss(s);index=0;
        foreach(var p in Anchors()){string id="blocked-spirit:"+index++;s.spiritsById.Add(id,new SpiritState{spiritId=id,sourceInstanceId="M03",sourcePhase=1,locationKind=SpiritLocation.Ground,cell=p});}
        var spiritBefore=s.Copy();RoundEnd(s);VerifyWave(spiritBefore,s);
        session=Fresh();s=session.State;KillBoss(s);uint seed=s.randomState;var trial=s.Copy();RoundEnd(trial);
        Check(s.randomState==seed && !s.eventLog.Any(e=>e.kind=="PostBossWave"),"copied round-end simulation does not mutate original");RoundEnd(s);
        Check(s.randomState==trial.randomState && s.eventLog.Where(e=>e.kind=="CorpseSummoned").Select(e=>e.cell).SequenceEqual(trial.eventLog.Where(e=>e.kind=="CorpseSummoned").Select(e=>e.cell)),"same random seed gives same wave and fallback");
        session=Fresh();s=session.State;KillBoss(s);
        for(int i=0;i<s.Map.terrain.Length;i++)s.Map.terrain[i]=1;
        foreach(var u in s.unitsById.Values.Where(ForestRules.Alive))s.Map.terrain[u.cell.y*s.Map.width+u.cell.x]=0;
        RoundEnd(s);Check(s.eventLog.Single(e=>e.kind=="PostBossWave").amount==0 && s.eventLog.Any(e=>e.kind=="SummonShortfall"&&e.amount==2),"full map reports shortage without overwriting or hanging");
        var emptyWave=s.Copy();RoundEnd(s);
        Check(s.eventLog.Count(e=>e.kind=="PostBossWave")==1 && !s.eventLog.Skip(emptyWave.eventLog.Count).Any(e=>e.kind=="SummonShortfall"||e.kind=="CorpseSummoned"),"an empty first wave still counts as the only wave");
        session=Fresh();s=session.State;KillBoss(s);s.Rules.roundLimit=1;RoundEnd(s);Check(s.result==BattleResult.Defeat && !s.eventLog.Any(e=>e.kind=="PostBossWave"),"timeout prevents wave");
        session=Fresh();s=session.State;KillBoss(s);foreach(var tree in s.unitsById.Values.Where(u=>u.tree!=null)){tree.lifeState=LifeState.Dead;tree.currentHp=0;}s.groundFog.Clear();RoundEnd(s);
        Check(s.result==BattleResult.Victory && !s.eventLog.Any(e=>e.kind=="PostBossWave"),"victory prevents wave");
        session=Fresh();s=session.State;KillBoss(s);foreach(var u in s.unitsById.Values.Where(u=>s.GetTemplate(u.templateId).faction==Faction.Ally)){u.currentHp=1;u.poisonStacks=3;s.groundFog.Add(u.cell);}RoundEnd(s);
        Check(s.result==BattleResult.Defeat && !s.eventLog.Any(e=>e.kind=="PostBossWave"),"allied elimination prevents wave");
        session=Fresh();s=session.State;KillBoss(s);RoundEnd(s);ForestRules.BeginPlayerPhase(s);ForestRules.EndPlayerPhase(s);
        var corpse=s.unitsById.Values.First(u=>u.instanceId.Contains(":reinforcement:"));Check(ForestRules.ChooseEnemyAction(s,corpse.instanceId)!=null,"wave corpses join next enemy phase");
        session.Restart();ForestRules.BeginPlayerPhase(session.State);RoundEnd(session.State);Check(!session.State.unitsById.Keys.Any(id=>id.Contains(":reinforcement:")) && !session.State.eventLog.Any(e=>e.kind=="PostBossWave"),"restart removes waves and stops spawning until boss dies again");
        return "PASS post-boss reinforcements ("+checks+" assertions): real final death; one wave per battle on the death round only; exact preferred tiles; occupied/blocked/spirit nearest fallback; no overlaps; RNG/copy isolation; full-map shortage stays a single attempt; finish/timeout/elimination stop; AI and restart.";
    }
}
