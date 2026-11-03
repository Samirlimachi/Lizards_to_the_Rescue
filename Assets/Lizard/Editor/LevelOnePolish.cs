using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Level 1 → Pulir estación (visual).
    /// Pulido visual sobre la estación ya construida, sin tocar la jugabilidad: materiales cartoon sin contorno,
    /// muros de ladrillo, carteles de pizarra con letra de juego, comida con modelo, lámparas, charcos y plantas.
    /// </summary>
    public static class LevelOnePolish
    {
        const string ScenePath = "Assets/Scenes/LEVEL1.unity";
        const string Dir = "Assets/Lizard/LevelOne/Materials";

        static Material Toon(string name, Color c, float spec = 0.25f)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var fresh = LizardMaterials.Toon(c, 0f, spec);
            if (m) { m.shader = fresh.shader; m.CopyPropertiesFromMaterial(fresh); EditorUtility.SetDirty(m); return m; }
            AssetDatabase.CreateAsset(fresh, path); return fresh;
        }

        static GameObject Box(Transform parent, string n, Vector3 p, Vector3 s, Material m, bool col = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
            g.transform.SetParent(parent, false); g.transform.position = p; g.transform.localScale = s;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        [MenuItem("Lizards/Level 1/Pulir estación (visual)")]
        public static string Polish()
        {
            if (Application.isPlaying) return "Sal de Play antes.";
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) { if (scene.isDirty) EditorSceneManager.SaveScene(scene); scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); }
            Transform root = null;
            foreach (var g in scene.GetRootGameObjects()) if (g.name.StartsWith("LEVEL 1")) root = g.transform;
            if (!root) return "No encuentro la estación (LEVEL 1).";
            var old = root.Find("Pulido (generado)"); if (old) Object.DestroyImmediate(old.gameObject);
            var deco = new GameObject("Pulido (generado)").transform; deco.SetParent(root, false);
            var log = new List<string>();

            // ---- 1. Materiales cartoon (sin contorno) con la paleta del refugio ----
            var palette = new Dictionary<string, Color>
            {
                { "Basalto", new Color(0.34f, 0.37f, 0.36f) }, { "Baldosa", new Color(0.44f, 0.48f, 0.45f) },
                { "Lavanda", new Color(0.45f, 0.35f, 0.66f) }, { "Lavanda clara", new Color(0.6f, 0.5f, 0.78f) },
                { "Naranja", new Color(1f, 0.46f, 0.12f) }, { "Amarillo", new Color(1f, 0.8f, 0.18f) },
                { "Metal", new Color(0.3f, 0.34f, 0.4f) }, { "Oscuro", new Color(0.1f, 0.11f, 0.14f) },
                { "Menta", new Color(0.35f, 0.85f, 0.62f) }, { "Fruta", new Color(0.95f, 0.25f, 0.3f) },
                { "Luz", new Color(1f, 0.95f, 0.7f) }, { "Madera", new Color(0.72f, 0.5f, 0.3f) },
            };
            int mats = 0;
            foreach (var kv in palette) { var m = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/{kv.Key}.mat"); if (m) { Toon(kv.Key, kv.Value, kv.Key == "Metal" || kv.Key == "Menta" ? 0.5f : 0.25f); mats++; } }
            var agua = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/Agua.mat");
            if (agua) { var w = LizardMaterials.Transparent(new Color(0.25f, 0.85f, 0.75f, 0.78f), 0.95f); agua.shader = w.shader; agua.CopyPropertiesFromMaterial(w); EditorUtility.SetDirty(agua); }
            log.Add($"{mats} materiales cartoon");

            // ---- 2. Luz cálida y niebla lila ----
            RenderSettings.ambientSkyColor = new Color(0.82f, 0.78f, 0.92f); RenderSettings.ambientEquatorColor = new Color(0.55f, 0.5f, 0.65f); RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.36f);
            RenderSettings.fogColor = new Color(0.42f, 0.36f, 0.55f); RenderSettings.fogStartDistance = 45; RenderSettings.fogEndDistance = 120;
            foreach (var cam in Object.FindObjectsByType<Camera>()) cam.backgroundColor = RenderSettings.fogColor;

            // ---- 3. Muros de ladrillo ----
            var brickLight = Toon("Ladrillo claro", new Color(0.55f, 0.45f, 0.76f));
            var trim = Toon("Zócalo", new Color(0.3f, 0.3f, 0.34f));
            int bricks = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            {
                string n = r.name;
                if (!(n.StartsWith("Muro") || n.StartsWith("Pared salida") || n.StartsWith("Marco puerta"))) continue;
                var b = r.bounds; bool alongX = b.size.x > b.size.z; float len = alongX ? b.size.x : b.size.z;
                for (float y = 0.6f; y < b.size.y - 0.3f; y += 1.0f)
                    for (float u = -len / 2 + 0.8f + ((int)(y) % 2) * 0.8f; u < len / 2 - 0.4f; u += 3.2f)
                    {
                        Vector3 off = alongX ? new Vector3(u, y - b.size.y / 2, 0) : new Vector3(0, y - b.size.y / 2, u);
                        Vector3 s = alongX ? new Vector3(1.4f, 0.42f, b.size.z + 0.05f) : new Vector3(b.size.x + 0.05f, 0.42f, 1.4f);
                        Box(deco, "Ladrillo", b.center + off, s, brickLight); bricks++;
                    }
                Box(deco, "Zócalo", new Vector3(b.center.x, b.min.y + 0.2f, b.center.z), alongX ? new Vector3(b.size.x, 0.4f, b.size.z + 0.15f) : new Vector3(b.size.x + 0.15f, 0.4f, b.size.z), trim);
            }
            log.Add($"{bricks} ladrillos");

            // ---- 4. Carteles: pizarra con marco rojo y letra de juego ----
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset");
            var outline = AssetDatabase.LoadAssetAtPath<Material>("Assets/Lizard/Models/Lobby/Rotulo_Bangers.mat");
            var chalk = Toon("Pizarra", new Color(0.12f, 0.15f, 0.15f), 0.1f);
            var frameRed = Toon("Marco rojo", new Color(0.85f, 0.18f, 0.16f));
            int signs = 0;
            foreach (var tm in root.GetComponentsInChildren<TextMesh>(true))
            {
                if (tm.transform.IsChildOf(deco)) continue;
                Transform panel = null;
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) if (r.name.StartsWith("Panel · ") && Vector3.Distance(r.bounds.center, tm.transform.position) < 0.6f) { panel = r.transform; break; }
                tm.GetComponent<MeshRenderer>().enabled = false;
                Vector3 size = panel ? panel.localScale : new Vector3(6f, 2f, 0.1f);
                Vector3 c = panel ? panel.position : tm.transform.position;
                if (panel) panel.GetComponent<MeshRenderer>().sharedMaterial = chalk;
                // Marco rojo detrás y patas hasta el suelo.
                Box(deco, "Marco", c + Vector3.forward * 0.05f, new Vector3(size.x + 0.35f, size.y + 0.35f, size.z * 0.8f), frameRed);
                if (c.y - size.y / 2 > 0.4f)
                    foreach (float sx in new[] { -0.35f, 0.35f }) Box(deco, "Pata", new Vector3(c.x + sx * size.x, (c.y - size.y / 2) / 2, c.z + 0.1f), new Vector3(0.15f, c.y - size.y / 2, 0.15f), frameRed);
                var label = new GameObject("Rótulo").AddComponent<TextMeshPro>();
                label.transform.SetParent(deco, false); label.transform.position = c + Vector3.back * (size.z / 2 + 0.03f);
                label.rectTransform.sizeDelta = new Vector2(size.x - 0.3f, size.y - 0.2f);
                if (font) label.font = font; if (outline) label.fontSharedMaterial = outline;
                label.text = tm.text; label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMin = 0.4f; label.fontSizeMax = 6f;
                label.color = Color.white; label.enableVertexGradient = true;
                label.colorGradient = new VertexGradient(new Color(1f, 0.97f, 0.7f), new Color(1f, 0.97f, 0.7f), new Color(1f, 0.75f, 0.3f), new Color(1f, 0.75f, 0.3f));
                var sl = tm.GetComponent<SignLabel>() ?? tm.gameObject.AddComponent<SignLabel>(); sl.source = tm; sl.target = label;
                signs++;
            }
            log.Add($"{signs} carteles");

            // ---- 5. Comida con modelo nuevo (misma posición y valor) ----
            int food = 0;
            foreach (var f in Object.FindObjectsByType<FoodItem>(FindObjectsInactive.Include))
            {
                if (!f.transform.IsChildOf(root)) continue;
                Vector3 p = f.transform.position; float v = f.nutrition, s = f.size; string n = f.name;
                Object.DestroyImmediate(f.gameObject);
                var nf = s > 0.4f ? FoodFactory.CreateBig(p, v, s) : FoodFactory.CreateFruit(p, v, s);
                nf.name = s > 0.4f ? "Sandía · arrástrala al depósito" : "Manzana · comer [E]"; nf.transform.SetParent(root, true);
                food++;
            }
            log.Add($"{food} comidas");

            // ---- 6. Lámparas colgantes, charcos y plantas ----
            var lampMat = Toon("Lámpara", new Color(1f, 0.92f, 0.6f), 0.6f);
            var cable = Toon("Cable", new Color(0.12f, 0.12f, 0.14f));
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            for (int z = -20; z <= 52; z += 12)
            {
                Box(deco, "Cable", new Vector3(0, 6.2f, z), new Vector3(0.05f, 2.2f, 0.05f), cable);
                var shade = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(shade.GetComponent<Collider>());
                shade.name = "Lámpara"; shade.transform.SetParent(deco, false); shade.transform.position = new Vector3(0, 5f, z); shade.transform.localScale = new Vector3(0.7f, 0.45f, 0.7f);
                shade.GetComponent<Renderer>().sharedMaterial = lampMat;
                var li = new GameObject("Luz").AddComponent<Light>(); li.transform.SetParent(shade.transform, false); li.type = LightType.Point;
                li.color = new Color(1f, 0.86f, 0.6f); li.range = 12f; li.intensity = 1.6f; li.shadows = LightShadows.None;
            }
            var puddle = Toon("Charco", new Color(0.4f, 0.85f, 0.75f), 0.9f);
            var rnd = new System.Random(3);
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = new Vector3((float)rnd.NextDouble() * 18f - 9f, 0.03f, (float)rnd.NextDouble() * 70f - 22f);
                if (p.z > -4f && p.z < 6f) continue; // no sobre el canal
                var pd = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(pd.GetComponent<Collider>());
                pd.name = "Charco"; pd.transform.SetParent(deco, false); pd.transform.position = p; pd.transform.localScale = new Vector3(1.2f + (float)rnd.NextDouble() * 1.4f, 0.01f, 0.8f + (float)rnd.NextDouble());
                pd.GetComponent<Renderer>().sharedMaterial = puddle;
            }
            var matita = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Lizard/Models/Lobby/Kit_Matita.asset");
            var hongos = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Lizard/Models/Lobby/Amb_Hongos.asset");
            var vtx = AssetDatabase.LoadAssetAtPath<Material>("Assets/Lizard/Models/Lobby/Atrezo_ColorVertice.mat");
            int plants = 0;
            if (vtx)
                for (int z = -24; z <= 54; z += 5)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var mesh = (z / 5 + s) % 4 == 0 && hongos ? hongos : matita; if (!mesh) continue;
                        var g = new GameObject(mesh == hongos ? "Hongos" : "Planta"); g.transform.SetParent(deco, false);
                        g.transform.position = new Vector3(s * (10.6f - (float)rnd.NextDouble() * 0.6f), 0f, z + (float)rnd.NextDouble() * 2f);
                        g.transform.rotation = Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                        g.transform.localScale = Vector3.one * (mesh == hongos ? 0.35f : 1.6f);
                        g.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = vtx; mr.shadowCastingMode = ShadowCastingMode.Off;
                        plants++;
                    }
            log.Add($"lámparas, charcos y {plants} plantas");

            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            string msg = "[LEVEL1] Pulido: " + string.Join(", ", log);
            Debug.Log(msg); return msg;
        }
    }
}
