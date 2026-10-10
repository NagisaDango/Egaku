using System;
using System.Collections.Generic;
using UnityEngine;

namespace Egaku.Shapes
{
    /// <summary>One sampled boundary feeds both the fill triangulation and collider.</summary>
    public static class ShapeGeometry
    {
        private const float Epsilon = 0.000001f;
        public const int MaximumBoundaryPoints = 2048;

        public static List<ShapePoint> Preset(ShapePreset preset)
        {
            if (preset == ShapePreset.Circle || preset == ShapePreset.Ellipse)
            {
                float x = preset == ShapePreset.Circle ? 1f : 2f;
                const float k = 0.55228475f;
                var circle = new List<ShapePoint>();
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    Vector2 p = new Vector2(Mathf.Cos(a) * x, Mathf.Sin(a));
                    Vector2 t = new Vector2(-Mathf.Sin(a) * x, Mathf.Cos(a)) * k;
                    circle.Add(new ShapePoint(p) { incoming = -t, outgoing = t, mode = ShapePointMode.Smooth });
                }
                return circle;
            }
            if (preset == ShapePreset.Triangle)
                return Points(new Vector2(-2, -1), new Vector2(2, -1), new Vector2(0, 1));
            if (preset == ShapePreset.Slope)
                return Points(new Vector2(-2, -1), new Vector2(2, -1), new Vector2(2, 1));
            return Points(new Vector2(-2, -1), new Vector2(2, -1), new Vector2(2, 1), new Vector2(-2, 1));
        }

        private static List<ShapePoint> Points(params Vector2[] positions)
        {
            var result = new List<ShapePoint>();
            foreach (Vector2 p in positions) result.Add(new ShapePoint(p));
            return result;
        }

        public static Vector2 Evaluate(ShapePoint a, ShapePoint b, float t)
        {
            // A zero handle pair is a straight segment, irrespective of point modes.
            if (a.outgoing == Vector2.zero && b.incoming == Vector2.zero)
                return Vector2.Lerp(a.position, b.position, t);
            float u = 1 - t;
            return u * u * u * a.position + 3 * u * u * t * (a.position + a.outgoing) +
                   3 * u * t * t * (b.position + b.incoming) + t * t * t * b.position;
        }

