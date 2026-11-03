using System;
using UnityEngine;

namespace Lizards
{
    /// <summary>Movimiento con Rigidbody: suelo y pendientes, coyote time, buffer de salto, control aéreo y retroceso.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class LizardMotor : MonoBehaviour
    {
        LizardPlayer p;
        Rigidbody rb;

        public Vector3 MoveInput { get; set; }
        /// <summary>Corriendo (Shift). Sin correr camina a <see cref="walkFactor"/> de la velocidad.</summary>
        public bool Sprinting { get; set; }
        [Range(0.2f, 1f)] public float walkFactor = 0.5f;
        public bool Grounded { get; private set; }
        /// <summary>Flotando o nadando en una <see cref="SwimZone"/>.</summary>
        public bool Swimming { get; private set; }
        public SwimZone SwimZoneNow { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public Vector3 LastVelocity { get; private set; }
        public bool Stunned => Time.time < stunUntil;
        public bool IsTugged => Time.time < tuggedUntil;
        public float PlanarSpeed => Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).magnitude;
        public bool IsBraking { get; private set; }
        public bool Sliding => Time.time < slideUntil;
        public event Action OnSlideStart;
        float slideUntil = -10f, nextSlide;
        Vector3 facePoint; float faceUntil;

        /// <summary>Gira rápido hacia un punto durante un tiempo (p. ej. al lanzar la lengua).</summary>
        public void FaceTowards(Vector3 worldPoint, float seconds)
        {
            facePoint = worldPoint;
            faceUntil = Mathf.Max(faceUntil, Time.time + seconds);
        }
        public bool IsFacingOverride => Time.time < faceUntil;

        public event Action<float> OnLanded;
        public event Action OnJumped;

        float lastGroundedTime = -10f, lastJumpRequest = -10f, jumpLockUntil, stunUntil, tuggedUntil;
        float prevVerticalSpeed;

        void Awake()
        {
            p = GetComponent<LizardPlayer>();
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public void RequestJump() => lastJumpRequest = Time.time;

        /// <summary>Se lanza de panza en la dirección de la carrera. Solo en forma normal y yendo rápido.</summary>
        public bool TryStartSlide()
        {
            var cfg = p.Config;
            if (Sliding || Time.time < nextSlide || Stunned || p.Form != LizardForm.Normal) return false;
            Vector3 planar = Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up);
            Vector3 dir = planar.sqrMagnitude > 0.01f ? planar.normalized : MoveInput.sqrMagnitude > 0.01f ? MoveInput.normalized : transform.forward;
            if (planar.magnitude < cfg.slideMinSpeed && MoveInput.sqrMagnitude < 0.25f) return false;
            float speed = Mathf.Max(planar.magnitude, cfg.slideSpeed);
            Vector3 v = dir * speed;
            v.y = Grounded ? cfg.slideHop : Mathf.Min(rb.linearVelocity.y, 0f);
            rb.linearVelocity = v;
            if (Grounded) jumpLockUntil = Time.time + 0.1f;
            slideUntil = Time.time + cfg.slideDuration;
            nextSlide = slideUntil + cfg.slideCooldown;
            rb.MoveRotation(Quaternion.LookRotation(dir, Vector3.up));
            OnSlideStart?.Invoke();
            return true;
        }
        public void Stun(float t) => stunUntil = Mathf.Max(stunUntil, Time.time + t);
        /// <summary>Lo llama la lengua de otro jugador: reduce nuestro control en suelo para que el tirón funcione.</summary>
        public void MarkTugged(float t = 0.15f) => tuggedUntil = Mathf.Max(tuggedUntil, Time.time + t);

        public void ApplyKnockback(Vector3 dir, float force, float up)
        {
            dir = Vector3.ProjectOnPlane(dir, Vector3.up).normalized;
            rb.linearVelocity = dir * force + Vector3.up * up;
            Grounded = false;
            jumpLockUntil = Time.time + 0.15f;
        }

        void FixedUpdate()
        {
            var s = p.Stats; var cfg = p.Config; float dt = Time.fixedDeltaTime;
            bool wasGrounded = Grounded;
            CheckGround(s, cfg);
            if (Grounded && !wasGrounded) OnLanded?.Invoke(-prevVerticalSpeed);

            // En el agua: flota con la cabeza fuera; se puede saltar para salir (no cuenta si va subiendo de un salto).
            bool wasSwimming = Swimming;
            Swimming = SwimZone.Find(transform.position, out float surface, out var zone) && rb.linearVelocity.y < 2.5f
                       && !(Grounded && transform.position.y > surface - 0.25f);
            SwimZoneNow = Swimming ? zone : null;
            if (Swimming) lastGroundedTime = Time.time;
            if (Swimming && !wasSwimming) Fx.Burst(transform.position + Vector3.up * 0.4f, new Color(0.85f, 1f, 0.9f), 14, 3f, 0.1f);

            // Salto con coyote time y buffer.
            bool canCoyote = Time.time - lastGroundedTime <= cfg.coyoteTime;
            if (Time.time - lastJumpRequest <= cfg.jumpBufferTime && canCoyote && Time.time >= jumpLockUntil && !Stunned && s.jumpVelocity > 0f)
            {
                Vector3 v0 = rb.linearVelocity;
                v0.y = Mathf.Max(v0.y, 0f) + s.jumpVelocity;
                v0.y = Mathf.Min(v0.y, s.jumpVelocity * 1.2f);
                rb.linearVelocity = v0;
                lastJumpRequest = lastGroundedTime = -10f;
                jumpLockUntil = Time.time + 0.15f;
                Grounded = false;
                OnJumped?.Invoke();
            }

            Vector3 v = rb.linearVelocity;
            Vector3 desired = MoveInput * s.moveSpeed * (Sprinting || p.Form == LizardForm.Giant ? 1f : walkFactor);
            float control = Stunned ? 0.1f : IsTugged ? 0.15f : 1f;
            bool tongueAnchored = p.Tongue && p.Tongue.IsSwinging;

            // Nadando: empuje de flotación (con un leve vaivén), sin gravedad y más lento que en tierra.
            if (Swimming && !tongueAnchored && Time.time >= jumpLockUntil) // recién saltado: deja salir del agua
            {
                IsBraking = false;
                float scaleK = Mathf.Max(0.5f, s.visualScale);
                float targetY = surface - Mathf.Lerp(0.5f, 0.22f, Mathf.Clamp01(Vector3.ProjectOnPlane(v, Vector3.up).magnitude / 2f)) * scaleK + Mathf.Sin(Time.time * 2.2f) * 0.04f;
                float ay = (targetY - transform.position.y) * 22f - v.y * 5f;
                rb.AddForce(-Physics.gravity + Vector3.up * ay, ForceMode.Acceleration);
                Vector3 hs = new Vector3(v.x, 0f, v.z);
                Vector3 want = new Vector3(desired.x, 0f, desired.z) * 0.55f;
                hs = Vector3.MoveTowards(hs, want, (want.sqrMagnitude > 0.01f ? s.groundAccel * 0.45f : s.groundDecel * 0.25f) * dt);
                rb.linearVelocity = hs + Vector3.up * rb.linearVelocity.y;
                if (MoveInput.sqrMagnitude > 0.01f)
                    rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(MoveInput.normalized, Vector3.up), s.turnSpeed * 0.6f * dt));
                prevVerticalSpeed = rb.linearVelocity.y;
                LastVelocity = rb.linearVelocity;
                return;
            }

