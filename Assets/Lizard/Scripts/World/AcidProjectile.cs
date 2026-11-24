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
        bool cosmetic;
        Rigidbody rb;
        Transform blob;
        float dieAt, born, nextDrip, size;
        bool done;

        /// <summary>cosmetic = copia de red del escupitajo de otro jugador: se ve y salpica igual, pero no hace daño
        /// (el daño ya lo aplicó el escupitajo de verdad, en el cliente de quien lo lanzó).</summary>
        public static AcidProjectile Spawn(LizardPlayer owner, Vector3 pos, Vector3 velocity, bool cosmetic = false)
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
            a.owner = owner; a.rb = rb; a.blob = vis.transform; a.size = 0.26f * scale; a.cosmetic = cosmetic;
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
            if (done) return;
            rb.AddForce(Physics.gravity * owner.Config.acidGravityScale, ForceMode.Acceleration);
            // Agua: salpica en la superficie (antes la atravesaba y se estampaba en el fondo, sin verse).
            if (SwimZone.Find(transform.position, out float surface, out _) && transform.position.y < surface + 0.05f) { WaterSplash(surface); return; }
            // Se acabó el vuelo sin tocar nada: revienta en el aire (antes dejaba una mancha flotando).
            if (Time.time > dieAt) AirBurst();
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
                // Fuego amigo: solo cuenta en el cliente del que recibe el golpe (su lagarto es el de verdad).
                // Por eso lo aplica la copia de red del escupitajo cuando toca a nuestro jugador local.
                if (cfg.acidFriendlyFire && player.IsControlled)
                    player.Health.TakeHit(new DamageInfo { type = DamageType.Enemy, sourcePosition = transform.position, foodLoss = -1, source = gameObject });
            }
            var enemy = c.collider.GetComponentInParent<EnemyHealth>();
            if (!cosmetic)
            {
                // Daño a enemigos y a la membrana: solo el escupitajo de verdad (la red ya lo reparte a los demás).
                enemy?.Damage(cfg.acidDamage, transform.position);
                c.collider.GetComponentInParent<AcidVulnerable>()?.HitByAcid(c.GetContact(0).point);
            }
            var contact = c.GetContact(0);
            Vector3 normal = contact.normal.sqrMagnitude > 0.01f ? contact.normal : -rb.linearVelocity.normalized;
            if (enemy || player)
                HitCreature(contact.point, normal, enemy ? enemy.transform : player.transform);   // a un ser vivo no se le pega una mancha
            else if (c.rigidbody && !c.rigidbody.isKinematic)
                Splat(contact.point, normal, c.rigidbody.transform, c.collider, 0.7f);           // caja o comida: mancha pequeña que viaja con ella
            else
                Splat(contact.point, normal, c.collider.transform, c.collider, 1f);              // pared, suelo, puerta…
        }

        /// <summary>Superficie: mancha pegada a ras + corona de salpicadura.</summary>
        void Splat(Vector3 at, Vector3 normal, Transform stickTo, Collider surface, float scale)
        {
            if (done) return;
            done = true;
            GooSplat.Spawn(at, normal, size * 2.6f * scale, stickTo, surface);
            GooCrown.Spawn(at, normal, size * 1.8f * scale);
            Fx.Burst(at + normal * 0.05f, acid, 8, 2.5f, size * 0.3f);
            Sfx.Play(Sfx.Stick, at, 0.6f);
            Sfx.Play(Sfx.Dissolve, at, 0.35f);
            Destroy(gameObject);
        }

        /// <summary>Enemigo o jugador: ¡plaf! con corona, chisporroteo de ácido que le sigue y gotas que caen al suelo.</summary>
        void HitCreature(Vector3 at, Vector3 normal, Transform target)
        {
            if (done) return;
            done = true;
            GooCrown.Spawn(at, normal, size * 2f);
            AcidSizzle.Attach(target, at);
            Fx.Burst(at + normal * 0.05f, acid, 12, 3.5f, size * 0.3f);
            Fx.Burst(at + normal * 0.05f, new Color(0.85f, 1f, 0.6f), 5, 2f, size * 0.15f);
            Vector3 back = (normal + Vector3.up * 0.6f).normalized;
            for (int i = 0; i < 6; i++)
                GooDrop.Spawn(at + normal * 0.1f, (back + Random.insideUnitSphere * 0.7f).normalized * Random.Range(2f, 3.5f), size * Random.Range(0.14f, 0.24f));
            Sfx.Play(Sfx.Eject, at, 0.6f);
            Destroy(gameObject);
        }

        /// <summary>No tocó nada: revienta en el aire y las gotas caen al suelo.</summary>
        void AirBurst()
        {
            if (done) return;
            done = true;
            Fx.Burst(transform.position, acid, 8, 2f, size * 0.3f);
            Vector3 v = rb.linearVelocity * 0.3f;
            for (int i = 0; i < 5; i++)
                GooDrop.Spawn(transform.position, v + Random.insideUnitSphere * 1.5f, size * Random.Range(0.15f, 0.25f));
            Sfx.Play(Sfx.Eject, transform.position, 0.3f);
            Destroy(gameObject);
        }

        /// <summary>Cae al agua: salpicadura en la superficie y se disuelve.</summary>
        void WaterSplash(float surface)
        {
            if (done) return;
            done = true;
            Vector3 at = new Vector3(transform.position.x, surface + 0.02f, transform.position.z);
            Fx.Burst(at, new Color(0.8f, 1f, 0.95f), 12, 3f, size * 0.3f);
            Fx.Burst(at, acid, 6, 1.5f, size * 0.25f);
            Sfx.Play(Sfx.Land, at, 0.45f);
            Sfx.Play(Sfx.Dissolve, at, 0.3f);
            Destroy(gameObject);
        }
    }
}