        public static bool TryBuild(IList<ShapePoint> points, float tolerance, float minimumEdge, int depth,
            out Vector2[] boundary, out int[] triangles, out string error)
        {
            boundary = Array.Empty<Vector2>(); triangles = Array.Empty<int>(); error = null;
            if (points == null || points.Count < 3) { error = "至少需要三個控制點。"; return false; }
            if (points.Count > MaximumBoundaryPoints) { error = "控制點過多（上限 2048）。"; return false; }
            if (!Finite(tolerance) || !Finite(minimumEdge) || tolerance < 0.001f || minimumEdge < 0.001f)
            { error = "曲線容差與最短邊長須為有限數值且至少 0.001。"; return false; }
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (p == null || !Finite(p.position) || !Finite(p.incoming) || !Finite(p.outgoing))
                { error = $"控制點 {i + 1} 含無效座標。"; return false; }
                for (int j = 0; j < i; j++)
                    if ((p.position - points[j].position).sqrMagnitude < Epsilon * Epsilon)
                    { error = $"控制點 {j + 1} 與 {i + 1} 重複。"; return false; }
            }
            var samples = new List<Vector2>();
            depth = Mathf.Clamp(depth, 4, 12);
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Count];
                samples.Add(a.position);
                if (a.outgoing != Vector2.zero || b.incoming != Vector2.zero)
                    if (!Subdivide(a.position, a.position + a.outgoing, b.position + b.incoming,
                        b.position, tolerance, depth, samples))
                    { error = "曲線超出細分上限；提高容差或簡化切線。"; return false; }
                if (samples.Count > MaximumBoundaryPoints)
                { error = "輪廓超出 2048 點；提高曲線容差或簡化形狀。"; return false; }
            }
            boundary = samples.ToArray();
            for (int i = 0; i < samples.Count; i++)
            {
                if (Vector2.Distance(samples[i], samples[(i + 1) % samples.Count]) < minimumEdge)
                { error = $"輪廓邊 {i + 1} 過短；移開控制點、縮短切線或提高容差。"; return false; }
                Vector2 before = samples[i] - samples[(i + samples.Count - 1) % samples.Count];
                Vector2 after = samples[(i + 1) % samples.Count] - samples[i];
                if (Mathf.Abs(Cross(before, after)) <= Epsilon && Vector2.Dot(before, after) < 0)
                { error = $"輪廓點 {i + 1} 的相鄰邊折返重疊。"; return false; }
                for (int j = i + 1; j < samples.Count; j++)
                {
                    if (j == i + 1 || (i == 0 && j == samples.Count - 1)) continue;
                    if (Intersects(samples[i], samples[(i + 1) % samples.Count], samples[j], samples[(j + 1) % samples.Count]))
                    { error = $"輪廓邊 {i + 1} 與 {j + 1} 相交／接觸；不支援自交或洞。"; return false; }
                }
            }
            float area = SignedArea(samples);
            if (Mathf.Abs(area) < minimumEdge * minimumEdge)
            { error = "無法形成有效區域（面積接近零）。"; return false; }
            if (!Triangulate(samples, area, out triangles))
            { error = "無法三角化；檢查重疊、折返及退化邊。"; return false; }
            return true;
        }

        private static bool Subdivide(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float tolerance, int depth, List<Vector2> output)
        {
            // Distance to the *segment*, not its infinite line, catches collinear
            // overshooting/looping handles. De Casteljau preserves the original curve.
            if (DistanceToSegment(b, a, d) <= tolerance && DistanceToSegment(c, a, d) <= tolerance) return true;
            if (depth == 0 || output.Count >= MaximumBoundaryPoints) return false;
            Vector2 ab = (a + b) / 2, bc = (b + c) / 2, cd = (c + d) / 2;
            Vector2 abc = (ab + bc) / 2, bcd = (bc + cd) / 2, mid = (abc + bcd) / 2;
            if (!Subdivide(a, ab, abc, mid, tolerance, depth - 1, output)) return false;
            output.Add(mid);
            return Subdivide(mid, bcd, cd, d, tolerance, depth - 1, output);
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 v = b - a;
            float t = v.sqrMagnitude > Epsilon * Epsilon ? Mathf.Clamp01(Vector2.Dot(p - a, v) / v.sqrMagnitude) : 0;
            return Vector2.Distance(p, a + t * v);
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static bool Finite(Vector2 v) => Finite(v.x) && Finite(v.y);
        public static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        public static float SignedArea(IList<Vector2> p)
        {
            // Translate to the first point to avoid cancellation far from local origin.
            float area = 0;
            for (int i = 1; i < p.Count - 1; i++) area += Cross(p[i] - p[0], p[i + 1] - p[0]);
            return area * 0.5f;
        }
        private static bool Intersects(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross(b - a, c - a), abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c), cdB = Cross(d - c, b - c);
            if (((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon)) &&
                ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon))) return true;
            return (Mathf.Abs(abC) <= Epsilon && DistanceToSegment(c, a, b) <= Epsilon) ||
                   (Mathf.Abs(abD) <= Epsilon && DistanceToSegment(d, a, b) <= Epsilon) ||
                   (Mathf.Abs(cdA) <= Epsilon && DistanceToSegment(a, c, d) <= Epsilon) ||
                   (Mathf.Abs(cdB) <= Epsilon && DistanceToSegment(b, c, d) <= Epsilon);
        }

        private static bool Triangulate(List<Vector2> p, float area, out int[] triangles)
        {
            var indices = new List<int>(); var result = new List<int>();
            for (int i = 0; i < p.Count; i++) indices.Add(area > 0 ? i : p.Count - 1 - i);
            // Redundant collinear boundary vertices stay in the collider/mesh vertex
            // buffer, but are omitted from the ear ring to avoid degenerate triangles.
            bool reduced = true;
            while (reduced && indices.Count > 3)
            {
                reduced = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    Vector2 a = p[indices[(i + indices.Count - 1) % indices.Count]], b = p[indices[i]], c = p[indices[(i + 1) % indices.Count]];
                    if (DistanceToSegment(b, a, c) <= Epsilon)
                    { indices.RemoveAt(i); reduced = true; break; }
                }
            }
            while (indices.Count > 3)
            {
                bool found = false;
                for (int i = 0; i < indices.Count; i++)
                {
                    int a = indices[(i + indices.Count - 1) % indices.Count], b = indices[i], c = indices[(i + 1) % indices.Count];
                    if (Cross(p[b] - p[a], p[c] - p[b]) <= Epsilon) continue;
                    bool inside = false;
                    foreach (int j in indices)
                    {
                        if (j == a || j == b || j == c) continue;
                        if (Cross(p[b] - p[a], p[j] - p[a]) >= -Epsilon &&
                            Cross(p[c] - p[b], p[j] - p[b]) >= -Epsilon &&
                            Cross(p[a] - p[c], p[j] - p[c]) >= -Epsilon) { inside = true; break; }
                    }
                    if (inside) continue;
                    // Front face points towards a standard 2D camera at negative Z.
                    result.Add(a); result.Add(c); result.Add(b);
                    indices.RemoveAt(i); found = true; break;
                }
                if (!found) { triangles = Array.Empty<int>(); return false; }
            }
            if (indices.Count != 3 || Mathf.Abs(Cross(p[indices[1]] - p[indices[0]], p[indices[2]] - p[indices[0]])) <= Epsilon)
            { triangles = Array.Empty<int>(); return false; }
            result.Add(indices[0]); result.Add(indices[2]); result.Add(indices[1]);
            triangles = result.ToArray(); return true;
        }

        public static void SplitSegment(List<ShapePoint> points, int index, float t)
        {
            var a = points[index]; var b = points[(index + 1) % points.Count];
            if (a.outgoing == Vector2.zero && b.incoming == Vector2.zero)
            { points.Insert(index + 1, new ShapePoint(Vector2.Lerp(a.position, b.position, t))); return; }
            Vector2 ab = Vector2.Lerp(a.position, a.position + a.outgoing, t);
            Vector2 bc = Vector2.Lerp(a.position + a.outgoing, b.position + b.incoming, t);
            Vector2 cd = Vector2.Lerp(b.position + b.incoming, b.position, t);
            Vector2 abc = Vector2.Lerp(ab, bc, t), bcd = Vector2.Lerp(bc, cd, t), mid = Vector2.Lerp(abc, bcd, t);
            // Exact split preserves the curve, including asymmetric smooth handles.
            a.outgoing = ab - a.position; b.incoming = cd - b.position;
            points.Insert(index + 1, new ShapePoint(mid) { incoming = abc - mid, outgoing = bcd - mid, mode = ShapePointMode.Smooth });
        }
    }
}
