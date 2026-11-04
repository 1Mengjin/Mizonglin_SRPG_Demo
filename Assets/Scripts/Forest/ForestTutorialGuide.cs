using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zilong.Forest
{
    [Flags] public enum ForestTutorialTopic { None = 0, Fog = 1, Mask = 2 }

    // Presentation progress belongs to a run, never to the authored scenario or rule state.
    public sealed class ForestTutorialGuide
    {
        public ForestTutorialTopic Shown { get; private set; }
        public const int EncounterDistance = 4;
        public void Reset() => Shown = ForestTutorialTopic.None;
        public void MarkShown(ForestTutorialTopic topic) => Shown |= topic;

        public ForestTutorialTopic Pending(ForestBattleState s, string actorId = null, string targetId = null, IList<Vector2Int> path = null)
        {
            if (s.result != BattleResult.InProgress) return ForestTutorialTopic.None;
            var allies = s.unitsById.Values.Where(u => ForestRules.Alive(u) && s.GetTemplate(u.templateId).faction == Faction.Ally).ToArray();
            var bosses = s.unitsById.Values.Where(u => ForestRules.Alive(u) && u.maskBoss != null).ToArray();
            s.unitsById.TryGetValue(actorId ?? "", out var actor);
            s.unitsById.TryGetValue(targetId ?? "", out var target);
            ForestTutorialTopic pending = ForestTutorialTopic.None;
            bool fog = allies.Any(u => u.poisonStacks > 0 || !ForestRules.Flying(u) && s.groundFog.Contains(u.cell));
            if (actor != null && ForestRules.Alive(actor) && s.GetTemplate(actor.templateId).faction == Faction.Ally && !ForestRules.Flying(actor) && path != null)
                fog |= path.Any(s.groundFog.Contains);
            if (fog) pending |= ForestTutorialTopic.Fog;
            bool mask = actor?.maskBoss != null && ForestRules.Alive(actor) || target?.maskBoss != null && ForestRules.Alive(target)
                || bosses.Any(b => allies.Any(a => ForestRules.Distance(a.cell, b.cell) <= EncounterDistance));
            if (actor != null && ForestRules.Alive(actor) && s.GetTemplate(actor.templateId).faction == Faction.Ally && path != null)
                mask |= bosses.Any(b => path.Any(p => ForestRules.Distance(p, b.cell) <= EncounterDistance));
            if (mask) pending |= ForestTutorialTopic.Mask;
            return pending & ~Shown;
        }

        public static string Title(ForestTutorialTopic topic) => topic == ForestTutorialTopic.Fog ? "瘴气与瘴毒" : "千面与元神";
        public static string Body(ForestTutorialTopic topic, ForestBattleState s)
        {
            var r = s.Rules;
            string Heading(string value) => "<b>" + value + "</b>\n";
            if (topic == ForestTutorialTopic.Fog)
            {
                string attackLoss = (100.0 * (r.poisonAttackDenominator - r.poisonAttackNumerator) / r.poisonAttackDenominator).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                return Heading("瘴气地块") + $"回合结束时，落地角色若处在瘴气中，会叠加1层瘴毒，最多 {r.poisonMaxStacks}层。\n\n"
                    + Heading("瘴毒效果") + $"1层：攻击力降低 {attackLoss}%。\n"
                    + $"2层：攻击力降低 {attackLoss}%，移动力减少 {r.poisonMovePenalty}。\n"
                    + $"{r.poisonMaxStacks}层：保留以上惩罚，回合末再损失最大生命的 {r.poisonDamagePercent}%（向上取整）。\n\n"
                    + Heading("应对方式") + "离开瘴气后，回合末减少1层瘴毒。飞行时瘴毒层数不变；已有层数的属性惩罚仍然生效。";
            }
            int phases = s.unitsById.Values.Where(u => u.maskBoss != null).Select(u => s.GetTemplate(u.templateId).maskBoss.phaseCount).DefaultIfEmpty(4).First();
            return Heading("千面的阶段") + $"千面共有 {phases}个阶段。每次击破会在附近可用地块掉落1颗元神；最后阶段击破后死亡。\n"
                + Heading("元神：携带、吞下或投掷") + $"落地角色接触后可携带或吞下，携带后可投掷。射程 {r.spiritThrowRange}，清除落点曼哈顿半径 {r.spiritClearRadius} 内的地面瘴气。元神操作不消耗主要行动。\n"
                + Heading("净行") + $"吞下后获得 {r.purificationCellsPerSpirit}格清雾额度。随落地实际移动逐格清雾，包含起点与落点；仅成功清雾才扣额度，用尽后消失。\n"
                + Heading("蚀痕") + $"每份额度每清除 {r.purificationCellsPerScar} 格瘴气，叠加1层不可驱散的蚀痕。每层使最大生命与当前生命减少 {r.scarHpPenalty}，最低保留1点。";
        }
    }
}
