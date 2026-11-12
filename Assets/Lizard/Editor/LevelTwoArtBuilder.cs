using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using K = Lizards.EditorTools.SewerKit;
using Object = UnityEngine.Object;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Level 2 → Construir alcantarillas (arte de referencia).
    /// Reconstruye LEVEL2 siguiendo las láminas: 01 muelle con balsa y gran tubería naranja, 02 sala circular con el embudo,
    /// 03 canal sinuoso con plataformas lima y aros, 04 almacén con estantes y balcón, 05 taller con placa azul y persiana,
    /// 06 depósito con cascadas, pasarelas y elevador, 07 sala de bombas con canal, puente y despensa del fondo.
    /// La jugabilidad (cuota de provisiones, bol, balsa de regreso) es la de la expedición.
    /// </summary>
    public static partial class LevelTwoArtBuilder
    {
        const string ScenePath = "Assets/Scenes/LEVEL2.unity";
        const string Dir = "Assets/Lizard/LevelTwo/Arte";
        const float H = 7.5f;
        static Transform sceneRoot, currentRoom, colGroup;
        static K glowKit;
        static Material vertexMat, waterMat, fallMat, unlitMat, limeMat, plateMat;
        static FoodItem[] foods;
        static TMP_FontAsset font;
        static int foodSerial;
        static readonly List<(K k, Transform t)> rooms = new();
        static readonly List<ExpeditionSupplyCrate> caches = new();

        static Material Mat(string name, Shader sh, Action<Material> setup)
        {
            string path = Dir + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh; setup(m); EditorUtility.SetDirty(m); return m;
        }

        static void Col(string name, Vector3 c, Vector3 s, Quaternion? r = null)
        {
            var g = new GameObject(name); g.transform.SetParent(colGroup, false); g.transform.SetPositionAndRotation(c, r ?? Quaternion.identity);
            g.AddComponent<BoxCollider>().size = s;
        }

        static K Room(string name)
        {
            currentRoom = new GameObject(name).transform; currentRoom.SetParent(sceneRoot, false);
            colGroup = new GameObject("Colisiones").transform; colGroup.SetParent(currentRoom, false);
            var k = new K(); rooms.Add((k, currentRoom)); return k;
        }

        static void Corridor(K k, Vector3 inner, Vector3 outer, float width = 6f)
        {
            var f = outer - inner; f.y = 0; f.Normalize(); var side = new Vector3(-f.z, 0, f.x); float hw = width * 0.5f;
            var i2 = inner - f * 0.6f; var o2 = outer + f * 0.2f;
            Wall(k, o2 + side * hw, i2 + side * hw, H, null, false);
            Wall(k, i2 - side * hw, o2 - side * hw, H, null, false);
            var a = inner - f * 0.6f; var b = outer + f * 1.0f;
            var min = Vector3.Min(a - side * hw, b + side * hw); var max = Vector3.Max(a - side * hw, b + side * hw);
            min = Vector3.Min(min, Vector3.Min(a + side * hw, b - side * hw)); max = Vector3.Max(max, Vector3.Max(a + side * hw, b - side * hw));
            Floor(k, new Rect(min.x, min.z, max.x - min.x, max.z - min.z), -0.015f);
            HazardStrip(k, inner + f * 0.9f - side * hw, inner + f * 0.9f + side * hw, 0.55f, 0.03f);
        }

        // ---------------- Comida, cajas y piezas con física ----------------
        static void LoadFoods()
        {
            string[] ids = { "Apple", "Banana", "Corn", "Broccoli", "Avocado", "Pepper" };
            foods = ids.Select(id => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/LevelTwo/Expedition/FoodPrefabs/" + id + ".prefab")).Where(g => g).Select(g => g.GetComponent<FoodItem>()).ToArray();
            if (foods.Length == 0) throw new Exception("Faltan los prefabs de comida de la expedición (Lizards/Level 2/Construir expedición los crea).");
        }
        static FoodItem Food(Vector3 p, int index = -1, float value = 1, float scale = 1)
        {
            int kind = index < 0 ? foodSerial++ % foods.Length : index % foods.Length;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(foods[kind].gameObject);
            go.transform.SetParent(currentRoom, true); go.transform.position = p; go.transform.rotation = Quaternion.Euler(0, Rf(0, 360), 0); go.transform.localScale *= scale;
            var b = FoodCollectionBuilder.VisualBounds(go); go.transform.position += Vector3.up * (p.y + 0.03f - b.min.y);
            var food = go.GetComponent<FoodItem>(); food.nutrition = value; food.size = foods[kind].size * scale;
            if (scale > 1.2f) food.GetComponent<Rigidbody>().mass = 3;
            go.name = (scale > 1.2f ? "Provisión grande · arrastrar" : "Comida") + " · " + go.GetComponent<FoodCollectionIdentity>().modelId;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform); PrefabUtility.RecordPrefabInstancePropertyModifications(food);
            PrefabUtility.RecordPrefabInstancePropertyModifications(food.GetComponent<Rigidbody>());
            return food;
        }
        static void Cluster(Vector3 p, int count, float step = 0.75f)
        {
            for (int i = 0; i < count; i++) Food(p + new Vector3((i % 3) * step + Rf(-0.12f, 0.12f), 0, (i / 3) * step + Rf(-0.1f, 0.1f)));
        }
        static GameObject PhysCrate(string name, Vector3 c, float s, float mass, bool marked = false)
        {
            var go = new GameObject(name); go.transform.SetParent(currentRoom, false); go.transform.position = c;
            var k = new K { bake = false }; Crate(k, c, s, 0, marked);
            var vis = k.Flush("Modelo", go.transform, vertexMat, c); vis.transform.localPosition = Vector3.zero;
            go.AddComponent<BoxCollider>().size = Vector3.one * s;
            var rb = go.AddComponent<Rigidbody>(); rb.mass = mass; rb.linearDamping = 0.35f; rb.angularDamping = 0.6f; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<TongueTarget>().kind = TongueTargetKind.Object;
            return go;
        }
        static void Cache(Vector3 c, int count = 3)
        {
            var go = PhysCrate("Caja de provisiones · abrir [E]", c, 1.15f, 3, true);
            var cache = go.AddComponent<ExpeditionSupplyCrate>(); cache.closedVisual = go.transform.Find("Modelo").gameObject; cache.closedCollider = go.GetComponent<Collider>(); cache.count = count;
            cache.foods = Enumerable.Range(0, foods.Length).Select(i => foods[(i + caches.Count) % foods.Length]).ToArray();
            var open = new GameObject("Caja abierta"); open.transform.SetParent(go.transform, false);
            var k = new K { bake = false };
            k.Box(c + Vector3.down * 0.52f, new Vector3(1.15f, 0.1f, 1.15f), Quaternion.identity, WoodDark);
            for (int side = -1; side <= 1; side += 2) for (int row = 0; row < 3; row++)
            {
                float y = -0.36f + row * 0.27f;
                k.Box(c + new Vector3(side * 0.55f, y, 0), new Vector3(0.09f, 0.23f, 1.15f), Quaternion.identity, V(Wood, 0.05f));
                k.Box(c + new Vector3(0, y, side * 0.55f), new Vector3(1.05f, 0.23f, 0.09f), Quaternion.identity, V(Wood, 0.05f));
            }
            var ov = k.Flush("Tablones", open.transform, vertexMat, c); ov.transform.localPosition = Vector3.zero;
            foreach (var (p, s) in new[] { (new Vector3(0, -0.52f, 0), new Vector3(1.15f, 0.1f, 1.15f)), (new Vector3(0.55f, -0.1f, 0), new Vector3(0.09f, 0.8f, 1.15f)), (new Vector3(-0.55f, -0.1f, 0), new Vector3(0.09f, 0.8f, 1.15f)), (new Vector3(0, -0.1f, 0.55f), new Vector3(1.05f, 0.8f, 0.09f)), (new Vector3(0, -0.1f, -0.55f), new Vector3(1.05f, 0.8f, 0.09f)) })
            { var bc = open.AddComponent<BoxCollider>(); bc.center = p; bc.size = s; }
            cache.openVisual = open; open.SetActive(false); caches.Add(cache);
        }
        static void Anchor(Vector3 p, float chainTop)
        {
            var k = rooms[rooms.Count - 1].k;
            k.Torus(p, Quaternion.identity, 0.42f, 0.085f, Yellow, 28, 8);
            for (float y = p.y + 0.5f; y < chainTop - 0.05f; y += 0.2f)
                k.Torus(new Vector3(p.x, y, p.z), ((int)((y - p.y) / 0.2f)) % 2 == 0 ? Quaternion.identity : Quaternion.Euler(0, 90, 0), 0.07f, 0.025f, Yellow, 10, 5);
            var go = new GameObject("Aro de enganche · lengua"); go.transform.SetParent(currentRoom, false); go.transform.position = p;
            go.AddComponent<SphereCollider>().radius = 0.5f; go.AddComponent<TongueTarget>().kind = TongueTargetKind.GrapplePoint;
        }
        static void Recovery(Vector3 p)
        {
            var k = rooms[rooms.Count - 1].k;
            k.Cyl(p, p + Vector3.up * 0.1f, 0.8f, Mint, 20, true);
            k.Torus(p + Vector3.up * 0.1f, Quaternion.Euler(90, 0, 0), 0.8f, 0.06f, MintDark, 24, 6);
            var go = new GameObject("Punto de recuperación"); go.transform.SetParent(currentRoom, false); go.transform.position = p;
            var s = go.AddComponent<SphereCollider>(); s.radius = 0.8f; s.isTrigger = true; go.AddComponent<RescuePoint>();
        }
        static TMP_Text Sign(string text, Vector3 p, float width = 3, float yaw = 0)
        {
            var owner = new GameObject("Señal · " + text.Split('\n')[0]).transform; owner.SetParent(currentRoom, false); owner.position = p; owner.rotation = Quaternion.Euler(0, yaw, 0);
            var k = rooms[rooms.Count - 1].k; var rot = owner.rotation;
            k.Box(p, new Vector3(width + 0.2f, 1.1f, 0.12f), rot, WoodDark);
            k.Box(p - owner.forward * 0.07f, new Vector3(width, 0.92f, 0.04f), rot, new Color(0.16f, 0.18f, 0.2f));
            foreach (float x in new[] { -0.32f, 0.32f }) { var q = p + owner.right * (x * width); k.Cyl(new Vector3(q.x, 0, q.z), new Vector3(q.x, p.y - 0.4f, q.z), 0.06f, WoodDark, 8); }
            foreach (int s in new[] { -1, 1 }) k.Sphere(p + owner.right * s * (width / 2 + 0.02f) + Vector3.up * 0.5f, Vector3.one * 0.09f, Orange, 6);
            var label = new GameObject("Texto").AddComponent<TextMeshPro>(); label.transform.SetParent(owner, false); label.transform.localPosition = new Vector3(0, 0, -0.11f);
            label.rectTransform.sizeDelta = new Vector2(width - 0.2f, 0.82f); label.font = font; label.text = text; label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true; label.fontSizeMin = 0.4f; label.fontSizeMax = 3; label.color = new Color(1f, 0.95f, 0.8f);
            return label;
        }
        static void Slug(Vector3 a, Vector3 b)
        {
            var go = new GameObject("Babosa patrullera"); go.transform.SetParent(currentRoom, false); go.transform.position = a;
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; var col = go.AddComponent<CapsuleCollider>(); col.direction = 2; col.radius = 0.38f; col.height = 1.3f;
            var patrol = go.AddComponent<PatrolEnemy>(); patrol.pointA = a; patrol.pointB = b; patrol.speed = 1.15f; go.AddComponent<DamageSource>(); go.AddComponent<EnemyHealth>();
            var k = new K { bake = false };
            k.Sphere(a, new Vector3(0.48f, 0.33f, 0.68f), Lime, 14); k.Sphere(a + new Vector3(0, 0.12f, 0.35f), new Vector3(0.34f, 0.3f, 0.34f), Lime, 12);
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = a + new Vector3(s * 0.16f, 0.62f, 0.42f); k.Cyl(eye - Vector3.up * 0.3f, eye, 0.045f, LimeDark, 6);
                k.Sphere(eye, Vector3.one * 0.12f, White, 8); k.Sphere(eye + Vector3.forward * 0.08f, Vector3.one * 0.065f, Black, 6);
            }
            for (int i = 0; i < 4; i++) k.Sphere(a + new Vector3(Rf(-0.3f, 0.3f), 0.25f, Rf(-0.5f, 0.2f)), Vector3.one * 0.07f, LimeDark, 6);
            var v = k.Flush("Cuerpo", go.transform, vertexMat, a); v.transform.localPosition = Vector3.zero;
        }

        // =====================================================================================
        [MenuItem("Lizards/Level 2/Construir alcantarillas (arte de referencia)")]
        public static string Build()
        {
            if (Application.isPlaying) return "Sal de Play antes de construir.";
            var active = EditorSceneManager.GetActiveScene(); if (active.isDirty) EditorSceneManager.SaveScene(active);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory("Backups/Level2Arte"); if (File.Exists(ScenePath)) File.Copy(ScenePath, "Backups/Level2Arte/LEVEL2_" + stamp + ".unity");
            Directory.CreateDirectory(Dir + "/Materials");
            if (AssetDatabase.IsValidFolder(Dir + "/Meshes")) AssetDatabase.DeleteAsset(Dir + "/Meshes");
            Directory.CreateDirectory(Dir + "/Meshes"); AssetDatabase.Refresh();
            K.MeshDir = Dir + "/Meshes"; K.Serial = 0; K.Lamps.Clear();
            K.Bevel = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Lizard/LevelOne/ReferenceStyle/Meshes/Bloque biselado unitario.asset");
            rnd = new System.Random(4242); foodSerial = 0; rooms.Clear(); caches.Clear();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject saltenas = null;
            foreach (var g in scene.GetRootGameObjects()) { if (g.name == "Salteñas rompibles") saltenas = g; else Object.DestroyImmediate(g); }

            vertexMat = Mat("Alcantarilla · color de vértice", Shader.Find("Lizards/Toon"), m =>
            {
                m.SetColor("_BaseColor", Color.white); m.SetFloat("_VertexColorWeight", 1); m.SetFloat("_OutlineWidth", 0); m.SetFloat("_SpecSize", 0);
                m.SetFloat("_ShadowThreshold", 0.05f); m.SetFloat("_ShadowSoftness", 0.3f); m.SetColor("_ShadowColor", new Color(0.66f, 0.62f, 0.85f)); m.SetColor("_RimColor", new Color(1, 0.9f, 1, 0.08f));
            });
            waterMat = Mat("Agua turquesa", Shader.Find("Lizards/SewerWater"), m => { m.SetColor("_BaseColor", new Color(0.07f, 0.70f, 0.70f)); m.SetColor("_LineColor", new Color(0.72f, 1f, 0.95f)); m.SetFloat("_Flow", 0); m.SetFloat("_Scale", 0.6f); });
            fallMat = Mat("Cascada", Shader.Find("Lizards/SewerWater"), m => { m.SetColor("_BaseColor", new Color(0.25f, 0.85f, 0.82f)); m.SetColor("_LineColor", new Color(0.92f, 1f, 1f)); m.SetFloat("_Flow", 1); });
            unlitMat = Mat("Luz de farol", Shader.Find("Lizards/UnlitVertex"), m => m.SetColor("_BaseColor", Color.white));
            var membrane = LizardMaterials.Transparent(new Color(0.55f, 1f, 0.4f, 0.5f), 0.9f);
            limeMat = Mat("Membrana ácida", membrane.shader, m => m.CopyPropertiesFromMaterial(membrane));
            plateMat = Mat("Placa azul", Shader.Find("Lizards/Toon"), m => { m.CopyPropertiesFromMaterial(vertexMat); m.SetColor("_BaseColor", new Color(0.3f, 0.5f, 1f)); });
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset");
            LoadFoods();

            sceneRoot = new GameObject("LEVEL2 · Alcantarillas de colores").transform;
            glowKit = new K { bake = false };

            BuildHub(out var goal, out var board);
            BuildDock(out var boat);
            BuildCanal();
            BuildStore();
            BuildWorkshop();
            BuildTank();
            BuildPumps();
            PlaceSlimes();

            // Volcado de todas las salas (con la luz de los faroles ya conocida).
            foreach (var (k, t) in rooms) k.Flush(t.name + " · arte", t, vertexMat);
            glowKit.Flush("Faroles y ventanas encendidas", sceneRoot, unlitMat, Vector3.zero, false, false);

            // ---- Misión: cuota de provisiones y regreso en balsa ----
            var mission = new GameObject("Expedición · cuota y regreso").AddComponent<LevelTwoExpedition>(); mission.transform.SetParent(sceneRoot, false);
            mission.bowl = goal; mission.boat = boat; mission.bowlBoard = board; mission.arrival = boat.position + new Vector3(0, 0.95f, 0);
            var beacon = new K { bake = false }; beacon.Sphere(boat.position + Vector3.up * 3.2f, Vector3.one * 0.25f, Lime, 14);
            mission.extractionBeacon = beacon.Flush("Baliza de extracción", sceneRoot, unlitMat, boat.position + Vector3.up * 3.2f, false, false).transform;
            LevelTwoExpeditionBuilder.CreateHUD(mission);

            if (saltenas)
            {
                var spots = new[] { new Vector3(-9, 0.7f, -4), new Vector3(24, 0.7f, -5), new Vector3(-28, 0.7f, 12) }; int i = 0;
                foreach (Transform c in saltenas.transform) { c.position = spots[i % spots.Length]; c.rotation = Quaternion.identity; i++; }
            }

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Prefabs/Sunny.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab); player.name = "Jugador · Sunny"; player.transform.position = mission.arrival;
            player.GetComponent<LizardInput>().controlled = true; var pc = player.GetComponent<LizardPlayer>(); pc.displayName = "Sunny"; pc.startFood = 0;
            var camGo = new GameObject("Cámara de expedición"); camGo.tag = "MainCamera"; var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 180; cam.fieldOfView = 60;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.11f, 0.24f); camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<LizardCameraRig>(); rig.target = pc; camGo.transform.position = mission.arrival + new Vector3(0, 3, -6); camGo.transform.LookAt(mission.arrival);
            var hud = new GameObject("HUD del jugador").AddComponent<LizardHUD>(); hud.rig = rig; hud.showDebug = false;

            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.72f, 0.86f); RenderSettings.ambientEquatorColor = new Color(0.55f, 0.46f, 0.6f); RenderSettings.ambientGroundColor = new Color(0.3f, 0.26f, 0.34f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(0.2f, 0.11f, 0.24f); RenderSettings.fogStartDistance = 45; RenderSettings.fogEndDistance = 130;
            var sun = new GameObject("Luz principal").AddComponent<Light>(); sun.transform.SetParent(sceneRoot, false); sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.92f); sun.intensity = 1.2f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.55f; sun.transform.rotation = Quaternion.Euler(62, -30, 0);

            var build = EditorBuildSettings.scenes.ToList(); if (!build.Any(x => x.path == ScenePath)) { build.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = build.ToArray(); }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            float loose = Object.FindObjectsByType<FoodItem>().Sum(f => f.nutrition), boxed = caches.Sum(c => c.count);
            string msg = $"[LEVEL2] Alcantarillas de colores construidas: 7 salas, {K.Serial} mallas, {caches.Count} cajas, {loose + boxed:0} de comida en total.";
            Debug.Log(msg); return msg;
        }

        // =====================================================================================
        // 02 · Sala circular del embudo
        static void BuildHub(out FoodGoal goal, out TMP_Text board)
        {
            var k = Room("02 · Sala del embudo");
            const float R = 14f; float delta = Mathf.Asin(5f / R) * Mathf.Rad2Deg;
            var pts = new List<Vector3>(); var holes = new Dictionary<int, Hole[]>();
            foreach (float c in new[] { 0f, 90f, 180f, 270f })
            {
                holes[pts.Count] = new[] { new Hole(5f, 6f) };
                Arc(pts, Vector3.zero, R, c - delta, c + delta, 1); pts.RemoveAt(pts.Count - 1);
                Arc(pts, Vector3.zero, R, c + delta, c + 90 - delta, 4); pts.RemoveAt(pts.Count - 1);
            }
            Polygon(k, pts, H, holes, false);
            // Pilastras entre los tramos curvos.
            for (int i = 0; i < 4; i++) foreach (float off in new[] { 33f, 57f })
            {
                float a = (i * 90 + off) * Mathf.Deg2Rad; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var rot = Quaternion.LookRotation(-d);
                for (float y = 0; y < H - 0.3f; y += 0.64f) k.Box(d * (R - 0.2f) + Vector3.up * (y + 0.31f), new Vector3(((int)(y / 0.64f)) % 2 == 0 ? 1.25f : 1.05f, 0.6f, 0.5f), rot, V(((int)(y / 0.64f)) % 2 == 0 ? Brick2 : Brick, 0.05f));
                k.Block(d * (R + 0.3f) + Vector3.up * (H + 0.35f), new Vector3(1.6f, 0.7f, 1.8f), rot, V(Coping, 0.04f));
                Col("Pilastra", d * (R - 0.2f) + Vector3.up * (H / 2), new Vector3(1.25f, H, 0.6f), rot);
            }

            // Suelo radial: cuñas claras y oscuras, anillo amarillo.
            var colK = new K { bake = false };
            float[] rings = { 4.45f, 6.9f, 9.5f, 10.0f, 14.6f };
            for (int ri = 0; ri < rings.Length - 1; ri++)
            {
                float r0 = rings[ri], r1 = rings[ri + 1]; bool yellowRing = ri == 2; int wedges = yellowRing ? 48 : 16;
                for (int w = 0; w < wedges; w++)
                {
                    float a0 = w * 360f / wedges + (yellowRing ? 0 : 0.6f), a1 = (w + 1) * 360f / wedges - (yellowRing ? 0 : 0.6f);
                    var col = yellowRing ? Yellow : V(w % 2 == 0 ? TileB : TileA * 0.92f, 0.03f);
                    float g = yellowRing ? 0 : 0.05f; int sub = yellowRing ? 1 : 4;
                    for (int s = 0; s < sub; s++)
                    {
                        float b0 = Mathf.Lerp(a0, a1, s / (float)sub) * Mathf.Deg2Rad, b1 = Mathf.Lerp(a0, a1, (s + 1) / (float)sub) * Mathf.Deg2Rad;
                        Vector3 D(float a) => new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        float y = yellowRing ? 0.025f : 0.02f;
                        var p00 = D(b0) * (r0 + g) + Vector3.up * y; var p01 = D(b0) * (r1 - g) + Vector3.up * y; var p11 = D(b1) * (r1 - g) + Vector3.up * y; var p10 = D(b1) * (r0 + g) + Vector3.up * y;
                        k.Quad(p00, p01, p11, p10, col, Vector3.up);
                        k.Quad(p01, p11, p11 + Vector3.down * 0.15f, p01 + Vector3.down * 0.15f, col * 0.85f, D((b0 + b1) / 2));
                        k.Quad(p00, p10, p10 + Vector3.down * 0.15f, p00 + Vector3.down * 0.15f, col * 0.85f, -D((b0 + b1) / 2));
                    }
                }
            }
            k.Lathe(Vector3.up * -0.1f, new[] { new Vector2(14.8f, 0), new Vector2(4.3f, 0) }, new Color(0.25f, 0.24f, 0.3f), 64);
            colK.Lathe(Vector3.zero, new[] { new Vector2(14.8f, 0.02f), new Vector2(4.35f, 0.02f) }, Color.white, 64);
            // Embudo central: aro naranja con remaches y paredes de lamas hasta la rejilla.
            k.Lathe(Vector3.zero, new[] { new Vector2(4.5f, 0), new Vector2(4.5f, 0.18f), new Vector2(4.35f, 0.24f), new Vector2(3.55f, 0.24f), new Vector2(3.4f, 0.05f) }, Orange, 64);
            k.Torus(Vector3.up * 0.2f, Quaternion.Euler(90, 0, 0), 4.5f, 0.06f, OrangeDark, 64, 6);
            for (int i = 0; i < 30; i++) { float a = i * Mathf.PI * 2 / 30; k.Sphere(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.95f + Vector3.up * 0.25f, new Vector3(0.1f, 0.06f, 0.1f), OrangeDark, 6); }
            var funnelProf = new[] { new Vector2(3.4f, 0.05f), new Vector2(2.4f, -0.75f), new Vector2(1.45f, -1.55f), new Vector2(1.38f, -1.62f) };
            k.Lathe(Vector3.zero, funnelProf, new Color(0.62f, 0.4f, 0.66f), 48, null, (i, j) => V(i % 2 == 0 ? new Color(0.64f, 0.42f, 0.68f) : new Color(0.55f, 0.35f, 0.6f), 0.02f));
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                k.Cyl(d * 3.35f + Vector3.up * 0.06f, d * 1.42f + Vector3.up * -1.56f, 0.035f, new Color(0.4f, 0.24f, 0.45f), 5, false);
            }
            colK.Lathe(Vector3.zero, new[] { new Vector2(4.5f, 0.24f), new Vector2(3.4f, 0.05f), new Vector2(2.4f, -0.75f), new Vector2(1.4f, -1.6f), new Vector2(0f, -1.62f) }, Color.white, 48);
            k.Cyl(Vector3.up * -1.66f, Vector3.up * -1.6f, 1.42f, new Color(0.08f, 0.07f, 0.1f), 28);
            k.Torus(Vector3.up * -1.58f, Quaternion.Euler(90, 0, 0), 1.38f, 0.1f, SteelDark, 32, 6);
            for (int i = -4; i <= 4; i++) { float x = i * 0.3f; float hh = Mathf.Sqrt(1.38f * 1.38f - x * x); k.Box(new Vector3(x, -1.56f, 0), new Vector3(0.09f, 0.08f, hh * 2), Quaternion.identity, new Color(0.35f, 0.34f, 0.42f)); }
            k.Box(new Vector3(0, -1.53f, 0), new Vector3(2.7f, 0.08f, 0.12f), Quaternion.identity, new Color(0.35f, 0.34f, 0.42f));
            var foam = new K { bake = false };
            for (int i = 0; i < 22; i++) { float a = i * Mathf.PI * 2 / 22; foam.Sphere(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.4f + Vector3.up * -1.5f, new Vector3(0.22f, 0.16f + 0.08f * (i % 3), 0.22f), White, 6); }
            foam.Flush("Espuma del sumidero", currentRoom, unlitMat, Vector3.zero, false, false);
            var cm = colK.Flush("Colisión del suelo circular", colGroup, vertexMat, Vector3.zero, true, false); Object.DestroyImmediate(cm.GetComponent<MeshRenderer>());

            var trig = new GameObject("Embudo · entrega de provisiones"); trig.transform.SetParent(currentRoom, false); trig.transform.position = new Vector3(0, -0.85f, 0);
            var tc = trig.AddComponent<BoxCollider>(); tc.isTrigger = true; tc.size = new Vector3(6.4f, 1.7f, 6.4f); goal = trig.AddComponent<FoodGoal>();

            // Rejillas, tubos grandes con válvula, faroles y franjas en las bocas.
            foreach (float a in new[] { 0f, 90f, 180f, 270f })
            {
                var d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad));
                Grate(k, d * 7.9f, 0.95f, 2.0f, -a);
                var side = new Vector3(-d.z, 0, d.x);
                HazardStrip(k, d * 12.7f - side * 3f, d * 12.7f + side * 3f, 0.55f, 0.04f);
            }
            foreach (float a in new[] { 52f, 128f })
            {
                var d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad)); var p = d * 11.2f;
                k.Block(p + Vector3.up * 0.55f, new Vector3(2.2f, 1.1f, 2.2f), Quaternion.LookRotation(-d), V(StoneGray, 0.03f));
                k.Block(p + Vector3.up * 1.15f, new Vector3(1.9f, 0.2f, 1.9f), Quaternion.LookRotation(-d), V(StoneGray2, 0.03f));
                Pipe(k, p + Vector3.up * 1.2f, p + Vector3.up * 6.6f, 0.78f, Mint, 2.6f);
                Elbow(k, p + Vector3.up * 7.9f, Vector3.up, d, 1.3f, 0.78f, Mint);
                Pipe(k, p + Vector3.up * 7.9f + d * 1.3f, d * 15.8f + Vector3.up * 7.9f, 0.78f, Mint, 2.2f);
                Valve(k, p + Vector3.up * 2.3f - d * 0.85f, -d, 0.62f);
                Col("Tubo grande", p + Vector3.up * 4f, new Vector3(1.8f, 8f, 1.8f));
            }
            foreach (float a in new[] { 36f, 144f, 216f, 324f })
            {
                var d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad));
                Lantern(k, d * (R - 0.1f) + Vector3.up * 3.8f, -d);
            }
            foreach (float a in new[] { 233f, 307f })
            {
                var d = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad));
                Window(k, d * (R - 0.75f) + Vector3.up * 3.2f, -d, 1.3f, 2.2f);
            }
            board = Sign("BOL DE PROVISIONES", new Vector3(-5.6f, 2.0f, -6.8f), 3.2f, 30);
            Sign("CANAL", new Vector3(-11.4f, 1.7f, -4.6f), 2.0f, -90);
            Sign("TALLER", new Vector3(11.4f, 1.7f, -4.6f), 2.0f, 90);
            Sign("BOMBAS", new Vector3(4.6f, 1.7f, 11.4f), 2.0f, 0);
            // Cesta renovable, cajas y barriles.
            var basket = new GameObject("Cesta de provisiones renovable"); basket.transform.SetParent(currentRoom, false); basket.transform.position = new Vector3(-9.5f, 0.05f, 4.5f);
            var sp = basket.AddComponent<FoodSpawner>(); sp.prefab = foods[0]; sp.respawnDelay = 10; sp.value = 1;
            k.Lathe(new Vector3(-9.5f, 0, 4.5f), new[] { new Vector2(0, 0.02f), new Vector2(0.55f, 0.02f), new Vector2(0.75f, 0.45f), new Vector2(0.68f, 0.45f), new Vector2(0.5f, 0.1f), new Vector2(0, 0.1f) }, Wood, 18);
            Crate(k, new Vector3(10.5f, 0.55f, -6.5f), 1.1f, 15); Crate(k, new Vector3(11.4f, 0.45f, -5.1f), 0.9f, -10); Col("Cajas", new Vector3(10.9f, 0.55f, -5.8f), new Vector3(1.9f, 1.1f, 2.4f));
            Barrel(k, new Vector3(-10.2f, 0, 7.2f)); Barrel(k, new Vector3(-11.1f, 0, 6.3f), 0.42f, 1.05f); Col("Barriles", new Vector3(-10.6f, 0.55f, 6.7f), new Vector3(1.8f, 1.1f, 1.8f));
            Cluster(new Vector3(7.5f, 0, 5f), 3); Cache(new Vector3(9f, 0.6f, -8.8f)); Recovery(new Vector3(-8.5f, 0.02f, -8.5f));

            // Pasillos a las cuatro salas.
            float chord = Mathf.Sqrt(R * R - 25f);
            Corridor(k, new Vector3(0, 0, -chord), new Vector3(0, 0, -18f));
            Corridor(k, new Vector3(-chord, 0, 0), new Vector3(-17f, 0, 0));
            Corridor(k, new Vector3(chord, 0, 0), new Vector3(17f, 0, 0));
            Corridor(k, new Vector3(0, 0, chord), new Vector3(0, 0, 17f));
        }

        // =====================================================================================
        // 01 · Muelle de llegada
        static void BuildDock(out Transform boat)
        {
            var k = Room("01 · Muelle de llegada");
            var pts = new List<Vector3>();
            Arc(pts, new Vector3(8, 0, -35), 7, -90, 0, 4);
            pts.Add(new Vector3(15, 0, -18)); pts.Add(new Vector3(-15, 0, -18));
            Arc(pts, new Vector3(-8, 0, -35), 7, 180, 270, 4);
            Polygon(k, pts, H, new Dictionary<int, Hole[]> { { 5, new[] { new Hole(15f, 6f) } } });

            var pc = new Vector2(2, -31); var half = new Vector2(10.5f, 6.5f); const float pr = 4.5f, surf = -0.35f;
            Func<Vector2, float> pool = p => SdRoundBox(p, pc, half, pr);
            Floor(k, new Rect(-15, -42, 30, 24), 0, pool);
            Curb(k, RoundBoxOutline(pc, half, pr), 0f, PoolEdge, StoneGray, false, 0.85f);
            WaterBody(currentRoom, "Agua del muelle", new Rect(-9.2f, -38.2f, 22.4f, 14.4f), surf, pool, 3f);

            // Balsa naranja.
            var raftRoot = new GameObject("Balsa de expedición").transform; raftRoot.SetParent(currentRoom, false); raftRoot.position = new Vector3(1.5f, -0.55f, -31f);
            var rk = new K { bake = false }; var keep = colGroup; colGroup = raftRoot; Raft(rk, raftRoot.position, 0); colGroup = keep;
            rk.Flush("Modelo de la balsa", raftRoot, vertexMat, raftRoot.position).transform.localPosition = Vector3.zero;
            boat = raftRoot;
            var rippleK = new K { bake = false };
            for (int i = 0; i < 16; i++) { float a = i * Mathf.PI / 8; rippleK.Sphere(raftRoot.position + new Vector3(Mathf.Cos(a) * 3.05f, 0.21f, Mathf.Sin(a) * 2.05f), new Vector3(0.35f, 0.05f, 0.35f), White, 6); }
            rippleK.Flush("Espuma de la balsa", currentRoom, unlitMat, Vector3.zero, false, false);

            // Muelle de madera.
            float dy = 0.32f; var dock = new Rect(-12f, -30.5f, 7.6f, 5f);
            for (float x = dock.xMin + 0.25f; x < dock.xMax; x += 0.52f) k.Box(new Vector3(x, dy - 0.08f, dock.center.y), new Vector3(0.46f, 0.16f, dock.height + Rf(-0.1f, 0.1f)), Quaternion.Euler(0, Rf(-1, 1), 0), V(rnd.NextDouble() < 0.5 ? Wood : WoodLight, 0.05f));
            foreach (float z in new[] { dock.yMin + 0.3f, dock.yMax - 0.3f }) k.Box(new Vector3(dock.center.x, dy - 0.25f, z), new Vector3(dock.width, 0.22f, 0.22f), Quaternion.identity, WoodDark);
            foreach (float x in new[] { dock.xMax - 0.3f, dock.xMax - 2.8f, dock.xMax - 5.3f })
                foreach (float z in new[] { dock.yMin + 0.25f, dock.yMax - 0.25f })
                {
                    bool tall = x == dock.xMax - 0.3f;
                    k.Cyl(new Vector3(x, -2.3f, z), new Vector3(x, tall ? 1.15f : dy - 0.17f, z), 0.22f, V(WoodDark, 0.04f), 10, true);
                    if (tall) k.Cyl(new Vector3(x, 1.15f, z), new Vector3(x, 1.25f, z), 0.26f, Yellow, 10, true);
                }
            Col("Muelle", new Vector3(dock.center.x, dy - 0.3f, dock.center.y), new Vector3(dock.width, 0.6f, dock.height));
            Rope(k, new Vector3(-7.5f, dy, -26.3f)); Rope(k, new Vector3(-6.7f, dy, -26.9f), 0.35f);

            // Gran tubería naranja con reja y cascada.
            var mouth = new Vector3(13.4f, 3.1f, -31f); var face = Vector3.left; var frot = Face(face);
            k.Cyl(new Vector3(15.2f, mouth.y, mouth.z), mouth + face * 0.2f, 2.85f, Orange, 32, false);
            k.Torus(mouth + face * 0.25f, frot, 2.75f, 0.5f, Orange, 40, 12);
            k.Torus(new Vector3(14.6f, mouth.y, mouth.z), frot, 2.95f, 0.35f, OrangeDark, 40, 8);
            k.Cyl(new Vector3(14.9f, mouth.y, mouth.z), new Vector3(14.8f, mouth.y, mouth.z), 2.4f, new Color(0.1f, 0.06f, 0.12f), 32);
            for (int i = 0; i < 20; i++) { float a = i * Mathf.PI / 10; k.Sphere(mouth + face * 0.7f + frot * new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 2.75f, Vector3.one * 0.13f, OrangeDark, 6); }
            for (int i = -3; i <= 3; i++) { float z = i * 0.62f; float hh = Mathf.Sqrt(Mathf.Max(0, 2.3f * 2.3f - z * z)); k.Cyl(new Vector3(mouth.x - 0.1f, mouth.y - hh, mouth.z + z), new Vector3(mouth.x - 0.1f, mouth.y + hh, mouth.z + z), 0.12f, new Color(0.42f, 0.36f, 0.5f), 8); }
            k.Cyl(new Vector3(mouth.x - 0.1f, mouth.y + 0.6f, mouth.z - 2.2f), new Vector3(mouth.x - 0.1f, mouth.y + 0.6f, mouth.z + 2.2f), 0.09f, new Color(0.42f, 0.36f, 0.5f), 8);
            var sheet = new K { bake = false }; sheet.Quad(new Vector3(15f, 0.75f, -32.5f), new Vector3(13.1f, 0.75f, -32.5f), new Vector3(13.1f, 0.75f, -29.5f), new Vector3(15f, 0.75f, -29.5f), Color.white, Vector3.up);
            sheet.Flush("Agua de la tubería", currentRoom, waterMat, Vector3.zero, false, false);
            Waterfall(currentRoom, new Vector3(13.05f, 0.78f, -31f), face, 3.6f, surf, 0.9f);
            Col("Tubería grande", new Vector3(14.3f, mouth.y, mouth.z), new Vector3(1.9f, 6.4f, 6.4f));

            // Tubos menta con válvula a los lados del paso norte.
            foreach (int s in new[] { -1, 1 })
            {
                var b = new Vector3(s * 5.6f, 0, -18.75f);
                k.Block(b + Vector3.up * 0.35f, new Vector3(1.3f, 0.7f, 1.2f), Quaternion.identity, V(StoneGray, 0.03f));
                Pipe(k, b + Vector3.up * 0.7f, b + Vector3.up * 6.4f, 0.42f, Mint, 2.2f);
                Elbow(k, b + Vector3.up * 7.2f, Vector3.up, Vector3.forward, 0.8f, 0.42f, Mint);
                Pipe(k, b + Vector3.up * 7.2f + Vector3.forward * 0.8f, b + Vector3.up * 7.2f + Vector3.forward * 1.8f, 0.42f, Mint, 2f);
                Valve(k, b + Vector3.up * 1.8f + Vector3.back * 0.5f, Vector3.back, 0.42f);
                Col("Tubo", b + Vector3.up * 3.5f, new Vector3(1.3f, 7f, 1.3f));
            }
            LifeRing(k, new Vector3(-7.6f, 3.0f, -18.3f), Vector3.back);
            Valve(k, new Vector3(7.6f, 2.8f, -18.3f), Vector3.back, 0.55f);
            foreach (float x in new[] { -11.6f, 11.6f }) Lantern(k, new Vector3(x, 4.2f, -18.1f), Vector3.back);
            Lantern(k, new Vector3(-14.9f, 4f, -26.5f), Vector3.right); Lantern(k, new Vector3(14.9f, 4f, -22f), Vector3.left);
            foreach (float x in new[] { -3.5f, 5.5f }) Lantern(k, new Vector3(x, 4f, -41.9f), Vector3.forward);
            foreach (var p in new[] { new Vector3(-4.5f, 0, -23.35f), new Vector3(8.5f, 0, -23.35f), new Vector3(-9.8f, 0, -36.2f), new Vector3(2f, 0, -38.7f), new Vector3(13.6f, 0, -36.5f) }) Bollard(k, p);
            Crate(k, new Vector3(-13.6f, 0.6f, -20.6f), 1.2f, 8); Crate(k, new Vector3(-12.2f, 0.45f, -20.2f), 0.9f, -12); Crate(k, new Vector3(-13.5f, 1.65f, -20.7f), 0.85f, 20);
            Col("Cajas", new Vector3(-13f, 0.8f, -20.5f), new Vector3(2.6f, 1.6f, 1.6f));
            Barrel(k, new Vector3(-13.9f, 0, -23.2f)); Col("Barril", new Vector3(-13.9f, 0.55f, -23.2f), new Vector3(0.9f, 1.1f, 0.9f));
            Crate(k, new Vector3(-10.6f, dy + 0.4f, -29.6f), 0.8f, 25); Col("Caja", new Vector3(-10.6f, dy + 0.4f, -29.6f), Vector3.one * 0.8f);
            HazardStrip(k, new Vector3(-3, 0, -18.4f), new Vector3(3, 0, -18.4f), 0.55f, 0.03f);
            Sign("ALCANTARILLAS\nLLENA EL BOL · VUELVE A LA BALSA", new Vector3(5.5f, 1.8f, -21.2f), 3.6f, 0);
            Cluster(new Vector3(-14.3f, 0, -28.6f), 3); Food(new Vector3(-6.5f, dy, -28.5f)); Cache(new Vector3(11f, 0.6f, -20.5f));
            PhysCrate("Caja suelta", new Vector3(9.5f, 0.5f, -40f), 1f, 3);
        }

        // =====================================================================================
        // 03 · Canal con plataformas lima y aros
        static void BuildCanal()
        {
            var k = Room("03 · Canal de los aros");
            Polygon(k, new List<Vector3> { new(-40, 0, -12), new(-17, 0, -12), new(-17, 0, 7), new(-40, 0, 7) }, H,
                new Dictionary<int, Hole[]> { { 1, new[] { new Hole(12f, 6f) } }, { 2, new[] { new Hole(16f, 6f) } } });
            var line = Smooth(new List<Vector2> { new(-21f, 5.6f), new(-22.6f, 1.6f), new(-26.6f, -1.1f), new(-31f, -1.5f), new(-35f, 0.2f), new(-38.3f, 2.2f) }, 8);
            const float hw = 2.4f, surf = -0.4f;
            Func<Vector2, float> canal = p => SdPolyline(p, line, hw);
            Floor(k, new Rect(-40, -12, 23, 19), 0, canal);
            Curb(k, CanalOutline(line, hw), 0f, Yellow, Black, true, 0.75f);
            WaterBody(currentRoom, "Agua del canal", new Rect(-41f, -4.5f, 22f, 12.5f), surf, canal, 2.4f);
            LimePad(k, new Vector3(-24.6f, 0.05f, 0.1f), 1.05f, surf);
            LimePad(k, new Vector3(-29.2f, 0.05f, -1.45f), 1.15f, surf);
            LimePad(k, new Vector3(-35.6f, 0.05f, 0.6f), 1.1f, surf);
            // Tubo naranja con aros de enganche.
            Pipe(k, new Vector3(-39.9f, 6.3f, -0.6f), new Vector3(-21.6f, 6.3f, -0.6f), 0.22f, Orange, 3f);
            Elbow(k, new Vector3(-20.8f, 6.3f, -0.6f), Vector3.right, Vector3.forward, 0.8f, 0.22f, Orange);
            Pipe(k, new Vector3(-20.8f, 6.3f, 0.2f), new Vector3(-20.8f, 6.3f, 6.9f), 0.22f, Orange, 3f);
            k.Box(new Vector3(-20.8f, 6.3f, 6.7f), new Vector3(0.8f, 0.8f, 0.4f), Quaternion.identity, SteelDark);
            foreach (float x in new[] { -39.6f }) k.Box(new Vector3(x, 6.3f, -0.6f), new Vector3(0.4f, 0.8f, 0.8f), Quaternion.identity, SteelDark);
            var pipeCol = new GameObject("Tubo de los aros"); pipeCol.transform.SetParent(colGroup, false); pipeCol.transform.position = new Vector3(-30.6f, 6.3f, -0.6f);
            pipeCol.AddComponent<BoxCollider>().size = new Vector3(18.8f, 0.45f, 0.45f); pipeCol.AddComponent<TongueTarget>().kind = TongueTargetKind.Surface;
            foreach (float x in new[] { -22.4f, -27f, -32f, -37f }) Anchor(new Vector3(x, 4.3f, -0.6f), 6.1f);
            // Cascada desde la pared norte.
            Pipe(k, new Vector3(-21f, 2.7f, 7.2f), new Vector3(-21f, 2.7f, 6.1f), 0.48f, Mint, 2f);
            Waterfall(currentRoom, new Vector3(-21f, 2.35f, 5.95f), Vector3.back, 0.85f, surf, 0.5f);
            Valve(k, new Vector3(-19.4f, 2.8f, 6.75f), Vector3.back, 0.4f);
            // Ventanas, faroles y atrezo.
            foreach (float x in new[] { -25.6f, -37.4f }) Window(k, new Vector3(x, 2.6f, 6.75f), Vector3.back, 1.4f, 2.5f);
            foreach (float x in new[] { -25.6f, -31.4f }) Lantern(k, new Vector3(x, 4f, -11.9f), Vector3.forward);
            Lantern(k, new Vector3(-39.9f, 4f, -4f), Vector3.right);
            foreach (var (p, s, y) in new[] { (new Vector3(-38.5f, 0.6f, -10.3f), 1.2f, 10f), (new Vector3(-37.1f, 0.5f, -10.5f), 1f, -8f), (new Vector3(-38.4f, 1.7f, -10.2f), 0.95f, 20f), (new Vector3(-38.6f, 0.55f, -8.9f), 1.1f, 0f) }) Crate(k, p, s, y);
            Col("Cajas", new Vector3(-38f, 0.9f, -9.9f), new Vector3(2.8f, 1.8f, 2.8f));
            Crate(k, new Vector3(-22f, 0.55f, -10.7f), 1.1f, 12); Crate(k, new Vector3(-20.8f, 0.45f, -10.9f), 0.9f, -5); Col("Cajas", new Vector3(-21.5f, 0.55f, -10.8f), new Vector3(2.4f, 1.1f, 1.3f));
            Barrel(k, new Vector3(-39f, 0, 5.6f)); Col("Barril", new Vector3(-39f, 0.55f, 5.6f), new Vector3(0.9f, 1.1f, 0.9f));
            HazardStrip(k, new Vector3(-36f, 0, 6.6f), new Vector3(-30f, 0, 6.6f), 0.55f, 0.03f);
            Sign("CANAL\nSALTA A LAS PLATAFORMAS O USA LOS AROS", new Vector3(-21.5f, 1.8f, -6.5f), 3.6f, -60);
            Cluster(new Vector3(-31f, 0, 3.4f), 6); Cluster(new Vector3(-28f, 0, -9.5f), 4); Food(new Vector3(-29.2f, 0.12f, -1.45f));
            Cache(new Vector3(-37.2f, 0.6f, 5.8f)); Recovery(new Vector3(-19f, 0.02f, -9.8f));
            PhysCrate("Caja suelta", new Vector3(-25f, 0.5f, -6f), 1f, 3);
        }

        // =====================================================================================
        // 04 · Almacén con estantes, rejillas y balcón
        static void BuildStore()
        {
            var k = Room("04 · Almacén de latas");
            Polygon(k, new List<Vector3> { new(-40, 0, 9), new(-17, 0, 9), new(-17, 0, 30), new(-40, 0, 30) }, H,
                new Dictionary<int, Hole[]> { { 0, new[] { new Hole(7f, 6f) } }, { 1, new[] { new Hole(17f, 4f, 5.0f, 2.4f) } } }, false);
            Floor(k, new Rect(-40, 9, 23, 21), 0);
            Floor(k, new Rect(-40, 7, 23, 2), 0, p => p.x > -36f && p.x < -30f ? 1 : -1, 1.0f, false);
            Col("Paso al canal", new Vector3(-33, -1.25f, 8), new Vector3(6, 2.5f, 2.2f));
            // Balcón con arcadas y escalera.
            var bal = new Rect(-21.6f, 21.5f, 4.6f, 8.5f); const float by = 2.4f;
            Floor(k, bal, by, null, 1.15f, false);
            Col("Balcón", new Vector3(bal.center.x, by / 2, bal.center.y), new Vector3(bal.width, by, bal.height));
            Arcade(k, new Vector3(bal.xMin, 0, bal.yMin), new Vector3(bal.xMin, 0, bal.yMax), by, Vector3.left);
            // Cara sur del balcón: muro de piedra macizo (la escalera se apoya aquí, sin arco que parezca una puerta).
            k.Box(new Vector3(bal.center.x, by / 2, bal.yMin + 0.2f), new Vector3(bal.width, by, 0.4f), Quaternion.identity, StoneGray2);
            for (float x = bal.xMin + 0.4f; x < bal.xMax; x += 1.1f) for (float yy = 0.3f; yy < by - 0.2f; yy += 0.6f) k.Box(new Vector3(x + ((int)(yy / 0.6f) % 2) * 0.5f, yy, bal.yMin - 0.01f), new Vector3(1.0f, 0.5f, 0.06f), Quaternion.identity, V(StoneGray, 0.05f));
            k.Block(new Vector3(bal.center.x, by + 0.1f, bal.yMin + 0.2f), new Vector3(bal.width + 0.2f, 0.25f, 0.8f), Quaternion.identity, V(StoneGray2, 0.03f));
            Railing(k, new Vector3(bal.xMin + 0.15f, by, bal.yMin + 3.2f), new Vector3(bal.xMin + 0.15f, by, bal.yMax - 0.2f));
            Stairs(k, new Vector3(-20f, 0, 14.6f), new Vector3(-20f, by, bal.yMin), 2.9f, StoneGray, StoneGray2);
            Railing(k, new Vector3(-21.55f, 0, 14.6f), new Vector3(-21.55f, by, bal.yMin)); Railing(k, new Vector3(-21.55f, by, bal.yMin), new Vector3(-21.55f, by, bal.yMin + 3.2f));
            Deck(k, new Rect(-17.2f, 24f, 2.4f, 4f), by);
            // Estantes con latas.
            foreach (float z in new[] { 12.6f, 16f, 22.6f }) Shelf(k, new Vector3(-39.25f, 0, z), 90, 2.8f, 2.6f, 0.8f, 4, true);
            Shelf(k, new Vector3(-37.3f, 0, 29.25f), 180, 2.8f, 2.6f, 0.8f, 4, true);
            Shelf(k, new Vector3(-25.2f, 0, 29.25f), 180, 2.6f, 2.2f, 0.8f, 3, true, true);
            // Cajas, rejillas, tubos naranjas de suelo, ventana redonda.
            foreach (var (p, s, y) in new[] { (new Vector3(-24.2f, 0.6f, 11f), 1.2f, 5f), (new Vector3(-22.9f, 0.5f, 10.7f), 1f, -10f), (new Vector3(-24f, 1.7f, 11.1f), 0.95f, 15f), (new Vector3(-26f, 0.45f, 10.8f), 0.9f, 30f) }) Crate(k, p, s, y);
            Col("Cajas", new Vector3(-24.3f, 0.9f, 11f), new Vector3(4.2f, 1.8f, 1.6f));
            foreach (var (p, s, y) in new[] { (new Vector3(-36.6f, 0.6f, 25.6f), 1.2f, -5f), (new Vector3(-35.3f, 0.45f, 26.2f), 0.9f, 15f) }) Crate(k, p, s, y);
            Col("Cajas", new Vector3(-36f, 0.6f, 25.9f), new Vector3(2.6f, 1.2f, 1.8f));
            Grate(k, new Vector3(-29.5f, 0, 16.4f), 2.4f, 1.2f, 0); Grate(k, new Vector3(-29.5f, 0, 19.4f), 2.4f, 1.2f, 0);
            foreach (float off in new[] { 0f, 0.3f })
            {
                k.Cyl(new Vector3(-32.6f - off, 0.1f, 9.4f), new Vector3(-32.6f - off, 0.1f, 22.6f + off), 0.08f, Orange, 8);
                k.Cyl(new Vector3(-32.6f - off, 0.1f, 22.6f + off), new Vector3(-22.6f, 0.1f, 22.6f + off), 0.08f, Orange, 8);
                k.Sphere(new Vector3(-32.6f - off, 0.1f, 22.6f + off), Vector3.one * 0.1f, OrangeDark, 8);
            }
            RoundWindow(k, new Vector3(-31.3f, 4f, 29.75f), Vector3.back, 1.6f);
            Pipe(k, new Vector3(-38.9f, 0, 28.9f), new Vector3(-38.9f, 5.4f, 28.9f), 0.4f, Mint, 2.2f);
            Elbow(k, new Vector3(-38.9f, 6.2f, 28.9f), Vector3.up, Vector3.right, 0.8f, 0.4f, Mint);
            Pipe(k, new Vector3(-38.1f, 6.2f, 28.9f), new Vector3(-31.8f, 6.2f, 28.9f), 0.4f, Mint, 2.2f);
            Valve(k, new Vector3(-38.4f, 1.6f, 28.9f), Vector3.right, 0.42f); Col("Tubo", new Vector3(-38.9f, 3f, 28.9f), new Vector3(0.9f, 6f, 0.9f));
            Lantern(k, new Vector3(-39.9f, 4f, 19.3f), Vector3.right); Lantern(k, new Vector3(-36.5f, 4.2f, 29.9f), Vector3.back); Lantern(k, new Vector3(-25.6f, 4.2f, 29.9f), Vector3.back);
            Lantern(k, new Vector3(-26f, 4f, 9.1f), Vector3.forward); Lantern(k, new Vector3(-17.1f, 4.4f, 12.5f), Vector3.left);
            Sign("ALMACÉN\nABRE LAS CAJAS [E]", new Vector3(-31f, 1.8f, 11.5f), 3.2f, 0);
            Cache(new Vector3(-37.2f, 0.6f, 10.8f)); Cache(new Vector3(-22.5f, 0.6f, 27.6f)); Cache(new Vector3(-34.5f, 0.6f, 22.6f));
            Cluster(new Vector3(-31f, 0, 24f), 6); Food(new Vector3(-26.5f, 0, 15f), 2, 4, 1.8f); Cluster(new Vector3(-20.6f, by, 26.6f), 3);
            Recovery(new Vector3(-37.5f, 0.02f, 18.4f));
            PhysCrate("Caja suelta", new Vector3(-27f, 0.5f, 20.5f), 1f, 3);
        }

        /// <summary>Arcada de piedra gris bajo balcones y plataformas.</summary>
        static void Arcade(K k, Vector3 a, Vector3 b, float h, Vector3 facing, int skipFirst = 0)
        {
            var d = b - a; float L = d.magnitude; d /= L; var rot = Face(facing);
            int n = Mathf.Max(1, Mathf.RoundToInt(L / 2.6f)); float bay = L / n;
            k.Box((a + b) / 2 + Vector3.up * (h - 0.25f) - facing * 0.3f, new Vector3(L, 0.5f, 0.6f), rot, StoneGray);
            k.Box((a + b) / 2 + Vector3.up * (h * 0.5f) - facing * 0.45f, new Vector3(L, h, 0.3f), rot, StoneGray2 * 0.55f);
            for (int i = 0; i <= n; i++)
            {
                var p = a + d * (bay * i) - facing * 0.3f;
                k.Block(p + Vector3.up * (h - 0.6f) / 2, new Vector3(0.55f, h - 0.5f, 0.65f), rot, V(StoneGray, 0.03f));
                if (i == n) break;
                // Arco: paños de piedra entre la curva y la viga, y dovelas claras sobre la curva.
                float r = bay * 0.5f - 0.28f, spring = Mathf.Max(0.5f, h - 0.55f - r), top = h - 0.5f;
                var cu = a + d * (bay * (i + 0.5f)) + facing * 0.01f;
                const int steps = 14;
                for (int s = 0; s < steps; s++)
                {
                    float x0 = -r + 2 * r * s / steps, x1 = -r + 2 * r * (s + 1) / steps;
                    float y0 = spring + Mathf.Sqrt(Mathf.Max(0, r * r - x0 * x0)), y1 = spring + Mathf.Sqrt(Mathf.Max(0, r * r - x1 * x1));
                    k.Quad(cu + d * x0 + Vector3.up * y0, cu + d * x0 + Vector3.up * top, cu + d * x1 + Vector3.up * top, cu + d * x1 + Vector3.up * y1, StoneGray * 0.95f, facing);
                    k.Quad(cu + d * x0 + Vector3.up * y0, cu + d * x1 + Vector3.up * y1, cu + d * x1 + Vector3.up * y1 - facing * 0.6f, cu + d * x0 + Vector3.up * y0 - facing * 0.6f, StoneGray2, Vector3.down);
                }
                for (int s = 0; s <= 6; s++)
                {
                    float t = Mathf.PI * s / 6; var rad = (d * Mathf.Cos(t) + Vector3.up * Mathf.Sin(t)).normalized;
                    k.Block(cu + d * (Mathf.Cos(t) * (r + 0.1f)) + Vector3.up * (spring + Mathf.Sin(t) * (r + 0.1f)) + facing * 0.04f, new Vector3(0.36f, 0.3f, 0.12f), Quaternion.LookRotation(facing, rad), V(new Color(0.6f, 0.6f, 0.72f), 0.04f));
                }
            }
            k.Block((a + b) / 2 + Vector3.up * (h + 0.1f) - facing * 0.3f, new Vector3(L + 0.2f, 0.25f, 0.8f), rot, V(StoneGray2, 0.03f));
        }

        // =====================================================================================
        // 05 · Taller con placa azul, persiana y cámara de reserva
        static void BuildWorkshop()
        {
            var k = Room("05 · Taller de la placa");
            Polygon(k, new List<Vector3> { new(17, 0, -12), new(42, 0, -12), new(42, 0, 7), new(17, 0, 7) }, H,
                new Dictionary<int, Hole[]> { { 1, new[] { new Hole(9.5f, 6f, 4.0f, 0, false) } }, { 2, new[] { new Hole(5f, 6f) } }, { 3, new[] { new Hole(7f, 6f) } } });
            Floor(k, new Rect(17, -12, 25, 19), 0);
            Floor(k, new Rect(34, 7, 6, 2), 0, null, 1f, true);
            // Cámara de reserva tras la persiana.
            Polygon(k, new List<Vector3> { new(43, 0, -8), new(50, 0, -8), new(50, 0, 3), new(43, 0, 3) }, H, new Dictionary<int, Hole[]> { { 3, new[] { new Hole(5.5f, 6f, 4.0f, 0, false) } } }, false);
            Floor(k, new Rect(42, -8, 8, 11), 0);
            Shelf(k, new Vector3(49.25f, 0, -2.5f), -90, 3f, 2.4f, 0.8f, 4, true);
            Lantern(k, new Vector3(46.5f, 4f, 2.9f), Vector3.back);
            Cluster(new Vector3(44.5f, 0, -7f), 6); Food(new Vector3(47.5f, 0, 1.2f), 4, 4, 1.8f); Food(new Vector3(45.5f, 0, 1.4f), 0, 4, 1.8f); Cache(new Vector3(48.4f, 0.6f, -6.8f));
            // Persiana con franjas (se levanta con la placa).
            float gz = -2.5f;
            foreach (int s in new[] { -1, 1 })
            {
                k.Box(new Vector3(41.65f, 2.5f, gz + s * 3.35f), new Vector3(0.6f, 5f, 0.6f), Quaternion.identity, new Color(0.45f, 0.55f, 0.55f));
                k.Block(new Vector3(41.6f, 5.3f, gz + s * 3.35f), new Vector3(0.95f, 0.7f, 0.95f), Quaternion.identity, Orange);
                k.Block(new Vector3(41.6f, 0.2f, gz + s * 3.35f), new Vector3(0.8f, 0.4f, 0.8f), Quaternion.identity, Yellow);
            }
            k.Box(new Vector3(41.6f, 4.75f, gz), new Vector3(0.7f, 0.9f, 7.4f), Quaternion.identity, new Color(0.36f, 0.33f, 0.45f));
            for (int i = 0; i < 9; i++) k.Sphere(new Vector3(41.22f, 4.75f, gz - 3f + i * 0.75f), Vector3.one * 0.07f, Orange, 6);
            var doorK = new K { bake = false }; var dc = new Vector3(42.5f, 2.0f, gz);
            doorK.Box(dc, new Vector3(0.3f, 4f, 6f), Quaternion.identity, Black);
            foreach (int s in new[] { -1, 1 })
            {
                doorK.Stripes(new Vector3(42.5f + s * 0.16f, 0.2f, gz - 2.8f), new Vector3(0, 0, 5.6f), new Vector3(0, 3.6f, 0), 0.7f, Yellow, Black, new Vector3(s, 0, 0), 0.005f);
                for (int i = 0; i < 4; i++) doorK.Box(new Vector3(42.5f + s * 0.17f, 0.5f + i * 1f, gz), new Vector3(0.02f, 0.05f, 5.6f), Quaternion.identity, Black * 1.5f);
            }
            var gateGo = doorK.Flush("Persiana de la reserva", currentRoom, vertexMat, dc);
            gateGo.AddComponent<BoxCollider>().size = new Vector3(0.4f, 4f, 6f); var door = gateGo.AddComponent<Door>(); door.openOffset = new Vector3(0, 4.1f, 0);
            HazardStrip(k, new Vector3(41.0f, 0, gz - 3f), new Vector3(41.0f, 0, gz + 3f), 0.55f, 0.03f);
            // Placa azul en un cuadro amarillo.
            var pp = new Vector3(29f, 0, -2.5f);
            foreach (int s in new[] { -1, 1 })
            {
                k.Box(pp + new Vector3(0, 0.03f, s * 2.25f), new Vector3(4.7f, 0.03f, 0.22f), Quaternion.identity, Yellow);
                k.Box(pp + new Vector3(s * 2.25f, 0.03f, 0), new Vector3(0.22f, 0.03f, 4.7f), Quaternion.identity, Yellow);
            }
            k.Box(pp + Vector3.up * 0.06f, new Vector3(3f, 0.12f, 3f), Quaternion.identity, Orange);
            for (int i = 0; i < 4; i++) { var o = new Vector3(i % 2 == 0 ? -1.35f : 1.35f, 0.13f, i < 2 ? -1.35f : 1.35f); k.Sphere(pp + o, new Vector3(0.09f, 0.05f, 0.09f), OrangeDark, 6); }
            var plateK = new K { bake = false }; plateK.Block(pp + Vector3.up * 0.15f, new Vector3(2.6f, 0.16f, 2.6f), Quaternion.identity, Color.white);
            for (int i = 0; i < 4; i++) { var o = new Vector3(i % 2 == 0 ? -1.05f : 1.05f, 0.24f, i < 2 ? -1.05f : 1.05f); plateK.Sphere(pp + o, new Vector3(0.08f, 0.04f, 0.08f), new Color(0.8f, 0.85f, 1f), 6); }
            var plateVis = plateK.Flush("Placa azul", currentRoom, plateMat, pp + Vector3.up * 0.15f);
            var plateGo = new GameObject("Placa de carga · 12 kg"); plateGo.transform.SetParent(currentRoom, false); plateGo.transform.position = pp;
            var ptc = plateGo.AddComponent<BoxCollider>(); ptc.isTrigger = true; ptc.size = new Vector3(2.6f, 1.3f, 2.6f); ptc.center = Vector3.up * 0.65f;
            Col("Placa", pp + Vector3.up * 0.06f, new Vector3(3f, 0.12f, 3f));
            var plate = plateGo.AddComponent<PressurePlate>(); plate.requiredMass = 12; plate.doors = new[] { door }; plate.plateVisual = plateVis.transform;
            var weight = PhysCrate("Contrapeso marcado · 20 kg", new Vector3(22.5f, 0.75f, -8.6f), 1.4f, 20, true);
            Sign("CAJA MARCADA → PLACA AZUL", new Vector3(26.5f, 1.7f, -11f), 3.6f, 180);
            // Taller: panel de herramientas, banco, estantes, ventana, tubos.
            ToolBoard(k, new Vector3(25.5f, 2.6f, 6.85f), Vector3.back);
            Workbench(k, new Vector3(25.5f, 0, 6.2f), 0);
            Shelf(k, new Vector3(19.5f, 0, 6.25f), 180, 2.4f, 2.4f, 0.8f, 3, false, true);
            Window(k, new Vector3(31.5f, 3.2f, 6.75f), Vector3.back, 2.4f, 3.0f);
            Pipe(k, new Vector3(40.9f, 0, 5.9f), new Vector3(40.9f, 6.3f, 5.9f), 0.45f, Mint, 2.2f);
            Elbow(k, new Vector3(40.9f, 7.1f, 5.9f), Vector3.up, Vector3.forward, 0.8f, 0.45f, Mint);
            Pipe(k, new Vector3(40.9f, 7.1f, 6.7f), new Vector3(40.9f, 7.1f, 8f), 0.45f, Mint, 2f);
            Col("Tubo", new Vector3(40.9f, 3.2f, 5.9f), new Vector3(1f, 6.4f, 1f));
            foreach (var (p, s, y) in new[] { (new Vector3(18.6f, 0.6f, -10.4f), 1.2f, 8f), (new Vector3(19.9f, 0.5f, -10.7f), 1f, -6f), (new Vector3(18.7f, 1.7f, -10.3f), 0.95f, 25f), (new Vector3(18.5f, 0.55f, -9f), 1.1f, 0f), (new Vector3(21.1f, 0.45f, -10.8f), 0.9f, 12f) }) Crate(k, p, s, y);
            Col("Cajas", new Vector3(19.6f, 0.9f, -10f), new Vector3(4f, 1.8f, 2.8f));
            foreach (var (p, s, y) in new[] { (new Vector3(39.6f, 0.6f, -10.4f), 1.2f, -8f), (new Vector3(40.8f, 0.5f, -9.2f), 1f, 10f), (new Vector3(38.3f, 0.45f, -10.8f), 0.9f, 0f) }) Crate(k, p, s, y);
            Col("Cajas", new Vector3(39.6f, 0.6f, -10f), new Vector3(3.6f, 1.2f, 2.8f));
            Crate(k, new Vector3(36.4f, 0.55f, 5.6f), 1.1f, 10); Crate(k, new Vector3(37.6f, 0.45f, 5.9f), 0.9f, -15); Col("Cajas", new Vector3(37f, 0.55f, 5.7f), new Vector3(2.4f, 1.1f, 1.3f));
            Lantern(k, new Vector3(17.1f, 4f, -7f), Vector3.right); Lantern(k, new Vector3(17.1f, 4f, 4.5f), Vector3.right);
            Lantern(k, new Vector3(27f, 4f, -11.9f), Vector3.forward); Lantern(k, new Vector3(33f, 4f, -11.9f), Vector3.forward); Lantern(k, new Vector3(41.9f, 4f, -9f), Vector3.left);
            Cluster(new Vector3(32f, 0, -9.8f), 4); Cache(new Vector3(22f, 0.6f, 3.6f)); Recovery(new Vector3(34f, 0.02f, 3.5f));
        }

        // =====================================================================================
        // 06 · Depósito con cascadas, pasarelas y elevador
        static void BuildTank()
        {
            var k = Room("06 · Depósito de las cascadas");
            Polygon(k, new List<Vector3> { new(17, 0, 9), new(42, 0, 9), new(42, 0, 30), new(17, 0, 30) }, H,
                new Dictionary<int, Hole[]> { { 0, new[] { new Hole(20f, 6f) } }, { 3, new[] { new Hole(4f, 4f, 5.0f, 2.4f) } } });
            const float surf = -0.55f, up = 2.4f;
            Func<Vector2, float> tank = p => SdRoundBox(p, new Vector2(30f, 21.6f), new Vector2(8f, 5.2f), 0.6f);
            Floor(k, new Rect(17, 9, 25, 18), 0, tank);
            Curb(k, RoundBoxOutline(new Vector2(30f, 21.6f), new Vector2(8f, 5.2f), 0.6f), 0f, StoneGray, PoolEdge, false, 0.8f);
            WaterBody(currentRoom, "Agua del depósito", new Rect(21.5f, 15.8f, 17f, 11.6f), surf, tank, 2.6f);
            // Repisa superior con tres depósitos y las bocas de las cascadas.
            var ledge = new Rect(21.5f, 27f, 20.5f, 3f);
            Floor(k, ledge, up, null, 1.2f, false); Col("Repisa", new Vector3(ledge.center.x, up / 2, ledge.center.y), new Vector3(ledge.width, up, ledge.height));
            Wall(k, new Vector3(42f, 0, 27f), new Vector3(21.5f, 0, 27f), up, null, false, new Color(0.85f, 0.85f, 0.95f));
            foreach (float x in new[] { 26f, 30f, 34f })
            {
                k.Block(new Vector3(x, 1.5f, 26.7f), new Vector3(1.6f, 0.9f, 0.6f), Quaternion.identity, V(StoneGray, 0.03f));
                k.Box(new Vector3(x, 1.45f, 26.4f), new Vector3(1.1f, 0.5f, 0.1f), Quaternion.identity, new Color(0.1f, 0.06f, 0.12f));
                Waterfall(currentRoom, new Vector3(x, 1.3f, 26.35f), Vector3.back, 1.0f, surf, 0.45f);
            }
            foreach (float x in new[] { 27f, 31f, 35f }) Tank(k, new Vector3(x, up, 28.6f), 1.25f, 2.4f);
            foreach (float x in new[] { 29f, 33f }) k.Torus(new Vector3(x, up + 3.3f, 28.6f), Quaternion.identity, 1.5f, 0.3f, Mint, 16, 10, 180, 0);
            foreach (float x in new[] { 27f, 31f, 35f }) { Pipe(k, new Vector3(x, up + 3.4f, 28.6f), new Vector3(x, up + 3.4f, 30.8f), 0.3f, MintDark, 1.5f); }
            // Pasarela oeste, rampa de rejilla y puente hacia 07.
            Deck(k, new Rect(17f, 19f, 4.6f, 11f), up, 0);
            Railing(k, new Vector3(21.45f, up, 19f), new Vector3(21.45f, up, 27f));
            Stairs(k, new Vector3(19.3f, 0, 11f), new Vector3(19.3f, up, 19f), 4.2f, Steel, SteelDark);
            Railing(k, new Vector3(21.45f, 0, 11f), new Vector3(21.45f, up, 19f));
            Deck(k, new Rect(15f, 24f, 2.4f, 4f), up);
            // Elevador amarillo.
            var lp = new Vector3(40.2f, 0.05f, 25.3f);
            foreach (int sx in new[] { -1, 1 }) foreach (int sz in new[] { -1, 1 }) { k.Box(lp + new Vector3(sx * 1.45f, 2.9f, sz * 1.45f), new Vector3(0.22f, 5.8f, 0.22f), Quaternion.identity, Yellow); Col("Poste del elevador", lp + new Vector3(sx * 1.45f, 2.9f, sz * 1.45f), new Vector3(0.22f, 5.8f, 0.22f)); }
            foreach (float y in new[] { 5.75f, 3.2f }) foreach (int sz in new[] { -1, 1 }) k.Box(lp + new Vector3(0, y, sz * 1.45f), new Vector3(3.1f, 0.18f, 0.18f), Quaternion.identity, Yellow);
            for (int i = 0; i < 6; i++) k.Box(lp + new Vector3(1.45f, 0.45f + i * 0.9f, 0), new Vector3(0.12f, 0.12f, 2.9f), Quaternion.Euler(i % 2 == 0 ? 40 : -40, 0, 0), Yellow * 0.9f);
            k.Torus(lp + new Vector3(0, 5.95f, 0), Quaternion.Euler(0, 90, 0), 0.4f, 0.08f, Orange, 16, 6);
            k.Cyl(lp + new Vector3(0, 5.9f, 0), lp + new Vector3(0, 0.4f, 0), 0.03f, Black, 5);
            var liftK = new K { bake = false };
            liftK.Box(lp, new Vector3(2.5f, 0.24f, 2.5f), Quaternion.identity, Yellow);
            liftK.Stripes(lp + new Vector3(-1.25f, 0.125f, -1.25f), new Vector3(2.5f, 0, 0), new Vector3(0, 0, 0.35f), 0.3f, Yellow, Black, Vector3.up);
            liftK.Box(lp + Vector3.up * 0.14f, new Vector3(1.8f, 0.04f, 1.6f), Quaternion.identity, SteelDark);
            var lift = liftK.Flush("Elevador de inspección", currentRoom, vertexMat, lp);
            lift.AddComponent<BoxCollider>().size = new Vector3(2.5f, 0.24f, 2.5f);
            var lrb = lift.AddComponent<Rigidbody>(); lrb.isKinematic = true; lrb.interpolation = RigidbodyInterpolation.Interpolate;
            var mot = lift.AddComponent<LevelOneMotion>(); mot.travel = new Vector3(0, up + 0.1f, 0); mot.period = 7;
            // Ventanas, faroles, cajas.
            Window(k, new Vector3(41.75f, 2.8f, 16.5f), Vector3.left, 1.5f, 2.6f);
            Window(k, new Vector3(17.25f, 3.6f, 12.0f), Vector3.right, 1.4f, 2.4f);
            Lantern(k, new Vector3(17.1f, 4.5f, 22f), Vector3.right); Lantern(k, new Vector3(41.9f, 4f, 12.5f), Vector3.left); Lantern(k, new Vector3(41.9f, 4.6f, 21.5f), Vector3.left);
            Lantern(k, new Vector3(25.6f, 5.6f, 29.9f), Vector3.back); Lantern(k, new Vector3(38.5f, 5.6f, 29.9f), Vector3.back); Lantern(k, new Vector3(26f, 4f, 9.1f), Vector3.forward);
            Crate(k, new Vector3(23.2f, 0.55f, 10.3f), 1.1f, 5); Crate(k, new Vector3(24.4f, 0.45f, 10.1f), 0.9f, -12); Col("Cajas", new Vector3(23.8f, 0.55f, 10.2f), new Vector3(2.4f, 1.1f, 1.3f));
            Barrel(k, new Vector3(40.9f, 0, 10.2f)); Barrel(k, new Vector3(40f, 0, 10.4f), 0.42f, 1.05f); Col("Barriles", new Vector3(40.4f, 0.55f, 10.3f), new Vector3(1.9f, 1.1f, 1.1f));
            var pallet = new GameObject("Tablones rompibles"); pallet.transform.SetParent(currentRoom, false); pallet.transform.position = new Vector3(40.9f, 1f, 17.6f);
            var pk = new K { bake = false }; for (int i = 0; i < 5; i++) pk.Box(new Vector3(40.9f, 0.2f + i * 0.4f, 17.6f), new Vector3(0.15f, 0.34f, 2.4f), Quaternion.identity, V(Wood, 0.06f));
            foreach (float z in new[] { 16.7f, 18.5f }) pk.Box(new Vector3(40.75f, 1f, z), new Vector3(0.1f, 2f, 0.2f), Quaternion.identity, WoodDark);
            pk.Flush("Modelo", pallet.transform, vertexMat, pallet.transform.position).transform.localPosition = Vector3.zero;
            pallet.AddComponent<BoxCollider>().size = new Vector3(0.3f, 2f, 2.4f); pallet.AddComponent<Breakable>().requiredSpeed = 2.5f;
            Cache(new Vector3(40.2f, 0.6f, 15.3f), 2);
            Slug(new Vector3(23f, 0.4f, 13f), new Vector3(35f, 0.4f, 13f));
            Cluster(new Vector3(38.2f, up, 28.6f), 6); Cache(new Vector3(24.9f, up + 0.6f, 28.9f), 2); Cluster(new Vector3(26f, 0, 11.8f), 4);
            Food(new Vector3(36.5f, 0, 13.5f), 1, 4, 1.8f); Cluster(new Vector3(18.3f, up, 27.2f), 3);
        }

        // =====================================================================================
        // 07 · Sala de bombas con canal, puente y despensa
        static void BuildPumps()
        {
            var k = Room("07 · Sala de las bombas");
            Polygon(k, new List<Vector3> { new(-15, 0, 17), new(15, 0, 17), new(15, 0, 41), new(-15, 0, 41) }, H,
                new Dictionary<int, Hole[]> { { 0, new[] { new Hole(15f, 6f) } }, { 1, new[] { new Hole(9f, 4f, 5.0f, 2.4f) } }, { 2, new[] { new Hole(15f, 9f, 2.3f) } }, { 3, new[] { new Hole(15f, 4f, 5.0f, 2.4f) } } });
            const float up = 2.4f, surf = -0.5f;
            Func<Vector2, float> channel = p => SdRoundBox(p, new Vector2(0, 30.2f), new Vector2(2.5f, 9.7f), 0.3f);
            Floor(k, new Rect(-15, 17, 30, 24), 0, channel);
            Curb(k, RoundBoxOutline(new Vector2(0, 30.2f), new Vector2(2.5f, 9.7f), 0.3f), 0f, OrangeDark, Orange, false, 0.6f);
            WaterBody(currentRoom, "Agua del canal de bombas", new Rect(-3f, 20f, 6f, 20.5f), surf, channel, 2.4f);
            WaterFlowSheet(k);
            HazardStrip(k, new Vector3(-2.6f, 0, 19.75f), new Vector3(2.6f, 0, 19.75f), 0.5f, 0.15f);
            // Plataformas elevadas con fachada de ladrillo, escaleras y bombas.
            foreach (int s in new[] { -1, 1 })
            {
                var r = s < 0 ? new Rect(-15f, 23f, 10.6f, 10f) : new Rect(4.4f, 23f, 10.6f, 10f);
                Floor(k, r, up, null, 1.25f, false); Col("Plataforma", new Vector3(r.center.x, up / 2, r.center.y), new Vector3(r.width, up, r.height));
                float inner = s < 0 ? r.xMax : r.xMin, outer = s < 0 ? r.xMin : r.xMax;
                float stairX = s * 13.45f; // pegada al muro exterior: sin zanja entre la escalera y la pared
                // La fachada frontal mira a -z: dirección con el interior (-z) a la izquierda.
                var fa = s < 0 ? new Vector3(inner, 0, 23f) : new Vector3(outer, 0, 23f); var fb = s < 0 ? new Vector3(outer, 0, 23f) : new Vector3(inner, 0, 23f);
                Wall(k, fa, fb, up, new[] { new Hole(Mathf.Abs(fa.x - stairX), 3.0f, 9f, 2.3f, false) }, false);
                // Lado del canal (con hueco para el puente) y lado trasero.
                if (s < 0) { Wall(k, new Vector3(inner, 0, 33f), new Vector3(inner, 0, 23f), up, new[] { new Hole(5.5f, 3.2f, 9f, 2.3f, false) }, false); Wall(k, new Vector3(outer, 0, 33f), new Vector3(inner, 0, 33f), up, null, false); }
                else { Wall(k, new Vector3(inner, 0, 23f), new Vector3(inner, 0, 33f), up, new[] { new Hole(4.5f, 3.2f, 9f, 2.3f, false) }, false); Wall(k, new Vector3(inner, 0, 33f), new Vector3(outer, 0, 33f), up, null, false); }
                Stairs(k, new Vector3(stairX, 0, 17.6f), new Vector3(stairX, up, 23.1f), 2.7f, StoneGray, StoneGray2);
                Pump(k, new Vector3(s * 9.6f, up, 28.6f), Vector3.back, 41f);
                Lantern(k, new Vector3(s * 14.9f, 4.4f, 37f), new Vector3(-s, 0, 0));
                Window(k, new Vector3(s * 11.2f, 3.4f, 40.75f), Vector3.back, 1.5f, 2.6f);
                Lantern(k, new Vector3(s * 6.5f, 4.2f, 17.1f), Vector3.forward);
            }
            Deck(k, new Rect(-4.6f, 26f, 9.2f, 3f), up, surf - 1.5f);
            foreach (float z in new[] { 26.1f, 28.9f }) Railing(k, new Vector3(-4.4f, up, z), new Vector3(4.4f, up, z));
            Deck(k, new Rect(-2.9f, 36.2f, 5.8f, 1.6f), 0.12f);
            // Despensa del fondo tras la membrana ácida.
            Wall(k, new Vector3(4.5f, 0, 40.6f), new Vector3(4.5f, 0, 45.5f), H, null, false); Wall(k, new Vector3(4.5f, 0, 45.5f), new Vector3(-4.5f, 0, 45.5f), H, null, false); Wall(k, new Vector3(-4.5f, 0, 45.5f), new Vector3(-4.5f, 0, 40.6f), H, null, false);
            Floor(k, new Rect(-4.5f, 40f, 9f, 5.5f), 0);
            foreach (int s in new[] { -1, 1 }) Shelf(k, new Vector3(s * 2.4f, 0, 44.9f), 180, 2.6f, 2.5f, 0.7f, 4, true);
            Table(k, new Vector3(0, 0, 43.2f), 2.4f, 1.1f, 0);
            Sack(k, new Vector3(-3.6f, 0, 42.3f), 0.6f); Sack(k, new Vector3(-3.2f, 0, 43.2f), 0.5f); Sack(k, new Vector3(3.6f, 0, 42.5f), 0.55f); Sack(k, new Vector3(-2.2f, 1.62f, 44.95f), 0.4f);
            foreach (int s in new[] { -1, 1 }) HangingLamp(k, new Vector3(s * 1.6f, 6.8f, 43.2f), 2.6f);
            var acid = new GameObject("Membrana de la reserva"); acid.transform.SetParent(currentRoom, false); acid.transform.position = new Vector3(0, 2.1f, 41.5f);
            var ak = new K { bake = false }; ak.Box(acid.transform.position, new Vector3(9f, 4.2f, 0.18f), Quaternion.identity, Color.white);
            for (int i = 0; i < 14; i++) ak.Sphere(acid.transform.position + new Vector3(Rf(-4f, 4f), Rf(-1.8f, 1.8f), -0.1f), new Vector3(0.25f, 0.25f, 0.08f), new Color(0.8f, 1f, 0.7f), 8);
            var am = ak.Flush("Modelo", acid.transform, limeMat, acid.transform.position); am.transform.localPosition = Vector3.zero;
            acid.AddComponent<BoxCollider>().size = new Vector3(9f, 4.2f, 0.25f); acid.AddComponent<AcidVulnerable>().hitsToDissolve = 2;
            Sign("DESPENSA\nDISUELVE LA MEMBRANA CON ÁCIDO", new Vector3(7.2f, 1.8f, 38.6f), 3.4f, 0);
            Food(new Vector3(-0.6f, 1.0f, 43.2f), 0); Food(new Vector3(0.4f, 1.0f, 43f), 1); Food(new Vector3(0.9f, 1.0f, 43.4f), 2);
            Cluster(new Vector3(-1.5f, 0, 41.9f), 4); Food(new Vector3(2.6f, 0, 43.4f), 3, 4, 1.8f); Food(new Vector3(-2.8f, 0, 44.1f), 5, 4, 1.8f);
            // Resto: comida en plataformas, babosas, caja, recuperación.
            Cluster(new Vector3(-14f, up, 30.2f), 4); Cluster(new Vector3(12.4f, up, 25.0f), 4); Cluster(new Vector3(-9.5f, 0, 19f), 3);
            Cache(new Vector3(8.6f, 0.6f, 19.0f)); Recovery(new Vector3(-12f, 0.02f, 38.5f));
            Slug(new Vector3(-11f, 0.4f, 35.5f), new Vector3(-4.5f, 0.4f, 35.5f)); Slug(new Vector3(5f, 0.4f, 37.5f), new Vector3(12f, 0.4f, 37.5f));
            Crate(k, new Vector3(13.6f, 0.6f, 39.5f), 1.2f, 8); Crate(k, new Vector3(12.3f, 0.45f, 39.8f), 0.9f, -10); Col("Cajas", new Vector3(13f, 0.6f, 39.6f), new Vector3(2.6f, 1.2f, 1.4f));
            Barrel(k, new Vector3(-13.9f, 0, 39.8f)); Col("Barril", new Vector3(-13.9f, 0.55f, 39.8f), new Vector3(0.9f, 1.1f, 0.9f));
        }

        // Slimes de las alcantarillas (Lizards → Enemigos → Preparar slime crea el prefab).
        static void PlaceSlimes()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlimeSetup.PrefabPath);
            if (!prefab) { Debug.LogWarning("[LEVEL2] Falta el prefab del slime: usa Lizards/Enemigos/Preparar slime."); return; }
            var group = new GameObject("Enemigos · Slimes").transform; group.SetParent(sceneRoot, false);
            var spots = new[]
            {
                (new Vector3(-30f, 0f, -9f), 30f),     // 03 · orilla sur del canal
                (new Vector3(-28f, 0f, 18f), 160f),    // 04 · almacén
                (new Vector3(34f, 0f, 2.5f), 220f),    // 05 · taller, junto a la placa
                (new Vector3(12.8f, 2.4f, 31.6f), 200f) // 07 · plataforma de la bomba este
            };
            foreach (var (p, yaw) in spots)
            {
                var s = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                s.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0)); s.name = "Slime";
            }
        }

        static void WaterFlowSheet(K k)
        {
            // Bocas de desagüe bajo las bombas que alimentan el canal.
            foreach (int s in new[] { -1, 1 })
            {
                var p = new Vector3(s * 2.9f, 0.6f, 38.6f);
                Pipe(k, new Vector3(s * 4.2f, 0.6f, 38.6f), p, 0.35f, Mint, 2f);
                Waterfall(currentRoom, p + new Vector3(-s * 0.2f, -0.2f, 0), new Vector3(-s, 0, 0), 0.6f, -0.5f, 0.35f);
            }
        }
    }
}
