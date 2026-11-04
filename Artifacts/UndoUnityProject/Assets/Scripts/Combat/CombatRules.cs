using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Zilong.Demo
{
    public enum EnemyPolicy { Stationary, Chase, Guard }

    [Serializable] public class FighterTemplate
    {
        public string id, name;
        public string profession;
        public Sprite portrait;
        public int hp, attack, defense, move, range, radius;
        public EnemyPolicy policy;
        public FighterTemplate(string id,string name,int hp,int attack,int defense,int move,int range,EnemyPolicy policy=EnemyPolicy.Stationary)
        {
            this.id=id; this.name=name; this.hp=hp; this.attack=attack; this.defense=defense;
            this.move=move; this.range=range; this.policy=policy; radius=2;
        }
        public string Validate()
        {
            if(string.IsNullOrWhiteSpace(name)) return "名称不能为空";
            if(hp<1 || range<1) return "生命和射程必须是正整数";
            if(attack<0 || defense<0 || move<0 || radius<0) return "攻击、防御、移动和守卫半径不能为负数";
            if(!Enum.IsDefined(typeof(EnemyPolicy),policy)) return "敌方行为无效";
            return null;
        }
    }

    [Serializable] public class FighterBook
    {
        public FighterTemplate[] units;
        public static FighterBook Defaults() => new FighterBook { units=new[] {
            new FighterTemplate("U01","前锋",100,40,15,3,1),new FighterTemplate("U02","游侠",75,35,10,3,2),
            new FighterTemplate("U03","铁卫",130,30,25,2,1),new FighterTemplate("U04","斥候",80,32,10,4,1),
            new FighterTemplate("E01","弩手",60,30,10,0,2),new FighterTemplate("E02","追兵",70,35,12,3,1,EnemyPolicy.Chase),
            new FighterTemplate("E03","守卫",110,38,22,2,1,EnemyPolicy.Guard) } };
        public void Validate()
        {
            if(units==null || units.Length!=7) throw new InvalidDataException("需要七个单位模板");
            for(int i=0;i<units.Length;i++)
            {
                var t=units[i];
                if(t==null || t.id!=LevelMap.Templates[i]) throw new InvalidDataException("模板编号无效");
                var error=t.Validate(); if(error!=null) throw new InvalidDataException(t.id+" "+error);
            }
        }
        public static FighterBook Read(string path)
        {
            if(!File.Exists(path)) return Defaults();
            var book=JsonUtility.FromJson<FighterBook>(File.ReadAllText(path));
            if(book==null) throw new InvalidDataException("单位配置为空");
            book.Validate(); return book;
        }
        public void Save(string path)
        {
            Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path+".tmp",JsonUtility.ToJson(this,true));
            if(File.Exists(path)) File.Replace(path+".tmp",path,null); else File.Move(path+".tmp",path);
        }
        public void Apply(BattleState state,LevelMap map)
        {
            foreach(var unit in state.units)
            {
                var spawn=map.spawns.Find(s=>s.instanceId==unit.id);
                var t=Array.Find(units,f=>f.id==spawn.templateId);
                unit.stats=t; unit.anchor=spawn.Cell; unit.name=string.IsNullOrEmpty(spawn.label)?t.name:spawn.label; unit.hp=t.hp; unit.move=t.move;
            }
        }
    }

    public static class CombatRules
    {
        public static int Distance(Vector2Int a,Vector2Int b)=>Math.Abs(a.x-b.x)+Math.Abs(a.y-b.y);
        public static bool CanHit(DemoUnit a,Vector2Int from,DemoUnit b)
        {
            if (a?.forestBattle != null && b?.forestUnit != null)
                return Zilong.Forest.ForestRules.CanHit(a.forestBattle, a.forestUnit, from, b.forestUnit);
            if(a==null || b==null || a.enemy==b.enemy || a.hp<=0 || b.hp<=0) return false;
            int distance=Distance(from,b.cell);
            return distance>=1 && distance<=a.stats.range;
        }
        public static bool CanBasicAttack(BattleState state,DemoUnit target)
        {
            var actor=state.selected;
            if(actor==null || actor.enemy || actor.acted || actor.stats==null || target==null || target.stats==null) return false;
            if(state.phase!=SelectionPhase.Selected && state.phase!=SelectionPhase.Preview) return false;
            if (state.Forest != null)
                return Zilong.Forest.ForestRules.ValidateCommand(state.Forest, new Zilong.Forest.ForestCommand {
                    actorId = actor.id, targetId = target.id, kind = Zilong.Forest.ActionKind.BasicAttack,
                    path = state.Path(state.phase == SelectionPhase.Preview ? state.preview : actor.cell) }) == null;
            return CanHit(actor,state.phase==SelectionPhase.Preview?state.preview:actor.cell,target);
        }
        public static int Damage(DemoUnit a,DemoUnit b)=>a.forestBattle != null ? Zilong.Forest.ForestRules.Damage(a.forestBattle,a.forestUnit,b.forestUnit) : Math.Max(1,a.stats.attack-b.stats.defense);
        public static bool Counter(DemoUnit a,DemoUnit b)=>a.stats.range==1 && b.stats.range==1 && CanHit(b,b.cell,a);
        public static int Hazard(DemoUnit unit,int percent)=>(int)(((long)unit.stats.hp*percent+99)/100);
        public static void Hurt(BattleState state,DemoUnit unit,int amount)
        {
            unit.hp=Math.Max(0,unit.hp-amount);
            if(unit.hp==0) state.units.Remove(unit);
        }
        public static bool GoalReached(BattleState state,DemoUnit actor)=>!actor.enemy && actor.hp>0 && state.hasGoal && actor.cell==state.goal;

        public sealed class Plan
        {
            public List<Vector2Int> steps=new List<Vector2Int>();
            public DemoUnit target;
        }
        public static Plan Choose(BattleState state,DemoUnit actor)
        {
            var plan=new Plan();
            var players=state.units.Where(u=>!u.enemy).OrderBy(u=>u.id,StringComparer.Ordinal);
            var policy=actor.stats.policy;
            var candidates=players.Where(u=>policy!=EnemyPolicy.Guard || Distance(u.cell,actor.anchor)<=actor.stats.radius).ToList();
            plan.target=candidates.FirstOrDefault(u=>CanHit(actor,actor.cell,u));
            if(plan.target!=null || policy==EnemyPolicy.Stationary) return plan;
            var parent=new Dictionary<Vector2Int,Vector2Int>();
            var distance=new Dictionary<Vector2Int,int>{{actor.cell,0}};
            var queue=new Queue<Vector2Int>(); queue.Enqueue(actor.cell);
            var dirs=new[]{Vector2Int.left,Vector2Int.down,Vector2Int.up,Vector2Int.right};
            while(queue.Count>0)
            {
                var p=queue.Dequeue();
                foreach(var d in dirs)
                {
                    var n=p+d;
                    if(distance.ContainsKey(n)||!state.Inside(n)||state.blocked.Contains(n)||state.At(n)!=null) continue;
                    if(policy==EnemyPolicy.Guard && Distance(n,actor.anchor)>actor.stats.radius) continue;
                    parent[n]=p; distance[n]=distance[p]+1; queue.Enqueue(n);
                }
            }
            var best=(from target in candidates from p in distance.Keys
                      where CanHit(actor,p,target)
                      orderby distance[p], candidates.IndexOf(target), p.x,p.y
                      select new {target,p}).FirstOrDefault();
            if(best==null) return plan;
            for(var p=best.p;p!=actor.cell;p=parent[p]) plan.steps.Add(p);
            plan.steps.Reverse(); if(plan.steps.Count>actor.move) plan.steps.RemoveRange(actor.move,plan.steps.Count-actor.move);
            var end=plan.steps.Count==0?actor.cell:plan.steps[plan.steps.Count-1];
            if(CanHit(actor,end,best.target)) plan.target=best.target;
            return plan;
        }
    }
}
