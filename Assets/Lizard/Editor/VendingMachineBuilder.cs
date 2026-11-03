using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Crear expendedora (diseño nuevo).
    /// Sustituye la máquina del Lobby por el diseño de la hoja: carcasa roja redondeada sobre patitas,
    /// vitrina con 9 productos en cúpulas (mismo orden que la tienda), ojos de rana, cruceta amarilla y botón de carrito.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        static readonly Color Red = new(0.9f, 0.2f, 0.15f), RedDark = new(0.72f, 0.12f, 0.1f), Lavender = new(0.84f, 0.7f, 0.96f),
            Shelf = new(0.32f, 0.33f, 0.38f), Yellow = new(1f, 0.78f, 0.15f), GreenBtn = new(0.25f, 0.68f, 0.22f), Ink = new(0.12f, 0.1f, 0.1f);

        [MenuItem("Lizards/Crear expendedora (diseño nuevo)", priority = 12)]
        public static string BuildVendingMachine()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            var machine = GameObject.Find("Máquina expendedora roja");
            if (!l || !machine) return "No encuentro la expendedora del Lobby.";
            lobby = l.transform;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Lizard/Models", "Lobby");
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            var shop = Object.FindAnyObjectByType<SwampLobbyExpansion>();

            for (int i = machine.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(machine.transform.GetChild(i).gameObject);
            foreach (var c in machine.GetComponents<Collider>()) Object.DestroyImmediate(c);
            var root = machine.transform; // mira a -Z

            const float W = 2.8f, H = 2.3f, D = 1.1f, Y = 2.45f;            // medias medidas del cuerpo
            Vector3 win = new Vector3(-0.62f, Y, -D);                          // centro de la vitrina (cara delantera)
            Vector3 winH = new Vector3(1.78f, 1.95f, 0f);

            // ---- Carcasa: caja redondeada con hueco de vitrina delante y panel con rejilla detrás ----
            System.Func<Vector3, float> body = p =>
            {
                float d = Box(p, new Vector3(0, Y, 0), new Vector3(W, H, D), 0.38f);
                d = SMax(d, -Box(p, new Vector3(win.x, win.y, -D + 0.05f), new Vector3(winH.x, winH.y, 0.42f), 0.28f), 0.06f); // hueco
                d = Mathf.Min(d, Box(p, new Vector3(0, 1.75f, D + 0.02f), new Vector3(1.25f, 0.8f, 0.05f), 0.06f));           // tapa trasera
                for (int k = 0; k < 5; k++) d = SMax(d, -Box(p, new Vector3(0.8f, 1.35f + k * 0.2f, D + 0.06f), new Vector3(0.28f, 0.04f, 0.08f), 0.03f), 0.02f); // rejilla
                for (int sx = -1; sx <= 1; sx += 2) for (int sy = -1; sy <= 1; sy += 2)
                        d = SMax(d, -((p - new Vector3(sx * (W - 0.3f), Y + sy * (H - 0.3f), D)).magnitude - 0.09f), 0.02f);   // tornillos
                return d;
            };
            var bodyMesh = Sdf(body, new Bounds(new Vector3(0, Y, 0), new Vector3(W * 2f + 0.4f, H * 2f + 0.4f, D * 2f + 0.4f)), 0.035f, "Expendedora_Carcasa", (p, n) =>
            {
                bool inWindow = Mathf.Abs(p.x - win.x) < winH.x - 0.02f && Mathf.Abs(p.y - win.y) < winH.y - 0.02f && p.z > -D + 0.1f && p.z < 0.2f;
                if (inWindow) return Color.Lerp(new Color(0.7f, 0.55f, 0.9f), Lavender, Mathf.InverseLerp(win.y - winH.y, win.y + winH.y, p.y)); // fondo lavanda
                if (p.z > D - 0.05f && Mathf.Abs(p.x - 0.8f) < 0.33f && p.y > 1.25f && p.y < 2.35f && n.z < 0.5f) return RedDark;      // rejilla
                if ((p - new Vector3(Mathf.Sign(p.x) * (W - 0.3f), Y + Mathf.Sign(p.y - Y) * (H - 0.3f), D)).magnitude < 0.11f) return Ink;
                return Color.Lerp(RedDark, Red, Mathf.Clamp01(n.y * 0.3f + 0.75f));
            });
            Part("Carcasa", root, bodyMesh);
            var col = root.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, Y, 0); col.size = new Vector3(W * 2f, H * 2f, D * 2f);

            // Patitas.
            var feet = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2)
                        d = Mathf.Min(d, Mathf.Max(new Vector2(p.x - sx * (W - 0.45f), p.z - sz * (D - 0.35f)).magnitude - 0.2f, Mathf.Abs(p.y - 0.09f) - 0.09f) - 0.03f);
                return d;
            }, new Bounds(new Vector3(0, 0.1f, 0), new Vector3(W * 2f, 0.5f, D * 2f)), 0.03f, "Expendedora_Patas", (p, n) => Ink);
            Part("Patas", root, feet);

            // ---- Estantes con remaches azules ----
            float[] rows = { win.y + 1.3f, win.y + 0.05f, win.y - 1.2f };      // altura de cada fila (base del producto)
            var shelves = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int r = 0; r < 4; r++)
                {
                    float y = r == 0 ? win.y + 1.75f : rows[r - 1] - 0.08f;
                    d = Mathf.Min(d, Box(p, new Vector3(win.x, y, -0.62f), new Vector3(winH.x - 0.1f, 0.06f, 0.42f), 0.03f));
                    for (int b = 0; b < 4; b++) d = Mathf.Min(d, (p - new Vector3(win.x - 1.4f + b * 0.93f, y, -1.04f)).magnitude - 0.055f);
                }
                return d;
            }, new Bounds(new Vector3(win.x, win.y, -0.62f), new Vector3(winH.x * 2f, winH.y * 2f + 0.4f, 1.2f)), 0.025f, "Expendedora_Estantes",
                (p, n) => p.z < -1.0f ? new Color(0.35f, 0.75f, 1f) : Shelf);
            Part("Estantes", root, shelves);

            // ---- Productos en cúpulas (orden de la tienda) ----
            var glass = Mat("Expendedora_Cristal", () => LizardMaterials.Transparent(new Color(0.85f, 0.95f, 1f, 0.22f), 0.97f));
            var baseIdle = Mat("Expendedora_Base", () => LizardMaterials.Toon(new Color(0.35f, 0.85f, 0.3f)));
            var baseSel = Mat("Expendedora_BaseElegida", () => LizardMaterials.Toon(new Color(1f, 0.9f, 0.3f), 0f, 0.6f));
            var dome = Sdf(p => Mathf.Max(Mathf.Abs((p - Vector3.up * 0.0f).magnitude - 0.36f) - 0.012f, -p.y), new Bounds(new Vector3(0, 0.2f, 0), new Vector3(0.9f, 0.5f, 0.9f)), 0.012f, "Expendedora_Cupula", (p, n) => Color.white);
            var disc = Sdf(p => Mathf.Max(new Vector2(p.x, p.z).magnitude - 0.38f, Mathf.Abs(p.y) - 0.035f) - 0.01f, new Bounds(Vector3.zero, new Vector3(0.9f, 0.2f, 0.9f)), 0.015f, "Expendedora_BaseDisco", (p, n) => Color.white);
            var products = Products();
            var items = new List<Transform>(); var bases = new List<Renderer>();
            for (int i = 0; i < 9; i++)
            {
                int row = i / 3, colm = i % 3;
                Vector3 at = new Vector3(win.x - 1.15f + colm * 1.15f, rows[row], -0.75f);
                var slot = new GameObject("Producto " + (i + 1)).transform; slot.SetParent(root, false); slot.localPosition = at;
                var b = Part("Base", slot, disc, baseIdle); bases.Add(b.GetComponent<Renderer>());
                var item = new GameObject("Artículo").transform; item.SetParent(slot, false); item.localPosition = Vector3.up * 0.04f;
                Part("Malla", item, products[i]);
                items.Add(item);
                var dm = Part("Cúpula", slot, dome, glass); dm.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var pane = Sdf(p => Box(p, Vector3.zero, new Vector3(winH.x - 0.05f, winH.y - 0.05f, 0.02f), 0.02f), new Bounds(Vector3.zero, new Vector3(winH.x * 2f, winH.y * 2f, 0.2f)), 0.04f, "Expendedora_Vidrio", (p, n) => Color.white);
            var pg = Part("Vidrio", root, pane, glass); pg.transform.localPosition = new Vector3(win.x, win.y, -D + 0.02f);
            pg.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var display = root.gameObject.AddComponent<VendingDisplay>();
            display.items = items.ToArray(); display.bases = bases.ToArray(); display.baseIdle = baseIdle; display.baseSelected = baseSel;

            // ---- Panel derecho: ojos de rana, cruceta y carrito ----
            float px = 1.95f, fz = -D - 0.02f;
            var eyes = Sdf(p =>
            {
                float lid = Mathf.Max(SdfMesher.Ellipsoid(p, new Vector3(0, -0.05f, 0), new Vector3(0.62f, 0.3f, 0.12f)), -p.y - 0.12f);
                float e = Mathf.Min((p - new Vector3(-0.27f, 0.14f, -0.05f)).magnitude - 0.21f, (p - new Vector3(0.27f, 0.14f, -0.05f)).magnitude - 0.21f);
                return SdfMesher.SMin(lid, e, 0.05f);
            }, new Bounds(new Vector3(0, 0.05f, 0), new Vector3(1.5f, 0.9f, 0.5f)), 0.015f, "Expendedora_Ojos", (p, n) =>
            {
                foreach (float sx in new[] { -0.27f, 0.27f })
                {
                    Vector3 c = new Vector3(sx, 0.14f, -0.05f);
                    if ((p - c).magnitude < 0.23f && p.z < -0.1f)
                    {
                        if ((new Vector2(p.x - sx - 0.04f, p.y - 0.16f)).magnitude < 0.09f) return Ink;
                        return Color.white;
                    }
                }
                return new Color(0.3f, 0.75f, 0.25f);
            });
            var eg = Part("Ojos de rana", root, eyes); eg.transform.localPosition = new Vector3(px, 3.85f, fz);

            Mesh Tile(string name, Color c, System.Func<Vector3, bool> icon) => Sdf(p => Box(p, Vector3.zero, new Vector3(0.24f, 0.24f, 0.08f), 0.06f),
                new Bounds(Vector3.zero, new Vector3(0.7f, 0.7f, 0.3f)), 0.012f, name, (p, n) => p.z < -0.06f && icon(p) ? Ink : c);
            bool Arrow(Vector3 p) { return p.y < 0.11f && p.y > -0.07f && Mathf.Abs(p.x) < (0.11f - p.y) * 0.8f; } // triángulo hacia arriba
            var arrow = Tile("Expendedora_Flecha", Yellow, Arrow);
            var center = Sdf(p => Box(p, Vector3.zero, new Vector3(0.2f, 0.2f, 0.07f), 0.04f), new Bounds(Vector3.zero, new Vector3(0.6f, 0.6f, 0.3f)), 0.015f, "Expendedora_Centro", (p, n) => Yellow);
            var dpad = new GameObject("Cruceta").transform; dpad.SetParent(root, false); dpad.localPosition = new Vector3(px, 2.55f, fz);
            Part("Centro", dpad, center);
            // dirección, ángulo del icono, acción en la tienda (← → de uno en uno; ↑ ↓ de fila en fila)
            var dirs = new (Vector3 off, float rot, int action, string name)[] { (Vector3.up * 0.5f, 0f, -3, "Arriba"), (Vector3.down * 0.5f, 180f, 3, "Abajo"), (Vector3.left * 0.5f, 90f, -1, "Izquierda"), (Vector3.right * 0.5f, -90f, 1, "Derecha") };
            foreach (var d in dirs)
            {
                var b = Part("Botón " + d.name, dpad, arrow); b.transform.localPosition = d.off; b.transform.localRotation = Quaternion.Euler(0, 0, d.rot);
                AddButton(b, shop, d.action);
            }
            var cart = Sdf(p => Box(p, Vector3.zero, new Vector3(0.48f, 0.3f, 0.09f), 0.07f), new Bounds(Vector3.zero, new Vector3(1.2f, 0.8f, 0.3f)), 0.012f, "Expendedora_Carrito", (p, n) =>
            {
                if (p.z > -0.07f) return GreenBtn;
                // icono de carrito: cesta trapezoidal, asa y dos ruedas
                bool basket = p.y > -0.08f && p.y < 0.12f && Mathf.Abs(p.x + 0.02f) < 0.18f - (0.12f - p.y) * 0.3f && !(Mathf.Abs(p.x + 0.02f) < 0.13f - (0.12f - p.y) * 0.3f && p.y > -0.04f && p.y < 0.08f);
                bool handle = p.x < -0.18f && p.x > -0.3f && Mathf.Abs(p.y - 0.15f) < 0.025f;
                bool wheels = (new Vector2(p.x + 0.12f, p.y + 0.15f)).magnitude < 0.045f || (new Vector2(p.x - 0.1f, p.y + 0.15f)).magnitude < 0.045f;
                return basket || handle || wheels ? Color.white : GreenBtn;
            });
            var cb = Part("Botón comprar", root, cart); cb.transform.localPosition = new Vector3(px, 1.2f, fz);
            AddButton(cb, shop, 0);

            // Bandeja de recogida bajo la vitrina (donde cae la cápsula comprada).
            var tray = Sdf(p => SMax(Box(p, Vector3.zero, new Vector3(0.9f, 0.18f, 0.25f), 0.08f), -Box(p, new Vector3(0, 0.05f, -0.1f), new Vector3(0.78f, 0.14f, 0.25f), 0.05f), 0.03f),
                new Bounds(Vector3.zero, new Vector3(2.2f, 0.7f, 0.8f)), 0.02f, "Expendedora_Bandeja", (p, n) => p.y > 0.0f && n.y > 0.5f ? Ink : RedDark);
            var tg = Part("Bandeja de entrega", root, tray); tg.transform.localPosition = new Vector3(-0.62f, 0.3f, -D - 0.12f);

            // Cartel con el producto elegido, sobre la máquina.
            foreach (Transform t in lobby) if (t.name == "Cartel / EXPENDEDORA") t.position = root.position + new Vector3(0f, 5.75f, -0.3f);

            EditorSceneManager.MarkSceneDirty(l.scene);
            EditorSceneManager.SaveScene(l.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Expendedora nueva creada.");
            return "[Lizards] Expendedora nueva creada.";
        }

        static void AddButton(GameObject b, SwampLobbyExpansion shop, int action)
        {
            var bc = b.AddComponent<BoxCollider>(); bc.size = new Vector3(0.55f, 0.55f, 0.25f);
            if (action == 0) bc.size = new Vector3(1.0f, 0.65f, 0.25f);
            b.AddComponent<TongueTarget>();
            var vb = b.AddComponent<SwampVendorButton>(); vb.vendor = shop; vb.action = action;
        }

        /// <summary>Los 9 productos, en el orden de SwampLobbyExpansion.Names.</summary>
        static Mesh[] Products()
        {
            var m = new Mesh[9];
            // 1. Pelota rebotadora: roja con bultitos.
            m[0] = Sdf(p => (p - Vector3.up * 0.2f).magnitude - 0.19f - Mathf.Abs(Mathf.Sin(p.x * 30f) * Mathf.Sin(p.y * 30f) * Mathf.Sin(p.z * 30f)) * 0.02f,
                new Bounds(Vector3.up * 0.2f, Vector3.one * 0.5f), 0.012f, "Producto_Pelota", (p, n) => Color.Lerp(new Color(0.8f, 0.1f, 0.1f), new Color(1f, 0.3f, 0.25f), n.y * 0.5f + 0.5f));
            // 2. Granada: verde oscuro con gajos y anilla rosa.
            m[1] = Sdf(p => Mathf.Min(SdfMesher.Ellipsoid(p, Vector3.up * 0.18f, new Vector3(0.13f, 0.17f, 0.13f)) + Mathf.Abs(Mathf.Sin(p.y * 40f)) * 0.008f,
                    Mathf.Min(SdfMesher.Capsule(p, Vector3.up * 0.33f, Vector3.up * 0.38f, 0.05f, 0.05f, out _), new Vector2(new Vector2(p.x - 0.09f, p.y - 0.4f).magnitude - 0.05f, p.z).magnitude - 0.015f)),
                new Bounds(Vector3.up * 0.25f, Vector3.one * 0.5f), 0.01f, "Producto_Granada", (p, n) => p.y > 0.37f && p.x > 0.03f ? new Color(1f, 0.4f, 0.6f) : p.y > 0.32f ? new Color(0.4f, 0.4f, 0.42f) : new Color(0.2f, 0.4f, 0.22f));
            // 3. Rana hinchable.
            m[2] = Sdf(p => SdfMesher.SMin(SdfMesher.Ellipsoid(p, Vector3.up * 0.13f, new Vector3(0.18f, 0.13f, 0.15f)),
                    Mathf.Min((p - new Vector3(-0.08f, 0.27f, -0.04f)).magnitude - 0.06f, (p - new Vector3(0.08f, 0.27f, -0.04f)).magnitude - 0.06f), 0.04f),
                new Bounds(Vector3.up * 0.18f, Vector3.one * 0.5f), 0.01f, "Producto_Rana", (p, n) => p.y > 0.26f && p.z < -0.08f ? (Mathf.Abs(Mathf.Abs(p.x) - 0.08f) < 0.025f ? Ink : Color.white) : new Color(0.4f, 0.85f, 0.3f));
            // 4. Bolsa paracaídas: saco blanco con orejas y carita.
            m[3] = Sdf(p => SdfMesher.SMin(SdfMesher.Ellipsoid(p, Vector3.up * 0.16f, new Vector3(0.14f, 0.17f, 0.12f)),
                    Mathf.Min(SdfMesher.Capsule(p, new Vector3(-0.06f, 0.3f, 0), new Vector3(-0.08f, 0.42f, 0), 0.03f, 0.025f, out _), SdfMesher.Capsule(p, new Vector3(0.06f, 0.3f, 0), new Vector3(0.08f, 0.42f, 0), 0.03f, 0.025f, out _)), 0.03f),
                new Bounds(Vector3.up * 0.22f, Vector3.one * 0.5f), 0.01f, "Producto_Bolsa", (p, n) => p.z < -0.09f && p.y > 0.12f && p.y < 0.24f ? (Mathf.Abs(Mathf.Abs(p.x) - 0.04f) < 0.015f && p.y > 0.18f ? Ink : new Color(0.4f, 0.8f, 0.4f)) : new Color(0.96f, 0.96f, 0.95f));
            // 5. Palo selfie.
            m[4] = Sdf(p => Mathf.Min(SdfMesher.Capsule(p, new Vector3(-0.17f, 0.04f, 0), new Vector3(0.15f, 0.36f, 0), 0.018f, 0.014f, out _), Box(p, new Vector3(0.17f, 0.4f, 0), new Vector3(0.04f, 0.06f, 0.012f), 0.01f)),
                new Bounds(Vector3.up * 0.22f, Vector3.one * 0.6f), 0.008f, "Producto_Palo", (p, n) => new Color(0.15f, 0.15f, 0.18f));
            // 6. Cama elástica en miniatura.
            m[5] = Sdf(p =>
            {
                float top = Mathf.Max(new Vector2(p.x, p.z).magnitude - 0.18f, Mathf.Abs(p.y - 0.14f) - 0.02f);
                float rim = new Vector2(new Vector2(p.x, p.z).magnitude - 0.19f, p.y - 0.14f).magnitude - 0.02f;
                float legs = float.MaxValue; for (int k = 0; k < 4; k++) { float a = k * 1.57f + 0.78f; legs = Mathf.Min(legs, SdfMesher.Capsule(p, new Vector3(Mathf.Cos(a) * 0.15f, 0.13f, Mathf.Sin(a) * 0.15f), new Vector3(Mathf.Cos(a) * 0.19f, 0f, Mathf.Sin(a) * 0.19f), 0.012f, 0.012f, out _)); }
                return Mathf.Min(Mathf.Min(top, rim), legs);
            }, new Bounds(Vector3.up * 0.1f, Vector3.one * 0.5f), 0.008f, "Producto_Cama", (p, n) => new Vector2(p.x, p.z).magnitude > 0.17f ? new Color(0.5f, 0.5f, 0.55f) : new Color(0.1f, 0.1f, 0.12f));
            // 7-9. Chicles envueltos (amarillo, verde y rojo) con rayas.
            Color[] candy = { new(1f, 0.82f, 0.2f), new(0.35f, 0.8f, 0.4f), new(0.95f, 0.3f, 0.35f) };
            string[] cn = { "Producto_ChicleBarriga", "Producto_ChicleLengua", "Producto_ChicleCafeina" };
            for (int k = 0; k < 3; k++)
            {
                Color cc = candy[k];
                m[6 + k] = Sdf(p =>
                {
                    float body = SdfMesher.Ellipsoid(p, Vector3.up * 0.15f, new Vector3(0.14f, 0.09f, 0.09f));
                    float ends = Mathf.Min(SdfMesher.Capsule(p, new Vector3(-0.14f, 0.15f, 0), new Vector3(-0.24f, 0.15f, 0), 0.02f, 0.07f, out _), SdfMesher.Capsule(p, new Vector3(0.14f, 0.15f, 0), new Vector3(0.24f, 0.15f, 0), 0.02f, 0.07f, out _));
                    return SdfMesher.SMin(body, ends, 0.03f);
                }, new Bounds(Vector3.up * 0.15f, new Vector3(0.6f, 0.35f, 0.35f)), 0.008f, cn[k], (p, n) => Mathf.Abs(p.x) > 0.16f ? Color.Lerp(cc, Color.white, 0.3f) : (Mathf.Repeat(p.x * 14f + p.y * 6f, 1f) < 0.5f ? cc : Color.white));
            }
            return m;
        }
    }
}
