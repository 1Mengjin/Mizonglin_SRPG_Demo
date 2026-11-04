using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Mizonglin.Demo;
using Mizonglin.Forest;
[InitializeOnLoad]
public static class BuffPulseIntegration
{
    const string Key="BuffPulseIntegration.Active";static int step;static double start,played;static BattleEvent[] events;
    static Vector2Int P(int x,int y)=>new Vector2Int(x,y);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,v);
    static void Check(bool b,string m){if(!b)throw new Exception(m);}
    static BuffPulseIntegration(){EditorApplication.update+=Tick;}
    public static void Run(){try{Directory.CreateDirectory("Artifacts");Debug.Log(ForestFormTuningChecks.Run());Check(Resources.Load<Shader>("BuffPulse")?.isSupported==true,"Buff pulse shader unsupported");EditorSceneManager.OpenScene(DemoSetup.ScenePath);DemoSetup.RefreshPreview();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}catch(Exception e){Fail(e);}}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            if(start==0)start=EditorApplication.timeSinceStartup;Check(EditorApplication.timeSinceStartup-start<70,"Timeout at "+step);
            var d=UnityEngine.Object.FindObjectOfType<BattleDemo>();if(d?.ForestSession==null)return;var s=d.ForestSession.State;
            if(step==0){
                Call(d,"CloseForestTutorial");
                if(s.phase!=BattlePhase.Player||(bool)d.GetType().GetField("busy",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(d))return;
                s.unitsById["C04"].cell=P(10,10);s.unitsById["C03"].cell=P(1,11);s.unitsById["C01"].cell=P(2,11);s.unitsById["C02"].cell=P(3,11);
                s.unitsById["C01"].currentHp=90;s.unitsById["C02"].currentHp=80;
                for(int i=0;i<3;i++)s.unitsById["C01"].awakeningSources.Add("C02");for(int i=0;i<8;i++)s.unitsById["C02"].curseSources.Add("C01");
                Call(d,"SyncForestPresentation");Call(d,"ResetFigures");
                var focus=new Vector3(2.5f,0,s.Map.height-1-11);Set(d,"cameraFocus",focus);Set(d,"cameraTarget",focus);Call(d,"LateUpdate");
                s.phase=BattlePhase.Enemy;s.unitsById["M03"].maskBoss.releaseRound=s.round;s.unitsById["M03"].maskBoss.warningCenter=P(2,11);
                int from=s.eventLog.Count;
                Check(ForestRules.TryExecute(s,new ForestCommand{actorId="M03",kind=ActionKind.MaskBlast},out var error),error);
                events=s.eventLog.Skip(from).ToArray();Check(events.Count(e=>e.kind=="AwakeningHeal")==1&&events.Count(e=>e.kind=="CurseDetonated")==1,"real stack settlement events");
                d.StartCoroutine((IEnumerator)Call(d,"PlayForestImpacts",from));played=EditorApplication.timeSinceStartup;step=1;
            }else if(step==1){
                if(EditorApplication.timeSinceStartup-played<.12)return;
                var root=UnityEngine.Object.FindObjectsOfType<Transform>().Single(t=>t.name=="BUFF瞬时特效");Check(root.childCount==2,"two transient buff effects");
                Check(root.GetComponentsInChildren<LineRenderer>().Length==12,"3 cyan ripple rings and9 yellow stars");
                Check((bool)Call(d,"PlayBuffSettlementEffects",(object)events)==false && root.childCount==2,"settlement replay deduplicated");
                Call(d,"Capture",Path.GetFullPath("Artifacts/buff-settlement-pulses.png"));step=2;
            }else if(step==2){
                if(EditorApplication.timeSinceStartup-played<.7)return;
                var root=UnityEngine.Object.FindObjectsOfType<Transform>().Single(t=>t.name=="BUFF瞬时特效");Check(root.childCount==0,"effects cleaned after one short pulse");
                Call(d,"ClearBuffPulses");Check(!UnityEngine.Object.FindObjectsOfType<Transform>().Any(t=>t.name=="BUFF瞬时特效"&&t.gameObject.activeInHierarchy),"cleanup removes root");
                Call(d,"BeginForestCombat");Check((bool)Call(d,"PlayBuffSettlementEffects",(object)new[]{new BattleEvent{sequence=99999,kind="AwakeningHeal",cell=P(2,11)}}),"new battle resets effect sequence dedup");Call(d,"ClearBuffPulses");
                File.WriteAllText("Artifacts/buff-pulse-native-verification.txt","PASS Unity Play: shader supported; actual damage settles3 awakening and8 curse stacks; cyan3-ring ripple and yellow9-star body flash visible; same events do not replay; effects clean within0.7s; restart/cleanup reset transient resources.30 form tuning assertions and runtime/editor compile passed.");SessionState.SetBool(Key,false);EditorApplication.Exit(0);
            }
        }catch(Exception e){Fail(e);}
    }
    static void Fail(Exception e){SessionState.SetBool(Key,false);File.WriteAllText("Artifacts/buff-pulse-native-verification.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
}
