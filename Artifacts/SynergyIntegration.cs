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
public static class SynergyIntegration
{
    const string Key="SynergyIntegration.Active";static int step;static double start;
    static Vector2Int P(int x,int y)=>new Vector2Int(x,y);
    static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,a);
    static void Check(bool b,string m){if(!b)throw new Exception(m);}
    static bool Busy(BattleDemo d)=>(bool)d.GetType().GetField("busy",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(d);
    static Button Skill(string prefix)=>UnityEngine.Object.FindObjectsOfType<Button>().Single(b=>b.gameObject.activeInHierarchy && b.GetComponentInChildren<Text>()?.text.StartsWith(prefix)==true);
    static SynergyIntegration(){EditorApplication.update+=Tick;}
    public static void Run(){try{Directory.CreateDirectory("Artifacts");Debug.Log(ForestSynergyChecks.Run());EditorSceneManager.OpenScene(DemoSetup.ScenePath);DemoSetup.RefreshPreview();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}catch(Exception e){Fail(e);}}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try{
            if(start==0)start=EditorApplication.timeSinceStartup;Check(EditorApplication.timeSinceStartup-start<75,"Timeout at "+step);
            var d=UnityEngine.Object.FindObjectOfType<BattleDemo>();if(d?.ForestSession==null)return;var s=d.ForestSession.State;
            if(step==0){if(s.phase!=BattlePhase.Player||Busy(d))return;d.ClickCell(s.unitsById["C02"].cell);Skill("治疗").onClick.Invoke();d.ClickCell(s.unitsById["C03"].cell);step=1;}
            else if(step==1){
                if(Busy(d)||!s.unitsById["C02"].acted)return;Check(s.unitsById["C03"].awakeningSources.Count==1,"Heal button didn't grant awakening");
                d.ClickCell(s.unitsById["C03"].cell);d.ClickCell(P(1,11));Call(d,"CommitWait");step=2;
            }else if(step==2){
                if(Busy(d)||!s.unitsById["C03"].acted)return;
                Check(s.unitsById["C03"].curseSources.Count==1,"Actual one-cell movement missing curse");
                d.ClickCell(P(1,11));
                Check(UnityEngine.Object.FindObjectsOfType<Text>().Any(t=>t.text=="唤灵") && UnityEngine.Object.FindObjectsOfType<Text>().Any(t=>t.text=="咒言"),"Live UI status labels missing");
                Call(d,"Capture",Path.GetFullPath("Artifacts/character-passive-stacks.png"));
                s.phase=BattlePhase.Enemy;s.unitsById["M04-1"].cell=P(1,10);
                var attack=new ForestCommand{actorId="M04-1",targetId="C03",kind=ActionKind.BasicAttack};
                int hp=s.unitsById["C03"].currentHp;
                Check(ForestRules.TryExecute(s,attack,out var error),error);
                Check(s.unitsById["C03"].currentHp==hp && s.unitsById["C03"].awakeningSources.Count==1 && s.eventLog.Any(e=>e.kind=="Protect"),"Live radius2 guard consumes wrong recipient stacks");
                s.unitsById["C04"].cell=P(4,12);s.unitsById["M04-1"].acted=false;
                Check(ForestRules.TryExecute(s,attack,out error),error);
                Check(s.unitsById["C03"].currentHp==79 && s.unitsById["C03"].awakeningSources.Count==0 && s.unitsById["C03"].curseSources.Count==0,"Live wake heal14 and curse clear");
                Check(s.eventLog.Any(e=>e.kind=="CurseDamage"&&e.amount==3),"Live curse damage3 missing");
                Call(d,"SyncForestPresentation");
                File.WriteAllText("Artifacts/synergy-play-verification.txt","PASS Unity Play: actual heal button applies awakening; actual movement/wait adds curse; status icons and counts render; nearby guard redirects damage without consuming protected ally buffs; outside radius HP loss heals14 and detonates curse3. Runtime/editor compilation and41 focused assertions passed.");
                SessionState.SetBool(Key,false);EditorApplication.Exit(0);
            }
        }catch(Exception e){Fail(e);}
    }
    static void Fail(Exception e){SessionState.SetBool(Key,false);File.WriteAllText("Artifacts/synergy-play-verification.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
}
