using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Mancha de moco pegada a una superficie: aparece con un pequeño rebote, se queda un rato
    /// y se encoge hasta desaparecer. También crea gotitas que saltan y caen.
    /// </summary>
    public class GooSplat : MonoBehaviour
    {
        static Material gooMat, gooDarkMat;
        static Mesh sphere;

        public static Material GooMaterial => gooMat ? gooMat : (gooMat = LizardMaterials.Toon(new Color(0.45f, 0.86f, 0.18f), 0.0025f, 0.35f, new Color(0.55f, 0.7f, 0.75f)));
        static Material GooDark => gooDarkMat ? gooDarkMat : (gooDarkMat = LizardMaterials.Toon(new Color(0.3f, 0.66f, 0.12f), 0f, 0f));

        static Mesh SphereMesh
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

        float age, life;
        Vector3 fullScale;

        /// <summary>Crea la mancha en 'point' orientada a 'normal'. 'size' es el diámetro en metros.</summary>
        public static void Spawn(Vector3 point, Vector3 normal, float size, Transform stickTo = null)
        {
            if (!Application.isPlaying) return;
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            var root = new GameObject("Mancha de moco");
            root.transform.position = point + normal * 0.01f;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            if (stickTo && stickTo.gameObject.activeInHierarchy) root.transform.SetParent(stickTo, true);

            // Charco principal aplastado + manchas pequeñas alrededor (forma irregular).
            Blob(root.transform, Vector3.zero, new Vector3(1f, 0.16f, 0.85f) * size, GooMaterial);
            Blob(root.transform, new Vector3(0.05f, 0.02f, 0.02f) * size, new Vector3(0.55f, 0.14f, 0.5f) * size, GooDark);
            int n = Random.Range(4, 7);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                float r = Random.Range(0.5f, 0.8f) * size;
                float d = Random.Range(0.14f, 0.3f) * size;
                Blob(root.transform, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), new Vector3(d, d * 0.35f, d), GooMaterial);
            }

            var splat = root.AddComponent<GooSplat>();
            splat.life = Random.Range(3.5f, 5f);
            splat.fullScale = root.transform.localScale;
            root.transform.localScale = splat.fullScale * 0.2f;

            // Gotitas que saltan.
            for (int i = 0; i < 6; i++)
            {
                Vector3 dir = (normal * 1.4f + Random.insideUnitSphere).normalized;
                GooDrop.Spawn(point + normal * 0.05f, dir * Random.Range(2f, 4f), size * Random.Range(0.12f, 0.22f));
            }
        }

        static void Blob(Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = new GameObject("Gota");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = SphereMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            age += Time.deltaTime;
            float pop = age < 0.18f ? Mathf.Lerp(0.2f, 1.15f, age / 0.18f) : age < 0.3f ? Mathf.Lerp(1.15f, 1f, (age - 0.18f) / 0.12f) : 1f;
            float fade = age > life - 0.8f ? Mathf.Clamp01((life - age) / 0.8f) : 1f;
            // Tiembla un poco al principio (gelatina).
            float jiggle = age < 0.6f ? 1f + Mathf.Sin(age * 40f) * 0.06f * (1f - age / 0.6f) : 1f;
            transform.localScale = new Vector3(fullScale.x * pop * fade * jiggle, fullScale.y * pop * fade / jiggle, fullScale.z * pop * fade * jiggle);
            if (age >= life) Destroy(gameObject);
        }
    }

    /// <summary>Gotita de moco con gravedad que se aplasta y desaparece al tocar algo.</summary>
    public class GooDrop : MonoBehaviour
    {
        Vector3 vel; float size, age; bool landed;

        public static void Spawn(Vector3 pos, Vector3 velocity, float size)
        {
            var go = new GameObject("Gotita");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.AddComponent<MeshFilter>().sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
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
                transform.localScale = Vector3.Lerp(transform.localScale, new Vector3(size * 1.6f, size * 0.15f, size * 1.6f) * Mathf.Clamp01(1.6f - age), dt * 12f);
                if (age > 1.6f) Destroy(gameObject);
                return;
            }
            vel += Physics.gravity * dt;
            Vector3 step = vel * dt;
            if (Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude + size * 0.5f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.GetComponentInParent<LizardPlayer>())
            {
                transform.position = hit.point + hit.normal * 0.01f;
                transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                landed = true; age = 0.8f;
                return;
            }
            transform.position += step;
            // Se estira en la dirección de caída.
            if (vel.sqrMagnitude > 0.01f) transform.rotation = Quaternion.FromToRotation(Vector3.up, vel.normalized);
            transform.localScale = new Vector3(size, size * (1f + Mathf.Min(vel.magnitude * 0.08f, 0.8f)), size);
            if (age > 3f) Destroy(gameObject);
        }
    }
}
