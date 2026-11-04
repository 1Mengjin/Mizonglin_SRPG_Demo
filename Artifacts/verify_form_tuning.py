from pathlib import Path
import subprocess
import verify_forest_m1 as b
b.compile_to('FormTuningStandalone',b.runtime+['-r:"Artifacts/ForestM1.Runtime.dll"'],[Path('Assets/Editor/ForestFormTuningChecks.cs'),Path('Artifacts/FormTuningStandalone.cs')],'exe')
subprocess.run([str(b.MONO),'Artifacts/FormTuningStandalone.exe'],check=True,env=b.env)
