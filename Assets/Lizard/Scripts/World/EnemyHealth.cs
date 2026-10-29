using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Enemigo con vida: lo dañan el ácido y el gigante al rodar.</summary>
    public class EnemyHealth : MonoBehaviour
    {
        public float health = 2f;
        Renderer[] rends;
        float flashUntil;
        MaterialPropertyBlock mpb;

        void Awake() { rends = GetComponentsInChildren<Renderer>(); mpb = new MaterialPropertyBlock(); }

        public void Damage(float amount, Vector3 from)
        {
            if (health <= 0f) return;
            health -= amount;
            flashUntil = Time.time + 0.15f;
            Sfx.Play(Sfx.Hurt, transform.position, 0.5f);
            if (health <= 0f)
            {
                Fx.Burst(transform.position, new Color(0.6f, 0.2f, 0.7f), 20, 4f, 0.15f);
                Sfx.Play(Sfx.Break, transform.position);
                Destroy(gameObject);
            }
        }

        void Update()
        {
            bool f = Time.time < flashUntil;
            foreach (var r in rends)
            {
                if (f) { mpb.SetColor("_BaseColor", Color.white); r.SetPropertyBlock(mpb); }
                else r.SetPropertyBlock(null);
            }
        }
    }
}
