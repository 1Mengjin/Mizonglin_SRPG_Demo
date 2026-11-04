using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
#if ZILONG_STAGE_THREE_PLAYER
        [NonSerialized] // The reusable player scene serializes only the original stageTwo flag.
#endif
        public bool stageThree;
        bool battleEnded;
        int hazardPercent, combatEpoch;
        string resultReason;
        bool victory;
        bool VerifyCombatMode => Array.IndexOf(Environment.GetCommandLineArgs(),"-s3verify")>=0;
        string BookPath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(mapStore.Path),"unit-templates.json");
        void BeginCombat(LevelMap map)
        {
            FighterBook book;
            try { book=VerifyCombatMode ? FighterBook.Read(BookPath) : scenario != null ? scenario.CopyFighters() : FighterBook.Defaults(); }
            catch(Exception ex) { if(editing) EditorFeedback(ex.Message); else Notify(ex.Message); return; }
            State.Reset(map); book.Apply(State,map); hazardPercent=map.hazardPercent;
            resultReason=null; SetMode(false); Notify(State.hasGoal?"我方先行 十二回合内抵达目标":"迷踪林地块结构试玩");
            if (!VerifyCombatMode) StartCoroutine(BattleOpening());
        }
        void RefreshCombat()
        {
            RefreshOverheadHuds();
        }
        RectTransform CombatDialog(string title,Vector2 size)
        {
            modal=Box(canvas.transform,title,new Vector2(.5f,.5f),Vector2.zero,new Vector2(5000,5000),new Color(0,0,0,.74f)); modal.GetComponent<Image>().raycastTarget=true;
            var panel=Box(modal,"Panel",new Vector2(.5f,.5f),Vector2.zero,size,ink);
            Label(panel,"Title",title,new Vector2(.5f,1),new Vector2(0,-16),new Vector2(size.x-40,42),28,TextAnchor.MiddleCenter).color=gold;
            return panel;
        }
        void DialogActions(RectTransform panel,string left,UnityEngine.Events.UnityAction a,string right,UnityEngine.Events.UnityAction b)
        {
            Action(panel,left,Vector2.zero,new Vector2(40,22),new Vector2(185,55),new Color(.27f,.31f,.32f),a);
            Action(panel,right,new Vector2(1,0),new Vector2(-40,22),new Vector2(185,55),new Color(.18f,.44f,.38f),b);
        }
        IEnumerator PlayerAction(DemoUnit target)
        {
            if (ForestEnabled) { yield return ForestPlayerAction(ForestCommandFor(target == null ? Zilong.Forest.ActionKind.Wait : Zilong.Forest.ActionKind.BasicAttack, target)); yield break; }
            int epoch=combatEpoch;
            var actor=State.selected;
            if(busy || State.phase!=SelectionPhase.Preview || actor==null || actor.acted) yield break;
            if(target!=null && !CombatRules.CanHit(actor,State.preview,target)) yield break;
            busy=true; Refresh();
            // Allow the movement preview to reach its destination before the hit.
            while(walkingIndex<walkingPath.Count) yield return null;
            actor.cell=State.preview; State.Clear(); Refresh();
            yield return ResolveAction(actor,target);
            if(epoch!=combatEpoch || battleEnded) yield break;
            if(State.units.All(u=>u.enemy || u.acted)) { yield return EnemyRound(); yield break; }
            busy=false; Refresh();
        }
        IEnumerator ResolveAction(DemoUnit actor,DemoUnit target)
        {
            actor.acted=true;
            if(target!=null && CombatRules.CanHit(actor,actor.cell,target))
            {
                yield return Impact(target,CombatRules.Damage(actor,target),"攻击");
                if(CombatRules.Counter(actor,target))
                {
                    yield return new WaitForSecondsRealtime(.30f);
                    yield return Impact(actor,CombatRules.Damage(target,actor),"反击");
                }
            }
            if(actor.hp>0 && State.hazards.Contains(actor.cell) && hazardPercent>0)
                yield return Impact(actor,CombatRules.Hazard(actor,hazardPercent),"危险地块");
            if(CombatRules.GoalReached(State,actor)) FinishBattle(true,actor.name+" 已抵达目标格");
            else if(State.units.All(u=>u.enemy)) FinishBattle(false,"我方单位全部阵亡");
            else Refresh();
        }
        IEnumerator Impact(DemoUnit victim,int damage,string cause)
        {
            var body=figures[victim].GetComponentsInChildren<Renderer>().First(r => r.name == "Whitebox body").sharedMaterial;
            Color original=body.color; body.color=Color.white;
            Vector3 screen=view.WorldToScreenPoint(World(victim.cell,1.7f));
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,screen,HudCamera,out var pos);
            var number=Label(canvas.transform,"Hit",cause+"  −"+damage,new Vector2(.5f,.5f),pos,new Vector2(240,54),25,TextAnchor.MiddleCenter);
            number.color=gold; CombatRules.Hurt(State,victim,damage); Refresh();
            float start=Time.unscaledTime;
            while(Time.unscaledTime-start<.24f) { number.rectTransform.anchoredPosition=pos+Vector2.up*((Time.unscaledTime-start)*64); yield return null; }
            Destroy(number.gameObject); body.color=original;
            if(victim.hp==0) { ResetFigures(); Refresh(); }
        }
        IEnumerator EnemyRound()
        {
            if (ForestEnabled) { yield return ForestRound(); yield break; }
            int epoch=combatEpoch;
            busy=true; State.Clear(); Refresh();
            foreach(var unit in State.units.Where(u=>!u.enemy&&!u.acted).OrderBy(u=>u.id,StringComparer.Ordinal).ToArray())
            {
                yield return ResolveAction(unit,null);
                if(epoch!=combatEpoch || battleEnded) yield break;
            }
            yield return ShowTurnTransition(true, State.turn);
            foreach(var enemy in State.units.Where(u=>u.enemy).OrderBy(u=>u.id,StringComparer.Ordinal).ToArray())
            {
                var plan=CombatRules.Choose(State,enemy);
                foreach(var step in plan.steps)
                {
                    enemy.cell=step; yield return new WaitForSecondsRealtime(.13f);
                }
                yield return ResolveAction(enemy,plan.target);
                if(epoch!=combatEpoch || battleEnded) yield break;
            }
            if(State.layout.version==1 && State.turn>=12) { FinishBattle(false,"第 12 回合结束，未抵达目标格"); yield break; }
            yield return ShowTurnTransition(false, State.turn + 1);
            State.NextTurn();
            if(State.turn==4 && State.layout.version==1)
            {
                int added=0;
                foreach(var p in new[]{new Vector2Int(3,4),new Vector2Int(3,5),new Vector2Int(3,6)})
                    if(!State.blocked.Contains(p) && State.hazards.Add(p)) added++;
                SyncMapVisuals(); Notify($"窄道异变 新增 {added} 格危险地面");
            }
            phaseBanner.text=""; busy=false; Refresh();
        }
        void FinishBattle(bool won,string reason)
        {
            CloseForestTutorial();
            HideStatusDetails();
            ClearAnnouncements();
            busy=true; battleEnded=true; victory=won; resultReason=reason; phaseBanner.text=""; State.Clear(); Refresh();
            var panel=CombatDialog(won?"战斗胜利":"战斗失败",ForestEnabled ? new Vector2(760,560) : new Vector2(620,300));
            if (won)
            {
                var title = panel.Find("Title").GetComponent<Text>();
                title.fontSize = ForestEnabled ? 56 : 44; title.fontStyle = FontStyle.Bold;
                title.rectTransform.sizeDelta = new Vector2(panel.sizeDelta.x - 40, ForestEnabled ? 90 : 64);
                title.rectTransform.anchoredPosition = new Vector2(0, ForestEnabled ? -28 : -14);
            }
            string resultText = ForestEnabled
                ? (won ? ForestVictorySummary() : reason + "\n" + ForestFeedbackSummary())
                : $"第 {State.turn} 回合\n{reason}";
            Label(panel,"Result",resultText,new Vector2(.5f,.5f),Vector2.zero,ForestEnabled ? new Vector2(700,285) : new Vector2(550,110),ForestEnabled ? 22 : 25,TextAnchor.MiddleCenter);
            if (ForestEnabled) Action(panel,"查看战斗记录",new Vector2(.5f,0),new Vector2(0,90),new Vector2(230,44),new Color(.18f,.30f,.30f),ShowForestLedger);
            DialogActions(panel,VerifyCombatMode ? "返回编辑" : "退出战斗",()=>{
                if (VerifyCombatMode) { CloseModal(); busy=false; SetMode(true); return; }
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            },"重新挑战",()=>{
                CloseModal(); busy=false; StartPlay();
                if(battleEnded) Notify("重新开局失败，请检查关卡配置。");
            });
        }
        static string PolicyName(EnemyPolicy policy)=>new[]{"站桩","追击","守卫"}[(int)policy];
    }
}
