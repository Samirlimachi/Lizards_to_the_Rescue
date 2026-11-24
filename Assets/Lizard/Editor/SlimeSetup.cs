using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Enemigos → Preparar slime.
    /// Configura Slime.fbx (rig Generic, 7 clips con nombre, bucles y eventos), crea el material cartoon,
    /// el Animator Controller y el prefab Assets/Lizard/Prefabs/Slime.prefab con IA y ragdoll.
    /// </summary>
    public static class SlimeSetup
    {
        public const string Dir = "Assets/Lizard/Art/Slime";
        public const string Fbx = Dir + "/Slime.fbx";
        public const string PrefabPath = "Assets/Lizard/Prefabs/Slime.prefab";
        /// <summary>Tamaño en el juego: el modelo mide 0,75 m; a 1,4 queda un poco más alto que Sunny.</summary>
        public const float Scale = 1.4f;

        // nombre, último fotograma, bucle, eventos (fotograma, id)
        static readonly (string name, int len, bool loop, (int f, string id)[] ev)[] Clips =
        {
            ("Idle", 60, true, new (int, string)[0]),
            ("HopMove", 24, true, new[] { (SlimeEnemy.HopTakeoff, "takeoff"), (SlimeEnemy.HopLand, "land") }),
            ("Alert", 12, false, new[] { (8, "upright") }),
            ("AttackLeap", 54, false, new[] { (SlimeEnemy.LeapTakeoff, "takeoff"), (SlimeEnemy.LeapImpact, "impact"), (SlimeEnemy.LeapVulnerableEnd, "vulnerable_end") }),
            ("AttackSplash", 30, false, new[] { (SlimeEnemy.SplashExpel, "expel") }),
            ("HitReact", 9, false, new[] { (0, "hit") }),
            ("Death", 27, false, new[] { (27, "puddle") }),
        };

        [MenuItem("Lizards/Enemigos/Preparar slime")]
        public static string Setup()
        {
            ConfigureImporter();
            var mat = MakeMaterial();
            var ctrl = MakeController();
            var prefab = MakePrefab(mat, ctrl);
            AssetDatabase.SaveAssets();
            return "Slime listo: " + AssetDatabase.GetAssetPath(prefab);
        }

        static void ConfigureImporter()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true; imp.importBlendShapes = false; imp.importCameras = false; imp.importLights = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            // SlimeRagdoll copia la malla al morir y quita los triángulos que unen tapa y cuerpo:
            // sin Read/Write no puede leerlos en Play/compilación y la tapa estira toda la malla al salir volando.
            imp.isReadable = true;
            imp.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            imp.resampleCurves = true;
            var defaults = imp.defaultClipAnimations;
            var list = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            foreach (var c in Clips)
            {
                var src = defaults.FirstOrDefault(d => d.takeName.EndsWith("|" + c.name) || d.takeName == c.name);
                if (src == null) { Debug.LogWarning("[Slime] Falta el clip " + c.name + " en el FBX"); continue; }
                src.name = c.name; src.loopTime = c.loop; src.loopPose = false; src.lockRootRotation = src.lockRootPositionXZ = src.lockRootHeightY = false;
                float len = Mathf.Max(1, src.lastFrame - src.firstFrame);
                src.events = c.ev.Select(e => new AnimationEvent { time = Mathf.Clamp01(e.f / len), functionName = "OnSlimeEvent", stringParameter = e.id }).ToArray();
                list.Add(src);
            }
            imp.clipAnimations = list.ToArray();
            imp.SaveAndReimport();
        }

        /// <summary>Verde medio del cuerpo en la textura (para que los párpados se fundan con la piel).</summary>
        static Color AverageJelly()
        {
            var t = new Texture2D(2, 2);
            t.LoadImage(System.IO.File.ReadAllBytes(Dir + "/Slime_BaseColor.jpg"));
            var px = t.GetPixels32(); double r = 0, g = 0, b = 0; int n = 0;
            for (int i = 0; i < px.Length; i += 7)
            {
                var c = px[i]; if (c.g - Mathf.Max(c.r, c.b) < 40 || c.g < 120) continue;
                r += c.r; g += c.g; b += c.b; n++;
            }
            Object.DestroyImmediate(t);
            return n == 0 ? new Color(0.7f, 0.86f, 0.18f) : new Color((float)(r / n / 255), (float)(g / n / 255), (float)(b / n / 255));
        }

        static Material MakeMaterial()
        {
            string path = Dir + "/Slime_Toon.mat";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Slime_BaseColor.jpg");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Lizards/Toon")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Lizards/Toon");
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_OutlineWidth", 0f);                    // contorno negro solo en los personajes jugables
            m.SetFloat("_SpecSize", 0.18f); m.SetFloat("_ShadowThreshold", 0.05f); m.SetFloat("_ShadowSoftness", 0.25f);
            m.SetColor("_ShadowColor", new Color(0.62f, 0.7f, 0.8f)); m.SetColor("_RimColor", new Color(0.9f, 1f, 0.7f, 0.35f)); m.SetFloat("_RimPower", 2.6f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static AnimatorController MakeController()
        {
            string path = Dir + "/Slime.controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
            foreach (var c in Clips)
            {
                if (!clips.TryGetValue(c.name, out var clip)) continue;
                var st = sm.AddState(c.name); st.motion = clip; st.writeDefaultValues = true;
                if (c.name == "Idle") sm.defaultState = st;
            }
            return ctrl;
        }

        static GameObject MakePrefab(Material mat, AnimatorController ctrl)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var root = new GameObject("Slime");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            inst.name = "Modelo";
            // El frente del modelo (la cara) debe mirar a +Z, que es hacia donde avanza el enemigo.
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * Scale;
            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            inst.AddComponent<SlimeAnimationEvents>();
            var skin = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            skin.sharedMaterial = mat; skin.updateWhenOffscreen = false; skin.localBounds = new Bounds(new Vector3(0, 0.5f, 0), new Vector3(2.6f, 1.6f, 2.6f));
            Transform B(string n) => inst.GetComponentsInChildren<Transform>(true).First(t => t.name == n);

            var rb = root.AddComponent<Rigidbody>(); rb.mass = 4f; rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var cap = root.AddComponent<CapsuleCollider>(); cap.radius = 0.42f * Scale; cap.height = 0.84f * Scale; cap.center = new Vector3(0, 0.42f * Scale, 0);
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Dir + "/Slime.physicMaterial");
            if (!pm) { pm = new PhysicsMaterial("Slime") { dynamicFriction = 0.6f, staticFriction = 0.6f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Average }; AssetDatabase.CreateAsset(pm, Dir + "/Slime.physicMaterial"); }
            cap.sharedMaterial = pm;
            var hp = root.AddComponent<EnemyHealth>(); hp.health = 4f; hp.destroyOnDeath = false; hp.flashColor = new Color(1.9f, 1.9f, 1.6f);
            var rag = root.AddComponent<SlimeRagdoll>();
            rag.animator = anim; rag.skin = skin; rag.root = B("Root"); rag.body = B("Body"); rag.mid = B("Mid"); rag.top = B("Top"); rag.lid = B("Lid");
            rag.skirts = new[] { B("Skirt_F"), B("Skirt_B"), B("Skirt_L"), B("Skirt_R") };
            var mouth = new GameObject("Boca").transform; mouth.SetParent(root.transform, false); mouth.localPosition = new Vector3(0, 0.42f, 0.62f) * Scale; // por delante de la cápsula
            var ai = root.AddComponent<SlimeEnemy>();
            ai.hopDistance = 1.4f;
            ai.animator = anim; ai.midBone = B("Mid"); ai.topBone = B("Top"); ai.mouth = mouth; ai.ragdoll = rag; ai.health = hp;
            // Cara animada (ojos, párpados, cejas y boca) pegada a los anclajes que vienen en el FBX.
            var faceGo = new GameObject("Cara"); faceGo.transform.SetParent(inst.transform, false);
            var face = faceGo.AddComponent<SlimeFace>();
            face.skin = skin; face.body = B("Body"); face.mid = B("Mid"); face.top = B("Top");
            face.anchorEyeL = B("Face_EyeL"); face.anchorEyeR = B("Face_EyeR"); face.anchorMouth = B("Face_Mouth");
            var avg = AverageJelly(); face.jelly = avg; face.jellyDark = new Color(avg.r * 0.28f, avg.g * 0.36f, avg.b * 0.3f);
            ai.face = face;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
