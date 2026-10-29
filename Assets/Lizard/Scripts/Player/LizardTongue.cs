using UnityEngine;

namespace Lizards
{
    public enum TongueState { Idle, Extending, Attached, Retracting }
    public enum TongueTargetKind { Surface, GrapplePoint, Object }

    /// <summary>
    /// Lengua adhesiva: apunta con la cámara, se extiende, se adhiere, tira/arrastra, balancea y se retrae.
    /// Un solo objetivo activo. Las fuerzas son tipo muelle-amortiguador con límites, y la conexión se suelta
    /// si el objetivo desaparece, deja de ser válido, se estira de más o hay una pared en medio.
    /// </summary>
    public class LizardTongue : MonoBehaviour
    {
        enum Mode { None, Anchor, Pull, Partner }

        LizardPlayer p;
        Rigidbody rb;

        public TongueState State { get; private set; } = TongueState.Idle;
        public float ReelInput { get; set; }
        public float Tension01 { get; private set; }
        public float CurrentLength => length;

        // Apuntado
        public bool AimHasHit { get; private set; }
        public bool AimValid { get; private set; }
        public bool AimInRange { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public float AimDistance { get; private set; }
        public string AimLabel { get; private set; } = "";
        Collider aimCollider; Mode aimMode;

        // Conexión
        Mode mode;
        Collider targetCol; Transform targetTf; Rigidbody targetBody; Vector3 localAnchor;
        FoodItem food; LizardPlayer partner;
        Vector3 tip, missPoint; bool missing;
        float length, losBlocked;

        public Vector3 AttachPoint => targetTf ? targetTf.TransformPoint(localAnchor) : tip;
        public bool IsSwinging => State == TongueState.Attached && mode == Mode.Anchor;
        public FoodItem HeldFood => State == TongueState.Attached && food && food.State == FoodState.Grabbed && food.Holder == p ? food : null;
        public LizardPlayer HeldPartner => State == TongueState.Attached ? partner : null;
        public string AttachedLabel => mode switch
        {
            Mode.Anchor => "Enganchado (balancéate)",
            Mode.Partner => "Sujetando compañero",
            Mode.Pull => food ? "Sujetando comida" : (targetBody && targetBody.mass <= p.Stats.tongueMaxLiftMass ? "Acercando objeto" : "Arrastrando objeto"),
            _ => ""
        };

        LineRenderer line;
        Transform tipVis, marker;
        Renderer markerRenderer;
        static readonly Color pink = new Color(1f, 0.45f, 0.62f), red = new Color(0.9f, 0.1f, 0.2f);

        void Awake()
        {
            p = GetComponent<LizardPlayer>();
            rb = GetComponent<Rigidbody>();
            BuildVisuals();
        }

        void BuildVisuals()
        {
            var lgo = new GameObject("Lengua (línea)");
            lgo.transform.SetParent(transform, false);
            line = lgo.AddComponent<LineRenderer>();
            line.positionCount = 12;
            line.useWorldSpace = true;
            line.numCapVertices = 4;
            line.sharedMaterial = LizardMaterials.Particle();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;

            tipVis = MakeVisualSphere("Punta lengua", LizardMaterials.Lit(pink, 0.6f), 0.16f).transform;
            tipVis.gameObject.SetActive(false);

            marker = MakeVisualSphere("Marcador objetivo", LizardMaterials.Transparent(new Color(0.3f, 1f, 0.4f, 0.55f)), 0.3f).transform;
            markerRenderer = marker.GetComponent<Renderer>();
            marker.gameObject.SetActive(false);
        }

        GameObject MakeVisualSphere(string n, Material m, float d)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = n;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * d;
            return go;
        }

        // ---------------- Apuntado ----------------

        void Update()
        {
            if (p.IsControlled) UpdateAim();
            else { AimHasHit = AimValid = false; }
        }

        Ray AimRay()
        {
            Transform cam = p.CameraRig ? p.CameraRig.CameraTransform : (Camera.main ? Camera.main.transform : null);
            if (cam) return new Ray(cam.position, cam.forward);
            return new Ray(p.MouthPosition, transform.forward);
        }

