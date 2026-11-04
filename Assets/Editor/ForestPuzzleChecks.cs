using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zilong.Forest;

// Design invariants of the puzzle level. The numbers are authored in Assets/Data/ForestScenario.asset;
// ForestDefaults stays a separate preset, so Artifacts/verify_puzzle.py dumps the authored numbers to
// Artifacts/puzzle-config.txt and this check applies them before arranging its own arena.
public static class ForestPuzzleChecks
{
    static int checks;
    static Vector2Int P(int x,int y)=>new Vector2Int(x,y);
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static UnitState U(ForestBattleState s,string id)=>s.unitsById[id];
    static ForestCommand Cmd(string actor,ActionKind kind,string target=null)=>new ForestCommand{actorId=actor,kind=kind,targetId=target};
    static List<Vector2Int> Path(Vector2Int from,params Vector2Int[] steps){var path=new List<Vector2Int>{from};path.AddRange(steps);return path;}
    static void Act(ForestBattleState s,ForestCommand c){Check(ForestRules.TryExecute(s,c,out var error),error??c.kind.ToString());}
    static void Fog(ForestBattleState s,Vector2Int cell)=>s.groundFog.Add(cell);

    static ScenarioDefinition Authored()
    {
        var definition=ForestDefaults.Create();const string snapshot="Artifacts/puzzle-config.txt";
        if(!File.Exists(snapshot))throw new FileNotFoundException("run Artifacts/verify_puzzle.py first to dump the authored configuration",snapshot);
        foreach(var line in File.ReadAllLines(snapshot))
        {
            if(line.Length==0)continue;var parts=line.Split('=');Apply(definition,parts[0],parts[1]);
        }
        definition.ValidateOrThrow();return definition;
    }
    static void Apply(ScenarioDefinition definition,string path,string raw)
    {
        var keys=path.Split('.');object target;int index;
        if(keys[0]=="rules"){target=definition.rules;index=1;}
        else {target=Array.Find(definition.unitTemplates,t=>t.templateId==keys[1]);index=2;
            if(target==null)throw new Exception("配置快照含未知模板 "+keys[1]);}
        for(int i=index;i<keys.Length-1;i++)
        {
            target=target.GetType().GetField(keys[i]).GetValue(target);
            if(target==null)return; // the preset keeps unused blocks null; authored zeros add nothing
        }
        var field=target.GetType().GetField(keys[keys.Length-1]);if(field==null)return;
        object value=field.FieldType==typeof(bool)?(object)(raw=="1")
            :field.FieldType==typeof(int)?(object)int.Parse(raw)
            :field.FieldType.IsEnum?(object)Enum.ToObject(field.FieldType,int.Parse(raw)):(object)raw;
        field.SetValue(target,value);
    }
    static ForestBattleState Arena()
    {
        var definition=Authored();
        for(int i=0;i<definition.map.terrain.Length;i++){definition.map.terrain[i]=0;definition.map.regions[i]=3;definition.map.initialFog[i]=false;}
        definition.map.rootCells=new Vector2Int[0];
        var s=new ForestSession(definition).State;
        foreach(var unit in s.unitsById.Values){unit.lifeState=LifeState.Dead;unit.currentHp=0;}
        ForestRules.BeginPlayerPhase(s);return s;
    }
    static UnitState Spawn(ForestBattleState s,string id,Vector2Int cell)
    {
        var unit=s.unitsById[id];unit.lifeState=LifeState.Alive;unit.cell=cell;
        unit.currentHp=s.GetTemplate(unit.templateId).maxHp;unit.acted=false;unit.movementCommitted=false;return unit;
    }

