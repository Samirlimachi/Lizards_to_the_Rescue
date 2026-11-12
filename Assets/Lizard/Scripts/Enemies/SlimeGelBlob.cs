using UnityEngine;

namespace Lizards
{
    /// <summary>Gota de gel lanzada por el slime: vuela en parábola y al tocar el suelo deja un charco que ralentiza.</summary>
    public class SlimeGelBlob : MonoBehaviour
    {
        float slow, life, born; bool done; Vector3 baseScale;
        static Mesh sphere;
        Collider[] ignore;
        static readonly System.Collections.Generic.List<Collider> volley = new();
        static int volleyFrame = -1;

        public static SlimeGelBlob Launch(Vector3 from, Vector3 to, Collider[] ignore, float slowFactor, float puddleLife)
        {
            if (!sphere) { var t = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = t.GetComponent<MeshFilter>().sharedMesh; Destroy(t); }
            var go = new GameObject("Gota de gel");
            go.transform.position = from; go.transform.localScale = Vector3.one * 0.26f;
            go.AddComponent<MeshFilter>().sharedMesh = sphere;
            go.AddComponent<MeshRenderer>().sharedMaterial = SlimeGelPuddle.GelMaterial;
            var col = go.AddComponent<SphereCollider>(); col.radius = 0.5f;
            if (ignore != null) foreach (var c in ignore) if (c) Physics.IgnoreCollision(col, c);
            // Las gotas de una misma salpicadura no chocan entre sí al salir de la boca.
            if (volleyFrame != Time.frameCount) { volley.Clear(); volleyFrame = Time.frameCount; }
            foreach (var o in volley) if (o) Physics.IgnoreCollision(col, o);
            volley.Add(col);
            var rb = go.AddComponent<Rigidbody>(); rb.mass = 0.2f; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Parábola con tiempo de vuelo según distancia.
            Vector3 d = to - from; Vector3 flat = new Vector3(d.x, 0, d.z);
            float T = Mathf.Clamp(0.45f + flat.magnitude * 0.06f, 0.45f, 1.1f);
            rb.linearVelocity = flat / T + Vector3.up * (d.y / T + 0.5f * -Physics.gravity.y * T);
            var b = go.AddComponent<SlimeGelBlob>(); b.ignore = ignore; b.slow = slowFactor; b.life = puddleLife; b.born = Time.time; b.baseScale = go.transform.localScale;
            return b;
        }

        void Update()
        {
            // Bamboleo de gelatina en vuelo.
            float w = Mathf.Sin((Time.time - born) * 24f) * 0.15f;
            transform.localScale = new Vector3(baseScale.x * (1 + w), baseScale.y * (1 - w), baseScale.z * (1 + w));
            if (Time.time - born > 4f) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision c)
        {
            if (done) return; done = true;
            Vector3 p = c.GetContact(0).point;
            var player = c.collider.GetComponentInParent<LizardPlayer>();
            if (player) player.Motor.ApplySlow(slow, 1.2f);
            if (SwimZone.Find(p, out _, out _)) { Fx.Burst(p, new Color(0.8f, 1f, 0.6f), 10, 2f, 0.08f); Destroy(gameObject); return; }
            // El charco va al suelo que hay debajo del impacto (aunque la gota choque contra una pared).
            Vector3 ground = p;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * 0.5f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.collider.attachedRigidbody && !h.collider.attachedRigidbody.isKinematic) continue;      // comida, cajas, el propio slime
                if (ignore != null && System.Array.IndexOf(ignore, h.collider) >= 0) continue;
                if (h.distance < best) { best = h.distance; ground = h.point; }
            }
            SlimeGelPuddle.Create(ground + Vector3.up * 0.015f, 0.65f, slow, life, true, 0.7f, 0.18f);
            Fx.Burst(p, new Color(0.75f, 1f, 0.3f), 12, 2.5f, 0.09f);
            Sfx.Play(Sfx.Stick, p, 0.5f);
            Destroy(gameObject);
        }
    }
}
