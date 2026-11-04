using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zilong.Forest;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
        readonly ForestTutorialGuide tutorialGuide = new ForestTutorialGuide();
        RectTransform tutorialOverlay;
        readonly Color tutorialInk = new Color(.13f, .15f, .15f);

        void ResetForestTutorials() { CloseForestTutorial(); tutorialGuide.Reset(); }
        void CloseForestTutorial()
        {
            if (tutorialOverlay != null) { tutorialOverlay.gameObject.SetActive(false); ReleaseObject(tutorialOverlay.gameObject); }
            tutorialOverlay = null;
        }
        IEnumerator CheckForestTutorials(ForestCommand command = null)
        {
            if (!ForestEnabled || buildingPreview || !Application.isPlaying || battleEnded) yield break;
            var path = command?.path ?? (State.phase == SelectionPhase.Preview ? State.Path(State.preview) : null);
            var topics = tutorialGuide.Pending(forestSession.State, command?.actorId ?? State.selected?.id, command?.targetId, path);
            yield return PresentForestTutorials(topics);
        }
        IEnumerator PresentForestTutorials(ForestTutorialTopic topics)
        {
            topics &= ~tutorialGuide.Shown;
            if (!ForestEnabled || topics == ForestTutorialTopic.None || battleEnded) yield break;
            bool previousBusy = busy; int epoch = combatEpoch;
            busy = true; rightHeld = rightDragging = false; HideStatusDetails(); HideSkillDescription(); Refresh();
            foreach (var topic in new[] { ForestTutorialTopic.Fog, ForestTutorialTopic.Mask })
            {
                if ((topics & topic) == 0) continue;
                MakeForestTutorial(topic); tutorialGuide.MarkShown(topic);
                while (tutorialOverlay != null) yield return null;
            }
            if (epoch == combatEpoch && !battleEnded) { busy = previousBusy; Refresh(); }
        }
        Text TutorialText(Transform parent, string name, string value, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
        {
            var text = Label(parent, name, value, anchor, pos, size, fontSize, align); text.color = tutorialInk; return text;
        }
        void MakeForestTutorial(ForestTutorialTopic topic)
        {
            CloseForestTutorial();
            tutorialOverlay = Box(canvas.transform, "Forest tutorial overlay", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, .65f));
            tutorialOverlay.anchorMin = Vector2.zero; tutorialOverlay.anchorMax = Vector2.one;
            tutorialOverlay.offsetMin = tutorialOverlay.offsetMax = Vector2.zero; tutorialOverlay.GetComponent<Image>().raycastTarget = true;
            var panel = Box(tutorialOverlay, "Tutorial panel", new Vector2(.5f, .5f), Vector2.zero, new Vector2(1320, 720), new Color(.96f, .94f, .86f));
            panel.GetComponent<Image>().raycastTarget = true;
            var title = TutorialText(panel, "Tutorial title", ForestTutorialGuide.Title(topic), new Vector2(0, 1), new Vector2(32, -24), new Vector2(1120, 54), 36, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            Box(panel, "Header divider", new Vector2(0, 1), new Vector2(32, -94), new Vector2(1256, 2), new Color(.50f, .47f, .36f, .45f));
            var close = Action(panel, "×", Vector2.one, new Vector2(-20, -18), new Vector2(64, 54), new Color(.61f, .20f, .18f), CloseForestTutorial);
            close.name = "Close tutorial"; close.GetComponentInChildren<Text>().fontSize = 38;
            var illustration = Box(panel, "Tutorial illustration", new Vector2(0, 1), new Vector2(32, -118), new Vector2(420, 490), new Color(.86f, .85f, .77f));
            if (topic == ForestTutorialTopic.Fog) MakeFogTutorialDiagram(illustration);
            else MakeMaskTutorialDiagram(illustration);

            var viewport = Box(panel, "Tutorial text viewport", new Vector2(0, 1), new Vector2(488, -118), new Vector2(800, 490), Color.clear);
            viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            var body = TutorialText(viewport, "Tutorial body", ForestTutorialGuide.Body(topic, forestSession.State), new Vector2(0, 1), Vector2.zero, new Vector2(774, 490), 23, TextAnchor.UpperLeft);
            body.lineSpacing = 1.05f; body.verticalOverflow = VerticalWrapMode.Overflow;
            Canvas.ForceUpdateCanvases(); body.rectTransform.sizeDelta = new Vector2(774, Mathf.Max(490, body.preferredHeight + 12));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = body.rectTransform;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            var track = Box(viewport, "Scroll track", new Vector2(1, .5f), Vector2.zero, new Vector2(7, 490), new Color(0, 0, 0, .10f));
            var handle = Box(track, "Scroll handle", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(.36f, .38f, .32f, .65f));
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop; bar.handleRect = handle;
            bar.targetGraphic = handle.GetComponent<Image>(); handle.GetComponent<Image>().raycastTarget = true;
            scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            Action(panel, "我知道了", new Vector2(1, 0), new Vector2(-32, 24), new Vector2(206, 58), new Color(.18f, .36f, .31f), CloseForestTutorial);
            tutorialOverlay.SetAsLastSibling();
        }
        void MakeFogTutorialDiagram(RectTransform parent)
        {
            TutorialText(parent, "Fog illustration title", "瘴气地块", new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(380, 40), 28, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                Box(parent, "Fog diagram tile", new Vector2(.5f, 1), new Vector2((x - 1) * 96, -76 - y * 80), new Vector2(90, 74), (x == 1 || y == 1) ? new Color(.50f, .36f, .59f) : new Color(.53f, .65f, .51f));
            var unit = Disc(parent, "Grounded character", new Vector2(.5f, 1), new Vector2(0, -160), 62, new Color(.12f, .40f, .35f));
            Label(unit, "Character caption", "角色", new Vector2(.5f, .5f), Vector2.zero, new Vector2(60, 40), 22, TextAnchor.MiddleCenter);
            TutorialText(parent, "Poison progression", "回合末叠加瘴毒", new Vector2(.5f, 1), new Vector2(0, -326), new Vector2(380, 42), 26, TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                var card = Box(parent, "Poison tier " + (i + 1), new Vector2(.5f, 1), new Vector2((i - 1) * 128, -382), new Vector2(118, 78), new Color(.32f + i * .08f, .18f, .22f));
                Label(card, "Tier caption", (i + 1) + "层\n" + new[] { "降低攻击", "降低移动", "回合扣血" }[i], new Vector2(.5f, .5f), Vector2.zero, new Vector2(114, 72), 21, TextAnchor.MiddleCenter);
            }
        }
        void MakeMaskTutorialDiagram(RectTransform parent)
        {
            int phases = forestSession.State.unitsById.Values.Where(u => u.maskBoss != null).Select(u => forestSession.State.GetTemplate(u.templateId).maskBoss.phaseCount).DefaultIfEmpty(4).First();
            TutorialText(parent, "Mask illustration title", "千面  " + phases + "阶段", new Vector2(.5f, 1), new Vector2(0, -16), new Vector2(380, 40), 28, TextAnchor.MiddleCenter).fontStyle = FontStyle.Bold;
            var face = Box(parent, "Mask diagram", new Vector2(.5f, 1), new Vector2(0, -92), new Vector2(178, 144), new Color(.27f, .24f, .32f));
            Box(face, "Left eye", new Vector2(.5f, .5f), new Vector2(-43, 22), new Vector2(38, 12), gold);
            Box(face, "Right eye", new Vector2(.5f, .5f), new Vector2(43, 22), new Vector2(38, 12), gold);
            Box(face, "Mouth", new Vector2(.5f, .5f), new Vector2(0, -35), new Vector2(58, 9), gold);
            TutorialText(parent, "Spirit drop caption", "击破阶段  ↓  掉落元神", new Vector2(.5f, 1), new Vector2(0, -264), new Vector2(392, 44), 26, TextAnchor.MiddleCenter);
            var spirit = Disc(parent, "Spirit diagram", new Vector2(.5f, 1), new Vector2(0, -326), 72, new Color(.19f, .55f, .58f));
            Label(spirit, "Spirit caption", "元神", new Vector2(.5f, .5f), Vector2.zero, new Vector2(68, 42), 23, TextAnchor.MiddleCenter);
            TutorialText(parent, "Spirit choices", "吞下：净行清雾\n投掷：范围清雾", new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(392, 72), 25, TextAnchor.MiddleCenter);
        }
    }
}
