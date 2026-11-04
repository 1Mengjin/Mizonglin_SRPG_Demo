"""Compile all actual runtime/editor code, regress M1, then verify controlled M2 scenarios."""
from pathlib import Path
import subprocess
import verify_forest_m1 as build

build.compile_to('ForestMilestoneTwoStandalone', build.runtime + ['-r:"Artifacts/ForestM1.Runtime.dll"'],
                 [Path('Assets/Editor/ForestCombatChecks.cs'), Path('Artifacts/ForestMilestoneTwoStandalone.cs')], 'exe')
subprocess.run([str(build.MONO), 'Artifacts/ForestMilestoneTwoStandalone.exe'], check=True, env=build.env)
