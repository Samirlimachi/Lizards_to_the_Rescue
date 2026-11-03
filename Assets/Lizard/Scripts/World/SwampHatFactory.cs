using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Sombreros del refugio. Se colocan en la tapa de la cabeza de Sunny (entre los ojos) para que sigan a la cabeza
    /// al hablar, comer, en forma renacuajo y rodando como bola. Estilo cartoon con contorno.
    /// </summary>
    public static class SwampHatFactory
    {
        public const string PrefsKey = "Lizards.Hat";
        public static readonly string[] Names = { "Sin sombrero", "Sombrero de copa", "Gorra", "Gorro de fiesta", "Corona", "Auriculares" };
        const string HatName = "Sombrero del refugio";

        public static void Save(int index) { PlayerPrefs.SetInt(PrefsKey, index); PlayerPrefs.Save(); }
        public static int Load() => Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, 0), 0, Names.Length - 1);

        /// <summary>Pone (o quita, con índice 0) el sombrero. 'silent' evita el mensaje.</summary>
        public static Transform Equip(LizardPlayer p, int index, bool silent = false)
        {
            if (!p) return null;
            foreach (var t in p.GetComponentsInChildren<Transform>(true))
                if (t && t.name == HatName) { t.gameObject.SetActive(false); Kill(t.gameObject); }
            index = Mathf.Clamp(index, 0, Names.Length - 1);
            if (index == 0) { if (!silent) p.Message("Sin sombrero"); return null; }

            // Ancla: tapa de la cabeza de Sunny (sigue a la boca, a la forma y al rodar). Si no hay, el cuerpo.
            var sunny = p.GetComponentInChildren<SunnyVisual>();
            Transform anchor = sunny && sunny.headBone ? sunny.headBone : p.transform;
            var root = new GameObject(HatName).transform;
            root.SetParent(anchor, false);
            // En el hueso de la tapa: encima de la cabeza, un poco hacia delante, entre los ojos.
            root.localPosition = sunny ? new Vector3(0f, 0.22f, 0.24f) : new Vector3(0f, 1.2f, 0f);
            root.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            root.localScale = Vector3.one * 0.8f;

            Material M(Color c, float spec = 0.25f) => LizardMaterials.Toon(c, 0.003f, spec);
            Transform Part(PrimitiveType shape, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
            {
                var o = GameObject.CreatePrimitive(shape);
                o.name = "Pieza de sombrero";
                Kill(o.GetComponent<Collider>());
                o.transform.SetParent(root, false);
                o.transform.localPosition = pos; o.transform.localScale = scale; o.transform.localRotation = Quaternion.Euler(euler);
                var r = o.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                return o.transform;
            }

            switch (index)
            {
                case 1: // Copa con cinta
                {
                    var black = M(new Color(0.12f, 0.12f, 0.16f), 0.45f);
                    Part(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.62f, 0.025f, 0.62f), black);
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.2f, 0.4f), black);
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0.07f, 0f), new Vector3(0.41f, 0.045f, 0.41f), M(new Color(0.85f, 0.2f, 0.25f)));
                    break;
                }
                case 2: // Gorra con visera y botón
                {
                    var blue = M(new Color(0.2f, 0.5f, 0.95f));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.3f, 0.52f), blue);
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.0f, 0.27f), new Vector3(0.44f, 0.04f, 0.32f), M(new Color(1f, 0.85f, 0.2f)), new Vector3(-6f, 0f, 0f));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.19f, 0f), Vector3.one * 0.07f, M(new Color(1f, 0.85f, 0.2f)));
                    break;
                }
                case 3: // Gorro de fiesta con rayas y pompón
                {
                    var cone = new GameObject("Cono");
                    cone.transform.SetParent(root, false);
                    cone.AddComponent<MeshFilter>().sharedMesh = ConeMesh(0.2f, 0.55f, 20);
                    cone.AddComponent<MeshRenderer>().sharedMaterial = M(new Color(0.95f, 0.35f, 0.75f));
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.31f, 0.025f, 0.31f), M(new Color(1f, 0.85f, 0.2f)));
                    Part(PrimitiveType.Cylinder, new Vector3(0f, 0.32f, 0f), new Vector3(0.19f, 0.022f, 0.19f), M(new Color(0.35f, 0.85f, 1f)));
                    Part(PrimitiveType.Sphere, new Vector3(0f, 0.57f, 0f), Vector3.one * 0.13f, M(Color.white, 0.1f));
                    break;
                }
                case 4: // Corona con gemas
                {
                    var gold = M(new Color(1f, 0.78f, 0.15f), 0.6f);
                    var crown = new GameObject("Corona");
                    crown.transform.SetParent(root, false);
                    crown.AddComponent<MeshFilter>().sharedMesh = CrownMesh(0.24f, 0.13f, 0.1f, 5);
                    crown.AddComponent<MeshRenderer>().sharedMaterial = gold;
                    var gem = new[] { new Color(0.9f, 0.15f, 0.25f), new Color(0.2f, 0.5f, 1f), new Color(0.2f, 0.85f, 0.4f) };
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2f;
                        Part(PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * 0.245f, 0.07f, Mathf.Cos(a) * 0.245f), Vector3.one * 0.06f, M(gem[i % 3], 0.8f));
                        Part(PrimitiveType.Sphere, new Vector3(Mathf.Sin(a) * 0.24f, 0.26f, Mathf.Cos(a) * 0.24f), Vector3.one * 0.045f, gold);
                    }
                    break;
                }
                case 5: // Auriculares con diadema
                {
                    var band = M(new Color(0.2f, 0.2f, 0.25f), 0.4f);
                    var torus = new GameObject("Diadema");
                    torus.transform.SetParent(root, false);
                    torus.transform.localPosition = new Vector3(0f, -0.05f, -0.05f);
                    torus.AddComponent<MeshFilter>().sharedMesh = ArcMesh(0.36f, 0.035f, 24);
                    torus.AddComponent<MeshRenderer>().sharedMaterial = band;
                    foreach (float x in new[] { -0.37f, 0.37f })
                    {
                        Part(PrimitiveType.Sphere, new Vector3(x, -0.08f, -0.05f), new Vector3(0.1f, 0.22f, 0.22f), M(new Color(0.95f, 0.35f, 0.3f)));
                        Part(PrimitiveType.Sphere, new Vector3(x * 0.95f, -0.08f, -0.05f), new Vector3(0.09f, 0.16f, 0.16f), band);
                    }
                    break;
                }
            }
            if (!silent)
            {
                p.Message(Names[index]);
                Fx.Burst(root.position, new Color(1f, 0.9f, 0.5f), 8, 1.5f, 0.06f);
            }
            return root;
        }

        static void Kill(Object o) { if (!o) return; if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        // ---- Mallas sencillas ----

        static Mesh ConeMesh(float radius, float height, int n)
        {
            var v = new Vector3[n + 2]; var t = new int[n * 6];
            for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2f; v[i] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius); }
            v[n] = new Vector3(0f, height, 0f); v[n + 1] = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                t[i * 6] = i; t[i * 6 + 1] = n; t[i * 6 + 2] = j;
                t[i * 6 + 3] = j; t[i * 6 + 4] = n + 1; t[i * 6 + 5] = i;
            }
            var m = new Mesh { name = "Cono", vertices = v, triangles = t };
            m.RecalculateNormals();
            return m;
        }

        static Mesh CrownMesh(float radius, float baseH, float peakH, int peaks)
        {
            int n = peaks * 2 * 3;
            var v = new Vector3[n * 2]; var t = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                float top = baseH + (Mathf.Cos(a * peaks) * 0.5f + 0.5f) * peakH;
                v[i] = new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
                v[n + i] = new Vector3(Mathf.Sin(a) * radius, top, Mathf.Cos(a) * radius);
            }
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                // Solo la cara exterior: con doble cara las normales se anulaban y salían rayas oscuras.
                t[k++] = i; t[k++] = j; t[k++] = n + i; t[k++] = j; t[k++] = n + j; t[k++] = n + i;
            }
            var m = new Mesh { name = "Corona", vertices = v, triangles = t };
            m.RecalculateNormals();
            return m;
        }

        /// <summary>Medio aro (diadema) en el plano XY.</summary>
        static Mesh ArcMesh(float radius, float thickness, int n)
        {
            const int sides = 8;
            var v = new Vector3[(n + 1) * sides]; var t = new int[n * sides * 6];
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.PI * i / n;
                Vector3 c = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Vector3 outward = c.normalized;
                for (int s = 0; s < sides; s++)
                {
                    float b = s / (float)sides * Mathf.PI * 2f;
                    v[i * sides + s] = c + (outward * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * thickness;
                }
            }
            int k = 0;
            for (int i = 0; i < n; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = i * sides + s, a1 = i * sides + (s + 1) % sides, b0 = a0 + sides, b1 = a1 + sides;
                    t[k++] = a0; t[k++] = b0; t[k++] = a1; t[k++] = a1; t[k++] = b0; t[k++] = b1;
                }
            var m = new Mesh { name = "Diadema", vertices = v, triangles = t };
            m.RecalculateNormals();
            return m;
        }
    }
}
