using System;
using System.IO;
class ForestMilestoneFourStandalone
{
    static void Main()
    {
        string report = ForestEnemyChecks.Run();
        File.WriteAllText("Artifacts/forest-m4-standalone-verification.txt", report);
        Console.WriteLine(report);
    }
}
