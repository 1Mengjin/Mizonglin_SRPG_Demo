"""Compile an isolated copy of the project and verify the undo button in actual Unity Play."""
from pathlib import Path
import json
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'Artifacts/UndoUnityProject'
PROJECT.mkdir(parents=True, exist_ok=True)
for name in ('Assets', 'ProjectSettings'):
    shutil.copytree(ROOT / name, PROJECT / name, dirs_exist_ok=True)
(PROJECT / 'Packages').mkdir(exist_ok=True)
shutil.copytree(ROOT / 'Library/PackageCache/com.unity.ugui@1.0.0', PROJECT / 'Packages/com.unity.ugui', dirs_exist_ok=True)
for name, version in (('com.unity.test-framework', '1.1.33'), ('com.unity.ext.nunit', '1.0.6')):
    shutil.copytree(ROOT / f'Library/PackageCache/{name}@{version}', PROJECT / 'Packages' / name, dirs_exist_ok=True)
manifest = json.loads((ROOT / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))
dependencies = {k: v for k, v in manifest['dependencies'].items() if k.startswith('com.unity.modules.')}
dependencies['com.unity.ugui'] = 'file:com.unity.ugui'
dependencies['com.unity.test-framework'] = 'file:com.unity.test-framework'
dependencies['com.unity.ext.nunit'] = 'file:com.unity.ext.nunit'
(PROJECT / 'Packages/manifest.json').write_text(json.dumps({'dependencies': dependencies}, indent=2), encoding='utf-8')
shutil.copy2(ROOT / 'Artifacts/UndoIntegration.cs', PROJECT / 'Assets/Editor/UndoIntegration.cs')
(PROJECT / 'Artifacts').mkdir(parents=True, exist_ok=True)
report = PROJECT / 'Artifacts/undo-ui-verification.txt'
if report.exists():
    report.unlink()
log = ROOT / 'Artifacts/undo-ui-unity.log'
result = subprocess.run([r'G:\Unity\Editors\Unity2022.3\Editor\Unity.exe', '-batchmode', '-projectPath', str(PROJECT),
                         '-executeMethod', 'UndoIntegration.Run', '-screen-width', '1600', '-screen-height', '900',
                         '-logFile', str(log)], cwd=ROOT, timeout=600)
if report.exists():
    shutil.copy2(report, ROOT / 'Artifacts' / report.name)
    print(report.read_text(encoding='utf-8-sig'))
capture = PROJECT / 'Artifacts/undo-ui.png'
if capture.exists():
    shutil.copy2(capture, ROOT / 'Artifacts' / capture.name)
if result.returncode != 0 or not report.exists():
    tail = log.read_text(encoding='utf-8-sig', errors='replace')[-11000:] if log.exists() else 'Unity exited before creating a log.'
    print(tail)
    raise SystemExit(result.returncode or 1)
