from pathlib import Path
p=Path('Assets/Scripts/Map/LevelMap.cs')
s=p.read_text(encoding='utf-8-sig').replace('public string instanceId, templateId;', 'public string instanceId, templateId, label;')
s=s.replace('public sealed class LevelMap','public sealed partial class LevelMap')
s=s.replace('p.x<10 && p.y<10','p.x<width && p.y<height').replace('tiles[p.y*10+p.x]','tiles[p.y*width+p.x]')
s=s.replace('if(version!=1 || width!=10 || height!=10 || tiles==null || tiles.Length!=100 || spawns==null) return "配置格式错误：需要版本 1 的 10×10 地图。";', 'if((version!=1 && version!=2) || width<1 || height<1 || width>64 || height>64 || tiles==null || tiles.Length!=width*height || spawns==null) return "配置格式错误：尺寸应为 1 至 64，地块数量须匹配尺寸。";\n            if(version==2) { var layerError=ValidateLayers(); if(layerError!=null) return layerError; }')
s=s.replace('for(int i=0;i<100;i++)', 'for(int i=0;i<tiles.Length;i++)').replace('{i%10},{i/10}', '{i%width},{i/width}')
s=s.replace('if(!hasGoal) return', 'if(!hasGoal && version==1) return').replace('if(!Inside(Goal))', 'if(hasGoal && !Inside(Goal))').replace('if(At(Goal)==Ground.Blocked)', 'if(hasGoal && At(Goal)==Ground.Blocked)')
s=s.replace('tiles[p.y*width+p.x]=(int)ground; return true;', '''tiles[p.y*width+p.x]=(int)ground;
            if(ground==Ground.Blocked && version==2)
            {
                regions[p.y*width+p.x]=0; initialFog[p.y*width+p.x]=false;
                rootCandidates.Remove(p); patrol.Remove(p);
            }
            return true;''')
p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Combat/BattleState.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('public const int Size = 10;', 'public const int Size = 10;\n        public int Width { get; private set; } = 10;\n        public int Height { get; private set; } = 10;\n        public LevelMap layout;\n        public readonly HashSet<Vector2Int> fog = new HashSet<Vector2Int>();')
s=s.replace('Clear(); turn = 1;', 'Width=Height=10; layout=null; fog.Clear();\n            Clear(); turn = 1;')
s=s.replace('Clear(); turn=1;', 'Width=map.width; Height=map.height; layout=map.Clone(); fog.Clear();\n            Clear(); turn=1;')
s=s.replace('for(int y=0;y<10;y++) for(int x=0;x<10;x++)','for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)')
s=s.replace('if(map.At(p)==Ground.Hazard) hazards.Add(p);','if(map.At(p)==Ground.Hazard) hazards.Add(p); if(map.FogAt(p)) fog.Add(p);')
s=s.replace('p.x < Size && p.y < Size','p.x < Width && p.y < Height')
p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Combat/CombatRules.cs');s=p.read_text(encoding='utf-8-sig').replace('unit.name=t.name;', 'unit.name=string.IsNullOrEmpty(spawn.label)?t.name:spawn.label;');p.write_text(s,encoding='utf-8')
