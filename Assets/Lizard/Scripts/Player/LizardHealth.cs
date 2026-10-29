using System;
using UnityEngine;

namespace Lizards
{
    public enum DamageType { Enemy, Trap, Hazard, Impact, Debug }

    public struct DamageInfo
    {
        public DamageType type;
        public Vector3 sourcePosition;
        public float knockbackScale;
        /// <summary>Comida que se pierde; negativo = valor por defecto de la configuración.</summary>
        public float foodLoss;
        public GameObject source;
    }

    /// <summary>
    /// La reserva de comida actúa como protección:
    /// con comida → expulsa parte; vacío → renacuajo. Incluye intervalo de invulnerabilidad.
    /// </summary>
    public class LizardHealth : MonoBehaviour
    {
        LizardPlayer p;
        float invulnerableUntil;

        public bool Invulnerable => Time.time < invulnerableUntil;
        public float InvulnerableRemaining => Mathf.Max(0f, invulnerableUntil - Time.time);
        public int HitsTaken { get; private set; }
        public int HitsIgnored { get; private set; }
        public event Action<DamageInfo> OnHit;

        void Awake() => p = GetComponent<LizardPlayer>();

        public bool TakeHit(DamageInfo info)
        {
            if (Invulnerable) { HitsIgnored++; return false; }
            var cfg = p.Config;
            invulnerableUntil = Time.time + cfg.invulnerabilityTime;
            HitsTaken++;

            Vector3 dir = p.transform.position - info.sourcePosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = -p.transform.forward;
            float kb = info.knockbackScale <= 0f ? 1f : info.knockbackScale;
            float massFactor = p.Form == LizardForm.Giant ? 0.4f : 1f;
            p.Motor.ApplyKnockback(dir, cfg.knockbackForce * kb * massFactor, cfg.knockbackUp * kb * massFactor);
            p.Motor.Stun(cfg.stunTime);
            p.Tongue.Release(false);

            if (p.Form == LizardForm.Small)
            {
                p.Message("¡Ay! Eres muy pequeño: pide ayuda o busca comida");
            }
            else if (p.Stomach.Food > 0.001f)
            {
                float loss = info.foodLoss >= 0f ? info.foodLoss : cfg.damageEjectAmount;
                p.Stomach.EjectPellets(loss, (dir.normalized + Vector3.up * 1.2f).normalized, 5f, 0.6f);
                if (p.Stomach.Food <= 0.001f) p.Message("¡Estás vacío! Otro golpe te convertirá en renacuajo");
                p.EvaluateForm();
            }
            else
            {
                p.Forms.RequestForm(LizardForm.Small, true);
            }

            p.Visual?.PlayHurt();
            Sfx.Play(Sfx.Hurt, p.CenterPosition);
            Fx.Burst(p.CenterPosition, new Color(1f, 0.85f, 0.3f), 8, 3f, 0.1f);
            p.CameraRig?.Shake(0.2f);
            OnHit?.Invoke(info);
            return true;
        }
    }
}
