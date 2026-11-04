using System;
using System.Collections.Generic;
using System.IO;
using Mizonglin.Demo;

static class DescriptionTextChecks
{
    static int checks;
    static void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
    static void Main()
    {
        var rows = new List<BattleTextEntry> { new BattleTextEntry { id = "status.guard-radius", title = "{角色名}的护卫", description = "范围{层数}格\n{默认说明}" } };
        var values = new Dictionary<string, string> { ["角色名"] = "阿双", ["层数"] = "2" };
        var text = BattleTextCatalog.Resolve(rows, "status.guard-radius", "护卫", "原始动态说明", values);
        Check(text.title == "阿双的护卫" && text.description == "范围2格\n原始动态说明", "Editable text expands live fields and default description");
        text = BattleTextCatalog.Resolve(rows, "missing", "默认", "自动文案", values);
        Check(text.title == "默认" && text.description == "自动文案", "Missing entry preserves existing automatic text");
        rows[0].title = ""; rows[0].description = "";
        text = BattleTextCatalog.Resolve(rows, "status.guard-radius", "护卫", "动态状态", values);
        Check(text.title == "护卫" && text.description == "动态状态", "Empty fields fall back independently");
        rows[0].description = "<b>{角色名}</b> {未定义} {层数}";
        text = BattleTextCatalog.Resolve(rows, "status.guard-radius", "护卫", "", values);
        Check(text.description == "<b>阿双</b> {未定义} 2", "Rich text retained; unknown placeholders do not throw");
        values["角色名"] = "含{层数}的名称";
        text = BattleTextCatalog.Resolve(rows, "status.guard-radius", "护卫", "", values);
        Check(text.description.Contains("含{层数}的名称"), "Substituted values are not recursively expanded");
        Check(rows[0].description == "<b>{角色名}</b> {未定义} {层数}", "Rendering does not modify saved templates");
        text = BattleTextCatalog.Resolve(null, "none", "标题", "效果", null);
        Check(text.title == "标题" && text.description == "效果", "Unavailable catalog has safe fallback");
        string report = $"PASS description templates ({checks} assertions): live substitution, fallback, empty fields, rich text, unknown tokens and read-only expansion.\n";
        File.WriteAllText("Artifacts/description-text-verification.txt", report); Console.Write(report);
    }
}
