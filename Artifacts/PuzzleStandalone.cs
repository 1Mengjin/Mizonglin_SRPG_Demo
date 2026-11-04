using System;
using System.IO;
class PuzzleStandalone{static void Main(){var result=ForestPuzzleChecks.Run();Console.WriteLine(result);File.WriteAllText("Artifacts/puzzle-verification.txt",result);}}
