using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Acumulador de geometría con color por vértice. Cada sala junta miles de piezas (ladrillos, remaches,
    /// tablones…) en pocas mallas, y al final se "hornea" la luz cálida de los faroles en los colores.
    /// </summary>
    public sealed class SewerKit
    {
        public readonly List<Vector3> V = new();
        public readonly List<Vector3> N = new();
        public readonly List<Color> C = new();
        public readonly List<Vector2> U = new();
        public readonly List<int> T = new();
        public bool bake = true;

        public static readonly List<(Vector3 p, Color c, float r)> Lamps = new();
        public static string MeshDir;
        public static int Serial;

        int Add(Vector3 p, Vector3 n, Color c, Vector2 uv = default) { V.Add(p); N.Add(n); C.Add(c); U.Add(uv); return V.Count - 1; }

        void TriAuto(int a, int b, int c)
        {
            var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (Vector3.Dot(n, N[a] + N[b] + N[c]) < 0) T.AddRange(new[] { a, c, b }); else T.AddRange(new[] { a, b, c });
        }

        /// <summary>Cuadrilátero plano; la cara visible es la que apunta hacia 'facing'.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, Vector3 facing, Vector2[] uv = null)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(n, facing) < 0) n = -n;
            int i = Add(a, n, col, uv != null ? uv[0] : default); Add(b, n, col, uv != null ? uv[1] : default);
            Add(c, n, col, uv != null ? uv[2] : default); Add(d, n, col, uv != null ? uv[3] : default);
            TriAuto(i, i + 1, i + 2); TriAuto(i, i + 2, i + 3);
        }

        public void Poly(IList<Vector3> pts, Color col, Vector3 facing)
        {
            if (pts.Count < 3) return;
            int s = V.Count;
            foreach (var p in pts) Add(p, facing.normalized, col);
            for (int i = 1; i < pts.Count - 1; i++) TriAuto(s, s + i, s + i + 1);
        }

        static readonly Vector3[] FaceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };

        public void Box(Matrix4x4 m, Color col, bool bottom = true)
        {
            foreach (var n in FaceN)
            {
                if (!bottom && n == Vector3.down) continue;
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                Vector3 c0 = n * 0.5f;
                Vector3 a = c0 - u * 0.5f - v * 0.5f, b = c0 + u * 0.5f - v * 0.5f, c = c0 + u * 0.5f + v * 0.5f, d = c0 - u * 0.5f + v * 0.5f;
                Vector3 wa = m.MultiplyPoint3x4(a), wb = m.MultiplyPoint3x4(b), wc = m.MultiplyPoint3x4(c), wd = m.MultiplyPoint3x4(d);
                Quad(wa, wb, wc, wd, col, m.MultiplyVector(n));
            }
        }
        public void Box(Vector3 c, Vector3 s, Color col) => Box(Matrix4x4.TRS(c, Quaternion.identity, s), col);
        public void Box(Vector3 c, Vector3 s, Quaternion r, Color col) => Box(Matrix4x4.TRS(c, r, s), col);

        public static Mesh Bevel;
        /// <summary>Bloque biselado (bordes redondeados): para piedras de remate, losas y bloques gruesos.</summary>
        public void Block(Vector3 c, Vector3 s, Quaternion r, Color col)
        {
            if (!Bevel) { Box(c, s, r, col); return; }
            var m = Matrix4x4.TRS(c, r, s); var it = m.inverse.transpose;
            var bv = Bevel.vertices; var bn = Bevel.normals; var bt = Bevel.triangles; int s0 = V.Count;
            for (int i = 0; i < bv.Length; i++) Add(m.MultiplyPoint3x4(bv[i]), it.MultiplyVector(bn[i]).normalized, col);
            for (int i = 0; i < bt.Length; i++) T.Add(s0 + bt[i]);
        }

        /// <summary>Cilindro (o cono si r2 != r) entre a y b.</summary>
        public void Cyl(Vector3 a, Vector3 b, float r, Color col, int sides = 14, bool caps = true, float r2 = -1, Color? capCol = null)
        {
            if (r2 < 0) r2 = r;
            var ax = (b - a); if (ax.sqrMagnitude < 1e-6f) return; ax.Normalize();
            var u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var w = Vector3.Cross(ax, u);
            int s0 = V.Count;
            for (int i = 0; i <= sides; i++)
            {
                float t = i * Mathf.PI * 2 / sides; var d = u * Mathf.Cos(t) + w * Mathf.Sin(t);
                var slope = (r - r2) / Mathf.Max(0.001f, Vector3.Distance(a, b));
                var n = (d + ax * slope).normalized;
                Add(a + d * r, n, col); Add(b + d * r2, n, col);
            }
            for (int i = 0; i < sides; i++) { int k = s0 + i * 2; TriAuto(k, k + 1, k + 2); TriAuto(k + 1, k + 3, k + 2); }
            if (!caps) return;
            var cc = capCol ?? col;
            foreach (var (p, rr, n) in new[] { (a, r, -ax), (b, r2, ax) })
            {
                if (rr <= 0.0001f) continue;
                int c0 = Add(p, n, cc);
                for (int i = 0; i <= sides; i++) { float t = i * Mathf.PI * 2 / sides; Add(p + (u * Mathf.Cos(t) + w * Mathf.Sin(t)) * rr, n, cc); }
                for (int i = 0; i < sides; i++) TriAuto(c0, c0 + 1 + i, c0 + 2 + i);
            }
        }

        /// <summary>Toroide en el plano XY de 'rot' (normal = rot*forward). arc en grados.</summary>
        public void Torus(Vector3 c, Quaternion rot, float R, float r, Color col, int seg = 32, int sides = 8, float arc = 360, float start = 0)
        {
            int s0 = V.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = (start + arc * i / seg) * Mathf.Deg2Rad; var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2 / sides; var n = o * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    Add(c + rot * (o * R + n * r), rot * n, col);
                }
            }
            int st = sides + 1;
            for (int i = 0; i < seg; i++) for (int j = 0; j < sides; j++)
                { int a = s0 + i * st + j, b = a + 1, d = a + st, e = d + 1; TriAuto(a, d, b); TriAuto(b, d, e); }
        }

        /// <summary>Superficie de revolución alrededor del eje Y de 'rot'. Perfil (radio, altura); el sentido del perfil decide la cara visible.</summary>
        public void Lathe(Vector3 c, Vector2[] prof, Color col, int seg = 24, Quaternion? rot = null, Func<int, int, Color> paint = null)
        {
            var q = rot ?? Quaternion.identity; int s0 = V.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int j = 0; j < prof.Length; j++)
                {
                    var t = prof[Mathf.Min(j + 1, prof.Length - 1)] - prof[Mathf.Max(0, j - 1)];
                    var n2 = new Vector2(t.y, -t.x).normalized;
                    var n = d * n2.x + Vector3.up * n2.y;
                    Add(c + q * (d * prof[j].x + Vector3.up * prof[j].y), q * n, paint != null ? paint(i, j) : col);
                }
            }
            int st = prof.Length;
            for (int i = 0; i < seg; i++) for (int j = 0; j < st - 1; j++)
                { int a = s0 + i * st + j, b = a + 1, d = a + st, e = d + 1; TriAuto(a, b, d); TriAuto(b, e, d); }
        }

        public void Sphere(Vector3 c, Vector3 radii, Color col, int seg = 12)
        {
            var prof = new Vector2[seg / 2 + 1];
            for (int i = 0; i < prof.Length; i++) { float a = -Mathf.PI / 2 + Mathf.PI * i / (prof.Length - 1); prof[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
            int s0 = V.Count; Lathe(Vector3.zero, prof, col, seg);
            for (int i = s0; i < V.Count; i++) { V[i] = c + Vector3.Scale(V[i], radii); N[i] = new Vector3(N[i].x / radii.x, N[i].y / radii.y, N[i].z / radii.z).normalized; }
        }

        // -------- Recorte 2D para franjas diagonales (puertas, bordillos, marcas de suelo) --------
        static List<Vector2> Clip(List<Vector2> poly, Vector2 n, float d)
        {
            var o = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                float da = Vector2.Dot(a, n) - d, db = Vector2.Dot(b, n) - d;
                if (da >= 0) o.Add(a);
                if (da * db < 0) o.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            return o;
        }
        /// <summary>Rectángulo con franjas diagonales alternas. origin = esquina, u/v = ejes (con largo), facing = normal visible.</summary>
        public void Stripes(Vector3 origin, Vector3 u, Vector3 v, float stripe, Color a, Color b, Vector3 facing, float lift = 0.004f)
        {
            float lu = u.magnitude, lv = v.magnitude; var du = u / lu; var dv = v / lv; var n2 = new Vector2(1, 1).normalized;
            float max = (lu + lv) * 0.7072f; int k = 0;
            for (float s = -stripe; s < max + stripe; s += stripe, k++)
            {
                var poly = new List<Vector2> { new(0, 0), new(lu, 0), new(lu, lv), new(0, lv) };
                poly = Clip(poly, n2, s); poly = Clip(poly, -n2, -(s + stripe));
                if (poly.Count < 3) continue;
                var pts = new List<Vector3>(); foreach (var p in poly) pts.Add(origin + du * p.x + dv * p.y + facing.normalized * lift);
                Poly(pts, k % 2 == 0 ? a : b, facing);
            }
        }

        // -------- Volcado a la escena --------
        public GameObject Flush(string name, Transform parent, Material mat, Vector3 origin = default, bool collider = false, bool shadows = true)
        {
            if (V.Count == 0) return null;
            var cols = new List<Color>(C);
            if (bake)
                for (int i = 0; i < V.Count; i++)
                {
                    var p = V[i]; var c = cols[i];
                    float ao = Mathf.Lerp(0.82f, 1f, Mathf.Clamp01((p.y + 0.3f) / 3.5f));
                    Color add = Color.black;
                    foreach (var l in Lamps)
                    {
                        float d = Vector3.Distance(p, l.p); if (d > l.r) continue;
                        float f = 1 - d / l.r; add += l.c * (f * f * 0.55f);
                    }
                    cols[i] = new Color(c.r * ao + add.r * c.r + add.r * 0.18f, c.g * ao + add.g * c.g + add.g * 0.18f, c.b * ao + add.b * c.b + add.b * 0.18f, c.a);
                }
            // Los colores de vértice no se corrigen solos: se pasan a espacio lineal para que se vean como en la paleta.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                for (int i = 0; i < cols.Count; i++) { var a = cols[i].a; cols[i] = cols[i].linear; var t = cols[i]; t.a = a; cols[i] = t; }
            var mesh = new Mesh { name = name };
            if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            var verts = new List<Vector3>(V.Count); foreach (var p in V) verts.Add(p - origin);
            mesh.SetVertices(verts); mesh.SetNormals(N); mesh.SetColors(cols); mesh.SetUVs(0, U); mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshDir + "/M" + (Serial++) + ".asset");
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = origin;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        public static Color Vary(Color c, System.Random rnd, float amount = 0.06f)
        {
            float k = 1 + ((float)rnd.NextDouble() * 2 - 1) * amount;
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
        }
    }
}