    static void CorpseCurse()
    {
        // 1. One 75 attack cannot kill the 85 hp corpse.
        var s=Arena();Spawn(s,"C03",P(2,2));Spawn(s,"C01",P(12,12));Spawn(s,"C04",P(16,13));Spawn(s,"M04-1",P(3,2));
        Act(s,Cmd("C03",ActionKind.BasicAttack,"M04-1"));
        Check(s.GetTemplate("M04").maxHp==85,"corpse template is authored at 85 hp");
        Check(s.eventLog.Single(e=>e.kind=="Attack").amount==75,"one grounded attack deals exactly 75");
        Check(U(s,"M04-1").currentHp==10 && U(s,"M04-1").lifeState==LifeState.Alive,"one attack leaves the corpse at 10");
        Check(!s.eventLog.Any(e=>e.kind=="CurseDamage"),"no curse detonates without movement");
        // 2. Two curse layers add 6: 81 total, still alive.
        s=Arena();Spawn(s,"C03",P(3,2));Spawn(s,"C01",P(12,12));Spawn(s,"C04",P(16,13));Spawn(s,"M04-1",P(6,2));
        var attack=Cmd("C03",ActionKind.BasicAttack,"M04-1");
        attack.path=Path(P(3,2),P(4,2),P(5,2));
        Act(s,attack);
        Check(s.eventLog.Where(e=>e.kind=="CurseAdded").Sum(e=>e.amount)==2,"two movement steps add two curse layers");
        Check(s.eventLog.Single(e=>e.kind=="CurseDetonated").amount==2,"the counter detonates both layers");
        Check(s.eventLog.Single(e=>e.kind=="CurseDamage").amount==6,"two layers deal exactly 6");
        Check(U(s,"M04-1").currentHp==4 && U(s,"M04-1").lifeState==LifeState.Alive,"75+6=81 cannot kill an 85 hp corpse");
        // 3. Three curse layers add 10: exactly 85, the corpse dies.
        s=Arena();Spawn(s,"C03",P(2,2));Spawn(s,"C01",P(12,12));Spawn(s,"C04",P(16,13));Spawn(s,"M04-1",P(6,2));
        attack=Cmd("C03",ActionKind.BasicAttack,"M04-1");attack.path=Path(P(2,2),P(3,2),P(4,2),P(5,2));
        Act(s,attack);
        Check(s.eventLog.Where(e=>e.kind=="CurseAdded").Sum(e=>e.amount)==3,"three movement steps add three curse layers");
        Check(s.eventLog.Single(e=>e.kind=="CurseDetonated").amount==3,"the counter detonates all three layers");
        Check(s.eventLog.Single(e=>e.kind=="CurseDamage").amount==10,"three layers deal exactly 10");
        Check(U(s,"C03").currentHp==65,"the corpse counter hits for 30");
        Check(U(s,"M04-1").currentHp==0 && U(s,"M04-1").lifeState==LifeState.Dead,"75+10=85 kills the corpse");
        Check(s.eventLog.Any(e=>e.kind=="Counter"&&e.amount==30),"the corpse really counters and triggers the curse");
    }

    static void DevourerTalisman()
    {
        // 4. 85*150/100-8=119 against 120 hp: the talisman alone cannot kill.
        var s=Arena();Spawn(s,"C01",P(2,2));Spawn(s,"C02",P(13,13));var devourer=Spawn(s,"M02-1",P(3,2));
        devourer.devourer.carriedFogCount=3;Fog(s,P(17,13));
        var talisman=Cmd("C01",ActionKind.Talisman);talisman.targetCell=P(3,2);
        Act(s,talisman);
        Check(s.GetTemplate("M02").maxHp==120,"devourer template is authored at 120 hp");
        Check(s.eventLog.Single(e=>e.kind=="TalismanDamage").amount==119,"talisman deals 85*150/100-8=119");
        Check(devourer.currentHp==1 && devourer.lifeState==LifeState.Alive,"119 cannot kill a 120 hp devourer");
        Check(ForestRules.IsStunned(s,devourer),"talisman stuns the devourer");
        // 5. One basic attack first, then the talisman: always lethal, silenced, no fog spilled.
        s=Arena();Spawn(s,"C01",P(2,2));Spawn(s,"C02",P(13,13));devourer=Spawn(s,"M02-1",P(3,2));
        devourer.devourer.carriedFogCount=3;Fog(s,P(17,13));
        Act(s,Cmd("C01",ActionKind.BasicAttack,"M02-1"));
        Check(devourer.currentHp==43,"one basic attack deals 85-8=77");
        U(s,"C01").acted=false;Act(s,talisman);
        Check(devourer.currentHp==0 && devourer.lifeState==LifeState.Dead,"basic attack plus talisman is always lethal");
        Check(s.eventLog.Any(e=>e.kind=="DeathrattleSilenced"&&e.amount==3),"the killing blow is silenced so the deathrattle never fires");
        Check(!s.eventLog.Any(e=>e.kind=="FogSpilled"),"a silenced death spills no fog");
        Check(devourer.devourer.carriedFogCount==3,"the dead record still carries its three fog");
    }

