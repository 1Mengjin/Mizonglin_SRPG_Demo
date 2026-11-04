using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Zilong.Forest
{
    public static class ForestAudit
    {
        public static List<string> Check(ForestBattleState s)
        {
            var errors = new List<string>();
            if (s.groundFog.Count + s.CarriedFogCount != s.statistics.initialFogCount + s.statistics.generatedFogCount - s.statistics.clearedFogCount) errors.Add("污染总量不守恒");
            if (s.eventLog.Where(e => e.kind == "FogGenerated").Sum(e => e.amount) != s.statistics.generatedFogCount) errors.Add("新增瘴气与逐格记录不符");
            if (s.eventLog.Where(e => e.kind == "FogCleared").Sum(e => e.amount) != s.statistics.clearedFogCount) errors.Add("清雾与逐格记录不符");
            if (s.groundFog.Any(p => !s.Map.Walkable(p))) errors.Add("不可走格存在地面雾");
            foreach (var u in s.unitsById.Values)
            {
                if (u.devourer != null && s.eventLog.Where(e => e.sourceId == u.instanceId && e.kind == "FogAbsorbed").Sum(e => e.amount)
                    - s.eventLog.Where(e => e.sourceId == u.instanceId && e.kind == "FogSpilled").Sum(e => e.amount) != u.devourer.carriedFogCount) errors.Add(u.instanceId + "携雾与转移记录不符");
                if (u.devourer != null && !s.GetTemplate(u.templateId).devourer.growthEnabled && (ForestRules.Stats(s, u).maxHp != s.GetTemplate(u.templateId).maxHp
                    || ForestRules.Stats(s, u).attack != s.GetTemplate(u.templateId).attack || u.currentHp > s.GetTemplate(u.templateId).maxHp)) errors.Add(u.instanceId + "增长关闭时属性变化");
                if (u.carriedSpiritId != null && (!s.spiritsById.TryGetValue(u.carriedSpiritId, out var held) || held.locationKind != SpiritLocation.Carried || held.carrierId != u.instanceId)) errors.Add(u.instanceId + "携带元神引用不一致");
                if (u.maskBoss != null)
                    for (int phase = 1; phase <= s.GetTemplate(u.templateId).maskBoss.phaseCount; phase++)
                        if (s.spiritsById.Values.Count(a => a.sourceInstanceId == u.instanceId && a.sourcePhase == phase) != (((u.maskBoss.resolvedPhaseMask & (1 << (phase - 1))) != 0) ? 1 : 0)) errors.Add(u.instanceId + "阶段" + phase + "元神数量不符");
            }
            if (s.spiritsById.Values.Where(a => a.locationKind == SpiritLocation.Ground).GroupBy(a => a.cell).Any(g => g.Count() > 1)) errors.Add("同格存在多颗地面元神");
            foreach (var a in s.spiritsById.Values)
            {
                if (a.sourceInstanceId == null || !s.unitsById.TryGetValue(a.sourceInstanceId, out var source) || source.maskBoss == null
                    || a.sourcePhase < 1 || a.sourcePhase > s.GetTemplate(source.templateId).maskBoss.phaseCount) errors.Add(a.spiritId + "来源阶段不合法");
                if (a.locationKind == SpiritLocation.Ground && (!s.Map.Walkable(a.cell) || a.carrierId != null)) errors.Add(a.spiritId + "地面状态不合法");
                if (a.locationKind == SpiritLocation.Carried && (a.carrierId == null || !s.unitsById.TryGetValue(a.carrierId, out var carrier) || !ForestRules.Alive(carrier) || carrier.carriedSpiritId != a.spiritId)) errors.Add(a.spiritId + "携带者不一致");
                if (a.locationKind == SpiritLocation.Consumed && (a.carrierId != null || a.consumedBy == null || !s.unitsById.ContainsKey(a.consumedBy))) errors.Add(a.spiritId + "消耗记录不完整");
                if (s.pendingDrops.Count(p => p.spiritId == a.spiritId) != (a.locationKind == SpiritLocation.Pending ? 1 : 0)) errors.Add(a.spiritId + "待掉落记录不一致");
            }
            if (s.pendingDrops.Any(p => !s.spiritsById.ContainsKey(p.spiritId))) errors.Add("待掉落引用未知元神");
            return errors;
        }
        public static string EventText(BattleEvent e)
        {
            string kind;
            switch (e.kind)
            {
                case "FogGenerated": kind = "新增瘴气"; break; case "FogAbsorbed": kind = "吸收转移"; break; case "FogSpilled": kind = "死亡泄雾"; break; case "FogCleared": kind = "实际清雾"; break;
                case "SpiritDrop": kind = "元神落地"; break; case "SpiritPending": kind = "元神待掉落"; break; case "SpiritPickup": kind = "携带元神"; break; case "ThrowSpirit": kind = "投掷元神"; break; case "SwallowSpirit": kind = "吞下元神"; break;
                case "FormHealthAdjusted": kind = "形态生命等比例换算"; break;
                case "AwakeningAdded": kind = "施加唤灵"; break; case "AwakeningHeal": kind = "唤灵回复"; break;
                case "CurseAdded": kind = "起咒叠层"; break; case "CurseDetonated": kind = "咒言引爆"; break; case "CurseDamage": kind = "咒言伤害"; break; case "FormCounterDamage": kind = "攻形态反击"; break;
                case "ReinforcementOrigin": kind = "腐卒首选刷怪点"; break; case "PostBossWave": kind = "千面死亡后持续刷怪"; break;
                case "CorpseSummoned": kind = "召唤腐卒"; break; case "SummonShortfall": kind = "腐卒召唤缺额"; break;
                case "MaskCharge": kind = "千面蓄力"; break; case "MaskBlast": kind = "千面释放"; break; case "PhaseChanged": kind = "千面转阶段"; break;
                case "Stun": kind = "晕眩"; break; case "SkillCopied": kind = "复制技能"; break; case "ExtraAction": kind = "增加行动"; break; case "SkillCost": kind = "生命代价"; break;
                case "Attack": kind = "普攻"; break; case "Counter": kind = "反击"; break; case "Heal": kind = "治疗"; break; case "Reflection": kind = "反伤"; break; case "Death": kind = "阵亡"; break;
                case "TreeBurst": kind = "爆果覆盖"; break; case "SpillOverflow": kind = "泄雾溢出"; break; case "Move": kind = "移动步数"; break; case "ActionEnd": kind = "主要行动结束"; break;
                case "Talisman": kind = "符箓镇"; break; case "TalismanDamage": kind = "符箓伤害"; break; case "RetreatDamage": kind = "退去伤害"; break; case "MaskBlastDamage": kind = "千面技能伤害"; break;
                case "PoisonDamage": kind = "瘴毒掉血"; break; case "PoisonLayers": kind = "瘴毒层数"; break; case "SelfDamage": kind = "攻形态自伤"; break; case "ScarDamage": kind = "蚀痕代价"; break;
                case "Scar": kind = "蚀痕层数"; break; case "Purify": kind = "净行清雾"; break; case "Cleanse": kind = "涤尘"; break; case "Protect": kind = "保护转移"; break;
                case "AllyTeleported": kind = "符箓·引传送"; break; case "RetreatNoEffect": kind = "退去无有效目标"; break; case "ReflectReady": kind = "准备反伤"; break;
                case "StanceQueued": kind = "安排下回合姿态"; break; case "StanceCopied": kind = "复制姿态安排"; break; case "Stance": kind = "姿态切换"; break;
                case "FreeSkillAction": kind = "免费技能完成"; break; case "FreeSpiritAction": kind = "免费元神操作完成"; break; case "FruitGrowth": kind = "受击催熟"; break;
                case "PhaseDefeated": kind = "击破千面阶段"; break; case "Patrol": kind = "巡逻移动"; break; case "SourcesCleared": kind = "全部雾源已除"; break;
                case "PlayerPhase": kind = "我方阶段"; break; case "EnemyPhase": kind = "敌方阶段"; break; case "RoundEnd": kind = "回合末"; break; case "GuardForm": kind = "切换守形态"; break; case "AttackForm": kind = "切换攻形态"; break;
                case "Victory": kind = "战斗胜利"; break; case "Timeout": kind = "超时失败"; break; case "Elimination": kind = "全灭失败"; break;
                default: kind = e.kind; break;
            }
            return "[" + e.sequence + "] 第" + e.round + "回合 " + (e.sourceId ?? "关卡") + " " + kind
                + (e.targetId == null ? "" : " → " + e.targetId) + " " + e.amount + " (" + e.cell.x + "," + e.cell.y + ")";
        }
        public static string Summary(ForestBattleState s)
        {
            var errors = Check(s);
            return "污染核对 " + (errors.Count == 0 ? "一致" : string.Join("；", errors))
                + "\n地面" + s.groundFog.Count + " + 携雾" + s.CarriedFogCount + " = 初始" + s.statistics.initialFogCount + " + 新增" + s.statistics.generatedFogCount + " - 清除" + s.statistics.clearedFogCount
                + "\n元神 地面" + s.spiritsById.Values.Count(a => a.locationKind == SpiritLocation.Ground) + " 携带" + s.spiritsById.Values.Count(a => a.locationKind == SpiritLocation.Carried)
                + " 已消耗" + s.spiritsById.Values.Count(a => a.locationKind == SpiritLocation.Consumed) + " 待掉落" + s.pendingDrops.Count;
        }
        public static string Markdown(ForestBattleState s, string strategy, string reason, ScenarioDefinition initial = null, uint initialSeed = 0)
        {
            var b = new StringBuilder("# 迷踪林战报\n\n");
            b.AppendLine("策略标记：" + strategy).AppendLine().AppendLine("记录原因：" + reason).AppendLine().AppendLine("战局结果：" + ForestFeedback.Outcome(s)).AppendLine();
            b.AppendLine(ForestFeedback.ResultSummary(s)).AppendLine().AppendLine(Summary(s)).AppendLine();
            b.AppendLine("实际累计秒数：" + s.statistics.elapsedSeconds.ToString("F2", CultureInfo.InvariantCulture)).AppendLine();
            b.AppendLine("场景：" + s.ScenarioId + "；回合上限：" + s.Rules.roundLimit + "；随机状态：" + s.randomState).AppendLine();
            if (initial != null)
            {
                b.AppendLine("初始随机种子：" + initialSeed).AppendLine();
                b.AppendLine("## 本局起始布置\n\n| 实例 | 模板 | 起点 |\n|---|---|---|");
                foreach (var spawn in initial.spawns) b.AppendLine("|" + spawn.instanceId + "|" + spawn.templateId + "|(" + spawn.cell.x + "," + spawn.cell.y + ")|");
                b.AppendLine("\n## 本局模板数值\n\n| 模板 | 生命 | 攻击 | 防御 | 移动 | 普攻射程 |\n|---|---|---|---|---|---|");
                foreach (var t in initial.unitTemplates) b.AppendLine("|" + t.templateId + "|" + t.maxHp + "|" + t.attack + "|" + t.defense + "|" + t.move + "|" + (t.hasBasicAttack ? t.basicRange.ToString(CultureInfo.InvariantCulture) : "无") + "|");
            }
            b.AppendLine("## 单位结果\n\n| 实例 | 生命 | 行动 | 伤害 | 受伤 | 清雾 | 蚀痕 | 曾濒死 |\n|---|---|---|---|---|---|---|---|");
            foreach (var u in s.unitsById.Values.OrderBy(u => u.instanceId, StringComparer.Ordinal)) b.AppendLine("|" + u.instanceId + "|" + u.currentHp + "/" + ForestRules.Stats(s, u).maxHp + "|" + u.statistics.actions + "|" + u.statistics.damageDealt + "|" + u.statistics.damageTaken + "|" + u.statistics.fogCleared + "|" + u.scarStacks + "|" + (u.everNearDeath ? "是" : "否") + "|");
            b.AppendLine("\n## 元神记录\n");
            foreach (var a in s.spiritsById.Values.OrderBy(a => a.spiritId, StringComparer.Ordinal)) b.AppendLine("- " + a.spiritId + "：" + a.locationKind + "，携带者 " + (a.carrierId ?? "无") + "，消耗者 " + (a.consumedBy ?? "无"));
            b.AppendLine("\n逐笔事件另存同名CSV。时长来自实际运行累计；未运行的策略及零秒记录不代表已完成实际时长验收。");
            return b.ToString();
        }
        static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        public static string Export(ForestBattleState s, string folder, string strategy, string reason, ScenarioDefinition initial = null, uint initialSeed = 0)
        {
            Directory.CreateDirectory(folder);
            string stem = Path.Combine(folder, "forest-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            File.WriteAllText(stem + ".md", Markdown(s, strategy, reason, initial, initialSeed), new UTF8Encoding(false));
            var b = new StringBuilder("sequence,revision,round,kind,source,target,amount,requestedAmount,x,y\n");
            foreach (var e in s.eventLog) b.AppendLine(string.Join(",", e.sequence, e.revision, e.round, Csv(e.kind), Csv(e.sourceId), Csv(e.targetId), e.amount, e.requestedAmount, e.cell.x, e.cell.y));
            File.WriteAllText(stem + ".csv", b.ToString(), new UTF8Encoding(false)); return stem + ".md";
        }
    }
}
