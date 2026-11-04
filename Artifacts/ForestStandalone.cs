using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Mizonglin.Demo;
class ForestStandalone
{
    static void Check(bool value,string reason) { if(!value) throw new Exception(reason); }
    static int Bfs(LevelMap m,Vector2Int start,Vector2Int end,HashSet<Vector2Int> blocked)
    {
        var q=new Queue<Vector2Int>(); q.Enqueue(start); var ds=new Dictionary<Vector2Int,int>{{start,0}};
        while(q.Count>0) { var p=q.Dequeue(); if(p==end)return ds[p];
            foreach(var d in new[]{new Vector2Int(0,1),new Vector2Int(0,-1),new Vector2Int(1,0),new Vector2Int(-1,0)}) {
                var n=p+d; if(!m.Inside(n)||m.At(n)==Ground.Blocked||ds.ContainsKey(n)||blocked.Contains(n))continue;
                ds[n]=ds[p]+1;q.Enqueue(n);
            }
        }return -1;
    }
    static void Main()
    {
        var m=LevelMap.Forest(); Check(m.Validate()==null,m.Validate()); var start=new Vector2Int(2,11);var end=new Vector2Int(14,3);
        var fog=new HashSet<Vector2Int>();var none=new HashSet<Vector2Int>(); int count=0;
        for(int y=0;y<m.height;y++)for(int x=0;x<m.width;x++) {
            var p=new Vector2Int(x,y); if(m.FogAt(p))fog.Add(p);
            if(m.At(p)!=Ground.Blocked){count++;Check(Bfs(m,start,p,none)>=0,"Connectivity");}
        }
        Check(count==149&&fog.Count==15,"Counts");
        var blocked=new HashSet<Vector2Int>(m.rootCandidates);blocked.Add(new Vector2Int(15,6));blocked.Add(new Vector2Int(16,6));
        Check(Bfs(m,start,end,blocked)==20,"Central");Check(Bfs(m,start,end,fog)==22,"West");
        var east=new HashSet<Vector2Int>(fog);east.UnionWith(m.rootCandidates);var tree=new Vector2Int(14,10);
        Check(Bfs(m,start,tree,east)+Bfs(m,tree,end,east)==22,"East");
        Check(m.spawns.Count==11&&m.patrol.Count==4&&m.rootCandidates.Count==2,"Markers");
        foreach(var p in new[]{new Vector2Int(7,4),new Vector2Int(8,4),new Vector2Int(12,7),new Vector2Int(12,8)})Check(m.At(p)==Ground.Blocked,"Cutout");
        Check(m.Paint(new Vector2Int(1,1),Ground.Blocked,out var err)&&!m.FogAt(new Vector2Int(1,1))&&m.RegionAt(new Vector2Int(1,1))==MapRegion.密林,"Blocked layer sync");
        Check(m.Paint(new Vector2Int(1,1),Ground.Normal,out err)&&m.Validate()==null,"Reopen terrain");
        string result="PASS: actual C# factory and validation; 18x14; 149 connected cells; 15 initial fog cells; routes 20/22/22; four cutouts; 11 spawns; root/patrol markers; painting layer consistency. Runtime and editor assemblies compiled against installed Unity references. Unity rendering/Play checks blocked by missing license.";
        File.WriteAllText("Artifacts/forest-standalone-verification.txt",result); Console.WriteLine(result);
        m=LevelMap.Forest();using(var w=new StreamWriter("Artifacts/forest-factory-cells.csv"))for(int y=0;y<14;y++)for(int x=0;x<18;x++){var p=new Vector2Int(x,y);w.WriteLine($"{x},{y},{(int)m.At(p)},{(int)m.RegionAt(p)},{(m.FogAt(p)?1:0)}");}
    }
}
