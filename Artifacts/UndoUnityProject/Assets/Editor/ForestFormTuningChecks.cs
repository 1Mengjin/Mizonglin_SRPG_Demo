using System;
using System.Linq;
using UnityEngine;
using Zilong.Forest;
public static class ForestFormTuningChecks
{
    static int n;
    static Vector2Int P(int x,int y)=>new Vector2Int(x,y);
    static UnitState U(ForestBattleState s,string id)=>s.unitsById[id];
    static void Check(bool ok,string message){n++;if(!ok)throw new Exception(message);}
    static ForestBattleState Fresh(int round=1,int enemyAttack=30)
    {
        var d=ForestDefaults.Create();for(int i=0;i<d.map.terrain.Length;i++){d.map.terrain[i]=0;d.map.regions[i]=3;d.map.initialFog[i]=false;}
        d.unitTemplates[7].maxHp=1000;d.unitTemplates[7].attack=enemyAttack;
        d.spawns[0].cell=P(2,2);d.spawns[1].cell=P(4,2);d.spawns[2].cell=P(2,4);d.spawns[3].cell=P(5,5);d.spawns[8].cell=P(6,5);
        var s=new ForestSession(d).State;s.round=round;ForestRules.BeginPlayerPhase(s);return s;
    }
    static ForestCommand Retreat()=>new ForestCommand{actorId="C04",kind=ActionKind.Retreat,targetId="M04-1"};
    static void Act(ForestBattleState s,ForestCommand c){Check(ForestRules.TryExecute(s,c,out var e),e??c.kind.ToString());}
    public static string Run()
    {
        n=0;var s=Fresh();var a=U(s,"C04");var stats=ForestRules.Stats(s,a);
        Check(stats.maxHp==140&&stats.attack==45&&stats.defense==54&&stats.move==2,"guard HP/attack unchanged, defense54 move2");
        Check(ForestRules.Damage(s,a,U(s,"M04-1"))==22,"guard basic retains50% without extra20% attack penalty");
        a.poisonStacks=2;Check(ForestRules.Stats(s,a).move==1,"guard poison combines with movement-1");
        s=Fresh(3);a=U(s,"C04");stats=ForestRules.Stats(s,a);
        Check(stats.maxHp==210&&a.currentHp==210&&stats.attack==108&&stats.move==3,"attack form HP150% attack120% original multiplier");
        a.poisonStacks=1;Check(ForestRules.Stats(s,a).attack==75,"poison applied with one final integer rounding");
        s=Fresh();a=U(s,"C04");a.currentHp=70;a.awakeningSources.Add("C02");a.curseSources.Add("C01");int start=s.eventLog.Count;
        s.round=3;s.phase=BattlePhase.PlayerStart;ForestRules.BeginPlayerPhase(s);
        Check(a.currentHp==105&&ForestRules.Stats(s,a).maxHp==210,"half HP becomes105/210 on entering attack");
        Check(a.awakeningSources.Count==1&&a.curseSources.Count==1&&!s.eventLog.Skip(start).Any(e=>e.kind=="AwakeningHeal"||e.kind=="CurseDetonated"),"form ratio change never triggers buffs");
        s.round=4;s.phase=BattlePhase.PlayerStart;ForestRules.BeginPlayerPhase(s);Check(a.currentHp==105,"same form never reapplies HP multiplier");
        s.round=5;s.phase=BattlePhase.PlayerStart;ForestRules.BeginPlayerPhase(s);Check(a.currentHp==70&&ForestRules.Stats(s,a).maxHp==140,"returning guard preserves half HP");
        s=Fresh();a=U(s,"C04");a.currentHp=1;s.round=3;s.phase=BattlePhase.PlayerStart;ForestRules.BeginPlayerPhase(s);s.round=5;s.phase=BattlePhase.PlayerStart;ForestRules.BeginPlayerPhase(s);Check(a.currentHp==1&&ForestRules.Alive(a),"proportional conversion does not kill at1 HP");
        s=Fresh(3);a=U(s,"C04");s.phase=BattlePhase.Enemy;Act(s,new ForestCommand{actorId="M04-1",kind=ActionKind.BasicAttack,targetId="C04"});
        Check(a.currentHp==171&&s.eventLog.Any(e=>e.kind=="Attack"&&e.amount==39),"attack form takes130% incoming30=39");
        Check(U(s,"M04-1").currentHp==871,"existing120% form counter uses new attack108 ->129");
        s=Fresh(3);a=U(s,"C04");U(s,"C01").cell=P(5,7);U(s,"M04-1").cell=P(6,7);s.phase=BattlePhase.Enemy;
        Act(s,new ForestCommand{actorId="M04-1",kind=ActionKind.BasicAttack,targetId="C01"});
        Check(U(s,"C01").currentHp==90&&a.currentHp==181,"guarded22 damage amplified once to29 on attack-form protector");
        s=Fresh(3);a=U(s,"C04");Check(ForestRules.RetreatCost(s,a)==63,"retreat costs30% maximum210, not current HP");
        a.currentHp=100;Act(s,Retreat());Check(a.currentHp==30&&U(s,"M04-1").currentHp==925,"pay63, deal75, retain configured end-action self damage7");
        Check(s.eventLog.Single(e=>e.kind=="SkillCost").amount==63&&s.eventLog.Single(e=>e.kind=="RetreatDamage").amount==75,"cost and damage remain unaffected by own vulnerability");
        s=Fresh(3);a=U(s,"C04");a.currentHp=63;a.awakeningSources.Add("C02");a.curseSources.Add("C01");int revision=(int)s.revision;
        Check(!ForestRules.TryExecute(s,Retreat(),out _)&&a.currentHp==63&&!a.acted&&s.revision==revision,"cannot cast at exact cost, no side effects");
        a.currentHp=64;Act(s,Retreat());Check(a.currentHp==8,"pay at cost+1; awakening restores14 then end-action self damage7");
        s=Fresh();a=U(s,"C04");a.scarStacks=3;a.currentHp=110;s.round=3;s.phase=BattlePhase.PlayerStart;ForestRules.BeginPlayerPhase(s);
        Check(ForestRules.Stats(s,a).maxHp==165&&a.currentHp==165&&ForestRules.RetreatCost(s,a)==50,"scar-adjusted maxHP then150%, cost rounded up");
        s=Fresh(1,100);a=U(s,"C04");Act(s,new ForestCommand{actorId="C04",kind=ActionKind.Retreat});Check(a.reflectPercent==90,"guard retreat arms90% reflection");
        s.phase=BattlePhase.Enemy;Act(s,new ForestCommand{actorId="M04-1",kind=ActionKind.BasicAttack,targetId="C04"});
        Check(a.currentHp==94&&U(s,"M04-1").currentHp==959&&a.reflectPercent==0,"actual46 damage reflects41 once at90%");
        s=Fresh(3);a=U(s,"C04");a.awakeningSources.Add("C02");a.awakeningSources.Add("C02");a.curseSources.Add("C01");a.curseSources.Add("C01");s.phase=BattlePhase.Enemy;
        Act(s,new ForestCommand{actorId="M04-1",kind=ActionKind.BasicAttack,targetId="C04"});
        Check(s.eventLog.Count(e=>e.kind=="AwakeningHeal")==1&&s.eventLog.Count(e=>e.kind=="CurseDetonated")==1,"stack settlement emits one event per buff regardless stack count");
        Check(s.eventLog.Single(e=>e.kind=="CurseDetonated").cell==a.cell,"curse flash event located on buff holder, not damage source");
        return "PASS form tuning ("+n+" assertions): current form stats and retained half basic; guard movement; proportional HP both ways with no buff triggers; vulnerability; current-max30% cost/120% damage/insufficient HP;90% reflection; one buff settlement event at holder position.";
    }
}
