using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mizonglin.Forest;

namespace Mizonglin.Demo
{
    public sealed partial class BattleDemo
    {
        RectTransform announcementLayer, announcementContent;
        CanvasGroup announcementGroup;
        Image announcementShade;
        Font announcementFont;
        bool cinematicCamera;
        // 已播过完整版技能横框的 key（角色 id + 技能名）。只在一次战斗内有效，重开时由 BattleOpening 清空。
        readonly HashSet<string> presentedSkillCutIns = new HashSet<string>();

        Font LoadAnnouncementFont()
        {
            // The bundled calligraphic face keeps the round badges identical in the editor and the browser.
            var bundled = Resources.Load<Font>("Fonts/YanShiXiaXingKai");
            if (bundled != null) return bundled;
            return Font.CreateDynamicFontFromOSFont(new[] { "STXingkai", "KaiTi", "STKaiti", "Microsoft YaHei" }, 64);
        }
        void MakeAnnouncementUI()
        {
            announcementLayer = Box(canvas.transform, "Battle announcements", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, Color.clear);
            announcementLayer.anchorMin = Vector2.zero; announcementLayer.anchorMax = Vector2.one;
            announcementLayer.offsetMin = announcementLayer.offsetMax = Vector2.zero;
            announcementShade = announcementLayer.GetComponent<Image>(); announcementShade.raycastTarget = true;
            announcementGroup = announcementLayer.gameObject.AddComponent<CanvasGroup>();
            announcementFont = LoadAnnouncementFont();
            announcementLayer.gameObject.SetActive(false);
        }
        RectTransform BeginAnnouncement(string title, float shade = 0)
        {
            ClearAnnouncements(); HideSkillDescription(); HideStatusDetails();
            Canvas.ForceUpdateCanvases();
            announcementLayer.SetAsLastSibling(); announcementLayer.gameObject.SetActive(true);
            // Keep the transparent input blocker; only turn transitions dim the full scene.
            announcementShade.color = new Color(0, 0, 0, shade); announcementGroup.alpha = 1;
            announcementContent = Box(announcementLayer, title, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, Color.clear);
            return announcementContent;
        }
        void ClearAnnouncements()
        {
            if (announcementContent != null) { announcementContent.gameObject.SetActive(false); ReleaseObject(announcementContent.gameObject); }
            announcementContent = null; cinematicCamera = false;
            if (announcementLayer != null) announcementLayer.gameObject.SetActive(false);
        }
        RectTransform Ornament(Transform parent, string title, Vector2 position, Vector2 size, BattleAnnouncementGraphic.Motif motif, Color accent)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(BattleAnnouncementGraphic)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = position; rt.sizeDelta = size;
            var graphic = go.GetComponent<BattleAnnouncementGraphic>(); graphic.motif = motif; graphic.accent = accent; graphic.raycastTarget = false;
            return rt;
        }
        RectTransform AnnouncementPanel(Transform parent, string title, Vector2 position, Vector2 size)
        {
            // Use standard uGUI Images for both the fill and the four straight edges.
            var panel = Box(parent, title, new Vector2(.5f, .5f), position, size, new Color(0, 0, 0, .72f));
            var top = Box(panel, "Top border", new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 3), gold);
            top.anchorMin = new Vector2(0, 1); top.anchorMax = Vector2.one;
            var bottom = Box(panel, "Bottom border", new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 3), gold);
            bottom.anchorMin = Vector2.zero; bottom.anchorMax = new Vector2(1, 0);
            var left = Box(panel, "Left border", new Vector2(0, .5f), Vector2.zero, new Vector2(3, 0), gold);
            left.anchorMin = Vector2.zero; left.anchorMax = new Vector2(0, 1);
            var right = Box(panel, "Right border", new Vector2(1, .5f), Vector2.zero, new Vector2(3, 0), gold);
            right.anchorMin = new Vector2(1, 0); right.anchorMax = Vector2.one;
            return panel;
        }
        Text AnnouncementText(Transform parent, string title, string value, Vector2 pos, Vector2 size, int fontSize, Color color)
        {
            var text = Label(parent, title, value, new Vector2(.5f, .5f), pos, size, fontSize, TextAnchor.MiddleCenter);
            text.font = announcementFont; text.color = color; text.supportRichText = false;
            var outline = text.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0, 0, 0, .94f); outline.effectDistance = new Vector2(2, -2);
            return text;
        }
        static float EaseOut(float t) => 1 - Mathf.Pow(1 - Mathf.Clamp01(t), 3);
        static float EaseBack(float t)
        {
            t = Mathf.Clamp01(t) - 1;
            return 1 + 2.3f * t * t * t + 1.3f * t * t;
        }
        IEnumerator AnimateAnnouncement(float duration, System.Action<float> update)
        {
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < duration)
            {
                update(Mathf.Clamp01((Time.unscaledTime - started) / duration)); yield return null;
            }
            update(1);
        }
        IEnumerator BattleOpening()
        {
            // 新开一局（含重开）时清空技能横框分级记录，保证每个 (角色, 技能名) 的首次仍播完整版。
            presentedSkillCutIns.Clear();
            busy = true; Refresh();
            yield return ShowVictoryConditions();
            yield return ShowTurnTransition(false, State.turn);
            if (ForestEnabled) EnterForestPhase();
            if (ForestEnabled) yield return CheckForestTutorials();
            busy = false; Refresh();
        }
        IEnumerator ShowVictoryConditions()
        {
            var root = BeginAnnouncement("Victory conditions");
            float width = announcementLayer.rect.width, travel = width + 360;
            AnnouncementPanel(root, "Objective ribbon", Vector2.zero, new Vector2(width - 64, 154));
            float emblemX = -width * .5f + 225;
            AnnouncementText(root, "Objective title", "胜利\n条件", new Vector2(emblemX, 0), new Vector2(250, 132), 48, gold);
            string objective = ForestEnabled ? "清空当前场上\n全部瘴气" : State.hasGoal ? "十二回合内抵达目标格" : "敌方全灭";
            AnnouncementText(root, "Objective description", "◇  " + objective, new Vector2(160, 0), new Vector2(Mathf.Max(420, width - 650), 132), 33, new Color(.97f, .94f, .82f));
            yield return AnimateAnnouncement(BattlePacing.VictoryIn, t => root.anchoredPosition = new Vector2(Mathf.Lerp(travel, 0, EaseOut(t)), 0));
            yield return new WaitForSecondsRealtime(BattlePacing.VictoryHold);
            yield return AnimateAnnouncement(BattlePacing.VictoryOut, t => root.anchoredPosition = new Vector2(-travel * t * t * t, 0));
            ClearAnnouncements();
        }
        static string RoundNumeral(int round)
        {
            const string digits = "零一二三四五六七八九";
            if (round < 1 || round > 99) return round.ToString();
            if (round < 10) return digits[round].ToString();
            return (round < 20 ? "" : digits[round / 10].ToString()) + "十" + (round % 10 == 0 ? "" : digits[round % 10].ToString());
        }
        IEnumerator ShowTurnTransition(bool enemy, int round)
        {
            var root = BeginAnnouncement(enemy ? "Enemy turn transition" : "Player turn transition", .58f);
            Color tint = enemy ? new Color(.92f, .23f, .21f) : new Color(.05f, .90f, .71f);
            var seal = Ornament(root, "Turn seal", new Vector2(0, 112), Vector2.one * 320, BattleAnnouncementGraphic.Motif.Seal, tint);
            AnnouncementText(seal, "Faction character", enemy ? "阴" : "阳", new Vector2(-12, 6), new Vector2(264, 280), 194, tint);
            AnnouncementText(seal, "Formation", "之\n阵", new Vector2(106, -51), new Vector2(52, 108), 37, tint);
            AnnouncementText(root, "Faction caption", enemy ? "敌 方 回 合" : "我 方 回 合", new Vector2(0, -80), new Vector2(380, 45), 27, tint);
            string number = "第" + RoundNumeral(round) + "回合";
            var vertical = string.Join("\n", number.Select(c => c.ToString()));
            AnnouncementText(root, "Round number", vertical, new Vector2(0, -230), new Vector2(110, 246), number.Length > 4 ? 36 : 44, Color.white);
            yield return AnimateAnnouncement(BattlePacing.TurnSealIn, t => { root.localScale = Vector3.one * Mathf.Lerp(1.32f, 1, EaseOut(t)); announcementGroup.alpha = EaseOut(t); });
            yield return new WaitForSecondsRealtime(BattlePacing.TurnSealHold);
            yield return AnimateAnnouncement(BattlePacing.TurnSealOut, t => { root.localScale = Vector3.one * (1 + .10f * t); announcementGroup.alpha = 1 - t; });
            ClearAnnouncements();
        }
        static string SkillCaption(ActionKind kind, UnitState actor)
        {
            switch (kind)
            {
                case ActionKind.Talisman: return "符箓镇";
                case ActionKind.Echo: return "符箓·引";
                case ActionKind.Heal: return "治疗";
                case ActionKind.Cleanse: return "涤尘";
                case ActionKind.SwitchStance: return ForestRules.Flying(actor) ? "收翼" : "展翼";
                case ActionKind.Sprint: return "疾行";
                case ActionKind.Retreat: return "退去";
                case ActionKind.MaskCharge: return "千面蓄力";
                case ActionKind.MaskBlast: return "千面震击";
                case ActionKind.CarrySpirit: return "携带元神";
                case ActionKind.ThrowSpirit: return "投掷元神";
                case ActionKind.SwallowSpirit: return "吞下元神";
                default: return "技能";
            }
        }
        IEnumerator ShowSkillAnnouncement(ForestCommand command)
        {
            if (!ForestRules.IsSkill(command.kind)) yield break;
            var actor = State.units.FirstOrDefault(u => u.id == command.actorId);
            if (actor == null) yield break;
            string title = SkillCaption(command.kind, actor.forestUnit);
            if (actor.enemy) yield return ShowEnemySkill(actor, title);
            else yield return ShowSkillCutIn(actor, title);
        }
        IEnumerator ShowSkillCutIn(DemoUnit actor, string title)
        {
            // 分级：同一次战斗内同一 (角色, 技能名) 首次出现播完整版，其余走精简版。
            bool first = presentedSkillCutIns.Add((actor != null ? actor.id : "") + "|" + title);
            float inDuration = first ? BattlePacing.SkillInFirst : BattlePacing.SkillInRepeat;
            float holdDuration = first ? BattlePacing.SkillHoldFirst : BattlePacing.SkillHoldRepeat;
            float outDuration = first ? BattlePacing.SkillOutFirst : BattlePacing.SkillOutRepeat;
            var root = BeginAnnouncement("Player skill cut-in");
            float width = announcementLayer.rect.width, y = announcementLayer.rect.height * .18f;
            root.anchoredPosition = new Vector2(0, y);
            var ribbon = AnnouncementPanel(root, "Skill ribbon", Vector2.zero, new Vector2(width - 64, 178));
            var heading = AnnouncementPanel(root, "Skill name frame", new Vector2(190, 126), new Vector2(480, 64));
            AnnouncementText(heading, "Skill name", title, Vector2.zero, new Vector2(430, 62), 38, Color.white);
            var crop = Box(root, "Portrait crop", new Vector2(.5f, .5f), new Vector2(-width * .24f, 0), new Vector2(500, 170), Color.clear);
            crop.gameObject.AddComponent<RectMask2D>();
            var art = Box(crop, "Caster portrait", new Vector2(.5f, .5f), new Vector2(0, -30), Vector2.one * 410, Color.white);
            var portrait = art.GetComponent<Image>(); portrait.sprite = actor.stats?.portrait; portrait.preserveAspect = true;
            if (portrait.sprite == null) { portrait.color = Color.clear; AnnouncementText(crop, "Caster fallback", actor.name, Vector2.zero, new Vector2(440, 160), 65, jade); }
            AnnouncementText(ribbon, "Caster name", actor.name, new Vector2(width * .19f, 0), new Vector2(480, 90), 45, gold);
            Vector2 portraitRest = art.anchoredPosition;
            yield return AnimateAnnouncement(inDuration, t =>
            {
                ribbon.localScale = new Vector3(1, EaseOut(Mathf.Min(1, t * 1.8f)), 1);
                heading.localScale = Vector3.one * Mathf.Lerp(.94f, 1, EaseOut(t));
                art.anchoredPosition = portraitRest + Vector2.left * (470 * (1 - EaseBack(t)));
            });
            yield return new WaitForSecondsRealtime(holdDuration);
            yield return AnimateAnnouncement(outDuration, t => { root.anchoredPosition = new Vector2(-150 * t * t, y); announcementGroup.alpha = 1 - t; });
            ClearAnnouncements();
        }
        IEnumerator FocusEnemyForSkill(DemoUnit actor)
        {
            cinematicCamera = true; rightHeld = rightDragging = false;
            Vector3 target = figures.TryGetValue(actor, out var figure) ? figure.position : World(actor.cell);
            target.y = 0; target = ClampCameraFocus(target);
            Vector3 from = cameraFocus;
            yield return AnimateAnnouncement(BattlePacing.EnemySkillFocus, t => { cameraFocus = cameraTarget = Vector3.Lerp(from, target, t * t * (3 - 2 * t)); ApplyCamera(); });
        }
        IEnumerator ShowEnemySkill(DemoUnit actor, string title)
        {
            yield return FocusEnemyForSkill(actor);
            if (actor.forestUnit?.maskBoss != null) yield return PresentForestTutorials(ForestTutorialTopic.Mask);
            var root = BeginAnnouncement("Enemy skill notification"); cinematicCamera = true;
            float y = announcementLayer.rect.height * .35f;
            root.anchoredPosition = new Vector2(0, y);
            AnnouncementPanel(root, "Enemy skill frame", Vector2.zero, new Vector2(600, 112));
            var disc = Disc(root, "Skill icon border", new Vector2(.5f, .5f), new Vector2(-238, 0), 106, new Color(.10f, .12f, .13f));
            Ornament(disc, "Skill vortex", Vector2.zero, Vector2.one * 90, BattleAnnouncementGraphic.Motif.Vortex, new Color(.67f, .94f, .96f));
            AnnouncementText(root, "Enemy skill name", title, new Vector2(55, 18), new Vector2(410, 52), 36, Color.white);
            AnnouncementText(root, "Enemy caster", actor.name, new Vector2(55, -28), new Vector2(410, 30), 23, gold);
            yield return AnimateAnnouncement(BattlePacing.EnemySkillIn, t => { root.anchoredPosition = new Vector2(0, y + 28 * (1 - EaseOut(t))); announcementGroup.alpha = EaseOut(t); });
            yield return new WaitForSecondsRealtime(BattlePacing.EnemySkillHold);
            yield return AnimateAnnouncement(BattlePacing.EnemySkillOut, t => announcementGroup.alpha = 1 - t);
            ClearAnnouncements();
        }
        IEnumerator ShowRoundEndEnemySkills()
        {
            // Determine exactly which automatic skills will fire using the same atomic rule batch.
            // The live state, random state and event log remain untouched during the UI sequence.
            var forecast = forestSession.State.Copy(); int start = forecast.eventLog.Count;
            ForestRules.EndEnemyPhase(forecast);
            var skills = forecast.eventLog.Skip(start).Where(e => e.kind == "TreeBurst" || e.kind == "FogAbsorbed" || e.kind == "SelfDestruct")
                .GroupBy(e => new { e.sourceId, e.kind });
            foreach (var skill in skills)
            {
                var actor = State.units.FirstOrDefault(u => u.id == skill.Key.sourceId);
                if (actor != null) yield return ShowEnemySkill(actor, skill.Key.kind == "TreeBurst" ? "瘴果爆发"
                    : skill.Key.kind == "FogAbsorbed" ? "吞雾" : "自爆");
            }
        }
    }
}
