"""Compile and run actual status display model without Unity native Play."""
from pathlib import Path
import os
import subprocess

ROOT = Path(__file__).resolve().parents[1]
os.chdir(ROOT)
unity = Path(r'G:\Unity\Editors\Unity2022.3\Editor\Data')
lines = [s for s in Path('Artifacts/ForestM1.Runtime.rsp').read_text(encoding='utf-8-sig').splitlines()
         if not s.startswith(('"Assets/', '-out:', '-target:'))]
runtime_lines = lines + ['-target:library', '-out:"Artifacts/ForestM1.Runtime.dll"'] + ['"' + p.as_posix() + '"' for p in sorted(Path('Assets/Scripts').rglob('*.cs'))]
Path('Artifacts/StatusBarRuntime.rsp').write_text('\n'.join(runtime_lines), encoding='utf-8')
subprocess.run(['dotnet', str(unity / 'DotNetSdkRoslyn/csc.dll'), '@Artifacts/StatusBarRuntime.rsp'], check=True)
lines += ['-target:exe', '-out:"Artifacts/StatusBarChecks.exe"', '-r:"Artifacts/ForestM1.Runtime.dll"', '"Artifacts/StatusBarChecks.cs"']
Path('Artifacts/StatusBarChecks.rsp').write_text('\n'.join(lines), encoding='utf-8')
subprocess.run(['dotnet', str(unity / 'DotNetSdkRoslyn/csc.dll'), '@Artifacts/StatusBarChecks.rsp'], check=True)
env = dict(os.environ)
env['MONO_PATH'] = os.pathsep.join([str(ROOT / 'Artifacts'), str(unity / 'Managed'), str(unity / 'Managed/UnityEngine'), str(ROOT / 'Library/ScriptAssemblies')])
subprocess.run([str(unity / 'MonoBleedingEdge/bin/mono.exe'), 'Artifacts/StatusBarChecks.exe'], check=True, env=env)
