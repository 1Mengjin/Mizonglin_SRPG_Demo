using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mizonglin.Forest;
public static class ForestSynergyChecks
{
    static int n;
    static Vector2Int P(int x,int y)=>new Vector2Int(x,y);
    static UnitState U(ForestBattleState s,string id)=>s.unitsById[id];
    static void Check(bool b,string why){n++;if(!b)throw new Exception(why);}
    static ForestSession Fresh(int round=1)
    {
        var d=ForestDefaults.Create();for(int i=0;i<d.map.terrain.Length;i++){d.map.terrain[i]=0;d.map.regions[i]=3;d.map.initialFog[i]=false;}d.map.initialFog[13*d.map.width]=true;
        d.spawns[0].cell=P(2,2);d.spawns[1].cell=P(4,2);d.spawns[2].cell=P(2,4);d.spawns[3].cell=P(12,8);
        d.unitTemplates.First(t=>t.behaviorKind==BehaviorKind.Corpse).maxHp=600;
        var session=new ForestSession(d);session.State.round=round;ForestRules.BeginPlayerPhase(session.State);return session;
    }
    static ForestCommand C(string actor,ActionKind kind,string target=null,params Vector2Int[] path)=>new ForestCommand{actorId=actor,kind=kind,targetId=target,path=path.Length==0?null:path.ToList()};
    static void Act(ForestBattleState s,ForestCommand c){Check(ForestRules.TryExecute(s,c,out var error),error??c.kind.ToString());}
    static void Hit(ForestBattleState s,string target){s.phase=BattlePhase.Enemy;U(s,"M04-1").acted=false;Act(s,C("M04-1",ActionKind.BasicAttack,target));}
    public static string Run()
    {
        n=0;var session=Fresh();var s=session.State;
        Check(ForestRules.Stats(s,U(s,"C04")).defense==54,"guard defense36*150%");
        var a=Fresh(3).State;Check(ForestRules.Stats(a,U(a,"C04")).attack==108,"attack90*120%");U(a,"C04").poisonStacks=1;Check(ForestRules.Stats(a,U(a,"C04")).attack==75,"single integer rounding108*.7");
        U(s,"C03").cell=P(3,2);
        for(int i=0;i<4;i++){U(s,"C02").acted=false;Act(s,C("C02",ActionKind.Heal,"C03"));}
        Check(U(s,"C03").awakeningSources.Count==3,"awakening caps3 even at full HP");
        U(s,"C03").currentHp=40;U(s,"M04-1").cell=P(3,3);Hit(s,"C03");
        Check(U(s,"C03").currentHp==62 && U(s,"C03").awakeningSources.Count==0,"lose20 then heal42 at28 attack");
        Check(s.eventLog.Any(e=>e.kind=="AwakeningHeal"&&e.amount==42),"awakening heal visible event");
        s=Fresh().State;U(s,"C03").cell=P(3,2);Act(s,C("C02",ActionKind.Cleanse,"C03"));Check(U(s,"C03").awakeningSources.Count==1,"cleanse grants awakening");
        U(s,"C03").currentHp=10;U(s,"M04-1").cell=P(3,3);Hit(s,"C03");Check(U(s,"C03").currentHp==14 && ForestRules.Alive(U(s,"C03")),"awakening resolves before death");
        s=Fresh(3).State;U(s,"C04").cell=P(4,3);U(s,"C04").awakeningSources.Add("C02");U(s,"C04").currentHp=50;
        Act(s,C("C04",ActionKind.Wait));Check(U(s,"C04").currentHp==57 && U(s,"C04").awakeningSources.Count==0,"self damage7 heals14");
        s=Fresh().State;var move=C("C03",ActionKind.Wait,null,P(2,4),P(3,4),P(4,4));var preview=ForestRules.Preview(s,move,out _);
        Check(U(preview,"C03").curseSources.Count==2 && U(s,"C03").curseSources.Count==0,"movement preview stacks detached, starting cell excluded");Act(s,move);
        var teleport=C("C01",ActionKind.Echo,"C03");teleport.targetCell=P(1,2);Act(s,teleport);
        Check(U(s,"C03").curseSources.Count==7 && U(s,"C01").curseSources.Count==0,"teleport distance5 adds5 and FuSheng excludes self");
        U(s,"C02").cell=P(5,2);Act(s,C("C02",ActionKind.Wait,null,P(5,2),P(5,3),P(5,4),P(5,5)));
        Check(U(s,"C02").curseSources.Count==3,"other teammate movement grants curses");
        // Reuse a fresh turn to cross the cap, then take an actual attack.
        s.phase=BattlePhase.Player;U(s,"C03").acted=false;U(s,"C03").movementCommitted=false;
        Act(s,C("C03",ActionKind.Wait,null,P(1,2),P(1,3),P(1,4)));Check(U(s,"C03").curseSources.Count==8,"curse cap8");
        U(s,"M04-1").cell=P(2,4);int enemyHp=U(s,"M04-1").currentHp;Hit(s,"C03");
        Check(U(s,"C03").curseSources.Count==0 && s.eventLog.Any(e=>e.kind=="CurseDamage"&&e.amount==24),"8 stacks detonate24 fixed damage");
        Check(U(s,"M04-1").currentHp==enemyHp-24-65,"curse and ordinary counter each once");
        session=Fresh();s=session.State;U(s,"C04").cell=P(5,5);U(s,"C01").cell=P(5,7);U(s,"M04-1").cell=P(6,7);
        U(s,"C01").awakeningSources.Add("C02");U(s,"C01").curseSources.Add("C01");int allyHp=U(s,"C01").currentHp;
        Hit(s,"C01");Check(U(s,"C01").currentHp==allyHp && U(s,"C04").currentHp==118,"guard at2 redirects22 based on original defense");
        Check(U(s,"C01").awakeningSources.Count==1 && U(s,"C01").curseSources.Count==1,"unhurt ally retains stacks");
        s=Fresh(3).State;U(s,"C04").cell=P(5,5);U(s,"C01").cell=P(5,7);U(s,"M04-1").cell=P(6,7);Hit(s,"C01");
        Check(U(s,"C01").currentHp==90 && U(s,"C04").currentHp==181 && U(s,"M04-1").currentHp==471,"attack form protects2 and counter129 at distance3");
        Check(s.eventLog.Count(e=>e.kind=="FormCounterDamage")==1,"counter does not chain");
        s=Fresh().State;U(s,"C04").cell=P(5,4);U(s,"C01").cell=P(5,7);U(s,"M04-1").cell=P(6,7);Hit(s,"C01");Check(U(s,"C01").currentHp==68 && U(s,"C04").currentHp==140,"distance3 not protected");
        s=Fresh().State;U(s,"C04").cell=P(5,5);U(s,"C03").cell=P(6,5);U(s,"M03").maskBoss.warningCenter=P(6,5);U(s,"M03").maskBoss.releaseRound=1;s.phase=BattlePhase.Enemy;
        Act(s,C("M03",ActionKind.MaskBlast));Check(U(s,"C03").currentHp==85 && s.eventLog.Any(e=>e.kind=="Protect"&&e.targetId=="C03"),"active AOE protection");
        session=Fresh();s=session.State;U(s,"C03").awakeningSources.Add("C02");U(s,"C03").curseSources.Add("C01");var copy=s.Copy();U(copy,"C03").curseSources.Clear();U(copy,"C03").awakeningSources.Clear();
        Check(U(s,"C03").curseSources.Count==1 && U(s,"C03").awakeningSources.Count==1,"stack sources deep copied");
        Check(ForestStatusDisplay.Build(s,U(s,"C03")).Any(e=>e.id=="curse") && ForestStatusDisplay.Build(s,U(s,"C03")).Any(e=>e.id=="awakening"),"status icons and stack counts exposed");
        session.Restart();Check(session.State.unitsById.Values.All(u=>u.awakeningSources.Count==0&&u.curseSources.Count==0),"restart clears stacks");
        return "PASS character synergy ("+n+" assertions): form buffs; dynamic guard radius and AOE; ranged120% counter; awakening cap/trigger/heal/self-cost/lethal timing; movement/teleport curse cap/detonation; previews/copies/UI/restart.";
    }
}

