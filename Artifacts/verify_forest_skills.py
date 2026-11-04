"""Compile actual code and verify the latest skill changes without running Unity Play."""
from pathlib import Path
import subprocess
import verify_forest_m4
import verify_forest_m1 as build

build.compile_to('ForestSkillsStandalone', build.runtime + ['-r:"Artifacts/ForestM1.Runtime.dll"'],
                 [Path('Assets/Editor/ForestSkillChecks.cs'), Path('Artifacts/ForestSkillsStandalone.cs')], 'exe')
subprocess.run([str(build.MONO), 'Artifacts/ForestSkillsStandalone.exe'], check=True, env=build.env)
