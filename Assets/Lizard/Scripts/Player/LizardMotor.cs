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
        public bool Grounded { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public Vector3 LastVelocity { get; private set; }
        public bool Stunned => Time.time < stunUntil;
        public bool IsTugged => Time.time < tuggedUntil;
        public float PlanarSpeed => Vector3.ProjectOnPlane(rb.linearVelocity, Vector3.up).magnitude;
        public bool IsBraking { get; private set; }

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
            Vector3 desired = MoveInput * s.moveSpeed;
            float control = Stunned ? 0.1f : IsTugged ? 0.15f : 1f;
            bool tongueAnchored = p.Tongue && p.Tongue.IsSwinging;

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
            if (face.sqrMagnitude > 0.04f)
            {
                Quaternion want = Quaternion.LookRotation(face.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, want, s.turnSpeed * dt));
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
