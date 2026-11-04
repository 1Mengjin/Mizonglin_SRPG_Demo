using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zilong.Demo;
using Zilong.Forest;

public sealed class LevelDesigner : EditorWindow
{
    enum Brush { 查看, 普通地块, 障碍地块, 我方出生点, 敌方出生点, 移动出生点, 删除出生点, 区域归属, 初始瘴气, 清除瘴气, 根须格, 巡逻点, 奖励地块, 删除奖励 }
    ForestScenario data;
    Brush brush;
    int tab, factionFilter;
    string selectedTemplate = "C01", movingId, message;
    Vector2 scroll, gridScroll;
    Vector2Int? inspectedCell;
    MapRegion region = MapRegion.中庭;
    bool paintScene;
    float cellSize = 48;

    [MenuItem("紫龙 Demo/关卡设计")]
    public static void Open()
    {
        var window = GetWindow<LevelDesigner>("关卡设计");
        window.minSize = new Vector2(720, 600); window.Show();
    }
    void OnEnable()
    {
        data = DemoSetup.GetForestScenario();
        SceneView.duringSceneGui += SceneGUI; Undo.undoRedoPerformed += OnUndo;
    }
    void OnDisable() { SceneView.duringSceneGui -= SceneGUI; Undo.undoRedoPerformed -= OnUndo; }
    void OnUndo() { Repaint(); SceneView.RepaintAll(); }
    void Changed() { EditorUtility.SetDirty(data); Repaint(); SceneView.RepaintAll(); }

