using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Crear Jefe (modelo propio).
    /// Sustituye las piezas sueltas del Jefe del Lobby por el sapo rey original (JefeModel) y guarda mallas y materiales.
    /// </summary>
    public static class JefeBuilder
    {
        const string Dir = "Assets/Lizard/Models/Jefe";

        [MenuItem("Lizards/Crear Jefe (modelo propio)", priority = 7)]
        public static string Build()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var jefe = Object.FindAnyObjectByType<JefeController>();
            if (!jefe) return "No hay Jefe (JefeController) en la escena abierta.";
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Lizard/Models", "Jefe");

            Material Mat(string name, System.Func<Material> make)
            {
                string path = $"{Dir}/{name}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                var fresh = make();
                if (existing) { existing.shader = fresh.shader; existing.CopyPropertiesFromMaterial(fresh); EditorUtility.SetDirty(existing); return existing; }
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            var mats = new JefeModel.Materials
            {
                skin = Mat("Jefe_Piel", () => { var m = LizardMaterials.Toon(Color.white, 0.0065f, 0.25f); m.SetFloat("_VertexColorWeight", 1f); return m; }),
                eye = Mat("Jefe_Ojo", () => LizardMaterials.Toon(Color.white, 0.006f, 0.35f, new Color(0.8f, 0.85f, 0.95f))),
                iris = Mat("Jefe_Iris", () => LizardMaterials.Toon(new Color(0.95f, 0.18f, 0.08f), 0f, 0.5f)),
                pupil = Mat("Jefe_Pupila", () => LizardMaterials.Toon(new Color(0.05f, 0.05f, 0.07f), 0f, 0.6f)),
                lid = Mat("Jefe_Parpado", () => LizardMaterials.Toon(new Color(0.33f, 0.86f, 0.18f), 0.0065f, 0.25f)),
                tooth = Mat("Jefe_Diente", () => LizardMaterials.Toon(new Color(0.9f, 0.84f, 0.6f), 0.003f, 0.3f)),
                gold = Mat("Jefe_Oro", () => LizardMaterials.Toon(new Color(1f, 0.78f, 0.18f), 0.006f, 0.5f)),
                gem = Mat("Jefe_Gema", () => LizardMaterials.Toon(new Color(0.55f, 0.95f, 0.25f), 0.003f, 0.9f)),
                wood = Mat("Jefe_Tronco", () => LizardMaterials.Toon(new Color(0.55f, 0.34f, 0.18f), 0.003f, 0.1f)),
            };

            // Quita las piezas viejas (y un modelo anterior si se regenera).
            for (int i = jefe.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(jefe.transform.GetChild(i).gameObject);
            var rig = JefeModel.Build(jefe.transform, mats);

            SaveMeshes(rig.transform);
            BuildThrone(jefe.transform, mats.wood);
            foreach (var hub in Object.FindObjectsByType<SwampLobbyHub>(FindObjectsSortMode.None)) hub.kingBelly = null;

            var scene = jefe.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            string msg = "[Lizards] Jefe (sapo rey original) creado.";
            Debug.Log(msg);
            return msg;
        }
        static void SaveMeshes(Transform root)
        {
            var saved = new System.Collections.Generic.HashSet<Mesh>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (!mesh || AssetDatabase.Contains(mesh) || !saved.Add(mesh)) continue;
                string path = $"{Dir}/{mesh.name}.asset";
                // Se borra y se crea de nuevo: copiar encima deja a Unity dibujando la versión vieja.
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
            }
        }

        static float Box(Vector3 p, Vector3 c, Vector3 h, float r)
        {
            Vector3 q = new Vector3(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y), Mathf.Abs(p.z - c.z)) - h + Vector3.one * r;
            return new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f) - r;
        }

        static Mesh SdfColored(System.Func<Vector3, float> f, Bounds b, float cell, string name, System.Func<Vector3, Vector3, Color> col)
        {
            var mesh = SdfMesher.Build(f, b, cell, name);
            var v = mesh.vertices; var c = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) c[i] = col(v[i], SdfMesher.Gradient(f, v[i], cell * 0.5f).normalized).linear;
            mesh.colors = c;
            return mesh;
        }

        static GameObject Part(string name, Transform parent, Mesh mesh, Material m, bool collide)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        static Color Stone(Vector3 p, Vector3 n)
        {
            float noise = Mathf.PerlinNoise(p.x * 0.7f + 3f, p.y * 0.7f + p.z * 0.5f);
            Color c = Color.Lerp(new Color(0.22f, 0.21f, 0.26f), new Color(0.36f, 0.34f, 0.4f), noise);
            if (n.y > 0.55f && noise > 0.35f) c = Color.Lerp(c, new Color(0.2f, 0.33f, 0.14f), 0.85f); // musgo arriba
            if (Mathf.Abs(Mathf.Sin(p.y * 2.2f + noise * 3f)) < 0.06f && n.y < 0.5f) c *= 0.6f;               // grietas
            return c;
        }

        /// <summary>
        /// Trono a medida del Jefe (no lo atraviesa): tarima con escalón, respaldo alto con cuernos, cojín,
        /// reposabrazos separados de sus brazos y braseros de fuego verde. Quita el trono y las rocas viejas de la zona.
        /// </summary>
        public static void BuildThrone(Transform jefe, Material wood)
        {
            Vector3 center = jefe.position; center.y = 0f;
            var lobby = jefe.parent;
            string[] oldNames = { "Podio de piedra", "Trono de piedra", "Musgo del trono", "Antena", "Varilla antena", "Trono del Jefe (generado)" };
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t || t == jefe || t.IsChildOf(jefe)) continue;
                bool old = System.Array.IndexOf(oldNames, t.name) >= 0;
                Vector3 d = t.position - center; d.y = 0f;
                bool clutter = (t.name == "Roca esculpida" || t.name == "Arbusto de ribera" || t.name == "Hierba") && d.magnitude < 10f;
                if (old || clutter) Object.DestroyImmediate(t.gameObject);
            }

            var stoneMat = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/Jefe_Piel.mat"); // toon con color por vértice
            var root = new GameObject("Trono del Jefe (generado)").transform;
            root.SetParent(lobby, false);
            root.position = center; root.rotation = jefe.rotation;
            jefe.position = center + Vector3.up * 0.5f; // se sienta sobre la tarima

            // Tarima redonda con escalón delantero.
            System.Func<Vector3, float> dais = p =>
                Mathf.Min(Mathf.Max(new Vector2(p.x, p.z).magnitude - 8.2f, Mathf.Abs(p.y - 0.25f) - 0.25f),
                          Box(p, new Vector3(0f, 0.12f, -8.6f), new Vector3(4.5f, 0.12f, 1.0f), 0.08f));
            Part("Tarima", root, SdfColored(dais, new Bounds(new Vector3(0, 0.3f, -0.6f), new Vector3(17f, 0.9f, 18.4f)), 0.12f, "Trono_Tarima", Stone), stoneMat, true);

            // Respaldo alto con arco y cuernos retorcidos.
            System.Func<Vector3, float> back = p =>
            {
                float slab = Box(p, new Vector3(0f, 4.75f, 6.0f), new Vector3(6.6f, 4.75f, 0.7f), 0.35f);
                float arch = Mathf.Max(new Vector2(p.x / 6.6f, (p.y - 9.5f) / 4.2f).magnitude - 1f, Mathf.Abs(p.z - 6.0f) - 0.7f);
                float d = Mathf.Min(slab, Mathf.Max(arch, 9.3f - p.y));
                for (int i = -2; i <= 2; i++)
                {
                    float x = i * 2.4f;
                    var a = new Vector3(x, 9.5f + Mathf.Sqrt(Mathf.Max(0f, 1f - (x / 6.6f) * (x / 6.6f))) * 4.2f - 0.6f, 6.0f);
                    var b = a + new Vector3(i * 0.7f, 2.6f - Mathf.Abs(i) * 0.4f, 0.6f);
                    d = SdfMesher.SMin(d, SdfMesher.Capsule(p, a, b, 0.5f, 0.08f, out _), 0.25f);
                }
                return d;
            };
            Part("Respaldo", root, SdfColored(back, new Bounds(new Vector3(0, 8.5f, 6.2f), new Vector3(15.5f, 17.5f, 3.4f)), 0.14f, "Trono_Respaldo", Stone), stoneMat, true);

            // Cojín morado gastado detrás de la espalda.
            System.Func<Vector3, float> cushion = p => Box(p, new Vector3(0f, 5f, 4.95f), new Vector3(4.8f, 3.8f, 0.45f), 0.4f);
            Part("Cojín", root, SdfColored(cushion, new Bounds(new Vector3(0, 5f, 4.95f), new Vector3(10.4f, 8.4f, 1.6f)), 0.12f, "Trono_Cojin",
                (p, n) => Color.Lerp(new Color(0.24f, 0.08f, 0.28f), new Color(0.38f, 0.14f, 0.4f), Mathf.PerlinNoise(p.x * 0.8f, p.y * 0.8f))), stoneMat, false);

            // Reposabrazos (lejos de los brazos del Jefe) con braseros de fuego verde.
            var fireMat = new Material(LizardMaterials.Particle()); fireMat.SetColor("_BaseColor", new Color(0.55f, 1f, 0.3f, 0.9f));
            var fireCore = new Material(LizardMaterials.Particle()); fireCore.SetColor("_BaseColor", new Color(0.9f, 1f, 0.7f, 1f));
            SaveMat(fireMat, "Trono_Fuego"); SaveMat(fireCore, "Trono_FuegoNucleo");
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            for (int s = -1; s <= 1; s += 2)
            {
                int side = s;
                System.Func<Vector3, float> arm = p =>
                {
                    float pillar = Box(p, new Vector3(side * 7.6f, 2.6f, 0.6f), new Vector3(0.9f, 2.6f, 5.0f), 0.3f);
                    float bowl = Mathf.Max(Mathf.Abs((p - new Vector3(side * 7.6f, 5.9f, -3.4f)).magnitude - 1.0f) - 0.12f, p.y - 5.9f);
                    float stem = SdfMesher.Capsule(p, new Vector3(side * 7.6f, 5.0f, -3.4f), new Vector3(side * 7.6f, 5.2f, -3.4f), 0.35f, 0.35f, out _);
                    return Mathf.Min(pillar, SdfMesher.SMin(bowl, stem, 0.2f));
                };
                Part(s < 0 ? "Reposabrazos I" : "Reposabrazos D", root,
                    SdfColored(arm, new Bounds(new Vector3(s * 7.6f, 3.5f, 0f), new Vector3(3f, 7.4f, 11.6f)), 0.12f, s < 0 ? "Trono_Brazo_I" : "Trono_Brazo_D", Stone), stoneMat, true);

                var brazier = new GameObject("Brasero").transform;
                brazier.SetParent(root, false);
                brazier.localPosition = new Vector3(s * 7.6f, 5.6f, -3.4f);
                var flames = new Transform[3];
                for (int i = 0; i < 3; i++)
                {
                    var fl = new GameObject(i == 0 ? "Núcleo" : "Llama").transform;
                    fl.SetParent(brazier, false);
                    fl.localPosition = new Vector3(Mathf.Sin(i * 2.1f) * 0.15f, 0.55f + i * 0.4f, Mathf.Cos(i * 2.1f) * 0.15f);
                    fl.localScale = new Vector3(0.85f - i * 0.22f, 1.0f - i * 0.15f, 0.85f - i * 0.22f); // llamas que caben en el cuenco
                    fl.gameObject.AddComponent<MeshFilter>().sharedMesh = sphere;
                    var r = fl.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = i == 0 ? fireCore : fireMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    flames[i] = fl;
                }
                var light = new GameObject("Luz").AddComponent<Light>();
                light.transform.SetParent(brazier, false); light.transform.localPosition = Vector3.up * 1.2f;
                light.type = LightType.Point; light.color = new Color(0.5f, 1f, 0.35f); light.range = 14f; light.intensity = 2.5f; light.shadows = LightShadows.None;
                var fx = brazier.gameObject.AddComponent<BrazierFlicker>(); fx.flames = flames; fx.glow = light;
            }
            SaveMeshes(root);
        }

        static void SaveMat(Material m, string name)
        {
            string path = $"{Dir}/{name}.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
        }
    }
}
