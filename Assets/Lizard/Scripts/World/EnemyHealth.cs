using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Enemigo con vida: lo dañan el ácido y el gigante al rodar.
    /// Cada golpe se nota: destello blanco, número de daño, barra de vida flotante y la reacción propia del enemigo
    /// (la babosa se aplasta, retrocede y se enfada; el slime tiene su animación de golpe).
    /// En multijugador la vida la lleva el maestro; quien golpea ve la reacción al momento y los demás la reciben por red.
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        public float health = 2f;
        [Tooltip("Si es falso, el propio enemigo gestiona su muerte (p. ej. el slime con ragdoll).")]
        public bool destroyOnDeath = true;
        /// <summary>Multiplicador de daño recibido (p. ej. el slime expuesto tras su salto).</summary>
        public float damageMultiplier = 1f;
        [Tooltip("Barra de vida flotante que aparece al recibir daño.")]
        public bool showHealthBar = true;
        /// <summary>(daño, desde) — cada golpe recibido.</summary>
        public event System.Action<float, Vector3> OnDamaged;
        /// <summary>(desde) — al quedarse sin vida.</summary>
        public event System.Action<Vector3> OnDied;
        public bool Dead => health <= 0f;
        public float MaxHealth { get; private set; }
        /// <summary>Multijugador: si devuelve true, el daño se reenvía al cliente maestro (que es quien lleva la vida).</summary>
        public static System.Func<EnemyHealth, float, Vector3, bool> RemoteDamage;

        [Tooltip("Color del destello al recibir un golpe (por encima de 1 = brilla aunque el material use colores de vértice).")]
        public Color flashColor = new Color(2.6f, 2.6f, 2.6f);
        Renderer[] rends;
        float flashUntil, tintUntil, predictedUntil;
        Color tint;
        bool painted;
        MaterialPropertyBlock mpb;
        EnemyHealthBar bar;

        void Awake()
        {
            rends = GetComponentsInChildren<Renderer>(); mpb = new MaterialPropertyBlock();
            MaxHealth = Mathf.Max(0.01f, health);
            if (showHealthBar) { bar = gameObject.AddComponent<EnemyHealthBar>(); bar.Init(this); }
        }

        public void Damage(float amount, Vector3 from)
        {
            if (health <= 0f) return;
            if (RemoteDamage != null && RemoteDamage(this, amount, from))
            {
                // Copia de red: el maestro decide, pero quien golpea ve la reacción sin esperar (predicción).
                predictedUntil = Time.time + 0.5f;
                Feedback(amount * damageMultiplier, from, false);
                return;
            }
            amount *= damageMultiplier;
            health -= amount;
            OnDamaged?.Invoke(amount, from);
            Feedback(amount, from, health <= 0f);
            if (health <= 0f)
            {
                OnDied?.Invoke(from);
                // Enemigos sencillos: muerte animada con reventón. Los que gestionan su muerte (slime) ponen su propio efecto.
                if (destroyOnDeath) EnemyPoofDeath.Play(gameObject, from);
            }
        }

        /// <summary>Golpe que llega por red (lo aplicó el maestro): vida actual y reacción, sin repetirla si ya la predijimos.</summary>
        public void NetHit(float amount, Vector3 from, float healthLeft)
        {
            if (health <= 0f) return;
            health = Mathf.Max(0.001f, healthLeft);   // la muerte llega aparte (RPC_EnemyDied)
            if (Time.time < predictedUntil) { predictedUntil = 0f; return; }
            Feedback(amount, from, false);
        }

        /// <summary>Todo lo que hace que un golpe se sienta: destello, número, barra, sonido y la reacción del enemigo.</summary>
        void Feedback(float amount, Vector3 from, bool lethal)
        {
            flashUntil = Time.time + 0.12f;
            Sfx.Play(Sfx.Hurt, transform.position, 0.5f);
            Bounds b = VisualBounds();
            DamagePopup.Spawn(new Vector3(b.center.x, b.max.y + 0.1f, b.center.z), amount, lethal || amount >= 2f);
            if (bar) bar.Ping();
            var slug = GetComponent<SlugCrawl>(); if (slug && !lethal) slug.Hit(amount, from);
        }

        /// <summary>Tinte temporal (p. ej. la babosa enfadada se pone roja).</summary>
        public void Tint(Color c, float seconds) { tint = c; tintUntil = Time.time + seconds; }

        Bounds VisualBounds()
        {
            bool any = false; var b = new Bounds(transform.position, Vector3.one * 0.5f);
            foreach (var r in rends) { if (!r || !r.enabled) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        void Update()
        {
            bool flash = Time.time < flashUntil, tinted = Time.time < tintUntil;
            if (!flash && !tinted)
            {
                if (painted) { foreach (var r in rends) if (r) r.SetPropertyBlock(null); painted = false; }
                return;
            }
            Color c = flash ? flashColor : Color.Lerp(Color.white, tint, 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
            foreach (var r in rends)
            {
                if (!r) continue;
                r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", c); r.SetPropertyBlock(mpb);
            }
            painted = true;
        }
    }
}