    void OnGUI()
    {
        if (data == null) data = DemoSetup.GetForestScenario();
        if (data.definition == null) { EditorGUILayout.HelpBox("正式配置主体为空，请先修复配置。", MessageType.Error); return; }
        if (data.definition.map == null || data.definition.rules == null || data.definition.unitTemplates == null
            || data.definition.spawns == null || data.definition.unitTemplates.Any(t => t == null))
        { EditorGUILayout.HelpBox("配置主体缺失，请在Inspector修复配置后再编辑。", MessageType.Error); return; }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.LabelField("迷踪林 正式关卡配置", EditorStyles.boldLabel);
            if (GUILayout.Button("编辑技能与状态说明")) BattleTextEditor.Open();
            EditorGUILayout.HelpBox("4名角色 + 4类怪物，共8个模板。修改支持撤销；保存后，场景与Play使用同一份正式配置。", MessageType.Info);
            tab = GUILayout.Toolbar(tab, new[] { "地图与出生点", "单位模板", "关卡规则" });
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (tab == 0) DrawMap(); else if (tab == 1) DrawTemplates(); else DrawRules();
            EditorGUILayout.EndScrollView();
            string error = data.Validate();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("保存并刷新场景")) Save();
            if (GUILayout.Button("保存并开始试玩") && Save())
            {
                if (SceneManager.GetActiveScene().path != DemoSetup.ScenePath) DemoSetup.Open();
                if (SceneManager.GetActiveScene().path == DemoSetup.ScenePath) EditorApplication.isPlaying = true;
            }
            if (GUILayout.Button("定位配置资源")) { Selection.activeObject = data; EditorGUIUtility.PingObject(data); }
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawMap()
    {
        var d = data.definition; var map = d.map;
        brush = (Brush)EditorGUILayout.EnumPopup("画笔", brush);
        if (brush == Brush.我方出生点 || brush == Brush.敌方出生点)
        {
            var choices = d.unitTemplates.Where(t => t.faction == (brush == Brush.我方出生点 ? Faction.Ally : Faction.Enemy)).ToArray();
            if (choices.Length > 0)
            {
                int index = Math.Max(0, Array.FindIndex(choices, t => t.templateId == selectedTemplate));
                index = EditorGUILayout.Popup("放置模板", index, choices.Select(t => t.templateId + " " + t.name).ToArray());
                selectedTemplate = choices[index].templateId;
            }
        }
        if (brush == Brush.奖励地块 || brush == Brush.删除奖励)
            EditorGUILayout.HelpBox("奖励地块上方显示彩色闪光；我方确认移动并停留后拾取一次，仅显示2秒回弹提示。", MessageType.Info);
        if (brush == Brush.区域归属) region = (MapRegion)EditorGUILayout.EnumPopup("区域", region);
        EditorGUILayout.HelpBox("标记“刷”的四格为千面死亡后刷怪点：死亡当回合末随机生成2名腐卒，整局仅一波；被占用时转到最近可走空格。", MessageType.Info);
        paintScene = EditorGUILayout.Toggle("在Scene中绘制", paintScene);
        cellSize = EditorGUILayout.Slider("格子大小", cellSize, 36, 64);
        EditorGUILayout.LabelField($"{map.width}×{map.height} 左上(0,0) 根须 {map.rootCells.Length} 巡逻点 {map.patrolPoints.Length}");
        if (movingId != null) EditorGUILayout.HelpBox("已选 " + movingId + "，点击新的可走格。", MessageType.Info);
        gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.Height(Mathf.Min(540, map.height * cellSize + 28)));
        var style = new GUIStyle(GUI.skin.button) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
        for (int y = 0; y < map.height; y++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int x = 0; x < map.width; x++)
            {
                var p = new Vector2Int(x, y); int i = y * map.width + x;
                var spawn = d.spawns.FirstOrDefault(s => s.cell == p);
                Color color = map.Walkable(p) ? LevelMap.RegionColor((MapRegion)map.regions[i]) : Color.gray;
                if (map.initialFog[i]) color = new Color(.7f, .5f, .82f);
                string text = x + "," + y;
                if (ForestRules.IsPostBossSpawnCell(p)) text += " 刷";
                if (Array.IndexOf(map.rewardCells ?? new Vector2Int[0], p) >= 0) { text += " 奖"; color = new Color(1f, .76f, .35f); }
                if (Array.IndexOf(map.rootCells, p) >= 0) text += " 根";
                int patrol = Array.IndexOf(map.patrolPoints, p); if (patrol >= 0) text += " 巡" + (patrol + 1);
                if (spawn != null)
                {
                    var template = Array.Find(d.unitTemplates, t => t.templateId == spawn.templateId);
                    color = template.faction == Faction.Ally ? new Color(.35f, .85f, .73f) : new Color(.95f, .5f, .45f);
                    text += "\n" + template.name;
                }
                Color previous = GUI.backgroundColor; GUI.backgroundColor = color;
                if (GUILayout.Button(text, style, GUILayout.Width(cellSize + 8), GUILayout.Height(cellSize))) Apply(p);
                GUI.backgroundColor = previous;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        if (inspectedCell.HasValue)
        {
            var spawn = d.spawns.FirstOrDefault(s => s.cell == inspectedCell.Value);
            if (spawn != null)
            {
                EditorGUILayout.LabelField("出生实例", spawn.instanceId + " / " + spawn.templateId);
                if (GUILayout.Button("编辑此实例使用的模板")) { selectedTemplate = spawn.templateId; factionFilter = 2; tab = 1; }
            }
        }
        if (GUILayout.Button("恢复默认地图与出生布置（保留已编辑模板和规则）"))
        {
            Undo.RecordObject(data, "恢复正式地图与布置"); var defaults = ForestDefaults.Create();
            d.map = defaults.map; d.spawns = defaults.spawns; d.rootSourceInstanceId = defaults.rootSourceInstanceId;
            movingId = null; message = "已恢复默认地图与出生布置，可撤销；尚未保存。"; Changed();
        }
    }

