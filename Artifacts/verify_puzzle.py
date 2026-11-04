"""Compile actual project code and verify the authored puzzle invariants without Unity Play."""
from pathlib import Path
import re
import subprocess
import yaml
import verify_forest_m1 as b


def dump_authored_config():
    """ForestDefaults stays a separate preset, so export the authored numbers from the shipped .asset."""
    source = Path('Assets/Data/ForestScenario.asset').read_text(encoding='utf-8-sig')
    source = re.sub(r'^%.*\n', '', source, flags=re.M)
    source = re.sub(r'^--- !u!\d+ &-?\d+.*$', '---', source, flags=re.M)
    definition = yaml.load_all(source, Loader=yaml.BaseLoader).__next__()['MonoBehaviour']['definition']
    number = re.compile(r'^-?\d+$')
    lines = []

    def walk(prefix, values):
        for key, value in values.items():
            if key in ('portrait', 'name', 'profession', 'templateId'):
                continue
            path = f'{prefix}.{key}'
            if isinstance(value, dict):
                walk(path, value)
            elif isinstance(value, str) and number.match(value):
                lines.append(f'{path}={value}')
            elif isinstance(value, list) and value and isinstance(value[0], dict):
                for index, item in enumerate(value):
                    walk(f'{path}.{index}', item)

    for template in definition['unitTemplates']:
        walk(f"unitTemplates.{template['templateId']}", template)
    walk('rules', definition['rules'])
    Path('Artifacts/puzzle-config.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')


dump_authored_config()
b.compile_to('PuzzleStandalone', b.runtime + ['-r:"Artifacts/ForestM1.Runtime.dll"'], [Path('Assets/Editor/ForestPuzzleChecks.cs'), Path('Artifacts/PuzzleStandalone.cs')], 'exe')
subprocess.run([str(b.MONO), 'Artifacts/PuzzleStandalone.exe'], check=True, env=b.env)
