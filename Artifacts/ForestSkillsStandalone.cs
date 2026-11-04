using System;
using System.IO;
class ForestSkillsStandalone
{
    static void Main()
    {
        string report = ForestSkillChecks.Run();
        File.WriteAllText("Artifacts/forest-skills-verification.txt", report); Console.WriteLine(report);
    }
}
