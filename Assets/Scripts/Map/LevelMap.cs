using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Zilong.Demo
{
    public enum Ground { Normal, Blocked, Hazard }
    [Serializable] public sealed class SpawnPoint
    {
        public string instanceId, templateId, label;
        public int x, y;
        public Vector2Int Cell => new Vector2Int(x,y);
    }
    [Serializable] public sealed partial class LevelMap
    {
        public int version = 1, width = 10, height = 10;
        public int[] tiles = new int[100];
        public List<SpawnPoint> spawns = new List<SpawnPoint>();
        public int hazardPercent = 10;
        public bool hasGoal = true;
        public int goalX = 8, goalY = 9;
        public Vector2Int Goal => new Vector2Int(goalX,goalY);
        public bool Inside(Vector2Int p) => p.x>=0 && p.y>=0 && p.x<width && p.y<height;
        public Ground At(Vector2Int p) => (Ground)tiles[p.y*width+p.x];
        public SpawnPoint SpawnAt(Vector2Int p) => spawns.Find(s=>s.Cell==p);
        public LevelMap Clone() => JsonUtility.FromJson<LevelMap>(JsonUtility.ToJson(this));
        public static readonly string[] Templates = { "U01", "U02", "U03", "U04", "E01", "E02", "E03" };
        public static LevelMap Default()
        {
            var map=new LevelMap();
            foreach(int x in new[]{0,1,2,4,5,7,8,9}) map.tiles[50+x]=(int)Ground.Blocked;
            foreach(var p in new[]{new Vector2Int(6,4),new Vector2Int(6,5),new Vector2Int(6,6)}) map.tiles[p.y*map.width+p.x]=(int)Ground.Hazard;
            int[] xs={2,1,3,2,7,5,8}, ys={1,2,1,3,6,7,8};
            for(int i=0;i<7;i++) map.spawns.Add(new SpawnPoint {instanceId=i<4?"0"+(i+1):"E"+(i-3),templateId=Templates[i],x=xs[i],y=ys[i]});
            return map;
        }
        public string Validate()
        {
            if(hazardPercent<0 || hazardPercent>100) return "危险格伤害应在 0 到 100 之间";
            if((version!=1 && version!=2) || width<1 || height<1 || width>64 || height>64 || tiles==null || tiles.Length!=width*height || spawns==null) return "配置格式错误：尺寸应为 1 至 64，地块数量须匹配尺寸。";
            if(version==2) { var layerError=ValidateLayers(); if(layerError!=null) return layerError; }
            for(int i=0;i<tiles.Length;i++) if(tiles[i]<0 || tiles[i]>2) return $"格 ({i%width},{i/width}) 的地块类型无效。";
            var occupied=new HashSet<Vector2Int>(); var ids=new HashSet<string>(); int allies=0,enemies=0;
            foreach(var s in spawns)
            {
                if(s==null) return "配置包含空出生点。";
                string pos=$"({s.x},{s.y})";
                if(!Inside(s.Cell)) return "出生点 "+pos+" 越界。";
                if(At(s.Cell)==Ground.Blocked) return "出生点 "+pos+" 位于障碍上。";
                if(!occupied.Add(s.Cell)) return "出生点 "+pos+" 重叠。";
                if(string.IsNullOrWhiteSpace(s.instanceId) || !ids.Add(s.instanceId)) return "出生点 "+pos+" 编号为空或重复。";
                if(Array.IndexOf(Templates,s.templateId)<0) return "出生点 "+pos+" 临时模板无效。";
                if(s.templateId.StartsWith("U")) allies++; else enemies++;
            }
            if(!hasGoal && version==1) return "请放置 1 个目标格。";
            if(hasGoal && !Inside(Goal)) return $"目标格 ({goalX},{goalY}) 越界。";
            if(hasGoal && At(Goal)==Ground.Blocked) return $"目标格 ({goalX},{goalY}) 位于障碍上。";
            if(allies==0 || enemies==0) return "需要至少 1 个我方和 1 个敌方出生点。";
            return null;
        }
        public bool Paint(Vector2Int p,Ground ground,out string error)
        {
            error=null;
            if(!Inside(p)) error=$"格 ({p.x},{p.y}) 越界。";
            else if(ground==Ground.Blocked && (SpawnAt(p)!=null || hasGoal && Goal==p)) error=$"格 ({p.x},{p.y}) 有出生点或目标，请先移动或删除标记。";
            if(error!=null) return false;
            tiles[p.y*width+p.x]=(int)ground;
            if(ground==Ground.Blocked && version==2)
            {
                regions[p.y*width+p.x]=0; initialFog[p.y*width+p.x]=false;
                rootCandidates.Remove(p); patrol.Remove(p);
            }
            if(version==2 && ground!=Ground.Blocked && regions[p.y*width+p.x]==0) regions[p.y*width+p.x]=(int)MapRegion.中庭;
            return true;
        }
        public bool Place(Vector2Int p,string templateId,SpawnPoint moving,out string error)
        {
            error=null;
            if(!Inside(p)) error=$"出生点 ({p.x},{p.y}) 越界。";
            else if(At(p)==Ground.Blocked) error=$"格 ({p.x},{p.y}) 是障碍，不能放置出生点。";
            else if(SpawnAt(p)!=null && SpawnAt(p)!=moving) error=$"格 ({p.x},{p.y}) 已有出生点。";
            else if(Array.IndexOf(Templates,templateId)<0) error="临时模板无效。";
            if(error!=null) return false;
            if(moving==null)
            {
                int n=1; string prefix=templateId.StartsWith("U")?"U":"E";
                while(spawns.Exists(s=>s.instanceId==prefix+n.ToString("00"))) n++;
                moving=new SpawnPoint {instanceId=prefix+n.ToString("00"),templateId=templateId}; spawns.Add(moving);
            }
            moving.x=p.x; moving.y=p.y; return true;
        }
    }
    public sealed class MapStore
    {
        public readonly string Path;
        public MapStore(string path) { Path=path; }
        public bool Save(LevelMap map,out string error)
        {
            error=map.Validate(); if(error!=null) return false;
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                string temp=Path+".tmp";
                File.WriteAllText(temp,JsonUtility.ToJson(map,true));
                if(File.Exists(Path)) File.Replace(temp,Path,null); else File.Move(temp,Path);
                return true;
            }
            catch(Exception ex) { error="保存失败，原存档保留："+ex.Message; return false; }
        }
        public bool Load(out LevelMap map,out string error)
        {
            map=null; error=null;
            try
            {
                if(!File.Exists(Path)) { error="还没有已保存配置，请先保存当前地图。"; return false; }
                string json=File.ReadAllText(Path);
                // Missing fields must not silently inherit DTO defaults.
                foreach(string key in new[]{"version","width","height","tiles","spawns","hasGoal","goalX","goalY"})
                    if(!json.Contains("\""+key+"\"")) { error="配置缺少字段："+key; return false; }
                var loaded=JsonUtility.FromJson<LevelMap>(json);
                if(loaded!=null && !json.Contains("\"hazardPercent\"")) loaded.hazardPercent=10;
                error=loaded==null?"配置为空。":loaded.Validate();
                if(error!=null) return false;
                map=loaded; return true;
            }
            catch(Exception ex) { error="读取失败，编辑中的地图未改变："+ex.Message; return false; }
        }
    }
}

