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
        float length, losBlocked, overstretch, nextCreak;

        /// <summary>Tensión acumulada para la catapulta (0-1): sube al tensar la lengua con un compañero, baja al aflojar.</summary>
        public float CatapultCharge { get; private set; }

        // ---- Red ----
        /// <summary>Copia remota: la lengua solo refleja lo que llega por red (no simula ni aplica fuerzas).</summary>
        public bool NetMirror { get; set; }
        /// <summary>Compañero sujetado (también en la copia remota, para avisos y fuerzas en su dueño).</summary>
        public LizardPlayer PartnerTarget => partner;
        /// <summary>Lo rellena LizardNetSync: lanza al compañero remoto en su propio cliente.</summary>
        public System.Action<LizardPlayer, Vector3> NetLaunchPartner;
        /// <summary>Lo rellena LizardNetSync: pide al dueño de esta lengua que suelte (el sujetado quiere liberarse).</summary>
        public System.Action NetRequestRelease;

        public void SetFromNetwork(TongueState st, Vector3 tipPos, float len, float tension, float charge, LizardPlayer heldPartner)
        {
            State = st; tip = tipPos; length = len; Tension01 = tension; CatapultCharge = charge;
            partner = st == TongueState.Attached ? heldPartner : null;
            mode = partner ? Mode.Partner : st == TongueState.Attached ? Mode.Anchor : Mode.None;
            targetTf = null; targetCol = null; targetBody = null; food = null; carrying = false;
        }
        public bool CatapultReady => State == TongueState.Attached && mode == Mode.Partner && partner && CatapultCharge >= p.Config.catapultMinCharge;

        public Vector3 AttachPoint => targetTf ? targetTf.TransformPoint(localAnchor) : tip;
        public bool IsSwinging => State == TongueState.Attached && mode == Mode.Anchor;
        /// <summary>Punta actual de la lengua (o el punto de enganche).</summary>
        public Vector3 TipPosition => State == TongueState.Attached ? AttachPoint : tip;
        public FoodItem HeldFood => State == TongueState.Attached && food && food.State == FoodState.Grabbed && food.Holder == p ? food : null;
        public LizardPlayer HeldPartner => State == TongueState.Attached ? partner : null;
        /// <summary>Comida grande que va atrapada en la boca (no cabe para tragarla).</summary>
        public FoodItem CarriedInMouth => carrying && State == TongueState.Attached ? food : null;
        bool carrying;
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
        static readonly Color pink = new Color(1f, 0.32f, 0.78f), red = new Color(0.86f, 0.2f, 0.38f), outlineCol = new Color(0.62f, 0.14f, 0.3f);
        LineRenderer outline;
        TongueTube tube;
        Material tubeMat;
        Vector3[] tubePts = new Vector3[16];
        [Tooltip("Grosor de la lengua (se escala con la forma).")]
        public float tongueWidth = 0.17f;

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
            // Material propio para la cinta: se dibuja siempre después (encima) del borde.
            var mainMat = new Material(LizardMaterials.Particle()) { renderQueue = 3002 };
            line.sharedMaterial = mainMat;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            line.sortingOrder = 1;
            line.textureMode = LineTextureMode.Stretch;

            // Contorno: misma línea, más ancha y oscura, dibujada detrás (estilo cartoon).
            var ogo = new GameObject("Lengua (contorno)");
            ogo.transform.SetParent(transform, false);
            outline = ogo.AddComponent<LineRenderer>();
            outline.positionCount = line.positionCount;
            outline.useWorldSpace = true;
            outline.numCapVertices = 6;
            outline.sharedMaterial = new Material(LizardMaterials.Particle()) { renderQueue = 3001 };
            outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outline.startColor = outline.endColor = outlineCol;
            outline.sortingOrder = 0;
            outline.enabled = false;
            line.numCapVertices = 6;

            // Sin contorno: en un tubo tan fino el contorno (de grosor fijo en pantalla) lo tapaba y se veía negro.
            // El material vive en Resources para que el shader se incluya también en las builds.
            var tongueAsset = Resources.Load<Material>("Lizards_Lengua");
            var tongueShader = Shader.Find("Lizards/Tongue");
            tubeMat = tongueAsset ? new Material(tongueAsset) : tongueShader ? new Material(tongueShader) : LizardMaterials.Lit(pink, 0.4f);
            tubeMat.SetColor("_BaseColor", pink);
            tube = TongueTube.Create(transform, tubeMat, tubePts.Length);
            tube.Show(false);
            tipVis = MakeVisualSphere("Punta lengua", tubeMat, 0.16f).transform;
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
            if (p.IsControlled && !NetMirror) UpdateAim();
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
            AimHasHit = AimValid = AimInRange = false; aimCollider = null; AimLabel = ""; highlightTarget = null;
            if (!s.tongueEnabled) { AimLabel = "Lengua no disponible"; return; }

            Ray ray = AimRay();
            float maxD = s.tongueRange + (p.CameraRig ? p.CameraRig.CurrentDistance : 5f) + 2f;

            bool found = RaycastIgnoringSelf(ray, maxD, 0f, out RaycastHit hit);
            bool valid = found && Classify(hit.collider, out _, out _);
            if (!valid && RaycastIgnoringSelf(ray, maxD, p.Config.tongueAimAssistRadius, out RaycastHit assist) && Classify(assist.collider, out _, out _))
            {
                hit = assist; found = true;
                // SphereCast already supplies the surface point on concave meshes.
                // ClosestPoint supports only primitive colliders and convex meshes.
                if (!(assist.collider is MeshCollider mesh) || mesh.convex)
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
            if (aimMode != Mode.Anchor) highlightTarget = hit.collider; // objetos agarrables: contorno blanco
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
            // Se gira hacia donde apunta para que la boca (y la lengua) salgan en esa dirección.
            p.Motor.FaceTowards(missing ? missPoint : AimPoint, 0.5f);
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
            if (NetMirror) { NetRequestRelease?.Invoke(); return; }
            var s = p.Stats; var cfg = p.Config;
            if (State == TongueState.Attached)
            {
                tip = AttachPoint;
                if (withBoost && mode == Mode.Anchor && !p.Motor.Grounded)
                {
                    Vector3 v = rb.linearVelocity * cfg.tongueReleaseBoost + Vector3.up * 1.5f;
                    rb.linearVelocity = Vector3.ClampMagnitude(v, cfg.tongueMaxReleaseSpeed);
                }
                // Soltar a un compañero no lo lanza: conserva su propio movimiento. El lanzamiento es TryCatapult (Q).
            }
            if (carrying) StopCarry(withBoost);
            if (food) food.ReleaseGrab(p);
            ClearTarget();
            State = TongueState.Retracting;
        }

        /// <summary>
        /// Catapulta elástica: con un compañero sujeto y la lengua tensa, lo suelta con un impulso proporcional
        /// a la tensión acumulada, hacia donde mira la cámara. Sin carga, no sale un lanzamiento fuerte.
        /// </summary>
        public bool TryCatapult()
        {
            var cfg = p.Config;
            if (State != TongueState.Attached || mode != Mode.Partner || !partner) return false;
            if (CatapultCharge < cfg.catapultMinCharge) { p.Message("Aléjate para tensar la lengua antes de lanzar"); return false; }
            var target = partner;
            Transform cam = p.CameraRig ? p.CameraRig.CameraTransform : transform;
            Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = transform.forward;
            Vector3 toMe = (p.MouthPosition - target.CenterPosition); toMe.y = 0f;
            // Mezcla la dirección de la cámara con la del tirón, para que el elástico "dispare" de forma creíble.
            Vector3 dir = (Vector3.Lerp(toMe.normalized, fwd, 0.6f) + Vector3.up * cfg.catapultUpRatio).normalized;
            float speed = cfg.catapultMaxSpeed * Mathf.InverseLerp(cfg.catapultMinCharge * 0.5f, 1f, CatapultCharge);
            Vector3 v = target.Body.linearVelocity * 0.3f + dir * speed;
            Release(false);
            v = Vector3.ClampMagnitude(v, Mathf.Min(cfg.catapultMaxSpeed, cfg.tongueMaxReleaseSpeed));
            // Compañero de otro cliente: su dueño aplica la velocidad (una sola autoridad por cuerpo).
            if (target.Body.isKinematic && NetLaunchPartner != null) NetLaunchPartner(target, v);
            else target.Body.linearVelocity = v;
            target.Motor.MarkTugged(0.8f);
            Sfx.Play(Sfx.Jump, target.CenterPosition);
            Fx.Burst(target.CenterPosition, pink, 8, 3f, 0.08f);
            p.Message($"¡Catapulta! Has lanzado a {target.displayName}");
            return true;
        }

        Vector3 MouthHoldPoint() => p.MouthPosition + transform.forward * (food.size * 0.75f) + Vector3.down * food.size * 0.25f;

        void StartCarry()
        {
            carrying = true;
            food.Body.isKinematic = true;
            food.Body.interpolation = RigidbodyInterpolation.Interpolate;
            foreach (var fc in food.GetComponentsInChildren<Collider>())
                foreach (var pc in GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(fc, pc, true);
            Sfx.Play(Sfx.Eat, p.MouthPosition, 0.5f);
        }

        /// <summary>Suelta la comida de la boca; con impulso la lanza un poco hacia delante.</summary>
        void StopCarry(bool toss)
        {
            carrying = false;
            if (!food) return;
            food.Body.isKinematic = false;
            food.Body.linearVelocity = rb.linearVelocity + (toss ? transform.forward * 3f + Vector3.up * 2f : Vector3.zero);
            var foodCols = food.GetComponentsInChildren<Collider>();
            var myCols = GetComponentsInChildren<Collider>();
            // Reactiva las colisiones un instante después para que no salga disparada contra el propio jugador.
            StartCoroutine(RestoreCollisions(foodCols, myCols));
        }

        System.Collections.IEnumerator RestoreCollisions(Collider[] a, Collider[] b)
        {
            yield return new WaitForSeconds(0.4f);
            foreach (var x in a) if (x) foreach (var y in b) if (y) Physics.IgnoreCollision(x, y, false);
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
            carrying = false;
            food = null; partner = null; targetBody = null; targetTf = null; targetCol = null;
            mode = Mode.None; Tension01 = 0f; CatapultCharge = 0f; overstretch = 0f;
        }

        bool TargetAlive() =>
            targetTf && targetCol && targetCol.enabled && targetCol.gameObject.activeInHierarchy &&
            (!food || (food.State == FoodState.Grabbed && food.Holder == p)) &&
            (!partner || (partner.isActiveAndEnabled && partner != p));

        // ---------------- Física ----------------

        void FixedUpdate()
        {
            if (NetMirror) return;
            float dt = Time.fixedDeltaTime; var cfg = p.Config;
            switch (State)
            {
                case TongueState.Extending:
                    if (!missing && !targetTf) { missing = true; missPoint = tip; }
                    Vector3 goal = missing ? missPoint : AttachPoint;
                    p.Motor.FaceTowards(goal, 0.25f);
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
            // Salpicadura al pegarse: gotas rosas y un anillo de bolitas blancas.
            Fx.Burst(AttachPoint, pink, 14, 3.8f, 0.09f);
            Fx.Burst(AttachPoint, Color.white, 8, 2.6f, 0.07f);
            DustPuffs.Burst(AttachPoint, 0.16f, 7, 2.2f);
        }

        void UpdateAttached(float dt)
        {
            var s = p.Stats; var cfg = p.Config;
            if (!TargetAlive()) { Release(false); return; }

            Vector3 mouth = p.MouthPosition, anchor = AttachPoint;

            // Comida demasiado grande para tragar: al llegar a la boca se queda atrapada en ella.
            if (mode == Mode.Pull && food && !food.FitsMouth(s.mouthCapacity))
            {
                if (!carrying && length <= cfg.tongueMinLength * Mathf.Max(1f, s.visualScale * 0.6f) + 0.05f
                    && Vector3.Distance(mouth, food.transform.position) <= food.size + cfg.tongueMinLength + 0.2f)
                    StartCarry();
                if (carrying)
                {
                    food.Body.MovePosition(MouthHoldPoint());
                    food.Body.MoveRotation(transform.rotation);
                    Tension01 = 0f;
                    return;
                }
            }

            if (Blocked(mouth, anchor, targetCol))
            {
                losBlocked += dt;
                if (losBlocked > cfg.tongueLosGraceTime) { Release(false); p.Message("La lengua se ha soltado"); return; }
            }
            else losBlocked = 0f;

            bool lightPull = (mode == Mode.Pull && targetBody && targetBody.mass <= s.tongueMaxLiftMass) || mode == Mode.Partner;
            if (Mathf.Abs(ReelInput) > 0.01f) length += ReelInput * cfg.tongueReelSpeed * dt;
            else if (lightPull && mode != Mode.Partner) length -= cfg.tongueAutoReelSpeed * dt;
            float minLen = cfg.tongueMinLength * Mathf.Max(1f, s.visualScale * 0.6f);
            length = Mathf.Clamp(length, minLen, s.tongueRange);

            Vector3 delta = anchor - mouth;
            float dist = delta.magnitude;
            if (dist < 0.001f) return;
            Vector3 dir = delta / dist;
            // Estirada de más: aguanta un tiempo configurable y luego se suelta (nunca hace daño).
            float limit = s.tongueRange * cfg.tongueBreakStretch;
            if (dist > limit * 1.5f) { Release(false); p.Message("¡Demasiado lejos!"); return; }
            if (dist > limit)
            {
                overstretch += dt;
                if (overstretch > cfg.tongueOverstretchTime) { Release(false); p.Message("¡La lengua no da más y se suelta!"); return; }
            }
            else overstretch = Mathf.Max(0f, overstretch - dt * 2f);

            float stretch = dist - length;
            Tension01 = Mathf.Clamp01(stretch / 1.5f);

            // Catapulta: la carga sigue a la tensión con un compañero sujeto (sube despacio, baja más rápido al aflojar).
            if (mode == Mode.Partner)
            {
                float rate = Tension01 > CatapultCharge ? cfg.catapultChargeRate : cfg.catapultChargeRate * 2f;
                CatapultCharge = Mathf.MoveTowards(CatapultCharge, Tension01, rate * dt);
            }
            else CatapultCharge = 0f;

            // Crujido elástico cuando tira fuerte.
            if (Tension01 > cfg.tongueTensionFeedback && Time.time >= nextCreak)
            {
                nextCreak = Time.time + Mathf.Lerp(0.6f, 0.2f, Tension01);
                Sfx.Play(Sfx.Stick, Vector3.Lerp(mouth, anchor, 0.5f), 0.15f + 0.35f * Tension01);
            }

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

        // ---------------- Resaltado ----------------
        Collider highlightTarget;
        Renderer[] highlighted = new Renderer[0];
        static Material highlightMat;

        void UpdateHighlight()
        {
            Collider want = p.IsControlled && State == TongueState.Idle ? highlightTarget : null;
            Renderer[] rs = want ? (want.attachedRigidbody ? want.attachedRigidbody.GetComponentsInChildren<Renderer>() : want.GetComponentsInChildren<Renderer>()) : new Renderer[0];
            if (rs.Length == highlighted.Length && (rs.Length == 0 || rs[0] == highlighted[0])) return;
            SetHighlight(highlighted, false);
            highlighted = rs;
            SetHighlight(highlighted, true);
        }

        static void SetHighlight(Renderer[] rs, bool on)
        {
            if (!highlightMat) { var sh = Shader.Find("Lizards/Highlight"); if (!sh) return; highlightMat = new Material(sh); }
            foreach (var r in rs)
            {
                if (!r || r is ParticleSystemRenderer || r is LineRenderer) continue;
                var list = new System.Collections.Generic.List<Material>(r.sharedMaterials);
                list.Remove(highlightMat);
                if (on) list.Add(highlightMat);
                r.sharedMaterials = list.ToArray();
            }
        }

        void OnDisable() => SetHighlight(highlighted, false);

        void LateUpdate()
        {
            UpdateHighlight();
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

            line.enabled = false; outline.enabled = false; // sustituidas por el tubo 3D
            if (State == TongueState.Idle || CarriedInMouth) { tube.Show(false); tipVis.gameObject.SetActive(false); return; }
            tube.Show(true); tipVis.gameObject.SetActive(true);
            Vector3 a = p.MouthPosition;
            Vector3 b = State == TongueState.Attached ? AttachPoint : tip;
            float dist = Vector3.Distance(a, b);
            // Floja: cuelga en curva suave. Tensa: recta. Al lanzarse: ligera ondulación elástica.
            float sag = State == TongueState.Attached ? Mathf.Max(0f, length - dist) * 0.5f : 0f;
            Vector3 mid = (a + b) * 0.5f + Vector3.down * sag;
            Vector3 dir = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : transform.forward;
            Vector3 wiggleAxis = Vector3.Cross(dir, Vector3.up).sqrMagnitude > 1e-4f ? Vector3.Cross(dir, Vector3.up).normalized : transform.right;
            float wiggle = State == TongueState.Extending || State == TongueState.Retracting ? 0.06f * Mathf.Min(1f, dist) : 0f;
            for (int i = 0; i < tubePts.Length; i++)
            {
                float t = i / (tubePts.Length - 1f);
                Vector3 pt = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * mid + t * t * b;
                pt += wiggleAxis * Mathf.Sin(t * Mathf.PI * 2f + Time.time * 30f) * wiggle * Mathf.Sin(t * Mathf.PI);
                tubePts[i] = pt;
            }
            float scale = Mathf.Max(0.4f, s.visualScale);
            // Se estira: más fina con la tensión.
            float w = tongueWidth * scale * (State == TongueState.Attached ? Mathf.Lerp(1f, 0.65f, Tension01) : 1f);
            tube.SetPath(tubePts, w * 0.5f, w * 0.42f);
            Color tc = Color.Lerp(pink, red, Tension01 * 0.6f);
            if (overstretch > 0f) tc = Color.Lerp(tc, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 40f)); // aviso: está a punto de soltarse
            tubeMat.SetColor("_BaseColor", tc);
            tipVis.position = b;
            float tipPulse = State == TongueState.Attached ? 1f + Mathf.Sin(Time.time * 9f) * 0.04f : 1f;
            tipVis.localScale = Vector3.one * w * 2.1f * tipPulse / Mathf.Max(0.01f, transform.lossyScale.x);
        }
    }
}
