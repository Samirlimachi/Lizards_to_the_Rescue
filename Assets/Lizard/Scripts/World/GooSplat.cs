using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Mancha de moco pegada a una superficie (pared, suelo, caja): aparece con un rebote de gelatina, en las paredes
    /// chorrea hacia abajo, se queda un rato y se encoge hundiéndose en la superficie. Las manchitas de alrededor solo
    /// se ponen donde de verdad hay superficie (nunca cuelgan en el aire en una esquina).
    /// </summary>
    public class GooSplat : MonoBehaviour
    {
        static Material gooMat, gooDarkMat;
        static Mesh sphere;

        public static Material GooMaterial => gooMat ? gooMat : (gooMat = LizardMaterials.Toon(new Color(0.45f, 0.86f, 0.18f), 0.0025f, 0.35f, new Color(0.55f, 0.7f, 0.75f)));
        static Material GooDark => gooDarkMat ? gooDarkMat : (gooDarkMat = LizardMaterials.Toon(new Color(0.3f, 0.66f, 0.12f), 0f, 0f));

        public static Mesh SphereMesh
        {
            get
            {
                if (sphere) return sphere;
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
                return sphere;
            }
        }

        sealed class Drip { public Transform t; public Vector3 start, dir; public float width, length, delay; }
        readonly List<Drip> drips = new();
        float age, life;
        Vector3 fullScale;

        /// <summary>Crea la mancha en 'point' orientada a 'normal'. 'size' es el diámetro en metros.</summary>
        public static void Spawn(Vector3 point, Vector3 normal, float size, Transform stickTo = null, Collider surface = null)
        {
            if (!Application.isPlaying) return;
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            normal.Normalize();
            var root = new GameObject("Mancha de moco");
            root.transform.position = point + normal * 0.005f;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            if (stickTo && stickTo.gameObject.activeInHierarchy) root.transform.SetParent(stickTo, true);

            // Charco principal muy plano (a ras de la superficie) + centro más oscuro.
            Blob(root.transform, Vector3.zero, new Vector3(1f, 0.08f, 0.85f) * size, GooMaterial);
            Blob(root.transform, new Vector3(0.05f, 0.01f, 0.02f) * size, new Vector3(0.55f, 0.07f, 0.5f) * size, GooDark);
            // Manchitas alrededor, solo donde hay superficie de verdad debajo.
            int n = Random.Range(4, 7);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                float r = Random.Range(0.5f, 0.8f) * size;
                float d = Random.Range(0.14f, 0.3f) * size;
                Vector3 local = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                if (!OnSurface(root.transform, local, normal, surface)) continue;
                Blob(root.transform, local, new Vector3(d, d * 0.25f, d), GooMaterial);
            }

            var splat = root.AddComponent<GooSplat>();
            splat.life = Random.Range(3.5f, 5f);
            splat.fullScale = root.transform.localScale;
            root.transform.localScale = splat.fullScale * 0.2f;

            // En paredes y techos inclinados el moco chorrea hacia abajo (1–2 regueros).
            Vector3 down = Vector3.ProjectOnPlane(Vector3.down, normal);
            if (down.sqrMagnitude > 0.25f)
            {
                Vector3 downLocal = root.transform.InverseTransformDirection(down.normalized); downLocal.y = 0f; downLocal.Normalize();
                int count = Random.Range(1, 3);
                for (int i = 0; i < count; i++)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, downLocal) * Random.Range(-0.3f, 0.3f) * size;
                    float len = Random.Range(0.6f, 1.2f) * size;
                    if (!OnSurface(root.transform, side + downLocal * (size * 0.35f + len), normal, surface)) len *= 0.4f;
                    var t = Blob(root.transform, side + downLocal * size * 0.35f, Vector3.zero, GooMaterial);
                    splat.drips.Add(new Drip { t = t, start = t.localPosition, dir = downLocal, width = Random.Range(0.12f, 0.2f) * size, length = len, delay = Random.Range(0.15f, 0.45f) });
                }
            }
        }

        /// <summary>¿Hay superficie justo debajo de este punto de la mancha? (evita trozos colgando en bordes y esquinas)</summary>
        static bool OnSurface(Transform root, Vector3 local, Vector3 normal, Collider surface)
        {
            Vector3 p = root.TransformPoint(local);
            if (!Physics.Raycast(p + normal * 0.15f, -normal, out var hit, 0.3f, ~0, QueryTriggerInteraction.Ignore)) return false;
            return !surface || hit.collider == surface || Vector3.Dot(hit.normal, normal) > 0.9f;
        }

        static Transform Blob(Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = new GameObject("Gota");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = SphereMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void Update()
        {
            age += Time.deltaTime;
            // Aparece con un "plaf": crece con rebote y tiembla un poco.
            float pop = age < 0.12f ? Mathf.Lerp(0.2f, 1.2f, age / 0.12f) : age < 0.26f ? Mathf.Lerp(1.2f, 1f, (age - 0.12f) / 0.14f) : 1f;
            float jiggle = age < 0.6f ? 1f + Mathf.Sin(age * 40f) * 0.06f * (1f - age / 0.6f) : 1f;
            // Al final se encoge y se hunde en la superficie (no se queda flotando).
            float fade = age > life - 0.7f ? Mathf.Clamp01((life - age) / 0.7f) : 1f;
            float e = fade * fade;
            transform.localScale = new Vector3(fullScale.x * pop * e * jiggle, fullScale.y * pop * Mathf.Max(e, 0.01f) / jiggle, fullScale.z * pop * e * jiggle);

            foreach (var d in drips)
            {
                if (!d.t) continue;
                float k = Mathf.Clamp01((age - d.delay) / 0.9f);
                k = 1f - (1f - k) * (1f - k);   // corre rápido y frena
                float len = d.length * k;
                d.t.localPosition = d.start + d.dir * len * 0.5f;   // el reguero crece desde la mancha hacia abajo
                d.t.localRotation = Quaternion.LookRotation(d.dir, Vector3.up);
                d.t.localScale = new Vector3(d.width, d.width * 0.25f, Mathf.Max(0.001f, len));
            }
            if (age >= life) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Gotita de moco con gravedad: se estira al caer y, al tocar suelo o pared fijos, se aplasta y se seca.
    /// Si toca algo que se mueve (enemigo, jugador, caja) simplemente se deshace.
    /// </summary>
    public class GooDrop : MonoBehaviour
    {
        Vector3 vel; float size, age; bool landed;

        public static void Spawn(Vector3 pos, Vector3 velocity, float size)
        {
            if (!Application.isPlaying) return;
            var go = new GameObject("Gotita");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = GooSplat.SphereMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = GooSplat.GooMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var d = go.AddComponent<GooDrop>();
            d.vel = velocity; d.size = size;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (landed)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(size * 1.6f, size * 0.12f, size * 1.6f) * Mathf.Clamp01(1.6f - age), dt * 12f);
                if (age > 1.6f) Destroy(gameObject);
                return;
            }
            vel += Physics.gravity * dt;
            Vector3 step = vel * dt;
            if (Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude + size * 0.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                bool moving = (hit.rigidbody && !hit.rigidbody.isKinematic) || hit.collider.GetComponentInParent<LizardPlayer>() || hit.collider.GetComponentInParent<EnemyHealth>();
                if (moving) { Destroy(gameObject); return; }
                transform.position = hit.point + hit.normal * 0.005f;
                transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                landed = true; age = 0.8f;
                return;
            }
            // En el agua desaparece con un pequeño "plic".
            if (SwimZone.Find(transform.position, out float surface, out _) && transform.position.y < surface)
            {
                Fx.Burst(new Vector3(transform.position.x, surface, transform.position.z), new Color(0.8f, 1f, 0.95f), 2, 1f, 0.04f);
                Destroy(gameObject); return;
            }
            transform.position += step;
            // Se estira en la dirección de caída.
            if (vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.FromToRotation(Vector3.up, vel.normalized);
            transform.localScale = new Vector3(size, size * (1f + Mathf.Min(vel.magnitude * 0.08f, 0.8f)), size);
            if (age > 3f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Corona de salpicadura en el punto de impacto: un anillo de gotas que sale hacia fuera sobre la superficie
    /// y se encoge enseguida (0,25 s). Da el "¡plaf!" visual al golpe.
    /// </summary>
    public class GooCrown : MonoBehaviour
    {
        readonly List<Transform> bits = new();
        readonly List<Vector3> dirs = new();
        float age, size;
        const float Life = 0.28f;

        public static void Spawn(Vector3 point, Vector3 normal, float size)
        {
            if (!Application.isPlaying) return;
            var go = new GameObject("Corona de salpicadura");
            go.transform.position = point + normal * 0.03f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal.sqrMagnitude > 0.01f ? normal : Vector3.up);
            var c = go.AddComponent<GooCrown>(); c.size = size;
            int n = 8;
            for (int i = 0; i < n; i++)
            {
                float a = (i + Random.value * 0.5f) / n * Mathf.PI * 2f;
                var t = new GameObject("Gota").transform; t.SetParent(go.transform, false);
                t.gameObject.AddComponent<MeshFilter>().sharedMesh = GooSplat.SphereMesh;
                var r = t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = GooSplat.GooMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                c.bits.Add(t); c.dirs.Add(new Vector3(Mathf.Cos(a), 0.45f, Mathf.Sin(a)));
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / Life);
            float reach = (1f - (1f - k) * (1f - k)) * size * 0.9f;
            float s = size * 0.22f * (1f - k);
            for (int i = 0; i < bits.Count; i++)
            {
                bits[i].localPosition = dirs[i] * reach;
                bits[i].localRotation = Quaternion.LookRotation(dirs[i]);
                bits[i].localScale = new Vector3(s, s, s * 2.2f);
            }
            if (k >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Chisporroteo del ácido sobre un enemigo: burbujitas verdes que suben un momento siguiendo al enemigo.</summary>
    public class AcidSizzle : MonoBehaviour
    {
        float until, next;
        public static void Attach(Transform target, Vector3 point)
        {
            if (!Application.isPlaying || !target) return;
            var go = new GameObject("Ácido chisporroteando");
            go.transform.position = point;
            go.transform.SetParent(target, true);
            var s = go.AddComponent<AcidSizzle>(); s.until = Time.time + 0.7f;
            Sfx.Play(Sfx.Dissolve, point, 0.45f);
        }
        void Update()
        {
            if (Time.time >= until) { Destroy(gameObject); return; }
            if (Time.time < next) return;
            next = Time.time + 0.07f;
            Fx.Burst(transform.position + Random.insideUnitSphere * 0.12f, new Color(0.6f, 1f, 0.3f), 2, 0.9f, 0.05f);
        }
    }
}
