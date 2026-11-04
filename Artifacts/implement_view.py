from pathlib import Path
p=Path('Assets/Scripts/Presentation/BattleDemo.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('Vector3 World(Vector2Int p, float h = 0) => new Vector3(p.x,h,p.y);','''Vector3 World(Vector2Int p, float h = 0) => new Vector3(p.x,h,State.layout!=null && State.layout.topLeftCoordinates ? State.Height-1-p.y : p.y);
        Vector2Int CellFromWorld(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x),State.layout!=null && State.layout.topLeftCoordinates ? State.Height-1-Mathf.RoundToInt(p.z) : Mathf.RoundToInt(p.z));''')
a=s.index('            view.tag = "MainCamera";');b=s.index('                var p = new Vector2Int(x,y);',a)
s=s[:a]+'''            view.tag = "MainCamera"; view.orthographic = true;
            var center=new Vector3((State.Width-1)*.5f,0,(State.Height-1)*.5f);
            view.orthographicSize = Mathf.Max(State.Height*.62f+1,State.Width/(Mathf.Max(.8f,view.aspect)*1.15f)+1);
            view.transform.position = center + new Vector3(0,26,-12);
            view.transform.LookAt(center);
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.12f,.16f,.18f);
            view.nearClipPlane = .1f; view.farClipPlane = 80;
            boardRoot = new GameObject($"{State.Width} x {State.Height} board").transform;
            Shape("Board plinth",PrimitiveType.Cube,boardRoot,center+Vector3.down*.24f,new Vector3(State.Width+.3f,.38f,State.Height+.3f),new Color(.21f,.25f,.26f));
            for (int y=0;y<State.Height;y++) for(int x=0;x<State.Width;x++)
            {
'''+s[b:]
s=s.replace('new Color(.39f,.43f,.45f));','new Color(.25f,.32f,.17f));').replace('                obstacles[p].AddComponent<BoxCollider>();','')
s=s.replace('            goalRing=Ring', '''            if(State.layout!=null && State.layout.version==2)
            {
                foreach(var p in State.layout.rootCandidates)
                    Shape("根须候选 " + p,PrimitiveType.Cube,boardRoot,World(p,.06f),new Vector3(.8f,.025f,.09f),gold);
                for(int i=0;i<State.layout.patrol.Count;i++)
                {
                    var p=State.layout.patrol[i];
                    Shape("巡逻点 " + (i+1) + " " + p,PrimitiveType.Cube,boardRoot,World(p,.07f),new Vector3(.13f,.03f,.7f),new Color(.9f,.45f,.15f));
                }
            }
            goalRing=Ring''')
s=s.replace('if (State.blocked.Contains(p)) return new Color(.27f,.30f,.31f);','if (State.blocked.Contains(p)) return LevelMap.RegionColor(MapRegion.密林);')
s=s.replace('            return (p.x+p.y)%2', '''            if(State.fog.Contains(p)) return new Color(.60f,.40f,.73f);
            if(State.layout!=null && State.layout.version==2) return LevelMap.RegionColor(State.layout.RegionAt(p))*((p.x+p.y)%2==0?1f:.95f);
            return (p.x+p.y)%2''')
s=s.replace('State.hazards.Contains(pair.Key));','State.hazards.Contains(pair.Key) || State.fog.Contains(pair.Key));')
s=s.replace('new Vector2Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z))','CellFromWorld(p)')
s=s.replace('"穿过窄道 · 抵达金色目标格"','State.layout!=null && State.layout.version==2 ? "迷踪林 · 地块结构试玩" : "穿过窄道 · 抵达金色目标格"')
s=s.replace('if (!buildingPreview) Notify("我方先行 · 十二回合内抵达目标");','if (!buildingPreview) Notify(State.hasGoal?"我方先行 · 十二回合内抵达目标":"地块结构试玩 · 瘴气及根须为配置预览，战斗机制待接入");')
p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Combat/BattleDemo.Combat.cs');s=p.read_text(encoding='utf-8-sig').replace('if(State.turn==4)','if(State.turn==4 && State.layout.version==1)').replace('Notify("我方先行 · 十二回合内抵达目标");','Notify(State.hasGoal?"我方先行 · 十二回合内抵达目标":"迷踪林地块结构试玩");');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Map/BattleDemo.MapEditor.cs');s=p.read_text(encoding='utf-8-sig').replace('"固定 10×10 · 逐格修改"','$"{State.Width}×{State.Height} · 逐格修改"');p.write_text(s,encoding='utf-8')
p=Path('Assets/Editor/LevelDesigner.cs');s=p.read_text(encoding='utf-8-sig')
s=s.replace('目标格, 删除目标 }','目标格, 删除目标, 区域归属, 初始瘴气, 清除瘴气, 根须候选, 巡逻点 }').replace('int templateIndex;','int templateIndex;\n    MapRegion regionBrush = MapRegion.中庭;')
s=s.replace('            DrawGrid();','''            if(data.map.version==2)
            {
                regionBrush=(MapRegion)EditorGUILayout.EnumPopup("区域画笔",regionBrush);
                EditorGUILayout.HelpBox("左上角 (0,0)，向右为 X、向下为 Y。紫色为可清除初始瘴气；金色短线为根须候选；橙色数字为巡逻顺序。点选对应画笔可切换标记。巡逻按点击顺序构成相邻闭环。",MessageType.Info);
            }
            DrawGrid();''')
s=s.replace('data.map = LevelMap.Default(); data.fighters = FighterBook.Defaults();','data.map = LevelMap.Forest();')
s=s.replace('地图和七个模板将恢复默认，可使用撤销恢复。','地图将恢复迷踪林方案，单位模板保留，可使用撤销恢复。')
s=s.replace('for (int y = 9; y >= 0; y--)','for (int row = 0; row < data.map.height; row++)').replace('            EditorGUILayout.BeginHorizontal();\n            for (int x = 0; x < 10; x++)','            int y=data.map.topLeftCoordinates?row:data.map.height-1-row;\n            EditorGUILayout.BeginHorizontal();\n            for (int x = 0; x < data.map.width; x++)')
s=s.replace('                string label = x + "," + y;','''                if(data.map.version==2) color=ground==Ground.Blocked?LevelMap.RegionColor(MapRegion.密林):data.map.FogAt(cell)?new Color(.7f,.5f,.82f):LevelMap.RegionColor(data.map.RegionAt(cell));
                string label = x + "," + y;
                if(data.map.version==2)
                {
                    if(data.map.rootCandidates.Contains(cell)) label+=" 根";
                    int patrolIndex=data.map.patrol.IndexOf(cell); if(patrolIndex>=0) label+=" 巡"+(patrolIndex+1);
                }''')
s=s.replace('label += "\\n" + spawn.templateId;', 'label += "\\n" + (string.IsNullOrEmpty(spawn.label)?spawn.templateId:spawn.label);')
s=s.replace('if (brush == Brush.查看) { message = "当前格 " + cell; return; }','''if (brush == Brush.查看) { var spawn=data.map.SpawnAt(cell); message = "当前格 " + cell+" · "+data.map.RegionAt(cell)+(data.map.FogAt(cell)?" · 初始瘴气":"")+(spawn!=null?" · "+spawn.label:""); return; }''')
s=s.replace('        switch (brush)','''        if(data.map.version==2 && brush>=Brush.区域归属)
        {
            if(data.map.At(cell)==Ground.Blocked) { message="请先将密林改为普通地块，再设置区域与状态。"; return; }
            int i=cell.y*data.map.width+cell.x;
            if(brush==Brush.区域归属 && regionBrush!=MapRegion.密林) data.map.regions[i]=(int)regionBrush;
            if(brush==Brush.初始瘴气) data.map.initialFog[i]=true;
            if(brush==Brush.清除瘴气) data.map.initialFog[i]=false;
            if(brush==Brush.根须候选) { if(!data.map.rootCandidates.Remove(cell)) data.map.rootCandidates.Add(cell); }
            if(brush==Brush.巡逻点) { if(!data.map.patrol.Remove(cell)) data.map.patrol.Add(cell); }
            Changed(); return;
        }
        switch (brush)''')
s=s.replace('        if (changed) Changed();','''        if(changed && data.map.version==2 && data.map.At(cell)!=Ground.Blocked && data.map.RegionAt(cell)==MapRegion.密林)
            data.map.regions[cell.y*data.map.width+cell.x]=(int)(regionBrush==MapRegion.密林?MapRegion.中庭:regionBrush);
        if (changed) Changed();''')
s=s.replace('var cell = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z)));','var cell = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z)));') # transformed below
s=s.replace('Mathf.RoundToInt(point.z));','data.map.topLeftCoordinates?data.map.height-1-Mathf.RoundToInt(point.z):Mathf.RoundToInt(point.z));')
s=s.replace('new Vector3(cell.x,.05f,cell.y)', 'new Vector3(cell.x,.05f,data.map.topLeftCoordinates?data.map.height-1-cell.y:cell.y)')
s=s.replace('GUILayout.Height(43), GUILayout.MinWidth(41)','GUILayout.Height(40), GUILayout.MinWidth(48)')
s=s.replace('"地图格坐标从 0 开始。"','"地图格坐标从 0 开始。"')
p.write_text(s,encoding='utf-8')
