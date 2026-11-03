using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Arreglar kit (rocas, sauces, hongos, embarcadero).
    /// Rocas low-poly de verdad (pocas caras, musgo por cara), copa de sauce con tiras separadas,
    /// hongos de prismas limpios a escala razonable y el embarcadero sin rocas atravesándolo.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        [MenuItem("Lizards/Arreglar kit (rocas, sauces, hongos, embarcadero)", priority = 15)]
        public static string BuildKitFixes()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            string log = CleanRocks() + WillowCanopyV3() + PrismMushrooms() + DockFix();
            EditorSceneManager.MarkSceneDirty(l.scene);
            EditorSceneManager.SaveScene(l.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Kit arreglado:" + log);
            return log;
        }

        static Mesh SaveNew(Mesh m)
        {
            string path = $"{Dir}/{m.name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ---------------- Rocas low-poly ----------------

        /// <summary>Icosaedro subdividido una vez, con los vértices desplazados al azar y la base aplanada; caras planas y musgo por cara.</summary>
        static Mesh LowPolyRock(int seed)
        {
            var rnd = new System.Random(seed * 31 + 7);
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3> { new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1) };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            var faces = new List<int[]> { new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 }, new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
                new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 }, new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 } };
            var mid = new Dictionary<long, int>();
            int Mid(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (mid.TryGetValue(key, out int i)) return i;
                verts.Add(((verts[a] + verts[b]) * 0.5f).normalized); mid[key] = verts.Count - 1; return verts.Count - 1;
            }
            var sub = new List<int[]>();
            foreach (var f in faces) { int a = Mid(f[0], f[1]), b = Mid(f[1], f[2]), c = Mid(f[2], f[0]); sub.Add(new[] { f[0], a, c }); sub.Add(new[] { f[1], b, a }); sub.Add(new[] { f[2], c, b }); sub.Add(new[] { a, b, c }); }
            // Forma: radio al azar por vértice, algo achatada, base plana.
            for (int i = 0; i < verts.Count; i++)
            {
                var v = verts[i] * (0.42f + (float)rnd.NextDouble() * 0.14f);
                v.y *= 0.8f; if (v.y < -0.22f) v.y = -0.22f;
                verts[i] = v;
            }
            var nv = new List<Vector3>(); var nc = new List<Color>(); var nt = new List<int>();
            Color stoneA = new Color(0.82f, 0.77f, 0.66f), stoneB = new Color(0.9f, 0.86f, 0.76f), mossA = new Color(0.36f, 0.76f, 0.25f), mossB = new Color(0.46f, 0.88f, 0.3f);
            int fi = 0;
            foreach (var f in sub)
            {
                Vector3 a = verts[f[0]], b = verts[f[1]], c = verts[f[2]];
                Vector3 n = Vector3.Cross(b - a, c - a).normalized; Vector3 centroid = (a + b + c) / 3f;
                if (Vector3.Dot(n, centroid) < 0f) { var tmp = b; b = c; c = tmp; n = -n; }
                // Musgo en las caras de arriba, y en algunas laterales altas (como si goteara).
                bool moss = n.y > 0.55f || (n.y > 0.15f && centroid.y > 0.12f && (fi * 7 % 5) < 3);
                Color col = moss ? Color.Lerp(mossA, mossB, (fi * 13 % 7) / 7f) : Color.Lerp(stoneA, stoneB, (fi * 11 % 5) / 5f);
                int s = nv.Count; nv.Add(a); nv.Add(b); nv.Add(c);
                nc.Add(col.linear); nc.Add(col.linear); nc.Add(col.linear);
                nt.Add(s); nt.Add(s + 1); nt.Add(s + 2);
                fi++;
            }
            var m = new Mesh { name = "Kit_RocaLP_" + seed };
            m.SetVertices(nv); m.SetColors(nc); m.SetTriangles(nt, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return SaveNew(m);
        }

        static string CleanRocks()
        {
            var variants = new[] { LowPolyRock(1), LowPolyRock(2), LowPolyRock(3), LowPolyRock(4) };
            var dock = GameObject.Find("Embarcadero elevado");
            Bounds dockB = new Bounds();
            bool hasDock = false;
            if (dock) { foreach (var r in dock.GetComponentsInChildren<Renderer>()) { if (!hasDock) { dockB = r.bounds; hasDock = true; } else dockB.Encapsulate(r.bounds); } dockB.Expand(new Vector3(1f, 0f, 1f)); }
            int n = 0, removed = 0;
            var kill = new List<GameObject>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>())
            {
                if (!mf || !mf.sharedMesh || !(mf.sharedMesh.name.StartsWith("Kit_Roca_") || mf.sharedMesh.name.StartsWith("Kit_RocaLP_"))) continue;
                var t = mf.transform;
                var r = mf.GetComponent<Renderer>();
                if (hasDock && r && r.bounds.Intersects(dockB) && t.parent == lobby) { kill.Add(mf.gameObject); removed++; continue; }
                Bounds before = r ? r.bounds : new Bounds(t.position, Vector3.one);
                // Proporciones razonables: nada de placas altísimas ni láminas finas.
                Vector3 s = t.localScale;
                float flat = (s.x + s.z) * 0.5f;
                s.x = Mathf.Clamp(s.x, flat * 0.7f, flat * 1.3f); s.z = Mathf.Clamp(s.z, flat * 0.7f, flat * 1.3f);
                s.y = Mathf.Clamp(s.y, flat * 0.45f, flat * 0.95f);
                t.localScale = s;
                var nm = variants[n % variants.Length];
                mf.sharedMesh = nm;
                var mc = mf.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = nm;
                // Apoyada en el mismo suelo que antes.
                if (r) { float dy = before.min.y - r.bounds.min.y; t.position += Vector3.up * dy; }
                n++;
            }
            foreach (var g in kill) Object.DestroyImmediate(g);
            return $" {n} rocas low-poly ({removed} quitadas del embarcadero);";
        }

        // ---------------- Copa del sauce ----------------

        static string WillowCanopyV3()
        {
            var canopy = Sdf(p =>
            {
                // Cúpula redonda (sin cortar) y tiras separadas que cuelgan del borde.
                float d = SdfMesher.Ellipsoid(p, new Vector3(0, 6.1f, 0), new Vector3(3.1f, 1.7f, 3.1f));
                for (int i = 0; i < 18; i++)
                {
                    float a = i / 18f * Mathf.PI * 2f; Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    float len = 1.7f + (i % 3) * 0.35f;
                    Vector3 q = Quaternion.Inverse(Quaternion.LookRotation(dir)) * p;
                    q.z -= 2.85f + (6.0f - p.y) * 0.06f;
                    float strip = Box(q, new Vector3(0, 6.0f - len * 0.5f, 0), new Vector3(0.24f, len * 0.5f, 0.16f), 0.15f);
                    d = Mathf.Min(d, strip);
                }
                return d;
            }, new Bounds(new Vector3(0, 5.6f, 0), new Vector3(7.4f, 4.8f, 7.4f)), 0.045f, "Kit_Sauce_Copa3", (p, n) =>
            {
                float k = Mathf.Clamp01((p.y - 3.8f) / 4f);
                Color c = Color.Lerp(new Color(0.16f, 0.45f, 0.36f), new Color(0.42f, 0.8f, 0.32f), k);
                if (n.y > 0.75f) c = Color.Lerp(c, new Color(0.55f, 0.9f, 0.38f), 0.4f);
                return c;
            });
            int count = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>())
                if (mf.sharedMesh && (mf.sharedMesh.name == "Kit_Sauce_Copa" || mf.sharedMesh.name.StartsWith("Sauce_Hojas"))) { mf.sharedMesh = canopy; count++; }
            return $" {count} copas de sauce;";
        }

        // ---------------- Hongos de prismas ----------------

        static void Prism(List<Vector3> v, List<Color> c, List<int> t, Vector3 center, float r0, float r1, float y0, float y1, int sides, Color col, float twist = 0f)
        {
            Color lc = col.linear, top = (col * 1.12f).linear, bottom = (col * 0.75f).linear;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (i / (float)sides) * Mathf.PI * 2f + twist, a1 = ((i + 1) / (float)sides) * Mathf.PI * 2f + twist;
                Vector3 b0 = center + new Vector3(Mathf.Cos(a0) * r0, y0, Mathf.Sin(a0) * r0), b1 = center + new Vector3(Mathf.Cos(a1) * r0, y0, Mathf.Sin(a1) * r0);
                Vector3 u0 = center + new Vector3(Mathf.Cos(a0) * r1, y1, Mathf.Sin(a0) * r1), u1 = center + new Vector3(Mathf.Cos(a1) * r1, y1, Mathf.Sin(a1) * r1);
                int s = v.Count;
                v.Add(b0); v.Add(u0); v.Add(u1); v.Add(b1); c.Add(lc); c.Add(lc); c.Add(lc); c.Add(lc);
                t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
                int k = v.Count; v.Add(center + Vector3.up * y1); v.Add(u1); v.Add(u0); c.Add(top); c.Add(top); c.Add(top); t.Add(k); t.Add(k + 1); t.Add(k + 2);
                k = v.Count; v.Add(center + Vector3.up * y0); v.Add(b0); v.Add(b1); c.Add(bottom); c.Add(bottom); c.Add(bottom); t.Add(k); t.Add(k + 1); t.Add(k + 2);
            }
        }

        static string PrismMushrooms()
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            Color red = new Color(0.93f, 0.27f, 0.2f), stem = new Color(0.44f, 0.57f, 0.57f), under = new Color(0.72f, 0.2f, 0.16f);
            void Mush(Vector3 at, float s, float tw)
            {
                Prism(v, c, t, at, 0.3f * s, 0.26f * s, 0f, 1.3f * s, 6, stem, tw);                       // tallo
                Prism(v, c, t, at, 0.55f * s, 1.05f * s, 1.25f * s, 1.5f * s, 6, under, tw + 0.3f);       // bajo del sombrero
                Prism(v, c, t, at, 1.05f * s, 0.95f * s, 1.5f * s, 1.78f * s, 6, red, tw + 0.3f);         // sombrero plano
                Prism(v, c, t, at, 0.95f * s, 0.55f * s, 1.78f * s, 1.92f * s, 6, red, tw + 0.3f);        // bisel de arriba
            }
            Mush(Vector3.zero, 1f, 0f);
            Mush(new Vector3(1.15f, 0, 0.45f), 0.5f, 0.4f);
            Mush(new Vector3(-0.95f, 0, 0.65f), 0.42f, 0.9f);
            Mush(new Vector3(0.55f, 0, -1.0f), 0.36f, 0.2f);
            Mush(new Vector3(-0.7f, 0, -0.75f), 0.3f, 1.3f);
            var m = new Mesh { name = "Kit_HongosPrisma" };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            SaveNew(m);
            int n = 0;
            var root = lobby.Find("Setas (generadas)");
            if (root) foreach (Transform tr in root)
                {
                    var mf = tr.GetComponent<MeshFilter>(); if (!mf) continue;
                    mf.sharedMesh = m;
                    // Tamaño de juego: el grande ~2.5 m de alto, no gigante.
                    float s = Mathf.Clamp(tr.localScale.x * 0.62f, 0.9f, 1.35f);
                    tr.localScale = Vector3.one * s;
                    var cc = tr.GetComponent<CapsuleCollider>(); if (cc) { cc.center = new Vector3(0, 0.75f, 0); cc.radius = 0.3f; cc.height = 1.5f; }
                    n++;
                }
            return $" {n} grupos de hongos;";
        }

        // ---------------- Embarcadero ----------------

        static string DockFix() => RaisedDock();
    }
}
