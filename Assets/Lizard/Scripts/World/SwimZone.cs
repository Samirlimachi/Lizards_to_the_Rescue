using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Agua en la que se nada (círculo de radio <see cref="radius"/> con la superficie a <see cref="surfaceY"/> sobre el objeto).
    /// El motor del personaje la consulta para flotar y nadar; además deja ondas al moverse.
    /// </summary>
    public class SwimZone : MonoBehaviour
    {
        public static readonly List<SwimZone> All = new();
        public float radius = 4.2f;
        public float surfaceY = 1.2f;
        [Tooltip("Hasta qué profundidad bajo la superficie se considera dentro del agua.")]
        public float depth = 3f;
        public Material rippleMat;

        public float SurfaceWorld => transform.position.y + surfaceY;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>¿Está este punto (los pies) dentro de alguna zona de nado? Devuelve la altura de la superficie.</summary>
        public static bool Find(Vector3 pos, out float surface, out SwimZone zone)
        {
            foreach (var z in All)
            {
                Vector3 d = pos - z.transform.position; d.y = 0f;
                float s = z.SurfaceWorld;
                if (d.magnitude < z.radius && pos.y < s + 0.15f && pos.y > s - z.depth) { surface = s; zone = z; return true; }
            }
            surface = 0f; zone = null; return false;
        }

        // ---------------- Ondas ----------------

        struct Ripple { public Transform t; public Material m; public float age, life, size; }
        readonly List<Ripple> ripples = new();
        readonly Dictionary<LizardPlayer, float> nextRipple = new();
        static Mesh ringMesh;

        void Update()
        {
            float t = Time.time, dt = Time.deltaTime;
            UpdateFoam(dt);
            foreach (var p in LizardPlayer.All)
            {
                if (!p || !p.Motor || p.Motor.SwimZoneNow != this) continue;
                float speed = p.Motor.PlanarSpeed;
                nextRipple.TryGetValue(p, out float next);
                if (t < next) continue;
                // Al nadar, ondas seguidas y chapoteo; flotando quieto, alguna onda suave de vez en cuando.
                nextRipple[p] = t + (speed > 0.5f ? 0.22f : 1.1f);
                Spawn(new Vector3(p.transform.position.x, SurfaceWorld + 0.03f, p.transform.position.z), speed > 0.5f ? 1.6f : 1.1f);
                if (speed > 0.5f) Fx.Burst(p.transform.position + p.transform.forward * 0.3f + Vector3.up * 0.55f, new Color(0.85f, 1f, 0.9f), 3, 1.6f, 0.07f);
            }
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var r = ripples[i];
                r.age += dt;
                float k = r.age / r.life;
                if (k >= 1f) { Destroy(r.t.gameObject); Destroy(r.m); ripples.RemoveAt(i); continue; }
                r.t.localScale = Vector3.one * Mathf.Lerp(0.4f, r.size, Mathf.Sqrt(k));
                var c = r.m.GetColor("_BaseColor"); c.a = 0.6f * (1f - k); r.m.SetColor("_BaseColor", c);
                ripples[i] = r;
            }
        }

        // ---------------- Espuma: anillo blanco alrededor del cuerpo y estela al nadar ----------------

        readonly Dictionary<LizardPlayer, Transform> collars = new();
        struct Puff { public Transform t; public float age, life, size; }
        readonly List<Puff> puffs = new();
        readonly Dictionary<LizardPlayer, float> nextPuff = new();
        Material foamMat; static Mesh sphere;

        void UpdateFoam(float dt)
        {
            if (!foamMat) foamMat = LizardMaterials.Toon(new Color(0.96f, 1f, 1f), 0f, 0f, new Color(0.8f, 0.92f, 0.95f));
            if (!sphere) { var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = tmp.GetComponent<MeshFilter>().sharedMesh; Destroy(tmp); }
            float t = Time.time;
            foreach (var p in LizardPlayer.All)
            {
                if (!p || !p.Motor) continue;
                bool inside = p.Motor.SwimZoneNow == this;
                collars.TryGetValue(p, out var col);
                if (inside && !col)
                {
                    col = new GameObject("Espuma del cuerpo").transform; col.SetParent(transform, true);
                    for (int i = 0; i < 9; i++)
                    {
                        var b = new GameObject("Bola").transform; b.SetParent(col, false);
                        float a = i / 9f * Mathf.PI * 2f; b.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.42f;
                        b.localScale = new Vector3(0.32f, 0.12f, 0.32f) * (0.8f + (i % 3) * 0.15f);
                        b.gameObject.AddComponent<MeshFilter>().sharedMesh = sphere;
                        var mr = b.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = foamMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                    collars[p] = col;
                }
                if (col) col.gameObject.SetActive(inside);
                if (!inside) continue;
                float speed = p.Motor.PlanarSpeed, k = Mathf.Max(0.6f, p.Stats.visualScale);
                col.position = new Vector3(p.transform.position.x, SurfaceWorld + 0.02f, p.transform.position.z);
                col.rotation = Quaternion.Euler(0f, t * 40f, 0f);
                col.localScale = Vector3.one * k * (1f + Mathf.Sin(t * 6f) * 0.06f + Mathf.Clamp01(speed / 3f) * 0.25f);
                // Estela: bolitas de espuma que quedan detrás y se deshacen.
                nextPuff.TryGetValue(p, out float np);
                if (speed > 0.5f && t >= np)
                {
                    nextPuff[p] = t + 0.07f;
                    var go = new GameObject("Estela"); go.transform.SetParent(transform, true);
                    Vector3 back = -p.transform.forward * 0.45f * k + p.transform.right * Random.Range(-0.3f, 0.3f) * k;
                    go.transform.position = new Vector3(p.transform.position.x, SurfaceWorld + 0.02f, p.transform.position.z) + back;
                    go.AddComponent<MeshFilter>().sharedMesh = sphere;
                    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = foamMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    puffs.Add(new Puff { t = go.transform, life = Random.Range(0.6f, 1f), size = Random.Range(0.18f, 0.3f) * k });
                }
            }
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var pf = puffs[i]; pf.age += dt; float q = pf.age / pf.life;
                if (q >= 1f) { Destroy(pf.t.gameObject); puffs.RemoveAt(i); continue; }
                float s = pf.size * (1f - q * q);
                pf.t.localScale = new Vector3(s, s * 0.4f, s);
                puffs[i] = pf;
            }
        }

        void Spawn(Vector3 at, float size)
        {
            if (!rippleMat) return;
            if (!ringMesh) ringMesh = BuildRing();
            var go = new GameObject("Onda");
            go.transform.SetParent(transform, true);
            go.transform.position = at;
            go.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Material(rippleMat); mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ripples.Add(new Ripple { t = go.transform, m = m, life = 1.2f, size = size });
        }

        static Mesh BuildRing()
        {
            const int seg = 40; var v = new Vector3[seg * 2]; var tri = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d * 0.85f; v[i * 2 + 1] = d * 1f;
                int n = (i + 1) % seg;
                tri[i * 6] = i * 2; tri[i * 6 + 1] = n * 2; tri[i * 6 + 2] = i * 2 + 1;
                tri[i * 6 + 3] = i * 2 + 1; tri[i * 6 + 4] = n * 2; tri[i * 6 + 5] = n * 2 + 1;
            }
            var m = new Mesh { name = "Onda", vertices = v, triangles = tri };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
