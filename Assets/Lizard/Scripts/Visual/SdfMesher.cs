using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lizards
{
    /// <summary>
    /// Convierte una función de distancia con signo (negativa dentro) en una malla suave y cerrada
    /// mediante Surface Nets: un vértice por celda que cruza la superficie, proyectado sobre ella,
    /// con normales calculadas del gradiente.
    /// </summary>
    public static class SdfMesher
    {
        public static Mesh Build(Func<Vector3, float> sdf, Bounds bounds, float cell, string name)
        {
            Vector3 min = bounds.min - Vector3.one * cell * 2f;
            Vector3 size = bounds.size + Vector3.one * cell * 4f;
            int nx = Mathf.CeilToInt(size.x / cell) + 1, ny = Mathf.CeilToInt(size.y / cell) + 1, nz = Mathf.CeilToInt(size.z / cell) + 1;
            var field = new float[nx * ny * nz];
            int Idx(int x, int y, int z) => (z * ny + y) * nx + x;
            Vector3 P(int x, int y, int z) => min + new Vector3(x, y, z) * cell;
            for (int z = 0; z < nz; z++) for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++) field[Idx(x, y, z)] = sdf(P(x, y, z));

            int cx = nx - 1, cy = ny - 1, cz = nz - 1;
            var cellVert = new int[cx * cy * cz];
            for (int i = 0; i < cellVert.Length; i++) cellVert[i] = -1;
            int CIdx(int x, int y, int z) => (z * cy + y) * cx + x;

            var verts = new List<Vector3>();
            int[,] edges = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
            var cv = new float[8]; var cp = new Vector3[8];
            for (int z = 0; z < cz; z++) for (int y = 0; y < cy; y++) for (int x = 0; x < cx; x++)
            {
                int inside = 0;
                for (int c = 0; c < 8; c++)
                {
                    int ox = c & 1, oy = (c >> 1) & 1, oz = (c >> 2) & 1;
                    cv[c] = field[Idx(x + ox, y + oy, z + oz)];
                    cp[c] = P(x + ox, y + oy, z + oz);
                    if (cv[c] < 0f) inside++;
                }
                if (inside == 0 || inside == 8) continue;
                Vector3 sum = Vector3.zero; int n = 0;
                for (int e = 0; e < 12; e++)
                {
                    float a = cv[edges[e, 0]], b = cv[edges[e, 1]];
                    if ((a < 0f) == (b < 0f)) continue;
                    float t = a / (a - b);
                    sum += Vector3.Lerp(cp[edges[e, 0]], cp[edges[e, 1]], t); n++;
                }
                cellVert[CIdx(x, y, z)] = verts.Count;
                verts.Add(sum / n);
            }

            // Proyecta cada vértice sobre la superficie (2 pasos de Newton) y calcula su normal.
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 p = verts[i];
                for (int k = 0; k < 2; k++) { var g = Gradient(sdf, p, cell * 0.5f); p -= g * sdf(p); }
                verts[i] = p;
                normals[i] = Gradient(sdf, p, cell * 0.5f);
            }

            var tris = new List<int>();
            void Quad(int a, int b, int c, int d)
            {
                if (a < 0 || b < 0 || c < 0 || d < 0) return;
                Tri(a, b, c); Tri(a, c, d);
            }
            void Tri(int a, int b, int c)
            {
                Vector3 n = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
                if (Vector3.Dot(n, normals[a] + normals[b] + normals[c]) < 0f) { tris.Add(a); tris.Add(c); tris.Add(b); }
                else { tris.Add(a); tris.Add(b); tris.Add(c); }
            }
            for (int z = 1; z < cz; z++) for (int y = 1; y < cy; y++) for (int x = 0; x < cx; x++)
                if ((field[Idx(x, y, z)] < 0f) != (field[Idx(x + 1, y, z)] < 0f))
                    Quad(cellVert[CIdx(x, y - 1, z - 1)], cellVert[CIdx(x, y, z - 1)], cellVert[CIdx(x, y, z)], cellVert[CIdx(x, y - 1, z)]);
            for (int z = 1; z < cz; z++) for (int y = 0; y < cy; y++) for (int x = 1; x < cx; x++)
                if ((field[Idx(x, y, z)] < 0f) != (field[Idx(x, y + 1, z)] < 0f))
                    Quad(cellVert[CIdx(x - 1, y, z - 1)], cellVert[CIdx(x, y, z - 1)], cellVert[CIdx(x, y, z)], cellVert[CIdx(x - 1, y, z)]);
            for (int z = 0; z < cz; z++) for (int y = 1; y < cy; y++) for (int x = 1; x < cx; x++)
                if ((field[Idx(x, y, z)] < 0f) != (field[Idx(x, y, z + 1)] < 0f))
                    Quad(cellVert[CIdx(x - 1, y - 1, z)], cellVert[CIdx(x, y - 1, z)], cellVert[CIdx(x, y, z)], cellVert[CIdx(x - 1, y, z)]);

            var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Vector3 Gradient(Func<Vector3, float> sdf, Vector3 p, float h)
        {
            var g = new Vector3(
                sdf(p + new Vector3(h, 0, 0)) - sdf(p - new Vector3(h, 0, 0)),
                sdf(p + new Vector3(0, h, 0)) - sdf(p - new Vector3(0, h, 0)),
                sdf(p + new Vector3(0, 0, h)) - sdf(p - new Vector3(0, 0, h)));
            return g.sqrMagnitude > 1e-12f ? g.normalized : Vector3.up;
        }

        // ---- Primitivas ----
        public static float Ellipsoid(Vector3 p, Vector3 c, Vector3 r)
        {
            Vector3 q = p - c;
            float k0 = new Vector3(q.x / r.x, q.y / r.y, q.z / r.z).magnitude;
            float k1 = new Vector3(q.x / (r.x * r.x), q.y / (r.y * r.y), q.z / (r.z * r.z)).magnitude;
            return k1 > 1e-6f ? k0 * (k0 - 1f) / k1 : -Mathf.Min(r.x, Mathf.Min(r.y, r.z));
        }

        /// <summary>Cápsula con radio variable; 't' devuelve la posición relativa a lo largo del segmento.</summary>
        public static float Capsule(Vector3 p, Vector3 a, Vector3 b, float ra, float rb, out float t)
        {
            Vector3 ab = b - a;
            t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Vector3.Dot(ab, ab));
            return (p - (a + ab * t)).magnitude - Mathf.Lerp(ra, rb, t);
        }

        public static float SMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }
    }
}
