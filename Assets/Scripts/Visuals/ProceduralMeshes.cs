using System.Collections.Generic;
using UnityEngine;

namespace MTS.Visuals
{
    /// <summary>Procedural meshes for the visuals: icospheres, noise-displaced rocks, curved ground patches, discs.</summary>
    public static class ProceduralMeshes
    {
        private static readonly Dictionary<int, Mesh> SphereCache = new Dictionary<int, Mesh>();

        /// <summary>Unit icosphere (radius 1) with the given number of subdivisions (cached).</summary>
        public static Mesh Icosphere(int subdivisions)
        {
            if (SphereCache.TryGetValue(subdivisions, out Mesh cached) && cached != null)
            {
                return cached;
            }

            BuildIcosphere(subdivisions, out List<Vector3> verts, out List<int> tris);
            var mesh = new Mesh { name = "Icosphere" + subdivisions };
            if (verts.Count > 65000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(verts);
            mesh.SetNormals(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            SphereCache[subdivisions] = mesh;
            return mesh;
        }

        /// <summary>
        /// Irregular rock: an icosphere squashed into an ellipsoid and displaced along its normals by fractal noise,
        /// with a few impact pits. Radius ~1. Different seeds give different rocks.
        /// </summary>
        public static Mesh Rock(int seed, int subdivisions = 5, float roughness = 0.22f, Vector3? axes = null)
        {
            BuildIcosphere(subdivisions, out List<Vector3> verts, out List<int> tris);
            var rng = new System.Random(seed);
            Vector3 offset = new Vector3((float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f, (float)rng.NextDouble() * 100f);
            Vector3 shape = axes ?? new Vector3(1.0f, 0.82f + (float)rng.NextDouble() * 0.1f, 0.72f + (float)rng.NextDouble() * 0.12f);
            var pits = new List<Vector4>();
            for (int i = 0; i < 7; i++)
            {
                Vector3 d = new Vector3((float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1).normalized;
                pits.Add(new Vector4(d.x, d.y, d.z, 0.15f + (float)rng.NextDouble() * 0.25f));
            }

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 n = verts[i];
                float h = Fbm(n * 1.6f + offset, 5) - 0.5f;
                float ridges = 1f - Mathf.Abs(Fbm(n * 3.1f + offset * 1.3f, 3) * 2f - 1f);
                float r = 1f + h * roughness * 2f + ridges * roughness * 0.25f;
                foreach (Vector4 pit in pits)
                {
                    float a = Vector3.Angle(n, new Vector3(pit.x, pit.y, pit.z)) * Mathf.Deg2Rad;
                    float x = a / pit.w;
                    if (x < 1.3f)
                    {
                        r -= roughness * 0.45f * Mathf.Exp(-x * x * 2.5f) - roughness * 0.12f * Mathf.Exp(-(x - 1f) * (x - 1f) * 12f);
                    }
                }

                verts[i] = Vector3.Scale(n, shape) * r;
            }

            var mesh = new Mesh { name = "Rock" + seed };
            mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Curved ground patch centred on the origin: a polar grid with log-spaced rings out to <paramref name="radius"/>
        /// metres, each vertex lowered by the Earth's curvature so the patch lies on the R = 6371 km sphere whose top is
        /// at the origin. Normals point away from the sphere's centre.
        /// </summary>
        public static Mesh CurvedPatch(float radius, int rings = 160, int segments = 192, float innerRadius = 5f)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            const double R = MTS.Core.PhysicalConstants.EarthRadius;
            verts.Add(Vector3.zero);
            normals.Add(Vector3.up);
            double ratio = System.Math.Pow(radius / innerRadius, 1.0 / (rings - 1));
            for (int ring = 0; ring < rings; ring++)
            {
                double d = innerRadius * System.Math.Pow(ratio, ring);
                // place the vertex on the sphere at arc length d
                double theta = d / R;
                double horiz = R * System.Math.Sin(theta);
                double y = -(R - R * System.Math.Cos(theta));
                for (int s = 0; s < segments; s++)
                {
                    double phi = s * 2.0 * System.Math.PI / segments;
                    var p = new Vector3((float)(horiz * System.Math.Cos(phi)), (float)y, (float)(horiz * System.Math.Sin(phi)));
                    verts.Add(p);
                    normals.Add(new Vector3((float)(System.Math.Sin(theta) * System.Math.Cos(phi)), (float)System.Math.Cos(theta), (float)(System.Math.Sin(theta) * System.Math.Sin(phi))));
                }
            }

            for (int s = 0; s < segments; s++)
            {
                tris.Add(0);
                tris.Add(1 + (s + 1) % segments);
                tris.Add(1 + s);
            }

            for (int ring = 0; ring < rings - 1; ring++)
            {
                int a0 = 1 + ring * segments;
                int b0 = 1 + (ring + 1) * segments;
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    tris.Add(a0 + s);
                    tris.Add(a0 + s1);
                    tris.Add(b0 + s);
                    tris.Add(a0 + s1);
                    tris.Add(b0 + s1);
                    tris.Add(b0 + s);
                }
            }

            var mesh = new Mesh { name = "CurvedGround", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(radius * 2f, radius, radius * 2f));
            return mesh;
        }

        /// <summary>Flat unit disc in the XZ plane (radius 1, facing +Y).</summary>
        public static Mesh Disc(int segments = 96, int rings = 24)
        {
            var verts = new List<Vector3> { Vector3.zero };
            var tris = new List<int>();
            for (int r = 1; r <= rings; r++)
            {
                float rad = (float)r / rings;
                for (int s = 0; s < segments; s++)
                {
                    float a = s * Mathf.PI * 2f / segments;
                    verts.Add(new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad));
                }
            }

            for (int s = 0; s < segments; s++)
            {
                tris.Add(0);
                tris.Add(1 + (s + 1) % segments);
                tris.Add(1 + s);
            }

            for (int r = 0; r < rings - 1; r++)
            {
                int a0 = 1 + r * segments, b0 = 1 + (r + 1) * segments;
                for (int s = 0; s < segments; s++)
                {
                    int s1 = (s + 1) % segments;
                    tris.Add(a0 + s); tris.Add(a0 + s1); tris.Add(b0 + s);
                    tris.Add(a0 + s1); tris.Add(b0 + s1); tris.Add(b0 + s);
                }
            }

            var mesh = new Mesh { name = "Disc" };
            mesh.SetVertices(verts);
            var normals = new List<Vector3>();
            for (int i = 0; i < verts.Count; i++) normals.Add(Vector3.up);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Unit quad in the XY plane spanning -0.5..0.5 (for billboards).</summary>
        public static Mesh Quad()
        {
            var mesh = new Mesh { name = "BillboardQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        private static void BuildIcosphere(int subdivisions, out List<Vector3> verts, out List<int> tris)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            tris = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            // With this table cross(b - a, c - a) points outwards, which Unity treats as the front face.
            for (int s = 0; s < subdivisions; s++)
            {
                var cache = new Dictionary<long, int>();
                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2];
                    int ab = Mid(a, b, verts, cache), bc = Mid(b, c, verts, cache), ca = Mid(c, a, verts, cache);
                    next.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }

                tris = next;
            }
        }

        private static int Mid(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (cache.TryGetValue(key, out int idx)) return idx;
            idx = verts.Count;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            cache[key] = idx;
            return idx;
        }

        // ---- value noise matching MTSCommon.cginc closely enough for shapes ----
        private static float Hash(Vector3 p)
        {
            p = new Vector3(Frac(p.x * 0.1031f), Frac(p.y * 0.1031f), Frac(p.z * 0.1031f));
            float d = Vector3.Dot(p, new Vector3(p.z, p.y, p.x) + Vector3.one * 31.32f);
            p += Vector3.one * d;
            return Frac((p.x + p.y) * p.z);
        }

        private static float Frac(float x) => x - Mathf.Floor(x);

        public static float Noise(Vector3 p)
        {
            Vector3 i = new Vector3(Mathf.Floor(p.x), Mathf.Floor(p.y), Mathf.Floor(p.z));
            Vector3 f = p - i;
            Vector3 u = new Vector3(f.x * f.x * (3 - 2 * f.x), f.y * f.y * (3 - 2 * f.y), f.z * f.z * (3 - 2 * f.z));
            float n000 = Hash(i), n100 = Hash(i + new Vector3(1, 0, 0)), n010 = Hash(i + new Vector3(0, 1, 0)), n110 = Hash(i + new Vector3(1, 1, 0));
            float n001 = Hash(i + new Vector3(0, 0, 1)), n101 = Hash(i + new Vector3(1, 0, 1)), n011 = Hash(i + new Vector3(0, 1, 1)), n111 = Hash(i + new Vector3(1, 1, 1));
            return Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(n000, n100, u.x), Mathf.Lerp(n010, n110, u.x), u.y),
                Mathf.Lerp(Mathf.Lerp(n001, n101, u.x), Mathf.Lerp(n011, n111, u.x), u.y), u.z);
        }

        public static float Fbm(Vector3 p, int octaves)
        {
            float sum = 0, amp = 0.5f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Noise(p);
                p = p * 2.03f + Vector3.one * 17.1f;
                amp *= 0.5f;
            }

            return sum;
        }
    }
}
