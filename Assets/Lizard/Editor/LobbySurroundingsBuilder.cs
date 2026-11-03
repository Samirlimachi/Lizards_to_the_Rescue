using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Mejorar alrededores del Lobby.
    /// Muelle de madera en el embarcadero, estanque de aparición bonito, setas que no chocan con rocas, matas de césped,
    /// fondo del Jefe alegre (sin árboles secos ni fuego verde) y borde de la isla con rocas y tuberías que vierten agua.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        [MenuItem("Lizards/Mejorar alrededores del Lobby (muelle, estanque, orillas, tuberías)", priority = 11)]
        public static string BuildSurroundings()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Lizard/Models", "Lobby");
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            waterMat = Mat("Agua_Cascada", () => LizardMaterials.Transparent(new Color(0.55f, 0.95f, 0.95f, 0.72f), 0.95f));

            string log = Dock() + SpawnPond() + MushroomsAwayFromRocks() + GrassTufts() + FriendlyBackdrop() + Shoreline();
            EditorSceneManager.MarkSceneDirty(l.scene);
            EditorSceneManager.SaveScene(l.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Alrededores del Lobby:" + log);
            return log;
        }

        static Material waterMat;

        static Color Stone(Vector3 p, Vector3 n)
        {
            Color c = Color.Lerp(new Color(0.5f, 0.5f, 0.56f), new Color(0.72f, 0.7f, 0.74f), N(p, 1.6f));
            if (n.y > 0.55f) c = Color.Lerp(c, new Color(0.38f, 0.62f, 0.3f), Mathf.Clamp01((n.y - 0.55f) * 3f) * (0.6f + N(p, 3f) * 0.4f)); // musgo arriba
            return c;
        }

        static Mesh Boulder(int seed, string name)
        {
            return Sdf(p =>
            {
                var r = new System.Random(seed);
                float d = SdfMesher.Ellipsoid(p, new Vector3(0, 0.3f, 0), new Vector3(1.5f, 1.1f, 1.3f));
                for (int i = 0; i < 5; i++)
                {
                    var c = new Vector3((float)r.NextDouble() * 2f - 1f, (float)r.NextDouble() * 0.9f, (float)r.NextDouble() * 2f - 1f);
                    d = SdfMesher.SMin(d, (p - c).magnitude - (0.55f + (float)r.NextDouble() * 0.35f), 0.35f);
                }
                d += (N(p, 2.5f) - 0.5f) * 0.12f;
                return Mathf.Max(d, -0.7f - p.y);
            }, new Bounds(new Vector3(0, 0.3f, 0), new Vector3(4.2f, 3.2f, 4f)), 0.07f, name, Stone);
        }

        // ---------------- Muelle ----------------

        static string Dock()
        {
            var dock = GameObject.Find("Plataforma de embarque");
            if (!dock) return "";
            var remove = new List<GameObject>();
            foreach (Transform k in dock.transform)
            {
                if (k.name.StartsWith("Baranda") || k.name.StartsWith("Muelle (madera)")) remove.Add(k.gameObject);
                else { var r = k.GetComponent<MeshRenderer>(); if (r) r.enabled = false; } // se quedan solo como colisión
            }
            foreach (var g in remove) Object.DestroyImmediate(g);
            var root = new GameObject("Muelle (madera)").transform; root.SetParent(dock.transform, false);
            root.position = new Vector3(8f, 0f, 0f);
            // Tablones, escalones, postes hundidos y barandilla de cuerda.
            var mesh = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 9; i++)
                    d = Mathf.Min(d, Box(p, new Vector3(-2.68f + i * 0.67f, 1.37f, 13.5f), new Vector3(0.3f, 0.09f, 2.05f), 0.04f)); // tablones
                for (int i = 0; i < 6; i++)
                    d = Mathf.Min(d, Box(p, new Vector3(0, 0.12f + i * 0.18f + 0.1f, 9.0f + i * 0.6f), new Vector3(1.55f, 0.07f, 0.3f), 0.04f)); // escalones
                for (int s = -1; s <= 1; s += 2)
                {
                    d = Mathf.Min(d, Box(p, new Vector3(s * 1.6f, 0.62f, 10.5f), new Vector3(0.1f, 0.12f, 1.9f), 0.03f) ); // zancas
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 post = new Vector3(s * 2.9f, 0f, 11.7f + k * 1.8f);
                        d = Mathf.Min(d, SdfMesher.Capsule(p, post + Vector3.down * 0.8f, post + Vector3.up * 2.25f, 0.17f, 0.15f, out _));
                        d = Mathf.Min(d, (p - (post + Vector3.up * 2.3f)).magnitude - 0.2f);
                    }
                    d = Mathf.Min(d, Box(p, new Vector3(s * 2.9f, 1.22f, 13.5f), new Vector3(0.12f, 0.1f, 2.1f), 0.03f)); // viga lateral
                }
                return d;
            }, new Bounds(new Vector3(0, 1f, 12.5f), new Vector3(7f, 4.2f, 9f)), 0.04f, "Muelle_Madera", Wood);
            Part("Tablones", root, mesh);
            var rope = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int s = -1; s <= 1; s += 2)
                    for (int k = 0; k < 2; k++)
                    {
                        Vector3 a = new Vector3(s * 2.9f, 2.05f, 11.7f + k * 1.8f), b = new Vector3(s * 2.9f, 2.05f, 13.5f + k * 1.8f);
                        Vector3 mid = (a + b) * 0.5f + Vector3.down * 0.3f;
                        d = Mathf.Min(d, Mathf.Min(SdfMesher.Capsule(p, a, mid, 0.05f, 0.05f, out _), SdfMesher.Capsule(p, mid, b, 0.05f, 0.05f, out _)));
                    }
                return d;
            }, new Bounds(new Vector3(0, 1.9f, 13.5f), new Vector3(6.6f, 1f, 4.6f)), 0.025f, "Muelle_Cuerda", (p, n) => new Color(0.86f, 0.76f, 0.5f));
            Part("Cuerda", root, rope);
            // Salvavidas colgado de un poste.
            var ring = Sdf(p => new Vector2(new Vector2(p.x, p.y).magnitude - 0.38f, p.z).magnitude - 0.11f, new Bounds(Vector3.zero, new Vector3(1.2f, 1.2f, 0.4f)), 0.02f, "Muelle_Salvavidas",
                (p, n) => Mathf.Repeat(Mathf.Atan2(p.y, p.x) / Mathf.PI * 2f, 1f) < 0.5f ? new Color(1f, 0.35f, 0.25f) : Color.white);
            var rg = Part("Salvavidas", root, ring); rg.transform.localPosition = new Vector3(3.08f, 1.75f, 11.7f); rg.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            return " muelle;";
        }

        // ---------------- Estanque de aparición ----------------

        static string SpawnPond()
        {
            var basin = GameObject.Find("Piscina terracota");
            if (!basin) return "";
            Vector3 c = basin.transform.position;
            basin.GetComponent<MeshRenderer>().enabled = false;
            foreach (var n in new[] { "Espuma de piscina", "Junta del borde", "Hierba de piscina" }) Kill(n, c, 8f);
            var bottom = GameObject.Find("Fondo de piscina"); if (bottom) bottom.GetComponent<Renderer>().sharedMaterial = Mat("Estanque_Fondo", () => LizardMaterials.Toon(new Color(0.3f, 0.5f, 0.45f)));
            var pad = GameObject.Find("Nenúfar de aparición"); if (pad) pad.GetComponent<Renderer>().enabled = false;
            var padBase = GameObject.Find("Soporte del nenúfar"); if (padBase) padBase.GetComponent<Renderer>().enabled = false;
            var oldWater = GameObject.Find("Agua verde de aparición"); if (oldWater) oldWater.GetComponent<Renderer>().enabled = false;

            var root = Fresh("Estanque de aparición (generado)", c);
            // Borde de piedras redondeadas con musgo, hasta la altura del borde viejo.
            var rim = Sdf(p =>
            {
                float rr = new Vector2(p.x, p.z).magnitude, a = Mathf.Atan2(p.z, p.x);
                float lumps = Mathf.Sin(a * 14f) * 0.08f + Mathf.Sin(a * 5f + 1f) * 0.06f;
                float ring = new Vector2(rr - 4.6f, (p.y - 0.75f) * 1.3f).magnitude - (0.55f + lumps);
                return Mathf.Max(ring, -p.y - 0.3f);
            }, new Bounds(new Vector3(0, 0.6f, 0), new Vector3(11.6f, 2.2f, 11.6f)), 0.06f, "Estanque_Borde", Stone);
            Part("Borde de piedra", root, rim);
            // Agua clara que ondula.
            var water = Sdf(p => Mathf.Max(new Vector2(p.x, p.z).magnitude - 4.3f, Mathf.Abs(p.y) - 0.06f), new Bounds(Vector3.zero, new Vector3(9f, 0.4f, 9f)), 0.08f, "Estanque_Agua", (p, n) => Color.white);
            var wgo = Part("Agua", root, water, Mat("Estanque_AguaMat", () => LizardMaterials.Transparent(new Color(0.45f, 0.85f, 0.8f, 0.7f), 0.95f)));
            wgo.transform.localPosition = new Vector3(0, 0.12f, 0);
            wgo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var wf = wgo.AddComponent<WaterFlow>(); wf.kind = WaterFlow.Kind.Surface; wf.amplitude = 0.03f; wf.frequency = 2.5f; wf.speed = 1.8f;
            // Nenúfar grande (con muesca) y flor rosa en el centro: ahí apareces.
            var lily = Sdf(p =>
            {
                float d = Mathf.Max(new Vector2(p.x, p.z).magnitude - 1.25f, Mathf.Abs(p.y) - 0.08f) - 0.03f;
                float notch = Mathf.Max(Mathf.Abs(p.x) - Mathf.Max(0f, p.z) * 0.35f, -p.z);
                return Mathf.Max(d, -notch);
            }, new Bounds(Vector3.zero, new Vector3(3f, 0.5f, 3f)), 0.03f, "Estanque_Nenufar", (p, n) =>
            {
                float rr = new Vector2(p.x, p.z).magnitude;
                Color g = Color.Lerp(new Color(0.3f, 0.65f, 0.25f), new Color(0.5f, 0.82f, 0.35f), rr / 1.25f);
                if (Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.z, p.x) * 6f)) < 0.08f) g *= 0.85f; // nervios
                return g;
            });
            var lg = Part("Nenúfar", root, lily); lg.transform.localPosition = new Vector3(0, 0.22f, 0); lg.transform.localRotation = Quaternion.Euler(0, 200f, 0);
            var flower = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    float a = i / 8f * Mathf.PI * 2f; Vector3 dir = new Vector3(Mathf.Cos(a), 0.6f, Mathf.Sin(a));
                    d = SdfMesher.SMin(d, SdfMesher.Ellipsoid(Quaternion.Inverse(Quaternion.LookRotation(dir)) * (p - dir * 0.18f), Vector3.zero, new Vector3(0.12f, 0.06f, 0.24f)), 0.03f);
                }
                return SdfMesher.SMin(d, (p - new Vector3(0, 0.12f, 0)).magnitude - 0.1f, 0.04f);
            }, new Bounds(new Vector3(0, 0.15f, 0), new Vector3(1f, 0.7f, 1f)), 0.015f, "Estanque_Flor",
                (p, n) => p.y > 0.14f && new Vector2(p.x, p.z).magnitude < 0.12f ? new Color(1f, 0.85f, 0.3f) : Color.Lerp(new Color(1f, 0.55f, 0.75f), Color.white, p.y * 2f));
            var fl = Part("Flor", root, flower); fl.transform.localPosition = new Vector3(0.8f, 0.3f, 0.6f);
            // Juncos con espiga (en lugar de los palitos).
            var reed = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 5; i++)
                {
                    float a = i * 1.26f; Vector3 b = new Vector3(Mathf.Cos(a) * 0.15f, 0, Mathf.Sin(a) * 0.15f), t = b * 2.2f + Vector3.up * (1.2f + (i % 3) * 0.3f);
                    d = Mathf.Min(d, SdfMesher.Capsule(p, b, t, 0.035f, 0.02f, out _));
                    if (i % 2 == 0) d = Mathf.Min(d, SdfMesher.Capsule(p, t - (t - b).normalized * 0.4f, t - (t - b).normalized * 0.08f, 0.07f, 0.07f, out _));
                }
                return d;
            }, new Bounds(new Vector3(0, 0.9f, 0), new Vector3(1.4f, 2.2f, 1.4f)), 0.015f, "Estanque_Juncos",
                (p, n) => p.y > 0.95f && new Vector2(p.x, p.z).magnitude > 0.2f && p.y < 1.75f && Mathf.Abs(new Vector2(p.x, p.z).magnitude * 0f) < 1f && n.y < 0.9f && p.y > 1.0f && ((p.y > 1.05f && p.y < 1.45f) || (p.y > 1.35f && p.y < 1.75f)) ? new Color(0.55f, 0.35f, 0.2f) : new Color(0.35f, 0.62f, 0.25f));
            for (int i = 0; i < 4; i++)
            {
                float a = 0.6f + i * 1.7f;
                var rgo = Part("Juncos", root, reed); rgo.transform.localPosition = new Vector3(Mathf.Cos(a) * 3.6f, 0.05f, Mathf.Sin(a) * 3.6f);
            }
            return " estanque de aparición;";
        }

        // ---------------- Setas y césped ----------------

        static string MushroomsAwayFromRocks()
        {
            var root = lobby.Find("Setas (generadas)");
            if (!root) return "";
            int moved = 0;
            foreach (Transform m in root)
            {
                float s = m.localScale.x;
                for (int it = 0; it < 30; it++)
                {
                    Vector3 cap = m.position + Vector3.up * 1.4f * s;
                    Collider hit = null;
                    foreach (var c in Physics.OverlapSphere(cap, 1.3f * s, ~0, QueryTriggerInteraction.Ignore))
                        if (c.name == "Roca esculpida" || c.name.StartsWith("Sauce") || c.GetComponentInParent<Transform>().name == "Sauce") { hit = c; break; }
                    if (!hit) break;
                    Vector3 away = Flat(m.position - hit.bounds.center); if (away.sqrMagnitude < 0.01f) away = Vector3.right;
                    Vector3 np = m.position + away.normalized * 0.6f;
                    if (Physics.Raycast(new Vector3(np.x, 50f, np.z), Vector3.down, out var g, 100f, ~0, QueryTriggerInteraction.Ignore) && g.collider.name.StartsWith("Terreno")) np.y = g.point.y;
                    m.position = np;
                    if (it == 0) moved++;
                }
            }
            return $" {moved} setas apartadas de las rocas;";
        }

        static string GrassTufts()
        {
            var tuft = Sdf(p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 0.7f; float h = 0.35f + (i % 3) * 0.12f;
                    Vector3 b = new Vector3(Mathf.Cos(a) * 0.07f, 0, Mathf.Sin(a) * 0.07f);
                    Vector3 m = b + new Vector3(Mathf.Cos(a) * 0.08f, h * 0.55f, Mathf.Sin(a) * 0.08f), t = m + new Vector3(Mathf.Cos(a) * 0.12f, h * 0.45f, Mathf.Sin(a) * 0.12f);
                    d = Mathf.Min(d, Mathf.Min(SdfMesher.Capsule(p, b, m, 0.035f, 0.025f, out _), SdfMesher.Capsule(p, m, t, 0.025f, 0.008f, out _)));
                }
                return d;
            }, new Bounds(new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.8f, 0.9f)), 0.012f, "Mata_Cesped",
                (p, n) => Color.Lerp(new Color(0.25f, 0.5f, 0.18f), new Color(0.6f, 0.85f, 0.35f), Mathf.Clamp01(p.y / 0.6f)));
            int n = 0;
            foreach (Transform t in lobby)
            {
                if (t.name != "Hierba") continue;
                var mf = t.GetComponent<MeshFilter>(); if (!mf) continue;
                // Las hierbas eran palitos sueltos: ahora cada una es una mata de hojas curvadas, apoyada en el suelo.
                mf.sharedMesh = tuft; t.GetComponent<MeshRenderer>().sharedMaterial = vtx;
                Vector3 p = t.position;
                if (Physics.Raycast(new Vector3(p.x, p.y + 3f, p.z), Vector3.down, out var g, 10f, ~0, QueryTriggerInteraction.Ignore) && g.collider.name.StartsWith("Terreno")) p.y = g.point.y;
                t.position = p + new Vector3(((n * 37) % 7 - 3) * 0.12f, 0f, ((n * 53) % 7 - 3) * 0.12f);
                t.rotation = Quaternion.Euler(0f, n * 41f, 0f); t.localScale = Vector3.one * (1.2f + (n % 3) * 0.25f);
                foreach (var c in t.GetComponents<Collider>()) Object.DestroyImmediate(c);
                n++;
            }
            return $" {n} matas de césped;";
        }

        // ---------------- Fondo del Jefe (alegre) ----------------

        static string FriendlyBackdrop()
        {
            var back = lobby.Find("Fondo tenebroso (generado)");
            int removed = 0;
            if (back)
            {
                back.name = "Fondo del Jefe (generado)";
                for (int i = back.childCount - 1; i >= 0; i--) if (back.GetChild(i).name.StartsWith("Árbol seco")) { Object.DestroyImmediate(back.GetChild(i).gameObject); removed++; }
            }
            back = back ? back : lobby.Find("Fondo del Jefe (generado)");
            if (!back) { back = new GameObject("Fondo del Jefe (generado)").transform; back.SetParent(lobby, false); }
            for (int i = back.childCount - 1; i >= 0; i--) if (back.GetChild(i).name == "Árbol" || back.GetChild(i).name == "Macizo de flores") Object.DestroyImmediate(back.GetChild(i).gameObject);
            var trunk = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Sauce_Tronco_0.asset"); var leaves = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Sauce_Hojas_1.asset");
            Vector3[] spots = { new(-13f, 0f, 41f), new(13f, 0f, 41.5f), new(-22f, 0f, 39f), new(22f, 0f, 40f) };
            int k = 0;
            foreach (var s in spots)
            {
                Vector3 p = s; if (Physics.Raycast(new Vector3(p.x, 50f, p.z), Vector3.down, out var g, 100f, ~0, QueryTriggerInteraction.Ignore)) p.y = g.point.y;
                var tree = new GameObject("Árbol").transform; tree.SetParent(back, false); tree.position = p; tree.rotation = Quaternion.Euler(0, k * 70f, 0); tree.localScale = Vector3.one * (1.1f + (k % 2) * 0.2f);
                if (trunk) Part("Tronco", tree, trunk); if (leaves) Part("Copa", tree, leaves);
                k++;
            }
            // Macizos de flores de colores a los pies de la fábrica.
            var flowers = Sdf(p =>
            {
                float d = SdfMesher.Ellipsoid(p, Vector3.zero, new Vector3(1.6f, 0.45f, 0.9f));
                for (int i = 0; i < 14; i++) { float a = i * 2.4f, r = 0.3f + (i % 4) * 0.35f; d = SdfMesher.SMin(d, (p - new Vector3(Mathf.Cos(a) * r * 1.4f, 0.4f, Mathf.Sin(a) * r * 0.7f)).magnitude - 0.17f, 0.05f); }
                return Mathf.Max(d, -p.y - 0.1f);
            }, new Bounds(new Vector3(0, 0.25f, 0), new Vector3(3.8f, 1.2f, 2.4f)), 0.03f, "Fondo_Flores", (p, n) =>
            {
                if (p.y < 0.38f) return new Color(0.3f, 0.6f, 0.25f);
                float h = Mathf.Repeat(Mathf.Floor(p.x * 2.3f) * 0.37f + Mathf.Floor(p.z * 2.1f) * 0.21f, 1f);
                return h < 0.33f ? new Color(1f, 0.55f, 0.7f) : h < 0.66f ? new Color(1f, 0.85f, 0.3f) : new Color(0.7f, 0.6f, 1f);
            });
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(-20f + i * 8f, 0f, 44.6f);
                if (Physics.Raycast(new Vector3(p.x, 50f, p.z), Vector3.down, out var g, 100f, ~0, QueryTriggerInteraction.Ignore)) p.y = g.point.y;
                var f = Part("Macizo de flores", back, flowers); f.transform.position = p;
            }
            // Fuego de los braseros cálido (no verde).
            int fires = 0;
            foreach (var name in new[] { "Trono_Fuego", "Trono_FuegoNucleo" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Lizard/Models/Jefe/{name}.mat");
                if (m) { m.SetColor("_BaseColor", name.EndsWith("Nucleo") ? new Color(1f, 0.95f, 0.6f) : new Color(1f, 0.6f, 0.2f, 0.9f)); EditorUtility.SetDirty(m); fires++; }
            }
            foreach (var li in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
                if (li.name == "Luz" && li.transform.parent && li.transform.parent.name == "Brasero") li.color = new Color(1f, 0.7f, 0.35f);
            return $" fondo del Jefe ({removed} árboles secos → árboles con hojas y flores, fuego cálido);";
        }

        // ---------------- Orillas: rocas y tuberías con cascadas ----------------

        static string Shoreline()
        {
            var old = lobby.Find("Orillas (generadas)"); if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Orillas (generadas)").transform; root.SetParent(lobby, false);
            var boulders = new[] { Boulder(11, "Orilla_Roca_0"), Boulder(23, "Orilla_Roca_1"), Boulder(37, "Orilla_Roca_2") };
            float sea = -0.65f;
            var terrain = GameObject.Find("Terreno · lomas y ensenada").GetComponent<Collider>();
            Vector3 center = new Vector3(0f, 0f, 5f);

            // Busca la orilla en cada dirección: último punto con tierra por encima del agua.
            var shore = new List<(Vector3 p, Vector3 dir)>();
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f; Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 last = Vector3.zero; bool any = false;
                for (float d = 8f; d < 90f; d += 1f)
                {
                    Vector3 q = center + dir * d;
                    if (!terrain.Raycast(new Ray(new Vector3(q.x, 60f, q.z), Vector3.down), out var h, 120f)) break;
                    if (h.point.y < sea + 0.05f) break;
                    last = h.point; any = true;
                }
                if (any) shore.Add((last, dir));
            }

            int rocks = 0, pipes = 0;
            for (int i = 0; i < shore.Count; i++)
            {
                var (p, dir) = shore[i];
                // Rocas en la orilla, medio metidas en el agua.
                var rock = Part("Roca de orilla", root, boulders[i % 3]);
                rock.transform.position = new Vector3(p.x, sea + 0.1f, p.z) + dir * (1.2f + (i % 3) * 0.6f);
                rock.transform.rotation = Quaternion.Euler(0f, i * 53f, 0f);
                rock.transform.localScale = Vector3.one * (0.8f + (i * 7 % 5) * 0.18f);
                rocks++;
                if (i % 2 == 1)
                {
                    var r2 = Part("Roca de orilla", root, boulders[(i + 1) % 3]);
                    r2.transform.position = new Vector3(p.x, sea, p.z) + dir * 3.2f + Vector3.Cross(Vector3.up, dir) * 1.6f;
                    r2.transform.localScale = Vector3.one * 0.55f; rocks++;
                }
                // Cada 8 direcciones, una tubería de alcantarilla sobre una roca grande que vierte agua al mar.
                if (i % 8 == 3) { Pipe(root, p, dir, sea, boulders[i % 3]); pipes++; }
            }
            return $" orillas con {rocks} rocas y {pipes} tuberías con cascada.";
        }

        static Mesh pipeMesh, streamMesh, foamMesh, grateMesh;
        static void Pipe(Transform root, Vector3 shorePoint, Vector3 dir, float sea, Mesh boulder)
        {
            if (!pipeMesh)
            {
                pipeMesh = Sdf(p =>
                {
                    // Tubo que sale hacia +Z, con boca abierta, anillos y remaches.
                    float r = new Vector2(p.x, p.y).magnitude;
                    float tube = Mathf.Max(Mathf.Abs(r - 0.62f) - 0.1f, Mathf.Abs(p.z - 1.2f) - 1.4f);
                    for (int k = 0; k < 3; k++) tube = Mathf.Min(tube, Mathf.Max(Mathf.Abs(r - 0.7f) - 0.08f, Mathf.Abs(p.z - (0.2f + k * 1.0f)) - 0.12f));
                    tube = Mathf.Min(tube, Mathf.Max(Mathf.Abs(r - 0.72f) - 0.12f, Mathf.Abs(p.z - 2.55f) - 0.1f)); // boca
                    return tube;
                }, new Bounds(new Vector3(0, 0, 1.2f), new Vector3(2f, 2f, 3.4f)), 0.035f, "Orilla_Tuberia", (p, n) =>
                {
                    float r = new Vector2(p.x, p.y).magnitude;
                    if (r < 0.6f) return new Color(0.12f, 0.18f, 0.2f);
                    Color c = Color.Lerp(new Color(0.35f, 0.75f, 0.68f), new Color(0.55f, 0.9f, 0.8f), Mathf.Clamp01(n.y * 0.5f + 0.5f));
                    if (r > 0.75f) c *= 0.85f;
                    return c;
                });
                // Chorro: sale horizontal y cae en curva hasta el mar (eje vertical local = columna de agua).
                streamMesh = Sdf(p =>
                {
                    float d = float.MaxValue;
                    Vector3 prev = new Vector3(0, 0, -0.9f);
                    for (int k = 1; k <= 10; k++)
                    {
                        float t = k / 10f;
                        Vector3 q = new Vector3(0, -t * t * 3.2f, -0.9f + Mathf.Sqrt(t) * 0.9f);
                        d = Mathf.Min(d, SdfMesher.Capsule(p, prev, q, 0.42f + t * 0.06f, 0.42f + t * 0.08f, out _));
                        prev = q;
                    }
                    return d;
                }, new Bounds(new Vector3(0, -1.6f, -0.4f), new Vector3(1.6f, 4.4f, 2f)), 0.05f, "Orilla_Chorro", (p, n) => Color.white);
                foamMesh = Sdf(p => Mathf.Max(new Vector2(new Vector2(p.x, p.z).magnitude - 0.9f, p.y).magnitude - 0.22f, -p.y - 0.05f) + (Mathf.Sin(Mathf.Atan2(p.z, p.x) * 9f) * 0.05f),
                    new Bounds(Vector3.zero, new Vector3(2.8f, 0.6f, 2.8f)), 0.03f, "Orilla_Espuma", (p, n) => new Color(0.95f, 1f, 1f));
                grateMesh = Sdf(p =>
                {
                    float disc = Mathf.Max(new Vector2(p.x, p.y).magnitude - 0.58f, Mathf.Abs(p.z) - 0.04f);
                    float bars = float.MaxValue;
                    for (int k = -2; k <= 2; k++) bars = Mathf.Min(bars, Box(p, new Vector3(k * 0.22f, 0, 0), new Vector3(0.04f, 0.6f, 0.05f), 0.01f));
                    return Mathf.Min(Mathf.Max(bars, new Vector2(p.x, p.y).magnitude - 0.58f), Mathf.Max(Mathf.Abs(new Vector2(p.x, p.y).magnitude - 0.6f) - 0.06f, Mathf.Abs(p.z) - 0.06f)) + 0f * disc;
                }, new Bounds(Vector3.zero, new Vector3(1.6f, 1.6f, 0.3f)), 0.02f, "Orilla_Rejilla", (p, n) => new Color(0.25f, 0.3f, 0.32f));
            }
            var g = new GameObject("Tubería con cascada").transform; g.SetParent(root, false);
            g.position = new Vector3(shorePoint.x, sea, shorePoint.z) + dir * 1.5f;
            g.rotation = Quaternion.LookRotation(dir);
            var mound = Part("Roca", g, boulder); mound.transform.localPosition = new Vector3(0, 0.3f, -0.4f); mound.transform.localScale = new Vector3(1.6f, 1.9f, 1.4f);
            var pipe = Part("Tubo", g, pipeMesh); pipe.transform.localPosition = new Vector3(0, 2.6f, -0.8f);
            var grate = Part("Rejilla", g, grateMesh); grate.transform.localPosition = new Vector3(0, 2.6f, 0.95f);
            grate.transform.localScale = Vector3.one * 0.9f;
            Object.DestroyImmediate(grate); // la boca queda abierta: el agua sale por ella
            var stream = Part("Chorro", g, streamMesh, waterMat); stream.transform.localPosition = new Vector3(0, 2.6f, 2.65f);
            stream.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var wf = stream.AddComponent<WaterFlow>(); wf.kind = WaterFlow.Kind.Curtain; wf.amplitude = 0.06f; wf.frequency = 5f; wf.speed = 10f;
            var splash = new GameObject("Salpicadura").transform; splash.SetParent(g, false); splash.localPosition = new Vector3(0, 0.05f, 2.65f);
            var foam = Part("Espuma", splash, foamMesh); foam.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var ws = splash.gameObject.AddComponent<WaterfallSplash>(); ws.foam = foam.transform; ws.radius = 0.7f;
        }
    }
}
