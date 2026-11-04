using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Mizonglin.Demo
{
    public sealed partial class BattleDemo
    {
        public bool stageTwo;
        enum MapTool { Inspect, Normal, Blocked, Hazard, Ally, Enemy, Move, Goal, DeleteSpawn, DeleteGoal }
        LevelMap draft;
        MapStore mapStore;
        bool editing, dirty;
        string startupError;
        MapTool tool;
        int allyTemplate, enemyTemplate=4;
        SpawnPoint movingSpawn;
        RectTransform editorPanel, filePanel;
        Button returnToEditor, templateButton;
        Text toolText, fileStatus, editorMessage;
        readonly Dictionary<MapTool,Button> toolButtons=new Dictionary<MapTool,Button>();

        void InitializeMap()
        {
            string path = Path.Combine(Application.persistentDataPath, "custom-map.json");
            if (VerifyCombatMode) path = Path.Combine(Application.dataPath, "../Artifacts/combat-check/map.json");
            mapStore=new MapStore(path); draft=LevelMap.Default();
            if(File.Exists(path))
            {
                if(mapStore.Load(out var saved,out var error)) draft=saved;
                else startupError=error+" 当前显示默认草稿，原文件未被覆盖。";
            }
            dirty=!File.Exists(path) || startupError!=null;
            State.Reset(draft);
        }
        void MakeEditorUI()
        {
            editorPanel=Box(canvas.transform,"Map brushes",new Vector2(0,1),new Vector2(28,-28),new Vector2(275,690),ink);
            editorPanel.GetComponent<Image>().raycastTarget=true;
            Label(editorPanel,"Title","关卡模板",new Vector2(0,1),new Vector2(18,-16),new Vector2(240,36),27,TextAnchor.MiddleLeft);
            Label(editorPanel,"Subtitle",$"{State.Width}×{State.Height} 逐格修改",new Vector2(0,1),new Vector2(18,-57),new Vector2(240,28),18,TextAnchor.MiddleLeft);
            string[] titles={"查看 / 取消","普通地块","障碍地块","危险地块","我方出生点","敌方出生点","移动出生点","放置目标","删除出生点","删除目标"};
            for(int i=0;i<titles.Length;i++)
            {
                var t=(MapTool)i;
                var button=Action(editorPanel,titles[i],new Vector2(0,1),new Vector2(15+(i%2)*126,-104-(i/2)*58),new Vector2(119,48),new Color(.23f,.29f,.30f),()=> { tool=t; movingSpawn=null; RefreshEditor(); PaintBoard(); });
                button.GetComponentInChildren<Text>().fontSize=18; toolButtons[t]=button;
            }
            templateButton=Action(editorPanel,"",new Vector2(0,1),new Vector2(15,-408),new Vector2(245,49),new Color(.20f,.38f,.34f),()=>
            { if(tool==MapTool.Ally) allyTemplate=(allyTemplate+1)%4; else if(tool==MapTool.Enemy) enemyTemplate=4+(enemyTemplate-4+1)%3; RefreshEditor(); });
            toolText=Label(editorPanel,"Tool details","",new Vector2(0,1),new Vector2(18,-475),new Vector2(240,195),18,TextAnchor.UpperLeft);
            filePanel=Box(canvas.transform,"Map file actions",new Vector2(1,1),new Vector2(-28,-285),new Vector2(230,430),ink);
            filePanel.GetComponent<Image>().raycastTarget=true;
            fileStatus=Label(filePanel,"Save status","",new Vector2(.5f,1),new Vector2(0,-12),new Vector2(210,35),19,TextAnchor.MiddleCenter);
            EditorButton("保存当前配置",58,SaveMap);
            EditorButton("读取配置",118,()=>ConfirmDraftReplacement("读取已保存配置？",()=>LoadMap()));
            EditorButton("恢复默认模板",178,()=>ConfirmDraftReplacement("恢复默认模板？",RestoreDefault));
            EditorButton("开始试玩",238,StartPlay);
            editorMessage=Label(filePanel,"Persistent feedback","选择画笔后点击格子。",new Vector2(0,0),new Vector2(14,12),new Vector2(202,110),17,TextAnchor.UpperLeft);
            returnToEditor=Action(canvas.transform,"返回编辑",new Vector2(1,1),new Vector2(-28,-285),new Vector2(210,56),ink,()=>SetMode(true));
            returnToEditor.GetComponentInChildren<Text>().fontSize=21;
            if(stageThree) MakeCombatEditor();
        }
        void EditorButton(string title,float y,UnityEngine.Events.UnityAction action)
        { var b=Action(filePanel,title,new Vector2(.5f,1),new Vector2(0,-y),new Vector2(202,48),new Color(.23f,.34f,.33f),action); b.GetComponentInChildren<Text>().fontSize=20; }
        void SetMode(bool edit)
        {
            if (!VerifyCombatMode) StopAllCoroutines();
            CloseForestTutorial();
            HideStatusDetails();
            ClearAnnouncements(); busy=false;
            combatEpoch++; battleEnded=false; phaseBanner.text="";
            editing=edit; movingSpawn=null; hover=null; walkingPath.Clear(); toast.text="";
            State.Clear(); if(edit) State.Reset(draft);
            if (editorPanel != null) editorPanel.gameObject.SetActive(edit);
            if (filePanel != null) filePanel.gameObject.SetActive(edit);
            if (returnToEditor != null) returnToEditor.gameObject.SetActive(!edit);
            foreach(var button in new[]{wait,cancel,endTurn,restart}) button.gameObject.SetActive(!edit);
            count.gameObject.SetActive(!edit);
            SyncMapVisuals(); ResetFigures(); ResetCamera(); Refresh();
        }
        void RefreshEditor()
        {
            if(!editing) return;
            fileStatus.text=dirty?"● 草稿尚未保存":"✓ 与已保存配置一致";
            fileStatus.color=dirty?gold:jade;
            foreach(var pair in toolButtons) pair.Value.GetComponent<Image>().color=pair.Key==tool?new Color(.22f,.53f,.46f):new Color(.23f,.29f,.30f);
            templateButton.interactable=tool==MapTool.Ally || tool==MapTool.Enemy;
            int template=tool==MapTool.Enemy?enemyTemplate:allyTemplate;
            templateButton.GetComponentInChildren<Text>().text=$"单位模板 {LevelMap.Templates[template]}  ›";
            string[] descriptions={"查看格子信息。\n右键 / Esc 退出当前画笔。","点击格子恢复普通地面。\n保留出生点与目标标记。","点击格子设置不可通行障碍。\n有标记的格子需先移开标记。","点击格子标记危险区域。\n试玩可通行，暂不扣血。","点击空格新增我方出生点。\n点击模板按钮切换 U01—U04。","点击空格新增敌方出生点。\n点击模板按钮切换 E01—E03。",movingSpawn==null?"先点击一个出生点，\n再点击新位置。":"已选 "+movingSpawn.instanceId+"，点击新位置。\n右键取消移动。","点击可通行格设置唯一目标。\n再次点击其他格即可移动。","点击格子删除出生点。\n地形和目标标记保持不变。","点击金色目标格删除目标。\n保存前需重新放置。"};
            string cell="指向棋盘查看坐标";
            if(hover.HasValue)
            {
                var p=hover.Value; var spawn=draft.SpawnAt(p);
                cell=$"坐标 ({p.x},{p.y}) {new[]{"普通","障碍","危险"}[(int)draft.At(p)]}";
                if(spawn!=null) cell+=$"\n出生点 {spawn.instanceId} / {spawn.templateId}";
                if(draft.hasGoal && draft.Goal==p) cell+="\n目标格";
            }
            if(stageThree) descriptions[(int)MapTool.Hazard]=$"在此结束行动，扣最大生命的 {draft.hazardPercent}%。";
            toolText.text=descriptions[(int)tool]+"\n\n"+cell;
        }
        void EditorFeedback(string message) { editorMessage.text=message; editorMessage.color=gold; }
        void EditCell(Vector2Int p)
        {
            if(!draft.Inside(p)) return;
            string error=null; bool changed=false;
            if(tool==MapTool.Normal || tool==MapTool.Blocked || tool==MapTool.Hazard)
                changed=draft.Paint(p,(Ground)((int)tool-1),out error);
            else if(tool==MapTool.Ally || tool==MapTool.Enemy)
                changed=draft.Place(p,LevelMap.Templates[tool==MapTool.Ally?allyTemplate:enemyTemplate],null,out error);
            else if(tool==MapTool.Move)
            {
                if(movingSpawn==null) { movingSpawn=draft.SpawnAt(p); if(movingSpawn==null) error=$"格 ({p.x},{p.y}) 没有出生点。"; }
                else { changed=draft.Place(p,movingSpawn.templateId,movingSpawn,out error); if(changed) movingSpawn=null; }
            }
            else if(tool==MapTool.Goal)
            {
                if(draft.At(p)==Ground.Blocked) error=$"格 ({p.x},{p.y}) 是障碍，目标必须可通行。";
                else { draft.hasGoal=true; draft.goalX=p.x; draft.goalY=p.y; changed=true; }
            }
            else if(tool==MapTool.DeleteSpawn) { var s=draft.SpawnAt(p); if(s!=null) { draft.spawns.Remove(s); changed=true; } }
            else if(tool==MapTool.DeleteGoal && draft.hasGoal && draft.Goal==p) { draft.hasGoal=false; changed=true; }
            if(changed)
            {
                dirty=true; State.Reset(draft); SyncMapVisuals(); ResetFigures(); ResetCamera(); Refresh();
                EditorFeedback($"已修改 ({p.x},{p.y})，保存后生效。");
            }
            else if(error!=null) EditorFeedback(error);
            RefreshEditor(); PaintBoard();
        }
        void SaveMap()
        {
            if(!mapStore.Save(draft,out var error)) { EditorFeedback(error); return; }
            dirty=false; RefreshEditor(); EditorFeedback("保存成功。配置已写入本机，下次打开会自动读取。");
        }
        bool LoadMap()
        {
            if(!mapStore.Load(out var loaded,out var error)) { EditorFeedback(error); return false; }
            draft=loaded; dirty=false; SetMode(true); EditorFeedback("读取成功，已恢复保存的地图。"); return true;
        }
        void RestoreDefault()
        { draft=LevelMap.Default(); dirty=true; SetMode(true); EditorFeedback("已恢复默认草稿。点击保存后覆盖本地配置。"); }
        void StartPlay()
        {
            if (ForestEnabled) { BeginForestCombat(); return; }
            if (!VerifyCombatMode)
            {
                var map = scenario != null ? scenario.map : LevelMap.Default();
                BeginCombat(map.Clone());
                return;
            }
            if(editing && dirty) { EditorFeedback("草稿尚未保存，请先保存当前配置再试玩。"); return; }
            if(!mapStore.Load(out var saved,out var error)) { if(editing) EditorFeedback(error); else Notify(error); return; }
            if(stageThree) { BeginCombat(saved); return; }
            State.Reset(saved); SetMode(false); Notify("已从保存配置开始试玩 危险格暂不扣血");
        }
        void ConfirmDraftReplacement(string title,UnityEngine.Events.UnityAction action)
        {
            modal=Box(canvas.transform,"Map confirmation",new Vector2(.5f,.5f),Vector2.zero,new Vector2(4000,4000),new Color(0,0,0,.65f)); modal.GetComponent<Image>().raycastTarget=true;
            var panel=Box(modal,"Dialog",new Vector2(.5f,.5f),Vector2.zero,new Vector2(560,240),ink);
            Label(panel,"Question",title+"\n<size=20>当前编辑草稿将被替换。\n本地文件仅在点击保存时改变。</size>",new Vector2(.5f,1),new Vector2(0,-22),new Vector2(520,130),27,TextAnchor.MiddleCenter);
            Action(panel,"取消",new Vector2(0,0),new Vector2(40,25),new Vector2(190,50),ink,CloseModal);
            Action(panel,"确认",new Vector2(1,0),new Vector2(-40,25),new Vector2(190,50),new Color(.20f,.45f,.40f),()=>{CloseModal(); action();});
        }

    }
}
