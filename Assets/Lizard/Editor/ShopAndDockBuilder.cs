using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Crear tienda, balsa, cuenco y embarcadero (diseños nuevos).
    /// Recrea los modelos de las hojas de diseño y los coloca donde estaban sus versiones anteriores,
    /// conservando la lógica (campana, probador de sombreros, entrega de comida al Jefe y salida en balsa).
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        static readonly Color ShingleA = new(1f, 0.5f, 0.15f), ShingleB = new(0.95f, 0.42f, 0.12f), Mint = new(0.72f, 0.92f, 0.85f),
            Glass = new(0.12f, 0.35f, 0.42f), WallGrey = new(0.6f, 0.62f, 0.66f), Timber = new(0.28f, 0.2f, 0.16f), HatBlack = new(0.2f, 0.18f, 0.24f),
            Purple = new(0.62f, 0.3f, 0.85f), Gold = new(1f, 0.75f, 0.2f), Teal = new(0.2f, 0.62f, 0.7f), BustYellow = new(1f, 0.8f, 0.2f);

        [MenuItem("Lizards/Crear tienda, balsa, cuenco y embarcadero (diseños nuevos)", priority = 13)]
        public static string BuildShopAndDock()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Lizard/Models", "Lobby");
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            string log = HatShop() + LaunchRaft() + FeedingBowl() + RaisedDock();
            EditorSceneManager.MarkSceneDirty(l.scene);
            EditorSceneManager.SaveScene(l.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Diseños nuevos:" + log);
            return log;
        }

        // ================= Tienda de sombreros =================

        static string HatShop()
        {
            var oldShop = GameObject.Find("Tienda con forma de sombrero");
            Vector3 c = new Vector3(-22f, 0.36f, -3f);
            // Fuera lo anterior (se queda la base de piedra como suelo).
            foreach (var n in new[] { "Tienda de sombreros (generada)", "Campana de la tienda (generada)" }) { var o = lobby.Find(n); if (o) Object.DestroyImmediate(o.gameObject); }
            if (oldShop) for (int i = oldShop.transform.childCount - 1; i >= 0; i--) { var k = oldShop.transform.GetChild(i); if (k.name != "Base tienda") Object.DestroyImmediate(k.gameObject); }
            foreach (Transform t in lobby) if (t.name == "Cartel / SOMBREROS") t.gameObject.SetActive(false);
            var root = Fresh("Tienda de sombreros (generada)", c);
            const float R = 3.6f, WallH = 3.0f;

            // ---- Paredes redondas: zócalo oscuro, pared gris con vigas, puerta en arco y ventanas ----
            System.Func<Vector3, float> walls = p =>
            {
                float rr = new Vector2(p.x, p.z).magnitude;
                float wall = Mathf.Max(Mathf.Abs(rr - R) - 0.18f, Mathf.Max(-p.y, p.y - WallH));
                float plinth = Mathf.Max(rr - (R + 0.32f), Mathf.Abs(p.y - 0.18f) - 0.2f) - 0.04f;
                float floor = Mathf.Max(rr - R, Mathf.Abs(p.y - 0.05f) - 0.06f);
                float d = Mathf.Min(Mathf.Min(wall, plinth), floor);
                // Vigas verticales y viga superior.
                for (int k = 0; k < 10; k++)
                {
                    float a = (k + 0.5f) / 10f * Mathf.PI * 2f;
                    d = Mathf.Min(d, Box(Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0) * p, new Vector3(R + 0.12f, 1.55f, 0f), new Vector3(0.1f, 1.45f, 0.13f), 0.03f));
                }
                d = Mathf.Min(d, Mathf.Max(Mathf.Abs(rr - (R + 0.1f)) - 0.12f, Mathf.Abs(p.y - (WallH - 0.1f)) - 0.12f));
                // Puerta en arco (delante, -Z).
                float door = Mathf.Min(Box(p, new Vector3(0, 1.1f, -R), new Vector3(0.95f, 1.1f, 0.8f), 0.05f), Mathf.Max(new Vector2(p.x / 0.95f, (p.y - 2.2f) / 0.6f).magnitude - 1f, Mathf.Abs(p.z + R) - 0.8f));
                d = Mathf.Max(d, -door);
                d = Mathf.Min(d, Mathf.Max(Mathf.Abs(new Vector2(p.x / 1.1f, (p.y - 2.2f) / 0.75f).magnitude - 1f) - 0.1f, Mathf.Abs(p.z + R + 0.12f) - 0.1f) + (p.y < 2.2f ? 9f : 0f)); // marco arco
                return d;
            };
            var wallMesh = Sdf(walls, new Bounds(new Vector3(0, 1.6f, 0), new Vector3(R * 2f + 1.2f, 3.6f, R * 2f + 1.2f)), 0.05f, "TiendaS_Paredes", (p, n) =>
            {
                float rr = new Vector2(p.x, p.z).magnitude;
                if (p.y < 0.42f) return Timber * 1.1f;
                if (rr < R - 0.12f) return new Color(0.2f, 0.15f, 0.2f);                                   // interior
                if (rr > R + 0.05f && p.y < WallH - 0.25f) return Timber;                                   // vigas
                if (p.y > WallH - 0.25f) return Timber;
                return Color.Lerp(WallGrey, WallGrey * 1.08f, N(p, 1.5f));
            });
            Part("Paredes", root, wallMesh).AddComponent<MeshCollider>();

            // Ventanal turquesa a la derecha con tejadillo, y ventana en arco detrás.
            Mesh Panel(string name, Vector3 half, Color col) => Sdf(p => Box(p, Vector3.zero, half, 0.05f), new Bounds(Vector3.zero, half * 2f + Vector3.one * 0.3f), 0.03f, name, (p, n) => col);
            var big = Part("Ventanal", root, Panel("TiendaS_Ventanal", new Vector3(0.85f, 0.75f, 0.06f), Teal));
            PlaceOnWall(big.transform, -40f, R + 0.12f, 1.5f);
            var awning = Sdf(p =>
            {
                float d = Box(Quaternion.Euler(-25f, 0, 0) * p, Vector3.zero, new Vector3(1.05f, 0.08f, 0.45f), 0.06f);
                for (int k = -2; k <= 2; k++) d = SdfMesher.SMin(d, Box(Quaternion.Euler(-25f, 0, 0) * p, new Vector3(k * 0.42f, 0.04f, 0.2f), new Vector3(0.18f, 0.08f, 0.32f), 0.06f), 0.04f);
                return d;
            }, new Bounds(Vector3.zero, new Vector3(2.6f, 1f, 1.4f)), 0.03f, "TiendaS_Tejadillo", (p, n) => Mathf.Repeat(p.x * 2.4f, 1f) < 0.12f ? ShingleB * 0.8f : ShingleA);
            var aw = Part("Tejadillo", root, awning); PlaceOnWall(aw.transform, -40f, R + 0.45f, 2.45f);
            var backWin = Part("Ventana trasera", root, ArchWindow("TiendaS_VentanaArco"));
            PlaceOnWall(backWin.transform, 180f, R + 0.18f, 1.45f);

            // ---- Tejado de tejas naranjas en tres faldones ----
            System.Func<Vector3, float> roof = p =>
            {
                float rr = new Vector2(p.x, p.z).magnitude, a = Mathf.Atan2(p.z, p.x);
                float d = float.MaxValue;
                for (int k = 0; k < 3; k++)
                {
                    float y0 = 2.85f + k * 0.75f, r0 = 4.75f - k * 0.7f;
                    float tiles = Mathf.Abs(Mathf.Sin(a * (22 - k * 4) * 0.5f)) * 0.12f;           // bordes festoneados
                    float slope = (rr - (r0 - 1.1f)) * 0.55f;                                         // falda inclinada
                    float skirt = Mathf.Max(Mathf.Abs(p.y - (y0 + 0.55f - slope)) - 0.2f, Mathf.Max(rr - r0 - tiles, (r0 - 1.5f) - rr));
                    d = SdfMesher.SMin(d, skirt - 0.06f, 0.1f);
                }
                d = Mathf.Min(d, Mathf.Max(rr - 2.9f, Mathf.Abs(p.y - 4.9f) - 0.4f));             // tapa bajo el sombrero
                return d;
            };
            var roofMesh = Sdf(roof, new Bounds(new Vector3(0, 4.1f, 0), new Vector3(10.4f, 3.2f, 10.4f)), 0.05f, "TiendaS_Tejado", (p, n) =>
            {
                float a = Mathf.Atan2(p.z, p.x);
                int cell = Mathf.FloorToInt((a + Mathf.PI) / (Mathf.PI * 2f) * 22f) + Mathf.FloorToInt(p.y * 2.5f) * 7;
                Color c = (cell % 3 == 0) ? ShingleB : ShingleA;
                if (n.y < -0.2f) c = c * 0.7f;
                return Color.Lerp(c, c * 1.12f, Mathf.Clamp01(n.y));
            });
            Part("Tejado", root, roofMesh).AddComponent<MeshCollider>();

            // Buhardillas "ojo de rana" delante.
            for (int s = -1; s <= 1; s += 2)
            {
                var dorm = new GameObject("Buhardilla").transform; dorm.SetParent(root, false);
                dorm.localPosition = new Vector3(s * 1.5f, 4.6f, -3.25f); dorm.localRotation = Quaternion.Euler(-8f, s * 22f, 0f); dorm.localScale = Vector3.one * 1.15f;
                Part("Capucha", dorm, DormerHood());
                Part("Ventana", dorm, ArchWindow("TiendaS_VentanaArco")).transform.localPosition = new Vector3(0, 0.05f, -0.62f);
            }

            // ---- Chistera gigante, un poco ladeada ----
            var hat = Sdf(p =>
            {
                float rr = new Vector2(p.x / 1.05f, p.z).magnitude;
                float brim = Mathf.Max(rr - 3.9f, Mathf.Abs(p.y - (0.0f + Mathf.Pow(Mathf.Max(0f, rr - 2.6f), 2f) * 0.22f)) - 0.16f) - 0.05f;
                float crown = Mathf.Max(new Vector2(p.x, p.z).magnitude - (2.45f + p.y * 0.05f), Mathf.Abs(p.y - 1.45f) - 1.4f) - 0.12f;
                return SdfMesher.SMin(brim, crown, 0.2f);
            }, new Bounds(new Vector3(0, 1.3f, 0), new Vector3(8.6f, 3.6f, 8.6f)), 0.05f, "TiendaS_Chistera", (p, n) =>
            {
                if (p.y > 0.2f && p.y < 0.75f && new Vector2(p.x, p.z).magnitude > 2.4f) return Purple;
                return Color.Lerp(HatBlack, HatBlack * 1.3f, Mathf.Clamp01(n.y * 0.5f + 0.5f));
            });
            var hg = Part("Chistera", root, hat); hg.transform.localPosition = new Vector3(0.2f, 4.85f, 0.35f); hg.transform.localRotation = Quaternion.Euler(4f, 0f, -7f);

            // ---- Puerta: cortinas moradas con lazo dorado y alfombra que sale ----
            var curtain = Sdf(p =>
            {
                float wave = Mathf.Sin(p.x * 18f) * 0.05f;
                float d = Box(p, new Vector3(0, 1.15f, 0), new Vector3(0.42f, 1.15f, 0.06f + wave * 0f), 0.04f) + Mathf.Abs(wave) * 0.3f;
                float tie = new Vector2(new Vector2(p.x * 1.4f, p.z).magnitude - 0.18f, p.y - 1.0f).magnitude - 0.05f;
                return Mathf.Min(d, tie);
            }, new Bounds(new Vector3(0, 1.15f, 0), new Vector3(1.2f, 2.6f, 0.6f)), 0.025f, "TiendaS_Cortina", (p, n) => Mathf.Abs(p.y - 1.0f) < 0.07f ? Gold : Color.Lerp(Purple * 0.8f, Purple, Mathf.Repeat(p.x * 3f, 1f)));
            for (int s = -1; s <= 1; s += 2) { var cg = Part("Cortina", root, curtain); cg.transform.localPosition = new Vector3(s * 0.55f, 0.05f, -R + 0.1f); cg.transform.localRotation = Quaternion.Euler(0, 0, s * 4f); }
            var rug = Sdf(p =>
            {
                float curl = p.z < -2.6f ? (p.z + 2.6f) * (p.z + 2.6f) * -0.6f : 0f;
                return Box(p, new Vector3(0, 0.03f - curl * 0f, -1.55f), new Vector3(0.85f, 0.03f, 1.55f), 0.02f);
            }, new Bounds(new Vector3(0, 0.05f, -1.55f), new Vector3(2f, 0.3f, 3.5f)), 0.02f, "TiendaS_Alfombra", (p, n) => Mathf.Abs(Mathf.Abs(p.x) - 0.62f) < 0.07f ? Gold : Purple);
            var rg = Part("Alfombra", root, rug); rg.transform.localPosition = new Vector3(0, 0.02f, -R + 0.2f);

            // ---- Campana en soporte turquesa (a la izquierda de la puerta) ----
            var bracket = Sdf(p =>
            {
                float plate = Box(p, Vector3.zero, new Vector3(0.22f, 0.3f, 0.06f), 0.06f);
                float hook = new Vector2(new Vector2(p.y - 0.05f, p.z + 0.28f).magnitude - 0.2f, p.x).magnitude - 0.07f;
                hook = Mathf.Max(hook, -(p.z + 0.08f) * 1f + 0f);
                return Mathf.Min(plate, hook);
            }, new Bounds(new Vector3(0, 0, -0.25f), new Vector3(0.8f, 1f, 0.9f)), 0.02f, "TiendaS_Soporte", (p, n) => Teal);
            var br = Part("Soporte campana", root, bracket); PlaceOnWall(br.transform, 38f, R + 0.2f, 2.3f);
            var bellPivot = new GameObject("Pivote campana").transform; bellPivot.SetParent(br.transform, false); bellPivot.localPosition = new Vector3(0, -0.1f, -0.5f);
            var bellMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Campana_Bronce.asset");
            if (bellMesh) { var bg = Part("Campana", bellPivot, bellMesh); bg.transform.localScale = Vector3.one * 0.7f; }
            var clap = new GameObject("Badajo").transform; clap.SetParent(bellPivot, false); clap.localPosition = new Vector3(0, -0.15f, 0);
            var clapMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Campana_Badajo.asset");
            if (clapMesh) { var cm = Part("Malla", clap, clapMesh); cm.transform.localScale = Vector3.one * 0.7f; }
            bellPivot.gameObject.AddComponent<BellSwing>().clapper = clap;
            var anchor = GameObject.Find("Tocar campana");
            if (anchor) { Vector3 bp = br.transform.position; anchor.transform.position = new Vector3(bp.x, 0f, bp.z) - br.transform.forward * 1.2f; }

            // ---- Pedestales con busto: gafas de buceo y auriculares ----
            var bust = Bust();
            var goggles = Goggles(); var phones = Headphones();
            for (int s = 0; s < 2; s++)
            {
                var ped = new GameObject(s == 0 ? "Pedestal con gafas" : "Pedestal con audífonos").transform; ped.SetParent(root, false);
                ped.localPosition = new Vector3(1.8f + s * 1.35f, 0f, -R - 0.9f - s * 0.35f); ped.localRotation = Quaternion.Euler(0, -15f - s * 10f, 0);
                Part("Busto", ped, bust);
                Part(s == 0 ? "Gafas" : "Audífonos", ped, s == 0 ? goggles : phones);
                var cc = ped.gameObject.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 0.7f, 0); cc.radius = 0.45f; cc.height = 1.4f;
            }
            return " tienda de sombreros;";
        }

        static void PlaceOnWall(Transform t, float angleFromFront, float radius, float y)
        {
            // 0° = delante (-Z); positivo hacia la izquierda (+X visto desde fuera es la derecha de la tienda).
            float a = angleFromFront * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(-Mathf.Sin(a), 0f, -Mathf.Cos(a));
            t.localPosition = dir * radius + Vector3.up * y;
            t.localRotation = Quaternion.LookRotation(-dir);
        }

        static Mesh ArchWindow(string name)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/{name}.asset") is Mesh m0 && m0) return m0;
            return Sdf(p =>
            {
                float shape(Vector3 q, float w, float h) => Mathf.Min(Box(q, new Vector3(0, -h * 0.3f, 0), new Vector3(w, h * 0.7f, 0.1f), 0.03f), Mathf.Max(new Vector2(q.x / w, q.y / (h * 0.8f)).magnitude - 1f, Mathf.Abs(q.z) - 0.1f));
                float frame = Mathf.Max(shape(p, 0.62f, 0.62f), -shape(p + new Vector3(0, 0, 0.05f), 0.48f, 0.5f));
                float glass = shape(p + new Vector3(0, 0, -0.04f), 0.5f, 0.52f);
                float bars = Mathf.Min(Box(p, new Vector3(0, -0.05f, -0.03f), new Vector3(0.04f, 0.5f, 0.06f), 0.01f), Box(p, new Vector3(0, -0.05f, -0.03f), new Vector3(0.5f, 0.04f, 0.06f), 0.01f));
                return Mathf.Min(Mathf.Min(frame, glass), Mathf.Max(bars, shape(p, 0.5f, 0.52f)));
            }, new Bounds(new Vector3(0, -0.1f, 0), new Vector3(1.5f, 1.4f, 0.4f)), 0.015f, name, (p, n) =>
            {
                float inner = Mathf.Max(Mathf.Abs(p.x) / 0.48f, 0f);
                bool isGlass = p.z > -0.02f && Mathf.Abs(p.x) < 0.47f && p.y < 0.38f && p.y > -0.75f && Mathf.Abs(p.x) > 0.05f && Mathf.Abs(p.y + 0.05f) > 0.05f;
                return isGlass ? Glass : Mint;
            });
        }

        static Mesh DormerHood()
        {
            return Sdf(p =>
            {
                float outer = Mathf.Max(SdfMesher.Ellipsoid(p, new Vector3(0, -0.15f, 0.1f), new Vector3(0.85f, 0.85f, 0.75f)), -p.y - 0.75f);
                float inner = SdfMesher.Ellipsoid(p, new Vector3(0, -0.2f, -0.2f), new Vector3(0.68f, 0.72f, 0.75f));
                return Mathf.Max(Mathf.Max(outer, -inner), p.z - 0.6f) + Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.y + 0.15f, p.x) * 6f)) * 0.03f;
            }, new Bounds(new Vector3(0, -0.15f, 0.1f), new Vector3(2f, 2f, 1.8f)), 0.03f, "TiendaS_Buhardilla", (p, n) => Mathf.Repeat(Mathf.Atan2(p.y + 0.15f, p.x) * 6f / Mathf.PI, 1f) < 0.08f ? ShingleB * 0.8f : ShingleA);
        }

        static Mesh Bust()
        {
            return Sdf(p =>
            {
                float stand = Mathf.Min(Mathf.Max(new Vector2(p.x, p.z).magnitude - 0.38f, Mathf.Abs(p.y - 0.05f) - 0.05f) - 0.02f,
                                        Mathf.Max(new Vector2(p.x, p.z).magnitude - 0.08f, Mathf.Abs(p.y - 0.3f) - 0.25f));
                float cup = Mathf.Max(SdfMesher.Ellipsoid(p, new Vector3(0, 0.75f, 0), new Vector3(0.48f, 0.3f, 0.42f)), p.y - 0.85f);
                float head = SdfMesher.Ellipsoid(p, new Vector3(0, 1.0f, 0), new Vector3(0.42f, 0.3f, 0.38f));
                for (int s = -1; s <= 1; s += 2) head = SdfMesher.SMin(head, (p - new Vector3(s * 0.2f, 1.25f, -0.08f)).magnitude - 0.15f, 0.05f);
                head = Mathf.Max(head, -Mathf.Max(Mathf.Abs(p.y - 0.95f) - 0.015f, -p.z - 0.25f)); // boca
                return Mathf.Min(stand, SdfMesher.SMin(cup, head, 0.05f));
            }, new Bounds(new Vector3(0, 0.7f, 0), new Vector3(1.2f, 1.6f, 1.1f)), 0.02f, "TiendaS_Busto", (p, n) => p.y < 0.58f ? Ink : BustYellow);
        }

        static Mesh Goggles()
        {
            return Sdf(p =>
            {
                float mask = Box(p, new Vector3(0, 1.22f, -0.32f), new Vector3(0.36f, 0.12f, 0.08f), 0.07f);
                float strap = Mathf.Max(Mathf.Abs(SdfMesher.Ellipsoid(p, new Vector3(0, 1.2f, 0), new Vector3(0.45f, 0.4f, 0.42f))) - 0.025f, Mathf.Abs(p.y - 1.2f) - 0.04f);
                float snorkel = SdfMesher.Capsule(p, new Vector3(0.38f, 1.0f, -0.2f), new Vector3(0.5f, 1.6f, -0.05f), 0.04f, 0.04f, out _);
                return Mathf.Min(Mathf.Min(mask, strap), snorkel);
            }, new Bounds(new Vector3(0.1f, 1.3f, -0.1f), new Vector3(1.3f, 1f, 1.1f)), 0.015f, "TiendaS_Gafas", (p, n) => p.z < -0.38f && p.y > 1.15f && p.y < 1.3f ? new Color(0.55f, 0.9f, 1f) : p.y > 1.45f ? Teal * 0.7f : Teal);
        }

        static Mesh Headphones()
        {
            return Sdf(p =>
            {
                float band = Mathf.Max(Mathf.Abs(new Vector2(p.x, p.y - 1.1f).magnitude - 0.48f) - 0.05f, Mathf.Max(Mathf.Abs(p.z) - 0.07f, 1.1f - p.y));
                float cups = Mathf.Min(SdfMesher.Ellipsoid(p, new Vector3(-0.46f, 1.05f, 0), new Vector3(0.12f, 0.2f, 0.17f)), SdfMesher.Ellipsoid(p, new Vector3(0.46f, 1.05f, 0), new Vector3(0.12f, 0.2f, 0.17f)));
                return Mathf.Min(band, cups);
            }, new Bounds(new Vector3(0, 1.3f, 0), new Vector3(1.3f, 0.9f, 0.5f)), 0.015f, "TiendaS_Audifonos", (p, n) => p.y > 1.5f ? Ink : Purple);
        }

        // ================= Balsa de salida =================

        static float RoundRectDist(Vector2 q, Vector2 half, float r)
        {
            Vector2 d = new Vector2(Mathf.Abs(q.x), Mathf.Abs(q.y)) - half + Vector2.one * r;
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - r;
        }

        static string LaunchRaft()
        {
            var raft = GameObject.Find("Balsa naranja de salida") ?? GameObject.Find("Balsa de expedición");
            if (!raft) return "";
            raft.name = "Balsa naranja de salida";
            for (int i = raft.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(raft.transform.GetChild(i).gameObject);
            var half = new Vector2(3.3f, 2.7f); const float Tube = 0.55f, Y = 0.5f;
            var tube = Sdf(p =>
            {
                float ring = RoundRectDist(new Vector2(p.x, p.z), half, 1.1f);
                // Cámaras: estrechamientos suaves cada cierto trecho del contorno.
                float seg = Mathf.Abs(Mathf.Sin((p.x * 0.75f + p.z * 0.75f + Mathf.Atan2(p.z, p.x) * 2f)));
                return new Vector2(ring, p.y - Y).magnitude - Tube * (1f - 0.05f * Mathf.Pow(seg, 18f));
            }, new Bounds(new Vector3(0, Y, 0), new Vector3(half.x * 2f + 1.6f, 1.6f, half.y * 2f + 1.6f)), 0.05f, "BalsaS_Flotador", (p, n) =>
            {
                Color c = Color.Lerp(new Color(0.95f, 0.38f, 0.1f), new Color(1f, 0.52f, 0.18f), Mathf.Clamp01(n.y * 0.5f + 0.5f));
                return c;
            });
            Part("Flotador", raft.transform, tube).AddComponent<MeshCollider>();
            var floor = Sdf(p => Mathf.Max(RoundRectDist(new Vector2(p.x, p.z), half - Vector2.one * 0.2f, 1f), Mathf.Abs(p.y - 0.08f) - 0.08f),
                new Bounds(new Vector3(0, 0.1f, 0), new Vector3(half.x * 2f, 0.5f, half.y * 2f)), 0.05f, "BalsaS_Suelo", (p, n) => new Color(0.16f, 0.17f, 0.2f));
            Part("Suelo", raft.transform, floor).AddComponent<MeshCollider>();
            // Asas negras: dos por lado.
            var handles = Sdf(p =>
            {
                float d = float.MaxValue;
                var spots = new[] { new Vector3(-1.2f, 0, -half.y), new Vector3(1.2f, 0, -half.y), new Vector3(-1.2f, 0, half.y), new Vector3(1.2f, 0, half.y),
                                    new Vector3(-half.x, 0, -0.9f), new Vector3(-half.x, 0, 0.9f), new Vector3(half.x, 0, -0.9f), new Vector3(half.x, 0, 0.9f) };
                foreach (var s in spots)
                {
                    bool alongX = Mathf.Abs(s.z) > Mathf.Abs(s.x) - 0.1f && Mathf.Abs(s.z) >= half.y - 0.01f;
                    Vector3 q = p - (s + Vector3.up * (Y + Tube * 0.9f));
                    float u = alongX ? q.x : q.z, v = alongX ? q.z : q.x;
                    d = Mathf.Min(d, new Vector2(new Vector2(u, q.y * 1.4f).magnitude - 0.28f, v).magnitude - 0.055f);
                }
                return Mathf.Max(d, Y + Tube * 0.6f - p.y);
            }, new Bounds(new Vector3(0, Y + 0.6f, 0), new Vector3(half.x * 2f + 0.8f, 0.8f, half.y * 2f + 0.8f)), 0.025f, "BalsaS_Asas", (p, n) => Ink);
            Part("Asas", raft.transform, handles);
            // Bomba azul con bocina roja en una esquina.
            var horn = Sdf(p =>
            {
                float pump = Box(p, Vector3.zero, new Vector3(0.32f, 0.3f, 0.32f), 0.1f);
                float neck = SdfMesher.Capsule(p, new Vector3(0, 0.25f, 0), new Vector3(0, 0.45f, 0), 0.1f, 0.1f, out _);
                Vector3 q = p - new Vector3(0, 0.5f, 0);
                float cone = Mathf.Max(new Vector2(q.y, q.z).magnitude - (0.08f + Mathf.Max(0f, q.x) * Mathf.Max(0f, q.x) * 1.2f), Mathf.Max(-q.x - 0.1f, q.x - 0.75f));
                cone = Mathf.Max(cone, -(Mathf.Max(new Vector2(q.y, q.z).magnitude - (0.04f + Mathf.Max(0f, q.x) * Mathf.Max(0f, q.x) * 1.1f), 0.7f - q.x)));
                return Mathf.Min(Mathf.Min(pump, neck), cone);
            }, new Bounds(new Vector3(0.3f, 0.3f, 0), new Vector3(1.8f, 1.4f, 1.2f)), 0.02f, "BalsaS_Bocina", (p, n) => p.y > 0.38f ? new Color(0.9f, 0.18f, 0.18f) : new Color(0.15f, 0.4f, 0.9f));
            var hg = Part("Bocina", raft.transform, horn);
            hg.transform.localPosition = new Vector3(half.x - 0.35f, Y + Tube + 0.25f, half.y - 0.35f);
            hg.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
            // El tablón de acceso lleva hasta el borde.
            var plank = GameObject.Find("Tablón de acceso balsa");
            if (plank)
            {
                plank.SetActive(true);
                Vector3 a = new Vector3(2f, 0.08f, 10.6f), b = new Vector3(2f, 1.0f, 14.9f);
                plank.transform.position = (a + b) * 0.5f; plank.transform.rotation = Quaternion.LookRotation(b - a);
                plank.transform.localScale = new Vector3(2.6f, 0.2f, (b - a).magnitude);
            }
            return " balsa de salida;";
        }

        // ================= Cuenco de recolección =================

        static string FeedingBowl()
        {
            var pot = GameObject.Find("Cuenco de provisiones");
            if (!pot) return "";
            Vector3 c = pot.transform.position;
            foreach (var n in new[] { "Cuenco azul", "Borde naranja del cuenco", "Fondo cuenco" }) { var g = GameObject.Find(n); if (g) { var r = g.GetComponent<Renderer>(); if (r) r.enabled = false; } }
            for (int i = pot.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(pot.transform.GetChild(i).gameObject); // burbujas y luz del caldero
            var root = Fresh("Cuenco de recolección (generado)", c);
            const float R = 4.4f;
            var blue = Sdf(p => new Vector2(new Vector2(p.x, p.z).magnitude - R, (p.y - 0.62f) * 1.05f).magnitude - 0.72f,
                new Bounds(new Vector3(0, 0.62f, 0), new Vector3(R * 2f + 1.8f, 1.6f, R * 2f + 1.8f)), 0.06f, "Cuenco_Azul",
                (p, n) => Color.Lerp(new Color(0.12f, 0.3f, 0.65f), new Color(0.22f, 0.45f, 0.85f), Mathf.Clamp01(n.y * 0.5f + 0.5f)));
            Part("Aro azul", root, blue).AddComponent<MeshCollider>();
            var orange = Sdf(p => new Vector2(new Vector2(p.x, p.z).magnitude - (R - 0.1f), p.y - 1.75f).magnitude - 0.6f,
                new Bounds(new Vector3(0, 1.75f, 0), new Vector3(R * 2f + 1.4f, 1.4f, R * 2f + 1.4f)), 0.06f, "Cuenco_Naranja",
                (p, n) => Color.Lerp(new Color(0.88f, 0.42f, 0.1f), new Color(1f, 0.58f, 0.2f), Mathf.Clamp01(n.y * 0.5f + 0.5f)));
            Part("Aro naranja", root, orange).AddComponent<MeshCollider>();
            var floor = Sdf(p => Mathf.Max(new Vector2(p.x, p.z).magnitude - (R - 0.1f), Mathf.Abs(p.y - 0.1f) - 0.08f),
                new Bounds(new Vector3(0, 0.1f, 0), new Vector3(R * 2f, 0.5f, R * 2f)), 0.07f, "Cuenco_Suelo", (p, n) => new Color(0.14f, 0.15f, 0.2f));
            Part("Suelo", root, floor);
            // Tiras de luz turquesa sobre el aro naranja.
            var glow = Mat("Cuenco_Luz", () => { var m = LizardMaterials.Lit(new Color(0.4f, 1f, 1f), 0.6f, new Color(0.3f, 1.4f, 1.5f)); return m; });
            var strips = Sdf(p =>
            {
                float rr = new Vector2(p.x, p.z).magnitude, a = Mathf.Atan2(p.z, p.x);
                float arc = Mathf.Abs(Mathf.Repeat(a / (Mathf.PI * 2f) * 8f, 1f) - 0.5f) * (Mathf.PI * 2f / 8f) * rr; // distancia al centro del tramo
                float seg = Mathf.Max(arc - 0.55f, 0f);
                return new Vector3(seg, p.y - 2.3f, rr - (R - 0.45f)).magnitude - 0.07f;
            }, new Bounds(new Vector3(0, 2.3f, 0), new Vector3(R * 2f, 0.4f, R * 2f)), 0.035f, "Cuenco_Tiras", (p, n) => Color.white);
            Part("Tiras de luz", root, strips, glow);
            // Válvula azul con anilla.
            var valve = Sdf(p => Mathf.Min(SdfMesher.Capsule(p, Vector3.zero, new Vector3(0.9f, -0.6f, 0), 0.06f, 0.06f, out _),
                    new Vector2(new Vector2(p.x - 1.05f, p.y + 0.75f).magnitude - 0.17f, p.z).magnitude - 0.05f),
                new Bounds(new Vector3(0.5f, -0.4f, 0), new Vector3(1.8f, 1.4f, 0.5f)), 0.015f, "Cuenco_Valvula", (p, n) => new Color(0.25f, 0.55f, 0.95f));
            var vg = Part("Válvula", root, valve); vg.transform.localPosition = new Vector3(R + 0.55f, 1.3f, -1.2f); vg.transform.localRotation = Quaternion.Euler(0, 20f, 0);
            return " cuenco de recolección;";
        }

        // ================= Embarcadero elevado =================

        static string RaisedDock()
        {
            var dock = GameObject.Find("Plataforma de embarque");
            if (!dock) return "";
            for (int i = dock.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(dock.transform.GetChild(i).gameObject);
            var root = new GameObject("Embarcadero elevado").transform; root.SetParent(dock.transform, false);
            Vector3 c = new Vector3(8f, 0f, 13.5f); root.position = c;
            const float TopY = 1.45f; Vector2 half = new Vector2(3.0f, 2.0f);
            Color slate = new Color(0.27f, 0.4f, 0.55f);
            var mesh = Sdf(p =>
            {
                float top = Box(p, new Vector3(0, TopY - 0.2f, 0), new Vector3(half.x, 0.2f, half.y), 0.08f);
                float legs = float.MaxValue;
                for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2)
                        legs = Mathf.Min(legs, Box(p, new Vector3(sx * (half.x - 0.45f), (TopY - 0.3f) * 0.5f - 0.3f, sz * (half.y - 0.4f)), new Vector3(0.25f, (TopY - 0.3f) * 0.5f + 0.3f, 0.25f), 0.05f));
                // Rampa hacia -Z.
                Vector3 q = p - new Vector3(0, 0, -half.y);
                float ramp = Mathf.Max(Box(p, new Vector3(0, TopY * 0.5f - 0.25f, -half.y - 1.6f), new Vector3(1.4f, TopY * 0.5f + 0.25f, 1.6f), 0.04f), (q.y - TopY) + (-q.z) * (TopY / 3.2f));
                return Mathf.Min(Mathf.Min(top, legs), ramp);
            }, new Bounds(new Vector3(0, 0.5f, -1.4f), new Vector3(half.x * 2f + 0.6f, 2.6f, half.y * 2f + 4f)), 0.025f, "Embarcadero_Plataforma", (p, n) =>
            {
                if (n.y > 0.7f && p.y > TopY - 0.05f)
                {
                    if (new Vector2(p.x, p.z).magnitude < 1.05f) return new Color(1f, 0.82f, 0.2f);                                      // círculo amarillo
                    if (Mathf.Abs(p.x) > half.x - 0.95f && Mathf.Abs(p.x) < half.x - 0.45f && Mathf.Repeat(p.z * 2.2f + p.x * 1.1f, 1f) < 0.45f) return new Color(1f, 0.82f, 0.2f); // franjas
                }
                return Color.Lerp(slate * 0.85f, slate, Mathf.Clamp01(n.y * 0.5f + 0.5f));
            });
            var pg = Part("Plataforma", root, mesh);
            pg.AddComponent<MeshCollider>();
            // Barandillas amarillas en U a los lados.
            var rail = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 a = new Vector3(s * (half.x - 0.25f), TopY, -half.y + 0.5f), b = new Vector3(s * (half.x - 0.25f), TopY, half.y - 0.5f);
                    d = Mathf.Min(d, SdfMesher.Capsule(p, a, a + Vector3.up * 0.75f, 0.13f, 0.13f, out _));
                    d = Mathf.Min(d, SdfMesher.Capsule(p, b, b + Vector3.up * 0.75f, 0.13f, 0.13f, out _));
                    d = Mathf.Min(d, SdfMesher.Capsule(p, a + Vector3.up * 0.75f, b + Vector3.up * 0.75f, 0.13f, 0.13f, out _));
                }
                return d;
            }, new Bounds(new Vector3(0, TopY + 0.45f, 0), new Vector3(half.x * 2f + 0.6f, 1.4f, half.y * 2f)), 0.03f, "Embarcadero_Barandillas", (p, n) => Color.Lerp(new Color(0.95f, 0.62f, 0.12f), new Color(1f, 0.78f, 0.2f), Mathf.Clamp01(n.y * 0.5f + 0.5f)));
            var rg = Part("Barandillas", root, rail);
            foreach (int s in new[] { -1, 1 })
            {
                var bc = rg.AddComponent<BoxCollider>(); bc.center = new Vector3(s * (half.x - 0.25f), TopY + 0.45f, 0); bc.size = new Vector3(0.3f, 0.9f, half.y * 2f - 0.8f);
            }
            // El cartel de la balsa queda sobre la plataforma.
            foreach (Transform t in lobby) if (t.name == "Cartel / BALSA DE EXPEDICIÓN") t.position = c + new Vector3(0f, TopY + 1.9f, half.y - 0.2f);
            return " embarcadero elevado.";
        }
    }
}
