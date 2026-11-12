using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Slime de las alcantarillas. Las animaciones (en el sitio) solo deforman el cuerpo; este código mueve al slime,
    /// fija la dirección del salto, aplica el daño al aterrizar, lanza las gotas de gel y controla el ragdoll.
    ///
    /// Fotogramas clave (30 FPS) que usa este código:
    ///   HopMove 0–24 (bucle):   despegue 6 · aterrizaje 17
    ///   Alert 0–12:             erguido 8
    ///   AttackLeap 0–54:        despegue 15 · impacto 27 (único daño) · expuesto 27–48
    ///   AttackSplash 0–30:      expulsión de gel 19
    ///   HitReact 0–9 · Death 0–27 (charco)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SlimeEnemy : MonoBehaviour
    {
        public const float Fps = 30f;
        // Fotogramas de los clips (deben coincidir con Tools/Blender/slime_build.py).
        public const int HopLen = 24, HopTakeoff = 6, HopLand = 17;
        public const int AlertLen = 12;
        public const int LeapLen = 54, LeapTakeoff = 15, LeapImpact = 27, LeapVulnerableEnd = 48;
        public const int SplashLen = 30, SplashExpel = 19;
        public const int HitLen = 9;

        [Header("Partes")]
        public Animator animator;
        public Transform midBone, topBone, mouth;
        public SlimeRagdoll ragdoll;
        public EnemyHealth health;
        public SlimeFace face;

        [Header("Percepción")]
        public float sightRange = 10f, loseRange = 16f;
        [Header("Movimiento a saltitos")]
        public float hopDistance = 1.1f, wanderRadius = 3.5f, turnSpeed = 300f;
        [Header("Salto de ataque")]
        public float leapMin = 2.2f, leapMax = 6f, leapCooldown = 3.5f, leapDamageRadius = 1.35f, leapKnockback = 1.4f;
        [Header("Salpicadura de gel")]
        public float splashMin = 3.5f, splashMax = 10f, splashCooldown = 5.5f, splashSpread = 1.7f;
        public float puddleSlow = 0.5f, puddleLife = 5f;

        enum State { Idle, Alert, Hop, Leap, Splash, Hit, Dead }
        State state = State.Idle;
        float stateStart, nextLeap, nextSplash, nextWander;
        Rigidbody rb;
        Collider[] ownColliders;
        Vector3 home, hopTarget, leapTarget;
        bool hopToPlayer, launched, landed, holding, expelled;
        LizardPlayer target;
        // Inclinación procedimental hacia el lado del golpe (se suma a HitReact).
        Vector3 hitLeanDir; float hitLeanTime = -10f;

        float Frame => (Time.time - stateStart) * Fps;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (!health) health = GetComponent<EnemyHealth>();
            if (!ragdoll) ragdoll = GetComponent<SlimeRagdoll>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!face) face = GetComponentInChildren<SlimeFace>();
            ownColliders = GetComponentsInChildren<Collider>(true);
            home = transform.position;
            if (health)
            {
                health.destroyOnDeath = false;
                health.OnDamaged += OnDamaged;
                health.OnDied += OnDied;
            }
        }

        void Start() { Enter(State.Idle); nextWander = Time.time + Random.Range(1.5f, 3.5f); }

        // ------------------------------------------------------------------ estados
        void Enter(State s, string clip = null, float startFrame = 0f)
        {
            state = s; stateStart = Time.time - startFrame / Fps;
            launched = landed = holding = expelled = false;
            if (animator) { animator.speed = 1f; animator.CrossFadeInFixedTime(clip ?? ClipFor(s), s == State.Hit ? 0.04f : 0.1f, 0, startFrame / Fps); }
            if (health) health.damageMultiplier = 1f;
            if (face)
                face.Set(s switch
                {
                    State.Alert => SlimeFace.Expr.Surprised,
                    State.Hop => hopToPlayer ? SlimeFace.Expr.Angry : SlimeFace.Expr.Happy,
                    State.Leap => SlimeFace.Expr.Charging,
                    State.Splash => SlimeFace.Expr.Puffed,
                    State.Hit => SlimeFace.Expr.Hurt,
                    State.Dead => SlimeFace.Expr.Dead,
                    _ => idleMood
                }, s == State.Hit || s == State.Alert ? 22f : 10f);
        }
        static string ClipFor(State s) => s switch
        {
            State.Alert => "Alert", State.Hop => "HopMove", State.Leap => "AttackLeap", State.Splash => "AttackSplash",
            State.Hit => "HitReact", State.Dead => "Death", _ => "Idle"
        };

        void FixedUpdate()
        {
            if (state == State.Dead) return;
            target = NearestPlayer(out float dist);
            bool sees = target && dist < sightRange && LineOfSight(target);
            bool grounded = IsGrounded();
            float f = Frame;
            UpdateFace(f, sees);

            // Seguridad: si cae al agua o fuera del mapa vuelve a su sitio.
            if (transform.position.y < home.y - 4f) { rb.position = home + Vector3.up * 0.2f; rb.linearVelocity = Vector3.zero; Enter(State.Idle); return; }

            switch (state)
            {
                case State.Idle:
                    if (sees) { Enter(State.Alert); break; }
                    if (Time.time >= nextWander && grounded)
                    {
                        var r = Random.insideUnitCircle * wanderRadius; hopTarget = home + new Vector3(r.x, 0, r.y);
                        hopToPlayer = false; Enter(State.Hop); nextWander = Time.time + Random.Range(2.5f, 5f);
                    }
                    break;

                case State.Alert:
                    if (target) Face(target.transform.position, 2f);
                    if (f >= AlertLen) { hopToPlayer = true; Enter(State.Hop); }
                    break;

                case State.Hop:
                    if (hopToPlayer && target) { hopTarget = target.transform.position; if (!launched) Face(hopTarget, 1f); }
                    else if (!launched) Face(hopTarget, 1f);
                    if (!launched && f >= HopTakeoff)
                    {
                        launched = true;
                        Vector3 d = Flat(hopTarget - transform.position);
                        float flight = (HopLand - HopTakeoff) / Fps;
                        float step = Mathf.Min(hopDistance, d.magnitude);
                        rb.linearVelocity = (d.sqrMagnitude > 0.01f ? d.normalized : transform.forward) * (step / flight) + Vector3.up * (0.5f * -Physics.gravity.y * flight);
                    }
                    if (launched && !landed && f > HopTakeoff + 3 && grounded) { landed = true; rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0); }
                    if (f >= HopLen)
                    {
                        if (hopToPlayer && target && dist > loseRange) { hopToPlayer = false; Enter(State.Idle); break; }
                        if (hopToPlayer && target && sees && grounded && ChooseAttack(dist)) break;
                        if (!hopToPlayer && Flat(hopTarget - transform.position).magnitude < 0.5f) { Enter(State.Idle); break; }
                        if (!hopToPlayer && sees) { Enter(State.Alert); break; }
                        Enter(State.Hop);
                    }
                    break;

                case State.Leap: UpdateLeap(f, grounded); break;

                case State.Splash:
                    if (!expelled) { if (target) Face(target.transform.position, 1.5f); }
                    if (!expelled && f >= SplashExpel) { expelled = true; Expel(); }
                    if (f >= SplashLen) { nextSplash = Time.time + splashCooldown; hopToPlayer = true; Enter(State.Hop); }
                    break;

                case State.Hit:
                    if (f >= HitLen) { hopToPlayer = target; Enter(target ? State.Hop : State.Idle); }
                    break;
            }
        }

        bool ChooseAttack(float dist)
        {
            bool canLeap = Time.time >= nextLeap && dist >= leapMin && dist <= leapMax;
            bool canSplash = Time.time >= nextSplash && dist >= splashMin && dist <= splashMax;
            if (canLeap && (!canSplash || dist < 4.5f || Random.value < 0.6f)) { Enter(State.Leap); return true; }
            if (canSplash) { Enter(State.Splash); return true; }
            return false;
        }

        // ------------------------------------------------------------------ gestos de la cara
        SlimeFace.Expr idleMood = SlimeFace.Expr.Neutral;
        float nextMood;
        void UpdateFace(float f, bool sees)
        {
            if (!face) return;
            bool aware = state != State.Idle && !(state == State.Hop && !hopToPlayer);
            face.LookAt(aware && target ? target.CenterPosition : (Vector3?)null);
            switch (state)
            {
                case State.Idle:
                    // De vez en cuando cambia de humor: tranquilo, contento o curioso.
                    if (Time.time >= nextMood)
                    {
                        nextMood = Time.time + Random.Range(2.5f, 5f);
                        idleMood = Random.value < 0.55f ? SlimeFace.Expr.Neutral : SlimeFace.Expr.Happy;
                        face.Set(idleMood, 4f);
                        if (Random.value < 0.3f) face.Chatter(0.5f);
                    }
                    break;
                case State.Hop:
                    if (!hopToPlayer && f >= HopTakeoff && f < HopTakeoff + 1) face.Chatter(0.2f);   // tararea al saltar
                    break;
                case State.Leap:
                    if (!launched) face.Shake(Mathf.Clamp01(f / LeapTakeoff));                         // tiembla de rabia mientras carga
                    else if (!landed) { if (face.Current != SlimeFace.Expr.Yell) face.Set(SlimeFace.Expr.Yell, 25f); }
                    else if (f < LeapVulnerableEnd) { if (face.Current != SlimeFace.Expr.Dizzy) face.Set(SlimeFace.Expr.Dizzy, 20f); }
                    else if (face.Current != SlimeFace.Expr.Angry) face.Set(SlimeFace.Expr.Angry, 8f);
                    break;
                case State.Splash:
                    if (f < SplashExpel) face.Shake(Mathf.Clamp01(f / SplashExpel) * 0.7f);            // aguanta el aire
                    else if (f < SplashExpel + 6) { if (face.Current != SlimeFace.Expr.Spit) face.Set(SlimeFace.Expr.Spit, 30f); }
                    else if (face.Current != SlimeFace.Expr.Happy) { face.Set(SlimeFace.Expr.Happy, 8f); face.Chatter(0.35f); } // risita
                    break;
            }
        }

        // ------------------------------------------------------------------ salto de ataque
        void UpdateLeap(float f, bool grounded)
        {
            if (!launched)
            {
                // Carga aplastado: sigue al jugador con la mirada hasta el despegue.
                if (target) Face(target.transform.position, 1.6f);
                if (f >= LeapTakeoff)
                {
                    launched = true;
                    // La dirección se fija aquí y ya no cambia.
                    Vector3 to = target ? target.transform.position : transform.position + transform.forward * leapMin;
                    Vector3 d = Flat(to - transform.position); float dist = Mathf.Clamp(d.magnitude, 0.5f, leapMax);
                    Vector3 dir = d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
                    leapTarget = transform.position + dir * dist;
                    float flight = (LeapImpact - LeapTakeoff) / Fps;
                    float dy = Mathf.Clamp(to.y - transform.position.y, -2f, 1.5f);
                    rb.linearVelocity = dir * (dist / flight) + Vector3.up * (0.5f * -Physics.gravity.y * flight + dy / flight);
                    rb.MoveRotation(Quaternion.LookRotation(dir));
                    Sfx.Play(Sfx.Land, transform.position, 0.6f);
                }
                return;
            }
            if (!landed)
            {
                bool touch = f > LeapTakeoff + 3 && grounded && rb.linearVelocity.y <= 0.5f;
                if (touch) { LeapImpactNow(); return; }
                // Si todavía vuela, el clip espera en el último fotograma del vuelo hasta tocar el suelo.
                if (f >= LeapImpact - 1 && animator && !holding) { holding = true; animator.speed = 0f; }
                if (holding) stateStart = Time.time - (LeapImpact - 1) / Fps;
                return;
            }
            if (f >= LeapImpact && f < LeapVulnerableEnd) health.damageMultiplier = 2f; else health.damageMultiplier = 1f;
            if (f >= LeapLen) { nextLeap = Time.time + leapCooldown; hopToPlayer = true; Enter(State.Hop); }
        }

        void LeapImpactNow()
        {
            landed = true; holding = false;
            stateStart = Time.time - LeapImpact / Fps;
            if (animator) { animator.speed = 1f; animator.Play("AttackLeap", 0, LeapImpact / (float)LeapLen); }
            rb.linearVelocity = new Vector3(0, Mathf.Min(rb.linearVelocity.y, 0), 0);
            // Único momento del ataque que hace daño.
            foreach (var p in LizardPlayer.All)
            {
                if (!p) continue;
                Vector3 d = p.transform.position - transform.position;
                if (Flat(d).magnitude <= leapDamageRadius && Mathf.Abs(d.y) < 1.5f)
                    p.Health.TakeHit(new DamageInfo { type = DamageType.Enemy, sourcePosition = transform.position, knockbackScale = leapKnockback, foodLoss = -1f, source = gameObject });
            }
            Fx.Burst(transform.position + Vector3.up * 0.1f, new Color(0.7f, 0.95f, 0.25f), 26, 4.5f, 0.12f);
            Sfx.Play(Sfx.Land, transform.position, 1f);
        }

        // ------------------------------------------------------------------ salpicadura de gel
        void Expel()
        {
            if (!target) return;
            Vector3 from = mouth ? mouth.position : transform.position + Vector3.up * 0.55f + transform.forward * 0.45f;
            Vector3 aim = target.transform.position; Vector3 fwd = Flat(aim - transform.position).normalized; if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            // Abanico de tres gotas: una al jugador y dos a los lados para cerrarle el paso.
            for (int i = -1; i <= 1; i++)
            {
                Vector3 p = aim + side * (i * splashSpread) + fwd * (i == 0 ? 0.3f : -0.2f);
                SlimeGelBlob.Launch(from, p, ownColliders, puddleSlow, puddleLife);
            }
            Fx.Burst(from, new Color(0.75f, 1f, 0.3f), 16, 3f, 0.1f);
            Sfx.Play(Sfx.Stick, from, 0.9f);
        }

        // ------------------------------------------------------------------ golpes y muerte
        void OnDamaged(float amount, Vector3 from)
        {
            if (state == State.Dead || health.Dead) return;
            hitLeanDir = Flat(transform.position - from).normalized; hitLeanTime = Time.time;
            // En pleno salto no se interrumpe (ni durante el aterrizaje expuesto): solo se inclina.
            if (state == State.Leap && launched) return;
            Enter(State.Hit);
        }

        void OnDied(Vector3 from)
        {
            state = State.Dead;
            if (face) { face.Set(SlimeFace.Expr.Dead, 18f); face.LookAt(null); }
            if (ragdoll) ragdoll.Activate(from);
            else { if (animator) animator.CrossFadeInFixedTime("Death", 0.05f); Destroy(gameObject, 3f); }
            enabled = false;
        }

        void LateUpdate()
        {
            // Hundimiento hacia el lado del golpe: se suma a la pose de la animación.
            float t = (Time.time - hitLeanTime) / 0.3f;
            if (t >= 0f && t < 1f && midBone && state != State.Dead)
            {
                float k = Mathf.Sin(t * Mathf.PI) * (1f - t * 0.4f);
                Vector3 axis = Vector3.Cross(Vector3.up, hitLeanDir);
                midBone.rotation = Quaternion.AngleAxis(16f * k, axis) * midBone.rotation;
                if (topBone) topBone.rotation = Quaternion.AngleAxis(-8f * k, axis) * topBone.rotation;
            }
        }

        // ------------------------------------------------------------------ utilidades
        /// <summary>Receptor de los eventos de animación del FBX (los tiempos los controla el propio código).</summary>
        public void OnSlimeEvent(string id) { }

        void Face(Vector3 point, float speedMul)
        {
            Vector3 d = Flat(point - transform.position); if (d.sqrMagnitude < 0.0004f) return;
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(d), turnSpeed * speedMul * Time.fixedDeltaTime));
        }
        static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        bool IsGrounded()
        {
            var hits = Physics.RaycastAll(transform.position + Vector3.up * 0.25f, Vector3.down, 0.4f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits) if (h.rigidbody != rb && System.Array.IndexOf(ownColliders, h.collider) < 0) return true;
            return false;
        }

        LizardPlayer NearestPlayer(out float dist)
        {
            LizardPlayer best = null; dist = float.MaxValue;
            foreach (var p in LizardPlayer.All)
            {
                if (!p || !p.isActiveAndEnabled) continue;
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d < dist) { dist = d; best = p; }
            }
            return best;
        }

        bool LineOfSight(LizardPlayer p)
        {
            Vector3 a = transform.position + Vector3.up * 0.5f, b = p.CenterPosition;
            foreach (var h in Physics.RaycastAll(a, (b - a).normalized, Vector3.Distance(a, b), ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.rigidbody == rb || System.Array.IndexOf(ownColliders, h.collider) >= 0) continue;
                if (h.collider.GetComponentInParent<LizardPlayer>()) continue;
                if (h.rigidbody && !h.rigidbody.isKinematic) continue; // comida, cajas sueltas
                return false;
            }
            return true;
        }
    }
}
