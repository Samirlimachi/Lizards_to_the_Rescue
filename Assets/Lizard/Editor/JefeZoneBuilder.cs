using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Mejorar zona del Jefe.
    /// Balsa de troncos con mástil y farol, caldero de ofrendas burbujeante, rocas con musgo y grietas,
    /// y fondo más tenebroso (enredaderas en la fábrica y árboles secos). Se puede volver a ejecutar.
    /// </summary>
    public static class JefeZoneBuilder
    {
        const string Dir = "Assets/Lizard/Models/Jefe";

        [MenuItem("Lizards/Mejorar zona del Jefe (balsa, caldero, rocas, fondo)", priority = 8)]
        public static string Build()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var lobby = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!lobby) return "Esta escena no es el Lobby.";
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Lizard/Models", "Jefe");
            var vtx = Mat("Zona_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white, 0.003f, 0.15f); m.SetFloat("_VertexColorWeight", 1f); return m; });

            string log = Raft(lobby.transform, vtx) + Cauldron() + Rocks(vtx) + Backdrop(lobby.transform, vtx);
            var scene = lobby.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Zona del Jefe mejorada:" + log);
            return log;
        }

        static Material Mat(string name, System.Func<Material> make)
        {
            string path = $"{Dir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var fresh = make();
            if (existing) { existing.shader = fresh.shader; existing.CopyPropertiesFromMaterial(fresh); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        static Mesh SaveMesh(Mesh mesh)
        {
            string path = $"{Dir}/{mesh.name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Mesh Sdf(System.Func<Vector3, float> f, Bounds b, float cell, string name, System.Func<Vector3, Vector3, Color> col)
        {
            var mesh = SdfMesher.Build(f, b, cell, name);
            var v = mesh.vertices; var c = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) c[i] = col(v[i], SdfMesher.Gradient(f, v[i], cell * 0.5f).normalized).linear;
            mesh.colors = c;
            return SaveMesh(mesh);
        }

        static GameObject Part(string name, Transform parent, Mesh mesh, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static Color Wood(Vector3 p, Vector3 n)
        {
            float grain = Mathf.PerlinNoise(p.x * 3f, p.z * 0.6f + p.y * 2f);
            Color c = Color.Lerp(new Color(0.33f, 0.21f, 0.12f), new Color(0.52f, 0.34f, 0.19f), grain);
            if (n.y > 0.6f) c = Color.Lerp(c, new Color(0.6f, 0.42f, 0.25f), 0.3f);
            return c;
        }

        // ---------------- Balsa inflable redonda ----------------

        static string Raft(Transform lobby, Material vtx)
        {
            var raft = GameObject.Find("Balsa naranja de salida") ?? GameObject.Find("Balsa de expedición");
            if (!raft) return " (sin balsa)";
            raft.name = "Balsa naranja de salida";
            for (int i = raft.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(raft.transform.GetChild(i).gameObject);
            var wake = GameObject.Find("Estela de la balsa"); if (wake) wake.transform.localScale = new Vector3(1.05f, 1f, 1.05f);

            const float Major = 3.6f, Minor = 0.5f, RingY = 0.45f;
            // Flotador: tubo hinchado con 8 cámaras (ligeros estrechamientos entre ellas).
            System.Func<Vector3, float> tube = p =>
            {
                float ang = Mathf.Atan2(p.z, p.x);
                float pinch = 1f - 0.07f * Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 4f)), 12f);
                Vector2 q = new Vector2(new Vector2(p.x, p.z).magnitude - Major, p.y - RingY);
                return q.magnitude - Minor * pinch;
            };
            Part("Flotador", raft.transform, Sdf(tube, new Bounds(new Vector3(0, RingY, 0), new Vector3(9.4f, 1.4f, 9.4f)), 0.05f, "Balsa_Flotador", (p, n) =>
            {
                float ang = Mathf.Atan2(p.z, p.x);
                Color c = new Color(1f, 0.45f, 0.08f);
                if (Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 4f)), 12f) > 0.75f) c = new Color(0.55f, 0.2f, 0.05f);     // costuras entre cámaras
                else if (Mathf.Abs(p.y - RingY) < 0.09f && new Vector2(p.x, p.z).magnitude > Major) c = new Color(1f, 0.85f, 0.3f); // franja amarilla
                if (n.y > 0.7f) c = Color.Lerp(c, Color.white, 0.12f);                                                  // brillo de goma arriba
                return c;
            }), vtx).AddComponent<MeshCollider>();

            // Suelo de goma con nervios.
            System.Func<Vector3, float> deck = p =>
            {
                float rr = new Vector2(p.x, p.z).magnitude;
                float floor = Mathf.Max(rr - (Major - 0.1f), Mathf.Abs(p.y - 0.06f) - 0.09f);
                float ribs = Mathf.Max(Mathf.Max(Mathf.Abs(Mathf.Repeat(p.x + 0.35f, 0.7f) - 0.35f) - 0.06f, rr - (Major - 0.4f)), Mathf.Abs(p.y - 0.16f) - 0.04f);
                return Mathf.Min(floor, ribs);
            };
            var deckGo = Part("Suelo", raft.transform, Sdf(deck, new Bounds(new Vector3(0, 0.1f, 0), new Vector3(7.6f, 0.6f, 7.6f)), 0.05f, "Balsa_Suelo",
                (p, n) => p.y > 0.13f ? new Color(0.2f, 0.24f, 0.3f) : new Color(0.14f, 0.17f, 0.22f)), vtx);
            deckGo.AddComponent<MeshCollider>();

            // Asas de cuerda negra alrededor, una por cámara, y válvula.
            System.Func<Vector3, float> handles = p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    float a = (i + 0.5f) / 8f * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 c = dir * (Major + Minor * 0.85f) + Vector3.up * (RingY + 0.05f);
                    // Lazo: anillo vertical tangente al flotador.
                    Vector3 tangent = new Vector3(-dir.z, 0f, dir.x);
                    Vector3 q = p - c;
                    float u = Vector3.Dot(q, tangent), v = q.y, w = Vector3.Dot(q, dir);
                    d = Mathf.Min(d, new Vector2(new Vector2(u * 0.8f, v).magnitude - 0.28f, w - 0.05f).magnitude - 0.055f);
                }
                Vector3 valve = new Vector3(0f, RingY + Minor * 0.85f, -Major) ;
                d = Mathf.Min(d, SdfMesher.Capsule(p, valve, valve + new Vector3(0f, 0.22f, -0.12f), 0.1f, 0.08f, out _));
                return d;
            };
            Part("Asas", raft.transform, Sdf(handles, new Bounds(new Vector3(0, RingY, 0), new Vector3(9.8f, 1.6f, 9.8f)), 0.035f, "Balsa_Asas",
                (p, n) => p.y > RingY + 0.5f ? new Color(0.85f, 0.85f, 0.8f) : new Color(0.08f, 0.08f, 0.09f)), vtx);

            // Remo inflable apoyado dentro.
            System.Func<Vector3, float> oar = p =>
                SdfMesher.SMin(SdfMesher.Capsule(p, new Vector3(-1.6f, 0.3f, 1.1f), new Vector3(1.4f, 0.55f, 1.9f), 0.07f, 0.07f, out _),
                               SdfMesher.Ellipsoid(p, new Vector3(1.75f, 0.58f, 2.0f), new Vector3(0.5f, 0.06f, 0.26f)), 0.05f);
            Part("Remo", raft.transform, Sdf(oar, new Bounds(new Vector3(0, 0.45f, 1.5f), new Vector3(4.6f, 0.8f, 2.2f)), 0.03f, "Balsa_Remo",
                (p, n) => p.x > 1.2f ? new Color(1f, 0.85f, 0.3f) : new Color(0.25f, 0.25f, 0.28f)), vtx);

            // Tablón hasta el borde del flotador.
            var plank = GameObject.Find("Tablón de acceso balsa");
            if (plank)
            {
                Vector3 a = new Vector3(2f, 0.08f, 10.9f), b = new Vector3(2f, 0.9f, 14.7f);
                plank.transform.position = (a + b) * 0.5f;
                plank.transform.rotation = Quaternion.LookRotation(b - a);
                plank.transform.localScale = new Vector3(2.6f, 0.2f, (b - a).magnitude);
            }
            return " balsa inflable;";
        }

        static float BoxR(Vector3 p, Vector3 c, Vector3 h, float r)
        {
            Vector3 q = new Vector3(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y), Mathf.Abs(p.z - c.z)) - h + Vector3.one * r;
            return new Vector3(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f), Mathf.Max(q.z, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f) - r;
        }

        // ---------------- Caldero ----------------

        static string Cauldron()
        {
            var pot = GameObject.Find("Cuenco de provisiones");
            if (!pot) return "";
            var iron = Mat("Caldero_Hierro", () => LizardMaterials.Toon(new Color(0.16f, 0.16f, 0.2f), 0.003f, 0.45f));
            var rust = Mat("Caldero_Borde", () => LizardMaterials.Toon(new Color(0.42f, 0.24f, 0.14f), 0.003f, 0.3f));
            var brew = Mat("Caldero_Pocion", () => LizardMaterials.Toon(new Color(0.42f, 0.95f, 0.25f), 0f, 0.8f));
            var bowl = GameObject.Find("Cuenco azul"); if (bowl) bowl.GetComponent<Renderer>().sharedMaterial = iron;
            var rim = GameObject.Find("Borde naranja del cuenco"); if (rim) rim.GetComponent<Renderer>().sharedMaterial = rust;
            var bottom = GameObject.Find("Fondo cuenco");
            if (bottom)
            {
                bottom.GetComponent<Renderer>().sharedMaterial = brew;
                var p = bottom.transform.position; bottom.transform.position = new Vector3(p.x, 1.25f, p.z); // líquido casi hasta el borde
            }
            var old = pot.transform.Find("Burbujas"); if (old) Object.DestroyImmediate(old.gameObject);
            var bubbles = new GameObject("Burbujas").AddComponent<CauldronBubbles>();
            bubbles.transform.SetParent(pot.transform, false);
            bubbles.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            bubbles.bubbleMat = brew;
            var light = new GameObject("Brillo de la poción").AddComponent<Light>();
            light.transform.SetParent(bubbles.transform, false); light.transform.localPosition = Vector3.up * 1.5f;
            light.type = LightType.Point; light.color = new Color(0.5f, 1f, 0.35f); light.range = 9f; light.intensity = 1.6f; light.shadows = LightShadows.None;
            return " caldero burbujeante;";
        }

        // ---------------- Rocas ----------------

        static string Rocks(Material vtx)
        {
            int n = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mf.name != "Roca esculpida" || !mf.sharedMesh) continue;
                var mesh = mf.sharedMesh;
                var v = mesh.vertices; var nr = mesh.normals; var c = new Color[v.Length];
                var tr = mf.transform;
                for (int i = 0; i < v.Length; i++)
                {
                    Vector3 w = tr.TransformPoint(v[i]);
                    Vector3 wn = nr.Length == v.Length ? tr.TransformDirection(nr[i]).normalized : Vector3.up;
                    float noise = Mathf.PerlinNoise(w.x * 0.45f + 7f, w.z * 0.45f + w.y * 0.3f);
                    Color col = Color.Lerp(new Color(0.27f, 0.26f, 0.31f), new Color(0.46f, 0.43f, 0.47f), noise);
                    if (w.y < 0.1f) col = Color.Lerp(col, new Color(0.15f, 0.2f, 0.15f), 0.6f);            // húmedo junto al agua
                    if (wn.y > 0.5f) col = Color.Lerp(col, new Color(0.24f, 0.4f, 0.16f), Mathf.Clamp01((wn.y - 0.5f) * 3f) * (0.5f + noise * 0.5f)); // musgo
                    c[i] = col.linear;
                }
                mesh.colors = c;
                EditorUtility.SetDirty(mesh);
                mf.GetComponent<Renderer>().sharedMaterial = vtx;
                n++;
            }
            return $" {n} rocas con musgo;";
        }

        // ---------------- Fondo ----------------

        static float GroundY(Vector3 p)
        {
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, 60f, p.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore))
                if (!h.collider.GetComponentInParent<LizardPlayer>()) return h.point.y;
            return 0f;
        }

        static string Backdrop(Transform lobby, Material vtx)
        {
            var old = lobby.Find("Fondo tenebroso (generado)"); if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Fondo tenebroso (generado)").transform;
            root.SetParent(lobby, false);
            var rng = new System.Random(99);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

            // Enredaderas colgando de la cornisa de la fábrica.
            System.Func<Vector3, float> vines = p =>
            {
                float d = float.MaxValue;
                var r2 = new System.Random(5);
                for (int i = 0; i < 26; i++)
                {
                    float x = -32f + i * 2.8f + (float)r2.NextDouble() * 1.2f;
                    float len = 3f + (float)r2.NextDouble() * 6f;
                    if (Mathf.Abs(p.x - x) > 1.5f) continue;
                    Vector3 prev = new Vector3(x, 14f, 46.2f);
                    for (int k = 1; k <= 5; k++)
                    {
                        var nx = new Vector3(x + Mathf.Sin(k * 1.7f + i) * 0.35f, 14f - len * k / 5f, 46.15f);
                        d = Mathf.Min(d, SdfMesher.Capsule(p, prev, nx, 0.16f, 0.12f, out _));
                        if (k % 2 == 0) d = SdfMesher.SMin(d, SdfMesher.Ellipsoid(p, nx + new Vector3(0.2f, 0f, -0.1f), new Vector3(0.35f, 0.22f, 0.12f)), 0.06f);
                        prev = nx;
                    }
                }
                return d;
            };
            Part("Enredaderas", root, Sdf(vines, new Bounds(new Vector3(4f, 9.5f, 46.1f), new Vector3(78f, 10f, 1.2f)), 0.09f, "Fondo_Enredaderas",
                (p, n) => Color.Lerp(new Color(0.12f, 0.26f, 0.1f), new Color(0.26f, 0.44f, 0.16f), Mathf.PerlinNoise(p.x, p.y))), vtx);

            // Árboles secos y retorcidos entre el trono y la fábrica.
            Vector3[] spots = { new(-13f, 0f, 41f), new(13f, 0f, 41.5f), new(-22f, 0f, 39f), new(22f, 0f, 40f) };
            for (int t = 0; t < spots.Length; t++)
            {
                var basePos = spots[t]; basePos.y = GroundY(basePos);
                int seed = t;
                System.Func<Vector3, float> tree = p =>
                {
                    var r3 = new System.Random(seed * 13 + 1);
                    float Rr(float a, float b) => a + (float)r3.NextDouble() * (b - a);
                    Vector3 a0 = Vector3.zero, a1 = new Vector3(Rr(-0.8f, 0.8f), 3.5f, Rr(-0.5f, 0.5f)), a2 = a1 + new Vector3(Rr(-1f, 1f), 3f, Rr(-0.5f, 0.5f));
                    float d = SdfMesher.SMin(SdfMesher.Capsule(p, a0, a1, 0.75f, 0.5f, out _), SdfMesher.Capsule(p, a1, a2, 0.5f, 0.25f, out _), 0.3f);
                    for (int r = -1; r <= 1; r += 2) d = SdfMesher.SMin(d, SdfMesher.Capsule(p, Vector3.up * 0.2f, new Vector3(r * 1.3f, -0.1f, Rr(-0.6f, 0.6f)), 0.4f, 0.15f, out _), 0.3f);
                    for (int b = 0; b < 5; b++)
                    {
                        Vector3 s = Vector3.Lerp(a1, a2, Rr(0f, 1f));
                        Vector3 e = s + new Vector3(Rr(-2.6f, 2.6f), Rr(0.6f, 2.2f), Rr(-1.2f, 1.2f));
                        Vector3 e2 = e + new Vector3(Rr(-1f, 1f), Rr(-0.8f, 0.6f), Rr(-0.6f, 0.6f)); // ramas que caen como garras
                        d = SdfMesher.SMin(d, SdfMesher.Capsule(p, s, e, 0.22f, 0.1f, out _), 0.15f);
                        d = SdfMesher.SMin(d, SdfMesher.Capsule(p, e, e2, 0.1f, 0.03f, out _), 0.08f);
                    }
                    return d;
                };
                var go = Part("Árbol seco", root, Sdf(tree, new Bounds(new Vector3(0f, 4.5f, 0f), new Vector3(9f, 11f, 6f)), 0.08f, "Fondo_ArbolSeco_" + t,
                    (p, n) => Color.Lerp(new Color(0.16f, 0.13f, 0.12f), new Color(0.3f, 0.25f, 0.22f), Mathf.PerlinNoise(p.x * 2f, p.y * 1.5f))), vtx);
                go.transform.position = basePos;
                go.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                go.transform.localScale = Vector3.one * R(0.9f, 1.25f);
            }
            return " enredaderas y árboles secos.";
        }
    }
}
