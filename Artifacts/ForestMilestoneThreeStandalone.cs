using System;
using System.IO;
class ForestMilestoneThreeStandalone
{
    static void Main()
    {
        string report = ForestResourceChecks.Run();
        File.WriteAllText("Artifacts/forest-m3-standalone-verification.txt", report);
        Console.WriteLine(report);
    }
}