    void DrawTemplates()
    {
        var d = data.definition;
        factionFilter = GUILayout.Toolbar(factionFilter, new[] { "我方角色（4）", "敌方怪物（4）", "全部模板（8）" });
        var choices = d.unitTemplates.Where(t => factionFilter == 2 || (int)t.faction == factionFilter).ToArray();
        if (choices.Length == 0) return;
        int index = Math.Max(0, Array.FindIndex(choices, t => t.templateId == selectedTemplate));
        index = EditorGUILayout.Popup("当前模板", index, choices.Select(t => t.templateId + " " + t.name).ToArray());
        var template = choices[index]; selectedTemplate = template.templateId;
        EditorGUILayout.LabelField("此模板的实例", string.Join("、", d.spawns.Where(s => s.templateId == selectedTemplate).Select(s => s.instanceId)));
        EditorGUILayout.HelpBox("这里编辑共用模板。位置属于出生实例；局内生命、瘴毒和技能消耗不会写回模板。", MessageType.Info);
        var serialized = new SerializedObject(data); serialized.Update();
        var property = serialized.FindProperty("definition.unitTemplates").GetArrayElementAtIndex(Array.IndexOf(d.unitTemplates, template));
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.LabelField("模板编号", template.templateId);
        DrawFields(property, "name", "名称", "profession", "职业", "portrait", "头像");
        var element = property.FindPropertyRelative("element");
        element.enumValueIndex = EditorGUILayout.Popup("属相（本期不结算克制）", element.enumValueIndex, new[] { "无", "冰", "火", "雷", "光", "暗" });
        EditorGUILayout.Space(); EditorGUILayout.LabelField("基础属性", EditorStyles.boldLabel);
        int previousHp = property.FindPropertyRelative("maxHp").intValue;
        DrawFields(property, "maxHp", template.behaviorKind == BehaviorKind.MaskBoss ? "每阶段最大生命" : "最大生命", "defense", "基础防御");
        if (template.behaviorKind == BehaviorKind.MaskBoss && previousHp != property.FindPropertyRelative("maxHp").intValue)
            property.FindPropertyRelative("maskBoss.phaseHp").intValue = property.FindPropertyRelative("maxHp").intValue;
        if (template.behaviorKind == BehaviorKind.Tree || template.behaviorKind == BehaviorKind.MaskBoss)
            EditorGUILayout.LabelField("移动预算", "0（固定站桩）");
        else if (template.behaviorKind == BehaviorKind.Devourer) EditorGUILayout.LabelField("巡逻移动", "每次1格（固定）");
        else DrawFields(property, "move", "移动预算");
        if (template.hasBasicAttack) DrawFields(property, "attack", "基础攻击", "basicRange", "普攻射程");
        else EditorGUILayout.HelpBox("此类怪物没有普攻与反击，攻击和射程固定为0。", MessageType.Info);
        EditorGUILayout.Space(); EditorGUILayout.LabelField("专用参数", EditorStyles.boldLabel);
        switch (template.behaviorKind)
        {
            case BehaviorKind.FuSheng: DrawFields(property.FindPropertyRelative("talisman"), "castRange", "符箓距离", "squareSize", "伤害及晕眩范围边长", "maxUses", "全关次数", "echoRange", "符箓·引传送距离"); EditorGUILayout.HelpBox("符箓镇造成150%有效攻击的范围伤害并晕眩一回合，不清雾。晕眩期间该单位无法行动（技能、普攻、移动、自爆均暂停），被击杀时不触发亡语。符箓·引CD3，将范围内另一名存活队友传送至符生身边上下左右四格中指定的可走空格。先选队友，再选落点。被动起咒：其他队友每移动1格获得1层咒言，上限8；传送按距离计层；受伤清空，每层对来源造成4%符生有效攻击力固定伤害。", MessageType.Info); break;
            case BehaviorKind.SuLing: DrawFields(property.FindPropertyRelative("support"), "healRange", "治疗距离", "healAmount", "治疗量", "cleanseRange", "涤尘距离", "cleanseCooldownTurns", "涤尘冷却回合"); EditorGUILayout.HelpBox("治疗和涤尘额外为目标施加1层唤灵，上限3。实际扣血后清空，每层回复50%素铃有效攻击力的生命。", MessageType.Info); break;
            case BehaviorKind.QingYu:
                DrawFields(property.FindPropertyRelative("flight"), "switchCooldownTurns", "姿态切换冷却回合");
                EditorGUILayout.HelpBox("疾行免费增加本回合一次行动（含移动），CD2。使用展翼/收翼不消耗行动次数，下回合开始自动切换。飞行与落地状态保持到下次技能切换；飞行时移动力+1，跨地形与单位，不能攻击，已有三层瘴毒仍掉血。", MessageType.Info); break;
            case BehaviorKind.AShuang: DrawFields(property.FindPropertyRelative("dualForm"), "cycle", "形态周期", "guardTurns", "每周期守形态回合数", "guardDefense", "守形态防御（加成前）", "attackMultiplier", "攻形态攻击倍率（加成前）", "attackDefense", "攻形态防御", "attackRange", "攻形态射程", "selfDamagePercent", "自伤占模板生命百分比"); EditorGUILayout.HelpBox("守形态移动力-1，防御额外+50%，保留相邻普攻50%攻击伤害；攻形态最大生命+50%、攻击额外+20%、受到伤害+30%，切换形态按生命比例换算，受外部伤害后反击120%攻击力减防御的伤害。两形态均护卫2格内队友。退去：守形态反伤下次外来伤害的90%；攻形态扣除30%有效最大生命，造成实付生命120%的伤害，支付后至少保留1生命。", MessageType.Info); break;
            case BehaviorKind.Tree: DrawFields(property.FindPropertyRelative("tree"), "firstBurstRound", "首爆回合", "burstInterval", "爆果间隔", "maxFruitLevel", "果级上限"); EditorGUILayout.HelpBox("已接入正式敌方行为和回合末结算。", MessageType.Info); break;
            case BehaviorKind.Devourer: DrawFields(property.FindPropertyRelative("devourer"), "absorbRadius", "吸雾半径", "absorbLimit", "每回合吸雾上限", "growthFogPerTier", "增长阈值（未启用）", "growthTierLimit", "增长档位上限（未启用）"); EditorGUILayout.LabelField("增长", "关闭"); EditorGUILayout.HelpBox("已接入正式敌方行为和回合末结算。", MessageType.Info); break;
            case BehaviorKind.MaskBoss: DrawFields(property.FindPropertyRelative("maskBoss"), "phaseCount", "阶段数", "dropSearchRadius", "元神掉落搜索半径"); EditorGUILayout.LabelField("每阶段生命", property.FindPropertyRelative("maxHp").intValue + "（随上方最大生命同步）"); EditorGUILayout.HelpBox("已接入四阶段和逐阶段掉落；每次阶段复活在周围8格随机召唤1名腐卒（仅可走空位，避开地面元神，空位不足按实际数量召唤），最终死亡当回合末在(15,1)(16,1)(15,2)(16,2)随机生成2名腐卒（整局仅一波），占用或障碍时选择最近可走空格，同距按y、x排序；无普攻，朝随机角色方向蓄力一回合，预警3×3区域后造成固定60伤害。", MessageType.Info); break;
            case BehaviorKind.Corpse: EditorGUILayout.HelpBox("已接入正式敌方行为和回合末结算。", MessageType.Info); break;
        }
        if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); Changed(); }
    }
    static void DrawFields(SerializedProperty root, params string[] pairs)
    {
        for (int i = 0; i < pairs.Length; i += 2)
            EditorGUILayout.PropertyField(root.FindPropertyRelative(pairs[i]), new GUIContent(pairs[i + 1]), true);
    }
    void DrawRules()
    {
        var serialized = new SerializedObject(data); serialized.Update();
        var root = serialized.FindProperty("definition.rules"); EditorGUI.BeginChangeCheck();
        DrawFields(root, "roundLimit", "正式关卡回合上限", "poisonMaxStacks", "普通瘴毒上限", "poisonAttackNumerator", "瘴毒攻击倍率分子", "poisonAttackDenominator", "瘴毒攻击倍率分母", "poisonMovePenalty", "两层起移动减少", "poisonDamagePercent", "三层掉血百分比", "nearDeathPercent", "濒死百分比", "rootEntryCost", "根须格进入费用");
        EditorGUILayout.Space(); EditorGUILayout.LabelField("元神与净行", EditorStyles.boldLabel);
        DrawFields(root, "spiritCarryLimit", "携带上限", "spiritsPerGroundCell", "每格元神上限", "spiritThrowRange", "投掷距离", "spiritClearRadius", "投掷清雾半径", "purificationCellsPerSpirit", "每颗净行格数", "purificationCellsPerScar", "每层蚀痕所需清雾格数", "scarHpPenalty", "每层蚀痕生命代价");
        if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); Changed(); }
        var treeIds = data.definition.spawns.Where(s => Array.Find(data.definition.unitTemplates, t => t.templateId == s.templateId)?.behaviorKind == BehaviorKind.Tree).Select(s => s.instanceId).ToArray();
        int current = Array.IndexOf(treeIds, data.definition.rootSourceInstanceId) + 1;
        int selected = EditorGUILayout.Popup("根须来源实例", current, new[] { "未设置" }.Concat(treeIds).ToArray());
        if (selected != current) { Undo.RecordObject(data, "修改根须来源"); data.definition.rootSourceInstanceId = selected == 0 ? "" : treeIds[selected - 1]; Changed(); }
    }

    void Apply(Vector2Int cell)
    {
        var d = data.definition; var map = d.map; inspectedCell = cell;
        var spawn = d.spawns.FirstOrDefault(s => s.cell == cell);
        if (brush == Brush.查看) { message = (Array.IndexOf(map.rewardCells ?? new Vector2Int[0], cell) >= 0 ? "奖励地块 " : "") + $"({cell.x},{cell.y}) {(MapRegion)map.regions[cell.y * map.width + cell.x]}" + (spawn == null ? "" : " " + spawn.instanceId + " / " + spawn.templateId); Repaint(); return; }
        Undo.RecordObject(data, "编辑正式关卡格子"); string error = null;
        switch (brush)
        {
            case Brush.普通地块: case Brush.障碍地块: error = ForestAuthoring.Paint(d, cell, brush == Brush.障碍地块); break;
            case Brush.我方出生点: case Brush.敌方出生点: error = ForestAuthoring.Place(d, selectedTemplate, cell); break;
            case Brush.移动出生点:
                if (movingId == null) { movingId = spawn?.instanceId; message = movingId == null ? "请先选择一个出生点。" : "已选 " + movingId; Repaint(); return; }
                var moving = d.spawns.FirstOrDefault(s => s.instanceId == movingId);
                error = moving == null ? "出生点不存在" : ForestAuthoring.Place(d, moving.templateId, cell, movingId);
                if (error == null) movingId = null; break;
            case Brush.奖励地块:
                if (!map.Walkable(cell)) error = "奖励地块必须位于可走格";
                else if (spawn != null) error = "请选择没有出生点的空闲地块";
                else map.rewardCells = (map.rewardCells ?? new Vector2Int[0]).Union(new[] { cell }).ToArray();
                break;
            case Brush.删除奖励:
                map.rewardCells = (map.rewardCells ?? new Vector2Int[0]).Where(p => p != cell).ToArray(); break;
            case Brush.删除出生点: error = spawn == null ? "该格没有出生点" : ForestAuthoring.DeleteSpawn(d, spawn.instanceId); break;
            default:
                if (!map.Walkable(cell)) { error = "请先将此格改为普通地块"; break; }
                int i = cell.y * map.width + cell.x;
                if (brush == Brush.区域归属) { if (region == MapRegion.密林) error = "可走格不能分配密林区域"; else map.regions[i] = (int)region; }
                if (brush == Brush.初始瘴气) map.initialFog[i] = true;
                if (brush == Brush.清除瘴气) map.initialFog[i] = false;
                if (brush == Brush.根须格) map.rootCells = Array.IndexOf(map.rootCells, cell) >= 0 ? map.rootCells.Where(p => p != cell).ToArray() : map.rootCells.Concat(new[] { cell }).ToArray();
                if (brush == Brush.巡逻点) map.patrolPoints = Array.IndexOf(map.patrolPoints, cell) >= 0 ? map.patrolPoints.Where(p => p != cell).ToArray() : map.patrolPoints.Concat(new[] { cell }).ToArray();
                break;
        }
        message = error ?? $"已修改 ({cell.x},{cell.y})，尚未保存。";
        if (error == null) Changed(); Repaint();
    }
    bool Save()
    {
        message = data.Validate(); if (message != null) return false;
        EditorUtility.SetDirty(data); AssetDatabase.SaveAssets(); DemoSetup.RefreshPreview();
        message = "正式配置已保存并刷新；Play将使用这8个模板。"; return true;
    }
    void SceneGUI(SceneView view)
    {
        if (!paintScene || EditorApplication.isPlayingOrWillChangePlaymode || data?.definition?.map == null
            || SceneManager.GetActiveScene().path != DemoSetup.ScenePath || brush == Brush.查看) return;
        var e = Event.current; if (e.alt) return;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        var ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
        var point = ray.GetPoint(distance); var map = data.definition.map;
        var cell = new Vector2Int(Mathf.RoundToInt(point.x), map.height - 1 - Mathf.RoundToInt(point.z));
        if (!map.Inside(cell)) return;
        Handles.color = Color.yellow; Handles.DrawWireCube(new Vector3(cell.x, .05f, map.height - 1 - cell.y), new Vector3(.95f, .1f, .95f));
        if (e.type == EventType.MouseDown && e.button == 0) { Apply(cell); e.Use(); }
        view.Repaint();
    }
}
