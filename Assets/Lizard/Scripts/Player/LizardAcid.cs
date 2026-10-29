using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Escupitajo ácido (mecánica original de este proyecto). Reserva propia con recarga,
    /// tiempo entre disparos y prioridad de boca: no se puede disparar con la lengua fuera.
    /// </summary>
    public class LizardAcid : MonoBehaviour
    {
        LizardPlayer p;
        float nextShot;

        public float Ammo { get; private set; }
        public float CooldownRemaining => Mathf.Max(0f, nextShot - Time.time);

        void Awake() => p = GetComponent<LizardPlayer>();

        public void Refill() => Ammo = p.Config.maxAcid;

        void Update()
        {
            var cfg = p.Config;
            Ammo = Mathf.Min(cfg.maxAcid, Ammo + cfg.acidRegenPerSecond * Time.deltaTime);
        }

        public bool CanFire(out string reason)
        {
            var cfg = p.Config;
            reason = null;
            if (!p.Stats.acidEnabled) reason = "No puedes escupir ácido en esta forma";
            else if (p.Tongue.State != TongueState.Idle) reason = "La boca está ocupada con la lengua";
            else if (p.MouthBusy || Time.time < nextShot) reason = "";
            else if (Ammo < cfg.acidCost) reason = "Sin ácido: espera a que se recargue";
            return reason == null;
        }

        public bool TryFire()
        {
            if (!CanFire(out string reason))
            {
                if (!string.IsNullOrEmpty(reason)) p.Message(reason);
                return false;
            }
            var cfg = p.Config;
            Ammo -= cfg.acidCost;
            nextShot = Time.time + cfg.acidCooldown;
            p.SetMouthBusy(0.25f);

            Vector3 origin = p.MouthPosition;
            Vector3 aim = p.Tongue.AimHasHit ? p.Tongue.AimPoint : origin + AimForward() * 20f;
            Vector3 dir = (aim - origin).normalized;
            if (Vector3.Dot(dir, AimForward()) < 0.2f) dir = AimForward();
            AcidProjectile.Spawn(p, origin + dir * 0.2f, dir * cfg.acidSpeed + Vector3.up * cfg.acidArcUp);

            p.Visual?.PlaySpit();
            Sfx.Play(Sfx.Acid, origin);
            return true;
        }

        Vector3 AimForward()
        {
            Transform cam = p.CameraRig ? p.CameraRig.CameraTransform : null;
            return cam ? cam.forward : transform.forward;
        }
    }
}
