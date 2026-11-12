using UnityEngine;

namespace Lizards
{
    /// <summary>Charco de gel del slime. Si ralentiza, el efecto dura un momento después de salir del charco.</summary>
    public class SlimeGelPuddle : MonoBehaviour
    {
        static Material gel;
        public static Material GelMaterial
        {
            get
            {
                if (!gel) { gel = LizardMaterials.Transparent(new Color(0.68f, 0.92f, 0.22f, 0.78f), 0.95f); gel.name = "Gel del slime"; }
                return gel;
            }
        }

        float slow, life, born, grow, radius; bool slows;

        /// <summary>Crea un charco irregular en el suelo. grow = segundos que tarda en extenderse.</summary>
        public static SlimeGelPuddle Create(Vector3 pos, float radius, float slowFactor, float life, bool slows, float seedScale = 1f, float grow = 0.2f)
        {
            var go = new GameObject("Charco de gel");
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            go.AddComponent<MeshFilter>().sharedMesh = BlobMesh(radius * seedScale);
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = GelMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = go.AddComponent<SlimeGelPuddle>(); p.slow = slowFactor; p.life = life; p.born = Time.time; p.slows = slows; p.grow = Mathf.Max(0.01f, grow); p.radius = radius * seedScale;
            if (slows)
            {
                var c = go.AddComponent<CapsuleCollider>(); c.isTrigger = true; c.radius = radius * seedScale * 0.9f; c.height = 1.2f; c.center = Vector3.up * 0.4f;
            }
            go.transform.localScale = new Vector3(0.05f, 1, 0.05f);
            return p;
        }

        static Mesh BlobMesh(float radius)
        {
            const int n = 20;
            var v = new Vector3[n * 2 + 1]; var t = new int[n * 3 + n * 6];
            v[0] = Vector3.up * 0.03f;
            float ph = Random.value * 10f;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                float rr = radius * (1f + 0.18f * Mathf.Sin(a * 3 + ph) + 0.1f * Mathf.Sin(a * 5 + ph * 2));
                v[1 + i] = new Vector3(Mathf.Cos(a) * rr * 0.75f, 0.03f, Mathf.Sin(a) * rr * 0.75f);
                v[1 + n + i] = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
            }
            int k = 0;
            for (int i = 0; i < n; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % n, c = 1 + n + i, d = 1 + n + (i + 1) % n;
                t[k++] = 0; t[k++] = b; t[k++] = a;
                t[k++] = a; t[k++] = b; t[k++] = c; t[k++] = b; t[k++] = d; t[k++] = c;
            }
            var m = new Mesh { name = "Charco", vertices = v, triangles = t }; m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            float age = Time.time - born;
            float s = Mathf.Min(1f, age / grow); s = 1f - (1f - s) * (1f - s);
            float fade = Mathf.Clamp01((life - age) / 0.5f);
            float wob = 1f + Mathf.Sin(age * 9f) * 0.03f * (1f - Mathf.Min(1f, age));
            transform.localScale = new Vector3(s * fade * wob, 1f, s * fade * wob);
            if (age >= life) Destroy(gameObject);
        }

        void OnTriggerStay(Collider c)
        {
            if (!slows) return;
            var p = c.GetComponentInParent<LizardPlayer>();
            if (p && p.Motor) p.Motor.ApplySlow(slow, 0.6f);
        }
    }
}
