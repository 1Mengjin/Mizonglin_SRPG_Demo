from pathlib import Path
import subprocess
import verify_forest_m1 as b
b.compile_to('ReinforcementStandalone',b.runtime+['-r:"Artifacts/ForestM1.Runtime.dll"'],[Path('Assets/Editor/ForestReinforcementChecks.cs'),Path('Artifacts/ReinforcementStandalone.cs')],'exe')
subprocess.run([str(b.MONO),'Artifacts/ReinforcementStandalone.exe'],check=True,env=b.env)
