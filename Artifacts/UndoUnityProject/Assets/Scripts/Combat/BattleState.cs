using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zilong.Demo
{
    public enum SelectionPhase { None, Selected, Preview, Inspect }
    [Serializable]
    public sealed class DemoUnit
    {
        public string id, name;
        public bool enemy, acted;
        public Vector2Int cell;
        public int move, hp;
        public FighterTemplate stats;
        public Zilong.Forest.UnitState forestUnit;
        public Zilong.Forest.ForestBattleState forestBattle;
        public Vector2Int anchor;
        public DemoUnit(string id, string name, bool enemy, int x, int y, int move, int hp)
        { this.id = id; this.name = name; this.enemy = enemy; cell = new Vector2Int(x,y); this.move = move; this.hp = hp; }
    }

    // Logical position changes only on commit. A preview never mutates unit data.
    public sealed partial class BattleState
    {
        public const int Size = 10;
        public int Width { get; private set; } = 10;
        public int Height { get; private set; } = 10;
        public LevelMap layout;
        public readonly HashSet<Vector2Int> fog = new HashSet<Vector2Int>();
        public readonly List<DemoUnit> units = new List<DemoUnit>();
        public readonly HashSet<Vector2Int> blocked = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> hazards = new HashSet<Vector2Int>();
        public readonly Dictionary<Vector2Int, Vector2Int> parents = new Dictionary<Vector2Int, Vector2Int>();
        public readonly Dictionary<Vector2Int, int> reachable = new Dictionary<Vector2Int, int>();
        public DemoUnit selected;
        public SelectionPhase phase;
        public bool CanCancelMovementPreview => selected != null && !selected.enemy && !selected.acted && phase == SelectionPhase.Preview;
        public Vector2Int preview;
        public int turn;
        public Vector2Int goal = new Vector2Int(8,8);
        public bool hasGoal = true;
        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

        public BattleState() { Reset(); }
        public void Reset()
        {
            Forest = null;
            Width=Height=10; layout=null; fog.Clear();
            Clear(); turn = 1; units.Clear(); blocked.Clear(); hazards.Clear(); goal=new Vector2Int(8,8); hasGoal=true;
            units.Add(new DemoUnit("01", "前锋", false, 2,1,3,100));
            units.Add(new DemoUnit("02", "游侠", false, 1,2,3,75));
            units.Add(new DemoUnit("03", "铁卫", false, 3,1,2,130));
            units.Add(new DemoUnit("04", "斥候", false, 2,3,4,80));
            units.Add(new DemoUnit("E1", "弩手", true, 7,6,0,60));
            units.Add(new DemoUnit("E2", "追兵", true, 5,7,3,70));
            units.Add(new DemoUnit("E3", "守卫", true, 8,8,2,110));
            foreach (int x in new[] {0,1,2,4,5,7,8,9}) blocked.Add(new Vector2Int(x,5));
        }
        public void Reset(LevelMap map)
        {
            Forest = null;
            Width=map.width; Height=map.height; layout=map.Clone(); fog.Clear();
            Clear(); turn=1; units.Clear(); blocked.Clear(); hazards.Clear(); goal=map.Goal; hasGoal=map.hasGoal;
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++)
            { var p=new Vector2Int(x,y); if(map.At(p)==Ground.Blocked) blocked.Add(p); if(map.At(p)==Ground.Hazard) hazards.Add(p); if(map.FogAt(p)) fog.Add(p); }
            string[] names={"前锋","游侠","铁卫","斥候","弩手","追兵","守卫"};
            int[] moves={3,3,2,4,0,3,2}, hps={100,75,130,80,60,70,110};
            foreach(var spawn in map.spawns)
            {
                int i=Array.IndexOf(LevelMap.Templates,spawn.templateId);
                units.Add(new DemoUnit(spawn.instanceId,names[i],i>=4,spawn.x,spawn.y,moves[i],hps[i]));
            }
        }
        public bool Inside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Width && p.y < Height;
        public DemoUnit At(Vector2Int p) => units.Find(u => u.cell == p);
        public void Clear() { selected = null; phase = SelectionPhase.None; reachable.Clear(); parents.Clear(); preview = default; }
        public void Select(DemoUnit unit)
        {
            Clear(); selected = unit;
            if (unit == null) return;
            phase = unit.enemy || unit.acted ? SelectionPhase.Inspect : SelectionPhase.Selected;
            preview = unit.cell;
            if (phase == SelectionPhase.Inspect) return;
            if (Forest != null)
            {
                var area = Zilong.Forest.ForestPathfinder.Reachable(Forest, unit.forestUnit);
                foreach (var entry in area.costs) reachable.Add(entry.Key, entry.Value);
                foreach (var entry in area.parents) parents.Add(entry.Key, entry.Value);
                return;
            }
            var queue = new Queue<Vector2Int>(); queue.Enqueue(unit.cell); reachable[unit.cell] = 0;
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (reachable[p] >= unit.move) continue;
                foreach (var d in Directions)
                {
                    var n = p + d;
                    if (!Inside(n) || blocked.Contains(n) || At(n) != null || reachable.ContainsKey(n)) continue;
                    reachable[n] = reachable[p]+1; parents[n] = p; queue.Enqueue(n);
                }
            }
        }
        public bool Preview(Vector2Int p)
        {
            if (phase != SelectionPhase.Selected || !reachable.ContainsKey(p)) return false;
            preview = p; phase = SelectionPhase.Preview; return true;
        }
        public List<Vector2Int> Path(Vector2Int p)
        {
            var path = new List<Vector2Int>();
            if (selected == null || !reachable.ContainsKey(p)) return path;
            path.Add(p);
            while (p != selected.cell) { p = parents[p]; path.Add(p); }
            path.Reverse(); return path;
        }
        public void CollectMovementBorder(HashSet<Vector2Int> border)
        {
            border.Clear();
            if(phase!=SelectionPhase.Selected && phase!=SelectionPhase.Preview) return;
            foreach(var p in reachable.Keys)
            foreach(var d in Directions)
            {
                var n=p+d;
                if(Inside(n) && !blocked.Contains(n) && !reachable.ContainsKey(n)) border.Add(n);
            }
        }
        public void Cancel()
        {
            if (phase == SelectionPhase.Preview) Select(selected); else Clear();
        }
        public bool CommitWait()
        {
            if (Forest != null) return false; // Formal actions must commit through ForestRules.
            if (phase != SelectionPhase.Preview) return false;
            selected.cell = preview; selected.acted = true; Clear(); return true;
        }
        public void NextTurn() { Clear(); turn++; foreach (var u in units) u.acted = false; }
    }
}
