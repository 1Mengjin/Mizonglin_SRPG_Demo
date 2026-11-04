using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zilong.Demo
{
    // Retain Text's original layout and geometry, but sample glyphs from a denser font atlas.
    public sealed class SharpText : Text
    {
        static readonly HashSet<SharpText> activeTexts = new HashSet<SharpText>();
        static readonly List<SharpText> pendingTexts = new List<SharpText>();
        TextGenerator detailedGenerator;
        UIVertex[] originalVertices;
        readonly UIVertex[] quad = new UIVertex[4];
        bool detailed = true;
        public bool Detailed
        {
            get => detailed;
            set { if (detailed == value) return; detailed = value; SetVerticesDirty(); }
        }
        public bool UsingDetailedGlyphs { get; private set; }

        static SharpText() { Canvas.preWillRenderCanvases += PrepareAllGlyphs; }
        protected override void OnEnable() { base.OnEnable(); activeTexts.Add(this); }
        protected override void OnDisable() { activeTexts.Remove(this); base.OnDisable(); }
        static void PrepareAllGlyphs()
        {
            // Finish font-atlas growth before Graphic rebuilds, so one label cannot invalidate
            // another label's UVs halfway through generating its mesh.
            pendingTexts.Clear(); pendingTexts.AddRange(activeTexts);
            foreach (var label in pendingTexts)
                if (label != null && label.isActiveAndEnabled) label.PrepareGlyphs();
        }
        void PrepareGlyphs()
        {
            if (font == null || !font.dynamic || string.IsNullOrEmpty(text)) return;
            var settings = GetGenerationSettings(rectTransform.rect.size);
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                cachedTextGenerator.PopulateWithErrors(text, settings, gameObject);
                if (detailed)
                {
                    settings.scaleFactor *= 2;
                    if (detailedGenerator == null) detailedGenerator = new TextGenerator();
                    detailedGenerator.PopulateWithErrors(text, settings, gameObject);
                }
            }
            finally { m_DisableFontTextureRebuiltCallback = false; }
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            // Font-atlas coordinates may have changed while other labels were prewarmed.
            cachedTextGenerator.Invalidate();
            base.OnPopulateMesh(mesh); UsingDetailedGlyphs = false;
            if (!detailed || font == null || !font.dynamic || mesh.currentVertCount == 0) return;
            int count = mesh.currentVertCount;
            if (originalVertices == null || originalVertices.Length < count) originalVertices = new UIVertex[count];
            for (int i = 0; i < count; i++) mesh.PopulateUIVertex(ref originalVertices[i], i);
            var settings = GetGenerationSettings(rectTransform.rect.size);
            settings.scaleFactor *= 2;
            if (detailedGenerator == null) detailedGenerator = new TextGenerator();
            m_DisableFontTextureRebuiltCallback = true;
            try
            {
                detailedGenerator.Invalidate();
                detailedGenerator.PopulateWithErrors(text, settings, gameObject);
                var vertices = detailedGenerator.verts;
                if (vertices.Count == count)
                {
                    mesh.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        quad[i & 3] = originalVertices[i];
                        quad[i & 3].uv0 = vertices[i].uv0;
                        if ((i & 3) == 3) mesh.AddUIVertexQuad(quad);
                    }
                    UsingDetailedGlyphs = true;
                }
            }
            finally { m_DisableFontTextureRebuiltCallback = false; }
            // Preserve standard Text behavior if a font cannot supply matching higher-density glyphs.
            if (!UsingDetailedGlyphs) base.OnPopulateMesh(mesh);
        }
        protected override void OnDestroy()
        {
            if (detailedGenerator != null) ((System.IDisposable)detailedGenerator).Dispose();
            base.OnDestroy();
        }
    }
}
