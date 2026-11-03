using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Ambiente del pantano (rocas, hongos, islotes, colinas).
    /// Rocas redondeadas con gorro de musgo agrupadas en peñascos, hongos rojos de motas blancas,
    /// islotes en el mar con sauces, hongos y nenúfares, y colinas y montañas en el horizonte.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        [MenuItem("Lizards/Ambiente del pantano (rocas, hongos, islotes, colinas)", priority = 18)]
        public static string BuildAmbience()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            var boulders = new[] { SoftBoulder(1), SoftBoulder(2), SoftBoulder(3) };
            var mush = SpottedMushrooms();
            string log = ReplaceRocks(boulders) + ReplaceMushrooms(mush) + Islets(boulders, mush) + Horizon();
            EditorSceneManager.MarkSceneDirty(l.scene); EditorSceneManager.SaveScene(l.scene); AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Ambiente:" + log);
            return log;
        }

        // ---------------- Rocas redondeadas con musgo ----------------

        static Mesh SoftBoulder(int seed)
        {
            var mesh = Sdf(p =>
            {
                var r = new System.Random(seed * 53 + 1);
                float d = SdfMesher.Ellipsoid(p, new Vector3(0, 0.1f, 0), new Vector3(1f, 0.8f, 0.9f));
                for (int i = 0; i < 4; i++)
                {
                    var c = new Vector3((float)r.NextDouble() * 1.2f - 0.6f, (float)r.NextDouble() * 0.6f - 0.1f, (float)r.NextDouble() * 1.2f - 0.6f);
                    d = SdfMesher.SMin(d, (p - c).magnitude - (0.45f + (float)r.NextDouble() * 0.25f), 0.25f);
                }
                return Mathf.Max(d, -0.45f - p.y);
            }, new Bounds(new Vector3(0, 0.15f, 0), new Vector3(2.8f, 2.2f, 2.8f)), 0.14f, "Amb_Roca_" + seed, (p, n) =>
            {
                float cap = 0.25f + Mathf.Sin(Mathf.Atan2(p.z, p.x) * 5f + seed) * 0.12f;
                if (n.y > 0.5f && p.y > cap) return Color.Lerp(new Color(0.36f, 0.72f, 0.26f), new Color(0.5f, 0.86f, 0.32f), n.y);   // gorro de musgo
                return Color.Lerp(new Color(0.78f, 0.7f, 0.56f), new Color(0.9f, 0.84f, 0.7f), Mathf.Clamp01(n.y * 0.5f + 0.5f));      // piedra beige
            });
            return Facet(mesh); // caras suaves y grandes, estilo pintado
        }

        static string ReplaceRocks(Mesh[] b)
        {
            int n = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>())
            {
                if (!mf.sharedMesh || !mf.sharedMesh.name.StartsWith("Kit_RocaLP_")) continue;
                if (mf.name == "Roca grande" || mf.name == "Loma") continue; // los cierres grandes se quedan
                var r = mf.GetComponent<Renderer>(); float bottom = r.bounds.min.y;
                mf.sharedMesh = b[n % 3];
                var mc = mf.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = b[n % 3];
                mf.transform.localScale *= 0.55f;
                mf.transform.position += Vector3.up * (bottom - r.bounds.min.y);
                n++;
            }
            return $" {n} rocas redondeadas;";
        }

        // ---------------- Hongos de motas ----------------

        static Mesh SpottedMushrooms()
        {
            var specs = new[] { (Vector3.zero, 1f), (new Vector3(0.95f, 0, 0.35f), 0.55f), (new Vector3(-0.8f, 0, 0.55f), 0.45f), (new Vector3(0.3f, 0, -0.85f), 0.38f) };
            // Motas en coordenadas locales de un hongo de tamaño 1 (se reutilizan en todos).
            var spots = new List<Vector3>();
            var rnd = new System.Random(3);
            for (int i = 0; i < 11; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, e = 0.2f + (float)rnd.NextDouble() * 1.1f;
                spots.Add(new Vector3(Mathf.Cos(ang) * Mathf.Cos(e), 1.32f + Mathf.Sin(e) * 0.68f, Mathf.Sin(ang) * Mathf.Cos(e)));
            }
            return Sdf(p =>
            {
                float d = float.MaxValue;
                foreach (var (o, s) in specs)
                {
                    Vector3 q = (p - o) / s;
                    float stem = Mathf.Max(new Vector2(q.x, q.z).magnitude - (0.26f + Mathf.Exp(-q.y * 3f) * 0.12f), Mathf.Max(-q.y, q.y - 1.45f));
                    float cap = Mathf.Max(SdfMesher.Ellipsoid(q, new Vector3(0, 1.32f, 0), new Vector3(1f, 0.68f, 1f)), 1.22f - q.y + new Vector2(q.x, q.z).magnitude * 0.1f);
                    d = Mathf.Min(d, SdfMesher.SMin(stem, cap, 0.08f) * s);
                }
                return d;
            }, new Bounds(new Vector3(0, 1f, 0), new Vector3(3.6f, 2.4f, 3.4f)), 0.03f, "Amb_Hongos", (p, n) =>
            {
                // Color según el hongo más cercano, en sus coordenadas locales.
                Vector3 q = p; float best = float.MaxValue;
                foreach (var (o, s2) in specs) { Vector3 l = (p - o) / s2; float dd = (new Vector2(l.x, l.z)).magnitude; if (dd < best) { best = dd; q = l; } }
                if (q.y < 1.22f || n.y < -0.2f) return n.y < -0.2f && q.y > 0.9f ? new Color(0.9f, 0.82f, 0.68f) : new Color(0.96f, 0.92f, 0.82f);
                foreach (var sp in spots) if ((q - sp).sqrMagnitude < 0.03f) return new Color(1f, 0.98f, 0.95f);
                return Color.Lerp(new Color(0.82f, 0.13f, 0.12f), new Color(0.98f, 0.28f, 0.22f), Mathf.Clamp01(n.y));
            });
        }

        static string ReplaceMushrooms(Mesh m)
        {
            int n = 0;
            var root = lobby.Find("Setas (generadas)");
            if (root) foreach (Transform t in root) { var mf = t.GetComponent<MeshFilter>(); if (mf) { mf.sharedMesh = m; t.localScale = Vector3.one * 1.1f; n++; } }
            return $" {n} grupos de hongos de motas;";
        }

        // ---------------- Islotes alrededor ----------------

        static string Islets(Mesh[] rocks, Mesh mush)
        {
            var old = lobby.Find("Islotes (generados)"); if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Islotes (generados)").transform; root.SetParent(lobby, false);
            var trunk = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Kit_SauceLP_Tronco.asset");
            var canopy = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Kit_SauceLP_Copa.asset");
            var pad = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Estanque_Nenufar.asset");
            var islet = Sdf(p =>
            {
                float d = SdfMesher.Ellipsoid(p, Vector3.zero, new Vector3(4f, 1.2f, 3.4f));
                d = SdfMesher.SMin(d, SdfMesher.Ellipsoid(p, new Vector3(2.2f, 0, 1.4f), new Vector3(2.4f, 0.9f, 2.2f)), 0.6f);
                return Mathf.Max(d, -0.8f - p.y);
            }, new Bounds(Vector3.zero, new Vector3(11f, 3f, 9f)), 0.12f, "Amb_Islote", (p, n) => n.y > 0.55f ? new Color(0.36f, 0.68f, 0.26f) : new Color(0.86f, 0.78f, 0.58f));
            const float sea = -0.65f; Vector3 center = new Vector3(0, 0, 8f);
            var rnd = new System.Random(77);
            int count = 0;
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.15f;
                float rad = 64f + (float)rnd.NextDouble() * 22f;
                Vector3 pos = center + new Vector3(Mathf.Cos(a) * rad, sea - 0.1f, Mathf.Sin(a) * rad);
                if (pos.z > 46f && Mathf.Abs(pos.x) < 55f) continue; // tras la fábrica ya hay cierre
                var g = new GameObject("Islote").transform; g.SetParent(root, false);
                g.position = pos; g.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0); g.localScale = Vector3.one * (0.9f + (float)rnd.NextDouble() * 0.6f);
                Part("Tierra", g, islet);
                int trees = 1 + rnd.Next(3);
                for (int k = 0; k < trees && trunk && canopy; k++)
                {
                    var tr = new GameObject("Sauce").transform; tr.SetParent(g, false);
                    tr.localPosition = new Vector3(-1.5f + k * 1.8f, 0.9f, (float)rnd.NextDouble() * 1.6f - 0.8f); tr.localScale = Vector3.one * (0.7f + (float)rnd.NextDouble() * 0.3f);
                    tr.localRotation = Quaternion.Euler(0, k * 77f, 0);
                    Part("Tronco", tr, trunk); Part("Copa", tr, canopy);
                }
                if (rnd.NextDouble() < 0.6) { var m = Part("Hongos", g, mush); m.transform.localPosition = new Vector3(2.4f, 0.75f, 1.5f); m.transform.localScale = Vector3.one * 0.9f; }
                var rk = Part("Roca", g, rocks[i % 3]); rk.transform.localPosition = new Vector3(3.2f, 0.1f, -1.4f); rk.transform.localScale = Vector3.one * 1.3f;
                for (int k = 0; k < 3 && pad; k++)
                {
                    var lp = Part("Nenúfar", root, pad);
                    lp.transform.position = pos + new Vector3((float)rnd.NextDouble() * 12f - 6f, 0.12f, (float)rnd.NextDouble() * 12f - 6f) + (pos - center).normalized * -7f;
                    lp.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0); lp.transform.localScale = Vector3.one * (0.6f + (float)rnd.NextDouble() * 0.6f);
                }
                count++;
            }
            return $" {count} islotes;";
        }

        // ---------------- Colinas y montañas en el horizonte ----------------

        static string Horizon()
        {
            var old = lobby.Find("Horizonte (generado)"); if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Horizonte (generado)").transform; root.SetParent(lobby, false);
            var hill = Sdf(p => Mathf.Max(SdfMesher.Ellipsoid(p, Vector3.zero, new Vector3(1f, 1f, 1f)) + (N(p, 3f) - 0.5f) * 0.08f, -p.y),
                new Bounds(new Vector3(0, 0.5f, 0), new Vector3(2.4f, 1.3f, 2.4f)), 0.05f, "Amb_Colina", (p, n) => Color.Lerp(new Color(0.3f, 0.52f, 0.42f), new Color(0.45f, 0.7f, 0.42f), p.y));
            var far = Mat("Amb_Lejano", () => { var m = LizardMaterials.Toon(new Color(0.55f, 0.68f, 0.72f), 0f, 0f, new Color(0.5f, 0.6f, 0.75f)); m.SetFloat("_VertexColorWeight", 0.35f); return m; });
            var rnd = new System.Random(5);
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * Mathf.PI * 2f; float rad = 135f + (float)rnd.NextDouble() * 35f;
                var h = Part(i % 2 == 0 ? "Montaña" : "Colina", root, hill, i % 2 == 0 ? far : vtx);
                h.transform.position = new Vector3(Mathf.Cos(a) * rad, -1.5f, 8f + Mathf.Sin(a) * rad);
                float w = 22f + (float)rnd.NextDouble() * 18f;
                h.transform.localScale = new Vector3(w, (i % 2 == 0 ? 28f : 14f) + (float)rnd.NextDouble() * 10f, w * 0.8f);
                h.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                h.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return " colinas y montañas en el horizonte.";
        }
    }
}
