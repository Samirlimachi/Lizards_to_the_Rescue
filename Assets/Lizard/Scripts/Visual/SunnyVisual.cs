using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Visual del modelo Sunny (malla con skinning):
    /// - Tapa (de la boca hacia arriba) que sube al hablar y rebota al correr rápido.
    /// - Labio inferior que baja al hablar, comer o escupir.
    /// - Brazos "fideo" y cola con física de muelle (solo visual).
    /// - Compresión/estiramiento, bamboleo, destello de daño y cambio de tamaño por forma.
    /// </summary>
    public class SunnyVisual : LizardVisualBase
    {
        [Header("Huesos (los rellena SunnyRig)")]
        public Transform scalePivot, headBone, jawBone, bubble, ping;
        [Tooltip("Esfera limpia que sustituye al cuerpo en la forma gigante.")]
        public Transform giantBall;
        public SkinnedMeshRenderer skin;
        public Transform eyeLeft, eyeRight, bellyBone, legLeft, legRight;
        public Transform[] lids = new Transform[0];
        public float lidClosedY;
        public Vector3 lidClosedScale = Vector3.one * 0.2f;

        [Header("Piernas y párpados")]
        public float stepDegrees = 32f;
        public Vector2 blinkInterval = new Vector2(2f, 5f);
        [Header("Brazos y cola")]
        [Tooltip("Grados que cuelgan los brazos respecto a la pose en cruz del modelo.")]
        public float armHangDegrees = 50f;
        [Tooltip("En reposo, cuánto se adelantan los brazos (grados) para separarlos del cuerpo.")]
        public float armIdleForward = 12f;
        public NoodleChain armLeft = new NoodleChain(), armRight = new NoodleChain(),
            tail = new NoodleChain { frequency = 3.2f, dampingRatio = 0.6f, gravity = 0.4f, wobble = 0.4f, maxAngle = 45f };

        [Header("Brazos al correr")]
        [Tooltip("Ángulo de los brazos corriendo a toda velocidad (0 = horizontal, 72 = colgando).")]
        public float runArmDegrees = -4f;
        [Tooltip("Barrido de los brazos hacia atrás al correr (grados).")]
        public float runArmSweep = 72f;
        [Tooltip("Rigidez del brazo al correr (Hz): más alta = el brazo no se queda atrás, solo la mano.")]
        public float runArmFrequency = 5f;

        [Header("En el aire y tropiezos")]
        [Tooltip("Ángulo de los brazos en el aire (negativo = por encima de la horizontal).")]
        public float airArmDegrees = -30f;
        public float airArmFlap = 28f;
        [Tooltip("Velocidad de caída a partir de la cual aterriza de cara y se queda tirado.")]
        public float tumbleFallSpeed = 12f;
        public float tumbleSpinSpeed = 600f;
        public float lieTime = 0.55f, getUpTime = 0.35f;

        [Header("Deslizarse de panza")]
        public float slideBodyAngle = 62f;
        public float slideArmDegrees = 4f;
        public float slideDustInterval = 0.05f;

        [Header("Polvo")]
        [Range(0f, 1f)] public float dustSpeedThreshold = 0.35f;
        public float dustSize = 0.15f;

        [Header("Bola: piezas blandas")]
        // Estilo muñeco de trapo: muelles muy flojos, poco freno, mucho recorrido; se mueven sobre todo por gravedad e inercia.
        public SoftPart ballArmLeft = new SoftPart { flapAxis = Vector3.forward, flapDegrees = 8f, flapSpeed = 11f, frequency = 1.5f, dampingRatio = 0.2f, inertiaGain = 6.5f, maxDegrees = 85f };
        public SoftPart ballArmRight = new SoftPart { flapAxis = Vector3.back, flapDegrees = 8f, flapSpeed = 11f, frequency = 1.5f, dampingRatio = 0.2f, inertiaGain = 6.5f, maxDegrees = 85f };
        public SoftPart ballFootLeft = new SoftPart { flapAxis = Vector3.right, flapDegrees = 10f, flapSpeed = 14f, frequency = 1.7f, dampingRatio = 0.22f, inertiaGain = 6f, maxDegrees = 80f };
        public SoftPart ballFootRight = new SoftPart { flapAxis = Vector3.right, flapDegrees = 10f, flapSpeed = 14f, flapPhase = Mathf.PI, frequency = 1.7f, dampingRatio = 0.22f, inertiaGain = 6f, maxDegrees = 80f };
        public SoftPart ballTail = new SoftPart { flapAxis = Vector3.up, flapDegrees = 8f, flapSpeed = 8f, frequency = 1.3f, dampingRatio = 0.18f, inertiaGain = 7f, maxDegrees = 85f };
        [Tooltip("Cuánto tira la gravedad de las piezas blandas de la bola (m/s²): más alto = cuelgan más.")]
        public float ballPartGravity = 9f;
        public SoftPart ballFace = new SoftPart { flapAxis = Vector3.forward, flapDegrees = 7f, flapSpeed = 7f, frequency = 2.6f, dampingRatio = 0.3f, inertiaGain = 3.5f, maxDegrees = 30f };

        [Header("Apuntar con la lengua")]
        [Tooltip("Máxima inclinación del cuerpo hacia arriba/abajo al lanzar la lengua.")]
        public float aimPitchMax = 30f;
        [Range(0f, 1f)] public float aimPitchFollow = 0.7f;

        [Header("Reposo: mirar alrededor")]
        [Tooltip("Segundos quieto antes de empezar a mirar alrededor.")]
        public float idleDelay = 1.2f;
        public Vector2 lookInterval = new Vector2(1.4f, 3.2f);
        public float lookYaw = 38f, lookPitch = 10f;
        [Tooltip("Cuánto del giro siguen los ojos / la cabeza / el cuerpo.")]
        public Vector3 lookFollow = new Vector3(1f, 0.18f, 0.3f);

        [Header("Boca")]
        [Tooltip("Grados que sube la tapa con la voz al máximo.")]
        public float talkHeadUp = 16f;
        [Tooltip("Grados que baja el labio inferior con la voz al máximo.")]
        public float talkJawDown = 12f;

        [Header("Rebote de la tapa al correr")]
        [Tooltip("Fracción de la velocidad máxima a partir de la cual la tapa empieza a rebotar.")]
        [Range(0f, 1f)] public float flapSpeedThreshold = 0.35f;
        [Tooltip("Grados que la tapa se va hacia atrás (boca abierta) corriendo a toda velocidad.")]
        public float runMouthOpen = 20f;
        public float flapStepKick = 260f;
        public float flapLandKick = 30f;
        public float flapSpring = 260f;
        public float flapDamping = 9f;
        public float flapMax = 30f;

        [Header("Estilo")]
        [Tooltip("Tamaño de los ojos (1 = original del modelo).")]
        public float eyeScale = 1.25f;
        [Tooltip("Barriga en forma normal / renacuajo / gigante (1 = original).")]
        public Vector3 bellyByForm = new Vector3(1.05f, 1f, 1.35f);
        [Tooltip("Cuánto crece la barriga al llenarse de comida (forma normal).")]
        public float bellyFoodGrowth = 0.3f;

        [Header("Gigante: la barriga se infla como una pelota")]
        [Tooltip("Tamaño de cabeza y extremidades en gigante (1 = normal).")]
        public float giantModelScale = 1.2f;
        [Tooltip("Radio de la bola (debe coincidir con el colisionador del gigante).")]
        public float giantBallRadius = 1.2f;
        [Tooltip("Centro de la barriga en reposo (unidades del modelo).")]
        public float bellyCenterY = 0.33f, bellyRadius = 0.33f;
        [Tooltip("Cuánto se ensancha (y se achata) la bola para que se vea redonda.")]
        public float giantRoundness = 0.1f;
        public Vector3 giantHeadOffset = new Vector3(0f, 0.54f, 0.32f);
        public Vector3 giantArmOffset = new Vector3(0.62f, 0.18f, 0.12f);
        public Vector3 giantLegOffset = new Vector3(0.22f, -0.62f, 0.5f);
        public Vector3 giantTailOffset = new Vector3(0f, -0.2f, -0.68f);

        [Header("Cuerpo")]
        public float waddleDegrees = 7f;
        public float squashAmount = 0.12f;
        [Tooltip("Ensanchamiento horizontal extra del gigante.")]
        public float giantWidth = 1.25f;

        LizardPlayer p;
        LizardLipSync lip;
        Quaternion headRest, jawRest;
        Color baseColor = Color.white;
        MaterialPropertyBlock mpb;

        float scaleNow = 1f, widthNow = 1f, squash, squashVel, flap, flapVel, walkPhase, lastStepPhase;
        float gulpT = 9f, ejectT = 9f, spitT = 9f, hurtT = 9f, popT = 9f, crashT = 9f, helpT = 9f, crashMag;
        float head, jaw;
        bool flashing, previewInit;
        Vector3[] lidOpenPos, lidOpenScale;
        Quaternion legLRest, legRRest;
        float nextBlink = 2f, blinkT = 1f;
        float bellyNow = 1f;
        float idleTime, nextLook, curiousTilt, tiltTarget;
        Vector2 look, lookVel, lookTarget;
        Quaternion eyeLRest = Quaternion.identity, eyeRRest = Quaternion.identity;
        enum Tumble { None, Spinning, Lying, GettingUp }
        Tumble tumble;
        Vector3 tumbleAxis = Vector3.right;
        float tumbleAngle, tumbleT, airW, runW, armBaseFreq = -1f, armBaseGravity = -1f;
        float swimW, swimMoveW, swimPhase, sprintW, nextTrail; // nado: peso, cuánto se mueve y fase de la brazada
        float slideW, nextSlideDust, giantW;
        Vector3 headPosRest, jawPosRest, armLPosRest, armRPosRest, legLPosRest, legRPosRest, tailPosRest;
        bool posCached;
        Quaternion rollRot = Quaternion.identity;
        float aimPitch;
        Vector3 lastVel, bodyAccel;
        bool softReady;

        void Awake()
        {
            p = GetComponentInParent<LizardPlayer>();
            lip = GetComponentInParent<LizardLipSync>();
            headRest = headBone.localRotation;
            jawRest = jawBone.localRotation;
            mpb = new MaterialPropertyBlock();
            CacheRest();
            if (skin && skin.sharedMaterial && skin.sharedMaterial.HasProperty("_BaseColor")) baseColor = skin.sharedMaterial.GetColor("_BaseColor");
        }

        void Start()
        {
            SetupChains();
            SetupBallParts();
            // El jugador local recupera el color de piel que eligió en la ruleta.
            if (p && p.IsControlled && ColorWheelStation.TryLoad(out var saved)) SetSkinColor(saved);
            // …y el sombrero que eligió en el probador.
            if (p && p.IsControlled) { int hat = SwampHatFactory.Load(); if (hat > 0) SwampHatFactory.Equip(p, hat, true); }
            if (p)
            {
                p.Motor.OnLanded += impact =>
                {
                    float k = Mathf.Clamp01(impact / 12f);
                    squash += squashAmount * 2f * k;
                    flapVel += flapLandKick * k * 10f;
                    if (k > 0.2f) Sfx.Play(Sfx.Land, transform.position, 0.3f * k);
                    if (k > 0.25f) DustPuffs.Burst(transform.position, dustSize * scaleNow * (0.8f + k), 5 + Mathf.RoundToInt(k * 5f), 1.5f + k * 2f);
                    if (tumble == Tumble.Spinning) StartLie();
                    if (p.Form == LizardForm.Giant) { ballFace.Kick(new Vector3(-260f * k, 0f, Random.Range(-120f, 120f) * k)); ballArmLeft.Kick(Vector3.forward * 300f * k); ballArmRight.Kick(Vector3.back * 300f * k); }
                    else if (impact >= tumbleFallSpeed && p.Form != LizardForm.Giant) { tumbleAxis = Vector3.right; tumbleAngle = 0f; StartLie(); }
                };
                p.Motor.OnJumped += () => { squash -= squashAmount; Sfx.Play(Sfx.Jump, transform.position, 0.4f); };
                p.Motor.OnSlideStart += () =>
                {
                    squash -= squashAmount * 0.8f;
                    DustPuffs.Burst(transform.position, dustSize * scaleNow * 1.1f, 7, 2.2f);
                    Sfx.Play(Sfx.Jump, transform.position, 0.35f);
                };
                scaleNow = p.Stats.visualScale;
            }
        }

        void SetupBallParts()
        {
            if (!giantBall) return;
            Transform Find(string n) { foreach (var t in giantBall.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
            ballArmLeft.target = Find("Bracito I"); ballArmRight.target = Find("Bracito D");
            ballFootLeft.target = Find("Patita I"); ballFootRight.target = Find("Patita D");
            ballTail.target = Find("Colita");
            ballArmLeft.Init(); ballArmRight.Init(); ballFootLeft.Init(); ballFootRight.Init(); ballTail.Init(); ballFace.Init();
            softReady = true;
        }

        void SetupChains()
        {
            armLeft.restEuler = new Vector3(0f, 0f, armHangDegrees);
            armRight.restEuler = new Vector3(0f, 0f, -armHangDegrees);
            armLeft.Init(); armRight.Init(); tail.Init();
        }

        void CachePositions()
        {
            if (posCached) return;
            posCached = true;
            headPosRest = headBone.localPosition; jawPosRest = jawBone.localPosition;
            if (armLeft.bones != null && armLeft.bones.Length > 0) armLPosRest = armLeft.bones[0].localPosition;
            if (armRight.bones != null && armRight.bones.Length > 0) armRPosRest = armRight.bones[0].localPosition;
            if (legLeft) legLPosRest = legLeft.localPosition;
            if (legRight) legRPosRest = legRight.localPosition;
            if (tail.bones != null && tail.bones.Length > 0) tailPosRest = tail.bones[0].localPosition;
        }

        /// <summary>Coloca cabeza, brazos, piernas y cola sobre la bola según 'w' (0 = normal, 1 = gigante).</summary>
        void ApplyGiantLayout(float w)
        {
            CachePositions();
            headBone.localPosition = headPosRest + giantHeadOffset * w;
            jawBone.localPosition = jawPosRest + giantHeadOffset * w;
            if (armLeft.bones != null && armLeft.bones.Length > 0) armLeft.bones[0].localPosition = armLPosRest + Vector3.Scale(giantArmOffset, new Vector3(-1f, 1f, 1f)) * w;
            if (armRight.bones != null && armRight.bones.Length > 0) armRight.bones[0].localPosition = armRPosRest + giantArmOffset * w;
            if (legLeft) legLeft.localPosition = legLPosRest + Vector3.Scale(giantLegOffset, new Vector3(-1f, 1f, 1f)) * w;
            if (legRight) legRight.localPosition = legRPosRest + giantLegOffset * w;
            if (tail.bones != null && tail.bones.Length > 0) tail.bones[0].localPosition = tailPosRest + giantTailOffset * w;
        }

        /// <summary>La bola crece desde el tamaño de la barriga; el cuerpo se oculta cuando la bola ya lo cubre.</summary>
        void UpdateGiantBall(float w, float t)
        {
            if (!giantBall) return;
            bool on = w > 0.02f;
            if (giantBall.gameObject.activeSelf != on) giantBall.gameObject.SetActive(on);
            if (skin) skin.enabled = w < 0.45f;
            if (!on) return;
            float full = 2f * giantBallRadius / Mathf.Max(0.01f, scaleNow);
            float overshoot = 1f + Mathf.Sin(Mathf.Clamp01(w) * Mathf.PI) * 0.08f;
            float breathe = 1f + Mathf.Sin(t * 2f) * 0.012f * w;
            float size = Mathf.Lerp(0.7f, full, w) * overshoot;
            giantBall.localScale = new Vector3(size * breathe, size / breathe, size * breathe);
            giantBall.localPosition = new Vector3(0f, bellyCenterY, 0.04f);
        }

        /// <summary>Escala de la barriga para que el torso sea una bola del radio del colisionador.</summary>
        float GiantBellyScale(float modelScale) => giantBallRadius / Mathf.Max(0.01f, bellyRadius * modelScale);

        void CacheRest()
        {
            if (eyeLeft) eyeLRest = eyeLeft.localRotation;
            if (eyeRight) eyeRRest = eyeRight.localRotation;
            if (lidOpenPos != null) return;
            lidOpenPos = new Vector3[lids.Length]; lidOpenScale = new Vector3[lids.Length];
            for (int i = 0; i < lids.Length; i++) { lidOpenPos[i] = lids[i].localPosition; lidOpenScale[i] = lids[i].localScale; }
            if (legLeft) legLRest = legLeft.localRotation;
            if (legRight) legRRest = legRight.localRotation;
        }

        // ---- Color de piel (ruleta del lobby, personalización) ----
        Renderer[] tintRenderers;
        bool hasSkinTint; Color skinTint = Color.white;
        public Color SkinColor => skinTint;

        /// <summary>
        /// Cambia el color de piel de todo el personaje (cuerpo, tapa, párpados y bola del gigante con sus bracitos),
        /// respetando la barriga, la boca, los ojos y la punta de la cola.
        /// </summary>
        public void SetSkinColor(Color c)
        {
            skinTint = c; hasSkinTint = true;
            tintRenderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var r in tintRenderers) ApplyTint(r, false);
        }

        void ApplyTint(Renderer r, bool flash)
        {
            if (!r || !r.sharedMaterial || !r.sharedMaterial.HasProperty("_SkinTintWeight")) return;
            r.GetPropertyBlock(mpb ??= new MaterialPropertyBlock());
            mpb.Clear();
            if (hasSkinTint) { mpb.SetColor("_SkinTint", skinTint); mpb.SetFloat("_SkinTintWeight", 1f); }
            if (flash) { mpb.SetColor("_BaseColor", Color.Lerp(baseColor, Color.white, 0.6f)); }
            r.SetPropertyBlock(mpb);
        }

        public override void PlayGulp() => gulpT = 0f;
        public override void PlayEject() => ejectT = 0f;
        public override void PlaySpit() => spitT = 0f;
        public override void PlayHurt()
        {
            hurtT = 0f; flapVel += 300f;
            if (p && p.Form != LizardForm.Giant) StartTumble(p.Body.linearVelocity);
        }

        /// <summary>Empieza a dar vueltas en el aire en la dirección del empujón.</summary>
        void StartTumble(Vector3 worldVel)
        {
            Vector3 flat = Vector3.ProjectOnPlane(worldVel, Vector3.up);
            if (flat.sqrMagnitude < 0.01f) flat = -transform.forward;
            Vector3 axisWorld = Vector3.Cross(Vector3.up, flat.normalized);
            tumbleAxis = transform.InverseTransformDirection(axisWorld).normalized;
            tumble = Tumble.Spinning; tumbleT = 0f; tumbleAngle = 0f;
        }

        void StartLie()
        {
            tumble = Tumble.Lying; tumbleT = 0f;
            p?.Motor.Stun(lieTime + getUpTime * 0.6f);
            DustPuffs.Burst(transform.position, dustSize * scaleNow * 1.2f, 8, 2.5f);
            squash += 0.2f;
        }
        public override void PlayHelp() => helpT = 0f;
        public override void PlayCrash(float speed)
        {
            crashT = 0f; crashMag = Mathf.Clamp01(speed / 12f); squash += 0.25f * crashMag; flapVel += 200f * crashMag;
            ballFace.Kick(new Vector3(300f, 0f, 200f) * crashMag);
            ballTail.Kick(Vector3.up * 400f * crashMag);
        }

        public override void OnFormChanged(LizardForm prev, LizardForm now, bool animate)
        {
            if (animate) popT = 0f;
            if (bubble) bubble.gameObject.SetActive(now == LizardForm.Small);
            if (!animate && p) { scaleNow = p.Config.Get(now).visualScale; widthNow = now == LizardForm.Giant ? giantWidth : 1f; }
        }

        void LateUpdate()
        {
            if (!p) return;
            float dt = Time.deltaTime, t = Time.time;
            var stats = p.Stats;
            gulpT += dt; ejectT += dt; spitT += dt; hurtT += dt; popT += dt; crashT += dt; helpT += dt;

            // Tamaño por forma (con "pop" al transformarse) y compresión/estiramiento.
            bool isGiant = p.Form == LizardForm.Giant;
            scaleNow = Mathf.Lerp(scaleNow, isGiant ? giantModelScale : stats.visualScale, 1f - Mathf.Exp(-dt * 8f));
            widthNow = 1f;
            giantW = Mathf.MoveTowards(giantW, isGiant ? 1f : 0f, dt * 3f);
            float gw = Mathf.SmoothStep(0f, 1f, giantW);
            float pop = popT < 0.45f ? 1f + Mathf.Sin(popT / 0.45f * Mathf.PI) * 0.2f : 1f;
            squashVel += (-160f * squash - 14f * squashVel) * dt;
            squash += squashVel * dt;
            float air = p.Motor.Grounded ? 0f : Mathf.Clamp01(Mathf.Abs(p.Body.linearVelocity.y) / 15f) * squashAmount;
            float s = Mathf.Clamp(squash - air, -0.35f, 0.35f);
            float sc = scaleNow * pop;
            scalePivot.localScale = new Vector3(sc * widthNow * (1f + s * 0.5f), sc * (1f - s), sc * widthNow * (1f + s * 0.5f));

            // Bamboleo al caminar.
            float speed = p.Motor.PlanarSpeed;
            float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.1f, stats.moveSpeed));
            float moveAmt = p.Motor.Grounded ? speed01 : 0.2f;
            walkPhase += dt * (3f + speed * 2.4f) / Mathf.Max(0.5f, scaleNow);
            float lean = Mathf.Clamp(speed01 * 8f, 0f, 8f) * (1f - giantW);
            // Nadando: el cuerpo se tumba hacia delante al avanzar; quieto queda casi vertical, flotando.
            swimW = Mathf.MoveTowards(swimW, p.Motor.Swimming ? 1f : 0f, dt * 4f);
            swimMoveW = Mathf.MoveTowards(swimMoveW, p.Motor.Swimming && speed > 0.3f ? 1f : 0f, dt * 3f);
            swimPhase += dt * Mathf.Lerp(2.2f, 6.5f, swimMoveW);
            lean = Mathf.Lerp(lean, 4f + 28f * swimMoveW + Mathf.Sin(swimPhase * 2f) * 2f * swimMoveW, swimW); // nadando: algo inclinado, cabeza fuera mirando al frente
            if (crashT < 0.3f) lean += Mathf.Sin(crashT * 60f) * 6f * crashMag * (1f - crashT / 0.3f);
            // Reposo: tras un momento quieto mira alrededor (ojos → cabeza → cuerpo) y ladea la cabeza con curiosidad.
            bool still = speed < 0.25f && p.Motor.Grounded && p.Tongue.State == TongueState.Idle && p.Form != LizardForm.Small;
            idleTime = still ? idleTime + dt : 0f;
            if (idleTime > idleDelay)
            {
                if (t >= nextLook)
                {
                    nextLook = t + Random.Range(lookInterval.x, lookInterval.y);
                    bool center = Random.value < 0.3f;
                    lookTarget = center ? Vector2.zero : new Vector2(Random.Range(-lookYaw, lookYaw), Random.Range(-lookPitch * 0.5f, lookPitch));
                    tiltTarget = Random.value < 0.35f ? Random.Range(-9f, 9f) : 0f;
                }
            }
            else { lookTarget = Vector2.zero; tiltTarget = 0f; nextLook = t + 0.4f; }
            look = Vector2.SmoothDamp(look, lookTarget, ref lookVel, 0.35f);
            curiousTilt = Mathf.Lerp(curiousTilt, tiltTarget, 1f - Mathf.Exp(-dt * 3f));
            float idleW = Mathf.Clamp01((idleTime - idleDelay) * 2f);
            float sway = Mathf.Sin(t * 0.9f) * 1.5f * idleW;

            // Nadando: el cuerpo ondula (sube y baja con cada brazada), se balancea de lado a lado y serpentea un poco;
            // flotando quieto, se mece suave con el agua. Así nada queda rígido.
            float swimPitch = swimW * (Mathf.Sin(swimPhase * 2f) * 5f * swimMoveW + Mathf.Sin(t * 1.7f) * 3f * (1f - swimMoveW));
            float swimRoll = swimW * (Mathf.Sin(swimPhase) * 9f * swimMoveW + Mathf.Sin(t * 1.3f) * 4f * (1f - swimMoveW));
            float swimYaw = swimW * Mathf.Sin(swimPhase + 0.8f) * 7f * swimMoveW;
            scalePivot.localRotation = Quaternion.Euler(lean + swimPitch - look.y * lookFollow.z * 0.5f, look.x * lookFollow.z + swimYaw, Mathf.Sin(walkPhase) * waddleDegrees * moveAmt * (1f - giantW) + sway + swimRoll + curiousTilt * 0.4f);
            scalePivot.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(walkPhase)) * 0.04f * moveAmt * scaleNow * (1f - gw)
                                       + Vector3.up * (giantBallRadius - bellyCenterY * scaleNow) * gw;
            ApplyGiantLayout(gw);
            UpdateGiantBall(gw, t);

            // Piezas blandas de la bola: inercia + movimiento propio al rodar (aletear, pedalear, menear).
            Vector3 vel = p.Body.linearVelocity;
            if (dt > 0f) bodyAccel = Vector3.Lerp(bodyAccel, (vel - lastVel) / dt, 1f - Mathf.Exp(-dt * 20f));
            lastVel = vel;
            if (softReady && giantBall && giantBall.gameObject.activeSelf)
            {
                Vector3 accel = Vector3.ClampMagnitude(bodyAccel, 30f);
                // La gravedad "tira" de cada pieza: como la bola rueda, cambian de lado y dan tumbos (muñeco de trapo).
                Vector3 limbAccel = accel - Vector3.down * ballPartGravity;
                float roll = Mathf.Clamp01(Vector3.ProjectOnPlane(vel, Vector3.up).magnitude / Mathf.Max(1f, p.Config.giant.moveSpeed));
                float idleBob = 0.15f;
                float act = Mathf.Max(roll, idleBob);
                ballArmLeft.Step(dt, limbAccel, act, t); ballArmRight.Step(dt, limbAccel, act, t);
                ballFootLeft.Step(dt, limbAccel, roll, t); ballFootRight.Step(dt, limbAccel, roll, t);
                ballTail.Step(dt, limbAccel, act, t);
                ballFace.Simulate(dt, scalePivot.InverseTransformDirection(-accel), roll, t);
            }

            // Al lanzar la lengua se inclina hacia la altura del objetivo para que la boca apunte allí.
            float aimTarget = 0f;
            if (p.Tongue.State != TongueState.Idle && p.Tongue.State != TongueState.Retracting && !isGiant && !p.Tongue.CarriedInMouth)
            {
                Vector3 d = p.Tongue.TipPosition - p.MouthPosition;
                if (p.Tongue.State == TongueState.Extending && p.Tongue.AimHasHit) d = p.Tongue.AimPoint - p.MouthPosition;
                if (d.sqrMagnitude > 0.01f)
                    aimTarget = -Mathf.Clamp(Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg * aimPitchFollow, -aimPitchMax, aimPitchMax);
            }
            aimPitch = Mathf.Lerp(aimPitch, aimTarget, 1f - Mathf.Exp(-dt * 14f));
            if (Mathf.Abs(aimPitch) > 0.05f)
            {
                var arot = Quaternion.AngleAxis(aimPitch, Vector3.right);
                Vector3 ac = Vector3.up * 0.45f * scaleNow;
                scalePivot.localRotation = arot * scalePivot.localRotation;
                scalePivot.localPosition = ac + arot * (scalePivot.localPosition - ac);
            }

            // Gigante: es una bola de verdad. Todo el modelo (cabeza, brazos, patas, cola) rueda alrededor
            // del centro según lo que avanza, sin deslizar. Al dejar de ser gigante se endereza.
            if (isGiant && giantW > 0.5f)
            {
                Vector3 v = Vector3.ProjectOnPlane(p.Body.linearVelocity, Vector3.up);
                if (v.sqrMagnitude > 0.0025f)
                {
                    Vector3 axisWorld = Vector3.Cross(Vector3.up, v.normalized);
                    Vector3 axisLocal = transform.InverseTransformDirection(axisWorld);
                    float deg = v.magnitude / Mathf.Max(0.1f, giantBallRadius) * Mathf.Rad2Deg * dt;
                    rollRot = Quaternion.AngleAxis(deg, axisLocal) * rollRot;
                }
            }
            else rollRot = Quaternion.Slerp(rollRot, Quaternion.identity, 1f - Mathf.Exp(-dt * 6f));
            if (Quaternion.Angle(rollRot, Quaternion.identity) > 0.05f)
            {
                Vector3 c = Vector3.up * giantBallRadius * gw + Vector3.up * 0.45f * scaleNow * (1f - gw);
                scalePivot.localRotation = rollRot * scalePivot.localRotation;
                scalePivot.localPosition = c + rollRot * (scalePivot.localPosition - c);
            }

            // Tropiezo: vueltas en el aire → tirado en el suelo → se levanta. Gira alrededor del centro del cuerpo.
            if (tumble != Tumble.None)
            {
                tumbleT += dt;
                switch (tumble)
                {
                    case Tumble.Spinning:
                        tumbleAngle += tumbleSpinSpeed * dt;
                        if (tumbleT > 1.5f) StartLie();
                        break;
                    case Tumble.Lying:
                    {
                        // Termina de girar hasta quedar tumbado (90°, de cara o de espaldas según la vuelta).
                        float lieTarget = Mathf.Round((tumbleAngle - 90f) / 180f) * 180f + 90f;
                        tumbleAngle = Mathf.Lerp(tumbleAngle, lieTarget, 1f - Mathf.Exp(-dt * 18f));
                        if (tumbleT > lieTime) { tumble = Tumble.GettingUp; tumbleT = 0f; tumbleAngle = Mathf.Repeat(tumbleAngle + 180f, 360f) - 180f; }
                        break;
                    }
                    case Tumble.GettingUp:
                        tumbleAngle = Mathf.Lerp(tumbleAngle, 0f, 1f - Mathf.Exp(-dt * (6f / getUpTime)));
                        if (tumbleT > getUpTime) { tumble = Tumble.None; tumbleAngle = 0f; squash -= 0.12f; }
                        break;
                }
                var rot = Quaternion.AngleAxis(tumbleAngle, tumbleAxis);
                Vector3 c = Vector3.up * 0.45f * scaleNow;
                scalePivot.localRotation = rot * scalePivot.localRotation;
                scalePivot.localPosition += c - rot * c;
            }

            // Deslizarse: tumbado de panza hacia delante, girando alrededor del centro y pegado al suelo.
            bool sliding = p.Motor.Sliding;
            slideW = Mathf.MoveTowards(slideW, sliding ? 1f : 0f, dt * (sliding ? 9f : 4f));
            if (slideW > 0.001f)
            {
                float sw = Mathf.SmoothStep(0f, 1f, slideW);
                var srot = Quaternion.AngleAxis(slideBodyAngle * sw, Vector3.right);
                Vector3 c = Vector3.up * 0.45f * scaleNow;
                scalePivot.localRotation = srot * scalePivot.localRotation;
                scalePivot.localPosition += c - srot * c + Vector3.down * 0.14f * scaleNow * sw;
                if (sliding && p.Motor.Grounded && t >= nextSlideDust)
                {
                    nextSlideDust = t + slideDustInterval;
                    Vector3 side2 = transform.right * Random.Range(-0.18f, 0.18f) * scaleNow;
                    DustPuffs.Spawn(transform.position + side2 + Vector3.up * 0.04f * scaleNow, dustSize * scaleNow * Random.Range(0.8f, 1.2f),
                        -transform.forward * 1.2f + Random.insideUnitSphere * 0.4f, 0.4f);
                }
            }

            // Rebote de la tapa: muelle amortiguado que recibe un golpe en cada paso rápido.
            float stepPhase = Mathf.Floor(walkPhase / Mathf.PI);
            if (stepPhase != lastStepPhase)
            {
                lastStepPhase = stepPhase;
                if (p.Motor.Grounded && p.Motor.Sprinting && speed01 > dustSpeedThreshold && tumble == Tumble.None) // polvo solo corriendo
                {
                    Transform foot = ((int)stepPhase & 1) == 0 ? legLeft : legRight;
                    Vector3 at = foot ? new Vector3(foot.position.x, transform.position.y + 0.04f * scaleNow, foot.position.z) : transform.position;
                    Vector3 back = -transform.forward * (0.6f + speed01) + Random.insideUnitSphere * 0.3f;
                    DustPuffs.Spawn(at - transform.forward * 0.08f * scaleNow, dustSize * scaleNow * Random.Range(0.8f, 1.15f) * (0.6f + speed01 * 0.6f), back);
                }
                float over = Mathf.InverseLerp(flapSpeedThreshold, 1f, speed01);
                if (p.Motor.Grounded && over > 0f) flapVel += flapStepKick * over * Random.Range(0.8f, 1.2f);
            }
            flapVel += (-flapSpring * flap - flapDamping * flapVel) * dt;
            flap += flapVel * dt;
            if (flap > flapMax) { flap = flapMax; flapVel = -Mathf.Abs(flapVel) * 0.3f; }
            if (flap < -6f) { flap = -6f; flapVel = Mathf.Abs(flapVel) * 0.3f; }

            // Boca: voz + acciones.
            float talk = lip ? lip.Openness : 0f;
            float headTarget = talk * talkHeadUp;
            // Corriendo, el "viento" echa la tapa hacia atrás; los pasos la hacen rebotar (flap).
            if (p.Motor.Grounded && tumble == Tumble.None)
                headTarget = Mathf.Max(headTarget, runMouthOpen * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, speed01)) * (1f - giantW) * (1f - slideW));
            float jawTarget = talk * talkJawDown;
            if (p.Tongue.State != TongueState.Idle) { headTarget = Mathf.Max(headTarget, 18f); jawTarget = Mathf.Max(jawTarget, 8f); }
            var carried = p.Tongue.CarriedInMouth;
            if (carried)
            {
                float open = Mathf.Clamp(carried.size / Mathf.Max(0.1f, scaleNow) * 38f, 14f, 32f);
                headTarget = Mathf.Max(headTarget, open); jawTarget = Mathf.Max(jawTarget, open * 0.45f);
            }
            if (gulpT < 0.35f) { float k = Mathf.Sin(gulpT / 0.35f * Mathf.PI); headTarget = Mathf.Max(headTarget, k * 20f); jawTarget = Mathf.Max(jawTarget, k * 8f); }
            if (ejectT < 0.4f) { float k = Mathf.Sin(ejectT / 0.4f * Mathf.PI); headTarget = Mathf.Max(headTarget, k * 28f); jawTarget = Mathf.Max(jawTarget, k * 12f); }
            if (spitT < 0.3f) { float k = Mathf.Sin(spitT / 0.3f * Mathf.PI); headTarget = Mathf.Max(headTarget, k * 24f); jawTarget = Mathf.Max(jawTarget, k * 10f); }
            if (hurtT < 0.4f) jawTarget = Mathf.Max(jawTarget, 10f);
            if (helpT < 1f) headTarget = Mathf.Max(headTarget, Mathf.Abs(Mathf.Sin(helpT * 12f)) * 22f);
            head = Mathf.Lerp(head, headTarget, 1f - Mathf.Exp(-dt * 25f));
            jaw = Mathf.Lerp(jaw, jawTarget, 1f - Mathf.Exp(-dt * 25f));
            // Corriendo: la cabeza se echa atrás (el "viento") y los ojos miran hacia arriba/atrás.
            sprintW = Mathf.MoveTowards(sprintW, p.Motor.Grounded && p.Motor.Sprinting && speed01 > 0.4f && tumble == Tumble.None ? 1f : 0f, dt * 5f);
            headBone.localRotation = headRest * Quaternion.Euler(-(head + flap) - sprintW * 10f, look.x * lookFollow.y, curiousTilt * 0.6f);
            if (giantW > 0.01f) headBone.localRotation *= Quaternion.Slerp(Quaternion.identity, ballFace.Offset, giantW);
            if (eyeLeft) eyeLeft.localRotation = eyeLRest * Quaternion.Euler(-look.y * lookFollow.x * 0.6f - sprintW * 28f, look.x * lookFollow.x * 0.6f, 0f);
            if (eyeRight) eyeRight.localRotation = eyeRRest * Quaternion.Euler(-look.y * lookFollow.x * 0.6f - sprintW * 28f, look.x * lookFollow.x * 0.6f, 0f);
            // Estela de polvo corriendo: bolitas redondas en fila detrás, casi quietas, que se encogen.
            if (sprintW > 0.6f && t >= nextTrail)
            {
                nextTrail = t + 0.045f;
                Vector3 at = transform.position - transform.forward * 0.18f * scaleNow + Vector3.up * 0.06f * scaleNow + transform.right * Random.Range(-0.05f, 0.05f) * scaleNow;
                DustPuffs.Spawn(at, dustSize * scaleNow * Random.Range(1.0f, 1.3f), Vector3.up * 0.25f + Random.insideUnitSphere * 0.08f, 0.55f);
            }

            // Ojos grandes; se aprietan al recibir un golpe y se abren de sorpresa al aterrizar fuerte.
            float eyeK = eyeScale * (hurtT < 0.4f ? Mathf.Lerp(0.8f, 1f, hurtT / 0.4f) : 1f) * (1f + Mathf.Clamp(squash, 0f, 0.3f) * 0.4f);
            if (eyeLeft) eyeLeft.localScale = Vector3.one * eyeK;
            if (eyeRight) eyeRight.localScale = Vector3.one * eyeK;

            // Barriga: según la forma y la comida, con un rebote al tragar.
            if (bellyBone)
            {
                float target = p.Form == LizardForm.Small ? bellyByForm.y : bellyByForm.x;
                if (!giantBall) target = Mathf.Lerp(target, GiantBellyScale(scaleNow), gw);
                if (p.Form == LizardForm.Normal) target += bellyFoodGrowth * Mathf.Clamp01(p.Stomach.Food / p.Config.giantThreshold);
                if (gulpT < 0.4f) target += Mathf.Sin(gulpT / 0.4f * Mathf.PI) * 0.12f;
                bellyNow = Mathf.Lerp(bellyNow, target, 1f - Mathf.Exp(-dt * 8f));
                float breathe = 1f + Mathf.Sin(t * 2.2f) * 0.02f;
                float wide = 1f + giantRoundness * gw, tall = 1f - giantRoundness * 0.8f * gw;
                bellyBone.localScale = new Vector3(bellyNow * wide, bellyNow * breathe * tall, bellyNow * wide);
            }
            jawBone.localRotation = jawRest * Quaternion.Euler(jaw + Mathf.Max(0f, flap) * 0.3f, 0f, 0f);

            // Piernas: pasos alternos al caminar, recogidas en el aire.
            if (legLeft && legRight)
            {
                float step = Mathf.Sin(walkPhase) * stepDegrees * moveAmt;
                float tuck = (p.Motor.Grounded ? 0f : -25f) * (1f - slideW) - 20f * slideW;
                step *= 1f - slideW;
                step *= 1f - giantW;
                // Nadando: patalea rápido y alterno al avanzar; flotando, mueve las patas despacio.
                float kick = Mathf.Sin(t * 3f) * 10f * (1f - swimMoveW);                 // flotando: alternas, suaves
                float frog = (Mathf.Pow(Mathf.Sin(swimPhase) * 0.5f + 0.5f, 2f) - 0.5f) * 55f * swimMoveW; // nadando: juntas
                step = Mathf.Lerp(step, kick, swimW);
                tuck = Mathf.Lerp(tuck, -18f - 22f * swimMoveW + frog, swimW);
                legLeft.localRotation = legLRest * Quaternion.Euler(step + tuck, 0f, 0f);
                legRight.localRotation = legRRest * Quaternion.Euler(-step + tuck, 0f, 0f);
            }

            // Parpadeo (y ojos apretados al recibir un golpe).
            if (t >= nextBlink) { blinkT = 0f; nextBlink = t + Random.Range(blinkInterval.x, blinkInterval.y); }
            blinkT += dt;
            float lid = blinkT < 0.15f ? Mathf.Sin(blinkT / 0.15f * Mathf.PI) : 0f;
            if (hurtT < 0.5f) lid = Mathf.Max(lid, 0.75f);
            for (int i = 0; i < lids.Length; i++)
            {
                var op = lidOpenPos[i];
                lids[i].localPosition = new Vector3(op.x, Mathf.Lerp(op.y, lidClosedY, lid), op.z);
                lids[i].localScale = Vector3.Lerp(lidOpenScale[i], lidClosedScale, lid);
            }

            // Brazos fideo y cola: empujón lateral en cada paso, opuesto en cada brazo.
            // En el aire (o dando vueltas) los brazos suben y aletean; en el suelo cuelgan.
            bool airborne = (!p.Motor.Grounded && !p.Motor.Swimming) || tumble == Tumble.Spinning;
            airW = Mathf.MoveTowards(airW, airborne ? 1f : 0f, dt * (airborne ? 6f : 4f));
            float flapArm = Mathf.Sin(t * 17f) * airArmFlap * airW;
            // Al correr los brazos suben hacia los lados con un ligero barrido atrás; la mano se retrasa sola por inercia.
            float runTarget = p.Motor.Grounded && tumble == Tumble.None ? (p.Motor.Sprinting ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.85f, speed01)) : 0f) : runW;
            runW = Mathf.MoveTowards(runW, runTarget, dt * 4f);
            float hang = Mathf.Lerp(Mathf.Lerp(armHangDegrees, runArmDegrees, runW), airArmDegrees, airW);
            // Positivo = hacia atrás. En reposo se adelantan un poco; al correr se van atrás.
            float sweep = Mathf.Lerp(-armIdleForward, runArmSweep, runW) * (1f - airW);
            hang = Mathf.Lerp(hang, slideArmDegrees, slideW);
            sweep = Mathf.Lerp(sweep, 0f, slideW);
            // Braza: los brazos se abren hacia delante y tiran hacia atrás; quieto, chapotea suave para mantenerse a flote.
            if (swimW > 0.001f)
            {
                float stroke = Mathf.Sin(swimPhase);
                // Valores negativos = brazos arriba (como airArmDegrees). Flotando: alzados y chapoteando;
                // nadando: brazadas grandes que salen del agua por delante y vuelven atrás.
                // Flotando: brazos abiertos casi horizontales, apoyados sobre el agua, meciéndose con las ondas.
                // Nadando: brazadas a ras de agua (adelante → atrás) sin levantarlos por encima de la cabeza.
                // Flotando: brazos abiertos a los lados sobre el agua. Nadando (tumbado): brazadas de braza hacia atrás.
                float swimHang = Mathf.Lerp(12f + Mathf.Sin(t * 2.2f) * 5f, 5f + Mathf.Max(0f, stroke) * 20f, swimMoveW);
                float swimSweep = Mathf.Lerp(-10f + Mathf.Sin(t * 2.2f + 1f) * 8f, -30f + (stroke * 0.5f + 0.5f) * 110f, swimMoveW);
                hang = Mathf.Lerp(hang, swimHang, swimW);
                sweep = Mathf.Lerp(sweep, swimSweep, swimW);
                flapArm *= 1f - swimW;
            }
            armLeft.restEuler = new Vector3(0f, -sweep, hang + flapArm);
            armRight.restEuler = new Vector3(0f, sweep, -(hang - flapArm));
            if (armBaseFreq < 0f) armBaseFreq = armLeft.frequency;
            armLeft.frequency = armRight.frequency = Mathf.Lerp(armBaseFreq, runArmFrequency, runW);
            if (armBaseGravity < 0f) armBaseGravity = armLeft.gravity;
            armLeft.gravity = armRight.gravity = Mathf.Lerp(armBaseGravity, 0f, runW);

            // Solo se empujan al moverse; quietos vuelven a su pose y se quedan tranquilos.
            float wob = Mathf.Cos(walkPhase) * speed01 * moveAmt;
            Vector3 side = scalePivot.right, fwd = scalePivot.forward;
            Vector3 bounce = Vector3.up * Mathf.Abs(wob) * 0.4f;
            armLeft.Step(dt, (side * -wob * 0.5f + bounce) * armLeft.wobble * 12f);
            armRight.Step(dt, (side * wob * 0.5f + bounce) * armRight.wobble * 12f);
            float tailSway = Mathf.Sin(walkPhase) * speed01 + Mathf.Sin(t * 1.3f) * 0.12f * idleW;
            tailSway = Mathf.Lerp(tailSway, Mathf.Sin(swimPhase * 2f + 1f) * (0.4f + 1.2f * swimMoveW), swimW);
            tail.Step(dt, side * tailSway * tail.wobble * 10f);

            // Burbuja del renacuajo y señal de ayuda.
            if (bubble && bubble.gameObject.activeSelf)
            {
                float w = 1f + Mathf.Sin(t * 5f) * 0.04f + (helpT < 1f ? Mathf.Sin(helpT * 25f) * 0.08f : 0f);
                bubble.localScale = new Vector3(w, 2f - w, w) * 1.5f * scaleNow;
                bubble.localPosition = Vector3.up * 0.55f * scaleNow;
            }
            if (ping)
            {
                bool pinging = helpT < 1f;
                ping.gameObject.SetActive(pinging);
                if (pinging) { ping.localScale = Vector3.one * Mathf.Lerp(0.5f, 3f, helpT) * Mathf.Max(0.6f, scaleNow); ping.localPosition = Vector3.up * 1.4f * Mathf.Max(0.5f, scaleNow); }
            }

            // Destello de daño e invulnerabilidad.
            bool flash = hurtT < 0.15f || (p.Health.Invulnerable && Mathf.Repeat(t * 10f, 1f) < 0.3f);
            if (skin && (flash || flash != flashing))
            {
                flashing = flash;
                // El destello conserva el color de piel elegido.
                tintRenderers ??= GetComponentsInChildren<Renderer>(true);
                foreach (var r in tintRenderers) if (r == skin || r.sharedMaterial == skin.sharedMaterial) ApplyTint(r, flash);
            }
        }

        /// <summary>Vista previa en el editor (sin Play): tamaño de forma y apertura de boca 0..1.</summary>
        public void PreviewPose(float visualScale, bool giant, bool small, float talk01)
        {
            if (!headBone) return;
            if (!previewInit) { previewInit = true; headRest = headBone.localRotation; jawRest = jawBone.localRotation; }
            if (giant) visualScale = giantModelScale;
            scalePivot.localScale = Vector3.one * visualScale;
            scalePivot.localPosition = giant ? Vector3.up * (giantBallRadius - bellyCenterY * visualScale) : Vector3.zero;
            ApplyGiantLayout(giant ? 1f : 0f);
            if (giantBall)
            {
                giantBall.gameObject.SetActive(giant);
                if (giant) { float full = 2f * giantBallRadius / visualScale; giantBall.localScale = Vector3.one * full; giantBall.localPosition = new Vector3(0f, bellyCenterY, 0.04f); }
                if (skin) skin.enabled = !giant;
            }
            if (bubble) { bubble.gameObject.SetActive(small); bubble.localScale = Vector3.one * 1.5f * visualScale; bubble.localPosition = Vector3.up * 0.55f * visualScale; }
            headBone.localRotation = headRest * Quaternion.Euler(-talk01 * talkHeadUp, 0f, 0f);
            if (eyeLeft) eyeLeft.localScale = eyeRight.localScale = Vector3.one * eyeScale;
            if (bellyBone)
            {
                float k = giant && !giantBall ? GiantBellyScale(visualScale) : small ? bellyByForm.y : bellyByForm.x;
                float wide = giant ? 1f + giantRoundness : 1f, tall = giant ? 1f - giantRoundness * 0.8f : 1f;
                bellyBone.localScale = new Vector3(k * wide, k * tall, k * wide);
            }
            if (armLeft.bones != null && armLeft.bones.Length > 0) { SetupChains(); }
            jawBone.localRotation = jawRest * Quaternion.Euler(talk01 * talkJawDown, 0f, 0f);
        }
    }
}
