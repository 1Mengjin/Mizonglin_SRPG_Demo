using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using Zilong.Forest;

class ForestMilestoneOneStandalone
{
    static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";

    // Only exports public definition data to build/compare the checked-in Unity asset. Not a runtime save format.
    static string Json(object value)
    {
        if (value == null) return "null";
        if (value is string text) return Quote(text);
        if (value is bool flag) return flag ? "true" : "false";
        if (value.GetType().IsEnum) return Convert.ToInt32(value).ToString();
        if (value is int number) return number.ToString();
        if (value is Vector2Int cell) return "{\"x\":" + cell.x + ",\"y\":" + cell.y + "}";
        var values = new List<string>();
        if (value is IEnumerable items)
        {
            foreach (var item in items) values.Add(Json(item));
            return "[" + string.Join(",", values) + "]";
        }
        foreach (var field in value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            values.Add(Quote(field.Name) + ":" + Json(field.GetValue(value)));
        return "{" + string.Join(",", values) + "}";
    }

    static void Main()
    {
        var report = ForestDataChecks.Run();
        Directory.CreateDirectory("Artifacts");
        File.WriteAllText("Artifacts/forest-m1-standalone-verification.txt", report, new UTF8Encoding(false));
        File.WriteAllText("Artifacts/forest-m1-default-definition.json", Json(ForestDefaults.Create()), new UTF8Encoding(false));
        Console.WriteLine(report);
    }
}
