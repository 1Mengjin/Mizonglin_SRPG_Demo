from pathlib import Path
import subprocess
import verify_forest_m1 as build
build.compile_to('BalanceStandalone',build.runtime+['-r:"Artifacts/ForestM1.Runtime.dll"'],[Path('Assets/Editor/ForestBalanceChecks.cs'),Path('Artifacts/BalanceStandalone.cs')],'exe')
subprocess.run([str(build.MONO),'Artifacts/BalanceStandalone.exe'],check=True,env=build.env)
