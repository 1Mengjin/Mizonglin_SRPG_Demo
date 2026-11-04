using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Zilong.Forest
{
    public static class ForestFeedback
    {
        public static string Clock(double seconds)
        { var t = TimeSpan.FromSeconds(Math.Max(0, seconds)); return ((int)t.TotalMinutes).ToString("00") + ":" + t.Seconds.ToString("00"); }
        public static void Tick(ForestBattleState state, double seconds)
        {
            if (state.result == BattleResult.InProgress && seconds > 0 && !double.IsInfinity(seconds) && !double.IsNaN(seconds)) state.statistics.elapsedSeconds += seconds;
        }
        // Same clock, second bucket: only the frames where the player had no control (animations, settlement, enemy phase).
        public static void TickBlocking(ForestBattleState state, double seconds, bool blocking, bool enemyPhase)
        {
            if (!blocking || state.result != BattleResult.InProgress || !(seconds > 0) || double.IsInfinity(seconds) || double.IsNaN(seconds)) return;
            state.statistics.presentationSeconds += seconds;
            if (enemyPhase) state.statistics.enemyPhaseSeconds += seconds;
        }
        static string Seconds(double value) => value.ToString("F1", CultureInfo.InvariantCulture) + "s";
        static string Share(double part, double total) => (total <= 0 ? 0 : part / total * 100).ToString("F1", CultureInfo.InvariantCulture) + "%";
        public static int MajorActionCount(ForestBattleState s) => s.unitsById.Values
            .Where(u => s.GetTemplate(u.templateId).faction == Faction.Ally).Sum(u => u.statistics.actions);
        // Run-report duration breakdown: what the player spent on decisions versus what the game spent blocking input.
        public static string DurationBreakdown(ForestBattleState s)
        {
            var statistics = s.statistics;
            double total = Math.Max(0, statistics.elapsedSeconds);
            double blocking = Math.Min(total, Math.Max(0, statistics.presentationSeconds));
            double enemy = Math.Min(blocking, Math.Max(0, statistics.enemyPhaseSeconds));
            double player = Math.Max(0, total - blocking), own = Math.Max(0, blocking - enemy);
            int rounds = Math.Max(1, s.round), actions = Math.Max(1, MajorActionCount(s));
            return "决策与操作 " + Seconds(player) + " 占" + Share(player, total)
                + " 每回合" + Seconds(player / rounds) + " 每次主要行动" + Seconds(player / actions)
                + "\n我方演出与结算 " + Seconds(own) + " 占" + Share(own, total)
                + "\n敌方阶段 " + Seconds(enemy) + " 占" + Share(enemy, total)
                + "\n阻塞合计 " + Seconds(blocking) + " 占" + Share(blocking, total)
                + "\n每回合阻塞 " + Seconds(blocking / rounds) + " 每次主要行动阻塞 " + Seconds(blocking / actions);
        }
        public static string AbilityName(string id)
        {
            switch (id)
            { case "talisman": return "符箓镇"; case "echo": return "符箓·引"; case "heal": return "治疗"; case "cleanse": return "涤尘"; case "stance": return "姿态"; case "sprint": return "疾行"; default: return id; }
        }
        public static string UnitSummary(ForestBattleState s, UnitState u)
        {
            var t = s.GetTemplate(u.templateId); var lines = new List<string>();
            if (!ForestRules.Alive(u)) return "已阵亡" + (u.devourer == null ? "" : " 保留携雾 " + u.devourer.carriedFogCount)
                + (s.statistics.sourceDefeatedRounds.TryGetValue(u.instanceId, out int defeated) ? " 第" + defeated + "回合除源" : "");
            if (u.flight != null) lines.Add((ForestRules.Flying(u) ? "飞行 移动力+1 禁止攻击和元神操作" : "落地") + (u.flight.switchRound > 0 ? " 第" + u.flight.switchRound + "回合切换为" + (u.flight.pendingStance == FlightStance.Flying ? "飞行" : "落地") : ""));
            if (u.dualForm != null)
            {
                int cycle = (s.round - 1) % t.dualForm.cycle;
                lines.Add(u.dualForm.form == DualForm.Guard ? "守形态 移动力-1 防御+50% 相邻普攻50% 护卫2格 " + " " + (t.dualForm.guardTurns - cycle) + "回合后转攻"
                    : "攻形态 生命+50% 攻击+20% 易伤+30% 受伤反击120% 护卫2格 行动后自伤" + ((long)t.maxHp * t.dualForm.selfDamagePercent + 99) / 100 + " " + (t.dualForm.cycle - cycle) + "回合后转守");
            }
            if (u.awakeningSources.Count > 0) lines.Add("唤灵 " + u.awakeningSources.Count + "/3 受伤后清空并回复");
            if (u.curseSources.Count > 0) lines.Add("咒言 " + u.curseSources.Count + "/8 受伤后引爆");
            if (t.behaviorKind == BehaviorKind.FuSheng) lines.Add("起咒：队友每移动1格叠1层咒言，传送按距离计层");
            if (u.tree != null) lines.Add("果级 " + u.tree.fruitLevel + "/" + t.tree.maxFruitLevel + " " + (u.tree.lastBurstRound == 0 ? "首次落地前不成长" : "每回合自动+1层") + " " + (u.tree.nextBurstRound <= s.round ? "本回合末待爆果" : "第" + u.tree.nextBurstRound + "回合末爆果") + " 受我方普攻立即落地并回退至1层" + (ForestRules.IsStunned(s, u) ? "（晕眩中暂停）" : ""));
            if (u.devourer != null) lines.Add("携雾 " + u.devourer.carriedFogCount + " 下个巡逻点 " + s.Map.patrolPoints[u.devourer.nextPatrolIndex]
                + " 第" + ForestRules.DevourerSelfDestructRound + "回合末自爆：半径" + ForestRules.DevourerSelfDestructRadius + "内" + ForestRules.DevourerSelfDestructDamage + "伤害并释放携雾");
            if (u.maskBoss != null)
            {
                lines.Add("阶段复活：周围8格随机空位召唤1名腐卒，空位不足按实际数量召唤；最终死亡当回合末在东北四格随机生成2名腐卒（整局仅一波），落点占用时就近选择空格");
                lines.Add("千面阶段 " + u.maskBoss.phaseIndex + "/" + t.maskBoss.phaseCount + " 已产元神 " + s.spiritsById.Values.Count(a => a.sourceInstanceId == u.instanceId));
                lines.Add(u.maskBoss.releaseRound > 0 ? "蓄力范围已锁定 " + (u.maskBoss.releaseRound <= s.round ? "下次行动释放60伤害" : "第" + u.maskBoss.releaseRound + "回合释放60伤害") : "下次行动蓄力1回合");
            }
            if (t.behaviorKind == BehaviorKind.Corpse) lines.Add("按最短路径追击 移动后普攻");
            string status = ForestRules.IsStunned(s, u) ? (t.faction == Faction.Ally ? "晕眩 本回合禁止技能" : "晕眩 本回合无法行动 被击杀时不触发亡语") : "";
            if (u.reflectPercent > 0) status += " 下次反伤" + u.reflectPercent + "%";
            if (u.currentHp <= ForestRules.NearDeathLine(s, u) && t.faction == Faction.Ally) status += " 濒死";
            if (status.Length > 0) lines.Add(status.Trim());
            if (t.faction == Faction.Ally)
            {
                lines.Add("主要行动余 " + (u.acted ? 0 : 1 + u.bonusActions) + (u.movementCommitted ? " 本次移动已确认" : ""));
                lines.Add("携带元神 " + (u.carriedSpiritId == null ? 0 : 1) + " 净行余 " + u.purificationCharges.Sum(c => s.Rules.purificationCellsPerSpirit - c.clearedCount) + "格");
                lines.Add(string.Join("  ", u.abilityStates.Select(a => AbilityName(a.abilityId) + (a.remainingUses >= 0 ? "余" + a.remainingUses + "次" : a.availableRound > s.round ? "CD" + (a.availableRound - s.round) : "可用"))));
            }
            return string.Join("\n", lines);
        }
        public static HashSet<Vector2Int> ThreatCells(ForestBattleState s, UnitState u)
        {
            var cells = new HashSet<Vector2Int>(); if (!ForestRules.Alive(u) || s.GetTemplate(u.templateId).faction != Faction.Enemy) return cells;
            if (u.maskBoss != null)
            {
                if (u.maskBoss.releaseRound > 0) return new HashSet<Vector2Int>(ForestRules.MaskWarningCells(s, u));
                foreach (var direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                    { var p = u.cell + direction * 2 + new Vector2Int(x, y); if (s.Map.Inside(p)) cells.Add(p); }
                return cells;
            }
            if (u.tree != null || u.devourer != null)
            {
                int radius = u.tree != null ? u.tree.fruitLevel : s.GetTemplate(u.templateId).devourer.absorbRadius;
                for (int y = 0; y < s.Map.height; y++) for (int x = 0; x < s.Map.width; x++)
                { var p = new Vector2Int(x, y); if (s.Map.Walkable(p) && ForestRules.Distance(u.cell, p) <= radius) cells.Add(p); }
                return cells;
            }
            foreach (var origin in ForestPathfinder.Search(s, u, ForestRules.Stats(s, u).move).costs.Keys)
                for (int y = 0; y < s.Map.height; y++) for (int x = 0; x < s.Map.width; x++)
                { var p = new Vector2Int(x, y); int distance = ForestRules.Distance(origin, p); if (distance > 0 && distance <= ForestRules.Stats(s, u).range && s.Map.Walkable(p)) cells.Add(p); }
            return cells;
        }
        public static string ThreatCaption(ForestBattleState s, UnitState u) => ForestRules.IsStunned(s, u) && (u.maskBoss != null || u.tree != null || u.devourer != null) ? "晕眩期间无法行动，显示解除后的作用范围"
            : u.maskBoss != null ? u.maskBoss.releaseRound > 0 ? "红色边框为已锁定蓄力区域" : "橙色为下一次蓄力可能覆盖的方向区域"
            : u.tree != null ? "橙色为当前果级的下次铺雾范围" : u.devourer != null ? "蓝色为回合末吸雾范围" : "橙色为本次移动后可能普攻覆盖的范围";
        public static bool IsHealing(string kind) => kind == "Heal" || kind == "AwakeningHeal";
        public static bool IsDamage(string kind) => kind == "Attack" || kind == "Counter" || kind.EndsWith("Damage", StringComparison.Ordinal) || kind == "Reflection" || kind == "SkillCost";
        public static string Outcome(ForestBattleState s) => s.result == BattleResult.InProgress ? "进行中" : s.result == BattleResult.Victory ? "胜利"
            : s.endReason == BattleEndReason.Timeout ? "超时失败" : "全灭失败";
        public static string PreviewSummary(ForestBattleState before, ForestBattleState after)
        {
            var events = after.eventLog.Skip(before.eventLog.Count).ToArray(); var pieces = new List<string>();
            foreach (var e in events.Where(e => IsDamage(e.kind) || IsHealing(e.kind) || e.kind == "Stun"))
            {
                string name = e.targetId != null && after.unitsById.TryGetValue(e.targetId, out var u) ? after.GetTemplate(u.templateId).name : e.targetId;
                pieces.Add(e.kind == "Stun" ? name + "禁技能1回合" : name + (IsHealing(e.kind) ? "+" : "-") + e.amount);
            }
            foreach (var e in events.Where(e => e.kind == "PhaseChanged")) pieces.Add("千面进入阶段" + e.amount);
            int summoned = events.Count(e => e.kind == "CorpseSummoned");
            if (summoned > 0) pieces.Add("召唤" + summoned + "名腐卒");
            foreach (var e in events.Where(e => e.kind == "AllyTeleported")) pieces.Add("传送队友至(" + e.cell.x + "," + e.cell.y + ")");
            int cleared = after.statistics.clearedFogCount - before.statistics.clearedFogCount;
            if (cleared > 0) pieces.Add("实际清雾" + cleared + "格");
            return pieces.Count == 0 ? "确认后生效，右键或Esc取消" : "效果预览 " + string.Join(" / ", pieces.Take(6)) + (pieces.Count > 6 ? " / 更多见战斗记录" : "");
        }
        public static string ResultSummary(ForestBattleState s) => "第" + s.round + "回合 用时" + Clock(s.statistics.elapsedSeconds)
            + "\n主要行动" + s.unitsById.Values.Where(u => s.GetTemplate(u.templateId).faction == Faction.Ally).Sum(u => u.statistics.actions)
            + "次 元神消耗" + s.spiritsById.Values.Count(a => a.locationKind == SpiritLocation.Consumed) + "/" + s.spiritsById.Count
            + "\n阵亡" + s.unitsById.Values.Count(u => s.GetTemplate(u.templateId).faction == Faction.Ally && !ForestRules.Alive(u))
            + "人 全程无濒死：" + (s.statistics.everNearDeath ? "否" : "是")
            + "\n除源回合 " + string.Join(" ", s.unitsById.Values.Where(u => u.tree != null).OrderBy(u => u.instanceId, StringComparer.Ordinal)
                .Select(u => u.instanceId + ":" + (s.statistics.sourceDefeatedRounds.TryGetValue(u.instanceId, out int r) ? r.ToString(CultureInfo.InvariantCulture) : "未除源")));
    }
}
