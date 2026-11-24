using UnityEngine;
using UnityEngine.UI;
using Veilward.Domain;

namespace Veilward.Presentation
{
    // Один небольшой mesh в каждом из двух существующих slots; shared UI material, без textures.
    public sealed class StatusCueGraphic : MaskableGraphic
    {
        public StatusKind? Kind { get; private set; }
        public double ActiveTime { get; private set; }
        public const float Size = 28;

        public void Refresh(StatusKind? kind, double activeTime, Color tint)
        {
            if (Kind == kind && ActiveTime == activeTime && color == tint) return;
            Kind = kind; ActiveTime = activeTime;
            color = tint; enabled = kind.HasValue;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!Kind.HasValue) return;
            var phase = (float)(ActiveTime % 10) * 6.283185f;
            switch (Kind.Value)
            {
                case StatusKind.Burn:
                    // Три отдельно поднимающихся уголька; нет большого непрозрачного пятна.
                    for (var i = 0; i < 3; i++)
                    {
                        var rise = Mathf.Repeat((float)(ActiveTime % 10) * .8f + i / 3f, 1);
                        var p = new Vector2((i - 1) * 8 + Mathf.Sin(phase + i) * 2, -10 + rise * 20);
                        Diamond(vh, p, 2.4f, 4 * (1 - rise * .5f));
                    }
                    break;
                case StatusKind.Wet:
                    Stroke(vh, new Vector2(0, 12), new Vector2(-6, 1), 2);
                    Stroke(vh, new Vector2(-6, 1), new Vector2(-4, -4), 2);
                    Stroke(vh, new Vector2(-4, -4), new Vector2(4, -4), 2);
                    Stroke(vh, new Vector2(4, -4), new Vector2(6, 1), 2);
                    Stroke(vh, new Vector2(6, 1), new Vector2(0, 12), 2);
                    Arc(vh, new Vector2(0, -8), 7 + Mathf.Sin(phase) * 2, 2, 0, Mathf.PI * 2, 16, .32f);
                    break;
                case StatusKind.Gust:
                    Arc(vh, Vector2.zero, 11, 2, phase, phase + 2.3f, 10);
                    Arc(vh, Vector2.zero, 7, 1.6f, phase + 2.7f, phase + 5, 10);
                    break;
                case StatusKind.Crack:
                    var w = 1.9f + Mathf.Sin(phase) * .25f;
                    Stroke(vh, new Vector2(4, 12), new Vector2(-3, 3), w);
                    Stroke(vh, new Vector2(-3, 3), new Vector2(3, -2), w);
                    Stroke(vh, new Vector2(3, -2), new Vector2(-4, -12), w);
                    Stroke(vh, new Vector2(-3, 3), new Vector2(-11, 6), w);
                    Stroke(vh, new Vector2(3, -2), new Vector2(11, -5), w);
                    break;
            }
        }

        private void Diamond(VertexHelper vh, Vector2 p, float x, float y)
        { Quad(vh, p + Vector2.up * y, p + Vector2.right * x, p - Vector2.up * y, p - Vector2.right * x); }
        private void Stroke(VertexHelper vh, Vector2 a, Vector2 b, float width)
        {
            var d = (b - a).normalized; var n = new Vector2(-d.y, d.x) * (width * .5f);
            Quad(vh, a + n, b + n, b - n, a - n);
        }
        private void Arc(VertexHelper vh, Vector2 p, float radius, float width, float from, float to, int steps, float yScale = 1)
        {
            var prev = p + new Vector2(Mathf.Cos(from), Mathf.Sin(from) * yScale) * radius;
            for (var i = 1; i <= steps; i++)
            {
                var a = Mathf.Lerp(from, to, i / (float)steps);
                var next = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * yScale) * radius;
                Stroke(vh, prev, next, width); prev = next;
            }
        }
        private void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var index = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
            vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
