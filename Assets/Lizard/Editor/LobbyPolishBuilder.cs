using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Pulir suelo y alcantarillas.
    /// Pinta el terreno (césped con manchas de luz y sombra, tierra en los caminos y pendientes, arena húmeda en la orilla)
    /// y rehace las alcantarillas con brida, remaches, óxido y musgo, con el chorro saliendo de la boca.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        [MenuItem("Lizards/Pulir suelo y alcantarillas", priority = 19)]
        public static string BuildPolish()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            string log = PaintTerrain() + PolishPipes();
            EditorSceneManager.MarkSceneDirty(l.scene); EditorSceneManager.SaveScene(l.scene); AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Pulido:" + log);
            return log;
        }

        static string PaintTerrain()
        {
            var t = GameObject.Find("Terreno · lomas y ensenada");
            if (!t) return "";
            var mf = t.GetComponent<MeshFilter>(); var mesh = mf.sharedMesh;
            var v = mesh.vertices; var n = mesh.normals; var c = new Color[v.Length];
            Color grassA = new(0.34f, 0.66f, 0.24f), grassB = new(0.46f, 0.78f, 0.3f), grassDark = new(0.24f, 0.5f, 0.2f), flowerTint = new(0.6f, 0.82f, 0.32f);
            Color dirt = new(0.55f, 0.42f, 0.28f), sand = new(0.9f, 0.82f, 0.62f), wetSand = new(0.66f, 0.6f, 0.44f);
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = t.transform.TransformPoint(v[i]);
                Vector3 nw = n.Length == v.Length ? t.transform.TransformDirection(n[i]).normalized : Vector3.up;
                float big = Mathf.PerlinNoise(w.x * 0.06f + 3f, w.z * 0.06f + 7f), small = Mathf.PerlinNoise(w.x * 0.35f, w.z * 0.35f);
                Color col = Color.Lerp(grassA, grassB, big);
                col = Color.Lerp(col, grassDark, Mathf.Clamp01((0.35f - small) * 2.5f) * 0.5f);   // manchas de sombra
                col = Color.Lerp(col, flowerTint, Mathf.Clamp01((small - 0.72f) * 4f) * 0.6f);     // claros soleados
                // Pendientes fuertes: tierra.
                float slope = 1f - Mathf.Clamp01(nw.y);
                col = Color.Lerp(col, dirt, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.7f, slope)));
                // Orilla: arena seca y luego húmeda cerca del agua (sin borde verde duro).
                float h = w.y;
                float beach = Mathf.InverseLerp(0.35f, -0.15f, h);
                col = Color.Lerp(col, sand, Mathf.SmoothStep(0f, 1f, beach));
                col = Color.Lerp(col, wetSand, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, -0.65f, h)));
                c[i] = col.linear;
            }
            mesh.colors = c; EditorUtility.SetDirty(mesh);
            var mr = t.GetComponent<MeshRenderer>(); var mat = mr.sharedMaterial;
            if (mat.HasProperty("_VertexColorWeight")) { mat.SetFloat("_VertexColorWeight", 1f); mat.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(mat); }
            return " suelo pintado;";
        }

        static string PolishPipes()
        {
            var pipe = Sdf(p =>
            {
                float r = new Vector2(p.x, p.y).magnitude;
                float tube = Mathf.Max(Mathf.Abs(r - 0.6f) - 0.09f, Mathf.Abs(p.z - 1.1f) - 1.35f);
                // Brida de boca con remaches y dos anillos de refuerzo.
                tube = Mathf.Min(tube, Mathf.Max(Mathf.Abs(r - 0.72f) - 0.13f, Mathf.Abs(p.z - 2.4f) - 0.09f));
                for (int k = 0; k < 2; k++) tube = Mathf.Min(tube, Mathf.Max(Mathf.Abs(r - 0.69f) - 0.06f, Mathf.Abs(p.z - (0.4f + k * 0.9f)) - 0.07f));
                for (int k = 0; k < 8; k++)
                {
                    float a = k / 8f * Mathf.PI * 2f;
                    tube = Mathf.Min(tube, (p - new Vector3(Mathf.Cos(a) * 0.78f, Mathf.Sin(a) * 0.78f, 2.5f)).magnitude - 0.055f);
                }
                return tube;
            }, new Bounds(new Vector3(0, 0, 1.2f), new Vector3(2f, 2f, 3.2f)), 0.025f, "Pulido_Tuberia", (p, n) =>
            {
                float r = new Vector2(p.x, p.y).magnitude;
                if (r < 0.55f) return p.z > 2.2f ? new Color(0.16f, 0.32f, 0.3f) : new Color(0.06f, 0.1f, 0.1f);       // interior oscuro y mojado
                Color metal = Color.Lerp(new Color(0.28f, 0.62f, 0.58f), new Color(0.48f, 0.82f, 0.74f), Mathf.Clamp01(n.y * 0.5f + 0.5f));
                if (r > 0.74f && p.z > 2.3f) metal = new Color(0.62f, 0.66f, 0.68f);                                      // remaches
                else if (r > 0.66f) metal *= 0.82f;                                                                      // brida y anillos
                float rust = Mathf.PerlinNoise(p.x * 4f + 10f, p.z * 3f);
                if (n.y < -0.2f && rust > 0.55f) metal = Color.Lerp(metal, new Color(0.62f, 0.36f, 0.2f), 0.6f);        // óxido abajo
                if (n.y > 0.55f && Mathf.PerlinNoise(p.z * 2.5f, p.x * 5f) > 0.5f) metal = new Color(0.42f, 0.78f, 0.3f); // musgo arriba
                return metal;
            });
            // Chorro grueso que sale de la boca en arco y cae al agua.
            var stream = Sdf(p =>
            {
                float d = float.MaxValue; Vector3 prev = Vector3.zero;
                for (int k = 1; k <= 12; k++)
                {
                    float t = k / 12f;
                    Vector3 q = new Vector3(0, -t * t * 2.7f, t * 1.2f);
                    d = Mathf.Min(d, SdfMesher.Capsule(p, prev, q, 0.45f + t * 0.05f, 0.45f + t * 0.1f, out _));
                    prev = q;
                }
                return d;
            }, new Bounds(new Vector3(0, -1.3f, 0.6f), new Vector3(1.8f, 3.6f, 2.4f)), 0.04f, "Pulido_Chorro", (p, n) => Color.white);
            int count = 0;
            foreach (Transform g in Object.FindObjectsByType<Transform>())
            {
                if (g.name != "Tubería con cascada") continue;
                var tubo = g.Find("Tubo"); var chorro = g.Find("Chorro"); var spl = g.Find("Salpicadura");
                if (tubo) { tubo.GetComponent<MeshFilter>().sharedMesh = pipe; tubo.localPosition = new Vector3(0, 1.5f, -0.6f); }
                float mouthZ = -0.6f + 2.5f, mouthY = 1.5f;
                if (chorro) { chorro.GetComponent<MeshFilter>().sharedMesh = stream; chorro.localPosition = new Vector3(0, mouthY, mouthZ); chorro.localRotation = Quaternion.identity; var wf = chorro.GetComponent<WaterFlow>(); if (wf) Object.DestroyImmediate(wf); chorro.gameObject.AddComponent<WaterFlow>().kind = WaterFlow.Kind.Curtain; }
                if (spl) spl.localPosition = new Vector3(0, 0.05f, mouthZ + 1.2f);
                count++;
            }
            return $" {count} alcantarillas pulidas.";
        }
    }
}
