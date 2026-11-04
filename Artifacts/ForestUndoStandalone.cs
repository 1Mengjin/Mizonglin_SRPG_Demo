using System;
using System.IO;
class ForestUndoStandalone
{
    static void Main()
    {
        string report = ForestUndoChecks.Run();
        File.WriteAllText("Artifacts/forest-undo-verification.txt", report);
        Console.WriteLine(report);
    }
}
