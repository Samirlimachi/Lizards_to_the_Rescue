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
            p.SetMouthBusy(0.3f);

            // Como la lengua: apunta al punto de la mira y se gira hacia él antes de escupir.
            Vector3 aim = AimPoint();
            p.Motor.FaceTowards(aim, 0.45f);
            StartCoroutine(SpitAfterTurn(aim));
            return true;
        }

        /// <summary>Punto al que apunta la mira (lo que tiene delante la cámara) o, si no hay nada, un punto lejano.</summary>
        Vector3 AimPoint()
        {
            if (p.Tongue && p.Tongue.AimHasHit) return p.Tongue.AimPoint;
            Transform cam = p.CameraRig ? p.CameraRig.CameraTransform : null;
            if (!cam) return p.MouthPosition + transform.forward * 20f;
            return cam.position + cam.forward * 40f;
        }

        System.Collections.IEnumerator SpitAfterTurn(Vector3 aim)
        {
            // Deja girar un instante para que el escupitajo salga de la boca ya orientada.
            yield return new WaitForSeconds(0.1f);
            var cfg = p.Config;
            Vector3 origin = p.MouthPosition;
            Vector3 dir = (aim - origin).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            // Arco suave: compensa un poco la caída para que llegue cerca del punto apuntado.
            float dist = Vector3.Distance(origin, aim);
            float lift = cfg.acidArcUp * Mathf.Clamp01(dist / 15f);
            AcidProjectile.Spawn(p, origin + dir * 0.2f, dir * cfg.acidSpeed + Vector3.up * lift);
            p.Visual?.PlaySpit();
            Sfx.Play(Sfx.Acid, origin);
        }

        Vector3 AimForward()
        {
            Transform cam = p.CameraRig ? p.CameraRig.CameraTransform : null;
            return cam ? cam.forward : transform.forward;
        }
    }
}
