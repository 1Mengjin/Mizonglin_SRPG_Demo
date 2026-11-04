using UnityEngine;
using UnityEngine.UI;
using Mizonglin.Forest;
using System.Collections.Generic;

namespace Mizonglin.Demo
{
    public sealed partial class BattleDemo
    {
        RectTransform skillDescription;
        Transform warningRoot;
        readonly Dictionary<Vector2Int, GameObject> warningMarkers = new Dictionary<Vector2Int, GameObject>();
        ActionKind? ForestSlotAction(int index)
        {
            if (State.selected?.forestUnit == null) return null;
            switch (State.Forest.GetTemplate(State.selected.forestUnit.templateId).behaviorKind)
            {
                case BehaviorKind.FuSheng: return index == 0 ? ActionKind.Talisman : ActionKind.Echo;
                case BehaviorKind.SuLing: return index == 0 ? ActionKind.Heal : ActionKind.Cleanse;
                case BehaviorKind.QingYu: return index == 0 ? ActionKind.SwitchStance : ActionKind.Sprint;
                case BehaviorKind.AShuang: return index == 0 ? ActionKind.Retreat : (ActionKind?)null;
                default: return null;
            }
        }
        void AttachSkillDescription(Button button)
        {
            var hold = button.gameObject.AddComponent<SkillHoldDescription>();
            hold.show = () =>
            {
                HideSkillDescription();
                int index = forestSkillButtons.IndexOf(button);
                ActionKind? kind = index >= 0 ? ForestSlotAction(index) : button == carrySpiritButton ? ActionKind.CarrySpirit : button == throwSpiritButton ? ActionKind.ThrowSpirit : button == swallowSpiritButton ? ActionKind.SwallowSpirit : (ActionKind?)null;
                var wording = kind.HasValue ? SkillDescription(kind.Value) : new BattleDescription { title = button.GetComponentInChildren<Text>().text.Replace('\n', ' '), description = "暂无技能说明。" };
                skillDescription = Box(canvas.transform, "Skill description", new Vector2(.5f, .5f), new Vector2(0, -70), new Vector2(610, 210), ink);
                Label(skillDescription, "Skill title", wording.title, new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(570, 36), 25, TextAnchor.MiddleCenter);
                var text = Label(skillDescription, "Skill effect description", wording.description, new Vector2(.5f, 1), new Vector2(0, -68), new Vector2(570, 120), 21, TextAnchor.UpperLeft);
                Canvas.ForceUpdateCanvases();
                float height = Mathf.Max(80, text.preferredHeight);
                text.rectTransform.sizeDelta = new Vector2(570, height); skillDescription.sizeDelta = new Vector2(610, height + 96);
            };
            hold.hide = HideSkillDescription;
        }
        void HideSkillDescription() { if (skillDescription != null) ReleaseObject(skillDescription.gameObject); skillDescription = null; }
        void RefreshSkillSlots(ForestBattleState battle, UnitState unit, UnitTemplate template)
        {
            for (int i = 0; i < forestSkillButtons.Count; i++)
            {
                var button = forestSkillButtons[i]; var kind = ForestSlotAction(i);
                button.gameObject.SetActive(kind.HasValue);
                if (!kind.HasValue) continue;
                string name = "技能", abilityId = null;
                switch (kind.Value)
                {
                    case ActionKind.Talisman: name = "符箓镇"; abilityId = "talisman"; break;
                    case ActionKind.Echo: name = "符箓·引"; abilityId = "echo"; break;
                    case ActionKind.Heal: name = "治疗"; abilityId = "heal"; break;
                    case ActionKind.Cleanse: name = "涤尘"; abilityId = "cleanse"; break;
                    case ActionKind.SwitchStance: name = ForestRules.Flying(unit) ? "收翼" : "展翼"; abilityId = "stance"; break;
                    case ActionKind.Sprint: name = "疾行"; abilityId = "sprint"; break;
                    case ActionKind.Retreat: name = "退去"; break;
                }
                var ability = abilityId == null ? null : ForestRules.Ability(unit, abilityId);
                int cd = ability == null ? 0 : Mathf.Max(0, ability.availableRound - battle.round);
                bool usable = !busy && !State.selected.enemy && battle.phase == BattlePhase.Player && !ForestRules.IsStunned(battle, unit)
                    && (!unit.acted || ForestRules.IsFreeAction(kind.Value)) && cd == 0;
                string extra = cd > 0 ? "冷却 " + cd : kind == ActionKind.Talisman ? "剩余 " + ability.remainingUses
                    : kind == ActionKind.SwitchStance ? "下回合切换" : kind == ActionKind.Sprint ? "增加一次行动"
                    : kind == ActionKind.Echo ? "传送队友" : kind == ActionKind.Retreat ? unit.dualForm.form == DualForm.Guard ? "下次反伤90%" : "消耗生命攻击" : "可用";
                if (kind == ActionKind.Talisman) usable &= ability.remainingUses > 0;
                if (kind == ActionKind.SwitchStance) usable &= unit.flight.switchRound == 0;
                button.GetComponentInChildren<Text>().text = name + "\n" + extra; button.interactable = usable;
            }
        }
        void PaintMaskWarnings()
        {
            if (warningRoot == null || warningRoot.parent != boardRoot)
            {
                warningMarkers.Clear(); warningRoot = new GameObject("Mask charge warnings").transform; warningRoot.SetParent(boardRoot, false);
            }
            foreach (var marker in warningMarkers.Values) if (marker != null) marker.SetActive(false);
            foreach (var obstacle in obstacles.Values) obstacle.GetComponent<Renderer>().sharedMaterial.color = new Color(.25f, .32f, .17f);
            foreach (var boss in ForestDisplayState.unitsById.Values)
                foreach (var cell in ForestRules.MaskWarningCells(ForestDisplayState, boss))
                {
                    if (tiles.TryGetValue(cell, out var tile)) tile.sharedMaterial.color = new Color(.95f, .25f, .12f);
                    if (obstacles.TryGetValue(cell, out var block)) block.GetComponent<Renderer>().sharedMaterial.color = new Color(.85f, .24f, .12f);
                    if (!warningMarkers.TryGetValue(cell, out var marker) || marker == null)
                    {
                        marker = new GameObject("蓄力预警 " + cell); marker.transform.SetParent(warningRoot, false);
                        var line = marker.AddComponent<LineRenderer>(); line.sharedMaterial = Mat(new Color(1f, .22f, .08f));
                        line.useWorldSpace = false; line.loop = true; line.startWidth = line.endWidth = .06f; line.positionCount = 4;
                        line.SetPositions(new[] { new Vector3(-.46f, 0, -.46f), new Vector3(.46f, 0, -.46f), new Vector3(.46f, 0, .46f), new Vector3(-.46f, 0, .46f) });
                        warningMarkers[cell] = marker;
                    }
                    marker.transform.position = World(cell, State.blocked.Contains(cell) ? .53f : .13f); marker.SetActive(true);
                }
        }
    }
}
