from pathlib import Path
import struct,re
ns={'__name__':'source_data'}
exec(Path(r'G:\.工作夹\笔试紫龙\设计稿\layout.py').read_text(encoding='utf-8-sig'),ns)
assert ns['validate']()['可走格']==149
w,h=ns['W'],ns['H']; op=ns['OPEN']; fog=ns['HAZARD']; zones=ns['zone']
region_names=['密林','出生林','头道门','中庭','树精林A','北道','千面祭坛','东侧林道','树精林B','西径','东连接口']
ints=lambda a: b''.join(struct.pack('<i',i) for i in a).hex()
p=Path('Assets/Data/BattleScenario.asset');s=p.read_text(encoding='utf-8-sig')
a=s.index('  map:');b=s.index('  fighters:',a)
lines=['  map:','    version: 2','    width: 18','    height: 14','    tiles: '+ints([0 if (x,y) in op else 1 for y in range(h) for x in range(w)]),'    spawns:']
pts=ns['SPAWNS']+[ns['TREE_A'],ns['TREE_B'],ns['BOSS'],(16,7)]+ns['CORPSES']
labels=['前锋','游侠','铁卫','斥候','树精A','树精B','千面','吞噬怪','腐卒1','腐卒2','腐卒3']
for i,(x,y) in enumerate(pts):
    lines += ['    - instanceId: '+('0'+str(i+1) if i<4 else 'F'+str(i-3)), '      templateId: '+('U0'+str(i+1) if i<4 else 'E01' if i<=6 else 'E03'),'      label: '+labels[i],f'      x: {x}',f'      y: {y}']
lines+=['    hazardPercent: 10','    hasGoal: 0','    goalX: 14','    goalY: 3','    regions: '+ints([region_names.index(zones[y][x]) if zones[y][x] else 0 for y in range(h) for x in range(w)]),'    initialFog:']
lines += ['    - '+str(int((x,y) in fog)) for y in range(h) for x in range(w)]
for key,pts in [('rootCandidates',sorted(ns['A_CONTROL'])),('patrol',ns['PATROL'])]:
    lines+=['    '+key+':']
    for x,y in pts: lines += [f'    - x: {x}',f'      y: {y}']
lines+=['    topLeftCoordinates: 1']
p.write_text(s[:a]+'\n'.join(lines)+'\n'+s[b:],encoding='utf-8')
