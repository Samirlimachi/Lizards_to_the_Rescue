using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Level 2 → Construir alcantarillas.
    /// Nivel de alcantarilla original: llegas en balsa, en la sala central está el bol que hay que llenar de comida.
    /// Tres ramales: canal con enganches y agua para nadar (oeste), almacén con placa de contrapeso (este) y
    /// pasarelas altas con babosa y membrana de ácido (norte). Al llenar el bol se abre la gran tubería de salida.
    /// </summary>
    public static class LevelTwoBuilder
    {
        const string ScenePath = "Assets/Scenes/LEVEL2.unity";
        const string AssetRoot = "Assets/Lizard/LevelTwo";
        static Transform root;
        static Material floor, floorDark, brick, brickLight, trim, pipeGreen, pipeMint, purplePipe, orange, yellow, black, metal, wood, glow, slime, waterMat, fruit, red, board;
        static int serial;

        static GameObject Shape(string name, PrimitiveType shape, Vector3 p, Vector3 s, Material m, Transform parent = null, bool collision = true)
        {
            var g = GameObject.CreatePrimitive(shape); g.name = name;
            g.transform.SetParent(parent ? parent : root, false); g.transform.position = p; g.transform.localScale = s;
            g.GetComponent<Renderer>().sharedMaterial = m;
            if (!collision) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        static GameObject Box(string n, Vector3 p, Vector3 s, Material m, Transform t = null, bool c = true) => Shape(n, PrimitiveType.Cube, p, s, m, t, c);
        static Material Mat(string n, Color c, float spec = 0.2f)
        {
            string path = AssetRoot + "/Materials/" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var fresh = LizardMaterials.Toon(c, 0f, spec);
            if (m) { m.shader = fresh.shader; m.CopyPropertiesFromMaterial(fresh); EditorUtility.SetDirty(m); return m; }
            AssetDatabase.CreateAsset(fresh, path); return fresh;
        }
        static void Tube(string name, Vector3 a, Vector3 b, float r, Material m, bool collision = true)
        {
            var v = b - a; var g = Shape(name, PrimitiveType.Cylinder, (a + b) / 2, new Vector3(r * 2, v.magnitude / 2, r * 2), m, null, collision);
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, v.normalized);
        }
        static GameObject Ring(string name, Vector3 c, float radius, float tube, Material m, Quaternion rot, bool full = true)
        {
            const int sides = 10; int steps = full ? 48 : 24;
            var v = new Vector3[(steps + 1) * sides]; var tri = new int[steps * sides * 6];
            for (int j = 0; j <= steps; j++)
            {
                float a = j / (float)steps * Mathf.PI * (full ? 2 : 1); var o = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int i = 0; i < sides; i++) { float b = i * Mathf.PI * 2 / sides; v[j * sides + i] = o * (radius + tube * Mathf.Cos(b)) + Vector3.forward * tube * Mathf.Sin(b); }
            }
            int k = 0;
            for (int j = 0; j < steps; j++) for (int i = 0; i < sides; i++)
                { int a = j * sides + i, b = j * sides + (i + 1) % sides, cc = (j + 1) * sides + i, d = (j + 1) * sides + (i + 1) % sides; tri[k++] = a; tri[k++] = cc; tri[k++] = b; tri[k++] = b; tri[k++] = cc; tri[k++] = d; }
            var mesh = new Mesh { name = name, vertices = v, triangles = tri }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetRoot + "/Meshes/Ring" + serial++ + ".asset");
            var g = new GameObject(name); g.transform.SetParent(root); g.transform.position = c; g.transform.rotation = rot;
            g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }
        static TextMesh Sign(string text, Vector3 p, float w, float size, Quaternion rot)
        {
            var g = new GameObject("Pizarra"); g.transform.SetParent(root); g.transform.position = p; g.transform.rotation = rot;
            var b = Box("Marco", p, new Vector3(w + 0.3f, size * 26f + 0.5f, 0.15f), red, g.transform, false); b.transform.rotation = rot;
            var b2 = Box("Pizarra", p + rot * Vector3.forward * 0.06f, new Vector3(w, size * 26f, 0.1f), board, g.transform, false); b2.transform.rotation = rot;
            var tg = new GameObject("Texto"); tg.transform.SetParent(g.transform, false); tg.transform.position = p + rot * Vector3.forward * 0.13f; tg.transform.rotation = rot * Quaternion.Euler(0, 180, 0);
            var t = tg.AddComponent<TextMesh>(); t.text = text; t.fontSize = 96; t.characterSize = size * 0.55f; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = new Color(0.95f, 1f, 0.9f);
            Tube("Poste", p + Vector3.down * (size * 13f), new Vector3(p.x, 0, p.z), 0.08f, metal, false);
            return t;
        }
        static GameObject Phys(string n, Vector3 p, Vector3 s, float mass, Material m, bool barrel = false)
        {
            var g = Shape(n, barrel ? PrimitiveType.Cylinder : PrimitiveType.Cube, p, s, m);
            var rb = g.AddComponent<Rigidbody>(); rb.mass = mass; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rb.linearDamping = 0.3f;
            g.AddComponent<TongueTarget>().kind = TongueTargetKind.Object;
            if (!barrel) foreach (float o in new[] { -0.3f, 0.3f }) { var pl = Box("Refuerzo", p + Vector3.up * s.y * o + Vector3.back * (s.z / 2 + 0.02f), new Vector3(s.x * 1.02f, 0.1f, 0.05f), orange, root, false); pl.transform.SetParent(g.transform, true); }
            return g;
        }
        static void Food(Vector3 p, int count, float value = 1f)
        {
            for (int i = 0; i < count; i++)
            {
                var f = FoodFactory.CreateFruit(p + new Vector3((i % 4) * 0.7f, 0.3f, (i / 4) * 0.7f), value, 0.2f, fruit);
                f.name = "Fruta"; f.transform.SetParent(root);
            }
        }
        static void Burger(Vector3 p)
        {
            var b = FoodFactory.CreateBig(p, 6f, 0.75f, orange); b.name = "Hamburguesa gigante · arrástrala al bol"; b.transform.SetParent(root);
            var bun = Shape("Pan", PrimitiveType.Sphere, p + Vector3.up * 0.25f, new Vector3(1.5f, 0.6f, 1.5f), orange, b.transform, false);
            var lettuce = Shape("Lechuga", PrimitiveType.Cylinder, p, new Vector3(1.6f, 0.04f, 1.6f), slime, b.transform, false);
        }
        static void Checkpoint(Vector3 p, Vector3 size)
        {
            var g = new GameObject("Checkpoint"); g.transform.SetParent(root); g.transform.position = p;
            var t = g.AddComponent<BoxCollider>(); t.isTrigger = true; t.size = size; g.AddComponent<Checkpoint>();
        }
        static void Water(string name, Vector3 c, Vector3 size)
        {
            Box(name + " · fondo", c + Vector3.down * (size.y + 0.15f), new Vector3(size.x, 0.3f, size.z), floorDark);
            var w = Box(name, c, new Vector3(size.x, 0.08f, size.z), waterMat, null, false);
            w.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var z = new GameObject(name + " · nado"); z.transform.SetParent(root); z.transform.position = new Vector3(c.x, c.y, c.z);
            var sz = z.AddComponent<SwimZone>(); sz.radius = Mathf.Min(size.x, size.z) * 0.5f; sz.surfaceY = 0f; sz.depth = size.y + 1f;
            // SwimZone es circular: para canales largos se ponen varias zonas a lo largo.
            float len = Mathf.Max(size.x, size.z), wid = Mathf.Min(size.x, size.z);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / wid));
            Object.DestroyImmediate(z);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0f : (i / (float)(n - 1) - 0.5f) * (len - wid);
                Vector3 off = size.x > size.z ? new Vector3(t, 0, 0) : new Vector3(0, 0, t);
                var zz = new GameObject(name + " · nado " + i); zz.transform.SetParent(root); zz.transform.position = c + off;
                var s2 = zz.AddComponent<SwimZone>(); s2.radius = wid * 0.5f; s2.surfaceY = 0f; s2.depth = size.y + 1f;
            }
        }
        // Pared de ladrillo con hileras alternas y zócalo.
        static void BrickWall(string name, Vector3 c, Vector3 size)
        {
            Box(name, c, size, brick);
            bool alongX = size.x > size.z;
            float len = alongX ? size.x : size.z;
            for (float y = 0.6f; y < size.y - 0.3f; y += 1.2f)
                for (float u = -len / 2 + 0.9f + ((int)(y / 1.2f) % 2) * 0.9f; u < len / 2 - 0.5f; u += 3.6f)
                {
                    Vector3 off = alongX ? new Vector3(u, y - size.y / 2, 0) : new Vector3(0, y - size.y / 2, u);
                    Vector3 s = alongX ? new Vector3(1.6f, 0.5f, size.z + 0.06f) : new Vector3(size.x + 0.06f, 0.5f, 1.6f);
                    Box("Ladrillo", c + off, s, brickLight, null, false);
                }
            Box("Zócalo", new Vector3(c.x, 0.2f, c.z), alongX ? new Vector3(size.x, 0.4f, size.z + 0.2f) : new Vector3(size.x + 0.2f, 0.4f, size.z), trim, null, false);
        }

        [MenuItem("Lizards/Level 2/Construir alcantarillas")]
        public static string Build()
        {
            if (Application.isPlaying) return "Sal de Play antes de construir.";
            var active = EditorSceneManager.GetActiveScene(); if (active.isDirty) EditorSceneManager.SaveScene(active);
            Directory.CreateDirectory(AssetRoot + "/Materials"); Directory.CreateDirectory(AssetRoot + "/Meshes"); AssetDatabase.Refresh();
            foreach (var f in Directory.GetFiles(AssetRoot + "/Meshes", "*.asset")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var g in scene.GetRootGameObjects()) Object.DestroyImmediate(g);
            root = new GameObject("LEVEL 2 · Alcantarillas").transform; serial = 0;

            floor = Mat("Suelo", new Color(0.36f, 0.4f, 0.38f)); floorDark = Mat("Suelo oscuro", new Color(0.2f, 0.24f, 0.26f));
            brick = Mat("Ladrillo morado", new Color(0.42f, 0.33f, 0.62f)); brickLight = Mat("Ladrillo claro", new Color(0.5f, 0.41f, 0.71f));
            trim = Mat("Moldura", new Color(0.3f, 0.32f, 0.3f)); pipeGreen = Mat("Tubo verde lima", new Color(0.55f, 0.95f, 0.2f), 0.4f);
            pipeMint = Mat("Tubo menta", new Color(0.35f, 0.82f, 0.62f), 0.4f); purplePipe = Mat("Tubo morado", new Color(0.55f, 0.32f, 0.78f), 0.4f);
            orange = Mat("Naranja", new Color(1f, 0.45f, 0.12f)); yellow = Mat("Amarillo", new Color(1f, 0.78f, 0.15f)); black = Mat("Negro", new Color(0.1f, 0.1f, 0.12f));
            metal = Mat("Metal", new Color(0.3f, 0.34f, 0.38f), 0.5f); wood = Mat("Madera", new Color(0.72f, 0.55f, 0.36f)); glow = Mat("Luz", new Color(1f, 0.95f, 0.7f), 0.6f);
            slime = Mat("Baba", new Color(0.55f, 0.95f, 0.25f), 0.7f); fruit = Mat("Fruta", new Color(1f, 0.3f, 0.35f), 0.5f); red = Mat("Rojo", new Color(0.85f, 0.15f, 0.15f));
            board = Mat("Pizarra", new Color(0.12f, 0.14f, 0.14f));
            waterMat = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/Materials/Agua.mat");
            if (!waterMat) { waterMat = LizardMaterials.Transparent(new Color(0.25f, 0.85f, 0.75f, 0.75f), 0.95f); AssetDatabase.CreateAsset(waterMat, AssetRoot + "/Materials/Agua.mat"); }

            // ---- Ambiente: interior de alcantarilla, luz fresca y niebla lila ----
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.72f, 0.9f); RenderSettings.ambientEquatorColor = new Color(0.5f, 0.45f, 0.62f); RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.35f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(0.36f, 0.3f, 0.5f); RenderSettings.fogStartDistance = 40; RenderSettings.fogEndDistance = 110;
            var sun = new GameObject("Luz principal").AddComponent<Light>(); sun.transform.SetParent(root); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.color = new Color(1f, 0.96f, 0.9f); sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(55, 30, 0);

            // ================= 1. Muelle de llegada (z -40 … -22) =================
            Water("Canal de llegada", new Vector3(0, -0.6f, -34), new Vector3(8, 1.6f, 16));
            Box("Muelle oeste", new Vector3(-7.5f, -0.25f, -32), new Vector3(7, 0.5f, 20), floor);
            Box("Muelle este", new Vector3(7.5f, -0.25f, -32), new Vector3(7, 0.5f, 20), floor);
            var raft = Box("Balsa de llegada", new Vector3(0, -0.4f, -36), new Vector3(4.2f, 0.5f, 3.4f), orange);
            for (int s = -1; s <= 1; s += 2) { Tube("Flotador", new Vector3(s * 2.1f, -0.2f, -37.7f), new Vector3(s * 2.1f, -0.2f, -34.3f), 0.35f, orange, false); }
            Box("Tablón de bajada", new Vector3(-3.6f, -0.1f, -33), new Vector3(2.6f, 0.15f, 1.2f), wood);
            BrickWall("Muro sur", new Vector3(0, 4, -42.5f), new Vector3(24, 8, 1));
            for (int s = -1; s <= 1; s += 2) BrickWall("Muro muelle", new Vector3(s * 11.5f, 4, -32), new Vector3(1, 8, 20));
            Ring("Boca de tubería de llegada", new Vector3(0, 3.5f, -41.8f), 3.6f, 0.7f, pipeGreen, Quaternion.identity);
            Box("Oscuridad del túnel", new Vector3(0, 3.5f, -42.1f), new Vector3(6.8f, 6.8f, 0.1f), black, null, false);
            Box("Rampa al centro", new Vector3(0, 0.0f, -22.5f), new Vector3(8, 0.5f, 3), floor).transform.rotation = Quaternion.Euler(-4, 0, 0);
            Sign("1. Busca comida\n2. Llévala al bol\n3. ¡Sal por la gran tubería!", new Vector3(6, 2.4f, -26), 3.2f, 0.09f, Quaternion.Euler(0, 160, 0));
            Food(new Vector3(-8, 0, -30), 4); Phys("Caja", new Vector3(7, 0.6f, -28), Vector3.one * 1.1f, 3, wood);
            Checkpoint(new Vector3(0, 0.6f, -30), new Vector3(20, 2, 2));

            // ================= 2. Sala central del bol (z -20 … 16) =================
            Box("Suelo central", new Vector3(0, -0.25f, -2), new Vector3(36, 0.5f, 36), floor);
            for (int x = -16; x <= 16; x += 4) for (int z = -18; z <= 14; z += 4)
                    if ((x + z) % 8 == 0) Box("Losa", new Vector3(x, 0.01f, z), new Vector3(3.9f, 0.02f, 3.9f), floorDark, null, false);
            BrickWall("Muro oeste A", new Vector3(-18.5f, 5, -10.25f), new Vector3(1, 10, 19.5f));
            BrickWall("Muro oeste B", new Vector3(-18.5f, 5, 11.25f), new Vector3(1, 10, 9.5f));
            Box("Dintel oeste", new Vector3(-18.5f, 7.5f, 2), new Vector3(1, 5, 6.5f), brick);
            BrickWall("Muro este A", new Vector3(18.5f, 5, -13.25f), new Vector3(1, 10, 13.5f));
            BrickWall("Muro este B", new Vector3(18.5f, 5, 8.25f), new Vector3(1, 10, 15.5f));
            Box("Dintel este", new Vector3(18.5f, 7.5f, -2), new Vector3(1, 5, 6.5f), brick);
            BrickWall("Muro norte A", new Vector3(-11f, 5, 16.5f), new Vector3(14, 10, 1));
            BrickWall("Muro norte B", new Vector3(11f, 5, 16.5f), new Vector3(14, 10, 1));
            BrickWall("Muro sur A", new Vector3(-11.5f, 5, -20.5f), new Vector3(13, 10, 1));
            BrickWall("Muro sur B", new Vector3(11.5f, 5, -20.5f), new Vector3(13, 10, 1));
            // Bol de comida (inflable) en el centro.
            Vector3 bowl = new Vector3(0, 0, -2);
            Ring("Bol · aro azul", bowl + Vector3.up * 0.55f, 3.4f, 0.55f, Mat("Azul bol", new Color(0.18f, 0.4f, 0.85f)), Quaternion.Euler(90, 0, 0)).AddComponent<MeshCollider>();
            Ring("Bol · aro naranja", bowl + Vector3.up * 1.45f, 3.3f, 0.45f, orange, Quaternion.Euler(90, 0, 0)).AddComponent<MeshCollider>();
            Shape("Bol · fondo", PrimitiveType.Cylinder, bowl + Vector3.up * 0.05f, new Vector3(6.6f, 0.05f, 6.6f), floorDark);
            var depot = new GameObject("Bol · entrega aquí"); depot.transform.SetParent(root); depot.transform.position = bowl;
            var dc = depot.AddComponent<BoxCollider>(); dc.isTrigger = true; dc.size = new Vector3(6f, 3f, 6f); dc.center = Vector3.up * 1.5f;
            var goal = depot.AddComponent<FoodGoal>();
            var boardText = Sign("¡LLENA EL BOL!", new Vector3(-6, 3.4f, 6), 4.2f, 0.1f, Quaternion.Euler(0, 180, 0));
            // Tuberías por encima con enganches amarillos y tubos morados en las paredes.
            Tube("Tubo maestro", new Vector3(-18, 8, -12), new Vector3(18, 8, -12), 0.7f, pipeMint);
            Tube("Tubo maestro", new Vector3(-18, 8.5f, 8), new Vector3(18, 8.5f, 8), 0.6f, pipeMint);
            for (int i = -2; i <= 2; i++) Tube("Abrazadera", new Vector3(i * 7, 8, -12.15f), new Vector3(i * 7, 8, -11.85f), 0.85f, metal);
            for (int s = -1; s <= 1; s += 2) for (int k = 0; k < 3; k++) Tube("Conducto de pared", new Vector3(s * 17.6f, 1, -16 + k * 12), new Vector3(s * 17.6f, 9.5f, -16 + k * 12), 0.4f, purplePipe);
            for (int s = -1; s <= 1; s += 2) for (int k = 0; k < 4; k++) { var lamp = Box("Luz de pared", new Vector3(s * 17.9f, 6, -15 + k * 9), new Vector3(0.2f, 0.6f, 1.6f), glow, null, false); }
            // Atrezo: barriles, conos, cajas y un trampolín.
            Phys("Barril amarillo", new Vector3(12, 0.65f, -14), new Vector3(0.9f, 0.6f, 0.9f), 5, yellow, true);
            Phys("Barril amarillo", new Vector3(13.2f, 0.65f, -13), new Vector3(0.9f, 0.6f, 0.9f), 5, yellow, true);
            for (int i = 0; i < 4; i++) Shape("Cono", PrimitiveType.Cylinder, new Vector3(-12 + i * 1.4f, 0.4f, 12), new Vector3(0.45f, 0.4f, 0.45f), orange);
            var tramp = Shape("Trampolín", PrimitiveType.Cylinder, new Vector3(13, 0.3f, 10), new Vector3(2.6f, 0.15f, 2.6f), black);
            Object.DestroyImmediate(tramp.GetComponent<Collider>()); var mc = tramp.AddComponent<MeshCollider>(); mc.convex = true; tramp.AddComponent<SwampBounce>().bounceSpeed = 13f;
            Ring("Trampolín · borde", new Vector3(13, 0.38f, 10), 1.3f, 0.15f, red, Quaternion.Euler(90, 0, 0));
            Food(new Vector3(8, 0, 4), 6); Food(new Vector3(-10, 0, -10), 4);
            var spawner = new GameObject("Cesta de fruta renovable"); spawner.transform.SetParent(root); spawner.transform.position = new Vector3(-14, 0.2f, 4);
            var fs = spawner.AddComponent<FoodSpawner>(); fs.respawnDelay = 3; fs.radius = 0.2f;
            Shape("Cesta", PrimitiveType.Cylinder, new Vector3(-14, 0.15f, 4), new Vector3(1.4f, 0.15f, 1.4f), wood);
            Checkpoint(new Vector3(0, 0.6f, -16), new Vector3(30, 2, 2));

            // ================= 3. Ramal oeste: canal con enganches y nado (x -18 … -50) =================
            Box("Pasillo oeste · suelo", new Vector3(-27, -0.25f, 2), new Vector3(18, 0.5f, 8), floor);
            BrickWall("Pasillo oeste · muro N", new Vector3(-34, 4, 6.5f), new Vector3(32, 8, 1));
            BrickWall("Pasillo oeste · muro S", new Vector3(-34, 4, -2.5f), new Vector3(32, 8, 1));
            Water("Canal oeste", new Vector3(-41, -0.7f, 2), new Vector3(10, 1.8f, 8));
            for (int i = 0; i < 3; i++)
            {
                var ring = Ring("Enganche amarillo", new Vector3(-38 + i * 3.2f, 5.2f, 2), 0.45f, 0.09f, yellow, Quaternion.identity);
                ring.AddComponent<SphereCollider>().radius = 0.56f; ring.AddComponent<TongueTarget>().kind = TongueTargetKind.GrapplePoint;
                Tube("Cadena", new Vector3(-38 + i * 3.2f, 5.6f, 2), new Vector3(-38 + i * 3.2f, 8, 2), 0.05f, metal, false);
            }
            foreach (float x in new[] { -39f, -43f }) Shape("Nenúfar de paso", PrimitiveType.Cylinder, new Vector3(x, -0.6f, 2), new Vector3(1.6f, 0.08f, 1.6f), slime);
            Box("Plataforma oeste", new Vector3(-49, -0.25f, 2), new Vector3(6, 0.5f, 8), floor);
            BrickWall("Fondo oeste", new Vector3(-52.5f, 4, 2), new Vector3(1, 8, 9));
            Food(new Vector3(-50.5f, 0, -0.5f), 8);
            Recovery(new Vector3(-48, 0.15f, 4.5f));
            Sign("CANAL\nNada o engánchate a los aros\ncon la lengua (clic izq.)", new Vector3(-30, 2.6f, 5.6f), 3.6f, 0.08f, Quaternion.Euler(0, 180, 0));

            // ================= 4. Ramal este: almacén con contrapeso (x 18 … 44) =================
            Box("Almacén · suelo", new Vector3(31, -0.25f, -2), new Vector3(26, 0.5f, 16), floor);
            BrickWall("Almacén · muro N", new Vector3(31, 4, 6.5f), new Vector3(26, 8, 1));
            BrickWall("Almacén · muro S", new Vector3(31, 4, -10.5f), new Vector3(26, 8, 1));
            BrickWall("Almacén · fondo", new Vector3(44.5f, 4, -2), new Vector3(1, 8, 16));
            for (int i = 0; i < 6; i++) Phys("Caja de suministros", new Vector3(22 + (i % 3) * 1.4f, 0.65f, -8 + (i / 3) * 1.4f), Vector3.one * 1.2f, 4, wood);
            var weight = Phys("Contrapeso naranja · 20 kg", new Vector3(24, 0.75f, 3), Vector3.one * 1.4f, 20, orange);
            var plateRoot = new GameObject("Placa de presión"); plateRoot.transform.SetParent(root); plateRoot.transform.position = new Vector3(30, 0, 3);
            var pc = plateRoot.AddComponent<BoxCollider>(); pc.isTrigger = true; pc.size = new Vector3(3, 1.4f, 3); pc.center = Vector3.up * 0.7f;
            var pv = Box("Placa azul", new Vector3(30, 0.08f, 3), new Vector3(3, 0.16f, 3), Mat("Azul placa", new Color(0.2f, 0.55f, 1f)));
            Box("Marco compuerta", new Vector3(36, 3.5f, -2), new Vector3(0.8f, 7, 1.2f), metal);
            for (int s = -1; s <= 1; s += 2) Box("Pared compuerta", new Vector3(36, 3.5f, -2 + s * 5.25f), new Vector3(0.8f, 7, 5.5f), brick);
            var gate = Box("Compuerta del almacén", new Vector3(36, 2, -2), new Vector3(0.6f, 4, 5), yellow); var door = gate.AddComponent<Door>(); door.openOffset = new Vector3(0, 4.5f, 0);
            for (int i = 0; i < 5; i++) Box("Franja", new Vector3(35.65f, 0.6f + i * 0.8f, -2), new Vector3(0.05f, 0.35f, 5), black, gate.transform, false);
            var plate = plateRoot.AddComponent<PressurePlate>(); plate.requiredMass = 12; plate.doors = new[] { door }; plate.plateVisual = pv.transform;
            Burger(new Vector3(41, 1f, -2));
            Food(new Vector3(39, 0, -7), 8, 1);
            Sign("ALMACÉN\nPon el contrapeso naranja\nsobre la placa azul", new Vector3(26, 2.6f, 5.6f), 3.6f, 0.08f, Quaternion.Euler(0, 180, 0));

            // ================= 5. Ramal norte: pasarelas altas, babosa y ácido (z 16 … 46) =================
            Box("Galería norte · suelo", new Vector3(0, -0.25f, 31), new Vector3(10, 0.5f, 30), floor);
            for (int s = -1; s <= 1; s += 2)
            {
                BrickWall("Galería · muro", new Vector3(s * 9.5f, 5, 31), new Vector3(1, 10, 30));
                Box("Pasarela alta", new Vector3(s * 6.5f, 3, 31), new Vector3(5, 0.4f, 28), metal);
                for (int k = 0; k < 6; k++) Box("Barandilla", new Vector3(s * 4.1f, 3.6f, 19 + k * 5), new Vector3(0.12f, 0.9f, 0.12f), yellow, null, false);
                Tube("Pasamanos", new Vector3(s * 4.1f, 4.05f, 18), new Vector3(s * 4.1f, 4.05f, 44), 0.06f, yellow, false);
            }
            var ramp = Box("Rampa a la pasarela", new Vector3(-6.5f, 1.4f, 14.5f), new Vector3(5, 0.4f, 7), metal); ramp.transform.rotation = Quaternion.Euler(-24, 0, 0);
            Box("Paso galería", new Vector3(0, -0.25f, 16.5f), new Vector3(8, 0.5f, 1.6f), floor);
            var enemy = Shape("Babosa de las tuberías", PrimitiveType.Sphere, new Vector3(0, 0.45f, 28), new Vector3(1.4f, 0.8f, 1.8f), slime);
            enemy.AddComponent<Rigidbody>().isKinematic = true; var patrol = enemy.AddComponent<PatrolEnemy>(); patrol.pointA = new Vector3(0, 0.45f, 22); patrol.pointB = new Vector3(0, 0.45f, 36); patrol.speed = 1.5f;
            enemy.AddComponent<DamageSource>(); enemy.AddComponent<EnemyHealth>();
            foreach (float x in new[] { -0.35f, 0.35f }) { var eye = Shape("Ojo", PrimitiveType.Sphere, new Vector3(x, 0.85f, 27.4f), Vector3.one * 0.3f, glow, null, false); eye.transform.SetParent(enemy.transform, true); }
            Food(new Vector3(5.4f, 3.3f, 24), 6); Food(new Vector3(-7.6f, 3.3f, 36), 6);
            var acid = Box("Membrana de ácido", new Vector3(0, 1.6f, 40), new Vector3(8, 3.2f, 0.25f), Mat("Membrana", new Color(0.6f, 1f, 0.5f), 0.6f)); acid.AddComponent<AcidVulnerable>().hitsToDissolve = 2;
            Food(new Vector3(-1, 0, 42), 6, 2);
            Checkpoint(new Vector3(0, 0.6f, 18), new Vector3(8, 2, 1.5f));
            Recovery(new Vector3(3, 0.15f, 20));

            // ================= 6. Salida: gran tubería verde (norte, tras el bol) =================
            BrickWall("Muro final", new Vector3(0, 5, 46.5f), new Vector3(20, 10, 1));
            Ring("Gran tubería de salida", new Vector3(0, 3.5f, 45.6f), 3.2f, 0.9f, pipeGreen, Quaternion.identity);
            Box("Fondo de la tubería", new Vector3(0, 3.5f, 46f), new Vector3(6f, 6f, 0.1f), black, null, false);
            var exit = Box("Rejilla de salida", new Vector3(0, 2.2f, 44.8f), new Vector3(5.6f, 4.4f, 0.3f), metal).AddComponent<Door>(); exit.openOffset = new Vector3(0, 5f, 0);
            var missionRoot = new GameObject("Salida del nivel"); missionRoot.transform.SetParent(root); missionRoot.transform.position = new Vector3(0, 0, 45.6f);
            var ec = missionRoot.AddComponent<BoxCollider>(); ec.isTrigger = true; ec.size = new Vector3(5, 4, 1.5f); ec.center = Vector3.up * 2f;
            var mission = missionRoot.AddComponent<LevelOneMission>(); mission.depot = goal; mission.exitDoor = exit; mission.board = boardText; mission.requiredFood = 30; mission.coinReward = 250; mission.levelName = "Alcantarillas";

            // ---- Jugador, cámara y HUD ----
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Prefabs/Sunny.prefab") ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Prefabs/Lagarto.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab); player.name = "Jugador · Sunny"; player.transform.position = new Vector3(0, 0.4f, -36);
            player.GetComponent<LizardInput>().controlled = true; var pp = player.GetComponent<LizardPlayer>(); pp.displayName = "Sunny";
            var camGo = new GameObject("Cámara de Sunny"); camGo.tag = "MainCamera"; var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 160;
            cam.backgroundColor = RenderSettings.fogColor; cam.clearFlags = CameraClearFlags.SolidColor; camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<LizardCameraRig>(); rig.target = pp; camGo.transform.position = new Vector3(0, 3, -41);
            var hud = new GameObject("HUD").AddComponent<LizardHUD>(); hud.rig = rig; hud.showDebug = false;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            var build = EditorBuildSettings.scenes.ToList(); if (!build.Any(s => s.path == ScenePath)) { build.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = build.ToArray(); }
            string msg = "[LEVEL2] Alcantarillas construidas: llena el bol con 30 de comida y sal por la gran tubería.";
            Debug.Log(msg); return msg;
        }

        static void Recovery(Vector3 p)
        {
            var g = Shape("Fuente de recuperación", PrimitiveType.Cylinder, p, new Vector3(1.5f, 0.12f, 1.5f), pipeMint);
            var t = g.AddComponent<SphereCollider>(); t.isTrigger = true; t.radius = 0.8f; g.AddComponent<RescuePoint>();
        }
    }
}
