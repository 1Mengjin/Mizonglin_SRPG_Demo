using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zilong.Forest;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
        Transform buffPulseRoot;
        readonly HashSet<long> presentedBuffSequences = new HashSet<long>();
        readonly List<Material> buffPulseMaterials = new List<Material>();
        const float BuffPulseDuration = .46f;

        void ClearBuffPulses()
        {
            if (buffPulseRoot != null) { buffPulseRoot.gameObject.SetActive(false); ReleaseObject(buffPulseRoot.gameObject); }
            buffPulseRoot = null; presentedBuffSequences.Clear();
            foreach (var material in buffPulseMaterials) { materials.Remove(material); ReleaseObject(material); }
            buffPulseMaterials.Clear();
        }
        bool PlayBuffSettlementEffects(IEnumerable<BattleEvent> events)
        {
            bool played = false;
            foreach (var e in events)
            {
                if (e.kind != "AwakeningHeal" && e.kind != "CurseDetonated") continue;
                if (!presentedBuffSequences.Add(e.sequence)) continue;
                StartCoroutine(AnimateBuffPulse(e.cell, e.kind == "AwakeningHeal")); played = true;
            }
            return played;
        }
        IEnumerator AnimateBuffPulse(Vector2Int cell, bool awakening)
        {
            if (buffPulseRoot == null)
            {
                buffPulseRoot = new GameObject("BUFF瞬时特效").transform;
                buffPulseRoot.SetParent(boardRoot, false);
            }
            var root = new GameObject(awakening ? "唤灵青色波纹" : "咒言黄色周身闪光").transform;
            root.SetParent(buffPulseRoot, false); root.position = World(cell, .08f);
            var color = awakening ? new Color(.12f, 1f, .90f) : new Color(1f, .84f, .12f);
            var material = new Material(Resources.Load<Shader>("BuffPulse")); material.color = color;
            materials.Add(material); buffPulseMaterials.Add(material);
            int count = awakening ? 3 : 9;
            var lines = new LineRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var line = new GameObject(awakening ? "青色扩散波纹" : "黄色星芒").AddComponent<LineRenderer>();
                line.transform.SetParent(root, false); line.sharedMaterial = material; line.useWorldSpace = false;
                line.loop = true; line.positionCount = awakening ? 48 : 8;
                line.startWidth = line.endWidth = awakening ? .055f : .035f;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
                lines[i] = line;
            }
            float start = Time.unscaledTime;
            try
            {
                while (root != null && Time.unscaledTime - start < BuffPulseDuration)
                {
                    float t = Mathf.Clamp01((Time.unscaledTime - start) / BuffPulseDuration);
                    for (int i = 0; i < count; i++)
                    {
                        var line = lines[i]; float alpha;
                        if (awakening)
                        {
                            float wave = Mathf.Clamp01((t - i * .12f) / (1 - i * .12f));
                            float radius = Mathf.Lerp(.18f, 1.05f, wave); alpha = t < i * .12f ? 0 : (1 - wave) * .95f;
                            for (int j = 0; j < line.positionCount; j++)
                            {
                                float a = j * Mathf.PI * 2 / line.positionCount;
                                line.SetPosition(j, new Vector3(Mathf.Cos(a) * radius, .08f + i * .18f + wave * .12f, Mathf.Sin(a) * radius));
                            }
                        }
                        else
                        {
                            float a = i * Mathf.PI * 2 / count;
                            var center = new Vector3(Mathf.Cos(a) * (.36f + .14f * t), .35f + (i % 3) * .32f + t * .12f, Mathf.Sin(a) * (.36f + .14f * t));
                            float radius = (.12f + .04f * (i % 2)) * (1 + .6f * Mathf.Sin(t * Mathf.PI));
                            for (int j = 0; j < 8; j++)
                            {
                                float angle = j * Mathf.PI / 4; float r = radius * (j % 2 == 0 ? 1 : .22f);
                                var offset = view.transform.right * (Mathf.Cos(angle) * r) + view.transform.up * (Mathf.Sin(angle) * r);
                                line.SetPosition(j, center + offset);
                            }
                            alpha = Mathf.Pow(1 - t, 1.4f);
                        }
                        var tint = new Color(1, 1, 1, alpha); line.startColor = line.endColor = tint;
                    }
                    yield return null;
                }
            }
            finally
            {
                if (root != null) ReleaseObject(root.gameObject);
                materials.Remove(material); buffPulseMaterials.Remove(material); ReleaseObject(material);
            }
        }
    }
}
