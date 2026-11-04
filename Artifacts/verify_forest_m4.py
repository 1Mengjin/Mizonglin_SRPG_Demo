"""Compile actual project code, regress earlier rules, then run focused M4 logic checks."""
from pathlib import Path
import subprocess
import verify_forest_m3
import verify_forest_m1 as build

build.compile_to('ForestMilestoneFourStandalone', build.runtime + ['-r:"Artifacts/ForestM1.Runtime.dll"'],
                 [Path('Assets/Editor/ForestEnemyChecks.cs'), Path('Artifacts/ForestMilestoneFourStandalone.cs')], 'exe')
subprocess.run([str(build.MONO), 'Artifacts/ForestMilestoneFourStandalone.exe'], check=True, env=build.env)
