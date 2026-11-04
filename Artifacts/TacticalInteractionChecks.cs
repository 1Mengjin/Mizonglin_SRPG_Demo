using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zilong.Demo;
class TacticalInteractionChecks
{
    static void Check(bool ok,string reason) { if(!ok) throw new Exception(reason); }
    static readonly Vector2Int[] D={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
    static void CheckBorder(BattleState s)
    {
        var border=new HashSet<Vector2Int>(); s.CollectMovementBorder(border);
        foreach(var p in border) {
            Check(s.Inside(p) && !s.blocked.Contains(p) && !s.reachable.ContainsKey(p),"Invalid border cell");
            bool adjacent=false; foreach(var d in D) adjacent|=s.reachable.ContainsKey(p+d);
            Check(adjacent,"Border is not adjacent to reachable cells");
        }
        foreach(var p in s.reachable.Keys) foreach(var d in D) {
            var n=p+d;
            if(s.Inside(n) && !s.blocked.Contains(n) && !s.reachable.ContainsKey(n)) Check(border.Contains(n),"Missing border");
        }
        var saved=new HashSet<Vector2Int>(border);
        var destination=s.selected.cell;
        foreach(var p in s.reachable.Keys) if(p!=s.selected.cell) {destination=p;break;}
        Check(s.Preview(destination),"Preview rejected");
        s.CollectMovementBorder(border); Check(border.SetEquals(saved),"Preview changed original movement boundary");
        s.Cancel(); s.CollectMovementBorder(border); Check(border.SetEquals(saved),"Cancel changed movement boundary");
        s.Cancel(); s.CollectMovementBorder(border); Check(border.Count==0,"Cancel left residual red tiles");
    }
    static void Main()
    {
        var s=new BattleState(); s.units.Clear(); s.blocked.Clear();
        var actor=new DemoUnit("test","test",false,5,5,3,100);s.units.Add(actor);s.Select(actor);
        var border=new HashSet<Vector2Int>();s.CollectMovementBorder(border);
        Check(s.reachable.Count==25 && border.Count==16,"Open ground diamond");CheckBorder(s);
        actor.cell=Vector2Int.zero;s.Select(actor);s.CollectMovementBorder(border);
        Check(s.reachable.Count==10 && border.Count==5,"Corner clips boundary");CheckBorder(s);
        actor.move=0;s.Select(actor);s.CollectMovementBorder(border);Check(border.Count==2,"Zero move at corner");CheckBorder(s);
        // Populate managed model directly: JsonUtility.Clone requires a running Unity engine.
        var map=LevelMap.Forest();s.Clear();s.units.Clear();s.blocked.Clear();s.layout=map;
        typeof(BattleState).GetProperty("Width").SetValue(s,map.width);
        typeof(BattleState).GetProperty("Height").SetValue(s,map.height);
        for(int y=0;y<map.height;y++) for(int x=0;x<map.width;x++) {
            var p=new Vector2Int(x,y);if(map.At(p)==Ground.Blocked)s.blocked.Add(p);
        }
        foreach(var spawn in map.spawns) s.units.Add(new DemoUnit(spawn.instanceId,spawn.templateId,spawn.templateId.StartsWith("E"),spawn.x,spawn.y,3,100));
        foreach(var u in s.units) if(!u.enemy) {s.Select(u);CheckBorder(s);}
        s.Select(s.units[0]);s.Preview(s.units[0].cell);Check(s.CommitWait(),"Commit failed");
        s.CollectMovementBorder(border);Check(border.Count==0,"Commit left residual border");
        s.Select(s.units[0]);s.CollectMovementBorder(border);Check(border.Count==0,"Acted inspection has border");
        s.Select(s.units.Find(u=>u.enemy));s.CollectMovementBorder(border);Check(border.Count==0,"Enemy inspection has border");
        var message="PASS: actual C# movement border; open-ground diamond; map-edge clipping; zero movement; all four forest allies; complete adjacency; no overlap with reachable/blocked cells; preview preserves border; cancel, commit and inspection clear border. Camera rendering and mouse input require Unity Play verification.";
        File.WriteAllText("Artifacts/tactical-interaction-verification.txt",message);Console.WriteLine(message);
    }
}
