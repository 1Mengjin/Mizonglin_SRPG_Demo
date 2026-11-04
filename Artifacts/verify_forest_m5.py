"""Verify feedback, accounting and run-report infrastructure without Unity Play."""
from pathlib import Path
import subprocess
import verify_forest_skills
import verify_forest_m1 as build

build.compile_to('ForestMilestoneFiveStandalone', build.runtime + ['-r:"Artifacts/ForestM1.Runtime.dll"'],
                 [Path('Assets/Editor/ForestFeedbackChecks.cs'), Path('Artifacts/ForestMilestoneFiveStandalone.cs')], 'exe')
subprocess.run([str(build.MONO), 'Artifacts/ForestMilestoneFiveStandalone.exe'], check=True, env=build.env)
