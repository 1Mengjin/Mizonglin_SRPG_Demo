"""Run native serialization checks in a small isolated Unity project; never closes or edits the open scene."""
from pathlib import Path
import json
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'Artifacts/ForestM1UnityProject'
for directory in ('Assets/Editor', 'Assets/Data', 'ProjectSettings', 'Packages'):
    (PROJECT / directory).mkdir(parents=True, exist_ok=True)

sources = list((ROOT / 'Assets/Scripts/Forest').glob('*.cs')) + [
    ROOT / 'Assets/Scripts/Map/LevelMap.cs', ROOT / 'Assets/Scripts/Map/LevelMap.Forest.cs',
    ROOT / 'Assets/Scripts/Combat/BattleState.cs', ROOT / 'Assets/Scripts/Combat/CombatRules.cs',
    ROOT / 'Assets/Scripts/Combat/BattleState.Forest.cs',
    ROOT / 'Assets/Scripts/Configuration/BattleScenario.cs', ROOT / 'Assets/Editor/ForestDataChecks.cs',
    ROOT / 'Assets/Data/ForestScenario.asset', ROOT / 'Assets/Data/BattleScenario.asset',
]
for source in sources:
    target = PROJECT / source.relative_to(ROOT)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    meta = Path(str(source) + '.meta')
    if meta.exists():
        shutil.copy2(meta, Path(str(target) + '.meta'))
shutil.copy2(ROOT / 'ProjectSettings/ProjectVersion.txt', PROJECT / 'ProjectSettings/ProjectVersion.txt')
(PROJECT / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {
    'com.unity.modules.jsonserialize': '1.0.0', 'com.unity.modules.imgui': '1.0.0'
}}, indent=2), encoding='utf-8')
(PROJECT / 'Assets/Editor/ForestM1Batch.cs').write_text(r'''
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zilong.Demo;
using Zilong.Forest;
public static class ForestM1Batch
{
    public static void Run()
    {
        var formal = AssetDatabase.LoadAssetAtPath<ForestScenario>("Assets/Data/ForestScenario.asset");
        if (formal == null) throw new Exception("Formal .asset failed to load");
        string report = ForestDataChecks.Run(formal.definition);
        string json = JsonUtility.ToJson(formal.definition);
        var restored = JsonUtility.FromJson<ScenarioDefinition>(json);
        ForestDataChecks.Run(restored);
        if (json != JsonUtility.ToJson(restored)) throw new Exception("Formal JSON roundtrip mismatch");
        var legacy = AssetDatabase.LoadAssetAtPath<BattleScenario>("Assets/Data/BattleScenario.asset");
        if (legacy == null || legacy.Validate() != null) throw new Exception("Legacy .asset failed to load");
        var legacyMap = JsonUtility.FromJson<LevelMap>(JsonUtility.ToJson(legacy.map));
        if (legacyMap.Validate() != null) throw new Exception("Legacy JSON failed to load");
        var state = new BattleState(); state.Reset(legacyMap); legacy.CopyFighters().Apply(state, legacyMap);
        foreach (var map in new[] { LevelMap.Default(), LevelMap.Forest() })
        {
            state.Reset(map); FighterBook.Defaults().Apply(state, map);
            if (state.units.Count != map.spawns.Count) throw new Exception("Legacy initialization failed");
        }
        report += "\nPASS Unity batch: actual formal/legacy assets imported; native JSON roundtrip; actual legacy JSON initialization."
            + "\nExecuted in isolated verification project; original scene and gameplay bindings untouched.";
        File.WriteAllText(Path.GetFullPath("../forest-m1-unity-batch-verification.txt"), report);
        Debug.Log("FOREST_M1_NATIVE_PASS\n" + report);
    }
}
''', encoding='utf-8')
result = subprocess.run([r'G:\Unity\Editors\Unity2022.3\Editor\Unity.exe', '-batchmode', '-nographics', '-quit',
                         '-projectPath', str(PROJECT), '-executeMethod', 'ForestM1Batch.Run',
                         '-logFile', str(ROOT / 'Artifacts/forest-m1-unity-batch.log')], cwd=ROOT, timeout=180)
if result.returncode != 0:
    log = ROOT / 'Artifacts/forest-m1-unity-batch.log'
    print(log.read_text(encoding='utf-8-sig', errors='replace')[-10000:] if log.exists() else 'Unity exited before creating a log.')
    raise SystemExit(result.returncode)
print((ROOT / 'Artifacts/forest-m1-unity-batch-verification.txt').read_text(encoding='utf-8-sig'))