            // Deslizándose: conserva el impulso, frena poco y solo se puede girar un poco.
            if (Sliding && !tongueAnchored)
            {
                Vector3 planarS = Vector3.ProjectOnPlane(v, Vector3.up);
                if (MoveInput.sqrMagnitude > 0.01f && planarS.sqrMagnitude > 0.01f)
                    planarS = Vector3.RotateTowards(planarS, MoveInput.normalized * planarS.magnitude, cfg.slideSteer * Mathf.Deg2Rad * dt, 0f);
                if (Grounded) planarS = Vector3.MoveTowards(planarS, Vector3.zero, cfg.slideDecel * dt);
                rb.linearVelocity = planarS + Vector3.up * v.y;
                if (!Grounded && v.y < 0f) rb.AddForce(Physics.gravity * (cfg.fallGravityMultiplier - 1f), ForceMode.Acceleration);
                if (Grounded && planarS.magnitude < 1.5f) slideUntil = Time.time;
                if (planarS.sqrMagnitude > 0.04f)
                    rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(planarS.normalized, Vector3.up), 400f * dt));
                prevVerticalSpeed = rb.linearVelocity.y;
                LastVelocity = rb.linearVelocity;
                return;
            }

            if (Grounded && !tongueAnchored)
            {
                Vector3 planar = Vector3.ProjectOnPlane(v, GroundNormal);
                Vector3 target = Vector3.ProjectOnPlane(desired, GroundNormal).normalized * desired.magnitude;
                bool accelerating = desired.sqrMagnitude > 0.01f;
                IsBraking = accelerating && Vector3.Dot(planar, target) < 0f && planar.magnitude > 2f;
                float rate = accelerating && !IsBraking ? s.groundAccel : s.groundDecel;
                if (IsBraking) rate = Mathf.Max(s.groundDecel, s.groundAccel * 0.5f);
                Vector3 newPlanar = Vector3.MoveTowards(planar, target, rate * control * dt);
                if (control < 1f) newPlanar = Vector3.Lerp(planar, newPlanar, control);
                rb.linearVelocity = newPlanar + (v - planar);

                // Evita resbalar en pendientes cuando está quieto (el gigante sí rueda cuesta abajo).
                if (s.holdOnSlopes && !IsTugged)
                    rb.AddForce(-Vector3.ProjectOnPlane(Physics.gravity, GroundNormal), ForceMode.Acceleration);
                // Pegado ligero al suelo.
                if (Time.time >= jumpLockUntil) rb.AddForce(-GroundNormal * 5f, ForceMode.Acceleration);
            }
            else
            {
                IsBraking = false;
                Vector3 h = new Vector3(v.x, 0f, v.z);
                Vector3 target = new Vector3(desired.x, 0f, desired.z);
                if (target.sqrMagnitude > 0.01f)
                {
                    float accel = tongueAnchored ? s.airAccel * 0.6f : s.airAccel;
                    Vector3 dv = Vector3.ClampMagnitude(target - h, accel * dt) * control;
                    // Conserva el impulso por encima de la velocidad máxima (p. ej. tras balancearse).
                    if (h.magnitude > s.moveSpeed && Vector3.Dot(dv, h) > 0f) dv -= Vector3.Project(dv, h);
                    rb.linearVelocity += dv;
                }
                if (rb.linearVelocity.y < 0f && !tongueAnchored)
                    rb.AddForce(Physics.gravity * (cfg.fallGravityMultiplier - 1f), ForceMode.Acceleration);
                if (rb.linearVelocity.y < -cfg.maxFallSpeed)
                {
                    var c = rb.linearVelocity; c.y = -cfg.maxFallSpeed; rb.linearVelocity = c;
                }
            }

            // Orientación (solo yaw; el cuerpo nunca rota físicamente).
            Vector3 face = p.Form == LizardForm.Giant ? Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up) : MoveInput;
            if (p.Tongue && p.Tongue.State == TongueState.Attached && !p.Tongue.IsSwinging && MoveInput.sqrMagnitude < 0.01f)
                face = Vector3.ProjectOnPlane(p.Tongue.AttachPoint - transform.position, Vector3.up);
            float turn = s.turnSpeed;
            if (IsFacingOverride && p.Form != LizardForm.Giant)
            {
                Vector3 toTarget = Vector3.ProjectOnPlane(facePoint - transform.position, Vector3.up);
                if (toTarget.sqrMagnitude > 0.04f) { face = toTarget; turn = Mathf.Max(turn, 1440f); }
            }
            if (face.sqrMagnitude > 0.04f)
            {
                Quaternion want = Quaternion.LookRotation(face.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, want, turn * dt));
            }

            prevVerticalSpeed = rb.linearVelocity.y;
            LastVelocity = rb.linearVelocity;
        }

        void CheckGround(FormStats s, LizardConfig cfg)
        {
            float r = s.colliderRadius * 0.9f;
            Vector3 origin = rb.position + Vector3.up * (r + 0.05f);
            float dist = 0.05f + cfg.groundCheckDistance * Mathf.Max(1f, s.visualScale * 0.5f);
            Grounded = false;
            if (Time.time < jumpLockUntil) return;

            var hits = Physics.SphereCastAll(origin, r, Vector3.down, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider.attachedRigidbody == rb || h.distance <= 0f) continue;
                if (Vector3.Angle(h.normal, Vector3.up) > cfg.maxSlopeAngle) continue;
                if (h.distance < best) { best = h.distance; GroundNormal = h.normal; Grounded = true; }
            }
            if (Grounded) lastGroundedTime = Time.time; else GroundNormal = Vector3.up;
        }
    }
}
