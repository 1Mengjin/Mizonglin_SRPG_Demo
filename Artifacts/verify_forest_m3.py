"""Compile all project code and run M1, M2 and M3 controlled checks."""
from pathlib import Path
import subprocess
import verify_forest_m2
import verify_forest_m1 as build

build.compile_to('ForestMilestoneThreeStandalone', build.runtime + ['-r:"Artifacts/ForestM1.Runtime.dll"'],
                 [Path('Assets/Editor/ForestResourceChecks.cs'), Path('Artifacts/ForestMilestoneThreeStandalone.cs')], 'exe')
subprocess.run([str(build.MONO), 'Artifacts/ForestMilestoneThreeStandalone.exe'], check=True, env=build.env)
