using UnityEngine;
using UnityEngine.UI;

namespace Veilward.Presentation
{
    public enum VeilwardGlyph { Seal, Heart, Gold, Wave, Pause, Flame, Tide, Gale, Stone, Steam, Storm, Lock }

    // Контуры авторских пиктограмм в нормализованных координатах. Данные редактируются вместе с UI.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VeilwardUiGlyph : MaskableGraphic
    {
        [SerializeField] private VeilwardGlyph glyph;
        private Rect _square;
        private VertexHelper _vertices;

        public VeilwardGlyph Glyph
        {
            get => glyph;
            set { if (glyph == value) return; glyph = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            _vertices = vertices;
            var rect = GetPixelAdjustedRect();
            var side = Mathf.Min(rect.width, rect.height);
            _square = new Rect(rect.center - Vector2.one * side * .5f, Vector2.one * side);
            switch (glyph)
            {
                case VeilwardGlyph.Seal:
                    if (side < 60)
                    {
                        Ring(.42f, .036f, 24);
                        Ring(.29f, .041f, 4);
                        Ring(.12f, .046f, 4);
                        break;
                    }
                    Ring(.455f, .009f, 64);
                    Ring(.39f, .004f, 64);
                    Ring(.33f, .012f, 8);
                    Ring(.23f, .012f, 4);
                    Ring(.105f, .035f, 4);
                    for (var i = 0; i < 8; i++)
                    {
                        var angle = i * Mathf.PI / 4;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        var center = Vector2.one * .5f;
                        Stroke(center + direction * .35f, center + direction * .42f, .012f);
                    }
                    break;
                case VeilwardGlyph.Heart:
                    Path(.06f, .50f,.12f, .72f,.34f,.76f, .5f,.62f, .66f,.76f, .88f,.72f, .94f,.5f,.5f,.12f,.06f,.50f);
                    break;
                case VeilwardGlyph.Gold:
                    Ring(.42f, .07f, 8); Ring(.16f,.065f,4); break;
                case VeilwardGlyph.Wave:
                    Path(.08f,.28f,.37f,.65f,.57f,.39f,.76f,.71f,.92f,.52f);
                    Path(.13f,.16f,.37f,.44f,.56f,.2f,.77f,.5f,.9f,.36f); break;
                case VeilwardGlyph.Pause:
                    Stroke(new Vector2(.34f,.2f), new Vector2(.34f,.8f), .13f);
                    Stroke(new Vector2(.66f,.2f), new Vector2(.66f,.8f), .13f); break;
                case VeilwardGlyph.Flame:
                    Path(.50f,.1f,.23f,.23f,.19f,.45f,.33f,.64f,.4f,.48f,.58f,.89f,.76f,.57f,.83f,.32f,.67f,.15f,.50f,.1f);
                    Path(.49f,.13f,.43f,.32f,.57f,.48f,.64f,.25f,.60f,.13f); break;
                case VeilwardGlyph.Tide:
                    Path(.5f,.9f,.77f,.51f,.81f,.34f,.67f,.16f,.5f,.11f,.33f,.16f,.19f,.34f,.23f,.51f,.5f,.9f);
                    Path(.38f,.29f,.50f,.23f,.63f,.28f); break;
                case VeilwardGlyph.Gale:
                    Path(.12f,.65f,.67f,.65f,.79f,.72f,.76f,.83f,.63f,.85f);
                    Path(.11f,.46f,.83f,.46f,.91f,.53f,.87f,.64f);
                    Path(.28f,.27f,.61f,.27f,.71f,.19f,.65f,.12f,.52f,.12f); break;
                case VeilwardGlyph.Stone:
                    Path(.16f,.27f,.28f,.68f,.58f,.87f,.82f,.61f,.88f,.28f,.56f,.14f,.16f,.27f);
                    Path(.28f,.68f,.54f,.52f,.82f,.61f);
                    Path(.54f,.52f,.56f,.14f); break;
                case VeilwardGlyph.Steam:
                    Path(.18f,.2f,.82f,.2f); Path(.32f,.35f,.24f,.55f,.39f,.71f,.36f,.85f);
                    Path(.52f,.35f,.44f,.53f,.59f,.69f,.56f,.83f);
                    Path(.71f,.35f,.64f,.51f,.78f,.67f,.75f,.81f); break;
                case VeilwardGlyph.Storm:
                    Path(.62f,.9f,.26f,.48f,.49f,.48f,.38f,.1f,.76f,.57f,.54f,.57f,.62f,.9f); break;
                case VeilwardGlyph.Lock:
                    Path(.22f,.12f,.78f,.12f,.78f,.53f,.22f,.53f,.22f,.12f);
                    Path(.35f,.55f,.35f,.72f,.43f,.84f,.57f,.84f,.65f,.72f,.65f,.55f); break;
            }
            _vertices = null;
        }

        private void Path(params float[] points)
        {
            for (var i = 2; i < points.Length; i += 2)
                Stroke(new Vector2(points[i - 2], points[i - 1]), new Vector2(points[i], points[i + 1]), .062f);
        }

        private void Ring(float radius, float width, int sides)
        {
            var previous = Vector2.one * .5f + Vector2.right * radius;
            for (var i = 1; i <= sides; i++)
            {
                var angle = (float)i / sides * Mathf.PI * 2;
                var next = Vector2.one * .5f + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Stroke(previous, next, width);
                previous = next;
            }
        }

        private void Stroke(Vector2 from, Vector2 to, float width)
        {
            var delta = (to - from).normalized;
            var normal = new Vector2(-delta.y, delta.x) * width * .5f;
            var index = _vertices.currentVertCount;
            Vertex(from + normal); Vertex(to + normal); Vertex(to - normal); Vertex(from - normal);
            _vertices.AddTriangle(index, index + 1, index + 2);
            _vertices.AddTriangle(index, index + 2, index + 3);
        }

        private void Vertex(Vector2 point) =>
            _vertices.AddVert(_square.min + Vector2.Scale(_square.size, point), color, Vector2.zero);
    }
}
