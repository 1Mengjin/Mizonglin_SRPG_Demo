using UnityEngine;
using UnityEngine.UI;
using Zilong.Forest;

namespace Zilong.Demo
{
    public sealed class BattleStatusIcon : MaskableGraphic
    {
        public StatusIcon icon;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); float s = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .44f;
            void Line(float ax, float ay, float bx, float by, float width = .14f)
            {
                Vector2 a = new Vector2(ax, ay) * s, b = new Vector2(bx, by) * s;
                var n = new Vector2(-(b - a).y, (b - a).x).normalized * (s * width * .5f);
                int i = vh.currentVertCount; vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero); vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
            }
            void Ring(float x, float y, float radius)
            {
                for (int i = 0; i < 32; i++) { float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16; Line(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius, .1f); }
            }
            switch (icon)
            {
                case StatusIcon.Shield:
                    Line(-.75f, .7f, 0, .95f); Line(0, .95f, .75f, .7f); Line(.75f, .7f, .6f, -.3f); Line(.6f, -.3f, 0, -.95f); Line(0, -.95f, -.6f, -.3f); Line(-.6f, -.3f, -.75f, .7f); Line(0, .65f, 0, -.6f); break;
                case StatusIcon.Sword: case StatusIcon.Action:
                    Line(-.65f, -.75f, .65f, .75f, .25f); Line(-.75f, -.1f, -.1f, -.75f); Line(.65f, .75f, .25f, .65f); break;
                case StatusIcon.Wing:
                    Line(-.8f, -.6f, .8f, .8f); Line(.8f, .8f, .7f, -.15f); Line(.7f, -.15f, -.3f, -.4f); Line(.1f, .2f, .7f, .15f); Line(-.25f, -.1f, .3f, -.35f); break;
                case StatusIcon.Reflect:
                    Ring(0, 0, .72f); Line(.7f, .7f, .7f, .1f); Line(.7f, .7f, .1f, .7f); Line(-.7f, -.7f, -.7f, -.1f); Line(-.7f, -.7f, -.1f, -.7f); break;
                case StatusIcon.Link: Ring(-.32f, .25f, .52f); Ring(.32f, -.25f, .52f); break;
                case StatusIcon.Heart:
                    Line(-.8f, .25f, -.5f, .7f); Line(-.5f, .7f, 0, .35f); Line(0, .35f, .5f, .7f); Line(.5f, .7f, .8f, .25f); Line(.8f, .25f, 0, -.8f); Line(0, -.8f, -.8f, .25f); break;
                case StatusIcon.Poison: case StatusIcon.Spirit: case StatusIcon.Fruit:
                    Ring(0, -.17f, .61f); Line(-.58f, .1f, 0, .92f); Line(0, .92f, .58f, .1f); if (icon == StatusIcon.Poison) { Line(-.24f, -.05f, -.24f, -.28f); Line(.24f, -.05f, .24f, -.28f); } break;
                case StatusIcon.Scar:
                    Line(-.8f, .8f, .2f, -.2f, .2f); Line(-.2f, .2f, .8f, -.8f, .2f); Line(-.4f, .1f, -.1f, .4f); Line(.1f, -.4f, .4f, -.1f); break;
                case StatusIcon.Stun:
                    for (int i = 0; i < 5; i++) { float a = i * Mathf.PI * .4f; float b = a + Mathf.PI * .8f; Line(Mathf.Cos(a) * .85f, Mathf.Sin(a) * .85f, Mathf.Cos(b) * .85f, Mathf.Sin(b) * .85f); } break;
                case StatusIcon.Purify:
                    Line(0, -.9f, 0, .9f); Line(-.9f, 0, .9f, 0); Line(-.5f, -.5f, .5f, .5f, .09f); Line(-.5f, .5f, .5f, -.5f, .09f); break;
                case StatusIcon.Fog:
                    for (int i = -1; i <= 1; i++) { Line(-.8f, i * .5f, .3f, i * .5f); Line(.3f, i * .5f, .7f, i * .5f + .2f); } break;
                case StatusIcon.Mask:
                    Ring(0, 0, .85f); Line(-.5f, .25f, -.15f, .1f); Line(.5f, .25f, .15f, .1f); Line(-.3f, -.4f, .3f, -.4f); break;
                case StatusIcon.Cooldown:
                    Ring(0, 0, .82f); Line(0, .6f, 0, 0); Line(0, 0, .4f, -.22f); break;
                default:
                    Line(-.2f, .9f, -.65f, -.05f, .2f); Line(-.65f, -.05f, .5f, .1f, .2f); Line(.5f, .1f, -.1f, -.9f, .2f); break;
            }
        }
    }
}
