from pathlib import Path
import re,struct
p=Path('Assets/Data/BattleScenario.asset');s=p.read_text(encoding='utf-8-sig')
def arr(name):
    raw=bytes.fromhex(re.search(r'^    '+name+r': ([0-9a-f]+)$',s,re.M)[1]);return list(struct.unpack('<'+'i'*(len(raw)//4),raw))
tiles=arr('tiles');regions=arr('regions')
fog=[int(x) for x in re.findall(r'^    - ([01])$',s.split('    initialFog:\n')[1].split('    rootCandidates:')[0],re.M)]
rows=[list(map(int,line.split(','))) for line in Path('Artifacts/forest-factory-cells.csv').read_text().splitlines()]
assert len(rows)==len(tiles)==len(regions)==len(fog)==252
for x,y,t,r,f in rows:
    i=y*18+x;assert (tiles[i],regions[i],fog[i])==(t,r,f),(x,y)
ns={'__name__':'data_only'};exec(Path(r'G:\.工作夹\笔试紫龙\设计稿\layout.py').read_text(encoding='utf-8-sig'),ns)
for x,y,t,r,f in rows:assert ((x,y) in ns['OPEN'])==(t==0) and ((x,y) in ns['HAZARD'])==bool(f)
print('PASS: Unity asset = actual C# factory = design source (252 cells, terrain/regions/fog).')
# Keep geometry preview distinct from the old timed goal challenge.
p=Path('Assets/Scripts/Combat/BattleDemo.Combat.cs');s=p.read_text(encoding='utf-8-sig').replace('round.text=$"第 {State.turn:00} / 12 回合  ·  {(enemyPhase?"敌方":"我方")}阶段";', 'round.text=State.layout.version==2?$"第 {State.turn:00} 回合 · 地块试玩":$"第 {State.turn:00} / 12 回合  ·  {(enemyPhase?"敌方":"我方")}阶段";').replace('if(State.turn>=12)', 'if(State.layout.version==1 && State.turn>=12)');p.write_text(s,encoding='utf-8')
p=Path('Assets/Scripts/Presentation/BattleDemo.cs');s=p.read_text(encoding='utf-8-sig').replace('(stageThree?"<color=#CD6EB3>━ 危险格 · 行动后扣血</color>":', '(State.layout!=null && State.layout.version==2?"<color=#B980DB>■ 初始瘴气 · 状态预览</color>":stageThree?"<color=#CD6EB3>━ 危险格 · 行动后扣血</color>":');p.write_text(s,encoding='utf-8')
