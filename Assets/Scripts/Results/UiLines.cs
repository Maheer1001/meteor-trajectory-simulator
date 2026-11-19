using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTS.Results
{
    /// <summary>
    /// Immediate-style vector drawing for uGUI: polylines with mitred joins, dashed lines, filled fans and quads.
    /// Coordinates are in the RectTransform's local space measured from its bottom-left corner. Call
    /// <see cref="Clear"/>, add shapes, then <see cref="Commit"/>. Used by the charts and the map overlay.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class UiLines : MaskableGraphic
    {
        private readonly List<UIVertex> vertices = new List<UIVertex>();
        private readonly List<int> indices = new List<int>();

        public int VertexCount => vertices.Count;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        public void Clear()
        {
            vertices.Clear();
            indices.Clear();
        }

        public void Commit() => SetVerticesDirty();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (vertices.Count == 0)
            {
                return;
            }

            Rect r = rectTransform.rect;
            Vector3 offset = new Vector3(r.xMin, r.yMin, 0);
            for (int i = 0; i < vertices.Count; i++)
            {
                UIVertex v = vertices[i];
                v.position += offset;
                vh.AddVert(v);
            }

            for (int i = 0; i < indices.Count; i += 3)
            {
                vh.AddTriangle(indices[i], indices[i + 1], indices[i + 2]);
            }
        }

        private int Vert(Vector2 p, Color32 c)
        {
            UIVertex v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            vertices.Add(v);
            return vertices.Count - 1;
        }

        private void Tri(int a, int b, int c)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            Color32 c32 = color;
            int i0 = Vert(a, c32), i1 = Vert(b, c32), i2 = Vert(c, c32), i3 = Vert(d, c32);
            Tri(i0, i1, i2);
            Tri(i2, i3, i0);
        }

        public void Rect(Rect rect, Color color) =>
            Quad(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
                new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin), color);

        public void Segment(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 1e-8f)
            {
                return;
            }

            Vector2 n = new Vector2(-d.y, d.x).normalized * (width * 0.5f);
            Quad(a - n, a + n, b + n, b - n, color);
        }

        /// <summary>Polyline with mitred joins (miter length limited to 3 half-widths).</summary>
        public void Polyline(IList<Vector2> points, float width, Color color, bool closed = false)
        {
            int count = points.Count;
            if (count < 2)
            {
                return;
            }

            Color32 c32 = color;
            float half = width * 0.5f;
            int first = vertices.Count;
            int n = closed ? count + 1 : count;
            for (int k = 0; k < n; k++)
            {
                int i = k % count;
                Vector2 p = points[i];
                Vector2 prev = k > 0 ? points[(i - 1 + count) % count] : (closed ? points[count - 1] : p);
                Vector2 next = k < n - 1 ? points[(i + 1) % count] : (closed ? points[1 % count] : p);
                Vector2 d0 = (p - prev);
                Vector2 d1 = (next - p);
                if (d0.sqrMagnitude < 1e-10f) d0 = d1;
                if (d1.sqrMagnitude < 1e-10f) d1 = d0;
                d0.Normalize();
                d1.Normalize();
                Vector2 n0 = new Vector2(-d0.y, d0.x);
                Vector2 n1 = new Vector2(-d1.y, d1.x);
                Vector2 miter = (n0 + n1);
                float len = miter.magnitude;
                if (len < 1e-4f)
                {
                    miter = n0;
                    len = 1f;
                }
                else
                {
                    miter /= len;
                }

                float dot = Vector2.Dot(miter, n1);
                float scale = Mathf.Min(3f, 1f / Mathf.Max(0.2f, dot));
                Vector2 offset = miter * half * scale;
                Vert(p - offset, c32);
                Vert(p + offset, c32);
            }

            for (int k = 0; k < n - 1; k++)
            {
                int a = first + 2 * k;
                Tri(a, a + 1, a + 3);
                Tri(a + 3, a + 2, a);
            }
        }

        public void Dashed(Vector2 a, Vector2 b, float width, float dash, float gap, Color color)
        {
            float length = Vector2.Distance(a, b);
            if (length < 1e-3f)
            {
                return;
            }

            Vector2 dir = (b - a) / length;
            for (float s = 0; s < length; s += dash + gap)
            {
                Segment(a + dir * s, a + dir * Mathf.Min(length, s + dash), width, color);
            }
        }

        /// <summary>Dashed open polyline: dashes of the given length follow the path, continuing across vertices.</summary>
        public void DashedPolyline(IList<Vector2> points, float width, float dash, float gap, Color color)
        {
            float phase = 0f;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[i + 1];
                float length = Vector2.Distance(a, b);
                if (length < 1e-4f) continue;
                Vector2 dir = (b - a) / length;
                float s = 0f;
                while (s < length)
                {
                    float period = dash + gap;
                    float inPeriod = phase % period;
                    float step = Mathf.Max(1e-2f, Mathf.Min(length - s, inPeriod < dash ? dash - inPeriod : period - inPeriod));
                    if (inPeriod < dash)
                    {
                        Segment(a + dir * s, a + dir * (s + step), width, color);
                    }

                    s += step;
                    phase += step;
                }
            }
        }

        /// <summary>Filled polygon as a triangle fan around a centre (exact for star-shaped outlines).</summary>
        public void Fan(Vector2 centre, IList<Vector2> outline, Color color)
        {
            if (outline.Count < 3)
            {
                return;
            }

            Color32 c32 = color;
            int c = Vert(centre, c32);
            int first = vertices.Count;
            for (int i = 0; i < outline.Count; i++)
            {
                Vert(outline[i], c32);
            }

            for (int i = 0; i < outline.Count - 1; i++)
            {
                Tri(c, first + i, first + i + 1);
            }
        }

        /// <summary>Area between an outline and a horizontal line (used for rings that enclose a pole).</summary>
        public void FillToY(IList<Vector2> outline, float y, Color color)
        {
            Color32 c32 = color;
            for (int i = 0; i < outline.Count - 1; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[i + 1];
                int i0 = Vert(a, c32), i1 = Vert(b, c32), i2 = Vert(new Vector2(b.x, y), c32), i3 = Vert(new Vector2(a.x, y), c32);
                Tri(i0, i1, i2);
                Tri(i2, i3, i0);
            }
        }

        /// <summary>Filled disc approximated by a polygon.</summary>
        public void Disc(Vector2 centre, float radius, Color color, int segments = 24)
        {
            var outline = new List<Vector2>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                outline.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }

            Fan(centre, outline, color);
        }

        public void Circle(Vector2 centre, float radius, float width, Color color, int segments = 48)
        {
            var outline = new List<Vector2>(segments);
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.PI * 2f * i / segments;
                outline.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }

            Polyline(outline, width, color, true);
        }
    }
}