        void UpdateAim()
        {
            var s = p.Stats;
            AimHasHit = AimValid = AimInRange = false; aimCollider = null; AimLabel = "";
            if (!s.tongueEnabled) { AimLabel = "Lengua no disponible"; return; }

            Ray ray = AimRay();
            float maxD = s.tongueRange + (p.CameraRig ? p.CameraRig.CurrentDistance : 5f) + 2f;

            bool found = RaycastIgnoringSelf(ray, maxD, 0f, out RaycastHit hit);
            bool valid = found && Classify(hit.collider, out _, out _);
            if (!valid && RaycastIgnoringSelf(ray, maxD, p.Config.tongueAimAssistRadius, out RaycastHit assist) && Classify(assist.collider, out _, out _))
            {
                hit = assist; found = true;
                hit.point = assist.collider.ClosestPoint(ray.GetPoint(assist.distance));
            }
            if (!found) { AimPoint = ray.GetPoint(maxD); return; }

            AimHasHit = true;
            AimPoint = hit.point;
            AimDistance = Vector3.Distance(p.MouthPosition, AimPoint);
            AimInRange = AimDistance <= s.tongueRange;
            bool ok = Classify(hit.collider, out string label, out Mode m);
            AimLabel = label;
            if (!ok) return;
            if (!AimInRange) { AimLabel = label + " (fuera de alcance)"; return; }
            if (Blocked(p.MouthPosition, AimPoint, hit.collider)) { AimLabel = label + " (bloqueado)"; return; }
            AimValid = true; aimCollider = hit.collider; aimMode = m;
        }

        bool RaycastIgnoringSelf(Ray ray, float maxD, float radius, out RaycastHit best)
        {
            best = default;
            var hits = radius > 0f
                ? Physics.SphereCastAll(ray, radius, maxD, ~0, QueryTriggerInteraction.Ignore)
                : Physics.RaycastAll(ray, maxD, ~0, QueryTriggerInteraction.Ignore);
            float bd = float.MaxValue; bool any = false;
            foreach (var h in hits)
            {
                if (h.collider.attachedRigidbody == rb) continue;
                if (radius > 0f && h.distance <= 0f) continue;
                if (h.distance < bd) { bd = h.distance; best = h; any = true; }
            }
            return any;
        }

        bool Classify(Collider c, out string label, out Mode m)
        {
            var s = p.Stats; var cfg = p.Config;
            m = Mode.None; label = "";
            var f = c.GetComponentInParent<FoodItem>();
            if (f)
            {
                if (!f.CanBeGrabbedBy(p)) { label = "Comida (la tiene otro)"; return false; }
                if (f.Body.mass > s.tongueMaxGrabMass) { label = "Comida demasiado pesada"; return false; }
                label = f.FitsMouth(s.mouthCapacity) ? "Comida" : "Comida grande (arrastrar)";
                m = Mode.Pull; return true;
            }
            var other = c.GetComponentInParent<LizardPlayer>();
            if (other)
            {
                if (other == p) return false;
                if (!s.tongueCanGrabPlayers) { label = "No puedes agarrar compañeros en esta forma"; return false; }
                label = other.Form == LizardForm.Small ? "Compañero renacuajo (transportar)" : "Compañero";
                m = Mode.Partner; return true;
            }
            var tt = c.GetComponentInParent<TongueTarget>();
            if (!tt) return false;
            var body = c.attachedRigidbody;
            if (body && !body.isKinematic && body.mass < cfg.tongueAnchorMass)
            {
                if (body.mass > s.tongueMaxGrabMass) { label = "Demasiado pesado en esta forma"; return false; }
                label = body.mass <= s.tongueMaxLiftMass ? "Objeto ligero" : "Objeto pesado (arrastrar)";
                m = Mode.Pull; return true;
            }
            if (!s.tongueGrapple) { label = "No puedes engancharte en esta forma"; return false; }
            label = tt.kind == TongueTargetKind.GrapplePoint ? "Punto de enganche" : body ? "Objeto muy pesado (te atrae)" : "Superficie adhesiva";
            m = Mode.Anchor; return true;
        }

