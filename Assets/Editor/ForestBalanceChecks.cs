using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mizonglin.Demo;
using Mizonglin.Forest;

public static class ForestBalanceChecks
{
    static int checks;
    static Vector2Int P(int x,int y)=>new Vector2Int(x,y);
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static ForestSession Fresh()
    {
        var d=ForestDefaults.Create();
        for(int i=0;i<d.map.terrain.Length;i++){d.map.terrain[i]=0;d.map.regions[i]=3;d.map.initialFog[i]=false;}d.map.initialFog[13*d.map.width]=true;
        d.spawns[0].cell=P(2,2);d.spawns[1].cell=P(8,2);d.spawns[2].cell=P(2,4);d.spawns[3].cell=P(4,4);
        d.spawns[4].cell=P(9,3);
        var session=new ForestSession(d);ForestRules.BeginPlayerPhase(session.State);return session;
    }
    static ForestCommand Teleport(Vector2Int cell)=>new ForestCommand{actorId="C01",targetId="C02",kind=ActionKind.Echo,targetCell=cell};
    public static string Run()
    {
        checks=0;var session=Fresh();var s=session.State;var c=Teleport(P(3,2));
        var preview=ForestRules.Preview(s,c,out var error);
        Check(preview!=null && preview.unitsById["C02"].cell==P(3,2) && s.unitsById["C02"].cell==P(8,2),"distance6 teleport preview");
        s.unitsById["C02"].cell=P(9,2);Check(!ForestRules.TryExecute(s,c,out _),"distance7 rejected");s.unitsById["C02"].cell=P(8,2);
        foreach(var cell in new[]{P(3,3),P(2,2),P(-1,2),P(8,2)}){c.targetCell=cell;Check(!ForestRules.TryExecute(s,c,out _),"invalid destination "+cell);}
        c.targetCell=P(3,2);s.Map.terrain[2*s.Map.width+3]=1;Check(!ForestRules.TryExecute(s,c,out _),"blocked destination");s.Map.terrain[2*s.Map.width+3]=0;
        foreach(var id in new[]{"C01","M03","missing"}){c.targetId=id;Check(!ForestRules.TryExecute(s,c,out _),"invalid target "+id);}
        c.targetId="C02";s.unitsById["C02"].lifeState=LifeState.Dead;Check(!ForestRules.TryExecute(s,c,out _),"dead teammate");s.unitsById["C02"].lifeState=LifeState.Alive;
        s.unitsById["C02"].acted=true;s.unitsById["C02"].movementCommitted=true;
        Check(ForestRules.TryExecute(s,c,out error),error);
        Check(s.unitsById["C02"].acted && s.unitsById["C02"].movementCommitted && s.unitsById["C01"].acted,"teleport preserves teammate action state");
        Check(s.unitsById["C01"].currentHp==90 && ForestRules.Ability(s.unitsById["C01"],"echo").availableRound==4,"teleport no HP cost and CD3");
        s.unitsById["C01"].acted=false;Check(!ForestRules.TryExecute(s,Teleport(P(2,3)),out _),"teleport CD enforced");
        s=Fresh().State;c=Teleport(P(3,3));c.path=new List<Vector2Int>{P(2,2),P(3,2)};
        Check(ForestRules.TryExecute(s,c,out error) && s.unitsById["C02"].cell==P(3,3),"destination relative to moved caster: "+error);
        s=Fresh().State;var bird=s.unitsById["C03"];int baseMove=ForestRules.Stats(s,bird).move;bird.flight.stance=FlightStance.Flying;
        Check(ForestRules.Stats(s,bird).move==baseMove+1,"flying+1");
        Check(ForestPathfinder.ValidatePath(s,bird,new List<Vector2Int>{P(2,4),P(3,4),P(4,4),P(5,4),P(6,4),P(7,4)},out _)==null,"five-step flight path");
        bird.poisonStacks=2;Check(ForestRules.Stats(s,bird).move==baseMove,"flying poison still subtracts1");bird.flight.stance=FlightStance.Grounded;Check(ForestRules.Stats(s,bird).move==baseMove-1,"grounded restores move");
        session=Fresh();s=session.State;s.unitsById["C01"].cell=P(13,3);s.randomState=42;
        var attack=new ForestCommand{actorId="C01",targetId="M03",kind=ActionKind.BasicAttack};
        for(int phase=1;phase<=4;phase++)
        {
            s.unitsById["C01"].acted=false;s.unitsById["M03"].currentHp=1;
            int before=s.unitsById.Count;uint rng=s.randomState;
            preview=ForestRules.Preview(s,attack,out error);
            Check(preview!=null && s.unitsById.Count==before && s.randomState==rng,"summon preview isolated");
            Check(ForestRules.TryExecute(s,attack,out error),error);
            int count=phase<4?1:0;Check(s.unitsById.Count==before+count,"summon exactly1 on revival, none at death");
            Check(s.randomState==preview.randomState,"summon random state preview/commit agreement");
            foreach(var e in s.eventLog.Where(e=>e.kind=="CorpseSummoned").Skip(phase-1))
            {
                var u=s.unitsById[e.targetId];Check(u.currentHp==s.GetTemplate("M04").maxHp && Math.Abs(u.cell.x-14)<=1 && Math.Abs(u.cell.y-3)<=1,"summon location/full template HP");
                Check(ForestRules.SpiritAt(s,u.cell)==null,"summon avoids ground spirit");
                u.cell=P(0,s.eventLog.Where(a=>a.kind=="CorpseSummoned").TakeWhile(a=>a.targetId!=u.instanceId).Count());
            }
            foreach(var spirit in s.spiritsById.Values){spirit.locationKind=SpiritLocation.Consumed;spirit.consumedBy="C01";}
        }
        Check(s.eventLog.Count(e=>e.kind=="CorpseSummoned")==3,"three revivals produce3 corpses");
        Check(s.unitsById.Values.Where(ForestRules.Alive).GroupBy(u=>u.cell).All(g=>g.Count()==1),"no unit overlap");
        var view=new BattleState();view.BindForest(s,session.InitialDefinition);Check(view.units.Count==s.unitsById.Values.Count(ForestRules.Alive),"summons reach presentation adapter");
        s.phase=BattlePhase.Enemy;var summonedId=s.eventLog.First(e=>e.kind=="CorpseSummoned").targetId;
        Check(ForestRules.ChooseEnemyAction(s,summonedId)!=null,"summoned corpse participates in enemy AI");
        session.Restart();Check(session.State.unitsById.Count==11 && session.State.unitsById["M03"].maskBoss.phaseIndex==1,"restart clears summons");
        s=Fresh().State;s.unitsById["C01"].cell=P(13,3);s.unitsById["M03"].currentHp=1;
        for(int y=2;y<=4;y++)for(int x=13;x<=15;x++)if(P(x,y)!=P(14,3)&&P(x,y)!=P(13,3)&&P(x,y)!=P(15,3))s.Map.terrain[y*s.Map.width+x]=1;
        // Spirit drops onto the only free cell; summoning must not overwrite it or nearby units.
        Check(ForestRules.TryExecute(s,attack,out error),error);
        Check(s.unitsById.Count==11 && s.eventLog.Any(e=>e.kind=="SummonShortfall" && e.amount==1),"no free neighbor handles shortfall safely");
        return "PASS balance changes ("+checks+" assertions): teleport range/target/destination/CD/action/preview/moved caster; flight movement/poison; revive summons count/randomness/occupancy/spirits/AI/adapter/restart/shortfall.";
    }
}
