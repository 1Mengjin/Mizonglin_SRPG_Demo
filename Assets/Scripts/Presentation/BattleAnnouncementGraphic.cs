using UnityEngine;
using UnityEngine.UI;

namespace Mizonglin.Demo
{
    // Resolution-independent ornament; no screenshot fragments or external textures are needed.
    public sealed class BattleAnnouncementGraphic : MaskableGraphic
    {
        public enum Motif { Ribbon, Seal, Vortex }
        public Motif motif;
        public Color accent = new Color(.87f, .73f, .44f);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (motif == Motif.Ribbon)
            {
                float w = r.width * .5f, h = r.height * .5f;
                Quad(vh, new Vector2(-w, -h), new Vector2(w, -h), new Vector2(w, h), new Vector2(-w, h), new Color(0, 0, 0, .72f));
                Stroke(vh, new Vector2(-w + 1, -h + 1), new Vector2(w - 1, -h + 1), 2, accent);
                Stroke(vh, new Vector2(-w + 1, h - 1), new Vector2(w - 1, h - 1), 2, accent);
                Stroke(vh, new Vector2(-w + 1, -h + 1), new Vector2(-w + 1, h - 1), 2, accent);
                Stroke(vh, new Vector2(w - 1, -h + 1), new Vector2(w - 1, h - 1), 2, accent);
            }
            else if (motif == Motif.Seal)
            {
                float radius = Mathf.Min(r.width, r.height) * .46f;
                for (int i = 0; i < 128; i++)
                {
                    float a = i * Mathf.PI * 2 / 128, b = (i + 1) * Mathf.PI * 2 / 128;
                    float rough = 1 + .014f * Mathf.Sin(i * 2.7f);
                    var p = Polar(a, radius * rough); var q = Polar(b, radius * (1 + .014f * Mathf.Sin((i + 1) * 2.7f)));
                    Quad(vh, Vector2.zero, p, q, Vector2.zero, new Color(.015f, .035f, .035f, .92f));
                    Stroke(vh, p, q, 10, accent);
                    Stroke(vh, Polar(a, radius * .86f), Polar(b, radius * .86f), 2, accent);
                }
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6;
                    var p = Polar(a, radius * .68f); var q = Polar(a + .13f, radius * .79f);
                    Stroke(vh, p, q, 3, new Color(accent.r, accent.g, accent.b, .55f));
                    Stroke(vh, q, Polar(a + .32f, radius * .70f), 3, new Color(accent.r, accent.g, accent.b, .55f));
                }
            }
            else
            {
                float radius = Mathf.Min(r.width, r.height) * .43f;
                for (int arm = 0; arm < 5; arm++)
                    for (int i = 0; i < 28; i++)
                    {
                        float t = i / 28f, u = (i + 1) / 28f, a = arm * Mathf.PI * .4f;
                        Stroke(vh, Polar(a + t * 3.7f, radius * t), Polar(a + u * 3.7f, radius * u), 1 + 6 * t, accent);
                    }
            }
        }

        static Vector2 Polar(float a, float radius) => new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        static void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Quad(vh, a - normal, b - normal, b + normal, a + normal, c);
        }
        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
