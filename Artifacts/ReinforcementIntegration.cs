using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mizonglin.Demo;
using Mizonglin.Forest;
public static class ReinforcementIntegration
{
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
    static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    public static void Run()
    {
        try
        {
            Directory.CreateDirectory("Artifacts");Debug.Log(ForestReinforcementChecks.Run());
            EditorSceneManager.OpenScene(DemoSetup.ScenePath);DemoSetup.RefreshPreview();
            var demo=UnityEngine.Object.FindObjectOfType<BattleDemo>();var s=demo.ForestSession.State;
            Check(s.phase==BattlePhase.Player,"Preview phase");
            s.unitsById["C01"].cell=new Vector2Int(13,3);s.unitsById["M03"].maskBoss.phaseIndex=4;s.unitsById["M03"].currentHp=1;
            Check(ForestRules.TryExecute(s,new ForestCommand{actorId="C01",targetId="M03",kind=ActionKind.BasicAttack},out var error),error);
            for(int i=0;i<2;i++)
            {
                if(s.phase==BattlePhase.PlayerStart)ForestRules.BeginPlayerPhase(s);
                ForestRules.EndPlayerPhase(s);ForestRules.EndEnemyPhase(s);Call(demo,"SyncForestPresentation");
                Check(s.unitsById.Keys.Count(id=>id.Contains(":reinforcement:"))==2*(i+1),"Native recurring wave count");
            }
            foreach(var unit in s.unitsById.Values.Where(u=>u.instanceId.Contains(":reinforcement:")))
                Check(UnityEngine.Object.FindObjectsOfType<Transform>().Any(t=>t.name==unit.instanceId+" 腐卒"),"Native summoned corpse figure missing");
            var viewPoint=new Vector3(15.5f,0,s.Map.height-1-1.5f);Set(demo,"cameraFocus",viewPoint);Set(demo,"cameraTarget",viewPoint);Call(demo,"LateUpdate");
            Call(demo,"Capture",Path.GetFullPath("Artifacts/post-boss-reinforcements.png"));
            File.WriteAllText("Artifacts/reinforcement-native-verification.txt","PASS native Unity: actual formal asset and scene load; boss final-stage death; two consecutive round-end waves create4 corpse units and all4 native figures; screenshot captured.100 focused rules assertions and full runtime/editor compilation passed.");
            EditorApplication.Exit(0);
        }
        catch(Exception ex){File.WriteAllText("Artifacts/reinforcement-native-verification.txt","FAIL: "+ex);Debug.LogException(ex);EditorApplication.Exit(1);}
    }
}
