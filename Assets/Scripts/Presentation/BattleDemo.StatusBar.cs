using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mizonglin.Forest;

namespace Mizonglin.Demo
{
    public sealed partial class BattleDemo
    {
        sealed class StatusSlot
        {
            public Button button;
            public BattleStatusIcon icon;
            public Text count, caption;
        }
        RectTransform statusBar, statusPopup;
        RectTransform statusDivider;
        Text statusHeading, statusHelp;
        string statusOwnerId;
        List<ForestStatusEntry> statusEntries = new List<ForestStatusEntry>();
        readonly List<StatusSlot> statusSlots = new List<StatusSlot>();

        void MakeStatusBar()
        {
            statusSlots.Clear(); statusOwnerId = null;
            statusDivider = Box(selectionCard, "Status divider", new Vector2(0, 1), new Vector2(18, -238), new Vector2(514, 1), new Color(gold.r, gold.g, gold.b, .45f));
            statusHeading = Label(selectionCard, "Status heading", "状态", new Vector2(0, 1), new Vector2(18, -246), new Vector2(120, 26), 19, TextAnchor.MiddleLeft); statusHeading.color = gold;
            statusHelp = Label(selectionCard, "Status help", "点击查看效果", new Vector2(1, 1), new Vector2(-18, -246), new Vector2(200, 26), 18, TextAnchor.MiddleRight); statusHelp.color = new Color(.72f, .76f, .76f);
            statusBar = Box(selectionCard, "Status bar", new Vector2(0, 1), new Vector2(18, -282), new Vector2(514, 140), Color.clear);
        }
        void RefreshStatusBar(ForestBattleState battle, UnitState unit)
        {
            if (statusBar == null) return;
            if (statusOwnerId != unit.instanceId) HideStatusDetails();
            statusOwnerId = unit.instanceId; statusEntries = ForestStatusDisplay.Build(battle, unit);
            bool hasEffects = statusEntries.Count > 0;
            statusBar.gameObject.SetActive(hasEffects); statusDivider.gameObject.SetActive(hasEffects);
            statusHeading.gameObject.SetActive(hasEffects); statusHelp.gameObject.SetActive(hasEffects);
            int rows = Mathf.CeilToInt(statusEntries.Count / 8f);
            selectionCard.sizeDelta = new Vector2(550, hasEffects ? 282 + rows * 76 + 16 : 246);
            statusBar.sizeDelta = new Vector2(514, rows * 76);
            while (statusSlots.Count < statusEntries.Count)
            {
                int index = statusSlots.Count;
                var button = Action(statusBar, "", new Vector2(0, 1), Vector2.zero, new Vector2(58, 66), Color.black, () => ShowStatusDetails(index));
                button.name = "Status slot " + index;
                var caption = button.GetComponentInChildren<Text>();
                caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = caption.rectTransform.pivot = new Vector2(.5f, 0);
                caption.rectTransform.anchoredPosition = new Vector2(0, 3); caption.rectTransform.sizeDelta = new Vector2(58, 20);
                caption.fontSize = 16; caption.color = Color.white;
                var iconObject = new GameObject("Status icon", typeof(RectTransform), typeof(BattleStatusIcon)); iconObject.transform.SetParent(button.transform, false);
                var rt = (RectTransform)iconObject.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, 1);
                rt.anchoredPosition = new Vector2(0, -5); rt.sizeDelta = Vector2.one * 33;
                var icon = iconObject.GetComponent<BattleStatusIcon>(); icon.raycastTarget = false;
                var badge = Box(button.transform, "Stack background", new Vector2(1, 1), new Vector2(3, 3), new Vector2(31, 22), new Color(0, 0, 0, .96f));
                var countText = Label(badge, "Stack count", "", new Vector2(.5f, .5f), Vector2.zero, new Vector2(42, 23), 17, TextAnchor.MiddleCenter);
                countText.color = Color.white; countText.fontStyle = FontStyle.Bold;
                statusSlots.Add(new StatusSlot { button = button, icon = icon, count = countText, caption = caption });
            }
            for (int i = 0; i < statusSlots.Count; i++)
            {
                var slot = statusSlots[i]; slot.button.gameObject.SetActive(i < statusEntries.Count);
                if (i >= statusEntries.Count) continue;
                var entry = statusEntries[i];
                ((RectTransform)slot.button.transform).anchoredPosition = new Vector2(i % 8 * 65, -(i / 8) * 76);
                Color tint = entry.tone == StatusTone.Negative ? new Color(1, .42f, .35f) : entry.tone == StatusTone.Positive ? jade : gold;
                slot.icon.icon = entry.icon; slot.icon.color = tint; slot.icon.SetVerticesDirty();
                slot.button.GetComponent<Image>().color = new Color(tint.r * .16f, tint.g * .16f, tint.b * .16f, 1);
                slot.caption.text = StatusDescription(entry).title; slot.count.text = entry.count;
                slot.count.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(entry.count));
            }
        }
        void ShowStatusDetails(int index)
        {
            if (busy || index < 0 || index >= statusEntries.Count) return;
            HideStatusDetails(); var entry = statusEntries[index];
            var wording = StatusDescription(entry);
            statusPopup = Box(canvas.transform, "Status detail overlay", new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(0, 0, 0, .52f));
            statusPopup.anchorMin = Vector2.zero; statusPopup.anchorMax = Vector2.one; statusPopup.offsetMin = statusPopup.offsetMax = Vector2.zero;
            var dismiss = statusPopup.gameObject.AddComponent<Button>(); dismiss.targetGraphic = statusPopup.GetComponent<Image>(); dismiss.transition = Selectable.Transition.None;
            statusPopup.GetComponent<Image>().raycastTarget = true; dismiss.onClick.AddListener(HideStatusDetails);
            var panel = Box(statusPopup, "Status effect", new Vector2(.5f, .5f), Vector2.zero, new Vector2(640, 350), new Color(.025f, .035f, .04f, .98f));
            var title = Label(panel, "Status title", wording.title + (string.IsNullOrEmpty(entry.count) ? "" : "  " + entry.count), new Vector2(0, 1), new Vector2(28, -22), new Vector2(490, 44), 30, TextAnchor.MiddleLeft);
            title.color = gold; title.fontStyle = FontStyle.Bold;
            var text = Label(panel, "Status effect text", wording.description, new Vector2(0, 1), new Vector2(28, -86), new Vector2(584, 218), 23, TextAnchor.UpperLeft);
            Canvas.ForceUpdateCanvases();
            float height = Mathf.Max(350, text.preferredHeight + 126); panel.sizeDelta = new Vector2(640, height); text.rectTransform.sizeDelta = new Vector2(584, height - 112);
            Action(panel, "关闭", new Vector2(1, 1), new Vector2(-18, -22), new Vector2(92, 42), new Color(.16f, .23f, .24f), HideStatusDetails);
            statusPopup.SetAsLastSibling(); rightHeld = rightDragging = false;
        }
        void HideStatusDetails()
        {
            if (statusPopup != null) { statusPopup.gameObject.SetActive(false); ReleaseObject(statusPopup.gameObject); }
            statusPopup = null;
        }
    }
}
