using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Escupitajo de moco ácido: gota verde opaca con contorno que se estira y tiembla en vuelo,
    /// suelta goteritas y al impactar deja una mancha pegada a la superficie.
    /// </summary>
    public class AcidProjectile : MonoBehaviour
    {
        static readonly Color acid = new Color(0.45f, 0.86f, 0.18f);

        LizardPlayer owner;
        Rigidbody rb;
        Transform blob;
        float dieAt, born, nextDrip, size;
        bool done;

        public static AcidProjectile Spawn(LizardPlayer owner, Vector3 pos, Vector3 velocity)
        {
            var go = new GameObject("Escupitajo");
            go.transform.position = pos;
            var col = go.AddComponent<SphereCollider>();
            float scale = Mathf.Max(0.6f, owner.Stats.visualScale);
            col.radius = 0.12f * scale;
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearVelocity = velocity;
            rb.freezeRotation = true;
            foreach (var c in owner.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, c);

            // Gota visual (hija, para poder estirarla sin tocar la colisión).
            var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            vis.name = "Gota";
            Object.Destroy(vis.GetComponent<Collider>());
            vis.GetComponent<Renderer>().sharedMaterial = GooSplat.GooMaterial;
            vis.transform.SetParent(go.transform, false);

            var a = go.AddComponent<AcidProjectile>();
            a.owner = owner; a.rb = rb; a.blob = vis.transform; a.size = 0.26f * scale;
            a.born = Time.time; a.dieAt = Time.time + owner.Config.acidLifetime;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.12f; trail.startWidth = a.size * 0.7f; trail.endWidth = 0f;
            trail.sharedMaterial = LizardMaterials.Particle();
            trail.startColor = acid; trail.endColor = new Color(acid.r, acid.g, acid.b, 0f);
            trail.numCapVertices = 4;

            // Pequeña salpicadura al salir de la boca.
            for (int i = 0; i < 3; i++)
                GooDrop.Spawn(pos, velocity * 0.3f + Random.insideUnitSphere * 1.5f, a.size * 0.25f);
            return a;
        }

        void FixedUpdate()
        {
            rb.AddForce(Physics.gravity * owner.Config.acidGravityScale, ForceMode.Acceleration);
            if (Time.time > dieAt) Splat(transform.position, Vector3.up, null);
        }

        void LateUpdate()
        {
            if (done) return;
            // Se estira en la dirección del vuelo y tiembla como gelatina.
            Vector3 v = rb.linearVelocity;
            float t = Time.time - born;
            float stretch = 1f + Mathf.Min(v.magnitude * 0.035f, 0.7f);
            float wob = 1f + Mathf.Sin(t * 35f) * 0.1f;
            if (v.sqrMagnitude > 0.01f) blob.rotation = Quaternion.LookRotation(v.normalized) * Quaternion.Euler(0f, 0f, t * 400f);
            blob.localScale = new Vector3(size * wob, size / wob, size * stretch);

            if (Time.time >= nextDrip)
            {
                nextDrip = Time.time + 0.06f;
                GooDrop.Spawn(transform.position - v.normalized * size * 0.4f, v * 0.15f + Random.insideUnitSphere * 0.5f, size * Random.Range(0.15f, 0.25f));
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (done) return;
            var cfg = owner.Config;
            var player = c.collider.GetComponentInParent<LizardPlayer>();
            if (player)
            {
                if (player == owner) return;
                if (cfg.acidFriendlyFire)
                    player.Health.TakeHit(new DamageInfo { type = DamageType.Enemy, sourcePosition = transform.position, foodLoss = -1, source = gameObject });
            }
            c.collider.GetComponentInParent<EnemyHealth>()?.Damage(cfg.acidDamage, transform.position);
            c.collider.GetComponentInParent<AcidVulnerable>()?.HitByAcid();
            var contact = c.GetContact(0);
            // Se pega a lo estático o cinemático; sobre jugadores y objetos que se mueven solo salpica.
            Transform stick = c.rigidbody && !c.rigidbody.isKinematic ? c.rigidbody.transform : c.collider.transform;
            Splat(contact.point, contact.normal, player ? null : stick);
        }

        void Splat(Vector3 at, Vector3 normal, Transform stickTo)
        {
            if (done) return;
            done = true;
            GooSplat.Spawn(at, normal, size * 2.6f, stickTo);
            Fx.Burst(at, acid, 8, 2.5f, size * 0.35f);
            Sfx.Play(Sfx.Dissolve, at, 0.5f);
            Destroy(gameObject);
        }
    }
}
