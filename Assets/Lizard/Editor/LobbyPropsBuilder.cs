using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Mejorar atrezo del Lobby.
    /// Carteles de madera sin colisión, trampolín de feria, tienda de sombreros con tejado-chistera, campana colgante,
    /// setas, arbustos y sauces modelados, y la fuente de color (sustituye a la ruleta). Se puede volver a ejecutar.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        const string Dir = "Assets/Lizard/Models/Lobby";
        static Material vtx, outlineMat;
        static TMP_FontAsset font;
        static Transform lobby;

        [MenuItem("Lizards/Mejorar atrezo del Lobby (carteles, trampolín, tienda, campana, plantas, fuente)", priority = 9)]
        public static string Build()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Lizard/Models", "Lobby");
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white, 0.004f, 0.2f); m.SetFloat("_VertexColorWeight", 1f); return m; });

            string log = Signs() + Trampoline() + Shop() + Bell() + Mushrooms() + Bushes() + Willows() + Fountain() + RoundMachines();
            var scene = l.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Atrezo del Lobby:" + log);
            return log;
        }

        // ---------------- Utilidades ----------------

        static Material Mat(string name, System.Func<Material> make)
        {
            string path = $"{Dir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var fresh = make();
            if (existing) { existing.shader = fresh.shader; existing.CopyPropertiesFromMaterial(fresh); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        static Mesh Sdf(System.Func<Vector3, float> f, Bounds b, float cell, string name, System.Func<Vector3, Vector3, Color> col)
        {
            var mesh = SdfMesher.Build(f, b, cell, name);
            var v = mesh.vertices; var c = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) c[i] = col(v[i], SdfMesher.Gradient(f, v[i], cell * 0.5f).normalized).linear;
            mesh.colors = c;
            string path = $"{Dir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static GameObject Part(string name, Transform parent, Mesh mesh, Material m = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m ? m : vtx;
            return go;
        }

        static float Box(Vector3 p, Vector3 c, Vector3 h, float r)
        {
            Vector3 q = new Vector3(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y), Mathf.Abs(p.z - c.z)) - h + Vector3.one * r;
            return new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f) - r;
        }
        static float Cyl(Vector3 p, Vector3 c, float r, float h) => Mathf.Max(new Vector2(p.x - c.x, p.z - c.z).magnitude - r, Mathf.Abs(p.y - c.y) - h);
        static float SMax(float a, float b, float k) => -SdfMesher.SMin(-a, -b, k);
        static float N(Vector3 p, float s) => Mathf.PerlinNoise(p.x * s + 11.3f, p.y * s + p.z * s * 0.7f);

        static Color Wood(Vector3 p, Vector3 n)
        {
            float g = Mathf.PerlinNoise(p.x * 4f + p.z * 4f, p.y * 0.8f);
            return Color.Lerp(new Color(0.36f, 0.22f, 0.12f), new Color(0.58f, 0.38f, 0.2f), g);
        }

        static void Kill(string name, Vector3 near, float radius)
        {
            var list = new List<GameObject>();
            foreach (Transform t in lobby)
                if (t.name == name && (radius <= 0f || Vector3.Distance(Flat(t.position), Flat(near)) < radius)) list.Add(t.gameObject);
            foreach (var g in list) Object.DestroyImmediate(g);
        }
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        static Transform Fresh(string name, Vector3 pos)
        {
            var old = lobby.Find(name); if (old) Object.DestroyImmediate(old.gameObject);
            var t = new GameObject(name).transform;
            t.SetParent(lobby, false); t.position = pos;
            return t;
        }

        // ---------------- Carteles ----------------

        static string Signs()
        {
            int n = 0;
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset");
            // Material propio: contorno marrón oscuro grueso y sombra, como en un juego.
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF - Outline.mat");
            outlineMat = null;
            if (baseMat)
            {
                outlineMat = Mat("Rotulo_Bangers", () =>
                {
                    var m = new Material(baseMat);
                    m.SetColor("_OutlineColor", new Color(0.24f, 0.11f, 0.03f));
                    m.SetFloat("_OutlineWidth", 0.28f);
                    m.SetFloat("_FaceDilate", 0.15f);
                    if (m.HasProperty("_UnderlayColor")) { m.EnableKeyword("UNDERLAY_ON"); m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f)); m.SetFloat("_UnderlayOffsetX", 0.6f); m.SetFloat("_UnderlayOffsetY", -0.6f); m.SetFloat("_UnderlaySoftness", 0.2f); }
                    return m;
                });
            }
            foreach (Transform sign in lobby)
            {
                if (!sign.name.StartsWith("Cartel / ")) continue;
                foreach (var old in new[] { "Postes", "Sombra del texto", "Rótulo" }) { var o = sign.Find(old); if (o) Object.DestroyImmediate(o.gameObject); }
                var board = sign.Find("Tablero"); var text = sign.GetComponentInChildren<TextMesh>();
                // Los tableros que se reescriben en juego (salida y jugadores) no llevan sombra duplicada.
                bool dynamicText = sign.name.Contains("BALSA") || sign.name.Contains("JUGADORES");
                if (!board || !text) continue;
                var bm = board.GetComponent<MeshFilter>().sharedMesh;
                Vector3 size = bm && bm.name.StartsWith("Cartel_") ? bm.bounds.size - new Vector3(0.4f, 0.4f, 0f) : board.localScale; // al regenerar, medir el tablón ya hecho
                float w = sign.name.Contains("COLOR") ? 4.2f : Mathf.Max(1.8f, size.x), h = Mathf.Max(0.7f, size.y);
                // Sin colisión: el jugador ya no choca con los carteles.
                foreach (var c in sign.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

                // Tablón de madera con marco y clavos.
                float hw = w * 0.5f, hh = h * 0.5f;
                var mesh = Sdf(p =>
                {
                    float plank = Box(p, Vector3.zero, new Vector3(hw, hh, 0.08f), 0.06f);
                    float frame = Mathf.Max(Box(p, new Vector3(0, 0, -0.03f), new Vector3(hw + 0.12f, hh + 0.12f, 0.1f), 0.08f), -Box(p, Vector3.zero, new Vector3(hw - 0.02f, hh - 0.02f, 0.3f), 0.04f));
                    return Mathf.Min(plank, frame);
                }, new Bounds(Vector3.zero, new Vector3(w + 0.6f, h + 0.6f, 0.6f)), 0.03f, "Cartel_" + sign.name.Substring(9).Replace(' ', '_'), (p, nn) =>
                {
                    bool frame = Mathf.Abs(p.x) > hw - 0.03f || Mathf.Abs(p.y) > hh - 0.03f || p.z < -0.12f;
                    Color c = frame ? new Color(0.34f, 0.2f, 0.1f) : Color.Lerp(new Color(0.62f, 0.42f, 0.22f), new Color(0.72f, 0.52f, 0.3f), Mathf.PerlinNoise(p.x * 3f, p.y * 9f));
                    if (!frame && Mathf.Abs(Mathf.Repeat(p.y + hh, h / 3f)) < 0.025f) c *= 0.75f; // juntas entre tablas
                    return c;
                });
                board.GetComponent<MeshFilter>().sharedMesh = mesh;
                board.GetComponent<MeshRenderer>().sharedMaterial = vtx;
                board.localScale = Vector3.one;
                // Rótulo con letra de juego (Bangers) con contorno y degradado; el TextMesh queda oculto como fuente del texto.
                text.GetComponent<MeshRenderer>().enabled = false;
                var label = new GameObject("Rótulo").AddComponent<TextMeshPro>();
                label.transform.SetParent(sign, false);
                label.transform.localPosition = new Vector3(0f, 0f, -0.16f);
                label.rectTransform.sizeDelta = new Vector2(w - 0.35f, h - 0.18f);
                if (font) label.font = font;
                if (outlineMat) label.fontSharedMaterial = outlineMat;
                label.text = text.text;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true; label.fontSizeMin = 0.5f; label.fontSizeMax = dynamicText || sign.name.Contains("EXPENDEDORA") ? 4.5f : 9f;
                label.enableWordWrapping = false;
                label.characterSpacing = 4f;
                label.color = Color.white;
                label.enableVertexGradient = true;
                label.colorGradient = new VertexGradient(new Color(1f, 0.95f, 0.55f), new Color(1f, 0.95f, 0.55f), new Color(1f, 0.62f, 0.15f), new Color(1f, 0.62f, 0.15f));
                var sl = sign.GetComponent<SignLabel>() ?? sign.gameObject.AddComponent<SignLabel>();
                sl.source = text; sl.target = label;
                // Postes hasta el suelo (sin colisión) para que no floten.
                float bottom = sign.position.y - hh;
                if (bottom > 0.3f && !sign.name.Contains("EXPENDEDORA") && !sign.name.Contains("SALA"))
                {
                    float len = bottom + 0.3f;
                    var posts = Sdf(p => Mathf.Min(SdfMesher.Capsule(p, new Vector3(-hw * 0.7f, 0f, 0.15f), new Vector3(-hw * 0.7f, -len, 0.15f), 0.11f, 0.13f, out _),
                                                   SdfMesher.Capsule(p, new Vector3(hw * 0.7f, 0f, 0.15f), new Vector3(hw * 0.7f, -len, 0.15f), 0.11f, 0.13f, out _)),
                        new Bounds(new Vector3(0, -len * 0.5f, 0.15f), new Vector3(w * 0.7f * 2f + 0.8f, len + 0.6f, 0.6f)), 0.04f, "Cartel_Postes_" + n, Wood);
                    Part("Postes", sign, posts);
                }
                n++;
            }
            return $" {n} carteles;";
        }

        // ---------------- Trampolín ----------------

        static string Trampoline()
        {
            var bed = GameObject.Find("Cama elástica del refugio");
            if (!bed) return "";
            var bounce = bed.GetComponent<SwampBounce>();
            var oldRim = bed.transform.Find("Borde cama elástica"); if (oldRim) Object.DestroyImmediate(oldRim.gameObject);
            bed.GetComponent<MeshRenderer>().enabled = false; // se queda solo la colisión
            float top = bed.transform.position.y + bed.transform.lossyScale.y;
            var root = Fresh("Trampolín (visual)", new Vector3(bed.transform.position.x, top, bed.transform.position.z));
            const float R = 1.6f;
            // Marco acolchado a gajos rojo y amarillo, con patas curvas.
            var frame = Sdf(p =>
            {
                float pad = new Vector2(new Vector2(p.x, p.z).magnitude - R, (p.y - 0.02f) * 1.6f).magnitude - 0.24f;
                float d = pad;
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f;
                    Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    d = SdfMesher.SMin(d, SdfMesher.Capsule(p, o * (R - 0.05f) + Vector3.down * 0.1f, o * (R + 0.35f) + Vector3.down * top, 0.09f, 0.11f, out _), 0.08f);
                    d = Mathf.Min(d, (p - (o * (R + 0.35f) + Vector3.down * (top - 0.06f))).magnitude - 0.17f);
                }
                return d;
            }, new Bounds(new Vector3(0, -top * 0.5f, 0), new Vector3(R * 2f + 1.6f, top + 0.8f, R * 2f + 1.6f)), 0.04f, "Trampolin_Marco", (p, n) =>
            {
                if (p.y < -0.2f) return new Color(0.75f, 0.78f, 0.82f);
                float a = Mathf.Atan2(p.z, p.x) / (Mathf.PI * 2f) * 12f;
                return Mathf.Repeat(a, 2f) < 1f ? new Color(0.95f, 0.25f, 0.22f) : new Color(1f, 0.82f, 0.2f);
            });
            Part("Marco", root, frame);
            // Lona azul con estrella y muelles (se hunde al rebotar).
            var matRoot = new GameObject("Lona").transform; matRoot.SetParent(root, false);
            var lona = Sdf(p =>
            {
                float disc = Mathf.Max(new Vector2(p.x, p.z).magnitude - (R - 0.25f), Mathf.Abs(p.y + 0.02f) - 0.035f);
                float springs = float.MaxValue;
                for (int i = 0; i < 24; i++)
                {
                    float a = i / 24f * Mathf.PI * 2f; Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    springs = Mathf.Min(springs, SdfMesher.Capsule(p, o * (R - 0.27f) + Vector3.down * 0.02f, o * (R - 0.08f), 0.035f, 0.035f, out _));
                }
                return Mathf.Min(disc, springs);
            }, new Bounds(Vector3.zero, new Vector3(R * 2f + 0.4f, 0.4f, R * 2f + 0.4f)), 0.025f, "Trampolin_Lona", (p, n) =>
            {
                float r = new Vector2(p.x, p.z).magnitude;
                if (r > R - 0.27f) return new Color(0.7f, 0.72f, 0.76f);
                float a = Mathf.Atan2(p.z, p.x); float star = 0.55f * (0.6f + 0.4f * Mathf.Cos(a * 5f));
                if (r < star) return new Color(1f, 0.85f, 0.25f);
                return r > R - 0.42f ? new Color(0.95f, 0.95f, 0.95f) : new Color(0.15f, 0.3f, 0.65f);
            });
            Part("Malla", matRoot, lona);
            if (bounce) { bounce.mat = matRoot; EditorUtility.SetDirty(bounce); }
            return " trampolín;";
        }

        // ---------------- Tienda de sombreros ----------------

        static string Shop()
        {
            var shop = GameObject.Find("Tienda con forma de sombrero");
            if (!shop) return "";
            var remove = new List<GameObject>();
            foreach (Transform t in shop.transform)
                if (t.name.StartsWith("Pared") || t.name.StartsWith("Fondo interior") || t.name.StartsWith("Teja") || t.name.StartsWith("Ala del") ||
                    t.name.StartsWith("Ventana") || t.name.StartsWith("Marco ventana") || t.name.StartsWith("Barrote")) remove.Add(t.gameObject);
            foreach (var g in remove) Object.DestroyImmediate(g);
            foreach (var n in new[] { "Tejado naranja", "Copa del gran sombrero" }) { var g = GameObject.Find(n); if (g && g.transform.parent == lobby) Object.DestroyImmediate(g); }

            Vector3 c = new Vector3(-22f, 0.36f, -3f);
            var root = Fresh("Tienda de sombreros (generada)", c);
            // Casita: paredes de yeso crema con vigas de madera, zócalo de piedra, puerta en arco y ventanas redondas.
            System.Func<Vector3, float> walls = p =>
            {
                float shell = Mathf.Max(Box(p, new Vector3(0, 2.1f, 0), new Vector3(3.7f, 2.1f, 2.7f), 0.25f), -Box(p, new Vector3(0, 2.1f, 0), new Vector3(3.35f, 2.4f, 2.35f), 0.15f));
                float door = Mathf.Min(Box(p, new Vector3(0, 1.3f, -2.7f), new Vector3(1.15f, 1.3f, 0.6f), 0.05f), new Vector2(p.x / 1.15f, (p.y - 2.6f) / 0.9f).magnitude - 1f + Mathf.Max(0f, Mathf.Abs(p.z + 2.7f) - 0.6f) * 9f);
                shell = Mathf.Max(shell, -door);
                for (int s = -1; s <= 1; s += 2)
                {
                    shell = Mathf.Max(shell, -Mathf.Max((new Vector2(p.y - 2.4f, p.z - 0.2f)).magnitude - 0.75f, Mathf.Abs(Mathf.Abs(p.x) - 3.6f) - 0.6f)); // ventanas laterales
                    shell = Mathf.Min(shell, Mathf.Max(Mathf.Abs(new Vector2(p.y - 2.4f, p.z - 0.2f).magnitude - 0.8f) - 0.1f, Mathf.Abs(p.x - s * 3.75f) - 0.12f)); // marco
                    shell = Mathf.Min(shell, Box(p, new Vector3(s * 1.7f, 2.1f, -2.78f), new Vector3(0.14f, 2.1f, 0.12f), 0.04f)); // vigas fachada
                }
                shell = Mathf.Min(shell, Box(p, new Vector3(0, 4.05f, -2.78f), new Vector3(3.7f, 0.14f, 0.12f), 0.04f));
                shell = Mathf.Min(shell, Box(p, new Vector3(0, 0.25f, 0), new Vector3(3.85f, 0.25f, 2.85f), 0.1f)); // zócalo
                return shell;
            };
            var wallMesh = Sdf(walls, new Bounds(new Vector3(0, 2.2f, 0), new Vector3(8.6f, 5.0f, 6.6f)), 0.07f, "Tienda_Paredes", (p, n) =>
            {
                if (p.y < 0.52f) return Color.Lerp(new Color(0.42f, 0.4f, 0.44f), new Color(0.58f, 0.55f, 0.56f), N(p, 2f));
                bool beam = Mathf.Abs(p.y - 4.05f) < 0.2f || (Mathf.Abs(Mathf.Abs(p.x) - 1.7f) < 0.2f && p.z < -2.6f) || Mathf.Abs(Mathf.Abs(p.x) - 3.7f) < 0.15f && Mathf.Abs(Mathf.Abs(p.z) - 2.7f) < 0.3f;
                bool frame = Mathf.Abs(new Vector2(p.y - 2.4f, p.z - 0.2f).magnitude - 0.8f) < 0.14f && Mathf.Abs(p.x) > 3.5f;
                if (beam || frame) return Wood(p, n);
                if (Mathf.Abs(p.x) < 3.3f && Mathf.Abs(p.z) < 2.3f) return new Color(0.28f, 0.2f, 0.26f); // interior
                return Color.Lerp(new Color(0.93f, 0.86f, 0.7f), new Color(0.85f, 0.76f, 0.6f), N(p, 1.5f));
            });
            Part("Paredes", root, wallMesh).AddComponent<MeshCollider>();

            // Tejado: una chistera gigante con ala curvada, cinta morada con hebilla dorada y pluma rosa.
            System.Func<Vector3, float> hat = p =>
            {
                float rr = new Vector2(p.x / 1.15f, p.z).magnitude;
                float brim = Mathf.Max(rr - 4.6f, Mathf.Abs(p.y - (4.55f + Mathf.Pow(Mathf.Max(0f, rr - 3.2f), 2f) * 0.18f)) - 0.18f) - 0.05f;
                float crown = Mathf.Max(new Vector2(p.x / 1.1f, p.z).magnitude - (3.0f + (p.y - 4.6f) * 0.06f), Mathf.Abs(p.y - 6.4f) - 1.8f) - 0.05f;
                return SdfMesher.SMin(brim, crown, 0.15f);
            };
            var hatMesh = Sdf(hat, new Bounds(new Vector3(0, 6.2f, 0), new Vector3(11.4f, 4.4f, 9.8f)), 0.07f, "Tienda_Chistera", (p, n) =>
            {
                if (p.y > 4.75f && p.y < 5.5f && new Vector2(p.x / 1.1f, p.z).magnitude > 2.95f) return new Color(0.5f, 0.2f, 0.62f); // cinta
                Color c = Color.Lerp(new Color(0.12f, 0.12f, 0.16f), new Color(0.2f, 0.2f, 0.26f), N(p, 1.2f));
                if (n.y > 0.8f) c = Color.Lerp(c, new Color(0.3f, 0.3f, 0.38f), 0.5f);
                return c;
            });
            Part("Chistera", root, hatMesh).AddComponent<MeshCollider>();
            var buckle = Sdf(p => Mathf.Max(Box(p, Vector3.zero, new Vector3(0.55f, 0.42f, 0.08f), 0.06f), -Box(p, Vector3.zero, new Vector3(0.32f, 0.2f, 0.3f), 0.03f)),
                new Bounds(Vector3.zero, new Vector3(1.4f, 1.1f, 0.4f)), 0.025f, "Tienda_Hebilla", (p, n) => new Color(1f, 0.8f, 0.2f));
            var bk = Part("Hebilla", root, buckle); bk.transform.localPosition = new Vector3(0f, 5.12f, -3.05f);
            var feather = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 10; i++)
                {
                    float t = i / 9f; Vector3 c0 = new Vector3(Mathf.Sin(t * 1.8f) * 0.9f, t * 2.6f, -t * 0.4f);
                    d = SdfMesher.SMin(d, SdfMesher.Ellipsoid(p, c0, new Vector3(0.38f * (1f - t * 0.6f), 0.35f, 0.12f)), 0.15f);
                }
                return d;
            }, new Bounds(new Vector3(0.4f, 1.3f, -0.2f), new Vector3(2.4f, 3.6f, 1.2f)), 0.03f, "Tienda_Pluma", (p, n) => Color.Lerp(new Color(1f, 0.45f, 0.7f), new Color(1f, 0.8f, 0.9f), p.y / 2.6f));
            var fe = Part("Pluma", root, feather); fe.transform.localPosition = new Vector3(2.2f, 5.0f, -1.8f); fe.transform.localRotation = Quaternion.Euler(-10f, 0f, -18f);

            // Ventanas redondas iluminadas en la chistera y faroles a los lados de la puerta.
            var glass = Mat("Tienda_Cristal", () => LizardMaterials.Toon(new Color(1f, 0.85f, 0.45f), 0.003f, 0.9f));
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            for (int s = -1; s <= 1; s += 2)
            {
                var win = Part("Ventana chistera", root, sphere, glass); win.transform.localPosition = new Vector3(s * 1.3f, 6.8f, -3.05f); win.transform.localScale = new Vector3(0.9f, 0.9f, 0.25f);
                var lamp = Part("Farol", root, sphere, glass); lamp.transform.localPosition = new Vector3(s * 1.75f, 3.1f, -3.15f); lamp.transform.localScale = new Vector3(0.36f, 0.5f, 0.36f);
                var li = new GameObject("Luz farol").AddComponent<Light>(); li.transform.SetParent(lamp.transform, false);
                li.type = LightType.Point; li.color = new Color(1f, 0.8f, 0.45f); li.range = 6f; li.intensity = 1.4f; li.shadows = LightShadows.None;
            }
            return " tienda de sombreros;";
        }

        // ---------------- Campana ----------------

        static string Bell()
        {
            var anchor = GameObject.Find("Tocar campana");
            if (!anchor) return "";
            Vector3 b = anchor.transform.position;
            Kill("Soporte campana", b, 2f); Kill("Campana dorada", b, 2f);
            var root = Fresh("Campana de la tienda (generada)", b);
            // Poste con brazo curvo, tejadillo y cuerda.
            var post = Sdf(p =>
            {
                float d = SdfMesher.Capsule(p, new Vector3(0, 0, 0.3f), new Vector3(0, 3.4f, 0.3f), 0.17f, 0.14f, out _);
                d = SdfMesher.SMin(d, SdfMesher.Capsule(p, new Vector3(0, 3.3f, 0.3f), new Vector3(0, 3.4f, -0.9f), 0.11f, 0.1f, out _), 0.08f);
                d = Mathf.Min(d, Box(p, new Vector3(0, 3.75f, -0.3f), new Vector3(0.55f, 0.06f, 0.95f), 0.04f));
                d = Mathf.Min(d, Box(p, new Vector3(0, 0.12f, 0.3f), new Vector3(0.45f, 0.12f, 0.45f), 0.06f));
                return d;
            }, new Bounds(new Vector3(0, 1.9f, -0.2f), new Vector3(1.8f, 4.4f, 2.6f)), 0.035f, "Campana_Poste", Wood);
            Part("Poste", root, post);
            var col = root.gameObject.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 1.7f, 0.3f); col.radius = 0.2f; col.height = 3.4f;
            // Campana con su propio pivote (para balancearse) y badajo.
            var pivot = new GameObject("Pivote campana").transform; pivot.SetParent(root, false); pivot.localPosition = new Vector3(0, 3.3f, -0.9f);
            var bell = Sdf(p =>
            {
                float y = -p.y; // hacia abajo desde el pivote
                float prof = 0.22f + Mathf.Pow(Mathf.Clamp01((y - 0.15f) / 0.95f), 1.8f) * 0.42f + (y > 0.95f ? (y - 0.95f) * 0.9f : 0f);
                float outer = Mathf.Max(new Vector2(p.x, p.z).magnitude - prof, Mathf.Max(0.12f - y, y - 1.18f));
                float inner = Mathf.Max(new Vector2(p.x, p.z).magnitude - (prof - 0.07f), Mathf.Max(0.25f - y, y - 1.3f));
                float shell = Mathf.Max(outer, -inner);
                shell = SdfMesher.SMin(shell, (p - new Vector3(0, -0.12f, 0)).magnitude - 0.25f, 0.08f);
                shell = Mathf.Min(shell, Mathf.Max(Mathf.Abs(new Vector2(p.y + 0.02f, p.z).magnitude - 0.11f) - 0.035f, Mathf.Abs(p.x) - 0.035f)); // asa
                shell = Mathf.Min(shell, new Vector2(new Vector2(p.x, p.z).magnitude - 0.68f, p.y + 1.12f).magnitude - 0.05f); // labio
                return shell;
            }, new Bounds(new Vector3(0, -0.65f, 0), new Vector3(1.7f, 1.6f, 1.7f)), 0.02f, "Campana_Bronce", (p, n) =>
            {
                Color c = Color.Lerp(new Color(0.85f, 0.6f, 0.15f), new Color(1f, 0.85f, 0.35f), Mathf.Clamp01(n.y * 0.5f + 0.5f + N(p, 4f) * 0.2f));
                if (Mathf.Abs(p.y + 0.5f) < 0.04f) c *= 0.7f; // banda grabada
                return c;
            });
            Part("Campana", pivot, bell);
            var clap = new GameObject("Badajo").transform; clap.SetParent(pivot, false); clap.localPosition = new Vector3(0, -0.2f, 0);
            var clapMesh = Sdf(p => SdfMesher.SMin(SdfMesher.Capsule(p, Vector3.zero, new Vector3(0, -0.85f, 0), 0.03f, 0.03f, out _), (p - new Vector3(0, -0.9f, 0)).magnitude - 0.11f, 0.03f),
                new Bounds(new Vector3(0, -0.5f, 0), new Vector3(0.4f, 1.3f, 0.4f)), 0.015f, "Campana_Badajo", (p, n) => new Color(0.3f, 0.25f, 0.2f));
            Part("Malla", clap, clapMesh);
            var rope = Sdf(p => SdfMesher.Capsule(p, new Vector3(0, -1.15f, 0), new Vector3(0.05f, -2.5f, 0.05f), 0.035f, 0.035f, out _) - 0f,
                new Bounds(new Vector3(0, -1.8f, 0), new Vector3(0.4f, 1.6f, 0.4f)), 0.015f, "Campana_Cuerda", (p, n) => new Color(0.85f, 0.75f, 0.5f));
            Part("Cuerda", clap, rope).transform.localPosition = new Vector3(0, 0.2f, 0);
            var swing = pivot.gameObject.AddComponent<BellSwing>(); swing.clapper = clap;
            return " campana;";
        }

        // ---------------- Setas ----------------

        static string Mushrooms()
        {
            var caps = new List<Transform>();
            foreach (Transform t in lobby) if (t.name == "Seta roja") caps.Add(t);
            // Al regenerar ya no quedan las setas originales: se reutilizan las posiciones de las generadas.
            var spots = new List<(Vector3 pos, float s)>();
            var old = lobby.Find("Setas (generadas)");
            foreach (var cap in caps)
            {
                Vector3 bp = cap.position - Vector3.up * 1.69f * cap.lossyScale.y / 0.73f;
                var stemT = FindNear("Tallo de seta", cap.position, 1.5f);
                if (stemT) { bp.y = stemT.position.y - stemT.lossyScale.y * 1.07f; Object.DestroyImmediate(stemT.gameObject); }
                Kill("Mota clara", cap.position, 1.8f);
                spots.Add((bp, cap.lossyScale.x / 0.73f * 1.1f));
            }
            if (caps.Count == 0 && old) foreach (Transform t in old) spots.Add((t.position, t.localScale.x));
            foreach (var cap in caps) Object.DestroyImmediate(cap.gameObject);
            if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Setas (generadas)").transform; root.SetParent(lobby, false);
            var mesh = Sdf(p =>
            {
                float bend = Mathf.Sin(p.y * 1.4f) * 0.12f;
                Vector3 q = p - new Vector3(bend, 0f, 0f);
                float stem = new Vector2(q.x, q.z).magnitude - (0.32f - p.y * 0.05f + Mathf.Exp(-p.y * 4f) * 0.15f);
                stem = Mathf.Max(stem, Mathf.Max(-p.y, p.y - 1.6f));
                float ring = new Vector2(new Vector2(q.x, q.z).magnitude - 0.34f, p.y - 1.05f).magnitude - 0.06f;
                float capOuter = SdfMesher.Ellipsoid(p, new Vector3(0.12f, 1.55f, 0f), new Vector3(1.25f, 0.85f, 1.25f));
                float capCut = p.y - 1.45f + new Vector2(p.x - 0.12f, p.z).magnitude * 0.12f;
                float cap = SMax(capOuter, -capCut, 0.08f);
                float spots = float.MaxValue;
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.39f, r = 0.25f + (i % 3) * 0.33f;
                    Vector3 sp = new Vector3(0.12f + Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    sp.y = 1.55f + 0.85f * Mathf.Sqrt(Mathf.Max(0f, 1f - (Mathf.Pow((sp.x - 0.12f) / 1.25f, 2) + Mathf.Pow(sp.z / 1.25f, 2))));
                    spots = Mathf.Min(spots, (p - sp).magnitude - (0.13f + (i % 2) * 0.05f));
                }
                return Mathf.Min(SdfMesher.SMin(Mathf.Min(stem, ring), cap, 0.1f), SdfMesher.SMin(cap, spots, 0.04f));
            }, new Bounds(new Vector3(0.1f, 1.25f, 0), new Vector3(3.1f, 2.9f, 3.1f)), 0.03f, "Seta", (p, n) =>
            {
                if (p.y < 1.4f) return Color.Lerp(new Color(0.95f, 0.9f, 0.78f), new Color(0.82f, 0.74f, 0.6f), Mathf.Clamp01(1f - p.y));
                if (n.y < -0.3f) return Mathf.Repeat(Mathf.Atan2(p.z, p.x - 0.12f) * 8f, 1f) < 0.5f ? new Color(0.92f, 0.85f, 0.7f) : new Color(0.78f, 0.68f, 0.52f); // láminas
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.39f, r = 0.25f + (i % 3) * 0.33f;
                    if (new Vector2(p.x - 0.12f - Mathf.Cos(a) * r, p.z - Mathf.Sin(a) * r).magnitude < 0.17f + (i % 2) * 0.05f && p.y > 1.7f) return new Color(1f, 0.97f, 0.9f);
                }
                return Color.Lerp(new Color(0.85f, 0.12f, 0.12f), new Color(1f, 0.3f, 0.22f), Mathf.Clamp01((p.y - 1.5f) * 1.2f));
            });
            int n = 0;
            foreach (var spot in spots)
            {
                float s = spot.s; Vector3 basePos = spot.pos;
                // Si estorba junto a la expendedora, se aparta al otro lado y se hace más pequeña.
                var vend = GameObject.Find("Máquina expendedora roja");
                if (vend && Vector3.Distance(Flat(basePos), Flat(vend.transform.position)) < 11f && s > 1f)
                {
                    Vector3 away = Flat(basePos - vend.transform.position).normalized;
                    basePos = vend.transform.position + away * 14f + new Vector3(0f, 0f, n % 2 == 0 ? 5f : -6f);
                    if (Physics.Raycast(new Vector3(basePos.x, 50f, basePos.z), Vector3.down, out var gh, 100f, ~0, QueryTriggerInteraction.Ignore)) basePos.y = gh.point.y;
                    s = Mathf.Min(s * 0.6f, 1.4f);
                }
                var go = Part("Seta", root, mesh);
                go.transform.position = basePos; go.transform.rotation = Quaternion.Euler(0f, n * 67f, 0f); go.transform.localScale = Vector3.one * s;
                var cc = go.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 0.8f, 0); cc.radius = 0.35f; cc.height = 1.6f;
                n++;
            }
            return $" {n} setas;";
        }

        static Transform FindNear(string name, Vector3 pos, float r)
        {
            foreach (Transform t in lobby) if (t.name == name && Vector3.Distance(Flat(t.position), Flat(pos)) < r) return t;
            return null;
        }

        // ---------------- Arbustos ----------------

        static string Bushes()
        {
            var meshes = new Mesh[3];
            for (int v = 0; v < 3; v++)
            {
                int seed = v;
                meshes[v] = Sdf(p =>
                {
                    var r = new System.Random(seed * 31 + 3);
                    float d = SdfMesher.Ellipsoid(p, new Vector3(0, -0.05f, 0), new Vector3(0.42f, 0.3f, 0.42f));
                    for (int i = 0; i < 11; i++)
                    {
                        float a = (float)(r.NextDouble() * Mathf.PI * 2), el = (float)r.NextDouble() * 0.9f;
                        Vector3 c = new Vector3(Mathf.Cos(a) * Mathf.Cos(el), Mathf.Sin(el) * 0.7f, Mathf.Sin(a) * Mathf.Cos(el)) * 0.33f;
                        d = SdfMesher.SMin(d, (p - c).magnitude - (0.14f + (float)r.NextDouble() * 0.08f), 0.06f);
                    }
                    return Mathf.Max(d, -0.32f - p.y);
                }, new Bounds(Vector3.zero, Vector3.one * 1.3f), 0.02f, "Arbusto_" + v, (p, n) =>
                {
                    Color c = Color.Lerp(new Color(0.16f, 0.4f, 0.14f), new Color(0.42f, 0.72f, 0.24f), Mathf.Clamp01(n.y * 0.5f + 0.4f + p.y));
                    if (N(p, 9f) > 0.72f && n.y > 0.2f) c = (seed == 1 ? new Color(1f, 0.6f, 0.75f) : new Color(1f, 0.92f, 0.5f)); // florecillas
                    return c;
                });
            }
            int count = 0;
            foreach (Transform t in lobby)
            {
                if (t.name != "Arbusto de ribera") continue;
                var mf = t.GetComponent<MeshFilter>(); if (!mf) continue;
                mf.sharedMesh = meshes[count % 3];
                t.GetComponent<MeshRenderer>().sharedMaterial = vtx;
                t.localRotation = Quaternion.Euler(0f, count * 53f, 0f);
                count++;
            }
            return $" {count} arbustos;";
        }

        // ---------------- Sauces ----------------

        static string Willows()
        {
            var groups = new List<Transform>();
            foreach (Transform t in lobby) if (t.name == "Sauce / tronco y cortina") groups.Add(t);
            var meshes = new Mesh[2]; var leaves = new Mesh[2];
            for (int v = 0; v < 2; v++)
            {
                int seed = v;
                meshes[v] = Sdf(p =>
                {
                    float twist = Mathf.Sin(p.y * 0.7f + seed) * 0.25f;
                    float trunk = SdfMesher.Capsule(p, Vector3.zero, new Vector3(twist + 0.3f, 4.4f, 0f), 0.6f, 0.35f, out _);
                    for (int r = 0; r < 4; r++) // raíces
                    {
                        float a = r * 1.57f + seed;
                        trunk = SdfMesher.SMin(trunk, SdfMesher.Capsule(p, new Vector3(0, 0.6f, 0), new Vector3(Mathf.Cos(a) * 1.1f, -0.15f, Mathf.Sin(a) * 1.1f), 0.32f, 0.12f, out _), 0.25f);
                    }
                    for (int b = 0; b < 4; b++) // ramas hacia la copa
                    {
                        float a = b * 1.57f + 0.6f + seed;
                        trunk = SdfMesher.SMin(trunk, SdfMesher.Capsule(p, new Vector3(0.3f, 3.6f, 0), new Vector3(Mathf.Cos(a) * 1.8f, 5.3f, Mathf.Sin(a) * 1.8f), 0.24f, 0.1f, out _), 0.15f);
                    }
                    return trunk;
                }, new Bounds(new Vector3(0, 2.6f, 0), new Vector3(4.6f, 6.2f, 4.6f)), 0.05f, "Sauce_Tronco_" + v, (p, n) =>
                    Color.Lerp(new Color(0.3f, 0.2f, 0.12f), new Color(0.48f, 0.34f, 0.2f), Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.z, p.x) * 6f + p.y * 1.5f)) * 0.6f + N(p, 2f) * 0.4f));
                leaves[v] = Sdf(p =>
                {
                    // Copa redondeada de 4-5 bolas fundidas, sin nada colgando.
                    var r = new System.Random(seed * 17 + 5);
                    float d = SdfMesher.Ellipsoid(p, new Vector3(0.3f, 5.9f, 0f), new Vector3(2.6f, 1.7f, 2.6f));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f + (float)r.NextDouble() * 0.5f, rr = 1.6f + (float)r.NextDouble() * 0.5f;
                        d = SdfMesher.SMin(d, (p - new Vector3(Mathf.Cos(a) * rr + 0.3f, 5.5f + (float)r.NextDouble() * 1.2f, Mathf.Sin(a) * rr)).magnitude - (1.3f + (float)r.NextDouble() * 0.4f), 0.6f);
                    }
                    return d;
                }, new Bounds(new Vector3(0.3f, 6f, 0), new Vector3(8f, 5.4f, 8f)), 0.06f, "Sauce_Hojas_" + v, (p, n) =>
                {
                    // Luz arriba, sombra abajo, y manchas de hojas más claras.
                    float up = Mathf.Clamp01(n.y * 0.5f + 0.5f);
                    Color c = Color.Lerp(new Color(0.16f, 0.42f, 0.17f), new Color(0.5f, 0.82f, 0.3f), up);
                    if (N(p, 2.2f) > 0.66f && n.y > 0f) c = Color.Lerp(c, new Color(0.7f, 0.92f, 0.42f), 0.55f);
                    return c;
                });
            }
            int count = 0;
            foreach (var g in groups)
            {
                var trunkOld = g.Find("Tronco ensanchado");
                Vector3 pos = trunkOld ? trunkOld.position : g.position; pos.y = trunkOld ? trunkOld.position.y - 0.76f : 0f;
                float s = trunkOld ? trunkOld.lossyScale.x : 1f;
                for (int i = g.childCount - 1; i >= 0; i--) Object.DestroyImmediate(g.GetChild(i).gameObject);
                var tree = new GameObject("Sauce").transform; tree.SetParent(g, false);
                tree.position = pos; tree.rotation = Quaternion.Euler(0f, count * 47f, 0f); tree.localScale = Vector3.one * s;
                Part("Tronco", tree, meshes[count % 2]);
                Part("Copa y cortina", tree, leaves[count % 2]);
                var cc = tree.gameObject.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0, 2f, 0); cc.radius = 0.6f; cc.height = 4f;
                count++;
            }
            return $" {count} sauces;";
        }

        // ---------------- Fuente de color ----------------

        static string Fountain()
        {
            var sel = GameObject.Find("Selector de color");
            if (!sel) return "";
            Vector3 c = sel.transform.position;
            var wheel = sel.GetComponent<ColorWheelStation>(); if (wheel) Object.DestroyImmediate(wheel);
            Kill("Rueda de colores", c, 3f); Kill("Muestra de color", c, 4f);
            Transform sign = null; foreach (Transform t in lobby) if (t.name == "Cartel / COLOR") sign = t; // Find() trataría "/" como ruta
            if (sign)
            {
                sign.position = c + new Vector3(-6.2f, 2.4f, 2.5f);
                sign.rotation = Quaternion.Euler(0f, 30f, 0f);
                foreach (var tm in sign.GetComponentsInChildren<TextMesh>(true)) tm.text = "FUENTE DE COLOR";
                foreach (var tmp in sign.GetComponentsInChildren<TextMeshPro>(true)) tmp.text = "FUENTE DE COLOR";
            }

            var root = Fresh("Fuente de color (generada)", c);
            const float Rout = 4.6f, Rin = 4.0f, Wall = 0.95f, PoolY = 0.7f, MidY = 2.55f, TopY = 4.05f;
            // Escalones: dos fuera para subir al borde y tres dentro, sobre el agua, para llegar a la taza del medio.
            var steps = new (Vector3 c, float top, float r)[]
            {
                (new Vector3(0f, 0f, -6.3f), 0.32f, 0.6f), (new Vector3(0.6f, 0f, -5.45f), 0.62f, 0.55f),
                (new Vector3(0.3f, 0f, -3.3f), 0.98f, 0.55f), (new Vector3(1.5f, 0f, -2.6f), 1.5f, 0.5f), (new Vector3(2.3f, 0f, -1.3f), 2.05f, 0.5f),
            };
            System.Func<Vector3, float> stone = p =>
            {
                float rr = new Vector2(p.x, p.z).magnitude;
                // Pila cerrada con borde redondeado (más ancho arriba).
                float ring = Mathf.Max(Mathf.Max(rr - Rout, Rin - rr), Mathf.Max(-p.y, p.y - Wall + 0.12f)) - 0.04f;
                ring = Mathf.Min(ring, new Vector2(rr - (Rin + Rout) * 0.5f, p.y - (Wall - 0.12f)).magnitude - 0.38f);
                float floor = Mathf.Max(rr - Rin, Mathf.Abs(p.y - 0.05f) - 0.06f);
                // Columna, taza del medio, columna alta, taza de arriba y remate.
                float col1 = Mathf.Max(rr - (0.55f + Mathf.Exp(-p.y * 2.5f) * 0.4f), Mathf.Max(-p.y, p.y - 2.1f));
                float midOut = Mathf.Max(SdfMesher.Ellipsoid(p, new Vector3(0, 2.3f, 0), new Vector3(2.05f, 0.6f, 2.05f)), p.y - 2.75f);
                float mid = Mathf.Max(midOut, -SdfMesher.Ellipsoid(p, new Vector3(0, 2.8f, 0), new Vector3(1.8f, 0.45f, 1.8f)));
                mid = Mathf.Min(mid, new Vector2(rr - 1.95f, p.y - 2.72f).magnitude - 0.12f);
                float col2 = Mathf.Max(rr - (0.32f + Mathf.Exp(-(p.y - 2.6f) * 3f) * 0.2f), Mathf.Max(2.5f - p.y, p.y - 3.7f));
                float topOut = Mathf.Max(SdfMesher.Ellipsoid(p, new Vector3(0, 3.85f, 0), new Vector3(1.15f, 0.38f, 1.15f)), p.y - 4.12f);
                float top = Mathf.Max(topOut, -SdfMesher.Ellipsoid(p, new Vector3(0, 4.15f, 0), new Vector3(0.98f, 0.3f, 0.98f)));
                top = Mathf.Min(top, new Vector2(rr - 1.1f, p.y - 4.1f).magnitude - 0.08f);
                float finial = SdfMesher.SMin(SdfMesher.Capsule(p, new Vector3(0, 4.0f, 0), new Vector3(0, 4.75f, 0), 0.18f, 0.12f, out _), (p - new Vector3(0, 4.95f, 0)).magnitude - 0.3f, 0.08f);
                float body = SdfMesher.SMin(SdfMesher.SMin(col1, mid, 0.25f), SdfMesher.SMin(col2, Mathf.Min(top, finial), 0.15f), 0.2f);
                float d = Mathf.Min(Mathf.Min(ring, floor), body);
                foreach (var st in steps)
                    d = Mathf.Min(d, Mathf.Max(new Vector2(p.x - st.c.x, p.z - st.c.z).magnitude - st.r, Mathf.Max(-p.y, p.y - st.top)) - 0.06f);
                return d;
            };
            var stoneMesh = Sdf(stone, new Bounds(new Vector3(0, 2.6f, -0.6f), new Vector3(10f, 5.6f, 11.4f)), 0.06f, "Fuente_Piedra", (p, n) =>
            {
                float rr = new Vector2(p.x, p.z).magnitude;
                Color s = Color.Lerp(new Color(0.62f, 0.6f, 0.68f), new Color(0.84f, 0.82f, 0.88f), N(p, 2.5f));
                if (n.y > 0.6f) s = Color.Lerp(s, new Color(0.95f, 0.94f, 0.98f), 0.45f);                       // bordes claros
                if (p.y < 0.14f && rr < Rin) s = new Color(0.32f, 0.4f, 0.5f);                                  // fondo
                if (Mathf.Abs(p.y - 0.35f) < 0.05f && rr > Rin + 0.1f) s *= 0.8f;                                 // moldura
                if (N(p, 4f) > 0.74f && n.y > 0.3f && p.y < 1.2f) s = Color.Lerp(s, new Color(0.35f, 0.58f, 0.3f), 0.55f); // musgo
                return s;
            });
            Part("Piedra", root, stoneMesh).AddComponent<MeshCollider>();

            // Agua (material que cambia de color): pila, taza del medio, taza de arriba y dos cortinas que caen.
            var waterMat = Mat("Fuente_Agua", () => LizardMaterials.Transparent(new Color(0.36f, 0.76f, 0.2f, 0.8f), 0.95f));
            GameObject Sheet(string name, float radius, float y, float cell)
            {
                var m = Sdf(p => Mathf.Max(new Vector2(p.x, p.z).magnitude - radius, Mathf.Abs(p.y) - 0.07f), new Bounds(Vector3.zero, new Vector3(radius * 2f + 0.4f, 0.4f, radius * 2f + 0.4f)), cell, name, (p, n) => Color.white);
                var go = Part(name, root, m, waterMat); go.transform.localPosition = new Vector3(0f, y, 0f);
                var wf = go.AddComponent<WaterFlow>(); wf.kind = WaterFlow.Kind.Surface; wf.amplitude = 0.035f; wf.frequency = 3.5f; wf.speed = 2.6f;
                return go;
            }
            GameObject Curtain(string name, float r0, float y0, float r1, float y1)
            {
                var m = Sdf(p =>
                {
                    float k = Mathf.Clamp01((y0 - p.y) / (y0 - y1));
                    float r = Mathf.Lerp(r0, r1, Mathf.Sqrt(k));
                    return Mathf.Max(Mathf.Abs(new Vector2(p.x, p.z).magnitude - r) - 0.06f, Mathf.Max(y1 - p.y, p.y - y0));
                }, new Bounds(new Vector3(0, (y0 + y1) * 0.5f, 0), new Vector3(r1 * 2f + 0.6f, y0 - y1 + 0.4f, r1 * 2f + 0.6f)), 0.06f, name, (p, n) => Color.white);
                var go = Part(name, root, m, waterMat);
                var wf = go.AddComponent<WaterFlow>(); wf.kind = WaterFlow.Kind.Curtain; wf.amplitude = 0.05f; wf.frequency = 6f; wf.speed = 9f;
                return go;
            }
            var water = new List<GameObject>
            {
                Sheet("Fuente_AguaPila", Rin - 0.02f, PoolY, 0.08f),
                Sheet("Fuente_AguaMedio", 1.8f, MidY, 0.06f),
                Sheet("Fuente_AguaArriba", 0.95f, TopY, 0.05f),
                Curtain("Fuente_CaidaArriba", 1.15f, TopY, 1.55f, MidY),
                Curtain("Fuente_CaidaMedio", 2.05f, MidY - 0.05f, 2.75f, PoolY),
            };
            foreach (var w in water) w.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var spout = new GameObject("Caño").transform; spout.SetParent(root, false); spout.localPosition = new Vector3(0, 5.2f, 0);
            var light = new GameObject("Brillo del agua").AddComponent<Light>(); light.transform.SetParent(root, false); light.transform.localPosition = new Vector3(0, 2f, 0);
            light.type = LightType.Point; light.range = 10f; light.intensity = 1.6f; light.shadows = LightShadows.None;
            var f = root.gameObject.AddComponent<ColorFountain>();
            f.water = water.ConvertAll(w => (Renderer)w.GetComponent<MeshRenderer>()).ToArray();
            f.glow = light; f.spout = spout; f.poolRadius = Rin - 0.1f; f.waterTopY = PoolY; f.useRadius = 7f;
            f.splashRings = new[] { new Vector2(2.75f, PoolY), new Vector2(1.55f, MidY) };
            return " fuente de color.";
        }

        // ---------------- Máquina expendedora y cabina: cantos redondeados ----------------

        /// <summary>Cambia los cubos de la expendedora y la cabina por un cubo de cantos redondeados (se ve mucho menos "de caja").</summary>
        public static string RoundMachines()
        {
            if (!lobby) lobby = GameObject.Find("LOBBY / reconstrucción detallada").transform;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Lizard/Models", "Lobby");
            var rounded = Sdf(p => Box(p, Vector3.zero, Vector3.one * 0.5f, 0.12f), new Bounds(Vector3.zero, Vector3.one * 1.3f), 0.025f, "Cubo_Redondeado", (p, n) => Color.white);
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            int n = 0;
            foreach (var name in new[] { "Máquina expendedora roja", "Cabina telefónica violeta" })
            {
                var m = GameObject.Find(name); if (!m) continue;
                foreach (var mf in m.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh && (mf.sharedMesh.name == "Cube" || mf.sharedMesh.name == "Cubo_Redondeado")) { mf.sharedMesh = rounded; n++; }
                // Cúpula luminosa encima de la cabina.
                if (name.StartsWith("Cabina") && !m.transform.Find("Cúpula"))
                {
                    var dome = new GameObject("Cúpula"); dome.transform.SetParent(m.transform, false);
                    dome.transform.localPosition = new Vector3(0f, 3.95f, 0.25f); dome.transform.localScale = new Vector3(1.3f, 0.5f, 1.3f);
                    dome.AddComponent<MeshFilter>().sharedMesh = sphere;
                    var techo = m.transform.Find("Techo cabina");
                    dome.AddComponent<MeshRenderer>().sharedMaterial = techo ? techo.GetComponent<Renderer>().sharedMaterial : vtx;
                }
            }
            return $" {n} piezas redondeadas.";
        }
    }
}
