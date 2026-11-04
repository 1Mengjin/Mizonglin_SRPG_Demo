using System;
using System.IO;
class ReinforcementStandalone{static void Main(){var result=ForestReinforcementChecks.Run();Console.WriteLine(result);File.WriteAllText("Artifacts/reinforcement-verification.txt",result);}}
