using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Genera los assets (configuración, materiales, prefab del lagarto) y la escena de prueba.
    /// Menú: Lizards → Construir escena de prueba.
    /// </summary>
    public static class LizardSceneBuilder
    {
        const string Root = "Assets/Lizard";
        const string MatDir = Root + "/Materials";
        const string PrefabPath = Root + "/Prefabs/Lagarto.prefab";
        const string ScenePath = Root + "/Scenes/LizardTestZone.unity";
        const string ConfigPath = Root + "/LizardConfig.asset";

        static Transform level;

        [MenuItem("Lizards/Construir escena de prueba", priority = 0)]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            foreach (var d in new[] { Root, MatDir, Root + "/Prefabs", Root + "/Scenes" })
                if (!AssetDatabase.IsValidFolder(d)) { var parent = Path.GetDirectoryName(d).Replace('\\', '/'); AssetDatabase.CreateFolder(parent, Path.GetFileName(d)); }

            var cfg = AssetDatabase.LoadAssetAtPath<LizardConfig>(ConfigPath);
            if (!cfg) { cfg = ScriptableObject.CreateInstance<LizardConfig>(); AssetDatabase.CreateAsset(cfg, ConfigPath); }

            var mats = new LizardModelMaterials
            {
                skin = Mat("Lagarto_Piel", new Color(0.36f, 0.74f, 0.2f), 0.45f),
                belly = Mat("Lagarto_Vientre", new Color(0.83f, 0.91f, 0.45f), 0.4f),
                eyeWhite = Mat("Lagarto_OjoBlanco", Color.white, 0.8f),
                pupil = Mat("Lagarto_Pupila", new Color(0.05f, 0.05f, 0.07f), 0.9f),
                lid = Mat("Lagarto_Parpado", new Color(1f, 0.5f, 0.68f), 0.4f),
                mouth = Mat("Lagarto_Boca", new Color(0.35f, 0.08f, 0.12f), 0.3f),
                tongue = Mat("Lagarto_Lengua", new Color(1f, 0.45f, 0.62f), 0.6f),
                tailTip = Mat("Lagarto_PuntaCola", new Color(0.98f, 0.52f, 0.1f), 0.45f),
                spots = Mat("Lagarto_Manchas", new Color(0.27f, 0.6f, 0.15f), 0.4f),
                bubble = Mat("Lagarto_Burbuja", new Color(0.7f, 0.95f, 1f, 0.28f), 0.95f, true),
            };

            var phys = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MatDir + "/Lagarto_SinFriccion.physicMaterial");
            if (!phys)
            {
                phys = new PhysicsMaterial("Lagarto_SinFriccion")
                {
                    dynamicFriction = 0f, staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum,
                };
                AssetDatabase.CreateAsset(phys, MatDir + "/Lagarto_SinFriccion.physicMaterial");
            }

            // Prefab del lagarto
            var temp = LizardFactory.Create("Lagarto", Vector3.zero, cfg, mats, phys);
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp.gameObject, PrefabPath);
            Object.DestroyImmediate(temp.gameObject);

            // Escena
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.75f, 0.85f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.65f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.3f);

            var sun = new GameObject("Sol").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.2f; sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            level = new GameObject("Nivel de prueba").transform;
            BuildLevel();

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "Jugador";
            player.transform.position = new Vector3(0f, 0.1f, 2f);
            player.GetComponent<LizardPlayer>().displayName = "Sunny";

            var partner = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            partner.name = "Compañero (renacuajo)";
            partner.transform.position = new Vector3(4f, 0.1f, 186f);
            partner.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var pp = partner.GetComponent<LizardPlayer>();
            pp.displayName = "Compañero"; pp.startForm = LizardForm.Small; pp.dummyCallsHelp = true;
            partner.GetComponent<LizardInput>().controlled = false;

            var camGo = new GameObject("Cámara");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.82f, 0.98f);
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
            var rig = camGo.AddComponent<LizardCameraRig>();
            rig.target = player.GetComponent<LizardPlayer>();
            camGo.transform.position = new Vector3(0f, 3f, -4f);

            var sys = new GameObject("Sistemas (HUD y depuración)");
            sys.AddComponent<LizardHUD>().rig = rig;
            sys.AddComponent<LizardDebugTools>().rig = rig;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Escena de prueba generada en " + ScenePath);
        }

        [MenuItem("Lizards/Ejecutar autoprueba (en Play)", priority = 20)]
        public static void RunSelfTest()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[Lizards] Entra en Play antes de ejecutar la autoprueba."); return; }
            LizardSelfTest.Run(AssetDatabase.LoadAssetAtPath<LizardConfig>(ConfigPath));
        }

        // ------------------------------------------------------------------ Nivel

        static void BuildLevel()
        {
            var floor = Mat("Nivel_Suelo", new Color(0.55f, 0.72f, 0.45f), 0.1f);
            var wall = Mat("Nivel_Pared", new Color(0.82f, 0.78f, 0.7f), 0.1f);
            var plat = Mat("Nivel_Plataforma", new Color(0.55f, 0.6f, 0.85f), 0.2f);
            var grapple = Mat("Nivel_Enganche", new Color(1f, 0.85f, 0.15f), 0.6f);
            var sticky = Mat("Nivel_Adhesivo", new Color(0.7f, 0.4f, 0.9f), 0.5f);
            var crate = Mat("Nivel_Caja", new Color(0.75f, 0.55f, 0.32f), 0.2f);
            var heavy = Mat("Nivel_Pesado", new Color(0.45f, 0.45f, 0.5f), 0.3f);
            var hazard = Mat("Nivel_Peligro", new Color(0.95f, 0.25f, 0.2f), 0.3f);
            var lava = Mat("Nivel_Lava", new Color(1f, 0.45f, 0.05f), 0.6f);
            var enemyM = Mat("Nivel_Enemigo", new Color(0.55f, 0.25f, 0.75f), 0.5f);
            var breakM = Mat("Nivel_Rompible", new Color(0.65f, 0.42f, 0.25f), 0.1f);
            var vuln = Mat("Nivel_VulnerableAcido", new Color(0.75f, 0.7f, 0.55f), 0.2f);
            var rescue = Mat("Nivel_Rescate", new Color(0.3f, 0.85f, 1f, 0.5f), 0.9f, true);
            var goal = Mat("Nivel_Meta", new Color(1f, 0.85f, 0.2f), 0.5f);
            var fruit = Mat("Comida_Fruta", new Color(0.95f, 0.3f, 0.35f), 0.5f);
            var melon = Mat("Comida_Sandia", new Color(0.3f, 0.7f, 0.3f), 0.3f);

            // Suelo principal con hueco del foso (z 32–52).
            Box("Suelo A", new Vector3(0f, -0.5f, 13f), new Vector3(26f, 1f, 38f), floor);
            Box("Suelo B", new Vector3(0f, -0.5f, 152f), new Vector3(26f, 1f, 200f), floor);
            Box("Fondo del foso", new Vector3(0f, -5.5f, 42f), new Vector3(26f, 1f, 20f), floor);
            Box("Rampa de salida del foso", new Vector3(10.5f, -2.6f, 37.5f), new Vector3(4f, 0.4f, 11.5f), floor, new Vector3(27f, 0f, 0f));
            Box("Pared final", new Vector3(0f, 3f, 252f), new Vector3(26f, 6f, 1f), wall);

            // ---- 1. Movimiento ----
            Sign("1. MOVIMIENTO\nWASD + ratón · Espacio salta\nEscalones, rampas y plataformas", new Vector3(0f, 3.5f, 6f));
            Checkpoint(new Vector3(0f, 0f, 1f));
            for (int i = 0; i < 4; i++)
            {
                float h = 0.3f * (i + 1) + (i == 3 ? 0.6f : 0f);
                Box("Escalón " + (i + 1), new Vector3(-8f, h / 2f, 9f + i * 2f), new Vector3(3f, h, 2f), plat);
            }
            Box("Rampa suave (25°)", new Vector3(0f, 1.6f, 15f), new Vector3(4f, 0.4f, 8f), plat, new Vector3(-25f, 0f, 0f));
            Box("Rampa empinada (60°)", new Vector3(7f, 2f, 15f), new Vector3(4f, 0.4f, 5f), wall, new Vector3(-60f, 0f, 0f));
            Box("Plataforma 1", new Vector3(-4f, 0.6f, 23f), new Vector3(3f, 1.2f, 3f), plat);
            Box("Plataforma 2 (salta el hueco)", new Vector3(-4f, 1.4f, 27.5f), new Vector3(3f, 0.4f, 2.5f), plat);

            // ---- 2. Lengua ----
            Sign("2. LENGUA\nApunta a los puntos amarillos y MANTÉN clic izq.\nR/F o rueda: acortar/alargar · suelta para salir disparado", new Vector3(0f, 4f, 30.5f));
            Checkpoint(new Vector3(0f, 0f, 29f));
            for (int i = 0; i < 3; i++)
            {
                var g = Sphere("Punto de enganche " + (i + 1), new Vector3(i == 1 ? 1.5f : -1.5f, 8f + (i == 1 ? 1f : 0f), 36f + i * 6f), 0.9f, grapple);
                g.AddComponent<TongueTarget>().kind = TongueTargetKind.GrapplePoint;
            }
            var stickyWall = Box("Pared adhesiva", new Vector3(-12.5f, 4f, 42f), new Vector3(1f, 10f, 20f), sticky);
            stickyWall.AddComponent<TongueTarget>().kind = TongueTargetKind.Surface;
            Checkpoint(new Vector3(0f, 0f, 55f));

            // ---- 3. Objetos ligeros y pesados ----
            Sign("3. ARRASTRAR\nLigeros: vienen a ti · Pesados: arrástralos (o entre dos)\nMuy pesados: te atraen a ti", new Vector3(0f, 4f, 64f));
            for (int i = 0; i < 3; i++) PhysBox("Caja ligera " + (i + 1), new Vector3(-7f + i * 1.5f, 0.4f, 70f), Vector3.one * 0.7f, 2f, crate);
            PhysBox("Caja mediana (20 kg)", new Vector3(1f, 0.6f, 72f), Vector3.one * 1.2f, 20f, crate);
            var coop = PhysBox("Caja cooperativa (70 kg)", new Vector3(7f, 0.9f, 72f), new Vector3(2f, 1.8f, 2f), 70f, heavy);
            coop.GetComponent<Collider>().sharedMaterial = Friction(0.7f);
            var anchorBall = Sphere("Bola muy pesada (400 kg)", new Vector3(0f, 1.2f, 84f), 2.4f, heavy);
            var abr = anchorBall.AddComponent<Rigidbody>(); abr.mass = 400f; abr.angularDamping = 2f;
            anchorBall.AddComponent<TongueTarget>().kind = TongueTargetKind.Object;
            Box("Repisa alta", new Vector3(-8f, 1.5f, 80f), new Vector3(4f, 3f, 4f), plat);
            PhysBox("Caja en la repisa", new Vector3(-8f, 3.4f, 80f), Vector3.one * 0.7f, 2f, crate);

            // ---- 4. Comida ----
            Sign("4. COMIDA\nE: comer · G: expulsar · la lengua acerca comida\nLa sandía es grande: arrástrala a la meta dorada", new Vector3(0f, 4f, 90f));
            Checkpoint(new Vector3(0f, 0f, 89f));
            for (int i = 0; i < 6; i++) Fruit(new Vector3(-5f + i * 2f, 0.3f, 95f), fruit);
            var big = FoodFactory.CreateBig(new Vector3(0f, 0.8f, 103f), 4f, 0.7f, melon);
            big.transform.SetParent(level, true);
            var goalGo = Cylinder("Meta de entrega", new Vector3(8f, 0.05f, 106f), new Vector3(3f, 0.05f, 3f), goal);
            var gt = goalGo.AddComponent<BoxCollider>(); gt.isTrigger = true; gt.size = new Vector3(1f, 30f, 1f); gt.center = new Vector3(0f, 15f, 0f);
            goalGo.AddComponent<FoodGoal>();
            Spawner(new Vector3(-8f, 0f, 104f), "Generador de comida");

            // ---- 5. Gigante ----
            Sign("5. GIGANTE\nCome hasta llenar la barra · rueda, empuja, rompe\nPlaca azul: necesita mucho peso", new Vector3(0f, 4f, 113f));
            Checkpoint(new Vector3(0f, 0f, 112f));
            for (int i = 0; i < 10; i++) Fruit(new Vector3(-6f + (i % 5) * 3f, 0.3f, 116f + (i / 5) * 2f), fruit);
            Box("Paso estrecho (izq.)", new Vector3(-9.6f, 1.5f, 124f), new Vector3(3f, 3f, 4f), wall);
            Box("Paso estrecho (der.)", new Vector3(-4.4f, 1.5f, 124f), new Vector3(3f, 3f, 4f), wall);
            Sign("Paso estrecho:\nel gigante no cabe", new Vector3(-7f, 3.6f, 121.8f), 0.6f);
            var brk = Box("Muro rompible (solo gigante rodando)", new Vector3(4f, 1.5f, 130f), new Vector3(6f, 3f, 0.6f), breakM);
            brk.AddComponent<Breakable>();
            Fruit(new Vector3(4f, 0.3f, 133f), fruit);

            var plateGo = new GameObject("Placa de peso (40 kg)");
            plateGo.transform.SetParent(level); plateGo.transform.position = new Vector3(-6f, 0f, 138f);
            var trig = plateGo.AddComponent<BoxCollider>(); trig.isTrigger = true; trig.size = new Vector3(3f, 2f, 3f); trig.center = Vector3.up;
            var plateVis = Box("Placa", new Vector3(-6f, 0.08f, 138f), new Vector3(3f, 0.16f, 3f), plat);
            Object.DestroyImmediate(plateVis.GetComponent<Collider>());
            plateVis.transform.SetParent(plateGo.transform, true);
            var door = Box("Puerta (la abre la placa)", new Vector3(-6f, 2f, 144f), new Vector3(5f, 4f, 0.6f), wall);
            var drb = door.AddComponent<Rigidbody>(); drb.isKinematic = true;
            var plate = plateGo.AddComponent<PressurePlate>();
            plate.requiredMass = 40f; plate.plateVisual = plateVis.transform; plate.doors = new[] { door.AddComponent<Door>() };
            Box("Sala tras la puerta (izq.)", new Vector3(-8.75f, 2f, 147f), new Vector3(0.5f, 4f, 6f), wall);
            Box("Sala tras la puerta (der.)", new Vector3(-3.25f, 2f, 147f), new Vector3(0.5f, 4f, 6f), wall);
            Box("Sala tras la puerta (fondo)", new Vector3(-6f, 2f, 150f), new Vector3(6f, 4f, 0.5f), wall);
            for (int i = 0; i < 3; i++) Fruit(new Vector3(-7f + i, 0.3f, 147.5f), fruit);
            var boulder = Sphere("Roca (80 kg, empújala con el gigante)", new Vector3(4f, 0.9f, 140f), 1.8f, heavy);
            var brb = boulder.AddComponent<Rigidbody>(); brb.mass = 80f; brb.angularDamping = 1f;
            boulder.GetComponent<Collider>().sharedMaterial = Friction(0.6f);

            // ---- 6. Peligros ----
            Sign("6. PELIGROS\nCon comida: la pierdes · vacío: renacuajo", new Vector3(0f, 4f, 153f));
            Checkpoint(new Vector3(0f, 0f, 152f));
            for (int i = 0; i < 4; i++)
            {
                var spike = Box("Pinchos " + (i + 1), new Vector3(-8f + i * 1.2f, 0.35f, 158f), new Vector3(0.6f, 0.6f, 0.6f), hazard, new Vector3(45f, 0f, 45f));
                spike.AddComponent<DamageSource>().type = DamageType.Trap;
            }
            var lavaGo = Box("Superficie peligrosa", new Vector3(3f, 0.03f, 160f), new Vector3(8f, 0.06f, 3f), lava);
            lavaGo.AddComponent<DamageSource>().type = DamageType.Hazard;
            var enemy = Capsule("Enemigo patrulla", new Vector3(-8f, 0.8f, 168f), enemyM);
            enemy.AddComponent<DamageSource>().type = DamageType.Enemy;
            enemy.AddComponent<EnemyHealth>().health = 2f;
            var patrol = enemy.AddComponent<PatrolEnemy>();
            patrol.pointA = new Vector3(-8f, 0.8f, 168f); patrol.pointB = new Vector3(8f, 0.8f, 168f);
            for (int i = 0; i < 4; i++) Fruit(new Vector3(-3f + i * 2f, 0.3f, 172f), fruit);

            // ---- 7. Rescate ----
            Sign("7. RESCATE\nTransporta al renacuajo con la lengua y aliméntalo (G)\nSolo: anillo azul o comida · F12 cambia de personaje", new Vector3(0f, 4f, 178f));
            Checkpoint(new Vector3(0f, 0f, 177f));
            var rp = Cylinder("Punto de rescate", new Vector3(-7f, 0.6f, 186f), new Vector3(2.5f, 0.6f, 2.5f), rescue);
            rp.GetComponent<Collider>().isTrigger = true;
            rp.AddComponent<RescuePoint>();
            Spawner(new Vector3(7f, 0f, 192f), "Generador de comida (rescate)");

            // ---- 8. Ácido ----
            Sign("8. ÁCIDO\nClic derecho: escupir · disuelve el muro pálido\ny daña al enemigo morado", new Vector3(0f, 4f, 200f));
            Checkpoint(new Vector3(0f, 0f, 199f));
            Box("Pedestal", new Vector3(5f, 0.75f, 212f), new Vector3(2f, 1.5f, 2f), wall);
            var target = Sphere("Enemigo objetivo", new Vector3(5f, 2.2f, 212f), 1.4f, enemyM);
            target.AddComponent<EnemyHealth>().health = 3f;
            Box("Pasillo (izq.)", new Vector3(-9.5f, 1.5f, 212f), new Vector3(1f, 3f, 8f), wall);
            Box("Pasillo (der.)", new Vector3(-3.5f, 1.5f, 212f), new Vector3(1f, 3f, 8f), wall);
            var vw = Box("Muro vulnerable al ácido", new Vector3(-6.5f, 1.5f, 209f), new Vector3(5f, 3f, 0.5f), vuln);
            vw.AddComponent<AcidVulnerable>().hitsToDissolve = 2;
            Fruit(new Vector3(-6.5f, 0.3f, 213f), fruit);

            // ---- 9. Techos bajos ----
            Sign("9. TECHO BAJO\nCome dentro del túnel: no crecerás hasta salir", new Vector3(0f, 4f, 222f));
            Checkpoint(new Vector3(0f, 0f, 221f));
            Box("Túnel (techo)", new Vector3(0f, 1.9f, 233f), new Vector3(8f, 0.6f, 14f), wall);
            Box("Túnel (pared izq.)", new Vector3(-4.25f, 0.8f, 233f), new Vector3(0.5f, 1.6f, 14f), wall);
            Box("Túnel (pared der.)", new Vector3(4.25f, 0.8f, 233f), new Vector3(0.5f, 1.6f, 14f), wall);
            for (int i = 0; i < 12; i++) Fruit(new Vector3(-2.5f + (i % 4) * 1.6f, 0.3f, 228f + (i / 4) * 3f), fruit);
            Box("Esquina estrecha A", new Vector3(9f, 1.5f, 244f), new Vector3(0.5f, 3f, 6f), wall);
            Box("Esquina estrecha B", new Vector3(11.5f, 1.5f, 241.25f), new Vector3(5f, 3f, 0.5f), wall);
            Sign("Prueba a crecer junto a las paredes", new Vector3(10f, 3.6f, 241f), 0.6f);
        }

        // ------------------------------------------------------------------ Utilidades

        static Material Mat(string name, Color c, float smooth, bool transparent = false)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            m = transparent ? LizardMaterials.Transparent(c, smooth) : LizardMaterials.Lit(c, smooth);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static PhysicsMaterial Friction(float f)
        {
            string path = $"{MatDir}/Friccion_{f:0.0}.physicMaterial".Replace(',', '.');
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (m) return m;
            m = new PhysicsMaterial("Friccion") { dynamicFriction = f, staticFriction = f };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static GameObject Prim(PrimitiveType t, string name, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(level);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static GameObject Box(string n, Vector3 p, Vector3 s, Material m, Vector3 e = default) => Prim(PrimitiveType.Cube, n, p, s, m, e);
        static GameObject Sphere(string n, Vector3 p, float d, Material m) => Prim(PrimitiveType.Sphere, n, p, Vector3.one * d, m);
        static GameObject Cylinder(string n, Vector3 p, Vector3 s, Material m) => Prim(PrimitiveType.Cylinder, n, p, s, m);
        static GameObject Capsule(string n, Vector3 p, Material m) => Prim(PrimitiveType.Capsule, n, p, new Vector3(1f, 0.8f, 1f), m);

        static GameObject PhysBox(string n, Vector3 p, Vector3 s, float mass, Material m)
        {
            var go = Box(n, p, s, m);
            var rb = go.AddComponent<Rigidbody>(); rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<TongueTarget>().kind = TongueTargetKind.Object;
            return go;
        }

        static void Fruit(Vector3 p, Material m)
        {
            var f = FoodFactory.CreateFruit(p, 1f, 0.18f, m);
            f.transform.SetParent(level, true);
        }

        static void Spawner(Vector3 p, string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(level); go.transform.position = p;
            go.AddComponent<FoodSpawner>();
            var basePad = Cylinder(n + " (base)", p + Vector3.up * 0.05f, new Vector3(1f, 0.05f, 1f), Mat("Nivel_Generador", new Color(0.95f, 0.6f, 0.65f), 0.4f));
            basePad.transform.SetParent(go.transform, true);
        }

        static void Checkpoint(Vector3 p)
        {
            var go = new GameObject("Punto de control");
            go.transform.SetParent(level); go.transform.position = p;
            var c = go.AddComponent<BoxCollider>(); c.isTrigger = true; c.size = new Vector3(26f, 6f, 1.5f); c.center = Vector3.up * 3f;
            go.AddComponent<Checkpoint>();
        }

        static void Sign(string text, Vector3 p, float size = 1f)
        {
            var go = new GameObject("Cartel");
            go.transform.SetParent(level); go.transform.position = p;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            tm.fontSize = 64; tm.characterSize = 0.06f * size;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.15f, 0.2f, 0.15f);
        }
    }
}
