using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Mizonglin.Demo
{
    [Serializable]
    public sealed class BattleTextEntry
    {
        public string id, label;
        public string title;
        [TextArea(5, 16)] public string description;
    }
    public struct BattleDescription
    {
        public string title, description;
    }
    [CreateAssetMenu(menuName = "迷踪林战棋 Demo/技能与状态说明", fileName = "BattleTextCatalog")]
    public sealed class BattleTextCatalog : ScriptableObject
    {
        public const string ResourceName = "BattleTextCatalog";
        public List<BattleTextEntry> entries = new List<BattleTextEntry>();
        public BattleDescription Resolve(string id, string title, string description, IReadOnlyDictionary<string, string> values)
            => Resolve(entries, id, title, description, values);
        public static BattleDescription Resolve(IList<BattleTextEntry> entries, string id, string title, string description, IReadOnlyDictionary<string, string> values)
        {
            BattleTextEntry entry = null;
            if (entries != null) foreach (var item in entries) if (item != null && item.id == id) { entry = item; break; }
            string Expand(string template, string fallback) => string.IsNullOrEmpty(template) ? fallback : Regex.Replace(template, @"\{([^{}]+)\}", match =>
            {
                string key = match.Groups[1].Value;
                if (key == "默认标题") return title ?? "";
                if (key == "默认说明") return description ?? "";
                return values != null && values.TryGetValue(key, out var value) ? value ?? "" : match.Value;
            });
            return new BattleDescription { title = Expand(entry?.title, title), description = Expand(entry?.description, description) };
        }
        public string Validate()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id)) return "说明条目缺少编号。";
                if (!ids.Add(entry.id)) return "存在重复的说明编号：" + entry.id;
            }
            return null;
        }
    }
}
