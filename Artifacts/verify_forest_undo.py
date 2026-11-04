"""Compile all project code and run the undo feature logic checks without Unity Play."""
from pathlib import Path
import subprocess
import verify_forest_m1 as build

build.compile_to('ForestUndoStandalone', build.runtime + ['-r:"Artifacts/ForestM1.Runtime.dll"'],
                 [Path('Assets/Editor/ForestUndoChecks.cs'), Path('Artifacts/ForestUndoStandalone.cs')], 'exe')
subprocess.run([str(build.MONO), 'Artifacts/ForestUndoStandalone.exe'], check=True, env=build.env)
