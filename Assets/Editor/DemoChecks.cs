using System;
using System.IO;
using UnityEngine;
using Mizonglin.Demo;
public static class DemoChecks
{
    public static void VerifyModel()
    {
        var s=new BattleState();
        Action<bool,string> check=(ok,message)=>{if(!ok) throw new Exception(message);};
        check(s.units.Count==7 && s.blocked.Count==8,"Default content");
        for(int i=0;i<5;i++)
        {
            s.Select(s.units[0]); check(!s.reachable.ContainsKey(s.units[1].cell),"Unit blocks movement");
            check(!s.Preview(new Vector2Int(9,9)),"Out of range rejected");
            check(s.Preview(new Vector2Int(2,2)),"Reachable preview");
            check(s.units[0].cell==new Vector2Int(2,1),"Preview is transactional");
            s.Cancel(); check(s.phase==SelectionPhase.Selected,"Cancel returns to range");
            s.Cancel(); check(s.reachable.Count==0 && s.parents.Count==0 && s.selected==null,"No residual state");
        }
        s.Select(s.units[3]);
        foreach(var p in s.reachable.Keys)
        {
            var path=s.Path(p); check(path.Count-1==s.reachable[p],"Shared BFS path length");
            for(int i=1;i<path.Count;i++)
            { check(!s.blocked.Contains(path[i]) && s.At(path[i])==null,"Path occupancy"); check(Mathf.Abs(path[i].x-path[i-1].x)+Mathf.Abs(path[i].y-path[i-1].y)==1,"Four directional path"); }
        }
        s.Select(s.units[0]); s.Preview(new Vector2Int(2,2)); s.CommitWait();
        check(s.units[0].acted && s.units[0].cell==new Vector2Int(2,2),"Commit");
        s.Select(s.units[0]); check(s.phase==SelectionPhase.Inspect && !s.Preview(new Vector2Int(2,3)),"Acted lock");
        s.NextTurn(); check(!s.units[0].acted && s.turn==2,"Next turn");
        s.Reset(); check(s.units[0].cell==new Vector2Int(2,1) && s.turn==1,"Reset");
        Directory.CreateDirectory("Artifacts"); File.WriteAllText("Artifacts/model-verification.txt","PASS: default content, occupancy, unreachable rejection, five preview/cancel cycles, BFS path continuity and distance, wait commit, acted lock, turn reset, full reset.");
        Debug.Log("STAGE_ONE_MODEL_PASS");
    }

    public static void VerifyMap()
    {
        Action<bool,string> check=(ok,message)=>{if(!ok) throw new Exception(message);};
        var map=LevelMap.Default(); check(map.Validate()==null,"Default map valid");
        var invalid=map.Clone(); invalid.spawns[0].x=-1; check(invalid.Validate().Contains("(-1,1)"),"Coordinates in bounds error");
        invalid=map.Clone(); invalid.spawns[0].x=1; invalid.spawns[0].y=2; check(invalid.Validate().Contains("重叠"),"Duplicate spawn");
        invalid=map.Clone(); invalid.spawns[0].x=0; invalid.spawns[0].y=5; check(invalid.Validate().Contains("障碍"),"Blocked spawn");
        invalid=map.Clone(); invalid.goalX=0; invalid.goalY=5; check(invalid.Validate().Contains("目标格"),"Blocked goal");
        invalid=map.Clone(); invalid.spawns.RemoveAll(s=>s.templateId.StartsWith("U")); check(invalid.Validate()!=null,"Missing faction");
        var state=new BattleState(); state.Reset(map); state.Select(state.units[3]);
        check(state.reachable.ContainsKey(new Vector2Int(3,6)),"Default choke open");
        check(map.Paint(new Vector2Int(3,5),Ground.Blocked,out var error),"Close choke"); state.Reset(map); state.Select(state.units[3]);
        check(!state.reachable.ContainsKey(new Vector2Int(3,6)),"Choke blocks reachability");
        // Extend movement only inside this verification to check the actual detour path.
        state.units[3].move=20; state.Select(state.units[3]); var detour=state.Path(new Vector2Int(3,6));
        check(detour.Count>0 && detour.Exists(p=>p==new Vector2Int(6,5)),"Detour uses remaining opening");
        foreach(var p in detour) check(!state.blocked.Contains(p),"Detour never crosses obstacle");
        check(!map.Paint(map.spawns[0].Cell,Ground.Blocked,out error),"Painting blocks marker collision");
        check(!map.Place(map.spawns[0].Cell,"U01",null,out error),"Placement collision");
        string folder=Path.GetFullPath("Artifacts/StageTwoChecks"); Directory.CreateDirectory(folder);
        var store=new MapStore(Path.Combine(folder,"map.json")); check(store.Save(map,out error),"Save map");
        check(new MapStore(store.Path).Load(out var loaded,out error) && loaded.At(new Vector2Int(3,5))==Ground.Blocked,"Disk roundtrip");
        string original=File.ReadAllText(store.Path); check(!store.Save(invalid,out error) && File.ReadAllText(store.Path)==original,"Invalid save preserves disk");
        var clone=map.Clone(); clone.tiles[0]=2; check(map.tiles[0]!=2,"Clone isolates map");
        File.WriteAllText("Artifacts/stage-two-model-verification.txt","PASS: default map, coordinate errors, spawn overlap/obstacles, blocked goal, missing faction, choke closure, real detour, placement protection, disk roundtrip, failed-save preservation, clone isolation.");
        Debug.Log("STAGE_TWO_MODEL_PASS");
    }

    public static void CheckRules()
    {
        var map=LevelMap.Default(); var book=FighterBook.Defaults(); var s=new BattleState(); s.Reset(map); book.Apply(s,map);
        Action<bool,string> check=(ok,reason)=>{if(!ok) throw new Exception(reason);};
        var a=s.units[0]; var b=s.units[5];
        check(CombatRules.Choose(s,s.units[4]).steps.Count==0,"Stationary AI moved");
        check(CombatRules.Choose(s,b).steps.Count>0,"Chase AI failed to pursue");
        check(CombatRules.Choose(s,s.units[6]).steps.Count==0,"Guard escaped anchor");
        a.cell=new Vector2Int(8,6);
        var guard=CombatRules.Choose(s,s.units[6]); check(guard.target==a && guard.steps.TrueForAll(p=>CombatRules.Distance(p,s.units[6].anchor)<=2),"Guard did not intercept");
        a.cell=b.cell+Vector2Int.right; check(CombatRules.Counter(a,b),"Adjacent melee counter");
        b.stats.range=2; check(!CombatRules.Counter(a,b),"Ranged counter");
        b.stats.range=1; CombatRules.Hurt(s,b,999); check(!CombatRules.Counter(a,b) && !s.units.Contains(b),"Dead counter");
        check(CombatRules.Hazard(s.units[1],10)==8 && CombatRules.Hazard(a,0)==0,"Hazard rounding");
        a.stats.attack=0; check(CombatRules.Damage(a,s.units[1])==1,"Minimum damage");
    }

}
