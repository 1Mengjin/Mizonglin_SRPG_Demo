using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zilong.Forest;

public sealed class ForestDataWindow : EditorWindow
{
    public const string AssetPath = "Assets/Data/ForestScenario.asset";
    ForestScenario scenario;
    ForestSession session;
    Vector2 scroll;
    string message;
    MessageType messageType;

    [MenuItem("紫龙 Demo/迷踪林/里程碑1数据与状态")]
    public static void Open()
    {
        var window = GetWindow<ForestDataWindow>("迷踪林数据");
        window.minSize = new Vector2(680, 480);
        window.Show();
    }
    void OnEnable() { scenario = AssetDatabase.LoadAssetAtPath<ForestScenario>(AssetPath); }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("里程碑1：检查正式配置、初始化和重开状态。此窗口不推进回合，也不执行战斗结算。", MessageType.Info);
        EditorGUI.BeginChangeCheck();
        var selected = (ForestScenario)EditorGUILayout.ObjectField("正式关卡配置", scenario, typeof(ForestScenario), false);
        if (EditorGUI.EndChangeCheck()) { scenario = selected; session = null; message = null; }
        if (scenario == null)
        {
            if (GUILayout.Button("创建或选择默认正式配置")) CreateOrSelect();
            return;
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("校验配置")) Execute(() => {
            var error = scenario.Validate();
            if (error != null) throw new InvalidOperationException(error);
            return "配置校验通过。";
        });
        if (GUILayout.Button("查看或编辑配置")) { Selection.activeObject = scenario; EditorGUIUtility.PingObject(scenario); }
        if (GUILayout.Button("初始化本局")) Execute(() => {
            session = scenario.CreateSession();
            return "已按当前配置初始化；本局持有独立快照。";
        });
        using (new EditorGUI.DisabledScope(session == null))
            if (GUILayout.Button("重开本局")) Execute(() => {
                session.Restart(); return "已从本局初始快照重开。若编辑过配置，请重新初始化以载入修改。";
            });
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("执行里程碑1验收（A01—A04）")) Execute(() => ForestMilestoneOneChecks.RunFor(scenario));
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (scenario.definition != null) DrawDefinition(scenario.definition);
        if (session != null) DrawState(session.State);
        EditorGUILayout.EndScrollView();
    }

    void Execute(Func<string> action)
    {
        try { message = action(); messageType = MessageType.Info; }
        catch (Exception ex) { message = ex.Message; messageType = MessageType.Error; Debug.LogException(ex); }
    }

    void CreateOrSelect()
    {
        scenario = AssetDatabase.LoadAssetAtPath<ForestScenario>(AssetPath);
        if (scenario == null)
        {
            scenario = CreateInstance<ForestScenario>();
            scenario.definition = ForestDefaults.Create();
            AssetDatabase.CreateAsset(scenario, AssetPath);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = scenario;
    }

    static void DrawDefinition(ScenarioDefinition definition)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("配置基础值", EditorStyles.boldLabel);
        if (definition.map != null)
            EditorGUILayout.LabelField($"地图 {definition.map.width}×{definition.map.height} 规则 {definition.rulesetId} 根须来源 {definition.rootSourceInstanceId}");
        EditorGUILayout.LabelField("模板 / 名称 / 属相", "生命 / 攻击 / 防御 / 移动 / 普攻射程");
        foreach (var template in definition.unitTemplates ?? Array.Empty<UnitTemplate>())
        {
            if (template == null) continue;
            string stats = template.hasBasicAttack
                ? $"{template.maxHp} / {template.attack} / {template.defense} / {template.move} / {template.basicRange}"
                : $"{template.maxHp} / 无攻击 / {template.defense} / {template.move} / 无射程";
            EditorGUILayout.LabelField($"{template.templateId} {template.name} {ElementName(template.element)}", stats);
        }
    }

    static string ElementName(Element element) => new[] { "无", "冰", "火", "雷", "光", "暗" }.ElementAtOrDefault((int)element) ?? "无效";

    static void DrawState(ForestBattleState state)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("独立局内状态", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"第 {state.round} 回合 我方阶段开始（尚未执行结算） 未分胜负");
        EditorGUILayout.LabelField($"存活雾源 {state.LivingSourceCount} 地面雾 {state.groundFog.Count} 携雾 {state.CarriedFogCount} 元神 {state.spiritsById.Count}");
        foreach (var unit in state.unitsById.Values.OrderBy(u => u.instanceId, StringComparer.Ordinal))
        {
            var template = state.GetTemplate(unit.templateId);
            string extra = unit.tree != null ? $"果级 {unit.tree.fruitLevel}，待爆回合 {unit.tree.nextBurstRound}"
                : unit.devourer != null ? $"携雾 {unit.devourer.carriedFogCount}，下一巡逻格 {state.Map.patrolPoints[unit.devourer.nextPatrolIndex]}"
                : unit.maskBoss != null ? $"阶段 {unit.maskBoss.phaseIndex}，已掉落标记 {unit.maskBoss.resolvedPhaseMask}"
                : unit.flight != null ? "初始落地；使用切换技能后下回合生效"
                : unit.dualForm != null ? "守形态，阶段开始时才建立保护"
                : unit.abilityStates.Count > 0 ? string.Join("；", unit.abilityStates.Select(a =>
                    a.abilityId == "talisman" ? $"符箓镇剩余 {a.remainingUses} 次" : $"{a.abilityId}可用回合 {a.availableRound}"))
                : "无专用状态";
            EditorGUILayout.LabelField($"{unit.instanceId} {template.name} ({unit.cell.x},{unit.cell.y})",
                $"生命 {unit.currentHp} 瘴毒 {unit.poisonStacks} 蚀痕 {unit.scarStacks}");
            EditorGUILayout.LabelField("    " + extra, EditorStyles.miniLabel);
        }
    }
}
