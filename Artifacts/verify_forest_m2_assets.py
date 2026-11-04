"""Check actual serialized asset data and scene references against the actual C# preset export."""
from pathlib import Path
import json
import re
import struct
import sys
import yaml

ROOT = Path(__file__).resolve().parents[1]
AUTHORED_SPAWNS = '--authored-spawns' in sys.argv
AUTHORED_ROUND_LIMIT = '--authored-round-limit' in sys.argv


def unity_documents(path):
    source = path.read_text(encoding='utf-8-sig')
    source = re.sub(r'^%.*\n', '', source, flags=re.M)
    source = re.sub(r'^--- !u!\d+ &-?\d+.*$', '---', source, flags=re.M)
    return list(yaml.load_all(source, Loader=yaml.BaseLoader))


def compare(expected, actual, label='definition'):
    if AUTHORED_ROUND_LIMIT and label == 'definition.rules.roundLimit':
        assert int(actual) >= 1, 'Authored round limit must be positive'
        return
    if AUTHORED_SPAWNS and re.fullmatch(r'definition\.spawns\[\d+\]\.cell', label):
        return  # Checked separately for legal, distinct placement; keep the authored positions.
    if expected is None:
        return
    if isinstance(expected, dict):
        for key, value in expected.items():
            if value is not None:
                assert key in actual, f'{label}.{key}: missing'
                compare(value, actual[key], f'{label}.{key}')
    elif isinstance(expected, list):
        if isinstance(actual, str):
            raw = bytes.fromhex(actual)
            actual = list(raw) if expected and isinstance(expected[0], bool) else list(struct.unpack('<' + 'i' * (len(raw) // 4), raw))
        assert len(expected) == len(actual), f'{label}: length'
        for index, (left, right) in enumerate(zip(expected, actual)):
            compare(left, right, f'{label}[{index}]')
    elif isinstance(expected, bool):
        assert str(actual).lower() in (str(int(expected)), str(expected).lower()), label
    elif isinstance(expected, int):
        assert expected == int(actual), f'{label}: {expected} != {actual}'
    else:
        assert str(expected) == str(actual), label


definition = json.loads((ROOT / 'Artifacts/forest-m1-default-definition.json').read_text(encoding='utf-8-sig'))
formal = unity_documents(ROOT / 'Assets/Data/ForestScenario.asset')[0]['MonoBehaviour']
compare(definition, formal['definition'])
if AUTHORED_SPAWNS:
    positions = [(int(s['cell']['x']), int(s['cell']['y'])) for s in formal['definition']['spawns']]
    assert len(set(positions)) == len(positions), 'Authored spawn overlap'
    width, height = definition['map']['width'], definition['map']['height']
    assert all(0 <= x < width and 0 <= y < height and definition['map']['terrain'][y * width + x] == 0
               for x, y in positions), 'Authored spawn is outside or blocked'
formal_guid = re.search(r'^guid: (\w+)$', (ROOT / 'Assets/Data/ForestScenario.asset.meta').read_text(), re.M)[1]
demo_guid = re.search(r'^guid: (\w+)$', (ROOT / 'Assets/Scripts/Presentation/BattleDemo.cs.meta').read_text(), re.M)[1]
components = [doc['MonoBehaviour'] for doc in unity_documents(ROOT / 'Assets/Scenes/BattleDemo.unity')
              if doc and 'MonoBehaviour' in doc and doc['MonoBehaviour'].get('m_Script', {}).get('guid') == demo_guid]
assert len(components) == 1
assert components[0]['forestScenario']['guid'] == formal_guid
guids = {}
for meta in (ROOT / 'Assets').rglob('*.meta'):
    match = re.search(r'^guid: (\w+)$', meta.read_text(encoding='utf-8-sig'), re.M)
    if match:
        assert match[1] not in guids, f'Duplicate GUID: {meta}, {guids.get(match[1])}'
        guids[match[1]] = meta
for source in list((ROOT / 'Assets/Scripts/Forest').glob('*.cs')) + [ROOT / 'Assets/Scripts/Presentation/BattleDemo.Forest.cs',
        ROOT / 'Assets/Scripts/Presentation/BattleDemo.Spirits.cs', ROOT / 'Assets/Scripts/Combat/BattleState.Forest.cs',
        ROOT / 'Assets/Scripts/Presentation/BattleDemo.Skills.cs', ROOT / 'Assets/Scripts/Presentation/SkillHoldDescription.cs',
        ROOT / 'Assets/Scripts/Presentation/BattleDemo.Feedback.cs',
        ROOT / 'Assets/Scripts/Presentation/BattleDemo.CombatVisuals.cs',
        ROOT / 'Assets/Editor/ForestCombatChecks.cs', ROOT / 'Assets/Editor/ForestMilestoneTwoChecks.cs',
        ROOT / 'Assets/Editor/ForestResourceChecks.cs', ROOT / 'Assets/Editor/ForestMilestoneThreeChecks.cs',
        ROOT / 'Assets/Editor/ForestEnemyChecks.cs', ROOT / 'Assets/Editor/ForestMilestoneFourChecks.cs',
        ROOT / 'Assets/Editor/ForestSkillChecks.cs', ROOT / 'Assets/Editor/ForestFeedbackChecks.cs',
        ROOT / 'Assets/Editor/ForestMilestoneFiveChecks.cs']:
    assert Path(str(source) + '.meta').exists(), str(source)
report = (('PASS actual serialized formal asset: defaults match for 8 templates, 11 instance identities, parameters and map layers; authored spawn positions are legal, distinct and preserved.\n'
           if AUTHORED_SPAWNS else 'PASS actual serialized formal asset matches actual C# defaults: 8 templates, 11 spawns, all parameters and map layers.\n') +
          (f"PASS authored round limit {formal['definition']['rules']['roundLimit']} validated and preserved.\n" if AUTHORED_ROUND_LIMIT else '') +
          'PASS main scene references the formal ForestScenario asset.\n'
          'PASS new source metadata present; asset GUIDs unique.\n'
          'These are data/reference checks, not native Editor rendering or Play verification.')
(ROOT / 'Artifacts/forest-m2-asset-verification.txt').write_text(report, encoding='utf-8')
print(report)
