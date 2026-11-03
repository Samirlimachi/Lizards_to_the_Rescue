using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Crear modelo Rana (nuevo diseño).
    /// Genera textura de piel, materiales, el prefab Rana (mismos sistemas que el lagarto)
    /// y la escena RanaShowcase con las tres formas para revisar el diseño.
    /// </summary>
    public static class RanaBuilder
    {
        const string Root = "Assets/Lizard";
        const string TexPath = Root + "/Textures/Rana_Piel.png";
        const string PrefabPath = Root + "/Prefabs/Rana.prefab";
        const string ShowcasePath = Root + "/Scenes/RanaShowcase.unity";

        [MenuItem("Lizards/Crear modelo Rana (nuevo diseño)", priority = 2)]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildPrefab();
            BuildShowcase(false);
        }

        public static GameObject BuildPrefab()
        {
            foreach (var d in new[] { Root + "/Textures", Root + "/Materials", Root + "/Prefabs", Root + "/Scenes", Root + "/Meshes" })
                if (!AssetDatabase.IsValidFolder(d)) AssetDatabase.CreateFolder(Root, Path.GetFileName(d));

            var tex = SkinTexture();
            var rt = RanaModelFactory.CreateRuntimeMaterials(tex);
            var mats = new LizardModelMaterials
            {
                skin = Save(rt.skin, "Rana_Piel"), belly = Save(rt.belly, "Rana_Barriga"),
                eyeWhite = Save(rt.eyeWhite, "Rana_OjoBlanco"), pupil = Save(rt.pupil, "Rana_Pupila"),
                lid = Save(rt.lid, "Rana_Parpado"), mouth = Save(rt.mouth, "Rana_Boca"),
                tongue = Save(rt.tongue, "Rana_Lengua"), tailTip = Save(rt.tailTip, "Rana_Cola"),
                spots = Save(rt.spots, "Rana_Manchas"), bubble = Save(rt.bubble, "Rana_Burbuja"),
            };

            var cfg = AssetDatabase.LoadAssetAtPath<LizardConfig>(Root + "/LizardConfig.asset");
            var phys = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root + "/Materials/Lagarto_SinFriccion.physicMaterial");
            var temp = LizardFactory.Create("Rana", Vector3.zero, cfg, mats, phys, true, LizardModelStyle.Rana);
            // La malla del cuerpo se genera por código: se guarda como asset para que el prefab la conserve.
            foreach (var smr in temp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (AssetDatabase.Contains(smr.sharedMesh)) continue;
                string meshPath = $"{Root}/Meshes/{smr.sharedMesh.name}.asset";
                var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existingMesh) { EditorUtility.CopySerialized(smr.sharedMesh, existingMesh); smr.sharedMesh = existingMesh; }
                else AssetDatabase.CreateAsset(smr.sharedMesh, meshPath);
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp.gameObject, PrefabPath);
            Object.DestroyImmediate(temp.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Prefab Rana creado en " + PrefabPath);
            return prefab;
        }

        /// <summary>Con additive = true se crea junto a la escena abierta (sin cerrarla), desplazada lejos del nivel.</summary>
        public static UnityEngine.SceneManagement.Scene BuildShowcase(bool additive)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var prevActive = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            Vector3 o = additive ? new Vector3(0f, 1000f, 0f) : Vector3.zero;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.8f, 0.85f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.3f);

            var sun = new GameObject("Luz").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(40f, 200f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Suelo";
            floor.transform.position = o + new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(30f, 1f, 30f);
            floor.GetComponent<Renderer>().sharedMaterial = Save(LizardMaterials.Lit(new Color(0.22f, 0.22f, 0.23f), 0.1f), "Exhibicion_Suelo");

            Place(prefab, "Rana renacuajo", o + new Vector3(-2.6f, 0.05f, 0f), LizardForm.Small, 0f);
            Place(prefab, "Rana normal", o + new Vector3(-1.2f, 0.05f, 0f), LizardForm.Normal, 3f);
            Place(prefab, "Rana gigante", o + new Vector3(1.8f, 0.05f, 0f), LizardForm.Giant, 12f);

            var cam = new GameObject("Cámara exhibición").AddComponent<Camera>();
            if (!additive) cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.21f);
            cam.fieldOfView = 35f;
            cam.transform.position = o + new Vector3(0f, 1.6f, 8.5f);
            cam.transform.LookAt(o + new Vector3(0f, 1.1f, 0f));
            if (additive) cam.enabled = false;

            if (additive) { sun.enabled = false; RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox; }
            EditorSceneManager.SaveScene(scene, additive ? Root + "/Scenes/RanaShowcase_Aditiva.unity" : ShowcasePath);
            if (additive && prevActive.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(prevActive);
            Debug.Log("[Lizards] Escena de exhibición creada (pulsa Play para ver las formas animadas)");
            return scene;
        }

        static void Place(GameObject prefab, string name, Vector3 pos, LizardForm form, float food)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.position = pos;
            var p = go.GetComponent<LizardPlayer>();
            p.startForm = form; p.startFood = food; p.displayName = name;
            go.GetComponent<LizardInput>().controlled = false;
        }

        static Material Save(Material m, string name)
        {
            string path = $"{Root}/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { existing.shader = m.shader; existing.CopyPropertiesFromMaterial(m); existing.shaderKeywords = m.shaderKeywords; existing.renderQueue = m.renderQueue; EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Textura de piel: base blanca (se tiñe con el color del material) con manchas suaves.</summary>
        static Texture2D SkinTexture()
        {
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    // Ruido periódico en U para que no haya costura en la esfera.
                    float a = u * Mathf.PI * 2f;
                    float n1 = Mathf.PerlinNoise(Mathf.Cos(a) * 2.2f + 10f, Mathf.Sin(a) * 2.2f + v * 5f);
                    float n2 = Mathf.PerlinNoise(Mathf.Cos(a) * 6f + 40f, Mathf.Sin(a) * 6f + v * 14f);
                    float blotch = Mathf.SmoothStep(0.6f, 0.75f, n1) * 0.07f + (n2 - 0.5f) * 0.03f;
                    float k = 1f - blotch;
                    px[y * size + x] = new Color(k, k, k, 1f);
                }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(TexPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        }
    }
}
