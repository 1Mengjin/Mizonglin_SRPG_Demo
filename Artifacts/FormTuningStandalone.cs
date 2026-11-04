using System;
using System.IO;
class FormTuningStandalone{static void Main(){var report=ForestFormTuningChecks.Run();Console.WriteLine(report);File.WriteAllText("Artifacts/form-tuning-verification.txt",report);}}
