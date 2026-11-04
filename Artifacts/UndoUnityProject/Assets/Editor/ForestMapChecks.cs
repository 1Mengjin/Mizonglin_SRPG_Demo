using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zilong.Demo;

public static class ForestMapChecks
{
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    static int Distance(LevelMap map,Vector2Int start,Vector2Int end,HashSet<Vector2Int> excluded=null)
    {
        var distances=new Dictionary<Vector2Int,int>{{start,0}}; var queue=new Queue<Vector2Int>(); queue.Enqueue(start);
        while(queue.Count>0)
        {
            var p=queue.Dequeue(); if(p==end) return distances[p];
            foreach(var d in new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left})
            {
                var n=p+d;
                if(!map.Inside(n) || map.At(n)==Ground.Blocked || distances.ContainsKey(n) || excluded!=null && excluded.Contains(n)) continue;
                distances[n]=distances[p]+1; queue.Enqueue(n);
            }
        }
        return -1;
    }
    public static void Run()
    {
        DemoChecks.VerifyModel(); DemoChecks.VerifyMap(); DemoChecks.CheckRules();
        var map=DemoSetup.GetScenario().map;
        Check(map.Validate()==null,map.Validate());
        Check(map.width==18 && map.height==14,"Dimensions");
        var start=new Vector2Int(2,11); var boss=new Vector2Int(14,3);
        int open=0,fogCount=0; var fog=new HashSet<Vector2Int>();
        for(int y=0;y<map.height;y++) for(int x=0;x<map.width;x++)
        {
            var p=new Vector2Int(x,y);
            if(map.At(p)!=Ground.Blocked) { open++; Check(Distance(map,start,p)>=0,"Disconnected cell "+p); }
            if(map.FogAt(p)) { fogCount++; fog.Add(p); }
        }
        Check(open==149 && fogCount==15,"Cell counts");
        Check(map.spawns.Count==11 && map.rootCandidates.Count==2 && map.patrol.Count==4,"Markers");
        var direct=new HashSet<Vector2Int>(map.rootCandidates); direct.Add(new Vector2Int(15,6)); direct.Add(new Vector2Int(16,6));
        Check(Distance(map,start,boss,direct)==20,"Central route");
        Check(Distance(map,start,boss,fog)==22,"Fog-free route");
        var east=new HashSet<Vector2Int>(fog); east.UnionWith(map.rootCandidates);
        Check(Distance(map,start,new Vector2Int(14,10),east)+Distance(map,new Vector2Int(14,10),boss,east)==22,"East route");
        Check(!map.Inside(new Vector2Int(18,1)) && map.Inside(new Vector2Int(16,12)),"Bounds");
        var clone=map.Clone(); clone.initialFog[7*18+7]=false;
        Check(map.FogAt(new Vector2Int(7,7)),"Fog clone isolation");
        var state=new BattleState(); state.Reset(map); state.units.Clear();
        var walker=new DemoUnit("test","test",false,2,11,100,100); state.units.Add(walker); state.Select(walker);
        Check(state.reachable.Count==149 && state.reachable[boss]==20,"Runtime pathfinding uses full board");
        Check(state.fog.Count==15 && state.hazards.Count==0,"Fog separated from damaging terrain");
        var store=new MapStore(Path.GetFullPath("Artifacts/forest-roundtrip.json"));
        Check(store.Save(map,out var error),error); Check(store.Load(out var loaded,out error),error);
        Check(loaded.patrol.Count==4 && loaded.FogAt(new Vector2Int(11,9)) && loaded.RegionAt(new Vector2Int(16,6))==MapRegion.东连接口,"Layer roundtrip");
        Check(LevelMap.Forest().Validate()==null,"Factory valid");
        for(int y=0;y<14;y++) for(int x=0;x<18;x++)
        {
            var p=new Vector2Int(x,y); var factory=LevelMap.Forest();
            Check(factory.At(p)==map.At(p) && factory.RegionAt(p)==map.RegionAt(p) && factory.FogAt(p)==map.FogAt(p),"Factory/data mismatch");
        }
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(DemoSetup.ScenePath);
        DemoSetup.RefreshPreview(); DemoSetup.VerifySavedPreview();
        File.WriteAllText("Artifacts/forest-verification.txt","PASS: 18x14; 149 connected walkable cells; 15 independent initial fog cells; 11 spawns; 2 roots; 4 adjacent patrol points; routes 20/22/22; full-board runtime BFS; isolated state; serialized layers roundtrip; factory matches asset; saved scene preview. Combat mechanics and balance are not verified.");
        Debug.Log("FOREST_MAP_PASS");
    }
}
