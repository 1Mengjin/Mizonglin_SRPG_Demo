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
public static class BalanceIntegration
{
    const string Key="BalanceIntegration.Active";
    static int step;static double start;static int initialCount;
    static Vector2Int P(int x,int y)=>new Vector2Int(x,y);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);
    static void Check(bool b,string s){if(!b)throw new Exception(s);}
    static Button Skill(string prefix)=>UnityEngine.Object.FindObjectsOfType<Button>().Single(b=>b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text.StartsWith(prefix)==true);
    static BalanceIntegration(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        try{
            Directory.CreateDirectory("Artifacts");Debug.Log(ForestBalanceChecks.Run());
            var scenario=DemoSetup.GetForestScenario();Check(scenario.Validate()==null,"Formal configuration invalid");
            Check(scenario.definition.unitTemplates.First(t=>t.behaviorKind==BehaviorKind.FuSheng).talisman.echoRange==6,"Saved range6");
            var designer=EditorWindow.GetWindow<LevelDesigner>();designer.Show();designer.SendEvent(new Event{type=EventType.Layout});designer.Close();
            EditorSceneManager.OpenScene(DemoSetup.ScenePath);DemoSetup.RefreshPreview();
            SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }catch(Exception e){Fail(e);}
    }
    static bool Busy(BattleDemo d)=>(bool)d.GetType().GetField("busy",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(d);
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            if(start==0)start=EditorApplication.timeSinceStartup;Check(EditorApplication.timeSinceStartup-start<75,"Timed out at "+step);
            var d=UnityEngine.Object.FindObjectOfType<BattleDemo>();if(d?.ForestSession==null)return;var s=d.ForestSession.State;
            if(step==0){
                if(s.phase!=BattlePhase.Player || Busy(d))return;
                initialCount=s.unitsById.Count;
                d.ClickCell(s.unitsById["C01"].cell);Skill("符箓·引").onClick.Invoke();d.ClickCell(s.unitsById["C02"].cell);d.Cancel();
                Check(s.unitsById["C02"].cell==P(3,12) && !s.unitsById["C01"].acted && ForestRules.Ability(s.unitsById["C01"],"echo").availableRound==1,"Cancel consumed teleport");
                Skill("符箓·引").onClick.Invoke();d.ClickCell(s.unitsById["C02"].cell);
                d.ClickCell(P(2,11));Check(!s.unitsById["C01"].acted && !Busy(d),"Occupied destination consumed action");
                d.ClickCell(P(1,12));step=1;
            }else if(step==1){
                if(Busy(d)||!s.unitsById["C01"].acted)return;
                Check(s.unitsById["C02"].cell==P(1,12) && !s.unitsById["C02"].acted,"Actual two-step teleport");
                Check(ForestRules.Ability(s.unitsById["C01"],"echo").availableRound==4,"CD3 in Play");
                var bird=s.unitsById["C03"];int grounded=ForestRules.Stats(s,bird).move;bird.flight.stance=FlightStance.Flying;
                Call(d,"SyncForestPresentation");Check(d.State.units.Single(u=>u.id=="C03").move==grounded+1,"Flight bonus reaches UI");
                Call(d,"Capture",Path.GetFullPath("Artifacts/ally-teleport-play.png"));
                s.unitsById["C01"].cell=P(13,3);s.unitsById["C01"].acted=false;s.unitsById["C01"].movementCommitted=false;s.unitsById["M03"].currentHp=1;
                Call(d,"SyncForestPresentation");Call(d,"ResetFigures");d.ClickCell(P(13,3));d.ClickCell(P(14,3));step=2;
            }else if(step==2){
                if(Busy(d)||!s.eventLog.Any(e=>e.kind=="CorpseSummoned"))return;
                Check(s.unitsById.Count==initialCount+2 && s.unitsById["M03"].maskBoss.phaseIndex==2,"Actual revival summon2");
                foreach(var e in s.eventLog.Where(e=>e.kind=="CorpseSummoned"))Check(d.GetComponentsInChildren<Transform>().Any(t=>t.name==e.targetId+" 腐卒") || UnityEngine.Object.FindObjectsOfType<Transform>().Any(t=>t.name==e.targetId+" 腐卒"),"Summon figure missing");
                Call(d,"Capture",Path.GetFullPath("Artifacts/boss-summon-play.png"));
                File.WriteAllText("Artifacts/balance-play-verification.txt","PASS native Unity: formal configuration/range6; level designer; actual teleport skill button, teammate then destination, cancel and occupied-cell protection, teammate action retained, CD3; flight+1 in live view; actual boss revival adds2 visible corpse figures.");
                SessionState.SetBool(Key,false);EditorApplication.Exit(0);
            }
        }catch(Exception e){Fail(e);}
    }
    static void Fail(Exception e){SessionState.SetBool(Key,false);File.WriteAllText("Artifacts/balance-play-verification.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
}
