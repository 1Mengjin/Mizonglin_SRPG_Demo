from pathlib import Path
import subprocess
import verify_forest_m1 as b
b.compile_to('SynergyStandalone',b.runtime+['-r:"Artifacts/ForestM1.Runtime.dll"'],[Path('Assets/Editor/ForestSynergyChecks.cs'),Path('Artifacts/SynergyStandalone.cs')],'exe')
subprocess.run([str(b.MONO),'Artifacts/SynergyStandalone.exe'],check=True,env=b.env)
