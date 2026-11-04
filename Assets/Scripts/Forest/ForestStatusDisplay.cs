using System;
using System.Collections.Generic;
using System.Linq;

namespace Zilong.Forest
{
    public enum StatusIcon { Poison, Scar, Shield, Sword, Wing, Reflect, Link, Heart, Stun, Spirit, Purify, Action, Fruit, Fog, Mask, Charge, Cooldown }
    public enum StatusTone { Positive, Negative, Neutral }
    public sealed class ForestStatusEntry
    {
        public string id, title, count, description;
        public StatusIcon icon;
        public StatusTone tone;
    }
    // Only active in-battle effects and marks; action UI and permanent ability descriptions live elsewhere.
    public static class ForestStatusDisplay
    {
        public static List<ForestStatusEntry> Build(ForestBattleState s, UnitState u)
        {
            var entries = new List<ForestStatusEntry>(); var t = s.GetTemplate(u.templateId); var rules = s.Rules;
            if (!ForestRules.Alive(u)) return entries;
            bool ally = t.faction == Faction.Ally;
            void Add(string id, string title, StatusIcon icon, string count, string description, StatusTone tone = StatusTone.Positive)
                => entries.Add(new ForestStatusEntry { id = id, title = title, icon = icon, count = count, description = description, tone = tone });
            if (u.poisonStacks > 0)
                Add("poison", "瘴毒", StatusIcon.Poison, u.poisonStacks.ToString(),
                    $"当前 {u.poisonStacks} 层，上限 {rules.poisonMaxStacks} 层。\n攻击力降低 {100f * (rules.poisonAttackDenominator - rules.poisonAttackNumerator) / rules.poisonAttackDenominator:0.#}%；2层起移动力减少 {rules.poisonMovePenalty}。\n满层时，回合末损失最大生命的 {rules.poisonDamagePercent}%。在雾中增加层数，离雾减少层数；飞行时层数不变。", StatusTone.Negative);
            if (u.scarStacks > 0)
                Add("scar", "蚀痕", StatusIcon.Scar, u.scarStacks.ToString(), $"当前 {u.scarStacks} 层。每层使最大生命与当前生命减少 {rules.scarHpPenalty}。\n净行每清满 {rules.purificationCellsPerScar} 格瘴气增加1层。不可驱散。", StatusTone.Negative);
            if (ForestRules.IsStunned(s, u))
                Add("stun", "晕眩", StatusIcon.Stun, (u.stunnedUntilRound - s.round + 1).ToString(),
                    ally ? "禁止释放技能，可以移动和普通攻击。" : "无法进行任何操作（技能、普攻、移动、自爆）；被击杀时不触发亡语。", StatusTone.Negative);
            if (u.flight != null)
            {
                bool flying = ForestRules.Flying(u);
                if (flying) Add("stance", "飞行", StatusIcon.Wing, "", "移动力+1，可穿越障碍和单位，忽略根须额外费用。\n不能攻击、反击或操作元神，净行暂停，瘴毒层数不变。", StatusTone.Positive);
                if (u.flight.switchRound > 0)
                    Add("pending-stance", "待切换", StatusIcon.Cooldown, Math.Max(0, u.flight.switchRound - s.round).ToString(), $"第 {u.flight.switchRound} 回合开始切换为{(u.flight.pendingStance == FlightStance.Flying ? "飞行" : "落地")}，本回合姿态不变。", StatusTone.Neutral);
            }
            if (u.dualForm != null)
            {
                int cycle = (s.round - 1) % t.dualForm.cycle; bool guard = u.dualForm.form == DualForm.Guard;
                Add("form", guard ? "守形态" : "攻形态", guard ? StatusIcon.Shield : StatusIcon.Sword,
                    (guard ? t.dualForm.guardTurns - cycle : t.dualForm.cycle - cycle).ToString(),
                    guard ? $"移动力-1，防御+50%，当前防御 {ForestRules.Stats(s, u).defense}。普攻相邻敌人，造成50%攻击力的伤害，不反击。\n{t.dualForm.guardTurns - cycle} 回合后转为攻形态。"
                    : $"最大生命+50%，攻击+20%，受到伤害+30%；切换形态等比例换算生命。当前攻击 {ForestRules.Stats(s, u).attack}，射程 {t.dualForm.attackRange}。\n受伤后对伤害来源反击，造成120%攻击力的伤害，无距离限制。\n每次主要行动后损失初始最大生命的 {t.dualForm.selfDamagePercent}%。{t.dualForm.cycle - cycle} 回合后转为守形态。");
            }
            if (u.awakeningSources.Count > 0) Add("awakening", "唤灵", StatusIcon.Heart, u.awakeningSources.Count.ToString(), "上限3层。受伤后清空，每层恢复素铃当前攻击力50%的生命。", StatusTone.Positive);
            if (u.curseSources.Count > 0) Add("curse", "咒言", StatusIcon.Charge, u.curseSources.Count.ToString(), "上限8层。受伤后清空，向伤害来源造成每层4%符生当前攻击力的固定伤害。", StatusTone.Positive);
            if (u.reflectPercent > 0) Add("reflect", "反伤", StatusIcon.Reflect, u.reflectPercent + "%", $"下次受伤时，向伤害来源反还受到伤害的 {u.reflectPercent}%。触发后消失。");
            if (ally && u.currentHp <= ForestRules.NearDeathLine(s, u)) Add("near-death", "濒死", StatusIcon.Heart, "!", $"当前生命不高于最大生命的 {rules.nearDeathPercent}%。", StatusTone.Negative);
            if (ally)
            {
                if (u.carriedSpiritId != null) Add("spirit", "元神", StatusIcon.Spirit, "1", $"携带1颗元神，可投掷或吞下，不消耗主要行动。\n投掷射程 {rules.spiritThrowRange}，清除落点半径 {rules.spiritClearRadius} 内的瘴气。");
                int remaining = u.purificationCharges.Sum(c => rules.purificationCellsPerSpirit - c.clearedCount);
                if (remaining > 0) Add("purify", "净行", StatusIcon.Purify, remaining.ToString(), $"剩余 {remaining} 格清雾额度，清除移动经过地块的瘴气，包含起点与终点。\n每清 {rules.purificationCellsPerScar} 格增加1层蚀痕，飞行时暂停。");
            }
            if (u.tree != null && u.tree.fruitLevel > 1) Add("fruit", "瘴果", StatusIcon.Fruit, u.tree.fruitLevel.ToString(), $"当前果级 {u.tree.fruitLevel}/{t.tree.maxFruitLevel}。第 {u.tree.nextBurstRound} 回合末爆果，在半径 {u.tree.fruitLevel} 内铺下瘴气。\n受到普攻时果级增加1，爆果后回到1级。", StatusTone.Negative);
            if (u.devourer != null && u.devourer.carriedFogCount > 0) Add("fog", "携雾", StatusIcon.Fog, u.devourer.carriedFogCount.ToString(), $"当前携雾 {u.devourer.carriedFogCount}，死亡后释放为地面瘴气。", StatusTone.Negative);
            if (u.maskBoss != null)
            {
                if (u.maskBoss.releaseRound > 0) Add("charge", "蓄力", StatusIcon.Charge, Math.Max(0, u.maskBoss.releaseRound - s.round).ToString(), $"第 {u.maskBoss.releaseRound} 回合释放，对锁定的3×3区域造成60点固定伤害。", StatusTone.Negative);
            }
            return entries;
        }
    }
}
