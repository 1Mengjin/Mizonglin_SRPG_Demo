using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Mizonglin.Demo
{
    public sealed partial class BattleDemo
    {
        IEnumerator RunCombatChecks()
        {
            var stack=new Stack<IEnumerator>(); stack.Push(CombatScenarios()); string failure=null;
            while(stack.Count>0)
            {
                var it=stack.Peek(); object current=null;
                try { if(!it.MoveNext()) { stack.Pop(); continue; } current=it.Current; }
                catch(Exception ex) { failure=ex.ToString(); break; }
                if(current is IEnumerator child) stack.Push(child); else yield return current;
            }
            var result=failure??"PASS: configured stats and reach, selected-character HUD, direct basic attack, actual damage, counter, hazard, goal, elimination, timeout, AI, round-four event, restart and return to editor.";
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(mapStore.Path),"verification.txt"),result);
            Debug.Log(result); Application.Quit(failure==null?0:1);
        }
        static void Require(bool ok,string reason) { if(!ok) throw new InvalidOperationException(reason); }
        IEnumerator WaitAction()
        {
            float until=Time.realtimeSinceStartup+10;
            while(busy && !battleEnded && Time.realtimeSinceStartup<until) yield return null;
            Require(!busy || battleEnded,"Action failed to finish");
        }
        void Press(string caption)
        {
            var button=modal.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<Text>().text==caption);
            Require(button.interactable,caption+" disabled"); button.onClick.Invoke();
        }
        IEnumerator CombatScenarios()
        {
            yield return null;
            RestoreDefault(); var entrance=new Vector2Int(2,2); var enemyCell=new Vector2Int(3,2);
            var enemy=draft.spawns.Find(s=>s.templateId=="E02"); enemy.x=3; enemy.y=2;
            draft.goalX=2; draft.goalY=2; draft.tiles[22]=(int)Ground.Hazard; SaveMap();
            FighterBook.Defaults().Save(BookPath); StartPlay();
            var actor=State.units[0]; var target=State.At(enemyCell);
            Require(CombatRules.Damage(actor,target)==28,"Baseline 40-12");
            returnToEditor.onClick.Invoke(); OpenBook();
            fields[2].text="50"; fields[4].text="4"; fields[5].text="0"; SaveBook();
            Require(modal!=null && FighterBook.Read(BookPath).units[0].attack==40,"Invalid configuration wrote data");
            fields[5].text="2"; SaveBook(); StartPlay(); actor=State.units[0];
            ClickCell(actor.cell); Require(State.reachable.Values.Max()==4 && CombatRules.CanHit(actor,actor.cell,State.At(enemyCell)),"Configured move and range");
            returnToEditor.onClick.Invoke(); OpenBook(); fields[5].text="1"; SaveBook(); StartPlay();
            actor=State.units[0]; target=State.At(enemyCell); ClickCell(actor.cell);
            Require(selectionCard.gameObject.activeSelf && selectedName.text==actor.name,"Selected character card");
            Require(wait.interactable && !cancel.gameObject.activeSelf,"Stationary wait and hidden cancel");
            Require(skillSlots.gameObject.activeSelf,"Skill slots are visible with the action wheel");
            var slots=skillSlots.GetComponentsInChildren<Button>();
            Require(slots.Length==2 && slots.All(slot=>!slot.interactable),"Two placeholder skills on the upper-left arc");
            ClickCell(entrance);
            Require(cancel.gameObject.activeSelf && CombatRules.CanBasicAttack(State,target),"Moved cancel and basic attack range");
            ClickCell(enemyCell); Require(busy && modal==null,"Direct basic attack should start without a forecast modal");
            yield return WaitAction();
            Require(victory && battleEnded && target.hp==32 && actor.hp==70,"Attack/counter/hazard/goal order");
            Capture(Path.Combine(Path.GetDirectoryName(mapStore.Path),"victory.png"));
            Press("重新挑战"); Require(!battleEnded && State.units[0].hp==100 && State.turn==1,"Restart victory");
            returnToEditor.onClick.Invoke(); RestoreDefault();
            draft.spawns.RemoveAll(s=>s.templateId!="U01" && s.templateId!="E02"); enemy=draft.spawns.First(s=>s.templateId=="E02"); enemy.x=2;enemy.y=2; SaveMap();
            var book=FighterBook.Defaults(); book.units[5].attack=999; book.Save(BookPath); StartPlay();
            ClickCell(State.units[0].cell); ClickCell(State.units[0].cell); wait.onClick.Invoke(); yield return WaitAction();
            Require(battleEnded && !victory && resultReason.Contains("阵亡"),"Auto enemy turn and defeat");
            Press("重新挑战"); Require(!battleEnded && State.units[0].hp==100,"Restart defeat");
            returnToEditor.onClick.Invoke(); RestoreDefault(); SaveMap(); book=FighterBook.Defaults();
            foreach(var t in book.units) t.policy=EnemyPolicy.Stationary;
            book.Save(BookPath); StartPlay();
            for(int i=1;i<=12;i++)
            {
                Require(State.turn==i,"Turn counter"); yield return EnemyRound();
                if(i==3) Require(State.hazards.Contains(new Vector2Int(3,5)),"Fourth round event");
            }
            Require(battleEnded && !victory && resultReason.Contains("12"),"Timeout");
            Press("返回编辑"); Require(editing && !draft.At(new Vector2Int(3,5)).Equals(Ground.Hazard),"Runtime event mutated draft");
            FighterBook.Defaults().Save(BookPath);
            yield return VerifyDefaultRoute(new[]{new Vector2Int(3,6),new Vector2Int(4,9),new Vector2Int(8,9)},new Vector2Int(3,5));
            yield return VerifyDefaultRoute(new[]{new Vector2Int(4,4),new Vector2Int(6,6),new Vector2Int(6,9),new Vector2Int(8,9)},new Vector2Int(6,5));
        }
        IEnumerator VerifyDefaultRoute(Vector2Int[] moves,Vector2Int passage)
        {
            StartPlay(); var scout=State.units.Find(a=>a.id=="04");
            bool crossed=false;
            foreach(var destination in moves)
            {
                ClickCell(scout.cell);
                Require(State.reachable.ContainsKey(destination),"Default route unreachable "+destination+" round "+State.turn+" phase "+State.phase+" "+string.Join(" / ",State.units.Select(u=>u.id+u.cell.ToString()+" HP"+u.hp)));
                crossed |= State.Path(destination).Contains(passage);
                ClickCell(destination); wait.onClick.Invoke(); yield return WaitAction();
                if(battleEnded) break;
                endTurn.onClick.Invoke(); Press("确认"); yield return WaitAction();
            }
            Require(battleEnded && victory && crossed,"Default route failed "+passage);
            Debug.Log("DEFAULT_ROUTE_PASS "+passage+" turn "+State.turn+" scout HP "+scout.hp);
            Press("返回编辑");
        }
    }
}
