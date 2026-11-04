using System;
using System.IO;
class BalanceStandalone{static void Main(){string result=ForestBalanceChecks.Run();Console.WriteLine(result);File.WriteAllText("Artifacts/balance-verification.txt",result);}}
