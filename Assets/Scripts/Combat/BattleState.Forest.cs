using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mizonglin.Forest;

namespace Mizonglin.Demo
{
    public sealed partial class BattleState
    {
        public ForestBattleState Forest { get; private set; }
        public void BindForest(ForestBattleState forest, ScenarioDefinition definition)
        {
            Clear(); units.Clear(); blocked.Clear(); hazards.Clear(); fog.Clear();
            Forest = forest; Width = forest.Map.width; Height = forest.Map.height;
            layout = ForestAuthoring.ViewMap(definition); hasGoal = false; goal = default;
            for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            { var p = new Vector2Int(x, y); if (!forest.Map.Walkable(p)) blocked.Add(p); }
            SyncForest();
        }
        public void SyncForest()
        {
            if (Forest == null) return;
            turn = Forest.round; fog.Clear(); fog.UnionWith(Forest.groundFog);
            var old = units.ToDictionary(u => u.id); units.Clear();
            foreach (var unit in Forest.unitsById.Values.Where(ForestRules.Alive))
            {
                var t = Forest.GetTemplate(unit.templateId); var effective = ForestRules.Stats(Forest, unit);
                if (!old.TryGetValue(unit.instanceId, out var view))
                    view = new DemoUnit(unit.instanceId, t.name, t.faction == Faction.Enemy, unit.cell.x, unit.cell.y, effective.move, unit.currentHp);
                view.cell = unit.cell; view.hp = unit.currentHp; view.move = effective.move; view.acted = unit.acted;
                view.name = t.name; view.enemy = t.faction == Faction.Enemy; view.forestUnit = unit; view.forestBattle = Forest;
                view.stats = new FighterTemplate(t.templateId, t.name, effective.maxHp, effective.attack, effective.defense,
                    effective.move, effective.range) { profession = t.profession, portrait = t.portrait };
                units.Add(view);
            }
            if (selected != null && !units.Contains(selected)) Clear();
        }
    }
}
