using System;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Máquina de estados de forma (normal, renacuajo, gigante). Actualiza colisión, masa, habilidades y visual
    /// de forma coordinada. Antes de crecer comprueba si hay espacio; si no, intenta desplazarse un poco
    /// y, si tampoco cabe, aplaza la transformación hasta que haya sitio.
    /// </summary>
    public class LizardForms : MonoBehaviour
    {
        static readonly Collider[] overlapBuffer = new Collider[32];

        LizardPlayer p;
        Rigidbody rb;
        CapsuleCollider capsule;
        SphereCollider sphere;

        public LizardForm Current { get; private set; } = LizardForm.Normal;
        public LizardForm? Pending { get; private set; }
        public event Action<LizardForm, LizardForm> OnFormChanged;

        float nextPendingCheck;

        void Awake()
        {
            p = GetComponent<LizardPlayer>();
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            sphere = GetComponent<SphereCollider>();
        }

        public bool RequestForm(LizardForm f, bool force = false)
        {
            if (f == Current) { Pending = null; return true; }
            if (!force && !TryMakeRoom(f))
            {
                if (Pending != f) p.Message("¡No hay espacio para transformarte! Sal a una zona abierta");
                Pending = f;
                return false;
            }
            Pending = null;
            Apply(f, true);
            return true;
        }

        public void ApplyImmediate(LizardForm f) { Pending = null; Apply(f, false); }

        void Update()
        {
            if (Pending.HasValue && Time.time >= nextPendingCheck)
            {
                nextPendingCheck = Time.time + 0.25f;
                var f = Pending.Value;
                if (TryMakeRoom(f)) { Pending = null; Apply(f, true); }
            }
        }

        void Apply(LizardForm f, bool animate)
        {
            var prev = Current;
            Current = f;
            var s = p.Config.Get(f);

            if (s.useSphere)
            {
                sphere.enabled = true; capsule.enabled = false;
                sphere.radius = s.colliderRadius; sphere.center = Vector3.up * s.colliderRadius;
            }
            else
            {
                capsule.enabled = true; sphere.enabled = false;
                capsule.radius = s.colliderRadius; capsule.height = s.colliderHeight; capsule.center = Vector3.up * (s.colliderHeight * 0.5f);
            }
            rb.mass = s.mass;
            if (prev == LizardForm.Giant && f != LizardForm.Giant)
                rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, p.Config.normal.moveSpeed);

            p.Tongue?.OnFormChanged();
            p.Visual?.OnFormChanged(prev, f, animate);
            if (animate)
            {
                Sfx.Play(Sfx.Transform, p.CenterPosition);
                Fx.Burst(p.CenterPosition, new Color(0.55f, 0.9f, 0.3f), 18, 3f, 0.18f * s.visualScale);
                p.Message(f == LizardForm.Giant ? "¡Forma GIGANTE!" : f == LizardForm.Small ? "¡Forma RENACUAJO! Pide ayuda o busca comida" : "Forma normal");
            }
            OnFormChanged?.Invoke(prev, f);
        }

        /// <summary>Comprueba si la forma cabe; si no, prueba pequeños desplazamientos libres.</summary>
        bool TryMakeRoom(LizardForm f)
        {
            var target = p.Config.Get(f);
            var cur = p.Config.Get(Current);
            if (target.colliderRadius <= cur.colliderRadius && target.colliderHeight <= cur.colliderHeight) return true;
            if (Fits(rb.position, target)) return true;

            float step = target.colliderRadius * 0.6f;
            for (int ring = 1; ring <= 2; ring++)
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    Vector3 off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * step * ring;
                    Vector3 pos = rb.position + off + Vector3.up * 0.05f;
                    if (Fits(pos, target) && !Physics.Linecast(p.CenterPosition, pos + Vector3.up * cur.colliderRadius, ~0, QueryTriggerInteraction.Ignore))
                    {
                        rb.position = pos; transform.position = pos;
                        return true;
                    }
                }
            return false;
        }

        public bool Fits(Vector3 feet, FormStats s)
        {
            const float skin = 0.03f;
            int n;
            if (s.useSphere)
                n = Physics.OverlapSphereNonAlloc(feet + Vector3.up * (s.colliderRadius + skin), s.colliderRadius - skin, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            else
            {
                Vector3 a = feet + Vector3.up * (s.colliderRadius + skin);
                Vector3 b = feet + Vector3.up * (s.colliderHeight - s.colliderRadius);
                n = Physics.OverlapCapsuleNonAlloc(a, b, s.colliderRadius - skin, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            }
            for (int i = 0; i < n; i++)
            {
                var c = overlapBuffer[i];
                var body = c.attachedRigidbody;
                if (body == rb) continue;
                // Objetos dinámicos ligeros se apartan solos; no bloquean.
                if (body && !body.isKinematic && body.mass < s.mass * 0.5f) continue;
                return false;
            }
            return true;
        }
    }
}
