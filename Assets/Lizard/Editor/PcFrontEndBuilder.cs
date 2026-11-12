using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Lizards.EditorTools
{
    public static class PcFrontEndBuilder
    {
        const string ArtFolder = "Assets/Lizard/Art/PcMenu";
        const string PrefabPath = "Assets/Lizard/Resources/PcFrontEnd.prefab";
        const string MenuPath = "Assets/Scenes/MainMenu.unity";
        const string Fonts = "Assets/TextMesh Pro/Examples & Extras/Fonts/";
        const string SunnyPath = "Assets/Lizard/Prefabs/Sunny.prefab";
        const string ApplePath = "Assets/Lizard/Art/FoodCollection/Prefabs/Apple.prefab";
        const int Layer = 30;

        [MenuItem("Lizards/Interfaz PC/Preparar menú principal")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Sal del modo Play antes de preparar el menú.");
            Directory.CreateDirectory(ArtFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            var previous = SceneManager.GetActiveScene();
            var stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(stage);
            var materialAssets = new System.Collections.Generic.List<Material>();
            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.63f, .49f, .76f);
                var camera = new GameObject("Cámara de ilustración").AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << Layer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.nearClipPlane = .04f;
                camera.farClipPlane = 60;
                var light = new GameObject("Luz de ilustración").AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, .86f, .98f);
                light.intensity = 1.12f;
                light.cullingMask = 1 << Layer;
                light.transform.rotation = Quaternion.Euler(34, 205, -20);

                var set = new GameObject("Alcantarillas").transform;
                var violet = Material(new Color(.28f, .15f, .43f));
                var panel = Material(new Color(.40f, .23f, .55f));
                var dark = Material(new Color(.15f, .08f, .26f));
                var green = Material(new Color(.11f, .66f, .59f));
                var purple = Material(new Color(.57f, .095f, .62f));
                var orange = Material(new Color(.90f, .48f, .14f));
                var floor = Material(new Color(.22f, .23f, .29f));
                Box(set, new Vector3(0, 4.3f, -3.5f), new Vector3(29, 9, .8f), violet);
                Box(set, new Vector3(-10.3f, 4.3f, 0), new Vector3(.8f, 9, 10), panel);
                Box(set, new Vector3(0, -.15f, -.5f), new Vector3(26, .3f, 12), floor);
                Box(set, new Vector3(0, 8.15f, -2.9f), new Vector3(23, .45f, 1.7f), dark);
                for (int col = -5; col <= 5; col++)
                    for (int row = 0; row < 4; row++)
                        if ((col + row * 2) % 3 != 0)
                            Box(set, new Vector3(col * 2.1f + (row % 2) * .6f, 1f + row * 1.9f, -3.04f), new Vector3(1.88f, .12f, .12f), panel);
                Pipe(set, new[] { new Vector3(-11, 7.15f, -1.2f), new Vector3(-1.1f, 5.4f, -1.7f), new Vector3(3, 4.8f, -2.1f), new Vector3(3, .55f, -2.1f) }, .57f, purple, dark);
                Pipe(set, new[] { new Vector3(-7.2f, 8.55f, -1.2f), new Vector3(-2.6f, 7.7f, -1.9f), new Vector3(.7f, 7.1f, -2f), new Vector3(.7f, 5.9f, -2f) }, .27f, green, dark);
                Pipe(set, new[] { new Vector3(4.2f, .8f, -2.3f), new Vector3(4.2f, 3.1f, -2.3f), new Vector3(9.1f, 3.6f, -2.3f), new Vector3(9.1f, 7.2f, -2.3f) }, .31f, green, dark);
                Pipe(set, new[] { new Vector3(-4.2f, .2f, -.9f), new Vector3(-4.2f, 1.95f, -.9f), new Vector3(-.7f, 2.3f, -.9f) }, .08f, orange, dark);
                for (int i = 0; i < 6; i++)
                    Box(set, new Vector3(-3.8f + i * .5f, .61f, -2.5f), new Vector3(.24f, 1.22f, .18f), dark);
                Box(set, new Vector3(-2.55f, 1.28f, -2.52f), new Vector3(3.1f, .21f, .25f), panel);
                camera.fieldOfView = 46;
                camera.transform.position = new Vector3(3.4f, 2.3f, 9.4f);
                camera.transform.LookAt(new Vector3(-.6f, 4.7f, -2.4f));
                Save("Fondo", Render(camera, 1600, 900, new Color(.13f, .055f, .22f)), 1600, 900);
                set.gameObject.SetActive(false);

                var sunny = Model(SunnyPath);
                sunny.transform.rotation = Quaternion.Euler(0, 18, -9);
                camera.orthographic = true;
                camera.orthographicSize = .82f;
                camera.transform.position = new Vector3(0, .46f, 5);
                camera.transform.LookAt(new Vector3(0, .46f, 0));
                Save("Sunny", Alpha(camera, 600, 600), 600, 600);
                sunny.SetActive(false);

                var apple = Model(ApplePath);
                apple.transform.rotation = Quaternion.Euler(0, -22, 10);
                camera.orthographicSize = .33f;
                camera.transform.position = new Vector3(0, .14f, 5);
                camera.transform.LookAt(new Vector3(0, .12f, 0));
                Save("Manzana", Alpha(camera, 360, 360), 360, 360);

                sunny.SetActive(true);
                sunny.transform.SetPositionAndRotation(new Vector3(-.92f, -.44f, 0), Quaternion.Euler(-8, 38, -22));
                sunny.transform.localScale = Vector3.one * 1.56f;
                apple.transform.SetPositionAndRotation(new Vector3(1.46f, .20f, .25f), Quaternion.Euler(12, -30, 24));
                apple.transform.localScale *= 2.7f;
                var tongue = Material(new Color(.96f, .25f, .57f));
                var mouth = sunny.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Boca (origen)");
                Vector3 start = mouth ? mouth.position : new Vector3(-.4f, .45f, .35f);
                Vector3 end = new Vector3(1.02f, .35f, .27f);
                var trail = new GameObject("Lengua de la ilustración").transform;
                Vector3 last = start;
                for (int i = 1; i <= 18; i++)
                {
                    float t = i / 18f;
                    Vector3 point = Vector3.Lerp(start, end, t) + new Vector3(0, .34f * Mathf.Sin(t * Mathf.PI), .23f * Mathf.Sin(t * Mathf.PI));
                    float radius = Mathf.Lerp(.105f, .16f, t * t);
                    Cylinder(trail, last, point, radius, tongue);
                    Sphere(trail, point, radius, tongue);
                    last = point;
                }
                camera.orthographicSize = 1.32f;
                camera.transform.position = new Vector3(.13f, .48f, 6);
                camera.transform.LookAt(new Vector3(.13f, .48f, 0));
                Save("Sunny-Carga", Alpha(camera, 1200, 810), 1200, 810);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(stage, true);
                foreach (var material in materialAssets) Object.DestroyImmediate(material);
            }

            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(ArtFolder, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var root = new GameObject("Menú PC");
            try
            {
                var frontend = root.AddComponent<PcFrontEnd>();
                frontend.sewerBackground = Texture("Fondo");
                frontend.sunnyPortrait = Texture("Sunny");
                frontend.loadingAction = Texture("Sunny-Carga");
                frontend.applePortrait = Texture("Manzana");
                frontend.displayFont = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Bangers.ttf");
                frontend.bodyFont = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Roboto-Bold.ttf");
                frontend.ambientOcclusion = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/PC_Renderer.asset")
                    .OfType<ScriptableRendererFeature>().FirstOrDefault(feature => feature.name == "ScreenSpaceAmbientOcclusion");
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }

            var menu = SceneManager.GetSceneByPath(MenuPath);
            bool opened = !menu.isLoaded;
            if (opened)
                menu = File.Exists(MenuPath) ? EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Additive)
                    : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            if (!menu.GetRootGameObjects().Any(obj => obj.GetComponent<PcFrontEnd>()))
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), menu);
            if (!menu.GetRootGameObjects().Any(obj => obj.GetComponent<Camera>()))
            {
                var backgroundCamera = new GameObject("Cámara de menú", typeof(Camera), typeof(AudioListener));
                SceneManager.MoveGameObjectToScene(backgroundCamera, menu);
                var camera = backgroundCamera.GetComponent<Camera>();
                camera.cullingMask = 0;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.075f, .045f, .105f);
            }
            EditorSceneManager.SaveScene(menu, MenuPath);
            SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(menu, true);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuPath, true) }
                .Concat(EditorBuildSettings.scenes.Where(scene => scene.path != MenuPath)).ToArray();
            AssetDatabase.SaveAssets();

            Material Material(Color color)
            {
                var material = new Material(Shader.Find("Lizards/Toon") ?? Shader.Find("Universal Render Pipeline/Lit"));
                material.SetColor("_BaseColor", color);
                material.SetColor("_ShadowColor", new Color(.49f, .36f, .60f));
                material.SetFloat("_OutlineWidth", 0);
                materialAssets.Add(material);
                return material;
            }
        }

        static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtFolder + "/" + name + ".png");

        static GameObject Model(string path)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            foreach (var child in obj.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = Layer;
                if (child.name == "Bola gigante" || child.name == "Burbuja" || child.name == "Señal de ayuda") child.gameObject.SetActive(false);
            }
            return obj;
        }

        static Transform Shape(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.layer = Layer;
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj.transform;
        }

        static void Box(Transform parent, Vector3 position, Vector3 size, Material material) => Shape(parent, PrimitiveType.Cube, position, size, material);
        static void Sphere(Transform parent, Vector3 position, float radius, Material material) => Shape(parent, PrimitiveType.Sphere, position, Vector3.one * radius * 2, material);
        static void Cylinder(Transform parent, Vector3 from, Vector3 to, float radius, Material material)
        {
            Vector3 delta = to - from;
            var part = Shape(parent, PrimitiveType.Cylinder, (from + to) * .5f, new Vector3(radius * 2, delta.magnitude * .5f, radius * 2), material);
            part.rotation = Quaternion.FromToRotation(Vector3.up, delta);
        }

        static void Pipe(Transform parent, Vector3[] path, float radius, Material material, Material flange)
        {
            for (int i = 1; i < path.Length; i++)
            {
                Cylinder(parent, path[i - 1], path[i], radius, material);
                Sphere(parent, path[i], radius, material);
                Vector3 direction = (path[i] - path[i - 1]).normalized;
                for (int j = 1; j <= 2; j++)
                {
                    Vector3 point = Vector3.Lerp(path[i - 1], path[i], j / 3f);
                    Cylinder(parent, point - direction * .11f, point + direction * .11f, radius * 1.15f, flange);
                }
            }
        }

        static Color32[] Alpha(Camera camera, int width, int height)
        {
            var black = Render(camera, width, height, Color.black);
            var white = Render(camera, width, height, Color.white);
            for (int i = 0; i < black.Length; i++)
            {
                var b = black[i];
                var w = white[i];
                float alpha = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 765f);
                black[i] = alpha < .02f ? new Color32(0, 0, 0, 0) : new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b.r / alpha), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b.g / alpha), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b.b / alpha), 0, 255),
                    (byte)Mathf.RoundToInt(alpha * 255));
            }
            return black;
        }

        static Color32[] Render(Camera camera, int width, int height, Color background)
        {
            var target = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.backgroundColor = background;
                target.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                return image.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }

        static void Save(string name, Color32[] pixels, int width, int height)
        {
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels);
            image.Apply();
            File.WriteAllBytes(ArtFolder + "/" + name + ".png", image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }
    }
}
