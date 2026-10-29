using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Animación procedural: reposo, parpadeo, caminar, salto/caída/aterrizaje con compresión y estiramiento,
    /// mandíbula (lengua, comer, expulsar, ácido, voz), daño, transformación, renacuajo, rodar/chocar/frenar.
    /// Solo mueve transformaciones visuales; nunca toca la física.
    /// </summary>
    public class LizardVisual : MonoBehaviour
    {
        [Header("Piezas (las rellena LizardModelFactory)")]
        public Transform scalePivot, bodyPivot, rollPivot, body, belly, headPivot, jawPivot, mouthPoint, bubble, ping;
        public Transform[] eyes, lids, legs, tail, spots;

        struct Shape
        {
            public Vector3 bodyScale, bellyPos, bellyScale, headPos;
            public float rollY, headScale, legScale, tailScale;
        }

        static readonly Shape NormalShape = new Shape
        {
            bodyScale = new Vector3(0.78f, 0.7f, 0.95f), rollY = 0.42f,
            bellyPos = new Vector3(0f, 0.38f, 0.12f), bellyScale = new Vector3(0.62f, 0.55f, 0.72f),
            headPos = new Vector3(0f, 0.62f, 0.22f), headScale = 1f, legScale = 1f, tailScale = 1f,
        };
        static readonly Shape SmallShape = new Shape
        {
            bodyScale = new Vector3(0.62f, 0.55f, 0.7f), rollY = 0.5f,
            bellyPos = new Vector3(0f, 0.45f, 0.1f), bellyScale = new Vector3(0.48f, 0.42f, 0.5f),
            headPos = new Vector3(0f, 0.68f, 0.18f), headScale = 1.15f, legScale = 0.0f, tailScale = 1.7f,
        };
        static readonly Shape GiantShape = new Shape
        {
            bodyScale = new Vector3(1f, 1f, 1f), rollY = 0.5f,
            bellyPos = new Vector3(0f, 0.46f, 0.2f), bellyScale = new Vector3(0.85f, 0.8f, 0.7f),
            headPos = new Vector3(0f, 0.8f, 0.3f), headScale = 0.7f, legScale = 0.5f, tailScale = 0.7f,
        };

        LizardPlayer p;
        LizardLipSync lipSync;
        Shape shape;
        Vector3[] legRest; Quaternion[] tailRest;
        Renderer[] rends; Color[] baseColors; MaterialPropertyBlock mpb;

        float squash, squashVel, walkPhase, nextBlink, blinkT = 1f;
        float gulpT = 9f, ejectT = 9f, spitT = 9f, hurtT = 9f, popT = 9f, crashT = 9f, helpT = 9f, crashMag;
        float lastYaw, yawRate, jaw, scaleNow = 1f;
        float[] tailYaw;
        bool flashing;

        void Awake()
        {
            p = GetComponentInParent<LizardPlayer>();
            lipSync = GetComponentInParent<LizardLipSync>();
            shape = NormalShape;
            legRest = new Vector3[legs.Length];
            for (int i = 0; i < legs.Length; i++) legRest[i] = legs[i].localPosition;
            tailRest = new Quaternion[tail.Length];
            for (int i = 0; i < tail.Length; i++) tailRest[i] = tail[i].localRotation;
            tailYaw = new float[tail.Length];
            rends = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[rends.Length];
            for (int i = 0; i < rends.Length; i++)
                baseColors[i] = rends[i].sharedMaterial && rends[i].sharedMaterial.HasProperty("_BaseColor") ? rends[i].sharedMaterial.GetColor("_BaseColor") : Color.white;
            mpb = new MaterialPropertyBlock();
            nextBlink = Time.time + 2f;
        }

        void Start()
        {
            if (p)
            {
                p.Motor.OnLanded += OnLanded;
                p.Motor.OnJumped += () => { squash -= p.Config.squashAmount; Sfx.Play(Sfx.Jump, transform.position, 0.4f); };
                scaleNow = p.Stats.visualScale;
            }
        }

        void OnLanded(float impact)
        {
            float k = Mathf.Clamp01(impact / 12f);
            if (k > 0.2f) { squash += p.Config.squashAmount * 2f * k; Sfx.Play(Sfx.Land, transform.position, 0.3f * k); }
        }

        // ---- Disparadores ----
        public void PlayGulp() => gulpT = 0f;
        public void PlayEject() => ejectT = 0f;
        public void PlaySpit() => spitT = 0f;
        public void PlayHurt() => hurtT = 0f;
        public void PlayHelp() => helpT = 0f;
        public void PlayCrash(float speed) { crashT = 0f; crashMag = Mathf.Clamp01(speed / 12f); squash += 0.25f * crashMag; }

        public void OnFormChanged(LizardForm prev, LizardForm now, bool animate)
        {
            if (animate) popT = 0f;
            bubble.gameObject.SetActive(now == LizardForm.Small);
            foreach (var s in spots) s.gameObject.SetActive(now == LizardForm.Giant);
            if (!animate) { shape = Target(now); scaleNow = p.Config.Get(now).visualScale; ApplyShape(); }
        }

        static Shape Target(LizardForm f) => f == LizardForm.Small ? SmallShape : f == LizardForm.Giant ? GiantShape : NormalShape;

        void LateUpdate()
        {
            if (!p) return;
            float dt = Time.deltaTime, t = Time.time;
            var cfg = p.Config; var stats = p.Stats;
            gulpT += dt; ejectT += dt; spitT += dt; hurtT += dt; popT += dt; crashT += dt; helpT += dt;

            // Morph entre formas y escala global con "pop".
            var target = Target(p.Form);
            if (p.Form == LizardForm.Normal)
            {
                float fill = Mathf.Clamp01(p.Stomach.Food / cfg.giantThreshold);
                target.bellyScale *= 1f + 0.45f * fill;
                target.bellyPos.z += 0.08f * fill;
            }
            float m = 1f - Mathf.Exp(-dt * 10f);
            shape.bodyScale = Vector3.Lerp(shape.bodyScale, target.bodyScale, m);
            shape.bellyPos = Vector3.Lerp(shape.bellyPos, target.bellyPos, m);
            shape.bellyScale = Vector3.Lerp(shape.bellyScale, target.bellyScale, m);
            shape.headPos = Vector3.Lerp(shape.headPos, target.headPos, m);
            shape.rollY = Mathf.Lerp(shape.rollY, target.rollY, m);
            shape.headScale = Mathf.Lerp(shape.headScale, target.headScale, m);
            shape.legScale = Mathf.Lerp(shape.legScale, target.legScale, m);
            shape.tailScale = Mathf.Lerp(shape.tailScale, target.tailScale, m);
            scaleNow = Mathf.Lerp(scaleNow, stats.visualScale, 1f - Mathf.Exp(-dt * 8f));
            float pop = popT < 0.45f ? 1f + Mathf.Sin(popT / 0.45f * Mathf.PI) * 0.2f : 1f;
            scalePivot.localScale = Vector3.one * scaleNow * pop;
            ApplyShape();

            // Respiración.
            float breathe = 1f + Mathf.Sin(t * 2.2f) * 0.025f;
            belly.localScale = shape.bellyScale * breathe;
            if (gulpT < 0.4f) belly.localScale *= 1f + Mathf.Sin(gulpT / 0.4f * Mathf.PI) * 0.15f;

            // Compresión y estiramiento (muelle amortiguado + velocidad vertical).
            squashVel += (-160f * squash - 14f * squashVel) * dt;
            squash += squashVel * dt;
            float vy = p.Motor.Grounded ? 0f : p.Body.linearVelocity.y;
            float air = Mathf.Clamp(Mathf.Abs(vy) / 15f, 0f, 1f) * cfg.squashAmount;
            float s = Mathf.Clamp(squash - air, -0.35f, 0.35f);
            bodyPivot.localScale = new Vector3(1f + s * 0.5f, 1f - s, 1f + s * 0.5f);

            // Caminar / balanceo lateral.
            float speed = p.Motor.PlanarSpeed;
            float moveAmt = p.Motor.Grounded ? Mathf.Clamp01(speed / Mathf.Max(1f, stats.moveSpeed)) : 0.3f;
            walkPhase += dt * (4f + speed * 2.2f) / Mathf.Max(0.5f, scaleNow);
            float waddle = Mathf.Sin(walkPhase) * 5f * moveAmt;
            float tilt = p.Form == LizardForm.Giant && p.Motor.IsBraking ? -12f : 0f;
            if (crashT < 0.3f) tilt += Mathf.Sin(crashT * 60f) * 6f * crashMag * (1f - crashT / 0.3f);
            bodyPivot.localRotation = Quaternion.Euler(tilt, 0f, waddle);

            for (int i = 0; i < legs.Length; i++)
            {
                float side = (i % 2 == 0) ? 1f : -1f; float pair = i < 2 ? 0f : Mathf.PI;
                float swing = Mathf.Sin(walkPhase * 2f + pair + (side > 0 ? 0f : Mathf.PI)) * 35f * moveAmt;
                if (!p.Motor.Grounded) swing = (i < 2 ? -25f : 25f);
                if (p.Form == LizardForm.Giant && p.Motor.IsBraking) swing = Mathf.Sin(t * 30f + i) * 30f;
                legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
            }

            // Rodar (solo cuerpo y manchas; cabeza y ojos quedan reconocibles arriba).
            if (p.Form == LizardForm.Giant)
            {
                Vector3 v = Vector3.ProjectOnPlane(p.Body.linearVelocity, Vector3.up);
                if (v.sqrMagnitude > 0.01f)
                {
                    float r = Mathf.Max(0.1f, stats.colliderRadius);
                    Vector3 axis = Vector3.Cross(Vector3.up, v.normalized);
                    rollPivot.Rotate(axis, v.magnitude / r * Mathf.Rad2Deg * dt, Space.World);
                }
            }
            else rollPivot.localRotation = Quaternion.Slerp(rollPivot.localRotation, Quaternion.identity, m);

            // Mandíbula: la mayor apertura entre las acciones activas y la voz.
            float jawTarget = 0f;
            if (p.Tongue.State != TongueState.Idle) jawTarget = 28f;
            if (gulpT < 0.35f) jawTarget = Mathf.Max(jawTarget, Mathf.Sin(gulpT / 0.35f * Mathf.PI) * 25f);
            if (ejectT < 0.4f) jawTarget = Mathf.Max(jawTarget, Mathf.Sin(ejectT / 0.4f * Mathf.PI) * 38f);
            if (spitT < 0.3f) jawTarget = Mathf.Max(jawTarget, Mathf.Sin(spitT / 0.3f * Mathf.PI) * 32f);
            if (hurtT < 0.4f) jawTarget = Mathf.Max(jawTarget, 20f);
            if (helpT < 1f) jawTarget = Mathf.Max(jawTarget, Mathf.Abs(Mathf.Sin(helpT * 12f)) * 25f);
            if (lipSync) jawTarget = Mathf.Max(jawTarget, lipSync.Openness * cfg.maxJawAngle);
            jaw = Mathf.Lerp(jaw, jawTarget, 1f - Mathf.Exp(-dt * 25f));
            jawPivot.localRotation = Quaternion.Euler(jaw, 0f, 0f);

            // Cabeza: retroceso al escupir, avance al expulsar, temblor al pedir ayuda.
            float headPitch = 0f;
            if (spitT < 0.3f) headPitch -= Mathf.Sin(spitT / 0.3f * Mathf.PI) * 15f;
            if (ejectT < 0.4f) headPitch += Mathf.Sin(ejectT / 0.4f * Mathf.PI) * 12f;
            if (gulpT < 0.4f) headPitch -= Mathf.Sin(gulpT / 0.4f * Mathf.PI * 2f) * 6f;
            float headRoll = helpT < 1f ? Mathf.Sin(helpT * 20f) * 10f : 0f;
            headPivot.localRotation = Quaternion.Euler(headPitch, 0f, headRoll);

            // Parpadeo (y ojos entrecerrados al recibir daño).
            if (t >= nextBlink) { blinkT = 0f; nextBlink = t + Random.Range(cfg.blinkMin, cfg.blinkMax); }
            blinkT += dt;
            float lid = blinkT < 0.15f ? Mathf.Sin(blinkT / 0.15f * Mathf.PI) : 0f;
            if (hurtT < 0.5f) lid = Mathf.Max(lid, 0.7f);
            foreach (var l in lids)
            {
                l.localPosition = new Vector3(0f, Mathf.Lerp(0.09f, 0.02f, lid), -0.005f);
                l.localScale = new Vector3(0.285f, Mathf.Lerp(0.12f, 0.285f, lid), 0.285f);
            }

            // Cola: sigue el giro con retraso y se agita (mucho más en renacuajo).
            float yaw = transform.eulerAngles.y;
            yawRate = Mathf.Lerp(yawRate, Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(dt, 0.0001f), 1f - Mathf.Exp(-dt * 8f));
            lastYaw = yaw;
            bool tadpole = p.Form == LizardForm.Small;
            float wagAmp = tadpole ? 28f : 6f + moveAmt * 8f;
            float wagFreq = tadpole ? 12f : 3f + moveAmt * 6f;
            for (int i = 0; i < tail.Length; i++)
            {
                float targetYaw = Mathf.Clamp(-yawRate * 0.03f, -25f, 25f) + Mathf.Sin(t * wagFreq - i * 0.7f) * wagAmp * (0.4f + i * 0.15f);
                tailYaw[i] = Mathf.Lerp(tailYaw[i], targetYaw, 1f - Mathf.Exp(-dt * 12f));
                tail[i].localRotation = tailRest[i] * Quaternion.Euler(0f, tailYaw[i], 0f);
            }

            // Burbuja del renacuajo.
            if (bubble.gameObject.activeSelf)
            {
                float w = 1f + Mathf.Sin(t * 5f) * 0.04f + (helpT < 1f ? Mathf.Sin(helpT * 25f) * 0.08f : 0f);
                bubble.localScale = new Vector3(w, 2f - w, w) * 1.35f * scaleNow;
                bubble.localPosition = Vector3.up * 0.5f * scaleNow;
            }

            // Señal de ayuda: onda que crece.
            bool pinging = helpT < 1f;
            ping.gameObject.SetActive(pinging);
            if (pinging)
            {
                float k = helpT;
                ping.localScale = Vector3.one * Mathf.Lerp(0.5f, 3f, k) * Mathf.Max(0.6f, scaleNow);
                ping.localPosition = Vector3.up * (1.2f * Mathf.Max(0.5f, scaleNow));
            }

            // Destello de daño e invulnerabilidad.
            bool flash = hurtT < 0.15f || (p.Health.Invulnerable && Mathf.Repeat(t * 10f, 1f) < 0.3f);
            if (flash != flashing || flash)
            {
                flashing = flash;
                for (int i = 0; i < rends.Length; i++)
                {
                    if (rends[i].transform == bubble || rends[i].transform == ping) continue;
                    if (flash) { mpb.SetColor("_BaseColor", Color.Lerp(baseColors[i], Color.white, 0.6f)); rends[i].SetPropertyBlock(mpb); }
                    else rends[i].SetPropertyBlock(null);
                }
            }
        }

        void ApplyShape()
        {
            body.localScale = shape.bodyScale;
            rollPivot.localPosition = new Vector3(0f, shape.rollY, 0f);
            belly.localPosition = shape.bellyPos;
            headPivot.localPosition = shape.headPos;
            headPivot.localScale = Vector3.one * shape.headScale;
            for (int i = 0; i < legs.Length; i++)
            {
                legs[i].localScale = Vector3.one * shape.legScale;
                legs[i].gameObject.SetActive(shape.legScale > 0.05f);
                Vector3 rest = legRest[i];
                // Patas del gigante: bajan al contorno de la esfera.
                legs[i].localPosition = Vector3.Lerp(rest, new Vector3(rest.x * 1.3f, rest.y * shape.legScale, rest.z * 1.2f), 1f - shape.legScale);
            }
            if (tail.Length > 0) tail[0].localScale = Vector3.one * shape.tailScale;
        }
    }
}
