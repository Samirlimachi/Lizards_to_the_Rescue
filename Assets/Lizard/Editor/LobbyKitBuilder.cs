using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Aplicar kit del pantano y estaciones (hojas de diseño).
    /// Estanque de aparición nadable, cabina telefónica, rocas facetadas con musgo, sauces, hongos rojos, pastos,
    /// muro de fábrica, arco de alcantarilla, rampa de mantenimiento y carrito.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        [MenuItem("Lizards/Aplicar kit del pantano y estaciones (hojas de diseño)", priority = 14)]
        public static string BuildKit()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Lizard/Models", "Lobby");
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset");
            outlineMat = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/Rotulo_Bangers.mat");
            string log = SwimPond() + PhoneBooth() + AngularRocks() + WillowsV2() + MushroomClusters() + GrassKit() + FactoryWall() + SewerArch() + RampAndCart();
            EditorSceneManager.MarkSceneDirty(l.scene);
            EditorSceneManager.SaveScene(l.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Kit aplicado:" + log);
            return log;
        }

        // Malla facetada (estilo low-poly pintado): cada triángulo con sus propios vértices y normal plana.
        static Mesh Facet(Mesh m)
        {
            var v = m.vertices; var c = m.colors; var tri = m.triangles;
            var nv = new Vector3[tri.Length]; var nc = new Color[tri.Length]; var nt = new int[tri.Length];
            for (int i = 0; i < tri.Length; i++) { nv[i] = v[tri[i]]; nc[i] = c.Length > 0 ? c[tri[i]] : Color.white; nt[i] = i; }
            m.Clear(); m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = nv; m.colors = nc; m.triangles = nt; m.RecalculateNormals(); m.RecalculateBounds();
            EditorUtility.SetDirty(m);
            return m;
        }

        static Vector3 Ground(Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, 60f, p.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.name.StartsWith("Terreno")) return new Vector3(p.x, h.point.y, p.z);
            return new Vector3(p.x, 0f, p.z);
        }

        static Mesh LeafyPlant(string name, int seed)
        {
            return Sdf(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 1.05f + seed; Vector3 dir = new Vector3(Mathf.Cos(a), 0.7f, Mathf.Sin(a));
                    d = SdfMesher.SMin(d, SdfMesher.Ellipsoid(Quaternion.Inverse(Quaternion.LookRotation(dir)) * (p - dir * 0.2f), Vector3.zero, new Vector3(0.1f, 0.03f, 0.24f)), 0.03f);
                }
                return d;
            }, new Bounds(new Vector3(0, 0.2f, 0), new Vector3(0.9f, 0.6f, 0.9f)), 0.015f, name, (p, n) => Color.Lerp(new Color(0.22f, 0.55f, 0.2f), new Color(0.45f, 0.85f, 0.3f), Mathf.Clamp01(p.y * 3f)));
        }

        // ================= 1. Estanque de aparición nadable =================

        static string SwimPond()
        {
            var basin = GameObject.Find("Piscina terracota");
            if (!basin) return "";
            Vector3 c = basin.transform.position;
            var oldCol = basin.GetComponent<Collider>(); if (oldCol) oldCol.enabled = false;
            basin.GetComponent<MeshRenderer>().enabled = false;
            const float RimR = 4.65f, WaterY = 1.2f, RimTop = 1.45f;
            var root = Fresh("Estanque de aparición (generado)", c);

            // Borde de bloques rojizos (9 tramos con juntas), por fuera más oscuro.
            var rim = Sdf(p =>
            {
                float rr = new Vector2(p.x, p.z).magnitude, a = Mathf.Atan2(p.z, p.x);
                Vector2 q = new Vector2(Mathf.Abs(rr - RimR) - 0.5f, Mathf.Abs(p.y - RimTop * 0.5f) - RimTop * 0.5f) + Vector2.one * 0.28f;
                float d = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 0.28f;
                float seam = Mathf.Abs(Mathf.Repeat(a / (Mathf.PI * 2f) * 9f + 0.5f, 1f) - 0.5f) * (Mathf.PI * 2f / 9f) * rr;
                return SMax(d, -(seam - 0.04f), 0.06f) - 0f;
            }, new Bounds(new Vector3(0, RimTop * 0.5f, 0), new Vector3(RimR * 2f + 1.6f, RimTop + 0.4f, RimR * 2f + 1.6f)), 0.05f, "Estanque2_Borde", (p, n) =>
            {
                Color coral = new Color(0.86f, 0.36f, 0.34f);
                Color c0 = Color.Lerp(coral * 0.82f, coral * 1.08f, Mathf.Clamp01(n.y * 0.5f + 0.55f));
                return Color.Lerp(c0, c0 * 1.05f, N(p, 2f));
            });
            Part("Borde", root, rim).AddComponent<MeshCollider>();
            // Espuma blanca en la orilla interior.
            var foam = Sdf(p => new Vector2(new Vector2(p.x, p.z).magnitude - (RimR - 0.6f), (p.y - WaterY) * 2.2f).magnitude - 0.13f - Mathf.Sin(Mathf.Atan2(p.z, p.x) * 23f) * 0.02f,
                new Bounds(new Vector3(0, WaterY, 0), new Vector3(RimR * 2f, 0.6f, RimR * 2f)), 0.03f, "Estanque2_Espuma", (p, n) => new Color(0.95f, 1f, 0.95f));
            Part("Espuma", root, foam);
            // Agua verde brillante, nadable.
            var water = Sdf(p => Mathf.Max(new Vector2(p.x, p.z).magnitude - (RimR - 0.5f), Mathf.Abs(p.y) - 0.06f), new Bounds(Vector3.zero, new Vector3(RimR * 2f, 0.4f, RimR * 2f)), 0.08f, "Estanque2_Agua", (p, n) => Color.white);
            var waterMat2 = Mat("Estanque2_AguaMat", () => LizardMaterials.Transparent(new Color(0.62f, 0.98f, 0.7f, 0.55f), 0.92f));
            var wg = Part("Agua", root, water, waterMat2); wg.transform.localPosition = Vector3.up * WaterY;
            wg.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var wf = wg.AddComponent<WaterFlow>(); wf.kind = WaterFlow.Kind.Surface; wf.amplitude = 0.025f; wf.frequency = 2.2f; wf.speed = 1.5f;
            var zone = root.gameObject.AddComponent<SwimZone>(); zone.radius = RimR - 0.55f; zone.surfaceY = WaterY; zone.depth = 3f;
            zone.rippleMat = Mat("Estanque2_Onda", () => LizardMaterials.Transparent(new Color(1f, 1f, 1f, 0.6f), 0.5f));

            // Nenúfar (donde apareces), sobre el agua.
            var padMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Estanque_Nenufar.asset");
            if (padMesh) { var lp = Part("Nenúfar", root, padMesh); lp.transform.localPosition = Vector3.up * (WaterY + 0.06f); }
            var padCol = GameObject.Find("Soporte del nenúfar"); if (padCol) { var pp = padCol.transform.position; padCol.transform.position = new Vector3(pp.x, c.y + WaterY - 0.02f, pp.z); }
            foreach (var hub in Object.FindObjectsByType<SwampLobbyHub>()) { hub.spawn = c + Vector3.up * (WaterY + 0.45f); if (hub.roomMenu) hub.roomMenu.spawnPoint = hub.spawn; EditorUtility.SetDirty(hub); }

            // Juncos, planta de bayas y piedras con matas fuera.
            var reeds = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Estanque_Juncos.asset");
            for (int i = 0; i < 3 && reeds; i++) { float a = 0.5f + i * 0.5f; var r = Part("Juncos", root, reeds); r.transform.localPosition = new Vector3(Mathf.Cos(a) * (RimR - 0.9f), WaterY - 0.3f, Mathf.Sin(a) * (RimR - 0.9f)); }
            var berry = BerryPlant("Kit_Bayas");
            var bp = Part("Bayas", root, berry); bp.transform.localPosition = new Vector3(Mathf.Cos(0.9f) * (RimR - 0.4f), RimTop - 0.05f, Mathf.Sin(0.9f) * (RimR - 0.4f));
            var leafy = LeafyPlant("Kit_Matita", 0); var rockM = RockVariant(5);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.05f + 0.3f; Vector3 o = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (RimR + 0.75f);
                var lf = Part("Matita", root, leafy); lf.transform.position = Ground(c + o); lf.transform.localScale = Vector3.one * 1.3f;
                if (i % 2 == 0) { var rk = Part("Piedrita", root, rockM); rk.transform.position = Ground(c + o * 1.08f) + Vector3.up * 0.15f; rk.transform.localScale = Vector3.one * 0.45f; rk.transform.rotation = Quaternion.Euler(0, i * 50f, 0); }
            }
            // Escalones de piedra para subir al borde.
            for (int i = 0; i < 2; i++)
            {
                var st = Part("Escalón", root, rockM); st.transform.position = Ground(c + new Vector3(0, 0, RimR + 0.7f + i * 0.85f)) + Vector3.up * (0.2f - i * 0.15f);
                st.transform.localScale = new Vector3(1.4f, 0.9f - i * 0.4f, 0.9f);
                st.AddComponent<MeshCollider>().sharedMesh = rockM;
            }
            return " estanque nadable;";
        }

        static Mesh BerryPlant(string name)
        {
            return Sdf(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.25f; Vector3 top = new Vector3(Mathf.Cos(a) * 0.18f, 0.55f + (i % 2) * 0.15f, Mathf.Sin(a) * 0.18f);
                    d = Mathf.Min(d, SdfMesher.Capsule(p, Vector3.zero, top, 0.02f, 0.015f, out _));
                    d = Mathf.Min(d, (p - top).magnitude - 0.06f);
                    d = Mathf.Min(d, (p - (top * 0.8f + new Vector3(0.06f, 0, 0))).magnitude - 0.05f);
                }
                d = SdfMesher.SMin(d, SdfMesher.Ellipsoid(p, new Vector3(0, 0.08f, 0), new Vector3(0.22f, 0.08f, 0.22f)), 0.05f);
                return d;
            }, new Bounds(new Vector3(0, 0.35f, 0), new Vector3(0.7f, 0.9f, 0.7f)), 0.012f, name, (p, n) => p.y > 0.38f && new Vector2(p.x, p.z).magnitude > 0.05f && n.y > -0.5f && p.y > 0.4f ? new Color(0.2f, 0.3f, 0.75f) : new Color(0.3f, 0.65f, 0.25f));
        }

        // ================= 2. Cabina telefónica =================

        static string PhoneBooth()
        {
            var booth = GameObject.Find("Cabina telefónica violeta");
            if (!booth) return "";
            for (int i = booth.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(booth.transform.GetChild(i).gameObject);
            foreach (var cc in booth.GetComponents<Collider>()) Object.DestroyImmediate(cc);
            foreach (Transform t in lobby) if (t.name == "Cartel / SALA") t.gameObject.SetActive(false);
            Color violet = new Color(0.33f, 0.24f, 0.75f), violetD = new Color(0.25f, 0.18f, 0.6f), lav = new Color(0.78f, 0.6f, 1f), win = new Color(0.82f, 0.86f, 0.95f);
            System.Func<Vector3, float> body = p =>
            {
                float shell = Box(p, new Vector3(0, 1.85f, 0), new Vector3(0.95f, 1.6f, 0.8f), 0.14f);
                float cap = Box(p, new Vector3(0, 3.5f, 0), new Vector3(1.05f, 0.12f, 0.9f), 0.08f);
                float hollow = Box(p, new Vector3(0, 1.75f, -0.3f), new Vector3(0.78f, 1.45f, 0.8f), 0.06f);    // abierta por delante
                float d = Mathf.Min(SMax(shell, -hollow, 0.04f), cap);
                // Ventanitas en rejilla a los lados (huecos poco profundos).
                for (int s = -1; s <= 1; s += 2)
                    for (int r = 0; r < 4; r++) for (int k = 0; k < 3; k++)
                            d = SMax(d, -Box(p, new Vector3(s * 0.96f, 1.2f + r * 0.5f, -0.45f + k * 0.45f), new Vector3(0.05f, 0.17f, 0.15f), 0.03f), 0.02f);
                // Puerta trasera con rejilla.
                d = Mathf.Min(d, Box(p, new Vector3(0, 1.6f, 0.82f), new Vector3(0.6f, 1.25f, 0.03f), 0.03f));
                d = Mathf.Min(d, Box(p, new Vector3(0, 0.12f, 0), new Vector3(1.25f, 0.12f, 1.1f), 0.05f)); // base de cemento
                return d;
            };
            var mesh = Sdf(body, new Bounds(new Vector3(0, 1.8f, 0), new Vector3(2.8f, 3.9f, 2.6f)), 0.03f, "Cabina_Cuerpo", (p, n) =>
            {
                if (p.y < 0.26f) return new Color(0.55f, 0.56f, 0.6f);
                if (Mathf.Abs(p.x) > 0.9f && Mathf.Abs(p.x) < 0.94f && p.y > 1f && p.y < 3f) return win;                 // vidrio de ventanitas
                if (p.z > 0.84f && p.y < 0.75f && p.y > 0.45f && Mathf.Repeat(p.y * 12f, 1f) < 0.5f) return violetD * 0.7f; // rejilla
                if (Mathf.Abs(p.x) < 0.78f && p.z < 0.45f && p.y < 3.2f && p.y > 0.3f) return new Color(0.28f, 0.26f, 0.38f); // interior
                return Color.Lerp(violetD, violet, Mathf.Clamp01(n.y * 0.4f + 0.7f));
            });
            Part("Cabina", booth.transform, mesh);
            // Franja lavanda luminosa con el rótulo.
            var strip = Sdf(p => Box(p, Vector3.zero, new Vector3(0.6f, 0.15f, 0.04f), 0.04f), new Bounds(Vector3.zero, new Vector3(1.5f, 0.5f, 0.3f)), 0.02f, "Cabina_Franja", (p, n) => lav);
            var sg = Part("Franja", booth.transform, strip); sg.transform.localPosition = new Vector3(0, 3.15f, -0.84f);
            var label = new GameObject("Rótulo").AddComponent<TextMeshPro>(); label.transform.SetParent(sg.transform, false);
            label.transform.localPosition = new Vector3(0, 0, -0.05f); label.rectTransform.sizeDelta = new Vector2(1.15f, 0.28f);
            if (font) label.font = font; if (outlineMat) label.fontSharedMaterial = outlineMat;
            label.text = "SALA ONLINE"; label.alignment = TextAlignmentOptions.Center; label.enableAutoSizing = true; label.fontSizeMin = 0.3f; label.fontSizeMax = 3f; label.color = Color.white;
            // Teléfono de pared con auricular rojo (dentro, al fondo) y otro auricular en el lateral.
            var phone = Sdf(p =>
            {
                float boxD = Box(p, Vector3.zero, new Vector3(0.22f, 0.32f, 0.08f), 0.04f);
                float rec = SdfMesher.Capsule(p, new Vector3(-0.32f, 0.22f, -0.06f), new Vector3(-0.32f, -0.22f, -0.06f), 0.05f, 0.05f, out _);
                rec = Mathf.Min(rec, (p - new Vector3(-0.32f, 0.25f, -0.06f)).magnitude - 0.08f); rec = Mathf.Min(rec, (p - new Vector3(-0.32f, -0.25f, -0.06f)).magnitude - 0.08f);
                return Mathf.Min(boxD, rec);
            }, new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.4f)), 0.012f, "Cabina_Telefono", (p, n) =>
            {
                if (p.x < -0.22f) return new Color(0.9f, 0.15f, 0.15f);
                if (p.z < -0.07f && Mathf.Repeat(p.x * 9f, 1f) < 0.6f && Mathf.Repeat(p.y * 9f, 1f) < 0.6f && p.y < 0.05f) return new Color(0.75f, 0.75f, 0.8f); // teclas
                if (p.z < -0.07f && p.y > 0.12f) return new Color(0.55f, 0.45f, 0.9f);                                                               // pantallita
                return new Color(0.25f, 0.25f, 0.3f);
            });
            var ph = Part("Teléfono", booth.transform, phone); ph.transform.localPosition = new Vector3(0.1f, 1.85f, 0.38f);
            var side = Part("Auricular lateral", booth.transform, phone); side.transform.localPosition = new Vector3(-1.02f, 1.6f, -0.2f); side.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            // Letrero redondo con icono de teléfono encima.
            var disc = Sdf(p =>
            {
                float d = Mathf.Max(new Vector2(p.x, p.y).magnitude - 0.55f, Mathf.Abs(p.z) - 0.07f) - 0.03f;
                float post = Box(p, new Vector3(0, -0.72f, 0.05f), new Vector3(0.12f, 0.2f, 0.07f), 0.03f);
                return Mathf.Min(d, post);
            }, new Bounds(new Vector3(0, -0.2f, 0), new Vector3(1.4f, 1.8f, 0.4f)), 0.015f, "Cabina_Letrero", (p, n) =>
            {
                float r = new Vector2(p.x, p.y).magnitude;
                if (p.y < -0.55f) return new Color(0.15f, 0.15f, 0.25f);
                if (r > 0.47f) return new Color(0.15f, 0.15f, 0.25f);
                if (p.z < -0.06f)
                {
                    // Icono de auricular: arco grueso inclinado.
                    Vector2 q = new Vector2(p.x, p.y); q = new Vector2(q.x * 0.7f - q.y * 0.7f, q.x * 0.7f + q.y * 0.7f);
                    bool handle = Mathf.Abs(q.y + 0.05f) < 0.07f && Mathf.Abs(q.x) < 0.22f;
                    bool ends = (new Vector2(q.x - 0.22f, q.y + 0.02f)).magnitude < 0.1f || (new Vector2(q.x + 0.22f, q.y + 0.02f)).magnitude < 0.1f;
                    if (handle || ends) return new Color(0.15f, 0.15f, 0.25f);
                }
                return new Color(0.96f, 0.96f, 0.98f);
            });
            var dg = Part("Letrero", booth.transform, disc); dg.transform.localPosition = new Vector3(0, 4.45f, 0);
            // Plantitas en la base.
            var leafy = LeafyPlant("Kit_Matita", 0);
            foreach (var o in new[] { new Vector3(-1.2f, 0.2f, -1f), new Vector3(1.25f, 0.2f, -0.9f), new Vector3(1.2f, 0.2f, 1f) }) { var lf = Part("Matita", booth.transform, leafy); lf.transform.localPosition = o; }
            // Colisión: paredes y fondo (se puede entrar por delante).
            void Wall(Vector3 c, Vector3 s) { var b = booth.AddComponent<BoxCollider>(); b.center = c; b.size = s; }
            Wall(new Vector3(-0.88f, 1.8f, 0), new Vector3(0.2f, 3.4f, 1.7f)); Wall(new Vector3(0.88f, 1.8f, 0), new Vector3(0.2f, 3.4f, 1.7f));
            Wall(new Vector3(0, 1.8f, 0.75f), new Vector3(1.9f, 3.4f, 0.2f)); Wall(new Vector3(0, 3.5f, 0), new Vector3(2.1f, 0.3f, 1.8f));
            return " cabina telefónica;";
        }

        // ================= 3. Rocas facetadas con musgo =================

        static Mesh RockVariant(int seed)
        {
            string name = "Kit_Roca_" + seed;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/{name}.asset");
            if (existing && existing.vertexCount > 0 && rocksBuilt.Contains(seed)) return existing;
            rocksBuilt.Add(seed);
            var rnd = new System.Random(seed * 97 + 13);
            var planes = new List<Vector4>();
            for (int i = 0; i < 11; i++)
            {
                var n = new Vector3((float)rnd.NextDouble() * 2f - 1f, (float)rnd.NextDouble() * 1.4f - 0.3f, (float)rnd.NextDouble() * 2f - 1f).normalized;
                planes.Add(new Vector4(n.x, n.y, n.z, 0.38f + (float)rnd.NextDouble() * 0.12f));
            }
            planes.Add(new Vector4(0, -1, 0, 0.5f));
            var mesh = Sdf(p =>
            {
                float d = -9f;
                foreach (var pl in planes) d = Mathf.Max(d, p.x * pl.x + p.y * pl.y + p.z * pl.z - pl.w);
                return d;
            }, new Bounds(Vector3.zero, Vector3.one * 1.4f), 0.045f, name, (p, n) =>
            {
                Color stone = Color.Lerp(new Color(0.78f, 0.74f, 0.64f), new Color(0.9f, 0.87f, 0.78f), Mathf.Abs(Mathf.Sin(n.x * 5f + n.z * 3f)) * 0.6f);
                float drip = Mathf.Sin(Mathf.Atan2(p.z, p.x) * 9f + seed) * 0.08f;
                if (n.y > 0.55f || (n.y > 0.2f && p.y > 0.2f + drip)) return Color.Lerp(new Color(0.35f, 0.75f, 0.25f), new Color(0.45f, 0.88f, 0.3f), n.y);
                return stone;
            });
            return Facet(mesh);
        }
        static readonly HashSet<int> rocksBuilt = new();

        static string AngularRocks()
        {
            rocksBuilt.Clear();
            var variants = new[] { RockVariant(1), RockVariant(2), RockVariant(3) };
            int n = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>())
            {
                if (!mf || !mf.sharedMesh) continue;
                string nm = mf.name;
                if (nm != "Roca esculpida" && nm != "Roca de orilla" && !(nm == "Roca" && mf.transform.parent && mf.transform.parent.name == "Tubería con cascada")) continue;
                if (mf.sharedMesh.name.StartsWith("Kit_Roca_")) continue; // ya convertida
                var t = mf.transform;
                Bounds ob = mf.sharedMesh.bounds;
                var nmesh = variants[n % 3];
                Bounds nb = nmesh.bounds;
                // Encaja la roca nueva en el mismo volumen que ocupaba la vieja.
                t.position = t.TransformPoint(ob.center) - t.rotation * Vector3.Scale(t.localScale, Vector3.Scale(nb.center, new Vector3(ob.size.x / nb.size.x, ob.size.y / nb.size.y, ob.size.z / nb.size.z)));
                t.localScale = Vector3.Scale(t.localScale, new Vector3(ob.size.x / nb.size.x, ob.size.y / nb.size.y, ob.size.z / nb.size.z));
                mf.sharedMesh = nmesh;
                mf.GetComponent<MeshRenderer>().sharedMaterial = vtx;
                var mc = mf.GetComponent<MeshCollider>(); if (mc) mc.sharedMesh = nmesh;
                n++;
            }
            return $" {n} rocas facetadas;";
        }

        // ================= 4. Sauces =================

        static string WillowsV2()
        {
            var trunk = Sdf(p =>
            {
                float d = SdfMesher.Capsule(p, Vector3.zero, new Vector3(0, 4.6f, 0), 0.85f, 0.55f, out _);
                for (int r = 0; r < 5; r++)
                {
                    float a = r * 1.257f;
                    d = SdfMesher.SMin(d, SdfMesher.Capsule(p, new Vector3(0, 0.9f, 0), new Vector3(Mathf.Cos(a) * 1.4f, -0.1f, Mathf.Sin(a) * 1.4f), 0.45f, 0.18f, out _), 0.35f);
                }
                return Mathf.Max(d, -0.2f - p.y);
            }, new Bounds(new Vector3(0, 2.3f, 0), new Vector3(4f, 5.4f, 4f)), 0.05f, "Kit_Sauce_Tronco", (p, n) =>
                Color.Lerp(new Color(0.5f, 0.27f, 0.17f), new Color(0.66f, 0.38f, 0.24f), Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.z, p.x) * 7f + p.y * 0.8f))));
            var canopy = Sdf(p =>
            {
                float d = Mathf.Max(SdfMesher.Ellipsoid(p, new Vector3(0, 5.9f, 0), new Vector3(3.2f, 1.9f, 3.2f)), 4.9f - p.y);
                for (int i = 0; i < 26; i++)
                {
                    float a = i / 26f * Mathf.PI * 2f; Vector3 dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    float len = 2.0f + (i % 3) * 0.35f;
                    // Tira plana colgante, pegada a la copa, con la punta redondeada.
                    Vector3 q = Quaternion.Inverse(Quaternion.LookRotation(dir)) * (p - dir * 3.0f);
                    q.z += (6.2f - p.y) * 0.08f; // se curva un poco hacia fuera arriba
                    float strip = Box(q, new Vector3(0, 6.0f - len * 0.5f, 0), new Vector3(0.3f, len * 0.5f, 0.1f), 0.1f);
                    d = SdfMesher.SMin(d, strip, 0.08f);
                }
                return d;
            }, new Bounds(new Vector3(0, 5.6f, 0), new Vector3(7.6f, 5f, 7.6f)), 0.06f, "Kit_Sauce_Copa", (p, n) =>
                Color.Lerp(new Color(0.18f, 0.45f, 0.38f), new Color(0.42f, 0.78f, 0.3f), Mathf.Clamp01((p.y - 3.6f) / 3.8f)));
            int count = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                string mn = mf.sharedMesh.name;
                if (mn.StartsWith("Sauce_Tronco") || mn == "Kit_Sauce_Tronco") { mf.sharedMesh = trunk; count++; }
                else if (mn.StartsWith("Sauce_Hojas") || mn == "Kit_Sauce_Copa") mf.sharedMesh = canopy;
            }
            return $" {count} sauces;";
        }

        // ================= 5. Hongos rojos en grupo =================

        static string MushroomClusters()
        {
            Mesh one(string name) => Sdf(p =>
            {
                float d = float.MaxValue;
                var specs = new[] { (Vector3.zero, 1f), (new Vector3(1.1f, 0, 0.4f), 0.45f), (new Vector3(-0.9f, 0, 0.6f), 0.38f), (new Vector3(0.5f, 0, -0.95f), 0.32f), (new Vector3(-0.6f, 0, -0.7f), 0.25f) };
                foreach (var (o, s) in specs)
                {
                    Vector3 q = (p - o) / s;
                    float hex(Vector2 v, float r) { v = new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y)); return Mathf.Max(v.x * 0.866f + v.y * 0.5f, v.y) - r; }
                    float stem = Mathf.Max(hex(new Vector2(q.x, q.z), 0.32f - q.y * 0.03f), Mathf.Max(-q.y, q.y - 1.5f));
                    float cap = Mathf.Max(hex(new Vector2(q.x, q.z), 1.0f + (1.75f - q.y) * 0.25f), Mathf.Abs(q.y - 1.75f) - 0.28f);
                    d = Mathf.Min(d, Mathf.Min(stem, cap) * s);
                }
                return d;
            }, new Bounds(new Vector3(0, 1f, 0), new Vector3(4f, 2.6f, 4f)), 0.035f, name, (p, n) => p.y > 1.4f || (n.y < -0.3f && p.y > 0.6f) ? (n.y < -0.3f ? new Color(0.75f, 0.2f, 0.18f) : new Color(0.92f, 0.28f, 0.22f)) : new Color(0.42f, 0.55f, 0.55f));
            var cluster = Facet(one("Kit_Hongos"));
            int n2 = 0;
            var root = lobby.Find("Setas (generadas)");
            if (root) foreach (Transform t in root) { var mf = t.GetComponent<MeshFilter>(); if (mf) { mf.sharedMesh = cluster; n2++; } }
            return $" {n2} grupos de hongos;";
        }

        // ================= 6. Pastos =================

        static string GrassKit()
        {
            var g1 = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 0.9f; Vector3 dir = new Vector3(Mathf.Cos(a) * 0.5f, 1f, Mathf.Sin(a) * 0.5f).normalized;
                    Vector3 q = Quaternion.Inverse(Quaternion.LookRotation(dir, Vector3.up)) * p;
                    d = Mathf.Min(d, SdfMesher.Ellipsoid(Quaternion.Inverse(Quaternion.FromToRotation(Vector3.up, dir)) * p, dir * 0f + Vector3.up * 0.22f, new Vector3(0.07f, 0.24f, 0.025f)));
                }
                return d;
            }, new Bounds(new Vector3(0, 0.25f, 0), new Vector3(0.8f, 0.6f, 0.8f)), 0.012f, "Kit_Pasto1", (p, n) => Color.Lerp(new Color(0.25f, 0.6f, 0.2f), new Color(0.5f, 0.88f, 0.3f), p.y * 2f));
            var g2 = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 6; i++) { float a = i * 1.1f; Vector3 b = new Vector3(Mathf.Cos(a) * 0.08f, 0, Mathf.Sin(a) * 0.08f); d = Mathf.Min(d, SdfMesher.Capsule(p, b, b * 1.6f + Vector3.up * (0.5f + (i % 3) * 0.12f), 0.035f, 0.012f, out _)); }
                return d;
            }, new Bounds(new Vector3(0, 0.35f, 0), new Vector3(0.5f, 0.8f, 0.5f)), 0.01f, "Kit_Pasto2", (p, n) => Color.Lerp(new Color(0.25f, 0.55f, 0.2f), new Color(0.55f, 0.85f, 0.3f), p.y * 1.6f));
            var g3 = BerryPlant("Kit_Pasto3");
            var g4 = Sdf(p => Mathf.Min(SdfMesher.Ellipsoid(p, new Vector3(-0.12f, 0.04f, 0), new Vector3(0.16f, 0.07f, 0.13f)), SdfMesher.Ellipsoid(p, new Vector3(0.12f, 0.03f, 0.05f), new Vector3(0.14f, 0.06f, 0.12f))),
                new Bounds(new Vector3(0, 0.05f, 0), new Vector3(0.7f, 0.3f, 0.5f)), 0.01f, "Kit_Pasto4", (p, n) => Color.Lerp(new Color(0.3f, 0.7f, 0.25f), new Color(0.5f, 0.9f, 0.35f), n.y));
            var set = new[] { g1, g2, g3, g4 };
            int k = 0;
            foreach (Transform t in lobby) { if (t.name != "Hierba") continue; var mf = t.GetComponent<MeshFilter>(); if (mf) { mf.sharedMesh = set[k % 4]; k++; } }
            return $" {k} pastos;";
        }

        // ================= 7. Muro de fábrica =================

        static string FactoryWall()
        {
            var teal = Mat("Kit_TuboTurquesa", () => LizardMaterials.Toon(new Color(0.15f, 0.62f, 0.6f), 0f, 0.4f));
            var dark = Mat("Kit_MetalOscuro", () => LizardMaterials.Toon(new Color(0.22f, 0.24f, 0.28f), 0f, 0.3f));
            int n = 0;
            foreach (Transform t in lobby)
            {
                if (t.name.StartsWith("Tubería") || t.name.StartsWith("Marco del ventilador")) foreach (var r in t.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = teal; n++; }
                if (t.name.StartsWith("Abrazadera") || t.name.StartsWith("Aspas")) foreach (var r in t.GetComponentsInChildren<Renderer>()) r.sharedMaterial = dark;
            }
            var wall = GameObject.Find("Fachada de la fábrica");
            if (wall)
            {
                var b = wall.GetComponent<Renderer>().bounds;
                var old = lobby.Find("Placas de peligro (generadas)"); if (old) Object.DestroyImmediate(old.gameObject);
                var root = new GameObject("Placas de peligro (generadas)").transform; root.SetParent(lobby, false);
                var plate = Sdf(p => Box(p, Vector3.zero, new Vector3(1.6f, 0.38f, 0.08f), 0.06f) - 0f, new Bounds(Vector3.zero, new Vector3(3.6f, 1f, 0.4f)), 0.02f, "Kit_PlacaPeligro",
                    (p, n2) => Mathf.Abs(p.x) > 1.5f || Mathf.Abs(p.y) > 0.3f ? new Color(0.3f, 0.3f, 0.34f) : (Mathf.Repeat((p.x + p.y) * 1.6f, 1f) < 0.5f ? new Color(1f, 0.75f, 0.15f) : new Color(0.12f, 0.12f, 0.14f)));
                foreach (float x in new[] { -20f, -2f, 17f, 27f })
                {
                    var pg = Part("Placa de peligro", root, plate); pg.transform.position = new Vector3(x, 9.2f, b.min.z - 0.1f);
                }
            }
            return $" muro de fábrica ({n} piezas en turquesa);";
        }

        // ================= 8. Arco de alcantarilla =================

        static string SewerArch()
        {
            var arch = GameObject.Find("Arco de alcantarilla");
            if (!arch) return "";
            Vector3 c = Ground(arch.transform.position);
            arch.GetComponent<MeshRenderer>().enabled = false;
            foreach (Transform t in lobby) if (t.name == "Reja") { var r = t.GetComponent<MeshRenderer>(); if (r) r.enabled = false; }
            var root = Fresh("Arco de alcantarilla (generado)", c);
            const float Rin = 2.8f, Rout = 3.9f;
            var stones = Sdf(p =>
            {
                float rr = new Vector2(p.x, p.y).magnitude, a = Mathf.Atan2(p.y, p.x);
                float ring = Mathf.Max(Mathf.Max(rr - Rout, Rin - rr), Mathf.Max(Mathf.Abs(p.z) - 0.9f, -p.y));
                float seam = Mathf.Abs(Mathf.Repeat(a / Mathf.PI * 9f, 1f) - 0.5f) * (Mathf.PI / 9f) * rr;
                return SMax(ring - 0.06f, -(seam - 0.05f), 0.05f);
            }, new Bounds(new Vector3(0, Rout * 0.5f, 0), new Vector3(Rout * 2f + 0.4f, Rout + 0.4f, 2.2f)), 0.05f, "Kit_Arco", (p, n) =>
            {
                Color s = Color.Lerp(new Color(0.3f, 0.31f, 0.35f), new Color(0.42f, 0.43f, 0.47f), Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.y, p.x) * 9f)));
                float drip = Mathf.Sin(p.x * 3f) * 0.25f;
                if ((n.y > 0.4f && p.y > 1.5f) || (p.y > Rout - 1.1f + drip && n.z < -0.5f)) return new Color(0.4f, 0.82f, 0.28f); // musgo que gotea
                return s;
            });
            var sg = Part("Arco", root, Facet(stones)); sg.AddComponent<MeshCollider>();
            var bars = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int k = -5; k <= 5; k++) { float x = k * 0.48f; float h = Mathf.Sqrt(Mathf.Max(0f, Rin * Rin - x * x)); d = Mathf.Min(d, SdfMesher.Capsule(p, new Vector3(x, 0, 0), new Vector3(x, h - 0.05f, 0), 0.07f, 0.07f, out _)); }
                return d;
            }, new Bounds(new Vector3(0, Rin * 0.5f, 0), new Vector3(Rin * 2f, Rin + 0.3f, 0.4f)), 0.025f, "Kit_Barrotes", (p, n) => new Color(0.15f, 0.15f, 0.18f));
            Part("Barrotes", root, bars).AddComponent<MeshCollider>();
            var hole = Sdf(p => Mathf.Max(Mathf.Max(new Vector2(p.x, p.y).magnitude - Rin, -p.y), Mathf.Abs(p.z) - 0.05f), new Bounds(new Vector3(0, Rin * 0.5f, 0), new Vector3(Rin * 2f, Rin + 0.2f, 0.3f)), 0.05f, "Kit_ArcoFondo", (p, n) => new Color(0.06f, 0.07f, 0.09f));
            var hg = Part("Oscuridad", root, hole); hg.transform.localPosition = new Vector3(0, 0, 0.6f);
            var rock = RockVariant(5);
            foreach (var o in new[] { new Vector3(-Rout - 0.4f, 0.3f, -0.5f), new Vector3(Rout + 0.3f, 0.25f, -0.6f), new Vector3(-Rout + 0.4f, 0.15f, -1.2f) })
            { var r = Part("Piedra", root, rock); r.transform.localPosition = o; r.transform.localScale = Vector3.one * 0.7f; }
            root.rotation = Quaternion.Euler(0, arch.transform.eulerAngles.y, 0);
            return " arco de alcantarilla;";
        }

        // ================= 9. Rampa de mantenimiento y carrito =================

        static string RampAndCart()
        {
            int done = 0;
            foreach (var name in new[] { "Plataforma de mantenimiento", "Rampa de mantenimiento" })
            {
                var g = GameObject.Find(name); if (!g) continue;
                g.GetComponent<MeshRenderer>().enabled = false;
                var old = g.transform.Find("Placas"); if (old) Object.DestroyImmediate(old.gameObject);
                Vector3 size = g.transform.localScale; bool ramp = name.StartsWith("Rampa");
                var mesh = Sdf(p =>
                {
                    float d = Box(p, Vector3.zero, size * 0.5f, 0.05f);
                    // Juntas entre placas cada ~3.6 m y remaches.
                    float seamX = Mathf.Abs(Mathf.Repeat(p.x + size.x * 0.5f, size.x / 3f) - size.x / 6f);
                    return SMax(d, -Mathf.Max(seamX - size.x / 6f + 0.04f, -(p.y - size.y * 0.5f + 0.03f)), 0.02f);
                }, new Bounds(Vector3.zero, size + Vector3.one * 0.3f), 0.04f, ramp ? "Kit_Rampa" : "Kit_Plataforma", (p, n) =>
                {
                    bool top = n.y > 0.6f;
                    float zEdge = ramp ? size.z * 0.5f - p.z : -size.z * 0.5f + 1e9f;
                    if (top && ramp && zEdge < 0.45f) return Mathf.Repeat((p.x + p.z) * 1.4f, 1f) < 0.5f ? new Color(1f, 0.75f, 0.15f) : new Color(0.1f, 0.1f, 0.12f); // franja de peligro arriba
                    if (top && (new Vector2(Mathf.Repeat(p.x, size.x / 3f) - 0.25f, Mathf.Repeat(p.z, 1.2f) - 0.25f)).magnitude < 0.07f) return new Color(0.45f, 0.47f, 0.52f); // remaches
                    return Color.Lerp(new Color(0.16f, 0.17f, 0.2f), new Color(0.24f, 0.25f, 0.29f), top ? 1f : 0.4f);
                });
                var child = Part("Placas", g.transform, mesh);
                child.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f / size.z);
                done++;
            }
            foreach (Transform t in lobby) if (t.name == "Franja de seguridad") { var r = t.GetComponent<MeshRenderer>(); if (r) r.enabled = false; }

            var cart = GameObject.Find("Carrito de provisiones");
            if (cart)
            {
                foreach (var r in cart.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                var old = cart.transform.Find("Carrito (modelo)"); if (old) Object.DestroyImmediate(old.gameObject);
                var mesh = Sdf(p =>
                {
                    // Cesta de rejilla: marco + barras, chasis, asa roja, cuatro ruedas y cartel con rana.
                    float outer = Box(p, new Vector3(0, 0.3f, 0), new Vector3(0.48f, 0.25f, 0.34f), 0.03f);
                    float inner = Box(p, new Vector3(0, 0.38f, 0), new Vector3(0.44f, 0.25f, 0.3f), 0.02f);
                    float shell = Mathf.Max(outer, -inner);
                    float grid = Mathf.Min(Mathf.Abs(Mathf.Repeat(p.x, 0.12f) - 0.06f), Mathf.Abs(Mathf.Repeat(p.y, 0.12f) - 0.06f)) - 0.015f;
                    float basket = Mathf.Max(shell, Mathf.Min(grid, Mathf.Min(Mathf.Abs(p.z) - 0.31f < 0f ? Mathf.Abs(Mathf.Repeat(p.z, 0.12f) - 0.06f) - 0.015f : 9f, 9f)));
                    basket = Mathf.Min(basket, Mathf.Max(shell, Mathf.Abs(p.y - 0.53f) - 0.025f));
                    float chassis = Mathf.Min(SdfMesher.Capsule(p, new Vector3(-0.4f, -0.2f, -0.25f), new Vector3(-0.4f, 0.05f, -0.25f), 0.02f, 0.02f, out _), SdfMesher.Capsule(p, new Vector3(0.4f, -0.2f, 0.25f), new Vector3(0.4f, 0.05f, 0.25f), 0.02f, 0.02f, out _));
                    chassis = Mathf.Min(chassis, Mathf.Min(SdfMesher.Capsule(p, new Vector3(-0.4f, -0.2f, 0.25f), new Vector3(-0.4f, 0.05f, 0.25f), 0.02f, 0.02f, out _), SdfMesher.Capsule(p, new Vector3(0.4f, -0.2f, -0.25f), new Vector3(0.4f, 0.05f, -0.25f), 0.02f, 0.02f, out _)));
                    float handle = SdfMesher.Capsule(p, new Vector3(-0.3f, 0.7f, 0.45f), new Vector3(0.3f, 0.7f, 0.45f), 0.04f, 0.04f, out _);
                    handle = Mathf.Min(handle, Mathf.Min(SdfMesher.Capsule(p, new Vector3(-0.3f, 0.55f, 0.33f), new Vector3(-0.3f, 0.7f, 0.45f), 0.02f, 0.02f, out _), SdfMesher.Capsule(p, new Vector3(0.3f, 0.55f, 0.33f), new Vector3(0.3f, 0.7f, 0.45f), 0.02f, 0.02f, out _)));
                    float wheels = float.MaxValue;
                    foreach (var w in new[] { new Vector3(-0.4f, -0.27f, -0.25f), new Vector3(0.4f, -0.27f, -0.25f), new Vector3(-0.4f, -0.27f, 0.25f), new Vector3(0.4f, -0.27f, 0.25f) })
                        wheels = Mathf.Min(wheels, Mathf.Max(new Vector2(p.y - w.y, p.z - w.z).magnitude - 0.07f, Mathf.Abs(p.x - w.x) - 0.03f));
                    float sign = Box(p, new Vector3(0, 0.3f, -0.36f), new Vector3(0.2f, 0.16f, 0.015f), 0.02f);
                    return Mathf.Min(Mathf.Min(basket, chassis), Mathf.Min(Mathf.Min(handle, wheels), sign));
                }, new Bounds(new Vector3(0, 0.2f, 0.05f), new Vector3(1.2f, 1.3f, 1.2f)), 0.012f, "Kit_Carrito", (p, n) =>
                {
                    if (p.y > 0.66f && p.z > 0.4f) return new Color(0.9f, 0.18f, 0.18f);
                    if (p.y < -0.18f) return new Color(0.15f, 0.15f, 0.17f);
                    if (p.z < -0.35f && Mathf.Abs(p.x) < 0.22f && Mathf.Abs(p.y - 0.3f) < 0.18f)
                    {
                        Vector2 q = new Vector2(p.x, p.y - 0.28f);
                        if ((new Vector2(Mathf.Abs(q.x) - 0.07f, q.y - 0.07f)).magnitude < 0.035f) return Color.white;                 // ojos
                        if (q.magnitude < 0.11f) return new Color(0.35f, 0.75f, 0.3f);                                                 // cara de rana
                        return new Color(1f, 0.82f, 0.2f);
                    }
                    return new Color(0.75f, 0.77f, 0.8f);
                });
                var cg = Part("Carrito (modelo)", cart.transform, mesh);
                cg.transform.localPosition = new Vector3(0, -0.05f, 0); cg.transform.localScale = Vector3.one * 1.4f;
                done++;
            }
            return $" rampa y carrito ({done} piezas).";
        }
    }
}
