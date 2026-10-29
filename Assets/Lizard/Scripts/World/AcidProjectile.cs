using UnityEngine;

namespace Lizards
{
    public class AcidProjectile : MonoBehaviour
    {
        static readonly Color acid = new Color(0.45f, 1f, 0.15f);
        static Material mat;

        LizardPlayer owner;
        Rigidbody rb;
        float dieAt;
        bool done;

        public static AcidProjectile Spawn(LizardPlayer owner, Vector3 pos, Vector3 velocity)
        {
            mat ??= LizardMaterials.Lit(acid, 0.9f, acid * 0.6f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ácido";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.25f;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var col = go.GetComponent<SphereCollider>();
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearVelocity = velocity;
            foreach (var c in owner.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, c);
            var a = go.AddComponent<AcidProjectile>();
            a.owner = owner; a.rb = rb; a.dieAt = Time.time + owner.Config.acidLifetime;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.15f; trail.startWidth = 0.18f; trail.endWidth = 0f;
            trail.sharedMaterial = LizardMaterials.Particle();
            trail.startColor = acid; trail.endColor = new Color(acid.r, acid.g, acid.b, 0f);
            return a;
        }

        void FixedUpdate()
        {
            rb.AddForce(Physics.gravity * owner.Config.acidGravityScale, ForceMode.Acceleration);
            if (Time.time > dieAt) Splash(transform.position);
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
            Splash(c.GetContact(0).point);
        }

        void Splash(Vector3 at)
        {
            if (done) return;
            done = true;
            Fx.Burst(at, acid, 14, 3.5f, 0.1f);
            Sfx.Play(Sfx.Dissolve, at, 0.5f);
            Destroy(gameObject);
        }
    }
}
