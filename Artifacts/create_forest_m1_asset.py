"""One-time generation of the new formal asset from the actual C# default export; never overwrites it."""
from pathlib import Path
import json
import re
import struct
import uuid
import yaml

ROOT = Path(__file__).resolve().parents[1]
definition = json.loads((ROOT / 'Artifacts/forest-m1-default-definition.json').read_text(encoding='utf-8-sig'))


def asset_value(value):
    if isinstance(value, bool):
        return int(value)
    if isinstance(value, dict):
        return {key: ({'fileID': 0} if key == 'portrait' else asset_value(item))
                for key, item in value.items() if item is not None or key == 'portrait'}
    if isinstance(value, list):
        return [asset_value(item) for item in value]
    return value


data = asset_value(definition)
for key in ('terrain', 'regions'):
    data['map'][key] = struct.pack('<' + 'i' * len(data['map'][key]), *data['map'][key]).hex()
data['map']['initialFog'] = bytes(data['map']['initialFog']).hex()
script_guid = re.search(r'^guid: (\w+)$', (ROOT / 'Assets/Scripts/Forest/ForestScenario.cs.meta').read_text(), re.M)[1]
body = {
    'm_ObjectHideFlags': 0, 'm_CorrespondingSourceObject': {'fileID': 0},
    'm_PrefabInstance': {'fileID': 0}, 'm_PrefabAsset': {'fileID': 0},
    'm_GameObject': {'fileID': 0}, 'm_Enabled': 1, 'm_EditorHideFlags': 0,
    'm_Script': {'fileID': 11500000, 'guid': script_guid, 'type': 3},
    'm_Name': 'ForestScenario', 'm_EditorClassIdentifier': '', 'definition': data,
}
path = ROOT / 'Assets/Data/ForestScenario.asset'
with path.open('x', encoding='utf-8', newline='\n') as target:
    target.write('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n')
    target.write(yaml.safe_dump({'MonoBehaviour': body}, allow_unicode=True, sort_keys=False, width=4096))
meta = Path(str(path) + '.meta')
with meta.open('x', encoding='utf-8') as target:
    target.write('fileFormatVersion: 2\nguid: ' + uuid.uuid4().hex
                 + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
                 + '  userData: \n  assetBundleName: \n  assetBundleVariant: \n')
print('Created', path)
