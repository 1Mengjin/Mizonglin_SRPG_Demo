from pathlib import Path
import json,hashlib,shutil
workspace=Path(__file__).resolve().parents[1]
staged=workspace/'Artifacts/document-updates'
source=Path('G:/.工作夹/笔试紫龙').resolve()
backup=workspace/'Artifacts/document-originals';backup.mkdir(exist_ok=True)
manifest=json.loads((staged/'manifest.json').read_text(encoding='utf-8'))
# Validate every original before writing any file.
for item in manifest:
 target=(source/item['name']).resolve(); assert target.parent==source and target.suffix=='.md'
 assert hashlib.sha256(target.read_bytes()).hexdigest()==item['original_sha256'], 'Original changed: '+str(target)
 assert hashlib.sha256((staged/item['name']).read_bytes()).hexdigest()==item['staged_sha256']
for item in manifest:
 target=source/item['name'];shutil.copy2(target,backup/item['name']);shutil.copy2(staged/item['name'],target)
 assert hashlib.sha256(target.read_bytes()).hexdigest()==item['staged_sha256']
 print('Updated: '+str(target))
