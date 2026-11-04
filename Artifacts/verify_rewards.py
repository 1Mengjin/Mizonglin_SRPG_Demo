from pathlib import Path
import shutil, subprocess
root=Path.cwd(); project=root/'Artifacts/ForestM2UnityProject'
shutil.copytree(root/'Assets',project/'Assets',dirs_exist_ok=True)
shutil.copy2(root/'Artifacts/RewardIntegration.cs',project/'Assets/Editor/RewardIntegration.cs')
r=subprocess.run([r'G:\Unity\Editors\Unity2022.3\Editor\Unity.exe','-batchmode','-projectPath',str(project),'-executeMethod','RewardIntegration.Run','-screen-width','1600','-screen-height','900','-logFile',str(root/'Artifacts/reward-unity.log')],timeout=200)
for name in ['reward-verification.txt','reward-popup.png']:
 p=project/'Artifacts'/name
 if p.exists():shutil.copy2(p,root/'Artifacts'/name)
print((root/'Artifacts/reward-verification.txt').read_text() if (root/'Artifacts/reward-verification.txt').exists() else (root/'Artifacts/reward-unity.log').read_text(encoding='utf-8',errors='replace')[-6000:])
raise SystemExit(r.returncode)
