using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Mizonglin.Demo;
using Mizonglin.Forest;

[InitializeOnLoad]
public static class RewardIntegration
{
    const string Key = "RewardIntegration.Active";
    static double start, popupStart;
    static int step;
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(obj, args);
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    static RewardIntegration() { EditorApplication.update += Tick; }
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Artifacts");
            var d = ForestDefaults.Create(); var p = new Vector2Int(4,10); var from = new Vector2Int(3,10);
            d.spawns[0].cell = from;
            var session = new ForestSession(d); var s = session.State; ForestRules.BeginPlayerPhase(s);
            var c = new ForestCommand {actorId="C01", kind=ActionKind.Wait, path=new System.Collections.Generic.List<Vector2Int>{from,p}};
            Check(ForestRules.Preview(s,c,out _) != null && s.remainingRewards.Contains(p), "Preview consumed reward");
            var through = new ForestCommand {actorId="C01", kind=ActionKind.Wait, path=new System.Collections.Generic.List<Vector2Int>{from,p,new Vector2Int(4,11)}};
            // Use a detached test map without the unit occupying 4,11.
            var blocker = s.At(new Vector2Int(4,11)); if(blocker != null) blocker.cell = new Vector2Int(1,10);
            var pass = ForestRules.Preview(s,through,out var error);
            Check(pass != null && pass.remainingRewards.Contains(p), "Passing through consumed reward: " + error);
            int hp = s.unitsById["C01"].currentHp;
            Check(ForestRules.TryExecute(s,c,out error), error);
            Check(!s.remainingRewards.Contains(p) && s.eventLog.Count(e=>e.kind=="RewardCollected")==1 && s.unitsById["C01"].currentHp==hp, "Pickup state/effect");
            s.unitsById["C01"].acted=false;
            Check(ForestRules.TryExecute(s,new ForestCommand{actorId="C01",kind=ActionKind.Wait},out error),error);
            Check(s.eventLog.Count(e=>e.kind=="RewardCollected")==1,"Repeated pickup");
            session.Restart(); Check(session.State.remainingRewards.Contains(p),"Restart did not restore reward");
            var copy=d.Copy(); copy.map.rewardCells[0]=from; Check(d.map.rewardCells[0]==p,"Reward cells not deep copied");
            Check(ForestAuthoring.Paint(copy,p,true)==null && !copy.map.rewardCells.Contains(p),"Obstacle did not remove reward");
            copy.map.rewardCells=new[]{new Vector2Int(0,0)}; Check(copy.Validate()!=null,"Invalid reward accepted");
            Check(DemoSetup.GetForestScenario().definition.map.rewardCells.Contains(p),"Saved asset missing reward");
            EditorSceneManager.OpenScene(DemoSetup.ScenePath); DemoSetup.RefreshPreview();
            var demo=UnityEngine.Object.FindObjectOfType<BattleDemo>();
            Check(demo.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("奖励闪光")),"Preview missing sparkle");
            SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
        }
        catch(Exception ex){Fail(ex);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling) return;
        try
        {
            if(start==0)start=EditorApplication.timeSinceStartup;
            Check(EditorApplication.timeSinceStartup-start<75,"Play timeout at " + step);
            var demo=UnityEngine.Object.FindObjectOfType<BattleDemo>();if(demo?.ForestSession==null)return;
            var s=demo.ForestSession.State;
            if(step==0)
            {
                if(s.phase!=BattlePhase.Player || (bool)demo.GetType().GetField("busy",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(demo))return;
                s.unitsById["C01"].cell=new Vector2Int(3,10); demo.State.SyncForest(); Call(demo,"ResetFigures"); Call(demo,"Refresh");
                demo.ClickCell(new Vector2Int(3,10));demo.ClickCell(new Vector2Int(4,10));demo.Cancel();
                Check(s.remainingRewards.Contains(new Vector2Int(4,10)),"UI cancel consumed reward");
                demo.ClickCell(new Vector2Int(4,10));Call(demo,"CommitWait");step=1;
            }
            else if(step==1)
            {
                var popup=UnityEngine.Object.FindObjectsOfType<Text>().FirstOrDefault(t=>t.text=="获得奖励");if(popup==null)return;
                popupStart=EditorApplication.timeSinceStartup;
                Check(!s.remainingRewards.Contains(new Vector2Int(4,10)),"Actual action did not collect");step=2;
            }
            else if(step==2)
            {
                if(EditorApplication.timeSinceStartup-popupStart<.35)return;
                var popup=UnityEngine.Object.FindObjectsOfType<Text>().FirstOrDefault(t=>t.text=="获得奖励");Check(popup!=null,"Popup disappeared early");
                Call(demo,"Capture",Path.GetFullPath("Artifacts/reward-popup.png"));step=3;
            }
            else if(step==3)
            {
                if(EditorApplication.timeSinceStartup-popupStart<2.15)return;
                Check(!UnityEngine.Object.FindObjectsOfType<Text>().Any(t=>t.text=="获得奖励"),"Popup lasts longer than 2s");
                Call(demo,"BeginForestCombat");Check(demo.ForestSession.State.remainingRewards.Contains(new Vector2Int(4,10)),"UI restart failed");
                File.WriteAllText("Artifacts/reward-verification.txt","PASS: full runtime/editor compile; saved asset; preview sparkle; preview/cancel/pass-through isolation; endpoint pickup once; no attribute reward effect; deep copy; obstacle cleanup; invalid cell validation; actual Play movement/wait popup and 2s dismissal; restart restore.");
                SessionState.SetBool(Key,false);EditorApplication.Exit(0);
            }
        }catch(Exception ex){Fail(ex);}
    }
    static void Fail(Exception ex){SessionState.SetBool(Key,false);File.WriteAllText("Artifacts/reward-verification.txt","FAIL: "+ex);Debug.LogException(ex);EditorApplication.Exit(1);}
}
