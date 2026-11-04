"""Compile the project, then verify and render actual Unity Play UI in an isolated copy."""
from pathlib import Path
import json
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'Artifacts/ForestUIUnityProject'
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
shutil.copy2(ROOT / 'Artifacts/AnnouncementIntegration.cs', PROJECT / 'Assets/Editor/AnnouncementIntegration.cs')
shutil.copy2(ROOT / 'Artifacts/AnnouncementPlayChecks.cs', PROJECT / 'Assets/Scripts/Verification/AnnouncementPlayChecks.cs')
shutil.copy2(ROOT / 'Artifacts/AnnouncementFrameChecks.cs', PROJECT / 'Assets/Editor/AnnouncementFrameChecks.cs')
shutil.copy2(ROOT / 'Artifacts/TutorialIntegration.cs', PROJECT / 'Assets/Editor/TutorialIntegration.cs')
shutil.copy2(ROOT / 'Artifacts/TutorialPlayChecks.cs', PROJECT / 'Assets/Scripts/Verification/TutorialPlayChecks.cs')
shutil.copy2(ROOT / 'Artifacts/DescriptionIntegration.cs', PROJECT / 'Assets/Editor/DescriptionIntegration.cs')
shutil.copy2(ROOT / 'Artifacts/DescriptionPlayChecks.cs', PROJECT / 'Assets/Scripts/Verification/DescriptionPlayChecks.cs')
shutil.copy2(ROOT / 'Artifacts/TextClarityIntegration.cs', PROJECT / 'Assets/Editor/TextClarityIntegration.cs')
shutil.copy2(ROOT / 'Artifacts/TextClarityPlayChecks.cs', PROJECT / 'Assets/Scripts/Verification/TextClarityPlayChecks.cs')
frames_only = '--frames-only' in sys.argv
tutorials = '--tutorials' in sys.argv
descriptions = '--descriptions' in sys.argv
text_clarity = '--text-clarity' in sys.argv
log = ROOT / ('Artifacts/text-clarity-unity.log' if text_clarity else 'Artifacts/description-ui-unity.log' if descriptions else 'Artifacts/tutorial-ui-unity.log' if tutorials else 'Artifacts/announcement-frames-unity.log' if frames_only else 'Artifacts/battle-ui-unity.log')
report = PROJECT / ('Artifacts/text-clarity-verification.txt' if text_clarity else 'Artifacts/description-ui-verification.txt' if descriptions else 'Artifacts/tutorial-ui-verification.txt' if tutorials else 'Artifacts/announcement-frames-verification.txt' if frames_only else 'Artifacts/battle-ui-verification.txt')
if report.exists():
    report.unlink()
result = subprocess.run([r'G:\Unity\Editors\Unity2022.3\Editor\Unity.exe', '-batchmode', *(['-quit'] if frames_only else []), '-projectPath', str(PROJECT),
                         '-executeMethod', 'TextClarityIntegration.Run' if text_clarity else 'DescriptionIntegration.Run' if descriptions else 'TutorialIntegration.Run' if tutorials else 'AnnouncementFrameChecks.Run' if frames_only else 'AnnouncementIntegration.Run', '-screen-width', '1600', '-screen-height', '900',
                         '-logFile', str(log)], cwd=ROOT, timeout=300)
if report.exists():
    shutil.copy2(report, ROOT / 'Artifacts' / report.name)
    print(report.read_text(encoding='utf-8-sig'))
for capture in (PROJECT / 'Artifacts').glob('ui-*.png'):
    shutil.copy2(capture, ROOT / 'Artifacts' / capture.name)
if result.returncode != 0 or not report.exists():
    log_text = log.read_text(encoding='utf-8-sig', errors='replace') if log.exists() else 'Unity exited before creating a log.'
    if not report.exists():
        reason = 'BLOCKED: No valid Unity Editor license found; actual Play assertions and visual captures did not run.' if 'No valid Unity Editor license found' in log_text else 'BLOCKED: Unity exited before completing the Play checks; see battle-ui-unity.log.'
        (ROOT / 'Artifacts' / report.name).write_text(reason + '\n', encoding='utf-8')
    print(log_text[-11000:])
    raise SystemExit(result.returncode or 1)
