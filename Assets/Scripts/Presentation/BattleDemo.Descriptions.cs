using System.Collections.Generic;
using UnityEngine;
using Mizonglin.Forest;

namespace Mizonglin.Demo
{
    public sealed partial class BattleDemo
    {
        BattleTextCatalog battleTextCatalog;
        BattleDescription Description(string id, string title, string description, string count = "", DemoUnit caster = null)
        {
            if (battleTextCatalog == null) battleTextCatalog = Resources.Load<BattleTextCatalog>(BattleTextCatalog.ResourceName);
            if (battleTextCatalog == null) return new BattleDescription { title = title, description = description };
            caster = caster ?? State.selected;
            var battle = ForestEnabled ? ForestDisplayState : null;
            var unit = battle != null && caster != null && battle.unitsById.TryGetValue(caster.id, out var live) ? live : null;
            var template = unit == null ? null : battle.GetTemplate(unit.templateId);
            var values = new Dictionary<string, string> {
                ["角色名"] = caster?.name ?? "", ["层数"] = count ?? "", ["回合"] = State.turn.ToString(),
                ["符箓距离"] = template?.talisman?.castRange.ToString() ?? "", ["符箓范围"] = template?.talisman?.squareSize.ToString() ?? "",
                ["传送距离"] = template?.talisman?.echoRange.ToString() ?? "", ["治疗量"] = template?.support?.healAmount.ToString() ?? "",
                ["治疗距离"] = template?.support?.healRange.ToString() ?? "", ["涤尘距离"] = template?.support?.cleanseRange.ToString() ?? "",
                ["涤尘冷却"] = template?.support?.cleanseCooldownTurns.ToString() ?? "", ["姿态冷却"] = template?.flight?.switchCooldownTurns.ToString() ?? "",
                ["投掷距离"] = battle?.Rules.spiritThrowRange.ToString() ?? "", ["清雾半径"] = battle?.Rules.spiritClearRadius.ToString() ?? "",
                ["净行额度"] = battle?.Rules.purificationCellsPerSpirit.ToString() ?? "", ["蚀痕间隔"] = battle?.Rules.purificationCellsPerScar.ToString() ?? "",
                ["蚀痕代价"] = battle?.Rules.scarHpPenalty.ToString() ?? ""
            };
            return battleTextCatalog.Resolve(id, title, description, values);
        }
        string StatusDescriptionId(ForestStatusEntry entry)
        {
            var unit = State.selected?.forestUnit;
            if (entry.id == "stance") return "status.stance." + (ForestRules.Flying(unit) ? "flying" : "grounded");
            if (entry.id == "form") return "status.form." + (unit?.dualForm?.form == DualForm.Guard ? "guard" : "attack");
            return "status." + entry.id;
        }
        BattleDescription StatusDescription(ForestStatusEntry entry) => Description(StatusDescriptionId(entry), entry.title, entry.description, entry.count);
        BattleDescription SkillDescription(ActionKind kind, DemoUnit actor = null)
        {
            actor = actor ?? State.selected;
            string fallback = kind == ActionKind.SwitchStance ? "展翼后下回合进入飞行，飞行移动力+1；收翼后下回合落地。"
                : kind == ActionKind.Echo ? "将范围内另一名存活队友传送至身边四邻格中指定的可走空格；先选择队友，再选择落点。"
                : "暂无技能说明。";
            return Description("skill." + kind, SkillCaption(kind, actor?.forestUnit), fallback, caster: actor);
        }
    }
}
