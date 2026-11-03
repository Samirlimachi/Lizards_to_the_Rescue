using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// El Jefe del refugio (Guardián del pantano). Da vida al personaje hecho de piezas sueltas:
    /// respira, parpadea, te mira, saluda con la mano, habla con bocadillo y frases según lo que has hecho.
    /// No modifica la escena: al empezar agrupa las piezas en articulaciones (cabeza y brazos).
    /// </summary>
    public class JefeController : MonoBehaviour
    {
        [Header("Diálogo")]
        public float greetRadius = 11f;
        public float talkRadius = 6.5f;
        public float charsPerSecond = 38f;
        public float lineHold = 4.5f;

        [Header("Mirada")]
        public float maxYaw = 35f, maxPitch = 14f;

        JefeRig rig;
        Quaternion armLRest, armRRest, torsoRest, crownRest;
        Vector3 torsoScale, sacScale;
        Quaternion[] lidRest = new Quaternion[0];
        float laughT = 9f, croakT = 9f, nextChuckle = 15f, mouthOpen;
        // Mirada inquieta (saltos rápidos de ojos), parpadeo desfasado por ojo y corona con muelle.
        Vector3 glance; float nextGlance, crownTilt, crownVel, tapBlendR, tapBlendL;
        readonly float[] lidBlinkDelay = { 0f, 0.07f };
        Quaternion elbowLRest = Quaternion.identity, elbowRRest = Quaternion.identity;
        Vector3 sprShR, sprElR, sprShL, sprElL, velShR, velElR, velShL, velElL, torsoSpr, torsoVel;

        /// <summary>Muelle amortiguado por eje (grados): da inercia, rebote y suavidad a cada articulación.</summary>
        static void Spring(ref Vector3 x, ref Vector3 v, Vector3 goal, float k, float damp, float dt)
        {
            dt = Mathf.Min(dt, 1f / 30f);
            v += ((goal - x) * k - v * damp) * dt;
            x += v * dt;
        }
        Transform head => rig ? rig.head : null;

        // Diálogo
        string fullLine = "", shownLine = "";
        float typed, holdUntil, nextBlink = 2f, blinkT = 1f, waveT = 9f, talkBounce, nextBlip;
        bool greeted, speaking;
        int lineIndex;
        Vector3 lookDir;
        GUIStyle bubbleStyle, nameStyle;

        static readonly string[] Jokes =
        {
            "Tengo tres ojos y aun así pierdo las llaves.",
            "No me mires la barriga: es pura fuerza.",
            "¿Sabías que las ranas sueñan en verde? Bueno, yo sí.",
            "Esta corona no es de adorno. Bueno… un poco sí.",
        };

        void Start()
        {
            rig = GetComponentInChildren<JefeRig>();
            if (!rig) { Debug.LogWarning("[Lizards] El Jefe no tiene modelo: usa Lizards → Crear Jefe (modelo propio)."); return; }
            armLRest = rig.armL.localRotation; armRRest = rig.armR.localRotation;
            if (rig.elbowL) elbowLRest = rig.elbowL.localRotation;
            if (rig.elbowR) elbowRRest = rig.elbowR.localRotation;
            torsoRest = rig.torso.localRotation; torsoScale = rig.torso.localScale;
            sacScale = rig.sac.localScale; crownRest = rig.crown.localRotation;
            lidRest = rig.lids.Select(l => l.localRotation).ToArray();
        }

        // ---------------- Diálogo ----------------

        void Say(string line)
        {
            fullLine = line; shownLine = ""; typed = 0f; speaking = true;
            holdUntil = float.MaxValue;
        }

        List<string> Lines(LizardPlayer p)
        {
            var list = new List<string> { "Soy el Jefe del pantano. Aquí te preparas antes de cada expedición." };
            if (!ColorWheelStation.TryLoad(out _)) list.Add("¿Ves esa ruleta? Te pinta del color que quieras. ¡Pruébala!");
            if (SwampHatFactory.Load() == 0) list.Add("Un buen rescatador necesita sombrero. En la tienda tienes para elegir.");
            var pocket = SwampPocket.Ensure();
            if (pocket.Coins < 40) list.Add("Con monedas sacas cosas de la expendedora. Trae comida y te pagaré.");
            list.Add("Tu misión: rescata a Pip y trae 8 de comida. La balsa te lleva a la estación.");
            list.Add("¿Quieres jugar con amigos? Usa el teléfono y crea una sala con un código.");
            list.Add(Jokes[Random.Range(0, Jokes.Length)]);
            return list;
        }

        void NextLine(LizardPlayer p)
        {
            if (speaking && typed < fullLine.Length) { typed = fullLine.Length; shownLine = fullLine; return; } // completar
            var lines = Lines(p);
            string line = lines[lineIndex % lines.Count];
            Say(line);
            if (System.Array.IndexOf(Jokes, line) >= 0) laughT = 0f;
            lineIndex++;
            talkBounce = 1f;
        }

        // ---------------- Animación ----------------

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            var p = LizardPlayer.All.FirstOrDefault(x => x.IsControlled);

            // Saludo al llegar y prompt para hablar.
            if (p && !LizardLobby.Visible)
            {
                float d = Vector3.Distance(Flat(p.transform.position), Flat(transform.position + transform.TransformVector(new Vector3(0f, 0f, -3f))));
                if (!greeted && d < greetRadius)
                {
                    greeted = true; waveT = 0f;
                    Say($"¡Croac! Bienvenido al Refugio, {p.displayName}.");
                    croakT = 0f;
                    Sfx.Play(Sfx.Help, transform.position + Vector3.up * 8f, 0.5f);
                }
                if (d < talkRadius)
                {
                    InteractPrompts.Offer(this, transform.position + Vector3.up * 3f + transform.forward * -4f, "Hablar con el Jefe", d);
                    if (InteractPrompts.IsActive(this) && p.Input.Pressed(p.Input.Interact)) NextLine(p);
                }
                if (d > greetRadius + 6f) greeted = false; // vuelve a saludar si te vas lejos y regresas
            }

            // Escribir el texto letra a letra, con "blips".
            if (speaking)
            {
                if (typed < fullLine.Length)
                {
                    typed = Mathf.Min(fullLine.Length, typed + dt * charsPerSecond);
                    shownLine = fullLine.Substring(0, (int)typed);
                    if (t >= nextBlip && fullLine.Length > 0) { nextBlip = t + 0.09f; Sfx.Play(Sfx.Jump, transform.position + Vector3.up * 8f, 0.12f); }
                    if (typed >= fullLine.Length) holdUntil = t + lineHold;
                }
                else if (t > holdUntil) speaking = false;
            }
            bool mouthMoving = speaking && typed < fullLine.Length;

            if (!rig) return;
            waveT += dt; laughT += dt; croakT += dt;
            talkBounce = Mathf.MoveTowards(talkBounce, 0f, dt * 2f);
            // Risita de vez en cuando aunque nadie le hable.
            if (t > nextChuckle) { nextChuckle = t + Random.Range(14f, 24f); if (!speaking) laughT = 0f; }
            bool laughing = laughT < 1.8f;
            float laugh = laughing ? Mathf.Sin(Mathf.Clamp01(laughT / 0.2f) * Mathf.PI * 0.5f) * (1f - Mathf.Clamp01((laughT - 1.4f) / 0.4f)) : 0f;

            // Respiración + barriga que tiembla al reír.
            float breathe = Mathf.Sin(t * 1.3f);
            float jiggle = laugh * Mathf.Sin(t * 22f) * 0.03f;
            rig.torso.localScale = Vector3.Scale(torsoScale, new Vector3(1f + breathe * 0.012f + jiggle, 1f + breathe * 0.018f - jiggle, 1f + breathe * 0.015f));

            // Mirar al jugador girando el cuerpo (poco) y los ojos (mucho).
            // Sin nadie cerca, mira de un lado a otro con saltos de ojos; con jugador, lo vigila con algún vistazo suelto.
            if (t >= nextGlance)
            {
                nextGlance = t + Random.Range(1.2f, 3.5f);
                glance = Quaternion.Euler(Random.Range(-8f, 12f), Random.Range(-35f, 35f), 0f) * -transform.forward;
            }
            bool near = p && Vector3.Distance(p.transform.position, transform.position) < 28f;
            Vector3 target = near ? (p.CenterPosition - rig.head.position).normalized : glance;
            if (near && Mathf.Repeat(t, 7f) > 6.4f && !speaking) target = glance; // vistazo rápido a otro lado
            lookDir = Vector3.Slerp(lookDir == Vector3.zero ? target : lookDir, target, 1f - Mathf.Exp(-dt * 7f));
            Vector3 local = transform.InverseTransformDirection(lookDir);
            float yaw = Mathf.Clamp(Mathf.Atan2(-local.x, -local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
            float sway = Mathf.Sin(t * 0.6f) * 1.5f + (speaking ? Mathf.Sin(t * 2.6f) * 1.5f : 0f);
            float bob = laugh * Mathf.Sin(t * 20f) * 2f - talkBounce * 3f;
            Spring(ref torsoSpr, ref torsoVel, new Vector3(bob * 0.8f + laugh * 4f, yaw * 0.25f, sway * 0.7f), 50f, 8f, dt);
            rig.torso.localRotation = torsoRest * Quaternion.Euler(torsoSpr);
            for (int i = 0; i < rig.irises.Length; i++)
            {
                var iris = rig.irises[i];
                Vector3 l = iris.parent.InverseTransformDirection(lookDir);
                // El ojo extra (el último) va a su aire: tiembla y mira desacompasado.
                bool extra = i == rig.irises.Length - 1 && rig.irises.Length > rig.lids.Length;
                if (extra) l += new Vector3(Mathf.PerlinNoise(t * 2.3f, 1f) - 0.5f, Mathf.PerlinNoise(t * 2.1f, 7f) - 0.5f, 0f) * 1.4f;
                var goal = Quaternion.LookRotation(new Vector3(Mathf.Clamp(l.x, -0.55f, 0.55f), Mathf.Clamp(l.y, -0.4f, 0.4f), 1f));
                iris.localRotation = Quaternion.Slerp(iris.localRotation, goal, 1f - Mathf.Exp(-dt * (extra ? 6f : 18f)));
            }

            // Parpadeo: cada ojo con un pequeño desfase (más inquietante); al reír, ojos entrecerrados.
            if (t >= nextBlink) { blinkT = 0f; nextBlink = t + Random.Range(2.2f, 5f) * (Random.value < 0.2f ? 0.15f : 1f); }
            blinkT += dt;
            for (int i = 0; i < rig.lids.Length; i++)
            {
                float bt = blinkT - (i < lidBlinkDelay.Length ? lidBlinkDelay[i] : 0f);
                float lid = bt > 0f && bt < 0.2f ? Mathf.Sin(bt / 0.2f * Mathf.PI) : 0f;
                lid = Mathf.Max(lid, laugh * 0.7f);
                rig.lids[i].localRotation = lidRest[i] * Quaternion.Euler(lid * 68f, 0f, 0f);
            }

            // Boca: abre la tapa de la cabeza al hablar y mucho al reír.
            float goalOpen = rig.restOpen + (mouthMoving ? 4f + Mathf.Abs(Mathf.Sin(t * 13f)) * 12f : 0f) + laugh * (14f + Mathf.Abs(Mathf.Sin(t * 18f)) * 6f);
            mouthOpen = Mathf.Lerp(mouthOpen == 0f ? goalOpen : mouthOpen, goalOpen, 1f - Mathf.Exp(-dt * 18f));
            rig.head.localRotation = Quaternion.Euler(-mouthOpen, 0f, 0f);

            // Saco bucal: se hincha al croar (saludo) y late un poco al hablar.
            float croak = croakT < 1.2f ? Mathf.Sin(croakT / 1.2f * Mathf.PI) : 0f;
            // Garganta de sapo: bombea rápido y poquito todo el rato.
            float gular = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 4.2f)), 3f) * 0.07f;
            float sacK = 1f + breathe * 0.03f + gular + croak * 0.9f + (mouthMoving ? Mathf.Abs(Mathf.Sin(t * 13f)) * 0.08f : 0f);
            rig.sac.localScale = Vector3.Scale(sacScale, new Vector3(1f + (sacK - 1f) * 0.5f, sacK, sacK));

            // Corona con muelle: se queda atrás al moverse y rebota con la risa.
            float crownGoal = Mathf.Sin(t * 1.1f) * 1.5f + sway * 0.6f;
            crownVel += ((crownGoal - crownTilt) * 60f - crownVel * 7f + laugh * Mathf.Sin(t * 20f) * 120f) * dt;
            crownTilt += crownVel * dt;
            rig.crown.localRotation = crownRest * Quaternion.Euler(laugh * Mathf.Sin(t * 20f + 0.8f) * 3f, 0f, crownTilt);

            // Brazos: tamborilea en el tronco (entra y sale suave); saluda al llegar; palmadas al reír; gesticula al hablar.
            float wave = waveT < 2.2f ? Mathf.SmoothStep(0f, 1f, waveT / 0.35f) * (1f - Mathf.SmoothStep(0f, 1f, (waveT - 1.8f) / 0.4f)) : 0f;
            float slap = laugh * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 14f)), 2f) * 14f;
            tapBlendR = Mathf.MoveTowards(tapBlendR, Mathf.Sin(t * 0.4f) > 0.6f && !speaking ? 1f : 0f, dt * 2f);
            tapBlendL = Mathf.MoveTowards(tapBlendL, Mathf.Sin(t * 0.4f + 2f) > 0.6f && !speaking ? 1f : 0f, dt * 2f);
            float tapR = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 6f)), 4f) * 5f * tapBlendR;
            float tapL = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 6f + 1.6f)), 4f) * 5f * tapBlendL;
            float gestR = speaking ? Mathf.Max(0f, Mathf.Sin(t * 2.4f)) : 0f, gestL = speaking ? Mathf.Max(0f, Mathf.Sin(t * 2.4f + 2.2f)) : 0f;
            float breathLift = breathe * 1.5f;

            // Objetivos (grados): hombro (x = adelante/arriba, z = abrir) y codo (x = levantar antebrazo, z = girar mano).
            // Derecha: saluda, tamborilea, gesticula y da palmadas en la rodilla al reír.
            // Saludo: el brazo se abre hacia fuera y el antebrazo sube en el plano lateral (nunca pasa por delante de la cara).
            Vector3 shR = new Vector3((-breathLift - gestR * 8f - slap * 0.3f) * (1f - wave) - wave * 10f, 0f, wave * 70f + gestR * 4f);
            Vector3 elR = new Vector3((-tapR * 2.2f - gestR * 22f - slap * 2.2f) * (1f - wave), 0f, wave * (140f + Mathf.Sin(waveT * 11f) * 18f));
            Vector3 shL = new Vector3(-breathLift - gestL * 8f - slap * 0.3f, 0f, -gestL * 4f);
            Vector3 elL = new Vector3(-tapL * 2.2f - gestL * 22f - slap * 2.2f, 0f, 0f);

            // Muelles: cada articulación persigue su objetivo con inercia (se pasa un poco y vuelve), el codo más suelto.
            Spring(ref sprShR, ref velShR, shR, 90f, 12f, dt); Spring(ref sprElR, ref velElR, elR, 140f, 10f, dt);
            Spring(ref sprShL, ref velShL, shL, 90f, 12f, dt); Spring(ref sprElL, ref velElL, elL, 140f, 10f, dt);
            rig.armR.localRotation = armRRest * Quaternion.Euler(sprShR);
            rig.armL.localRotation = armLRest * Quaternion.Euler(sprShL);
            if (rig.elbowR) rig.elbowR.localRotation = elbowRRest * Quaternion.Euler(sprElR + new Vector3(Mathf.Sin(t * 1.7f) * 1.5f, 0f, 0f));
            if (rig.elbowL) rig.elbowL.localRotation = elbowLRest * Quaternion.Euler(sprElL + new Vector3(Mathf.Sin(t * 1.5f + 1f) * 1.5f, 0f, 0f));
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ---------------- Bocadillo ----------------

        void OnGUI()
        {
            // Caja de diálogo fija abajo, centrada: siempre visible aunque el Jefe quede fuera de cámara.
            if (!speaking || shownLine.Length == 0 || LizardLobby.Visible) return;
            GUI.depth = -50; // por encima del HUD
            float ui = Mathf.Clamp(Screen.height / 720f, 0.8f, 2f);
            bubbleStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, alignment = TextAnchor.UpperLeft, richText = true };
            nameStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            bubbleStyle.fontSize = Mathf.RoundToInt(21 * ui); bubbleStyle.normal.textColor = new Color(0.93f, 0.95f, 0.85f);
            nameStyle.fontSize = Mathf.RoundToInt(17 * ui); nameStyle.normal.textColor = new Color(0.62f, 1f, 0.3f);

            float w = Mathf.Min(760f * ui, Screen.width - 40f);
            float pad = 22f * ui;
            float h = Mathf.Max(bubbleStyle.CalcHeight(new GUIContent(fullLine), w - pad * 2f), bubbleStyle.lineHeight * 2f) + 54f * ui;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - 110f * ui; // deja sitio al inventario de abajo
            GUI.DrawTexture(new Rect(x + 5f, y + 6f, w, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0f, 0f, 0f, 0.35f), 0, 18f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0.09f, 0.11f, 0.08f, 0.93f), 0, 18f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0.45f, 0.75f, 0.2f), 3f * ui, 18f);
            GUI.Label(new Rect(x + pad, y + 10f * ui, w - pad * 2f, 26f * ui), "EL JEFE DEL PANTANO", nameStyle);
            GUI.Label(new Rect(x + pad, y + 38f * ui, w - pad * 2f, h - 44f * ui), shownLine, bubbleStyle);
            if (typed >= fullLine.Length && Mathf.Repeat(Time.time, 1f) < 0.6f)
                GUI.Label(new Rect(x + w - pad - 120f * ui, y + h - 28f * ui, 120f * ui, 24f * ui), "[E] seguir ▸", nameStyle);
        }
    }
}