        bool Blocked(Vector3 from, Vector3 to, Collider allowed)
        {
            Vector3 d = to - from; float len = d.magnitude;
            if (len < 0.01f) return false;
            var allowedBody = allowed ? allowed.attachedRigidbody : null;
            foreach (var h in Physics.RaycastAll(from, d / len, len, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.distance > len - 0.08f) continue;
                if (h.collider == allowed) continue;
                var b = h.collider.attachedRigidbody;
                if (b == rb || (b && b == allowedBody)) continue;
                if (b && !b.isKinematic && b.mass < 1f) continue; // bolitas y migajas no cortan la lengua
                return true;
            }
            return false;
        }

        // ---------------- Acciones ----------------

        public void TryFire()
        {
            var s = p.Stats;
            if (!s.tongueEnabled) { p.Message("La lengua no está disponible en esta forma"); return; }
            if (State != TongueState.Idle || p.MouthBusy) return;

            if (AimValid && aimCollider)
            {
                missing = false;
                targetCol = aimCollider; targetTf = aimCollider.transform;
                localAnchor = targetTf.InverseTransformPoint(AimPoint);
                mode = aimMode;
            }
            else
            {
                missing = true;
                ClearTarget();
                Vector3 dir = (AimPoint - p.MouthPosition).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
                missPoint = p.MouthPosition + dir * Mathf.Min(s.tongueRange, AimHasHit ? AimDistance : s.tongueRange);
            }
            tip = p.MouthPosition;
            State = TongueState.Extending;
            Sfx.Play(Sfx.TongueOut, p.MouthPosition, 0.6f);
        }

        /// <summary>Engancha directamente a un collider (autoprueba y herramientas de depuración).</summary>
        public bool DebugAttach(Collider c)
        {
            if (State != TongueState.Idle || !Classify(c, out _, out Mode m)) return false;
            targetCol = c; targetTf = c.transform; missing = false; mode = m;
            localAnchor = targetTf.InverseTransformPoint(c.bounds.center);
            tip = p.MouthPosition;
            Attach();
            return State == TongueState.Attached;
        }

        public void Release(bool withBoost)
        {
            if (State == TongueState.Idle || State == TongueState.Retracting) return;
            var s = p.Stats; var cfg = p.Config;
            if (State == TongueState.Attached)
            {
                tip = AttachPoint;
                if (withBoost && mode == Mode.Anchor && !p.Motor.Grounded)
                {
                    Vector3 v = rb.linearVelocity * cfg.tongueReleaseBoost + Vector3.up * 1.5f;
                    rb.linearVelocity = Vector3.ClampMagnitude(v, cfg.tongueMaxReleaseSpeed);
                }
                if (withBoost && mode == Mode.Partner && partner)
                {
                    float d = Vector3.Distance(partner.CenterPosition, p.MouthPosition);
                    if (d <= cfg.partnerLaunchMaxDistance * Mathf.Max(1f, s.visualScale * 0.5f))
                    {
                        Transform cam = p.CameraRig ? p.CameraRig.CameraTransform : transform;
                        Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                        partner.Body.linearVelocity = Vector3.ClampMagnitude(
                            p.Body.linearVelocity + fwd * cfg.partnerLaunchForward + Vector3.up * cfg.partnerLaunchUp, cfg.tongueMaxReleaseSpeed);
                        partner.Motor.MarkTugged(0.6f);
                        Sfx.Play(Sfx.Jump, partner.CenterPosition);
                        p.Message($"¡Has lanzado a {partner.displayName}!");
                    }
                }
            }
            if (food) food.ReleaseGrab(p);
            ClearTarget();
            State = TongueState.Retracting;
        }

        public void OnFormChanged()
        {
            var s = p.Stats;
            if (!s.tongueEnabled) { Release(false); State = TongueState.Idle; return; }
            if (State != TongueState.Attached) return;
            if (mode == Mode.Anchor && !s.tongueGrapple) Release(false);
            else if (mode == Mode.Partner && !s.tongueCanGrabPlayers) Release(false);
            else if (mode == Mode.Pull && targetBody && targetBody.mass > s.tongueMaxGrabMass) Release(false);
        }

