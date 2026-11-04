using UnityEngine;
using UnityEngine.UI;

namespace Zilong.Demo
{
    public sealed partial class BattleDemo
    {
        [SerializeField, HideInInspector] GameObject editorPreviewRoot;
        bool buildingPreview;

        // The saved preview is excluded from builds and disabled before gameplay starts.
        static void ReleaseObject(Object value)
        {
            if (value == null) return;
#if UNITY_EDITOR
            if (UnityEditor.EditorUtility.IsPersistent(value)) return;
#endif
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

#if UNITY_EDITOR
        public GameObject RebuildEditorPreview()
        {
            if (Application.isPlaying) return null;
            if (editorPreviewRoot != null) DestroyImmediate(editorPreviewRoot);
            tiles.Clear(); obstacles.Clear(); hazardMarks.Clear();
            figures.Clear(); labels.Clear(); materials.Clear(); toolButtons.Clear();
            buildingPreview = true;
            var originalRoots = new System.Collections.Generic.HashSet<GameObject>(gameObject.scene.GetRootGameObjects());
            stageTwo = stageThree = true;
            Awake();
            editorPreviewRoot = new GameObject("关卡设计视图 Play 直接战斗");
            editorPreviewRoot.tag = "EditorOnly";
            editorPreviewRoot.transform.SetParent(transform, false);
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (!originalRoots.Contains(root)) root.transform.SetParent(editorPreviewRoot.transform, true);
            foreach (var button in editorPreviewRoot.GetComponentsInChildren<Button>(true)) button.interactable = false;
            Canvas.ForceUpdateCanvases();
            LateUpdate();
            buildingPreview = false;
            return editorPreviewRoot;
        }
#endif
    }
}
