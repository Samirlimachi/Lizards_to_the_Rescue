using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Punto central del personaje: une los sistemas (movimiento, formas, comida, daño, lengua, ácido, visual)
    /// y traduce la entrada en acciones. Todas las mutaciones de estado pasan por métodos públicos de esta clase
    /// o de sus sistemas, para poder darles autoridad de red más adelante.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-50)]
    public class LizardPlayer : MonoBehaviour
    {
        public LizardConfig config;
        public string displayName = "Lagarto";
        [Header("Estado inicial")]
        public LizardForm startForm = LizardForm.Normal;
        public float startFood = 0f;
        [Header("Compañero de prueba (sin control)")]
        public bool dummyCallsHelp = false;

        public static readonly List<LizardPlayer> All = new();

        public Rigidbody Body { get; private set; }
        public LizardMotor Motor { get; private set; }
        public LizardForms Forms { get; private set; }
        public LizardStomach Stomach { get; private set; }
        public LizardHealth Health { get; private set; }
        public LizardTongue Tongue { get; private set; }
        public LizardAcid Acid { get; private set; }
        public LizardVisual Visual { get; private set; }
        public LizardInput Input { get; private set; }
        public LizardCameraRig CameraRig { get; set; }

        public LizardConfig Config => config ? config : LizardConfig.Fallback;
        public LizardForm Form => Forms.Current;
        public FormStats Stats => Config.Get(Forms.Current);
        public bool IsControlled => Input && Input.controlled;

        public Vector3 MouthPosition => Visual && Visual.mouthPoint
            ? Visual.mouthPoint.position
            : transform.position + Vector3.up * (Stats.colliderHeight * 0.65f) + transform.forward * Stats.colliderRadius;
        public Vector3 CenterPosition => transform.position + Vector3.up * (Stats.useSphere ? Stats.colliderRadius : Stats.colliderHeight * 0.5f);

        float mouthBusyUntil;
        public bool MouthBusy => Time.time < mouthBusyUntil;
        public void SetMouthBusy(float seconds) => mouthBusyUntil = Mathf.Max(mouthBusyUntil, Time.time + seconds);

        public float HelpRequestedUntil { get; private set; }
        public bool IsRequestingHelp => Time.time < HelpRequestedUntil;
        float nextHelpTime, nextDummyHelp;

        Vector3 respawnPoint;
        float rescueCooldownUntil;

        public readonly List<(string text, float time)> Messages = new();

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Motor = GetComponent<LizardMotor>();
            Forms = GetComponent<LizardForms>();
            Stomach = GetComponent<LizardStomach>();
            Health = GetComponent<LizardHealth>();
            Tongue = GetComponent<LizardTongue>();
            Acid = GetComponent<LizardAcid>();
            Visual = GetComponentInChildren<LizardVisual>();
            Input = GetComponent<LizardInput>();
            respawnPoint = transform.position;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start()
        {
            Forms.ApplyImmediate(startForm);
            Stomach.DebugSetFood(startFood);
            Acid.Refill();
        }

        void Update()
        {
            if (transform.position.y < Config.killY) Respawn();

            if (!IsControlled)
            {
                Motor.MoveInput = Vector3.zero;
                if (dummyCallsHelp && Form == LizardForm.Small && Time.time > nextDummyHelp)
                {
                    nextDummyHelp = Time.time + 6f;
                    CallHelp();
                }
                return;
            }

            // Movimiento relativo a la cámara.
            Transform cam = CameraRig ? CameraRig.CameraTransform : (Camera.main ? Camera.main.transform : null);
            Vector2 mv = Input.MoveValue;
            Vector3 fwd = cam ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = cam ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized : Vector3.right;
            Motor.MoveInput = Vector3.ClampMagnitude(fwd * mv.y + right * mv.x, 1f);

            if (Input.Pressed(Input.Jump)) Motor.RequestJump();

            // Lengua: mantener para seguir enganchado (o pulsar de nuevo si está desactivado).
            if (Input.Pressed(Input.Tongue))
            {
                if (Tongue.State == TongueState.Idle) Tongue.TryFire();
                else if (!Config.tongueHoldToStay) Tongue.Release(true);
            }
            if (Config.tongueHoldToStay && Input.Released(Input.Tongue) && Tongue.State != TongueState.Idle && Tongue.State != TongueState.Retracting)
                Tongue.Release(true);
            Tongue.ReelInput = Input.ReelValue;

            if (Input.Pressed(Input.Acid)) Acid.TryFire();
            if (Input.Pressed(Input.Interact)) EatOrInteract();
            if (Input.Pressed(Input.Eject)) EjectOrFeed();
            if (Input.Pressed(Input.Help)) CallHelp();
        }

        // ---------------- Comida ----------------

        public FoodItem FindNearestFood(out bool fits)
        {
            fits = false;
            FoodItem best = null; float bestD = float.MaxValue; bool bestFits = false;
            foreach (var c in Physics.OverlapSphere(MouthPosition, Config.eatRadius * Mathf.Max(1f, Stats.visualScale), ~0, QueryTriggerInteraction.Collide))
            {
                var f = c.GetComponentInParent<FoodItem>();
                if (!f || !f.CanBeEatenBy(this)) continue;
                bool fit = f.FitsMouth(Stats.mouthCapacity);
                float d = (f.transform.position - MouthPosition).sqrMagnitude - (fit ? 1000f : 0f);
                if (d < bestD) { bestD = d; best = f; bestFits = fit; }
            }
            fits = bestFits;
            return best;
        }

        public void EatOrInteract()
        {
            var held = Tongue.HeldFood;
            if (held)
            {
                float d = Vector3.Distance(held.transform.position, MouthPosition);
                if (d <= Config.swallowDistance * 2f * Mathf.Max(1f, Stats.visualScale)) { TryEat(held); return; }
                Message(held.FitsMouth(Stats.mouthCapacity) ? "Acerca más la comida" : "Demasiado grande: arrástrala o llévala a la meta");
                return;
            }
            var food = FindNearestFood(out bool fits);
            if (!food) { Message("No hay comida cerca"); return; }
            if (!fits) { Message("Demasiado grande para tragar: arrástrala con la lengua"); return; }
            TryEat(food);
        }

        public bool TryEat(FoodItem f)
        {
            if (!f) return false;
            if (Stomach.Food >= Config.maxFood - 0.001f) { Message("¡Estás lleno!"); return false; }
            if (!f.FitsMouth(Stats.mouthCapacity)) { Message("Demasiado grande para tragar"); return false; }
            if (Tongue.HeldFood == f) Tongue.Release(false);
            if (!f.TryConsume(this, out float value)) return false;
            Stomach.Add(value, "comida");
            Visual?.PlayGulp();
            Sfx.Play(Sfx.Eat, MouthPosition);
            EvaluateForm();
            return true;
        }

        public LizardPlayer FindSmallPartner()
        {
            LizardPlayer best = null; float bestD = Config.feedRadius * Mathf.Max(1f, Stats.visualScale * 0.6f);
            foreach (var p in All)
            {
                if (p == this || p.Form != LizardForm.Small) continue;
                float d = Vector3.Distance(p.transform.position, transform.position);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        public void EjectOrFeed()
        {
            if (!Stats.canEject) { Message("En este estado no puedes expulsar comida"); return; }
            if (MouthBusy) return;
            var partner = FindSmallPartner();
            var held = Tongue.HeldFood;

            if (partner && held && held.FitsMouth(partner.Stats.mouthCapacity))
            {
                Tongue.Release(false);
                if (partner.TryEat(held)) Message($"Has alimentado a {partner.displayName}");
                return;
            }
            if (partner && Stomach.Food > 0.001f)
            {
                float amount = Stomach.Remove(Config.feedAmount, "alimentar");
                partner.ReceiveFood(amount, this);
                Fx.Burst(Vector3.Lerp(MouthPosition, partner.CenterPosition, 0.5f), new Color(0.8f, 0.95f, 0.4f), 10, 2f, 0.12f);
                Visual?.PlayEject();
                SetMouthBusy(0.3f);
                Message($"Has alimentado a {partner.displayName}");
                EvaluateForm();
                return;
            }
            if (Stomach.Food > 0.001f)
            {
                Vector3 dir = (transform.forward + Vector3.up * 0.6f).normalized;
                Stomach.EjectPellets(Config.voluntaryEjectAmount, dir, 4f, 0.2f);
                Visual?.PlayEject();
                Sfx.Play(Sfx.Eject, MouthPosition);
                SetMouthBusy(0.3f);
                EvaluateForm();
                return;
            }
            Message("No tienes comida que expulsar");
        }

        public void ReceiveFood(float amount, LizardPlayer from)
        {
            if (amount <= 0f) return;
            Stomach.Add(amount, from ? "alimentado por " + from.displayName : "alimentado");
            Visual?.PlayGulp();
            Sfx.Play(Sfx.Eat, MouthPosition);
            Message(from ? $"{from.displayName} te ha alimentado" : "Te han alimentado");
            EvaluateForm();
        }

        /// <summary>Recalcula la forma deseada según la reserva de comida.</summary>
        public void EvaluateForm()
        {
            var cfg = Config; float food = Stomach.Food;
            LizardForm desired = Form;
            switch (Form)
            {
                case LizardForm.Small: if (food >= cfg.smallRecoverFood - 0.001f) desired = LizardForm.Normal; break;
                case LizardForm.Normal: if (food >= cfg.giantThreshold - 0.001f) desired = LizardForm.Giant; break;
                case LizardForm.Giant: if (food < cfg.giantExitThreshold) desired = LizardForm.Normal; break;
            }
            Forms.RequestForm(desired);
        }

        // ---------------- Ayuda, rescate, reaparición ----------------

        public void CallHelp()
        {
            if (Time.time < nextHelpTime) return;
            nextHelpTime = Time.time + Config.helpCooldown;
            HelpRequestedUntil = Time.time + 4f;
            Visual?.PlayHelp();
            Sfx.Play(Sfx.Help, CenterPosition);
            if (IsControlled) Message(Form == LizardForm.Small ? "¡Pides ayuda! Busca comida o un punto de rescate" : "¡Pides ayuda!");
        }

        public bool Rescue(string source)
        {
            if (Form != LizardForm.Small || Time.time < rescueCooldownUntil) return false;
            rescueCooldownUntil = Time.time + 1f;
            Stomach.Add(Config.smallRecoverFood, source);
            EvaluateForm();
            Fx.Burst(CenterPosition, new Color(0.4f, 1f, 1f), 20, 3f, 0.15f);
            Message("¡Rescatado!");
            return true;
        }

        public void SetCheckpoint(Vector3 p) => respawnPoint = p;

        public void Respawn()
        {
            Tongue.Release(false);
            Body.position = respawnPoint + Vector3.up * 0.5f;
            transform.position = Body.position;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Message("Has vuelto al último punto de control");
        }

        public void Message(string text)
        {
            Messages.Add((text, Time.time));
            if (Messages.Count > 5) Messages.RemoveAt(0);
        }

        // ---------------- Impactos ----------------

        void OnCollisionEnter(Collision c)
        {
            float speed = Motor.LastVelocity.magnitude;
            Vector3 dir = Motor.LastVelocity.sqrMagnitude > 0.01f ? Motor.LastVelocity.normalized : transform.forward;

            if (Form == LizardForm.Giant)
            {
                var breakable = c.collider.GetComponentInParent<Breakable>();
                if (breakable && breakable.TryBreak(speed, true, dir * speed * Body.mass))
                {
                    Body.linearVelocity = Motor.LastVelocity * 0.6f;
                    Visual?.PlayCrash(speed);
                    CameraRig?.Shake(0.35f);
                    return;
                }
                var enemy = c.collider.GetComponentInParent<EnemyHealth>();
                if (enemy && speed >= Config.giantEnemyCrushSpeed) enemy.Damage(99f, transform.position);

                var other = c.rigidbody;
                if (other && !other.isKinematic && speed > 2f)
                {
                    // Impulso cómico extra, limitado para no crear fuerzas descontroladas.
                    float imp = Mathf.Min(Body.mass * speed * Config.giantImpactImpulseScale, Config.giantMaxImpactImpulse);
                    Vector3 n = Vector3.ProjectOnPlane(-c.GetContact(0).normal, Vector3.up).normalized;
                    other.AddForce(n * imp + Vector3.up * imp * 0.2f, ForceMode.Impulse);
                }
                if (speed > 4f) { Visual?.PlayCrash(speed); CameraRig?.Shake(Mathf.Clamp01(speed / 15f) * 0.3f); Sfx.Play(Sfx.Land, transform.position, 0.6f); }
                return;
            }

            if (Config.strongImpactsEnabled && c.relativeVelocity.magnitude >= Config.strongImpactSpeed && !c.collider.GetComponentInParent<FoodItem>())
                Health.TakeHit(new DamageInfo { type = DamageType.Impact, sourcePosition = c.GetContact(0).point, foodLoss = -1 });
        }
    }
}
