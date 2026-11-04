"""Compile actual project code against installed Unity references and run M1 data checks."""
from pathlib import Path
import os
import subprocess

ROOT = Path(__file__).resolve().parents[1]
os.chdir(ROOT)
UNITY = Path(r"G:\Unity\Editors\Unity2022.3\Editor\Data")
CSC = UNITY / "DotNetSdkRoslyn/csc.dll"
MONO = UNITY / "MonoBleedingEdge/bin/mono.exe"


def options(source):
    return [line for line in Path(source).read_text(encoding="utf-8-sig").splitlines()
            if not line.startswith(('"Assets/', '-out:', '-target:', '-analyzer:', '/analyzer:',
                                    '/additionalfile:', '-refout:'))]


def compile_to(name, lines, sources, target="library"):
    output = f"Artifacts/{name}.{'exe' if target == 'exe' else 'dll'}"
    lines = lines + [f'-target:{target}', f'-out:"{output}"'] + [f'"{p.as_posix()}"' for p in sources]
    rsp = Path(f"Artifacts/{name}.rsp")
    rsp.write_text("\n".join(dict.fromkeys(lines)), encoding="utf-8")
    subprocess.run(["dotnet", str(CSC), "@" + str(rsp)], check=True)


runtime = options("Artifacts/Assembly-CSharp.rsp")
compile_to("ForestM1.Runtime", runtime, sorted(Path("Assets/Scripts").rglob("*.cs")))
editor = options("Artifacts/Assembly-CSharp-Editor.rsp")
editor = [line for line in editor if "Assembly-CSharp.dll" not in line and "Assembly-CSharp.ref.dll" not in line]
editor.append('-r:"Artifacts/ForestM1.Runtime.dll"')
compile_to("ForestM1.Editor", editor, sorted(Path("Assets/Editor").rglob("*.cs")))
runner = runtime + ['-r:"Artifacts/ForestM1.Runtime.dll"']
compile_to("ForestMilestoneOneStandalone", runner,
           [Path("Assets/Editor/ForestDataChecks.cs"), Path("Artifacts/ForestMilestoneOneStandalone.cs")], "exe")
env = dict(os.environ)
env["MONO_PATH"] = os.pathsep.join([str(ROOT / "Artifacts"), str(UNITY / "Managed"),
                                     str(UNITY / "Managed/UnityEngine"), str(ROOT / "Library/ScriptAssemblies")])
subprocess.run([str(MONO), "Artifacts/ForestMilestoneOneStandalone.exe"], check=True, env=env)
print("PASS: complete runtime and editor compilation; actual C# milestone-1 checks.")
