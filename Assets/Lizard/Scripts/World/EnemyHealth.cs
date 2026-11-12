using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Enemigo con vida: lo dañan el ácido y el gigante al rodar.</summary>
    public class EnemyHealth : MonoBehaviour
    {
        public float health = 2f;
        [Tooltip("Si es falso, el propio enemigo gestiona su muerte (p. ej. el slime con ragdoll).")]
        public bool destroyOnDeath = true;
        /// <summary>Multiplicador de daño recibido (p. ej. el slime expuesto tras su salto).</summary>
        public float damageMultiplier = 1f;
        /// <summary>(daño, desde) — cada golpe recibido.</summary>
        public event System.Action<float, Vector3> OnDamaged;
        /// <summary>(desde) — al quedarse sin vida.</summary>
        public event System.Action<Vector3> OnDied;
        public bool Dead => health <= 0f;
        /// <summary>Multijugador: si devuelve true, el daño se reenvía al cliente maestro (que es quien lleva la vida).</summary>
        public static System.Func<EnemyHealth, float, Vector3, bool> RemoteDamage;
        Renderer[] rends;
        float flashUntil;
        MaterialPropertyBlock mpb;
        public Color flashColor = Color.white;

        void Awake() { rends = GetComponentsInChildren<Renderer>(); mpb = new MaterialPropertyBlock(); }

        public void Damage(float amount, Vector3 from)
        {
            if (health <= 0f) return;
            if (RemoteDamage != null && RemoteDamage(this, amount, from)) { flashUntil = Time.time + 0.15f; Sfx.Play(Sfx.Hurt, transform.position, 0.5f); return; }
            amount *= damageMultiplier;
            health -= amount;
            OnDamaged?.Invoke(amount, from);
            flashUntil = Time.time + 0.15f;
            Sfx.Play(Sfx.Hurt, transform.position, 0.5f);
            if (health <= 0f)
            {
                Fx.Burst(transform.position, new Color(0.6f, 0.2f, 0.7f), 20, 4f, 0.15f);
                Sfx.Play(Sfx.Break, transform.position);
                OnDied?.Invoke(from);
                if (destroyOnDeath) Destroy(gameObject);
            }
        }

        void Update()
        {
            bool f = Time.time < flashUntil;
            foreach (var r in rends)
            {
                if (!r) continue;
                if (f) { mpb.SetColor("_BaseColor", flashColor); r.SetPropertyBlock(mpb); }
                else r.SetPropertyBlock(null);
            }
        }
    }
}
