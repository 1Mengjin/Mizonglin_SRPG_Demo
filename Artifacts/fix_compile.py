from pathlib import Path
p=Path('Artifacts/Assembly-CSharp-Editor.rsp');s=p.read_text(encoding='utf-8').replace('Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll','Artifacts/Assembly-CSharp.dll');p.write_text(s,encoding='utf-8')
p=Path('Assets/Editor/DemoSetup.cs');s=p.read_text(encoding='utf-8-sig').replace('if (demo != null && (demo.transform.childCount == 0 || demo.scenario == null)) RefreshPreview();','''if (demo != null)
        {
            bool current=false;
            var map=GetScenario().map;
            foreach(var child in demo.GetComponentsInChildren<Transform>(true))
                if(child.name==$"{map.width} x {map.height} board") current=true;
            if(!current || demo.scenario==null) RefreshPreview();
        }''');p.write_text(s,encoding='utf-8')
import uuid
for name in ['Assets/Scripts/Map/LevelMap.Forest.cs','Assets/Editor/ForestMapChecks.cs']:
    p=Path(name+'.meta')
    if not p.exists():p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
