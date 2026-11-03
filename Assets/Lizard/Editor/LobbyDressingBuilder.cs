using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Vegetación, rampa del cuenco y túnel de salida.
    /// Llena el suelo (matas, flores, arbustos, losas del camino), coloca una rampa oscura con franja de peligro que se asoma
    /// al cuenco donde come el Jefe y construye un gran túnel de alcantarilla en la orilla por donde sale la balsa.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        [MenuItem("Lizards/Vegetación, rampa del cuenco y túnel de salida", priority = 20)]
        public static string BuildDressing()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            string log = PathSlabs() + FeedingRamp() + ExitTunnel() + Vegetation(); // la vegetación al final para que esquive lo nuevo
            EditorSceneManager.MarkSceneDirty(l.scene); EditorSceneManager.SaveScene(l.scene); AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Ambiente:" + log);
            return log;
        }

        static bool OnGrass(Vector3 p, out Vector3 hit, out Vector3 normal)
        {
            hit = p; normal = Vector3.up;
            if (!Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (!h.collider.name.StartsWith("Terreno") || h.point.y < 0.35f || h.normal.y < 0.82f) return false;
            hit = h.point; normal = h.normal; return true;
        }

        // ---------------- Losas del camino ----------------

        static string PathSlabs()
        {
            var slab = Sdf(p => Box(p, Vector3.zero, new Vector3(0.55f, 0.05f, 0.45f), 0.05f) + (N(p, 6f) - 0.5f) * 0.02f,
                new Bounds(Vector3.zero, new Vector3(1.4f, 0.3f, 1.2f)), 0.03f, "Amb_Losa", (p, n) => n.y > 0.5f ? Color.Lerp(new Color(0.86f, 0.78f, 0.6f), new Color(0.94f, 0.88f, 0.72f), N(p, 3f)) : new Color(0.66f, 0.58f, 0.44f));
            int n = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>())
                if (mf.name == "Piedra del camino") { mf.sharedMesh = slab; mf.GetComponent<MeshRenderer>().sharedMaterial = vtx; var s = mf.transform.localScale; mf.transform.localScale = new Vector3(s.x, 1f, s.z); n++; }
            return $" {n} losas;";
        }

        // ---------------- Vegetación ----------------

        static string Vegetation()
        {
            var old = lobby.Find("Vegetación (generada)"); if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Vegetación (generada)").transform; root.SetParent(lobby, false);
            var grass = new List<Mesh>();
            foreach (var nm in new[] { "Kit_Pasto1", "Kit_Pasto2", "Kit_Pasto4" }) { var m = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/{nm}.asset"); if (m) grass.Add(m); }
            var berries = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Kit_Pasto3.asset");
            var bushes = new List<Mesh>(); for (int i = 0; i < 3; i++) { var m = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Arbusto_{i}.asset"); if (m) bushes.Add(m); }
            var leafy = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Kit_Matita.asset");
            Color[] petals = { new(1f, 0.95f, 0.95f), new(1f, 0.85f, 0.25f), new(1f, 0.55f, 0.7f), new(0.65f, 0.6f, 1f) };
            var flowers = new Mesh[petals.Length];
            for (int k = 0; k < petals.Length; k++)
            {
                Color pc = petals[k];
                flowers[k] = Sdf(p =>
                {
                    float d = SdfMesher.Capsule(p, Vector3.zero, new Vector3(0, 0.32f, 0), 0.018f, 0.015f, out _);
                    for (int i = 0; i < 5; i++) { float a = i / 5f * Mathf.PI * 2f; d = Mathf.Min(d, SdfMesher.Ellipsoid(p, new Vector3(Mathf.Cos(a) * 0.06f, 0.34f, Mathf.Sin(a) * 0.06f), new Vector3(0.055f, 0.02f, 0.055f))); }
                    d = Mathf.Min(d, (p - new Vector3(0, 0.36f, 0)).magnitude - 0.035f);
                    d = Mathf.Min(d, SdfMesher.Ellipsoid(p, new Vector3(0.05f, 0.1f, 0), new Vector3(0.06f, 0.015f, 0.03f)));
                    return d;
                }, new Bounds(new Vector3(0, 0.2f, 0), new Vector3(0.4f, 0.5f, 0.4f)), 0.008f, "Amb_Flor_" + k, (p, n) =>
                    p.y > 0.33f ? ((new Vector2(p.x, p.z)).magnitude < 0.035f && p.y > 0.35f ? new Color(1f, 0.8f, 0.2f) : pc) : new Color(0.3f, 0.62f, 0.24f));
            }
            // Zonas a evitar: estaciones y caminos.
            var avoid = new List<Bounds>();
            foreach (Transform t in lobby)
            {
                if (t == root || t.name.StartsWith("Terreno") || t.name.StartsWith("Agua") || t.name.StartsWith("Horizonte") || t.name.StartsWith("Islotes") || t.name.StartsWith("Orillas")) continue;
                foreach (var r in t.GetComponentsInChildren<Renderer>()) { var b = r.bounds; if (b.size.x < 30f && b.size.z < 30f && b.size.y > 0.15f) avoid.Add(b); }
            }
            var rnd = new System.Random(2024);
            int placed = 0;
            for (int i = 0; i < 2600 && placed < 520; i++)
            {
                Vector3 p = new Vector3((float)rnd.NextDouble() * 96f - 48f, 0, (float)rnd.NextDouble() * 92f - 42f);
                if (!OnGrass(p, out var hit, out var nrm)) continue;
                bool blocked = false;
                foreach (var b in avoid) { var e = b; e.Expand(new Vector3(1.2f, 50f, 1.2f)); if (e.Contains(hit)) { blocked = true; break; } }
                if (blocked) continue;
                // Agrupar: las flores en manchas, las matas en todas partes.
                float patch = Mathf.PerlinNoise(hit.x * 0.12f + 4f, hit.z * 0.12f + 9f);
                double roll = rnd.NextDouble();
                Mesh mesh; float scale;
                if (patch > 0.6f && roll < 0.55) { mesh = flowers[(int)(Mathf.PerlinNoise(hit.x * 0.05f, hit.z * 0.05f) * 3.99f)]; scale = 1.1f + (float)rnd.NextDouble() * 0.6f; }
                else if (roll < 0.62) { if (grass.Count == 0) continue; mesh = grass[rnd.Next(grass.Count)]; scale = 1.3f + (float)rnd.NextDouble() * 1f; }
                else if (roll < 0.78 && bushes.Count > 0) { mesh = bushes[rnd.Next(bushes.Count)]; scale = 1.2f + (float)rnd.NextDouble() * 0.9f; }
                else if (roll < 0.9 && leafy) { mesh = leafy; scale = 1.4f + (float)rnd.NextDouble() * 0.8f; }
                else if (berries) { mesh = berries; scale = 1.2f + (float)rnd.NextDouble() * 0.6f; }
                else continue;
                var go = Part("Planta", root, mesh);
                go.transform.position = hit - Vector3.up * 0.03f;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, nrm) * Quaternion.Euler(0, (float)rnd.NextDouble() * 360f, 0);
                go.transform.localScale = Vector3.one * scale;
                go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                placed++;
            }
            return $" {placed} plantas, flores y arbustos;";
        }

        // ---------------- Rampa que se asoma al cuenco ----------------

        static string FeedingRamp()
        {
            foreach (var n in new[] { "Plataforma de mantenimiento", "Rampa de mantenimiento" })
            {
                var g = GameObject.Find(n); if (!g) continue;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.enabled = false;
                foreach (var c in g.GetComponentsInChildren<Collider>()) c.enabled = false;
            }
            var bowl = lobby.Find("Cuenco de recolección (generado)");
            Vector3 bc = bowl ? bowl.position : new Vector3(-5f, 0f, 24f);
            var old = lobby.Find("Rampa del cuenco (generada)"); if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Rampa del cuenco (generada)").transform; root.SetParent(lobby, false);
            // Mirando al cuenco desde el sur-oeste: sube desde el suelo hasta un balcón sobre el borde.
            Vector3 toBowl = new Vector3(0.55f, 0f, 1f).normalized;
            root.position = bc - toBowl * 15.6f; root.rotation = Quaternion.LookRotation(toBowl); // el balcón termina justo en el borde del cuenco, sin taparlo
            const float W = 3.4f, Len = 6.5f, TopY = 2.9f;
            var ramp = Sdf(p =>
            {
                // Rampa inclinada + balcón plano al final + soportes.
                float slope = Mathf.Max(Box(p, new Vector3(0, TopY * 0.5f, Len * 0.5f), new Vector3(W, TopY * 0.5f + 0.2f, Len * 0.5f), 0.12f), (p.y - (p.z / Len) * TopY) - 0.25f);
                float deck = Box(p, new Vector3(0, TopY - 0.12f, Len + 1.6f), new Vector3(W, 0.22f, 1.7f), 0.12f);
                float legs = Mathf.Min(Box(p, new Vector3(-W + 0.4f, TopY * 0.5f - 0.3f, Len + 2.8f), new Vector3(0.3f, TopY * 0.5f, 0.3f), 0.06f), Box(p, new Vector3(W - 0.4f, TopY * 0.5f - 0.3f, Len + 2.8f), new Vector3(0.3f, TopY * 0.5f, 0.3f), 0.06f));
                return Mathf.Min(Mathf.Min(slope, deck), legs);
            }, new Bounds(new Vector3(0, TopY * 0.5f, Len * 0.5f + 1.6f), new Vector3(W * 2f + 0.6f, TopY + 1f, Len + 4.6f)), 0.06f, "Amb_RampaCuenco", (p, n) =>
            {
                bool top = n.y > 0.5f;
                if (top && Mathf.Abs(Mathf.Repeat(p.z + 0.6f, 1.2f) - 0.6f) < 0.03f) return new Color(0.13f, 0.14f, 0.17f); // juntas de placas
                return Color.Lerp(new Color(0.2f, 0.21f, 0.26f), new Color(0.3f, 0.31f, 0.37f), top ? 0.85f : 0.3f + N(p, 2f) * 0.3f);
            });
            Part("Rampa", root, ramp).AddComponent<MeshCollider>();
            // Franjas de peligro como piezas aparte (bordes limpios, sin dientes de sierra).
            var yellow = Mat("Rampa_Amarillo", () => LizardMaterials.Toon(new Color(1f, 0.74f, 0.14f), 0f, 0.3f));
            var black = Mat("Rampa_Negro", () => LizardMaterials.Toon(new Color(0.11f, 0.11f, 0.13f), 0f, 0.2f));
            var metal = Mat("Rampa_Metal", () => LizardMaterials.Toon(new Color(0.62f, 0.65f, 0.7f), 0f, 0.6f));
            var tile = Sdf(p => Box(p, Vector3.zero, new Vector3(0.24f, 0.03f, 0.2f), 0.02f), new Bounds(Vector3.zero, new Vector3(0.7f, 0.2f, 0.6f)), 0.02f, "Rampa_Teja", (p, nn) => Color.white);
            void Stripe(float z, float y, float tilt)
            {
                for (int i = 0; i < 14; i++)
                {
                    var g = Part("Franja", root, tile, i % 2 == 0 ? yellow : black);
                    g.transform.localPosition = new Vector3(-W + 0.25f + i * 0.49f, y, z); g.transform.localRotation = Quaternion.Euler(-tilt, 0, 0);
                }
            }
            float slopeAng = Mathf.Atan2(TopY, Len) * Mathf.Rad2Deg;
            Stripe(0.45f, 0.25f + 0.45f / Len * TopY + 0.04f, slopeAng);   // pie de la rampa
            Stripe(Len - 0.1f, TopY + 0.13f, 0f);                          // inicio del balcón
            Stripe(Len + 3.05f, TopY + 0.13f, 0f);                         // borde del balcón (frente al cuenco)
            // Barandillas metálicas a los lados con postes y remaches.
            var rail = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 a = new Vector3(s * (W - 0.15f), 0.25f + 1.0f, 0.3f), b = new Vector3(s * (W - 0.15f), TopY + 1.0f, Len), c2 = new Vector3(s * (W - 0.15f), TopY + 1.0f, Len + 3.1f);
                    d = Mathf.Min(d, Mathf.Min(SdfMesher.Capsule(p, a, b, 0.07f, 0.07f, out _), SdfMesher.Capsule(p, b, c2, 0.07f, 0.07f, out _)));
                    for (int k = 0; k <= 4; k++)
                    {
                        Vector3 top2 = k < 3 ? Vector3.Lerp(a, b, k / 2f) : Vector3.Lerp(b, c2, (k - 2) / 2f);
                        d = Mathf.Min(d, SdfMesher.Capsule(p, top2 - Vector3.up * 1.0f, top2, 0.06f, 0.06f, out _));
                    }
                }
                return d;
            }, new Bounds(new Vector3(0, TopY * 0.6f + 0.5f, Len * 0.5f + 1.5f), new Vector3(W * 2f + 0.6f, TopY + 2.4f, Len + 4f)), 0.035f, "Rampa_Barandilla", (p, nn) => Color.white);
            var rg = Part("Barandillas", root, rail, yellow);
            // Remaches en el balcón, focos en las esquinas y caja de comida con frutas.
            var sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            for (int i = 0; i < 6; i++) for (int s = -1; s <= 1; s += 2) { var r = Part("Remache", root, sphere, metal); r.transform.localPosition = new Vector3(s * (W - 0.55f), TopY + 0.11f, Len + 0.4f + i * 0.5f); r.transform.localScale = new Vector3(0.12f, 0.05f, 0.12f); }
            var bulb = Mat("Rampa_Foco", () => LizardMaterials.Lit(new Color(1f, 0.9f, 0.6f), 0.5f, new Color(1.5f, 1.2f, 0.6f)));
            for (int s = -1; s <= 1; s += 2)
            {
                var lamp = Part("Foco", root, sphere, bulb); lamp.transform.localPosition = new Vector3(s * (W - 0.15f), TopY + 1.25f, Len + 3.1f); lamp.transform.localScale = Vector3.one * 0.32f;
                var li = new GameObject("Luz").AddComponent<Light>(); li.transform.SetParent(lamp.transform, false); li.type = LightType.Point; li.color = new Color(1f, 0.85f, 0.55f); li.range = 6f; li.intensity = 1.2f; li.shadows = LightShadows.None;
            }
            var crate = Sdf(p => Mathf.Max(Box(p, Vector3.zero, new Vector3(0.5f, 0.35f, 0.4f), 0.04f), -Box(p, new Vector3(0, 0.2f, 0), new Vector3(0.42f, 0.35f, 0.32f), 0.02f)),
                new Bounds(Vector3.zero, new Vector3(1.2f, 0.9f, 1f)), 0.02f, "Rampa_Caja", (p, nn) => Mathf.Abs(Mathf.Repeat(p.y + 0.35f, 0.23f) - 0.115f) < 0.015f ? new Color(0.35f, 0.22f, 0.12f) : new Color(0.62f, 0.42f, 0.24f));
            var cr = Part("Caja de comida", root, crate); cr.transform.localPosition = new Vector3(-W + 0.9f, TopY + 0.45f, Len + 1.2f); cr.AddComponent<BoxCollider>().size = new Vector3(1f, 0.7f, 0.8f);
            var fruit = Mat("Rampa_Fruta", () => LizardMaterials.Toon(new Color(1f, 0.4f, 0.3f), 0f, 0.5f));
            for (int i = 0; i < 4; i++) { var f = Part("Fruta", cr.transform, sphere, fruit); f.transform.localPosition = new Vector3(-0.22f + (i % 2) * 0.42f, 0.32f, -0.12f + (i / 2) * 0.24f); f.transform.localScale = Vector3.one * 0.26f; }
            // Pegotes de baba verde encima (como restos de comida del Jefe).
            var goo = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Kit_Pasto4.asset");
            if (goo) { var gm = Mat("Amb_Baba", () => LizardMaterials.Toon(new Color(0.45f, 0.9f, 0.3f), 0f, 0.7f)); var rnd = new System.Random(8); for (int i = 0; i < 4; i++) { var g = Part("Baba", root, goo, gm); g.transform.localPosition = new Vector3((float)rnd.NextDouble() * 4f - 2f, TopY + 0.12f, Len + 0.6f + (float)rnd.NextDouble() * 2f); g.transform.localScale = Vector3.one * 2.4f; } }
            return " rampa que se asoma al cuenco;";
        }

        // ---------------- Túnel de salida de la balsa ----------------

        static string ExitTunnel()
        {
            var raft = GameObject.Find("Balsa naranja de salida");
            Vector3 start = raft ? raft.transform.position : new Vector3(2f, 0f, 18f);
            var old = lobby.Find("Túnel de salida (generado)"); if (old) Object.DestroyImmediate(old.gameObject);
            // Busca, desde la balsa, la dirección de agua más larga y pon el túnel donde termina (en la orilla de enfrente o mar adentro).
            Vector3 bestDir = Vector3.forward; float bestLen = 0f;
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f; Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); float len = 0f;
                for (float s = 4f; s < 40f; s += 1f)
                {
                    Vector3 q = start + d * s;
                    if (Physics.Raycast(new Vector3(q.x, 60f, q.z), Vector3.down, out var h, 120f, ~0, QueryTriggerInteraction.Ignore) && h.collider.name.StartsWith("Terreno") && h.point.y > -0.5f) break;
                    len = s;
                }
                if (d.x < 0.6f || d.z > 0.5f) continue; // hacia el este de la ensenada: ni detrás del Jefe ni hacia la plaza
                if (len > bestLen) { bestLen = len; bestDir = d; }
            }
            float dist = bestLen + 1.5f; // la boca justo en la orilla, el agua entra
            dist = Mathf.Min(dist, 15f); bestDir = new Vector3(0.92f, 0f, 0.38f).normalized; // posición fija (el canal está excavado aquí)
            var root = new GameObject("Túnel de salida (generado)").transform; root.SetParent(lobby, false);
            root.position = new Vector3(start.x, -0.65f, start.z) + bestDir * dist; root.rotation = Quaternion.LookRotation(bestDir); // la cara -Z (boca con anillo) mira a la balsa
            const float R = 4.6f, Wall = 9.5f, H = 10f;
            var wall = Sdf(p =>
            {
                float block = Box(p, new Vector3(0, H * 0.5f - 1.5f, 2.2f), new Vector3(Wall, H * 0.5f + 1.5f, 2.2f), 0.2f);
                float hole = Mathf.Max(new Vector2(p.x, p.y).magnitude - R, -p.y - 3f);
                float ring = Mathf.Max(Mathf.Max(new Vector2(p.x, p.y).magnitude - (R + 0.9f), R - new Vector2(p.x, p.y).magnitude), Mathf.Max(Mathf.Abs(p.z + 0.3f) - 0.5f, -p.y - 1.5f));
                float d = Mathf.Min(SMax(block, -hole, 0.1f), ring);
                d = Mathf.Min(d, Box(p, new Vector3(0, H - 0.6f, 1.6f), new Vector3(Wall + 0.4f, 0.35f, 2.8f), 0.1f)); // cornisa
                return d;
            }, new Bounds(new Vector3(0, H * 0.5f - 1.5f, 2f), new Vector3(Wall * 2f + 1.6f, H + 4f, 6f)), 0.08f, "Amb_TunelMuro", (p, n) =>
            {
                float rr = new Vector2(p.x, p.y).magnitude;
                if (p.z < 0.25f && rr > R - 0.05f && rr < R + 0.95f) return Mathf.Repeat(Mathf.Atan2(p.y, p.x) / Mathf.PI * 7f, 1f) < 0.5f ? new Color(1f, 0.72f, 0.12f) : new Color(0.12f, 0.12f, 0.14f); // anillo de peligro
                if (rr < R + 0.1f && p.z > 0.2f) return new Color(0.2f, 0.21f, 0.25f);
                if (n.y > 0.6f) return new Color(0.38f, 0.74f, 0.3f);
                Color c = Color.Lerp(new Color(0.42f, 0.42f, 0.48f), new Color(0.55f, 0.55f, 0.6f), N(p, 0.8f));
                if (Mathf.Abs(Mathf.Repeat(p.y, 1.6f) - 0.8f) > 0.76f) c *= 0.85f; // juntas de paneles
                return c;
            });
            var wg = Part("Muro", root, wall); wg.AddComponent<MeshCollider>();
            var inner = Sdf(p => Mathf.Max(Mathf.Max(new Vector2(p.x, p.y).magnitude - R, -p.y - 3f), Mathf.Abs(p.z - 4.5f) - 0.1f), new Bounds(new Vector3(0, 1f, 4.5f), new Vector3(R * 2f + 0.4f, R + 4f, 0.6f)), 0.08f, "Amb_TunelFondo", (p, n) => new Color(0.04f, 0.05f, 0.07f));
            Part("Oscuridad", root, inner);
            // Farol encima de la boca y tubos verdes a los lados vertiendo agua.
            var lamp = Sdf(p => SdfMesher.SMin(SdfMesher.Ellipsoid(p, Vector3.zero, new Vector3(0.55f, 0.3f, 0.35f)), Box(p, new Vector3(0, 0.25f, 0.2f), new Vector3(0.65f, 0.12f, 0.35f), 0.05f), 0.05f),
                new Bounds(Vector3.zero, new Vector3(1.6f, 1f, 1f)), 0.02f, "Amb_TunelFarol", (p, n) => p.y < 0.05f ? new Color(1f, 0.92f, 0.55f) : new Color(0.2f, 0.2f, 0.24f));
            var lg = Part("Farol", root, lamp); lg.transform.localPosition = new Vector3(0, R + 1.6f, -0.9f);
            var li = new GameObject("Luz").AddComponent<Light>(); li.transform.SetParent(lg.transform, false); li.type = LightType.Point; li.color = new Color(1f, 0.85f, 0.55f); li.range = 12f; li.intensity = 2f; li.shadows = LightShadows.None;
            var pipeMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Pulido_Tuberia.asset");
            var streamMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Pulido_Chorro.asset");
            var wMat = AssetDatabase.LoadAssetAtPath<Material>($"{Dir}/Agua_Cascada.mat");
            for (int s = -1; s <= 1 && pipeMesh; s += 2)
            {
                var t = Part("Tubo lateral", root, pipeMesh); t.transform.localPosition = new Vector3(s * 7.2f, 3.2f, 1.6f); t.transform.localRotation = Quaternion.Euler(0, 180f, 0); t.transform.localScale = Vector3.one * 0.8f;
                if (streamMesh && wMat) { var st = Part("Chorro", root, streamMesh, wMat); st.transform.localPosition = new Vector3(s * 7.2f, 3.2f, -0.5f); st.transform.localRotation = Quaternion.Euler(0, 180f, 0); st.transform.localScale = Vector3.one * 0.8f; st.AddComponent<WaterFlow>().kind = WaterFlow.Kind.Curtain; }
            }
            // Canal de agua desde la ensenada hasta dentro del túnel (por encima de la franja de tierra).
            float top = -0.65f;
            for (float z = -7f; z <= 3f; z += 1f)
                for (float x = -R; x <= R; x += 1f)
                {
                    Vector3 q = root.TransformPoint(new Vector3(x, 0, z));
                    if (Physics.Raycast(new Vector3(q.x, 60f, q.z), Vector3.down, out var gh, 120f, ~0, QueryTriggerInteraction.Ignore) && gh.collider.name.StartsWith("Terreno")) top = Mathf.Max(top, gh.point.y);
                }
            var seaMat = GameObject.Find("Agua de la ensenada")?.GetComponent<Renderer>().sharedMaterial;
            var canal = Sdf(p => Box(p, Vector3.zero, new Vector3(R - 0.2f, 0.04f, 5.5f), 0.03f), new Bounds(Vector3.zero, new Vector3(R * 2f, 0.4f, 11.6f)), 0.12f, "Amb_TunelCanal", (p, n) => Color.white);
            var cg = Part("Canal", root, canal, seaMat ? seaMat : vtx);
            cg.transform.position = root.TransformPoint(new Vector3(0, 0, -2.5f)) + Vector3.up * (top + 0.65f + 0.06f);
            var banks = Sdf(p => Mathf.Min(Box(p, new Vector3(-R - 0.2f, 0, 0), new Vector3(0.45f, 0.35f, 5.5f), 0.15f), Box(p, new Vector3(R + 0.2f, 0, 0), new Vector3(0.45f, 0.35f, 5.5f), 0.15f)),
                new Bounds(Vector3.zero, new Vector3(R * 2f + 2f, 1f, 11.6f)), 0.06f, "Amb_TunelOrillas", Stone);
            var bg = Part("Orillas del canal", root, banks); bg.transform.position = cg.transform.position;
            // Cartel sobre la balsa apuntando al túnel.
            return $" túnel de salida a {dist:0} m de la balsa.";
        }
    }
}
