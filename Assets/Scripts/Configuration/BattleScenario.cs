using System;
using UnityEngine;

namespace Mizonglin.Demo
{
    [CreateAssetMenu(menuName = "迷踪林/关卡配置", fileName = "BattleScenario")]
    public sealed class BattleScenario : ScriptableObject
    {
        public LevelMap map = LevelMap.Default();
        public FighterBook fighters = FighterBook.Defaults();

        public string Validate()
        {
            if (map == null || fighters == null) return "关卡或单位配置为空";
            var error = map.Validate();
            if (error != null) return error;
            try { fighters.Validate(); }
            catch (Exception ex) { return ex.Message; }
            return null;
        }

        public FighterBook CopyFighters() => JsonUtility.FromJson<FighterBook>(JsonUtility.ToJson(fighters));
    }
}