    static void PurificationScars()
    {
        var s=Arena();var walker=Spawn(s,"C03",P(2,2));
        for(int x=2;x<=10;x++)Fog(s,P(x,2));
        s.spiritsById.Add("spirit:1",new SpiritState{spiritId="spirit:1",sourceInstanceId="M03",sourcePhase=1,locationKind=SpiritLocation.Ground,cell=P(2,2)});
        Act(s,new ForestCommand{actorId="C03",kind=ActionKind.SwallowSpirit,spiritId="spirit:1"});
        Check(s.Rules.purificationCellsPerSpirit==8 && s.Rules.purificationCellsPerScar==4,"one spirit clears 8 cells and one scar costs 4");
        Check(walker.purificationCharges.Count==1 && walker.purificationCharges[0].clearedCount==0,"the swallowed spirit is one fresh charge");
        Act(s,new ForestCommand{actorId="C03",kind=ActionKind.Wait,path=Path(P(2,2),P(3,2),P(4,2),P(5,2),P(6,2),P(7,2))});
        Check(walker.purificationCharges.Count==1 && walker.purificationCharges[0].clearedCount==6,"six cleared cells keep the charge alive");
        walker.acted=false;walker.movementCommitted=false;
        Act(s,new ForestCommand{actorId="C03",kind=ActionKind.Wait,path=Path(P(7,2),P(8,2),P(9,2),P(10,2))});
        Check(walker.purificationCharges.Count==0,"the charge is consumed at exactly 8 clears");
        Check(s.eventLog.Count(e=>e.kind=="Purify")==8 && s.eventLog.Count(e=>e.kind=="FogCleared")==8,"one spirit clears exactly eight cells");
        Check(walker.scarStacks==2 && s.eventLog.Count(e=>e.kind=="Scar")==2,"one spirit leaves exactly two scar layers");
        Check(s.eventLog.Where(e=>e.kind=="Scar").Select(e=>e.amount).SequenceEqual(new[]{1,2}),"scars are gained on the 4th and 8th cell");
        Check(ForestRules.Stats(s,walker).maxHp==75 && walker.currentHp==75,"two scars cost 20 maximum and current health");
        Check(s.groundFog.Count==1 && s.groundFog.Contains(P(10,2)),"the cell after the last charge stays fogged");
    }

    static void VictoryGate()
    {
        // 6. A living fog carrier blocks victory even on an empty field.
        var s=Arena();Spawn(s,"C01",P(2,2));Spawn(s,"C02",P(13,13));var devourer=Spawn(s,"M02-1",P(3,2));
        devourer.devourer.carriedFogCount=3;
        Act(s,Cmd("C02",ActionKind.Wait));
        Check(s.result==BattleResult.InProgress,"a living carrier of three fog blocks victory on an empty field");
        // 7. A normal kill spills that fog, so the field is dirty again; clearing it wins.
        devourer.currentHp=1;Act(s,Cmd("C01",ActionKind.BasicAttack,"M02-1"));
        Check(devourer.lifeState==LifeState.Dead && s.eventLog.Count(e=>e.kind=="FogSpilled")==3,"a normal kill spills all three fog");
        Check(s.groundFog.Count==3 && s.result==BattleResult.InProgress,"spilled fog blocks victory");
        s.groundFog.Clear();U(s,"C02").acted=false;Act(s,Cmd("C02",ActionKind.Wait));
        Check(s.result==BattleResult.Victory,"no living carrier and no ground fog wins");
        // 8. A silenced kill keeps the fog on the dead record and wins immediately.
        s=Arena();Spawn(s,"C01",P(2,2));Spawn(s,"C02",P(13,13));devourer=Spawn(s,"M02-1",P(3,2));
        devourer.devourer.carriedFogCount=3;devourer.currentHp=1;devourer.stunnedUntilRound=1;
        Act(s,Cmd("C01",ActionKind.BasicAttack,"M02-1"));
        Check(devourer.lifeState==LifeState.Dead && devourer.devourer.carriedFogCount==3,"the silenced corpse keeps its three fog");
        Check(!s.eventLog.Any(e=>e.kind=="FogSpilled") && s.groundFog.Count==0,"no fog reaches the ground");
        Check(s.result==BattleResult.Victory && s.endReason==BattleEndReason.Cleared,"a dead carrier no longer blocks victory");
    }

    static void MaskBlastFriendlyFire()
    {
        var s=Arena();var boss=Spawn(s,"M03",P(6,6));Spawn(s,"C01",P(7,6));Spawn(s,"C04",P(16,13));var corpse=Spawn(s,"M04-1",P(5,6));
        Fog(s,P(17,13));
        s.phase=BattlePhase.Enemy;boss.acted=false;boss.maskBoss.warningCenter=P(6,6);boss.maskBoss.releaseRound=1;
        Act(s,Cmd("M03",ActionKind.MaskBlast));
        Check(corpse.currentHp==85 && corpse.lifeState==LifeState.Alive,"mask blast never damages enemy units inside the warning");
        Check(U(s,"C01").currentHp==30,"mask blast still deals its fixed 60 to allies inside the warning");
        Check(!ForestRules.MaskWarningCells(s,boss).Any(),"release clears the warning");
    }

    public static string Run()
    {
        checks=0;
        CorpseCurse();
        DevourerTalisman();
        PurificationScars();
        VictoryGate();
        MaskBlastFriendlyFire();
        return "PASS puzzle invariants ("+checks+" assertions): 85 hp corpse survives 75 and 75+6, dies at exactly 75+10 (three curse layers); 120 hp devourer survives a 119 talisman and dies silenced with fog retained; one 8-cell spirit leaves exactly two scars; a living fog carrier blocks victory while a dead one does not; mask blast spares enemies. Numbers are read from the authored asset snapshot.";
    }
}
