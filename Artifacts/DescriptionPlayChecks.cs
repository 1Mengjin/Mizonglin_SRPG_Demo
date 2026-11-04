using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zilong.Demo;
using Zilong.Forest;

public sealed class DescriptionPlayChecks : MonoBehaviour
{
    BattleDemo demo; string report = "";
    object Call(string method, params object[] args) => typeof(BattleDemo).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(demo, args);
    T Field<T>(string name) => (T)typeof(BattleDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(demo);
    void Check(bool value, string why) { if (!value) throw new Exception(why); report += "PASS " + why + "\n"; }
    void Start()
    {
        demo = FindObjectOfType<BattleDemo>(); Directory.CreateDirectory("Artifacts");
        Field<ForestTutorialGuide>("tutorialGuide").MarkShown(ForestTutorialTopic.Fog | ForestTutorialTopic.Mask);
        Call("CloseForestTutorial"); StartCoroutine(Drive(Verify()));
    }
    IEnumerator Drive(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine); float deadline = Time.realtimeSinceStartup + 40;
        while (stack.Count > 0)
        {
            object next = null;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("Description checks timed out");
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
            }
            catch (Exception ex)
            {
                File.WriteAllText("Artifacts/description-ui-verification.txt", report + "FAIL " + ex);
                Debug.LogException(ex); EditorApplication.Exit(1); yield break;
            }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        File.WriteAllText("Artifacts/description-ui-verification.txt", report + "PASS actual Unity description asset, editor window, save and live UI bindings.\n"); EditorApplication.Exit(0);
    }
    IEnumerator Verify()
    {
        yield return null;
        while (Field<bool>("busy")) yield return null;
        var data = Resources.Load<BattleTextCatalog>(BattleTextCatalog.ResourceName);
        Check(data != null && data.entries.Count == 46 && data.Validate() == null, "Shipped catalog imports and loads from Resources with 46 unique entries");
        var caster = demo.State.units.Single(u => u.forestUnit.templateId == "C01");
        demo.ClickCell(caster.cell); demo.ClickCell(caster.cell);
        Check(Field<List<ForestStatusEntry>>("statusEntries").Count == 0 && !Field<RectTransform>("statusBar").gameObject.activeSelf && demo.State.phase == SelectionPhase.Preview, "Movement preview does not add preview/action/skill/passive entries; empty effects section is hidden");
        Call("Capture", Path.GetFullPath("Artifacts/ui-15-clean-status-card.png")); demo.Cancel(); demo.Cancel();
        var healer = demo.State.units.Single(u => u.forestUnit.templateId == "C02");
        demo.ClickCell(healer.cell);
        var step = ForestPathfinder.Reachable(demo.ForestSession.State, healer.forestUnit).costs.Keys.First(p => ForestRules.Distance(p, healer.cell) == 1);
        demo.ClickCell(step);
        var predicted = Field<ForestBattleState>("forestMovementPreview");
        Check(predicted != null && predicted.unitsById[healer.id].curseSources.Count > 0 && healer.forestUnit.curseSources.Count == 0 && Field<List<ForestStatusEntry>>("statusEntries").Count == 0, "Predicted movement buffs are not shown as applied effects");
        demo.Cancel(); demo.Cancel();
        var entry = data.entries.Single(e => e.id == "status.reflect");
        string originalTitle = entry.title, originalDescription = entry.description;
        entry.title = "可编辑反伤"; entry.description = "{角色名}的反伤比例：{层数}。\n这段文字来自配置资源。";
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        Check(!EditorUtility.IsDirty(data) && File.ReadAllText("Assets/Resources/BattleTextCatalog.asset").Contains("status.reflect"), "Edited descriptions save to the native asset");
        var actor = demo.State.units.Single(u => u.forestUnit.dualForm != null);
        var actual = actor.forestUnit; actual.reflectPercent = 60; actual.poisonStacks = 1; actual.scarStacks = 1;
        actual.awakeningSources.Add("C02"); actual.curseSources.Add("C01"); demo.State.SyncForest();
        demo.ClickCell(actor.cell);
        var entries = Field<List<ForestStatusEntry>>("statusEntries"); int index = entries.FindIndex(e => e.id == "reflect");
        Check(new[] { "reflect", "poison", "scar", "awakening", "curse" }.All(id => entries.Any(e => e.id == id)) && !entries.Any(e => e.id == "actions" || e.id == "guard-radius" || e.id == "incantation" || e.id.StartsWith("cd-") || e.id.StartsWith("uses-")), "Applied effects remain visible; permanent descriptions and counters are absent");
        Call("Capture", Path.GetFullPath("Artifacts/ui-16-active-effects-card.png"));
        int eventCount = demo.ForestSession.State.eventLog.Count, hp = actor.hp;
        Call("ShowStatusDetails", index);
        var popup = Field<RectTransform>("statusPopup");
        Check(popup.GetComponentsInChildren<Text>().Any(t => t.text == "可编辑反伤  60%"), "Configured status title is displayed");
        Check(popup.GetComponentsInChildren<Text>().Any(t => t.text == actor.name + "的反伤比例：60%。\n这段文字来自配置资源。"), "Configured description expands current character and stack count");
        Call("Capture", Path.GetFullPath("Artifacts/ui-13-editable-status.png")); Call("HideStatusDetails");
        entry.description = "保存后的第二版反伤说明"; EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        Call("ShowStatusDetails", index);
        Check(Field<RectTransform>("statusPopup").GetComponentsInChildren<Text>().Any(t => t.text == entry.description), "Reopening status details reads newly saved wording without restarting battle");
        Call("HideStatusDetails"); demo.Cancel();

        demo.ClickCell(caster.cell);
        var skill = data.entries.Single(e => e.id == "skill.Talisman"); string originalSkillTitle = skill.title, originalSkillDescription = skill.description;
        skill.title = "可编辑符箓"; skill.description = "{角色名}：施法{符箓距离}格，范围{符箓范围}×{符箓范围}。";
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
        var button = Field<List<Button>>("forestSkillButtons")[0]; button.GetComponent<SkillHoldDescription>().show.Invoke();
        var tooltip = Field<RectTransform>("skillDescription");
        var template = demo.ForestSession.State.GetTemplate(caster.forestUnit.templateId);
        Check(tooltip.GetComponentsInChildren<Text>().Any(t => t.text == "可编辑符箓") && tooltip.GetComponentsInChildren<Text>().Any(t => t.text == caster.name + "：施法" + template.talisman.castRange + "格，范围" + template.talisman.squareSize + "×" + template.talisman.squareSize + "。"), "Skill long-press callback uses editable title, body and real template parameters");
        Call("Capture", Path.GetFullPath("Artifacts/ui-14-editable-skill.png")); Call("HideSkillDescription");
        Check(demo.ForestSession.State.eventLog.Count == eventCount && actor.hp == hp, "Editing and reading descriptions does not execute skills or modify battle state");
        entry.title = originalTitle; entry.description = originalDescription; skill.title = originalSkillTitle; skill.description = originalSkillDescription;
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
    }
}
