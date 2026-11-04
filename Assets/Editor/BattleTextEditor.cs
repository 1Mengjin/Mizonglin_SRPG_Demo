using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zilong.Demo;

public sealed class BattleTextEditor : EditorWindow
{
    public const string AssetPath = "Assets/Resources/BattleTextCatalog.asset";
    BattleTextCatalog data;
    int selected, category;
    string search = "", message;
    Vector2 listScroll, contentScroll;
    [MenuItem("紫龙 Demo/技能与状态说明")]
    public static void Open()
    {
        var window = GetWindow<BattleTextEditor>("技能与状态说明");
        window.minSize = new Vector2(800, 580); window.Show();
    }
    void OnEnable() { data = AssetDatabase.LoadAssetAtPath<BattleTextCatalog>(AssetPath); Undo.undoRedoPerformed += Repaint; }
    void OnDisable() { Undo.undoRedoPerformed -= Repaint; }
    bool Visible(BattleTextEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.id)) return false;
        if (entry.id.StartsWith("status."))
        {
            string id = entry.id.Substring(7);
            if (id == "movement-preview" || id == "actions" || id == "guard-radius" || id == "incantation" || id == "stance.grounded" || id == "mask"
                || id.StartsWith("cd-") || id.StartsWith("uses-")) return false;
        }
        if (category == 1 && !entry.id.StartsWith("skill.") || category == 2 && !entry.id.StartsWith("status.")) return false;
        return string.IsNullOrWhiteSpace(search) || ((entry.label ?? "") + entry.id + entry.title).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    void OnGUI()
    {
        if (data == null) data = AssetDatabase.LoadAssetAtPath<BattleTextCatalog>(AssetPath);
        if (data == null) { EditorGUILayout.HelpBox("未找到技能说明配置：" + AssetPath, MessageType.Error); return; }
        EditorGUILayout.HelpBox("修改技能长按说明、状态详情及其显示名称。只改变文案；保存后重新打开说明即可看到修改。支持撤销。", MessageType.Info);
        category = GUILayout.Toolbar(category, new[] { "全部", "主动技能", "状态与被动" });
        search = EditorGUILayout.TextField("搜索名称", search);
        if (selected < 0 || selected >= data.entries.Count || !Visible(data.entries[selected])) selected = data.entries.FindIndex(Visible);
        EditorGUILayout.BeginHorizontal();
        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(245));
        for (int i = 0; i < data.entries.Count; i++)
        {
            var entry = data.entries[i]; if (!Visible(entry)) continue;
            if (GUILayout.Toggle(selected == i, entry.label, "Button", GUILayout.Height(29))) selected = i;
        }
        EditorGUILayout.EndScrollView();
        contentScroll = EditorGUILayout.BeginScrollView(contentScroll);
        if (selected >= 0 && selected < data.entries.Count)
        {
            selected = Mathf.Clamp(selected, 0, data.entries.Count - 1);
            var entry = data.entries[selected];
            EditorGUILayout.LabelField(entry.label, EditorStyles.boldLabel);
            var serialized = new SerializedObject(data); serialized.Update();
            var property = serialized.FindProperty("entries").GetArrayElementAtIndex(selected);
            EditorGUILayout.PropertyField(property.FindPropertyRelative("title"), new GUIContent("显示名称（可选）"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("description"), new GUIContent("效果描述"));
            serialized.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("名称或描述留空时，使用原有自动文案。输入 {默认说明} 可插入随当前状态变化的原说明；其他文字可直接修改。支持换行及 <b>加粗</b>、<color=#FFCC66>文字颜色</color>。", MessageType.None);
            EditorGUILayout.LabelField("可插入的变量", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel("{角色名}  {层数}  {回合}  {默认标题}  {默认说明}\n{符箓距离}  {符箓范围}  {传送距离}\n{治疗量}  {治疗距离}  {涤尘距离}  {涤尘冷却}  {姿态冷却}\n{投掷距离}  {清雾半径}  {净行额度}  {蚀痕间隔}  {蚀痕代价}", GUILayout.Height(96));
            EditorGUILayout.Space(); EditorGUILayout.LabelField("条目编号", entry.id);
            EditorGUILayout.LabelField("配置位置", AssetPath);
        }
        else EditorGUILayout.HelpBox("没有匹配的说明条目。", MessageType.Info);
        EditorGUILayout.EndScrollView(); EditorGUILayout.EndHorizontal();
        string error = data.Validate();
        if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("保存说明", GUILayout.Height(32)))
        {
                if (error == null) { EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data); message = "已保存。关闭并重新打开游戏中的说明即可查看。"; }
        }
        if (GUILayout.Button("定位配置资源", GUILayout.Height(32))) { Selection.activeObject = data; EditorGUIUtility.PingObject(data); }
        EditorGUILayout.EndHorizontal();
    }
}
