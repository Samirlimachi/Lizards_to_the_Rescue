using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Sauce low-poly y cierre del arco.
    /// Sauce de la hoja "Sauce del pantano": tronco facetado con raíces, copa de pétalos anchos que se solapan
    /// y hojas colgantes finas por debajo. También tapa por detrás el arco de alcantarilla.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        [MenuItem("Lizards/Sauce low-poly y cierre del arco", priority = 17)]
        public static string BuildWillowLP()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            var trunk = LPTrunk(); var canopy = LPCanopy();
            int n = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>())
            {
                if (!mf.sharedMesh) continue; string mn = mf.sharedMesh.name;
                if (mn.Contains("Sauce_Tronco")) { mf.sharedMesh = trunk; n++; }
                else if (mn.Contains("Sauce_Copa") || mn.StartsWith("Sauce_Hojas")) { mf.sharedMesh = canopy; mf.GetComponent<MeshRenderer>().sharedMaterial = vtx; }
            }
            string log = $" {n} sauces low-poly;" + CloseArchBack();
            EditorSceneManager.MarkSceneDirty(l.scene); EditorSceneManager.SaveScene(l.scene); AssetDatabase.SaveAssets();
            return log;
        }

        static void Quad(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col)
        {
            // Doble cara con vértices propios por cara (si se comparten, las normales se anulan y sale negro).
            int s = v.Count; v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            for (int i = 0; i < 4; i++) c.Add(col.linear);
            t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);
            s = v.Count; v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
            for (int i = 0; i < 4; i++) c.Add((col * 0.85f).linear);
            t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
        }

        static Mesh Finish(string name, List<Vector3> v, List<Color> c, List<int> t)
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return SaveNew(m);
        }

        /// <summary>Copa: pétalos curvos alrededor de una cúpula (con grosor) + hojas colgantes finas.</summary>
        static Mesh LPCanopy()
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            var rnd = new System.Random(4);
            Color top = new Color(0.45f, 0.75f, 0.35f), low = new Color(0.25f, 0.55f, 0.4f);
            // Cúpula interior oscura para tapar huecos.
            const int ring = 14; const float R = 3.0f, Cy = 6.0f;
            for (int i = 0; i < ring; i++)
            {
                float a0 = i / (float)ring * Mathf.PI * 2f, a1 = (i + 1) / (float)ring * Mathf.PI * 2f;
                for (int k = 0; k < 4; k++)
                {
                    float e0 = k / 4f * Mathf.PI * 0.5f, e1 = (k + 1) / 4f * Mathf.PI * 0.5f;
                    Vector3 P(float a, float e) => new Vector3(Mathf.Cos(a) * Mathf.Cos(e) * (R - 0.25f), Cy + Mathf.Sin(e) * 1.9f - 0.3f, Mathf.Sin(a) * Mathf.Cos(e) * (R - 0.25f));
                    Quad(v, c, t, P(a0, e0), P(a0, e1), P(a1, e1), P(a1, e0), Color.Lerp(low * 0.8f, top * 0.9f, k / 4f));
                }
            }
            // Pétalos: 16 tiras anchas desde la coronilla hasta abajo, cada una con 5 tramos, terminadas en punta.
            const int petals = 16;
            for (int i = 0; i < petals; i++)
            {
                float a = i / (float)petals * Mathf.PI * 2f + (float)rnd.NextDouble() * 0.08f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), side = new Vector3(-dir.z, 0, dir.x);
                float w = 0.95f + (float)rnd.NextDouble() * 0.2f, len = (float)rnd.NextDouble() * 0.6f;
                Vector3[] spine = new Vector3[6];
                for (int k = 0; k < 6; k++)
                {
                    float s = k / 5f; float e = Mathf.Lerp(Mathf.PI * 0.5f, -0.45f - len * 0.3f, s);
                    float r = R * Mathf.Cos(Mathf.Max(e, 0f)) + (e < 0 ? 0.15f : 0f);
                    spine[k] = dir * r + Vector3.up * (Cy + Mathf.Sin(e) * (e > 0 ? 2.0f : 2.4f));
                }
                for (int k = 0; k < 5; k++)
                {
                    float w0 = w * Mathf.Sin(Mathf.Lerp(0.25f, 1f, k / 5f) * Mathf.PI * 0.62f), w1 = k == 4 ? 0.05f : w * Mathf.Sin(Mathf.Lerp(0.25f, 1f, (k + 1) / 5f) * Mathf.PI * 0.62f);
                    Color col = Color.Lerp(top, low, k / 5f) * (i % 2 == 0 ? 1f : 0.92f);
                    Quad(v, c, t, spine[k] - side * w0, spine[k] + side * w0, spine[k + 1] + side * w1, spine[k + 1] - side * w1, col);
                }
                // Hojas colgantes finas bajo el pétalo.
                for (int h = 0; h < 2; h++)
                {
                    Vector3 o = spine[4] + side * (h == 0 ? -0.35f : 0.35f) - dir * 0.15f;
                    float hl = 1.4f + (float)rnd.NextDouble() * 1.4f, hw = 0.08f + (float)rnd.NextDouble() * 0.05f;
                    Quad(v, c, t, o - side * hw, o + side * hw, o + Vector3.down * hl + side * 0.01f, o + Vector3.down * hl - side * 0.01f, low * 0.95f);
                }
            }
            return Finish("Kit_SauceLP_Copa", v, c, t);
        }

        /// <summary>Tronco facetado (8 lados) que se abre en raíces abajo y en dos ramas arriba.</summary>
        static Mesh LPTrunk()
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            Color bark = new Color(0.72f, 0.42f, 0.3f);
            void Tube(Vector3 a, Vector3 b, float ra, float rb, int sides, Color col)
            {
                Vector3 axis = (b - a).normalized; Vector3 u = Vector3.Cross(axis, Vector3.forward).sqrMagnitude > 0.01f ? Vector3.Cross(axis, Vector3.forward).normalized : Vector3.right; Vector3 w = Vector3.Cross(axis, u);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                    Vector3 d0 = u * Mathf.Cos(a0) + w * Mathf.Sin(a0), d1 = u * Mathf.Cos(a1) + w * Mathf.Sin(a1);
                    int s = v.Count; v.Add(a + d0 * ra); v.Add(b + d0 * rb); v.Add(b + d1 * rb); v.Add(a + d1 * ra);
                    Color cc = col * (0.85f + (i % 3) * 0.08f);
                    for (int k = 0; k < 4; k++) c.Add(cc.linear);
                    t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);
                }
            }
            Tube(new Vector3(0, -0.2f, 0), new Vector3(0.1f, 2.2f, 0), 0.75f, 0.5f, 8, bark);
            Tube(new Vector3(0.1f, 2.1f, 0), new Vector3(-0.25f, 4.6f, 0.1f), 0.52f, 0.38f, 8, bark);
            Tube(new Vector3(-0.2f, 4.4f, 0.05f), new Vector3(-1.1f, 5.8f, 0.2f), 0.3f, 0.15f, 6, bark);
            Tube(new Vector3(-0.2f, 4.4f, 0.05f), new Vector3(0.9f, 5.9f, -0.2f), 0.3f, 0.15f, 6, bark);
            for (int r = 0; r < 5; r++)
            {
                float a = r * 1.257f + 0.3f; Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Tube(d * 0.3f + Vector3.up * 0.9f, d * 1.8f + Vector3.down * 0.05f, 0.35f, 0.08f, 5, bark * 0.95f);
            }
            return Finish("Kit_SauceLP_Tronco", v, c, t);
        }

        // ---------------- Arco de alcantarilla: tapa trasera ----------------

        static string CloseArchBack()
        {
            var arch = lobby.Find("Arco de alcantarilla (generado)");
            if (!arch) return "";
            var old = arch.Find("Túnel"); if (old) Object.DestroyImmediate(old.gameObject);
            // Túnel corto de piedra que se mete en una loma de tierra y musgo: por detrás ya no hay hueco.
            var tunnel = Sdf(p =>
            {
                float rr = new Vector2(p.x, p.y).magnitude;
                float shell = Mathf.Max(Mathf.Max(rr - 3.9f, 2.8f - rr), Mathf.Max(-p.y, Mathf.Abs(p.z - 2.2f) - 1.6f));
                float back = Mathf.Max(Mathf.Max(rr - 3.9f, -p.y), Mathf.Abs(p.z - 3.7f) - 0.15f);
                return Mathf.Min(shell, back) - 0.04f;
            }, new Bounds(new Vector3(0, 2f, 2.2f), new Vector3(8.4f, 4.4f, 3.8f)), 0.06f, "Kit_ArcoTunel", (p, n) => n.y > 0.5f ? new Color(0.4f, 0.8f, 0.28f) : new Color(0.32f, 0.33f, 0.37f));
            var tg = Part("Túnel", arch, tunnel); tg.AddComponent<MeshCollider>();
            var mound = LowPolyRock(21);
            var mg = Part("Loma", tg.transform, mound);
            mg.transform.localPosition = new Vector3(0, 0.6f, 3.4f); mg.transform.localScale = new Vector3(11f, 10f, 7f);
            mg.AddComponent<MeshCollider>();
            return " arco cerrado por detrás.";
        }
    }
}