        void ClearTarget()
        {
            food = null; partner = null; targetBody = null; targetTf = null; targetCol = null;
            mode = Mode.None; Tension01 = 0f;
        }

        bool TargetAlive() =>
            targetTf && targetCol && targetCol.enabled && targetCol.gameObject.activeInHierarchy &&
            (!food || (food.State == FoodState.Grabbed && food.Holder == p)) &&
            (!partner || (partner.isActiveAndEnabled && partner != p));

        // ---------------- Física ----------------

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime; var cfg = p.Config;
            switch (State)
            {
                case TongueState.Extending:
                    if (!missing && !targetTf) { missing = true; missPoint = tip; }
                    Vector3 goal = missing ? missPoint : AttachPoint;
                    tip = Vector3.MoveTowards(tip, goal, cfg.tongueExtendSpeed * dt);
                    if ((tip - goal).sqrMagnitude < 0.0004f)
                    {
                        if (missing) State = TongueState.Retracting;
                        else Attach();
                    }
                    break;
                case TongueState.Attached:
                    UpdateAttached(dt);
                    break;
                case TongueState.Retracting:
                    tip = Vector3.MoveTowards(tip, p.MouthPosition, cfg.tongueRetractSpeed * dt);
                    if ((tip - p.MouthPosition).sqrMagnitude < 0.01f) State = TongueState.Idle;
                    break;
            }
        }

        void Attach()
        {
            var s = p.Stats; var cfg = p.Config;
            if (!targetTf || !targetCol || !targetCol.enabled || Blocked(p.MouthPosition, AttachPoint, targetCol))
            { ClearTarget(); State = TongueState.Retracting; return; }

            if (mode == Mode.Pull)
            {
                food = targetCol.GetComponentInParent<FoodItem>();
                if (food && !food.TryGrab(p))
                {
                    p.Message("Otro jugador ya tiene esa comida");
                    food = null; ClearTarget(); State = TongueState.Retracting; return;
                }
                targetBody = targetCol.attachedRigidbody;
            }
            else if (mode == Mode.Partner)
            {
                partner = targetCol.GetComponentInParent<LizardPlayer>();
                targetBody = partner ? partner.Body : null;
            }
            else targetBody = targetCol.attachedRigidbody;

            length = Mathf.Clamp(Vector3.Distance(p.MouthPosition, AttachPoint), cfg.tongueMinLength, s.tongueRange);
            losBlocked = 0f;
            State = TongueState.Attached;
            Sfx.Play(Sfx.Stick, AttachPoint, 0.7f);
            Fx.Burst(AttachPoint, pink, 5, 1.5f, 0.06f);
        }

