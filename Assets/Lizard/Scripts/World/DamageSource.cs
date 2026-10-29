using UnityEngine;

namespace Lizards
{
    /// <summary>Fuente de daño configurable (enemigo, trampa, superficie peligrosa). Funciona con colisión o trigger.</summary>
    public class DamageSource : MonoBehaviour
    {
        public DamageType type = DamageType.Trap;
        public float knockbackScale = 1f;
        [Tooltip("Negativo = usar el valor por defecto de la configuración.")]
        public float foodLoss = -1f;
        public bool damageOnStay = true;
        public bool enabledDamage = true;

        void OnCollisionEnter(Collision c) => Hit(c.collider);
        void OnCollisionStay(Collision c) { if (damageOnStay) Hit(c.collider); }
        void OnTriggerEnter(Collider c) => Hit(c);
        void OnTriggerStay(Collider c) { if (damageOnStay) Hit(c); }

        void Hit(Collider c)
        {
            if (!enabledDamage) return;
            var p = c.GetComponentInParent<LizardPlayer>();
            if (!p) return;
            // El intervalo de invulnerabilidad del jugador evita daño repetido por segundo.
            p.Health.TakeHit(new DamageInfo { type = type, sourcePosition = transform.position, knockbackScale = knockbackScale, foodLoss = foodLoss, source = gameObject });
        }
    }
}
