using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zilong.Demo
{
    // Region ownership, terrain and initial dynamic state are authored independently.
    public enum MapRegion { 密林, 出生林, 头道门, 中庭, 树精林A, 北道, 千面祭坛, 东侧林道, 树精林B, 西径, 东连接口 }
    public sealed partial class LevelMap
    {
        public int[] regions;
        public bool[] initialFog;
        public List<Vector2Int> rootCandidates = new List<Vector2Int>();
        public List<Vector2Int> patrol = new List<Vector2Int>();
        public bool topLeftCoordinates;
        public bool FogAt(Vector2Int p) => Inside(p) && initialFog != null && initialFog.Length==width*height && initialFog[p.y*width+p.x];
        public MapRegion RegionAt(Vector2Int p) => regions!=null && regions.Length==width*height ? (MapRegion)regions[p.y*width+p.x] : MapRegion.中庭;
        public static Color RegionColor(MapRegion region)
        {
            switch(region)
            {
                case MapRegion.密林: return new Color(.25f,.32f,.17f);
                case MapRegion.出生林: return new Color(.65f,.60f,.77f);
                case MapRegion.头道门: return new Color(.64f,.67f,.70f);
                case MapRegion.千面祭坛: return new Color(.84f,.73f,.48f);
                case MapRegion.西径: case MapRegion.东侧林道: case MapRegion.东连接口: return new Color(.60f,.72f,.79f);
                case MapRegion.树精林A: case MapRegion.树精林B: case MapRegion.北道: return new Color(.60f,.74f,.53f);
                default: return new Color(.79f,.78f,.69f);
            }
        }
        string ValidateLayers()
        {
            if(regions==null || initialFog==null || regions.Length!=width*height || initialFog.Length!=width*height) return "区域层或初始瘴气层尺寸不匹配。";
            for(int i=0;i<tiles.Length;i++)
            {
                if(regions[i]<0 || regions[i]>10) return "区域编号无效。";
                if(tiles[i]==(int)Ground.Blocked && (initialFog[i] || regions[i]!=0)) return "密林格不能有瘴气或可走区域归属。";
                if(tiles[i]!=(int)Ground.Blocked && regions[i]==0) return "可走地块需要选择区域归属。";
            }
            if(rootCandidates==null || patrol==null) return "根须或巡逻列表为空。";
            foreach(var p in rootCandidates) if(!Inside(p) || At(p)==Ground.Blocked) return "根须候选点必须在可走格上。";
            var unique=new HashSet<Vector2Int>();
            for(int i=0;i<patrol.Count;i++)
            {
                var p=patrol[i];
                if(!Inside(p) || At(p)==Ground.Blocked || !unique.Add(p)) return "巡逻点越界、重叠或位于障碍上。";
                if(patrol.Count>1 && CombatRules.Distance(p,patrol[(i+1)%patrol.Count])!=1) return "巡逻必须由相邻格构成闭环。";
            }
            return null;
        }
        public static LevelMap Forest()
        {
            var map=new LevelMap {version=2,width=18,height=14,tiles=new int[252],regions=new int[252],initialFog=new bool[252],topLeftCoordinates=true,hasGoal=false};
            for(int i=0;i<map.tiles.Length;i++) map.tiles[i]=(int)Ground.Blocked;
            map.Room(MapRegion.出生林,1,10,4,12); map.Room(MapRegion.头道门,5,9,6,11);
            map.Room(MapRegion.中庭,7,5,11,10); map.Room(MapRegion.树精林A,1,1,8,4);
            map.Room(MapRegion.北道,9,1,11,2); map.Room(MapRegion.千面祭坛,12,1,16,5);
            map.Room(MapRegion.东侧林道,13,7,16,8); map.Room(MapRegion.树精林B,12,9,16,12);
            map.Room(MapRegion.西径,1,5,2,9); map.Room(MapRegion.东连接口,15,6,16,6);
            foreach(var p in new[]{new Vector2Int(7,4),new Vector2Int(8,4)}) { map.tiles[p.y*18+p.x]=1; map.regions[p.y*18+p.x]=0; }
            for(int y=7;y<=9;y++) for(int x=7;x<=11;x++) map.initialFog[y*18+x]=true;
            int[] xs={2,3,2,4,8,14,14,16,10,10,13}, ys={11,11,12,11,2,10,3,7,1,6,12};
            string[] labels={"前锋","游侠","铁卫","斥候","树精A","树精B","千面","吞噬怪","腐卒1","腐卒2","腐卒3"};
            for(int i=0;i<xs.Length;i++) map.spawns.Add(new SpawnPoint {instanceId=i<4?"0"+(i+1):"F"+(i-3),templateId=i<4?Templates[i]:i<=6?"E01":"E03",label=labels[i],x=xs[i],y=ys[i]});
            map.rootCandidates.AddRange(new[]{new Vector2Int(9,1),new Vector2Int(9,2)});
            map.patrol.AddRange(new[]{new Vector2Int(15,6),new Vector2Int(16,6),new Vector2Int(16,7),new Vector2Int(15,7)});
            return map;
        }
        void Room(MapRegion region,int x0,int y0,int x1,int y1)
        {
            for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++) { tiles[y*width+x]=0; regions[y*width+x]=(int)region; }
        }
    }
}