        void UpdateAttached(float dt)
        {
            var s = p.Stats; var cfg = p.Config;
            if (!TargetAlive()) { Release(false); return; }

            Vector3 mouth = p.MouthPosition, anchor = AttachPoint;
            if (Blocked(mouth, anchor, targetCol))
            {
                losBlocked += dt;
                if (losBlocked > cfg.tongueLosGraceTime) { Release(false); p.Message("La lengua se ha soltado"); return; }
            }
            else losBlocked = 0f;

            bool lightPull = (mode == Mode.Pull && targetBody && targetBody.mass <= s.tongueMaxLiftMass) || mode == Mode.Partner;
            if (Mathf.Abs(ReelInput) > 0.01f) length += ReelInput * cfg.tongueReelSpeed * dt;
            else if (lightPull) length -= cfg.tongueAutoReelSpeed * dt;
            float minLen = cfg.tongueMinLength * Mathf.Max(1f, s.visualScale * 0.6f);
            length = Mathf.Clamp(length, minLen, s.tongueRange);

            Vector3 delta = anchor - mouth;
            float dist = delta.magnitude;
            if (dist < 0.001f) return;
            Vector3 dir = delta / dist;
            if (dist > s.tongueRange * cfg.tongueBreakStretch) { Release(false); p.Message("¡Demasiado lejos!"); return; }

            float stretch = dist - length;
            Tension01 = Mathf.Clamp01(stretch / 1.5f);

            if (stretch > 0f)
            {
                if (mode == Mode.Anchor || !targetBody || targetBody.isKinematic)
                {
                    float relVel = Vector3.Dot(rb.linearVelocity, dir);
                    float accel = Mathf.Clamp(cfg.tongueSpring * stretch - cfg.tongueDamper * relVel, 0f, s.tongueMaxForce / rb.mass);
                    rb.AddForce(dir * accel, ForceMode.Acceleration);
                    if (stretch > cfg.tongueElasticSlack && relVel < 0f) rb.linearVelocity -= dir * relVel;
                    if (targetBody && !targetBody.isKinematic)
                        targetBody.AddForceAtPosition(-dir * accel * rb.mass, anchor, ForceMode.Force);
                }
                else
                {
                    Rigidbody tb = targetBody;
                    float relVel = Vector3.Dot(tb.linearVelocity - rb.linearVelocity, -dir);
                    float force = Mathf.Clamp((cfg.tongueSpring * stretch - cfg.tongueDamper * relVel) * tb.mass, 0f, s.tongueMaxForce);
                    if (mode == Mode.Partner) { tb.AddForce(-dir * force, ForceMode.Force); partner.Motor.MarkTugged(); }
                    else tb.AddForceAtPosition(-dir * force, anchor, ForceMode.Force);
                    if (lightPull && stretch > cfg.tongueElasticSlack * 2f && relVel < 0f) tb.linearVelocity += dir * relVel;
                    // Reacción: en el aire, el tirón también nos mueve a nosotros.
                    if (!p.Motor.Grounded) rb.AddForce(dir * force * 0.5f, ForceMode.Force);
                    tb.linearVelocity = Vector3.ClampMagnitude(tb.linearVelocity, cfg.tongueMaxBodySpeed);
                }
            }
            else Tension01 = 0f;

            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, cfg.tongueMaxBodySpeed);

            // Tragar automáticamente la comida que llega a la boca.
            if (food && dist <= cfg.swallowDistance * Mathf.Max(1f, s.visualScale) && food.FitsMouth(s.mouthCapacity)
                && p.Stomach.Food < cfg.maxFood - 0.001f)
                p.TryEat(food);
        }

        // ---------------- Visual ----------------

        void LateUpdate()
        {
            var s = p.Stats;
            bool showMarker = p.IsControlled && s.tongueEnabled && State == TongueState.Idle && AimHasHit;
            marker.gameObject.SetActive(showMarker);
            if (showMarker)
            {
                marker.position = AimPoint;
                float dCam = p.CameraRig ? Vector3.Distance(p.CameraRig.CameraTransform.position, AimPoint) : 6f;
                marker.localScale = Vector3.one * Mathf.Clamp(dCam * 0.03f, 0.15f, 0.8f) / Mathf.Max(0.01f, transform.lossyScale.x);
                Color c = AimValid ? new Color(0.3f, 1f, 0.4f, 0.6f) : !AimInRange && AimLabel.Length > 0 ? new Color(1f, 0.6f, 0.1f, 0.5f) : new Color(1f, 1f, 1f, 0.25f);
                markerRenderer.material.SetColor("_BaseColor", c);
            }

            if (State == TongueState.Idle) { line.enabled = false; tipVis.gameObject.SetActive(false); return; }
            line.enabled = true; tipVis.gameObject.SetActive(true);
            Vector3 a = p.MouthPosition;
            Vector3 b = State == TongueState.Attached ? AttachPoint : tip;
            float dist = Vector3.Distance(a, b);
            float sag = State == TongueState.Attached ? Mathf.Max(0f, length - dist) * 0.5f : 0f;
            Vector3 mid = (a + b) * 0.5f + Vector3.down * sag;
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = i / (line.positionCount - 1f);
                line.SetPosition(i, (1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b);
            }
            float scale = Mathf.Max(0.4f, s.visualScale);
            float w = Mathf.Lerp(0.09f, 0.04f, Tension01) * scale;
            line.startWidth = w; line.endWidth = w * 0.8f;
            Color col = Color.Lerp(pink, red, Tension01);
            line.startColor = col; line.endColor = col;
            tipVis.position = b;
            tipVis.localScale = Vector3.one * 0.16f * scale / Mathf.Max(0.01f, transform.lossyScale.x);
        }
    }
}
