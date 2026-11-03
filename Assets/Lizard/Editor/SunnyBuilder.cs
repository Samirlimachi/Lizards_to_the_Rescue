using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Crear Sunny (modelo Meshy con rig).
    /// Genera la malla con skinning (tapa, mandíbula, brazos fideo, cola), el material y el prefab Sunny.
    /// </summary>
    public static class SunnyBuilder
    {
        const string Dir = "Assets/Lizard/Models/Sunny";
        const string ObjPath = Dir + "/Meshy_AI_Sunny_the_Gecko_20261029_texture.obj";
        const string TexPath = Dir + "/Meshy_AI_Sunny_the_Gecko_20261029_texture.png";
        const string MeshPath = Dir + "/Sunny_Rig.asset";
        const string MatPath = Dir + "/Sunny_Piel.mat";
        const string BubblePath = Dir + "/Sunny_Burbuja.mat";
        public const string PrefabPath = "Assets/Lizard/Prefabs/Sunny.prefab";

        [MenuItem("Lizards/Crear Sunny (modelo propio v2)", priority = 3)]
        public static GameObject BuildPrefab()
        {
            const string v2 = "Assets/Lizard/Models/SunnyV2";
            if (!AssetDatabase.IsValidFolder(v2)) AssetDatabase.CreateFolder("Assets/Lizard/Models", "SunnyV2");

            Material Mat(string name, System.Func<Material> make)
            {
                string path = $"{v2}/{name}.mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                var fresh = make();
                if (existing) { existing.shader = fresh.shader; existing.CopyPropertiesFromMaterial(fresh); EditorUtility.SetDirty(existing); return existing; }
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }
            var mats = new SunnyModel.Materials
            {
                skin = Mat("Sunny_Piel", () => { var m = LizardMaterials.ToonCharacter(Color.white, 0.003f, 0.15f); m.SetFloat("_VertexColorWeight", 1f); return m; }),
                eyeWhite = Mat("Sunny_Ojo", () => LizardMaterials.ToonCharacter(Color.white, 0.0025f, 0.3f, new Color(0.78f, 0.84f, 0.95f))),
                pupil = Mat("Sunny_Pupila", () => LizardMaterials.ToonCharacter(new Color(0.05f, 0.05f, 0.07f), 0f, 0.6f)),
                lid = Mat("Sunny_Parpado", () => LizardMaterials.ToonCharacter(new SunnyModel.Settings().green, 0.0025f, 0.15f)),
                bubble = Mat("Sunny_Burbuja", () => LizardMaterials.Transparent(new Color(0.85f, 0.97f, 1f, 0.22f), 0.97f)),
                green = Mat("Sunny_Verde", () => LizardMaterials.ToonCharacter(new SunnyModel.Settings().green, 0.003f, 0.15f)),
                lime = Mat("Sunny_Lima", () => LizardMaterials.ToonCharacter(new SunnyModel.Settings().lime, 0.003f, 0.1f)),
                orange = Mat("Sunny_Naranja", () => LizardMaterials.ToonCharacter(new SunnyModel.Settings().tailOrange, 0.003f, 0.15f)),
            };

            var cfg = AssetDatabase.LoadAssetAtPath<LizardConfig>("Assets/Lizard/LizardConfig.asset");
            var phys = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Lizard/Materials/Lagarto_SinFriccion.physicMaterial");
            var temp = LizardFactory.Create("Sunny", Vector3.zero, cfg, null, phys, true, LizardModelStyle.Lagarto, t => SunnyModel.Build(t, mats));

            // Guarda las mallas generadas como assets (reutilizando los existentes para no romper referencias).
            void SaveMesh(Mesh mesh, System.Action<Mesh> assign)
            {
                string path = $"{v2}/{mesh.name}.asset";
                // Se borra y se crea de nuevo: copiar encima dejaba a Unity dibujando la versión vieja cargada en la GPU.
                if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                assign(mesh);
            }
            var smr = temp.GetComponentInChildren<SkinnedMeshRenderer>();
            SaveMesh(smr.sharedMesh, m => smr.sharedMesh = m);
            foreach (var mf in temp.GetComponentsInChildren<MeshFilter>())
                if (!AssetDatabase.Contains(mf.sharedMesh)) SaveMesh(mf.sharedMesh, m => mf.sharedMesh = m);

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp.gameObject, PrefabPath);
            Object.DestroyImmediate(temp.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Prefab Sunny (modelo propio v2) creado en " + PrefabPath);
            return prefab;
        }

        /// <summary>
        /// Copia la textura original y repinta el interior de la boca esculpida:
        /// paladar y fondo en rojo oscuro, suelo en rosa (lengua). Los labios por fuera no se tocan.
        /// </summary>
        static Texture2D PaintMouthInterior(Mesh source)
        {
            const string outPath = Dir + "/Sunny_Piel_Boca.png";
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            tex.LoadImage(System.IO.File.ReadAllBytes(TexPath));
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            var v = source.vertices; var n = source.normals; var uv = source.uv; var tris = source.triangles;
            var st = new SunnyRig.Settings();
            Color32 roof = new Color(0.42f, 0.07f, 0.14f), floor = new Color(0.93f, 0.42f, 0.58f);
            int painted = 0;
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                Vector3 cen = (v[a] + v[b] + v[c]) / 3f;
                Vector3 nn = (n[a] + n[b] + n[c]).normalized;
                float mouth = st.mouthY + st.mouthCurve * cen.x * cen.x;
                // Dentro de la boca: cerca de la línea, detrás de la cara delantera de los labios.
                bool inside = cen.z > 0.6f && cen.z < 0.915f && Mathf.Abs(cen.y - mouth) < 0.07f && Mathf.Abs(cen.x) < 0.3f
                              && (Mathf.Abs(nn.y) > 0.55f || nn.z < 0.4f);
                if (!inside) continue;
                painted++;
                Fill(px, w, h, uv[a], uv[b], uv[c], nn.y > 0.55f ? floor : roof);
            }
            Flatten(px);
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath);
            Debug.Log($"[Lizards] Interior de la boca repintado ({painted} triángulos) en {outPath}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        /// <summary>
        /// Estilo de colores planos: cada píxel pasa al color más cercano de la paleta propia de Sunny
        /// (comparando en un espacio que pesa más el tono que el brillo), con dos tonos de verde para dar volumen.
        /// </summary>
        static readonly Color[] Palette =
        {
            new Color(0.36f, 0.74f, 0.18f),  // verde hoja
            new Color(0.27f, 0.6f, 0.14f),   // verde sombra
            new Color(0.84f, 0.94f, 0.42f),  // barriga lima
            new Color(1f, 1f, 1f),           // ojo
            new Color(0.06f, 0.06f, 0.08f),  // pupila
            new Color(1f, 0.48f, 0.7f),      // párpado rosa
            new Color(1f, 0.55f, 0.08f),     // punta de la cola
            new Color(1f, 0.82f, 0.18f),     // transición amarilla de la cola
            new Color(0.42f, 0.07f, 0.14f),  // interior de la boca / línea de labios
            new Color(0.93f, 0.42f, 0.58f),  // lengua
        };

        static void Flatten(Color32[] px)
        {
            var cache = new System.Collections.Generic.Dictionary<int, Color32>();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                int key = (c.r >> 3) << 10 | (c.g >> 3) << 5 | (c.b >> 3);
                if (!cache.TryGetValue(key, out var outC))
                {
                    Color col = c;
                    Color.RGBToHSV(col, out float h, out float sat, out float val);
                    float best = float.MaxValue; Color pick = Palette[0];
                    foreach (var p in Palette)
                    {
                        Color.RGBToHSV(p, out float ph, out float ps, out float pv);
                        float dh = Mathf.Abs(h - ph); dh = Mathf.Min(dh, 1f - dh);
                        float d = dh * dh * 4f * Mathf.Min(sat, ps) + (sat - ps) * (sat - ps) + (val - pv) * (val - pv) * 0.8f;
                        if (d < best) { best = d; pick = p; }
                    }
                    outC = pick;
                    outC.a = 255;
                    cache[key] = outC;
                }
                px[i] = outC;
            }
        }

        static void Fill(Color32[] px, int w, int h, Vector2 a, Vector2 b, Vector2 c, Color32 col)
        {
            Vector2 A = new Vector2(a.x * w, a.y * h), B = new Vector2(b.x * w, b.y * h), C = new Vector2(c.x * w, c.y * h);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x))) - 1), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y))) - 1), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))) + 1);
            float area = (B.x - A.x) * (C.y - A.y) - (C.x - A.x) * (B.y - A.y);
            if (Mathf.Abs(area) < 1e-6f) return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 P = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = ((B.x - P.x) * (C.y - P.y) - (C.x - P.x) * (B.y - P.y)) / area;
                    float w1 = ((C.x - P.x) * (A.y - P.y) - (A.x - P.x) * (C.y - P.y)) / area;
                    float w2 = 1f - w0 - w1;
                    const float e = -0.08f; // pequeño margen para no dejar bordes sin pintar
                    if (w0 >= e && w1 >= e && w2 >= e) px[y * w + x] = col;
                }
        }

        /// <summary>Pone a Sunny como jugador (y compañero) en la escena abierta; desactiva los personajes anteriores.</summary>
        [MenuItem("Lizards/Usar Sunny como jugador en la escena abierta", priority = 4)]
        public static string UseInOpenScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ?? BuildPrefab();
            var scene = EditorSceneManager.GetActiveScene();
            var rig = Object.FindAnyObjectByType<LizardCameraRig>();
            var log = new System.Text.StringBuilder();
            foreach (var old in Object.FindObjectsByType<LizardPlayer>(FindObjectsInactive.Exclude))
            {
                if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(old.gameObject) == PrefabPath) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
                var np = go.GetComponent<LizardPlayer>();
                np.startForm = old.startForm; np.startFood = old.startFood; np.dummyCallsHelp = old.dummyCallsHelp;
                go.GetComponent<LizardInput>().controlled = old.GetComponent<LizardInput>().controlled;
                bool isPlayer = rig && rig.target == old;
                np.displayName = isPlayer ? "Sunny" : "Compañero";
                go.name = isPlayer ? "Jugador (Sunny)" : "Compañero (Sunny)";
                if (isPlayer) { Undo.RecordObject(rig, "Usar Sunny"); rig.target = np; }
                Undo.RecordObject(old.gameObject, "Usar Sunny");
                old.gameObject.SetActive(false);
                Undo.RegisterCreatedObjectUndo(go, "Usar Sunny");
                log.AppendLine(go.name + " sustituye a " + old.name);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return log.ToString();
        }
    }
}
