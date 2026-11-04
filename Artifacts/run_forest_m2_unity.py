"""Verify actual Editor GUI and Play workflows in an isolated copy using installed packages."""
from pathlib import Path
import json
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'Artifacts/ForestM2UnityProject'
PROJECT.mkdir(parents=True, exist_ok=True)
shutil.copytree(ROOT / 'Assets', PROJECT / 'Assets', dirs_exist_ok=True)
shutil.copytree(ROOT / 'ProjectSettings', PROJECT / 'ProjectSettings', dirs_exist_ok=True)
(PROJECT / 'Packages').mkdir(exist_ok=True)
shutil.copytree(ROOT / 'Library/PackageCache/com.unity.ugui@1.0.0', PROJECT / 'Packages/com.unity.ugui', dirs_exist_ok=True)
for name, version in (('com.unity.test-framework', '1.1.33'), ('com.unity.ext.nunit', '1.0.6')):
    shutil.copytree(ROOT / f'Library/PackageCache/{name}@{version}', PROJECT / 'Packages' / name, dirs_exist_ok=True)
manifest = json.loads((ROOT / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))
dependencies = {name: version for name, version in manifest['dependencies'].items() if name.startswith('com.unity.modules.')}
dependencies['com.unity.ugui'] = 'file:com.unity.ugui'
dependencies['com.unity.test-framework'] = 'file:com.unity.test-framework'
dependencies['com.unity.ext.nunit'] = 'file:com.unity.ext.nunit'
(PROJECT / 'Packages/manifest.json').write_text(json.dumps({'dependencies': dependencies}, indent=2), encoding='utf-8')
shutil.copy2(ROOT / 'Artifacts/ForestM2Integration.cs', PROJECT / 'Assets/Editor/ForestM2Integration.cs')
log = ROOT / 'Artifacts/forest-m2-unity-batch.log'
report = PROJECT / 'Artifacts/forest-m2-integration-verification.txt'
if report.exists(): report.unlink()
result = subprocess.run([r'G:\Unity\Editors\Unity2022.3\Editor\Unity.exe', '-batchmode', '-projectPath', str(PROJECT),
                         '-executeMethod', 'ForestM2Integration.Run', '-screen-width', '1600', '-screen-height', '900',
                         '-logFile', str(log)], cwd=ROOT, timeout=240)
for name in ('forest-m2-integration-verification.txt', 'forest-m2-unity-verification.txt', 'forest-m2-play.png'):
    source = PROJECT / 'Artifacts' / name
    if source.exists(): shutil.copy2(source, ROOT / 'Artifacts' / name)
if not report.exists() or result.returncode != 0:
    print(report.read_text(encoding='utf-8-sig', errors='replace') if report.exists() else 'No completed integration report')
    print(log.read_text(encoding='utf-8-sig', errors='replace')[-14000:] if log.exists() else 'Unity produced no log')
    raise SystemExit(result.returncode or 1)
print(report.read_text(encoding='utf-8-sig'))
