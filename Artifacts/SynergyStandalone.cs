using System;
using System.IO;
class SynergyStandalone{static void Main(){var report=ForestSynergyChecks.Run();Console.WriteLine(report);File.WriteAllText("Artifacts/synergy-verification.txt",report);}}
