using System;
using System.IO;
using UnityEngine;
using Mizonglin.Demo;
class BasicAttackChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static void Main()
    {
        var s=new BattleState();s.blocked.Clear();s.units.Clear();
        var a=new DemoUnit("A","前锋",false,2,2,3,100){stats=new FighterTemplate("U01","前锋",100,40,15,3,1)};
        var b=new DemoUnit("B","敌人",true,4,2,0,70){stats=new FighterTemplate("E02","敌人",70,35,12,0,1)};
        var friend=new DemoUnit("C","友军",false,2,3,3,100){stats=a.stats};
        s.units.Add(a);s.units.Add(b);s.units.Add(friend);
        Check(!CombatRules.CanBasicAttack(s,b),"No selected attacker");
        s.Select(a);Check(!CombatRules.CanBasicAttack(s,b),"Out of range from origin");
        Check(s.Preview(new Vector2Int(3,2)),"Reachable destination");
        Check(a.cell==new Vector2Int(2,2)&&CombatRules.CanBasicAttack(s,b),"Use preview instead of logical origin");
        Check(!CombatRules.CanBasicAttack(s,friend),"Friendly fire rejected");
        Check(!CombatRules.CanBasicAttack(s,a)&&!CombatRules.CanBasicAttack(s,null),"Self and empty cell rejected");
        a.acted=true;Check(!CombatRules.CanBasicAttack(s,b),"Acted attacker rejected");a.acted=false;
        b.hp=0;Check(!CombatRules.CanBasicAttack(s,b),"Dead target rejected");b.hp=70;
        a.hp=0;Check(!CombatRules.CanBasicAttack(s,b),"Dead attacker rejected");a.hp=100;
        s.Cancel();Check(!CombatRules.CanBasicAttack(s,b),"Cancel restores original attack origin");
        b.cell=new Vector2Int(3,2);Check(CombatRules.CanBasicAttack(s,b),"Stationary adjacent attack");
        a.stats.range=2;b.cell=new Vector2Int(4,2);Check(CombatRules.CanBasicAttack(s,b),"Configured ranged basic attack");
        Check(CombatRules.Damage(a,b)==28,"Existing damage unchanged");
        s.Select(b);Check(!CombatRules.CanBasicAttack(s,a),"Enemy inspection cannot issue attacks");
        s.Select(a);s.Preview(a.cell);s.CommitWait();Check(!CombatRules.CanBasicAttack(s,b),"Wait ends attack eligibility");
        var result="PASS: direct basic-attack rules; selection required; preview origin; original logical position preserved; melee/ranged reach; empty/friendly/self/dead/acted rejection; cancel restores origin; enemy inspection; wait clears eligibility; existing damage. Runtime and editor assemblies compiled. Unity HUD/portrait rendering and pointer integration not executed.";
        File.WriteAllText("Artifacts/basic-attack-verification.txt",result);Console.WriteLine(result);
    }
}
