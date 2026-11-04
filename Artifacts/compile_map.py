from pathlib import Path
for name in ['Assembly-CSharp','Assembly-CSharp-Editor']:
    s=Path(f'Library/Bee/artifacts/1900b0aE.dag/{name}.rsp').read_text(encoding='utf-8-sig')
    lines=[]
    for line in s.splitlines():
        if line.startswith(('-analyzer:','/analyzer:','/additionalfile:','-refout:')):continue
        if line.startswith('-out:'): line=f'-out:"Artifacts/{name}.dll"'
        if name.endswith('Editor') and line.startswith('-r:') and 'Assembly-CSharp.dll' in line:line='-r:"Artifacts/Assembly-CSharp.dll"'
        lines.append(line)
    lines.append('"Assets/Editor/ForestMapChecks.cs"' if name.endswith('Editor') else '"Assets/Scripts/Map/LevelMap.Forest.cs"')
    Path(f'Artifacts/{name}.rsp').write_text('\n'.join(lines),encoding='utf-8')
