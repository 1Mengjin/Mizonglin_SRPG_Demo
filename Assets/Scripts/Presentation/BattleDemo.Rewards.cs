using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
        Transform rewardRoot;
        readonly Dictionary<Vector2Int, Transform> rewardMarkers = new Dictionary<Vector2Int, Transform>();
        Mesh rewardStarMesh;
        RectTransform rewardPopup;

        void RefreshRewardMarkers()
        {
            if (!ForestEnabled || boardRoot == null) return;
            if (rewardRoot == null || rewardRoot.parent != boardRoot)
            {
                rewardMarkers.Clear();
                rewardRoot = new GameObject("奖励地块").transform;
                rewardRoot.SetParent(boardRoot, false);
            }
            // Only committed state controls visibility; movement previews never consume a reward.
            var remaining = forestSession.State.remainingRewards;
            foreach (var marker in rewardMarkers) marker.Value.gameObject.SetActive(remaining.Contains(marker.Key));
            foreach (var cell in remaining)
            {
                if (rewardMarkers.ContainsKey(cell)) continue;
                var marker = new GameObject("奖励闪光 " + cell).transform;
                marker.SetParent(rewardRoot, false);
                rewardMarkers.Add(cell, marker);
                AddRewardStar(marker, Vector3.zero, .28f, Color.white);
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.PI * .5f + .4f;
                    AddRewardStar(marker, new Vector3(Mathf.Cos(angle) * .24f, Mathf.Sin(angle) * .24f, -.01f),
                        .10f, Color.HSVToRGB(i * .25f, .62f, 1));
                }
            }
            AnimateRewardMarkers();
        }
        void AddRewardStar(Transform parent, Vector3 position, float size, Color color)
        {
            if (rewardStarMesh == null)
            {
                rewardStarMesh = new Mesh { name = "Reward four point sparkle" };
                var vertices = new Vector3[9]; var triangles = new int[24];
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4; float r = i % 2 == 0 ? 1 : .23f;
                    vertices[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
                    triangles[i * 3] = 0; triangles[i * 3 + 1] = (i + 1) % 8 + 1; triangles[i * 3 + 2] = i + 1;
                }
                rewardStarMesh.vertices = vertices; rewardStarMesh.triangles = triangles; rewardStarMesh.RecalculateNormals();
            }
            var star = new GameObject("彩色星芒", typeof(MeshFilter), typeof(MeshRenderer));
            star.transform.SetParent(parent, false); star.transform.localPosition = position; star.transform.localScale = Vector3.one * size;
            star.GetComponent<MeshFilter>().sharedMesh = rewardStarMesh;
            star.GetComponent<MeshRenderer>().sharedMaterial = Mat(color);
        }
        void AnimateRewardMarkers()
        {
            if (rewardRoot == null || view == null) return;
            float time = Application.isPlaying ? Time.unscaledTime : 0;
            foreach (var pair in rewardMarkers)
            {
                var marker = pair.Value; if (marker == null || !marker.gameObject.activeSelf) continue;
                float phase = time * 3.4f + pair.Key.x;
                marker.position = World(pair.Key, .65f + Mathf.Sin(phase) * .07f);
                marker.rotation = view.transform.rotation * Quaternion.Euler(0, 0, Mathf.Sin(time * 1.6f) * 12);
                marker.localScale = Vector3.one * (1 + Mathf.Sin(phase * 1.7f) * .15f);
            }
        }
        void ClearRewardPopup()
        {
            if (rewardPopup != null) ReleaseObject(rewardPopup.gameObject);
            rewardPopup = null;
        }
        IEnumerator ShowCollectedReward(int eventStart)
        {
            if (!forestSession.State.eventLog.Skip(eventStart).Any(e => e.kind == "RewardCollected")) yield break;
            ClearRewardPopup();
            rewardPopup = Box(canvas.transform, "获得奖励", new Vector2(.5f, .5f), new Vector2(0, 130), new Vector2(370, 110), ink);
            var popup = rewardPopup;
            Box(popup, "Accent", new Vector2(.5f, 1), new Vector2(0, -3), new Vector2(360, 4), gold);
            var text = Label(popup, "Reward message", "获得奖励", new Vector2(.5f, .5f), Vector2.zero, new Vector2(340, 90), 38, TextAnchor.MiddleCenter);
            text.color = new Color(1, .88f, .53f); text.fontStyle = FontStyle.Bold;
            var group = popup.gameObject.AddComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
            float start = Time.unscaledTime;
            while (popup != null && Time.unscaledTime - start < BattlePacing.RewardPopup)
            {
                float elapsed = Time.unscaledTime - start;
                float t = Mathf.Clamp01(elapsed / .52f);
                // Damped spring: overshoot, rebound, then settle at full size.
                float scale = t >= 1 ? 1 : 1 - Mathf.Exp(-7 * t) * Mathf.Cos(12 * t);
                popup.localScale = Vector3.one * Mathf.Max(.04f, scale);
                // 淡出锚定在同一总时长上，保证收尾 .22 秒的形状不变。
                group.alpha = Mathf.Clamp01(elapsed / .10f) * Mathf.Clamp01((BattlePacing.RewardPopup - elapsed) / .22f);
                yield return null;
            }
            if (rewardPopup == popup) ClearRewardPopup();
        }
    }
}
