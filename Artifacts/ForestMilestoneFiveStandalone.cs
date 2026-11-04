using System;
using System.IO;
class ForestMilestoneFiveStandalone
{
    static void Main()
    {
        string report = ForestFeedbackChecks.Run();
        File.WriteAllText("Artifacts/forest-m5-standalone-verification.txt", report); Console.WriteLine(report);
    }
}
