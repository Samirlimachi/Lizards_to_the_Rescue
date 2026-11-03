using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Mejorar entorno del Lobby.
    /// Crea caminos de piedras entre las zonas (con flores), limpia los carteles, añade una luz de relleno
    /// y la vida ambiental. Todo va dentro de "Ambiente (generado)" y se puede regenerar.
    /// </summary>
    public static class LobbyEnvironmentBuilder
    {
        const string RootName = "Ambiente (generado)";

        [MenuItem("Lizards/Mejorar entorno del Lobby", priority = 6)]
        public static string Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var hub = Object.FindAnyObjectByType<SwampLobbyHub>();
            var ex = Object.FindAnyObjectByType<SwampLobbyExpansion>();
            if (!hub || !ex) return "Esta escena no es el Lobby (no hay SwampLobbyHub / SwampLobbyExpansion).";

            var old = GameObject.Find(RootName);
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            var rng = new System.Random(1234);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            var stoneMats = new[] {
                LizardMaterials.Toon(new Color(0.86f, 0.8f, 0.66f), 0.002f, 0f),
                LizardMaterials.Toon(new Color(0.78f, 0.74f, 0.62f), 0.002f, 0f),
                LizardMaterials.Toon(new Color(0.9f, 0.85f, 0.72f), 0.002f, 0f) };
            var petalMats = new[] {
                LizardMaterials.Toon(new Color(1f, 0.5f, 0.7f), 0.002f, 0.2f),
                LizardMaterials.Toon(new Color(1f, 0.85f, 0.25f), 0.002f, 0.2f),
                LizardMaterials.Toon(new Color(0.6f, 0.75f, 1f), 0.002f, 0.2f),
                LizardMaterials.Toon(Color.white, 0.002f, 0.2f) };
            var stemMat = LizardMaterials.Toon(new Color(0.3f, 0.6f, 0.2f), 0f, 0f);
            SaveMats(stoneMats, "Camino_Piedra"); SaveMats(petalMats, "Flor_Petalo"); SaveMats(new[] { stemMat }, "Flor_Tallo");

            float GroundY(Vector3 p, out bool water)
            {
                water = false;
                foreach (var h in Physics.RaycastAll(new Vector3(p.x, 60f, p.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.GetComponentInParent<LizardPlayer>()) continue;
                    var n = h.collider.name;
                    if (!n.StartsWith("Terreno") && !n.Contains("Suelo") && !n.Contains("lomas")) continue;
                    return h.point.y;
                }
                water = true; return 0f;
            }

            // ---- Caminos ----
            Vector3 plaza = new Vector3(0f, 0f, -6f);
            Vector3 spawn = hub.spawn;
            var routes = new List<Vector3[]>
            {
                new[] { spawn, Vector3.Lerp(spawn, plaza, 0.5f) + Vector3.right * 0.8f, plaza },
                new[] { plaza, new Vector3(4f, 0f, -5.8f), Near(hub.telephone, plaza, 2.2f) },
                new[] { Near(hub.telephone, plaza, 2.2f), new Vector3(11f, 0f, -6f), Near(ex.vending, plaza, 3f) },
                new[] { plaza, new Vector3(-5f, 0f, -8.5f), Near(ex.colors, plaza, 2.6f) },
                new[] { Near(ex.colors, plaza, 2.6f), new Vector3(-16f, 0f, -9f), Near(hub.wardrobe, plaza, 2.4f) },
                new[] { Near(hub.wardrobe, plaza, 2.4f), Near(ex.bell, plaza, 1.6f) },
                new[] { plaza, new Vector3(1.5f, 0f, 2f), new Vector3(2f, 0f, 9f), Near(hub.boat, plaza, 4f) },
            };
            var pathRoot = new GameObject("Caminos").transform; pathRoot.SetParent(root, false);
            int stones = 0, flowers = 0;
            foreach (var route in routes)
            {
                // Catmull-Rom por los puntos de la ruta, piedras cada ~1.1 m.
                var pts = new List<Vector3>();
                for (int i = 0; i < route.Length - 1; i++)
                {
                    Vector3 p0 = route[Mathf.Max(0, i - 1)], p1 = route[i], p2 = route[i + 1], p3 = route[Mathf.Min(route.Length - 1, i + 2)];
                    int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 1.1f));
                    for (int s = 0; s < steps; s++) pts.Add(CatmullRom(p0, p1, p2, p3, s / (float)steps));
                }
                pts.Add(route[route.Length - 1]);
                for (int i = 0; i < pts.Count; i++)
                {
                    Vector3 p = pts[i];
                    Vector3 dir = i < pts.Count - 1 ? (pts[i + 1] - p).normalized : (p - pts[Mathf.Max(0, i - 1)]).normalized;
                    Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                    p += side * R(-0.25f, 0.25f);
                    float y = GroundY(p, out bool water);
                    if (water) continue;
                    var st = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    st.name = "Piedra del camino";
                    Object.DestroyImmediate(st.GetComponent<Collider>());
                    st.transform.SetParent(pathRoot, false);
                    st.transform.position = new Vector3(p.x, y + 0.03f, p.z);
                    st.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                    st.transform.localScale = new Vector3(R(0.8f, 1.15f), 0.05f, R(0.65f, 0.95f));
                    st.GetComponent<Renderer>().sharedMaterial = stoneMats[rng.Next(stoneMats.Length)];
                    stones++;

                    // Florecillas a los lados, de vez en cuando.
                    if (rng.NextDouble() < 0.35)
                    {
                        float sgn = rng.NextDouble() < 0.5 ? -1f : 1f;
                        Vector3 fp = p + side * sgn * R(0.9f, 1.5f);
                        float fy = GroundY(fp, out bool fw);
                        if (!fw) { Flower(pathRoot, new Vector3(fp.x, fy, fp.z), petalMats[rng.Next(petalMats.Length)], stemMat, R(0.7f, 1.2f), R(0f, 360f)); flowers++; }
                    }
                }
            }

            // ---- Carteles: solo títulos (las instrucciones las da el globo [E]) ----
            int signs = 0;
            foreach (var tm in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include))
            {
                string t = tm.text;
                string clean = t.StartsWith("COLOR") ? "COLOR" : t.StartsWith("SOMBREROS") ? "SOMBREROS" : t.StartsWith("SALA") ? "SALA ONLINE" : null;
                if (clean != null && clean != t) { Undo.RecordObject(tm, "Carteles"); tm.text = clean; EditorUtility.SetDirty(tm); signs++; }
            }

            // ---- Luz de relleno suave (del lado contrario al sol, sin sombras) ----
            var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            var fill = new GameObject("Luz de relleno").AddComponent<Light>();
            fill.transform.SetParent(root, false);
            fill.type = LightType.Directional; fill.shadows = LightShadows.None;
            fill.intensity = 0.35f; fill.color = new Color(0.7f, 0.82f, 1f);
            Light main = null; foreach (var l in sun) if (l.type == LightType.Directional && l != fill) { main = l; break; }
            fill.transform.rotation = main ? Quaternion.Euler(25f, main.transform.eulerAngles.y + 180f, 0f) : Quaternion.Euler(25f, 150f, 0f);

            // ---- Vida ambiental ----
            var amb = new GameObject("Vida ambiental").AddComponent<LobbyAmbience>();
            amb.transform.SetParent(root, false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            string msg = $"[Lizards] Entorno del Lobby mejorado: {stones} piedras, {flowers} flores, {signs} carteles limpios, luz de relleno y vida ambiental.";
            Debug.Log(msg);
            return msg;
        }

        static Vector3 Near(Transform target, Vector3 from, float stopShort)
        {
            Vector3 t = target.position; t.y = 0f; from.y = 0f;
            return t + (from - t).normalized * stopShort;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static void Flower(Transform parent, Vector3 pos, Material petal, Material stem, float size, float yaw)
        {
            var f = new GameObject("Flor").transform;
            f.SetParent(parent, false);
            f.position = pos; f.rotation = Quaternion.Euler(0f, yaw, 0f); f.localScale = Vector3.one * size;
            Part(f, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.03f, 0.12f, 0.03f), stem);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Part(f, PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.07f, 0.25f, Mathf.Sin(a) * 0.07f), new Vector3(0.09f, 0.035f, 0.09f), petal);
            }
            Part(f, PrimitiveType.Sphere, new Vector3(0f, 0.26f, 0f), Vector3.one * 0.06f, stem == null ? petal : petal);
        }

        static void Part(Transform parent, PrimitiveType type, Vector3 lp, Vector3 ls, Material m)
        {
            var o = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(o.GetComponent<Collider>());
            o.transform.SetParent(parent, false);
            o.transform.localPosition = lp; o.transform.localScale = ls;
            var r = o.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void SaveMats(Material[] mats, string name)
        {
            const string dir = "Assets/Lizard/Materials/Lobby";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/Lizard/Materials", "Lobby");
            for (int i = 0; i < mats.Length; i++)
            {
                string path = $"{dir}/{name}_{i}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing) { existing.shader = mats[i].shader; existing.CopyPropertiesFromMaterial(mats[i]); mats[i] = existing; }
                else AssetDatabase.CreateAsset(mats[i], path);
            }
        }
    }
}
