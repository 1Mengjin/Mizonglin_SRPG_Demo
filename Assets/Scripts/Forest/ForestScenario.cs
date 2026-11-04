using UnityEngine;

namespace Zilong.Forest
{
    [CreateAssetMenu(menuName = "紫龙/迷踪林正式配置", fileName = "ForestScenario")]
    public sealed class ForestScenario : ScriptableObject
    {
        // An empty asset is invalid; missing data must not silently become a default scenario.
        public ScenarioDefinition definition;
        public string Validate() => definition == null ? "迷踪林配置为空" : definition.Validate();
        public ForestSession CreateSession() => new ForestSession(definition);
    }
}
