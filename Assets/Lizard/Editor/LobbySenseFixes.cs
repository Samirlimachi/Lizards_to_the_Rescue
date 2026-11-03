using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Arreglar orillas, muro, plantas y aparición.
    /// Las tuberías salen de muros de contención de piedra (no flotan), la zona tras el muro de la fábrica queda cerrada
    /// y tapada con rocas grandes, las plantas y hongos se apoyan en el suelo y el jugador aparece sobre el nenúfar.
    /// </summary>
    public static partial class LobbyPropsBuilder
    {
        [MenuItem("Lizards/Arreglar orillas, muro, plantas y aparición", priority = 16)]
        public static string BuildSenseFixes()
        {
            if (EditorApplication.isPlaying) return "Sal del modo Play primero.";
            var l = GameObject.Find("LOBBY / reconstrucción detallada");
            if (!l) return "Esta escena no es el Lobby.";
            lobby = l.transform;
            vtx = Mat("Atrezo_ColorVertice", () => { var m = LizardMaterials.Toon(Color.white); m.SetFloat("_VertexColorWeight", 1f); return m; });
            string log = PipeHeadwalls() + CloseBehindFactory() + GroundProps() + SpawnOnPad();
            EditorSceneManager.MarkSceneDirty(l.scene);
            EditorSceneManager.SaveScene(l.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Lizards] Arreglos:" + log);
            return log;
        }

        static float TerrainY(Vector3 p, float fallback)
        {
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, 80f, p.z), Vector3.down, 160f, ~0, QueryTriggerInteraction.Ignore))
                if (h.collider.name.StartsWith("Terreno")) return h.point.y;
            return fallback;
        }

        // ---------------- Tuberías con muro de contención ----------------

        static string PipeHeadwalls()
        {
            var wallMesh = Sdf(p =>
            {
                // Muro de bloques de piedra que entra en la orilla, con remate y aletas a los lados.
                float wall = Box(p, new Vector3(0, 1.35f, -1.3f), new Vector3(2.3f, 2.6f, 1.1f), 0.12f);
                float cap = Box(p, new Vector3(0, 3.95f, -1.2f), new Vector3(2.5f, 0.2f, 1.25f), 0.08f);
                float wings = Mathf.Min(Box(p, new Vector3(-2.6f, 0.9f, -2.6f), new Vector3(0.5f, 2.1f, 1.6f), 0.1f), Box(p, new Vector3(2.6f, 0.9f, -2.6f), new Vector3(0.5f, 2.1f, 1.6f), 0.1f));
                float d = Mathf.Min(Mathf.Min(wall, cap), wings);
                // Juntas entre bloques en la cara delantera.
                float row = Mathf.Abs(Mathf.Repeat(p.y + 1.25f, 0.65f) - 0.325f), col = Mathf.Abs(Mathf.Repeat(p.x + (Mathf.Floor((p.y + 1.25f) / 0.65f) % 2) * 0.55f, 1.1f) - 0.55f);
                float seam = Mathf.Min(0.325f - row, 0.55f - col);
                d = SMax(d, -Mathf.Max(seam - 0.035f, -(p.z + 0.25f)), 0.02f);
                // Hueco por donde sale la tubería.
                d = Mathf.Max(d, -(new Vector2(p.x, p.y - 2.6f).magnitude - 0.78f));
                return d;
            }, new Bounds(new Vector3(0, 1.4f, -1.9f), new Vector3(6.6f, 5.6f, 4f)), 0.05f, "Kit_MuroTuberia", (p, n) =>
            {
                if (n.y > 0.6f) return new Color(0.4f, 0.78f, 0.3f);                                                  // musgo arriba
                Color s = Color.Lerp(new Color(0.48f, 0.5f, 0.55f), new Color(0.62f, 0.63f, 0.67f), N(p, 1.7f));
                if (p.y < 0.2f) s = Color.Lerp(s, new Color(0.25f, 0.38f, 0.32f), 0.6f);                             // mojado abajo
                return s;
            });
            int n = 0;
            foreach (Transform g in Object.FindObjectsByType<Transform>())
            {
                if (g.name != "Tubería con cascada") continue;
                var old = g.Find("Muro"); if (old) Object.DestroyImmediate(old.gameObject);
                var rock = g.Find("Roca"); if (rock) rock.gameObject.SetActive(false);
                // En vez de un muro (se veía por detrás), un peñasco con musgo medio enterrado en la orilla que se traga la tubería.
                var old2 = g.Find("Peñasco"); if (old2) Object.DestroyImmediate(old2.gameObject);
                var boulder = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Amb_Roca_2.asset");
                if (boulder)
                {
                    var w = Part("Peñasco", g, boulder);
                    w.transform.localPosition = new Vector3(0, 0.2f, -2.2f); w.transform.localScale = new Vector3(2.6f, 2.2f, 2.4f);
                    w.AddComponent<MeshCollider>().sharedMesh = boulder;
                }
                // La tubería entra en el muro.
                var pipe = g.Find("Tubo"); if (pipe) pipe.localPosition = new Vector3(0, 1.4f, -0.6f);
                var stream = g.Find("Chorro"); if (stream) stream.localPosition = new Vector3(0, 1.4f, 2.65f);
                var splash = g.Find("Salpicadura"); if (splash) splash.localPosition = new Vector3(0, 0.05f - 1.2f + 1.2f, 2.65f);
                n++;
            }
            return $" {n} tuberías con muro de piedra;";
        }

        // ---------------- Detrás del muro de la fábrica ----------------

        static string CloseBehindFactory()
        {
            var facade = GameObject.Find("Fachada de la fábrica");
            if (!facade) return "";
            var fb = facade.GetComponent<Renderer>().bounds;
            var old = lobby.Find("Cierre tras la fábrica (generado)"); if (old) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Cierre tras la fábrica (generado)").transform; root.SetParent(lobby, false);
            // Barrera invisible a lo ancho de toda la isla, a la altura del muro: no se puede pasar detrás.
            var barrier = root.gameObject.AddComponent<BoxCollider>();
            barrier.center = new Vector3((fb.min.x + fb.max.x) * 0.5f, 15f, fb.center.z);
            barrier.size = new Vector3(160f, 40f, fb.size.z);
            // Rocas grandes que tapan los huecos a los lados del muro (para que no se vea vacío).
            var big = new[] { LowPolyRock(11), LowPolyRock(12), LowPolyRock(13) };
            int k = 0;
            foreach (float x in new[] { fb.min.x - 4f, fb.min.x - 11f, fb.max.x + 4f, fb.max.x + 11f })
            {
                Vector3 p = new Vector3(x, 0, fb.center.z);
                p.y = TerrainY(p, -0.6f);
                var r = Part("Roca grande", root, big[k % 3]);
                r.transform.position = p + Vector3.up * 2.5f;
                r.transform.localScale = new Vector3(10f, 12f, 9f);
                r.transform.rotation = Quaternion.Euler(0, k * 40f, 0);
                r.AddComponent<MeshCollider>();
                k++;
            }
            // Detrás, copas de árboles que asoman por encima del muro.
            var canopy = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Kit_Sauce_Copa3.asset");
            var trunk = AssetDatabase.LoadAssetAtPath<Mesh>($"{Dir}/Kit_Sauce_Tronco.asset");
            for (int i = 0; i < 7 && canopy && trunk; i++)
            {
                Vector3 p = new Vector3(Mathf.Lerp(fb.min.x + 6f, fb.max.x - 6f, i / 6f), 0, fb.max.z + 6f + (i % 2) * 4f);
                p.y = TerrainY(p, 0f);
                var t = new GameObject("Árbol tras el muro").transform; t.SetParent(root, false);
                t.position = p; t.localScale = Vector3.one * (1.8f + (i % 3) * 0.3f); t.rotation = Quaternion.Euler(0, i * 51f, 0);
                Part("Tronco", t, trunk); Part("Copa", t, canopy);
            }
            return " zona tras el muro cerrada;";
        }

        // ---------------- Apoyar plantas y hongos en el suelo ----------------

        static string GroundProps()
        {
            var names = new HashSet<string> { "Hierba", "Arbusto de ribera", "Seta", "Matita", "Piedrita", "Macizo de flores", "Árbol", "Mata de césped" };
            var pond = lobby.Find("Estanque de aparición (generado)");
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Transform>())
            {
                if (!names.Contains(t.name) || !t.IsChildOf(lobby)) continue;
                if (pond && t.IsChildOf(pond) && t.name != "Matita" && t.name != "Piedrita") continue;
                var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float ground = TerrainY(t.position, float.NaN);
                if (float.IsNaN(ground)) continue;
                float sink = Mathf.Min(0.12f, b.size.y * 0.06f);
                float dy = (ground - sink) - b.min.y;
                if (Mathf.Abs(dy) > 0.03f) { t.position += Vector3.up * dy; n++; }
            }
            return $" {n} plantas/hongos apoyados en el suelo;";
        }

        // ---------------- Aparecer sobre el nenúfar ----------------

        static string SpawnOnPad()
        {
            var hub = Object.FindAnyObjectByType<SwampLobbyHub>();
            if (!hub) return "";
            var pad = GameObject.Find("Soporte del nenúfar");
            Vector3 spawn = hub.spawn;
            if (pad) { var c = pad.GetComponent<Collider>(); if (c) { c.enabled = true; spawn = new Vector3(c.bounds.center.x, c.bounds.max.y + 0.15f, c.bounds.center.z); } }
            hub.spawn = spawn; EditorUtility.SetDirty(hub);
            if (hub.roomMenu) { hub.roomMenu.spawnPoint = spawn; EditorUtility.SetDirty(hub.roomMenu); }
            int moved = 0;
            foreach (var p in Object.FindObjectsByType<LizardPlayer>())
                if (Vector3.Distance(new Vector3(p.transform.position.x, 0, p.transform.position.z), new Vector3(spawn.x, 0, spawn.z)) < 6f) { p.transform.position = spawn; EditorUtility.SetDirty(p.transform); moved++; }
            return $" aparición sobre el nenúfar ({moved} jugador movido).";
        }
    }
}
