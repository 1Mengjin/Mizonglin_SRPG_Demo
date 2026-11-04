using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zilong.Forest;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
        ForestBattleState forestMovementPreview;
        Transform spiritRoot;
        readonly Dictionary<string, GameObject> spiritMarkers = new Dictionary<string, GameObject>();
        Button carrySpiritButton, throwSpiritButton, swallowSpiritButton;
        ForestBattleState ForestDisplayState => forestMovementPreview ?? forestSession.State;
        string ForestPhaseCaption
        {
            get
            {
                var s = forestSession.State;
                return $"第 {s.round}/{s.Rules.roundLimit} 回合";
            }
        }

        void RefreshForestMovementPreview()
        {
            forestMovementPreview = null;
            if (State.phase == SelectionPhase.Preview && State.selected != null && !State.selected.enemy && !State.selected.acted)
                forestMovementPreview = ForestRules.PreviewMovement(forestSession.State, State.selected.id, State.Path(State.preview), out _);
        }
        DemoUnit ForestTargetAt(Vector2Int cell)
        {
            if (State.phase == SelectionPhase.Preview && State.selected != null)
            {
                if (cell == State.preview) return State.selected;
                if (cell == State.selected.cell && State.preview != State.selected.cell) return null;
            }
            return State.At(cell);
        }
        SpiritState ForestGroundSpirit()
        {
            if (State.selected == null || State.selected.enemy) return null;
            var actor = ForestDisplayState.unitsById[State.selected.id];
            if (ForestRules.Flying(actor)) return null;
            var path = State.phase == SelectionPhase.Preview ? State.Path(State.preview) : new List<Vector2Int> { actor.cell };
            return path.Select(p => ForestRules.SpiritAt(ForestDisplayState, p)).FirstOrDefault(s => s != null);
        }
        string ForestResourceSummary(ForestBattleState state, UnitState unit)
        {
            int remaining = unit.purificationCharges.Sum(c => state.Rules.purificationCellsPerSpirit - c.clearedCount);
            return "携带元神 " + (unit.carriedSpiritId == null ? "0" : "1") + " 净行余 " + remaining + " 格"
                + (ForestGroundSpirit() != null ? " 可选携带／吞下" : "")
                + (State.phase == SelectionPhase.Preview ? "\n元神操作将确认移动，不消耗行动次数" : "");
        }
        void MakeSpiritButtons()
        {
            carrySpiritButton = RoundAction(actionWheel, "携带\n元神", new Vector2(-190, -46), 94, new Color(.28f, .30f, .16f), () => SelectForestSkill(ActionKind.CarrySpirit));
            throwSpiritButton = RoundAction(actionWheel, "投掷\n元神", new Vector2(-190, 62), 94, new Color(.12f, .34f, .43f), () => SelectForestSkill(ActionKind.ThrowSpirit));
            swallowSpiritButton = RoundAction(actionWheel, "吞下\n净行", new Vector2(-190, 170), 94, new Color(.18f, .38f, .26f), () => SelectForestSkill(ActionKind.SwallowSpirit));
            carrySpiritButton.GetComponentInChildren<Text>().fontSize = throwSpiritButton.GetComponentInChildren<Text>().fontSize = swallowSpiritButton.GetComponentInChildren<Text>().fontSize = 19;
            AttachSkillDescription(carrySpiritButton); AttachSkillDescription(throwSpiritButton); AttachSkillDescription(swallowSpiritButton);
            carrySpiritButton.gameObject.SetActive(false); throwSpiritButton.gameObject.SetActive(false); swallowSpiritButton.gameObject.SetActive(false);
        }
        void RefreshSpiritButtons(ForestBattleState state, UnitState unit, bool enemy)
        {
            bool carrying = !enemy && unit.carriedSpiritId != null;
            bool ground = !enemy && ForestGroundSpirit() != null;
            carrySpiritButton.gameObject.SetActive(ground); throwSpiritButton.gameObject.SetActive(carrying); swallowSpiritButton.gameObject.SetActive(carrying || ground);
            bool usable = !enemy && !busy && !ForestRules.Flying(unit) && state.phase == BattlePhase.Player;
            carrySpiritButton.interactable = usable && ground && !carrying;
            throwSpiritButton.interactable = usable && carrying;
            swallowSpiritButton.interactable = usable && (carrying || ground);
        }
        void RefreshSpiritMarkers()
        {
            foreach (var stripe in hazardMarks) stripe.Value.SetActive(ForestDisplayState.groundFog.Contains(stripe.Key));
            if (spiritRoot == null || spiritRoot.parent != boardRoot)
            {
                spiritMarkers.Clear(); spiritRoot = new GameObject("Spirit resources").transform; spiritRoot.SetParent(boardRoot, false);
            }
            foreach (var marker in spiritMarkers.Values) if (marker != null) marker.SetActive(false);
            foreach (var spirit in ForestDisplayState.spiritsById.Values.Where(s => s.locationKind == SpiritLocation.Ground))
            {
                if (!spiritMarkers.TryGetValue(spirit.spiritId, out var marker) || marker == null)
                {
                    marker = new GameObject("元神 " + spirit.spiritId); marker.transform.SetParent(spiritRoot, false);
                    Shape("Spirit core", PrimitiveType.Sphere, marker.transform, Vector3.up * .20f, Vector3.one * .30f, gold);
                    var ring = Ring("Spirit ring", jade, .26f); ring.SetParent(marker.transform, false);
                    spiritMarkers[spirit.spiritId] = marker;
                }
                marker.SetActive(true); marker.transform.position = World(spirit.cell, .08f);
            }
        }
    }
}
