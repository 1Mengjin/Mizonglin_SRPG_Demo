using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Mizonglin.Demo;
using Mizonglin.Forest;

public sealed class TextClarityPlayChecks : MonoBehaviour
{
    BattleDemo demo; string report = "";
    object Call(string name, params object[] args) => typeof(BattleDemo).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(demo, args);
    T Field<T>(string name) => (T)typeof(BattleDemo).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(demo);
    void Check(bool ok, string why) { if (!ok) throw new Exception(why); report += "PASS " + why + "\n"; }
    void Start()
    {
        demo = FindObjectOfType<BattleDemo>(); Directory.CreateDirectory("Artifacts");
        Field<ForestTutorialGuide>("tutorialGuide").MarkShown(ForestTutorialTopic.Fog | ForestTutorialTopic.Mask);
        Call("CloseForestTutorial"); StartCoroutine(Drive(Verify()));
    }
    IEnumerator Drive(IEnumerator test)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(test); float deadline = Time.realtimeSinceStartup + 45;
        while (stack.Count > 0)
        {
            object next = null;
            try
            {
                if (Time.realtimeSinceStartup > deadline) throw new Exception("Text checks timed out");
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                next = stack.Peek().Current;
            }
            catch (Exception ex)
            {
                File.WriteAllText("Artifacts/text-clarity-verification.txt", report + "FAIL " + ex);
                Debug.LogException(ex); UnityEditor.EditorApplication.Exit(1); yield break;
            }
            if (next is IEnumerator nested) stack.Push(nested); else yield return next;
        }
        File.WriteAllText("Artifacts/text-clarity-verification.txt", report + "PASS actual Unity double-density text rendering with original geometry and display settings.\n");
        UnityEditor.EditorApplication.Exit(0);
    }
    IEnumerator Verify()
    {
        yield return null; while (Field<bool>("busy")) yield return null;
        var unit = demo.ForestSession.State.unitsById["C03"];
        for (int i = 0; i < 4; i++) unit.curseSources.Add("C01");
        demo.State.SyncForest(); var viewUnit = demo.State.units.First(u => u.id == unit.instanceId); demo.ClickCell(viewUnit.cell);
        int index = Field<List<ForestStatusEntry>>("statusEntries").FindIndex(e => e.id == "curse"); Call("ShowStatusDetails", index);
        var body = Field<RectTransform>("statusPopup").GetComponentsInChildren<SharpText>().Single(t => t.name == "Status effect text");
        var texts = FindObjectsOfType<SharpText>(); foreach (var text in texts) text.Detailed = false;
        Canvas.ForceUpdateCanvases();
        int width = Screen.width, height = Screen.height; var rect = body.rectTransform.rect; int fontSize = body.fontSize;
        float preferred = body.preferredHeight, scale = Field<Canvas>("canvas").scaleFactor;
        var vertices = body.canvasRenderer.GetMesh().vertices;
        Call("Capture", Path.GetFullPath("Artifacts/ui-17-text-before.png"));
        foreach (var text in texts) text.Detailed = true;
        Canvas.ForceUpdateCanvases();
        Check(body.UsingDetailedGlyphs, "Status Chinese text uses the double-density font glyphs");
        Check(vertices.SequenceEqual(body.canvasRenderer.GetMesh().vertices), "Every text vertex position remains unchanged");
        Check(body.fontSize == fontSize && body.rectTransform.rect == rect && Mathf.Approximately(body.preferredHeight, preferred), "Font size, wrapping bounds and preferred height remain unchanged");
        Check(Screen.width == width && Screen.height == height && Mathf.Approximately(Field<Canvas>("canvas").scaleFactor, scale), "Screen resolution and canvas scaling remain unchanged: " + width + "x" + height);
        Check(UnityEditor.PlayerSettings.defaultScreenWidth == 1920 && UnityEditor.PlayerSettings.defaultScreenHeight == 1080, "Original 1920x1080 player defaults are preserved");
        Check(texts.All(t => t.text.Length == 0 || t.font == null || t.font.dynamic), "Existing dynamic fonts and text components remain compatible");
        Call("Capture", Path.GetFullPath("Artifacts/ui-18-text-after.png"));
        report += "Detailed text components: " + texts.Count(t => t.UsingDetailedGlyphs) + "/" + texts.Length + "\n";
    }
}
