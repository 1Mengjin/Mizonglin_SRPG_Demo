using System;
using System.IO;
class ForestMilestoneTwoStandalone
{
    static void Main()
    {
        string report = ForestCombatChecks.Run();
        File.WriteAllText("Artifacts/forest-m2-standalone-verification.txt", report);
        Console.WriteLine(report);
    }
}
