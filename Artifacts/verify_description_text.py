from pathlib import Path
import os
import subprocess

ROOT = Path(__file__).resolve().parents[1]
os.chdir(ROOT)
unity = Path(r'G:\Unity\Editors\Unity2022.3\Editor\Data')
lines = [s for s in Path('Artifacts/ForestM1.Runtime.rsp').read_text(encoding='utf-8-sig').splitlines() if not s.startswith(('"Assets/', '-out:', '-target:'))]
lines += ['-target:exe', '-out:"Artifacts/DescriptionTextChecks.exe"', '-r:"Artifacts/ForestM1.Runtime.dll"', '"Artifacts/DescriptionTextChecks.cs"']
Path('Artifacts/DescriptionTextChecks.rsp').write_text('\n'.join(lines), encoding='utf-8')
subprocess.run(['dotnet', str(unity / 'DotNetSdkRoslyn/csc.dll'), '@Artifacts/DescriptionTextChecks.rsp'], check=True)
env = dict(os.environ)
env['MONO_PATH'] = os.pathsep.join([str(ROOT / 'Artifacts'), str(unity / 'Managed'), str(unity / 'Managed/UnityEngine'), str(ROOT / 'Library/ScriptAssemblies')])
subprocess.run([str(unity / 'MonoBleedingEdge/bin/mono.exe'), 'Artifacts/DescriptionTextChecks.exe'], check=True, env=env)
