from pathlib import Path
s=Path('Artifacts/Assembly-CSharp.rsp').read_text(encoding='utf-8')
lines=[l for l in s.splitlines() if not l.startswith(('"Assets/','-out:','-target:'))]
lines+=['-target:exe','-out:"Artifacts/ForestStandalone.exe"','-r:"Artifacts/Assembly-CSharp.dll"','"Artifacts/ForestStandalone.cs"']
Path('Artifacts/ForestStandalone.rsp').write_text('\n'.join(lines),encoding='utf-8')
