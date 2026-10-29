using UnityEngine.InputSystem;
using UnityEngine;

namespace Lizards
{
    /// <summary>Interfaz provisional (IMGUI): reservas, estado, objetivo de lengua, indicaciones y menú de controles.</summary>
    public class LizardHUD : MonoBehaviour
    {
        public LizardCameraRig rig;
        public static bool MenuOpen { get; private set; }
        public bool showDebug = true;

        GUIStyle label, big, small, box;
        Texture2D white;
        string rebindNote = "";

        LizardPlayer P => rig ? rig.target : null;

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.tabKey.wasPressedThisFrame)
            {
                MenuOpen = !MenuOpen;
                Cursor.lockState = MenuOpen ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = MenuOpen;
            }
            if (kb != null && kb.f10Key.wasPressedThisFrame) showDebug = !showDebug;
        }

        void Styles()
        {
            if (label != null) return;
            white = Texture2D.whiteTexture;
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
            label.normal.textColor = Color.white;
            big = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };
            small = new GUIStyle(label) { fontSize = 12 };
            box = new GUIStyle(GUI.skin.box);
        }

        void Bar(Rect r, float v, Color c, string text, float? mark1 = null, float? mark2 = null)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(r, white);
            GUI.color = c; GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(v), r.height - 4), white);
            GUI.color = Color.white;
            if (mark1.HasValue) GUI.DrawTexture(new Rect(r.x + r.width * mark1.Value - 1, r.y - 3, 3, r.height + 6), white);
            if (mark2.HasValue) { GUI.color = new Color(1f, 0.6f, 0.2f); GUI.DrawTexture(new Rect(r.x + r.width * mark2.Value - 1, r.y, 2, r.height), white); GUI.color = Color.white; }
            GUI.Label(new Rect(r.x + 6, r.y, r.width, r.height), text, small);
        }

        void OnGUI()
        {
            var p = P;
            if (!p) return;
            Styles();
            var cfg = p.Config;

            // ---- Panel de estado ----
            float x = 16, y = 16, w = 300;
            GUI.color = new Color(0f, 0f, 0f, 0.35f); GUI.DrawTexture(new Rect(8, 8, w + 16, 150), white); GUI.color = Color.white;
            string formName = p.Form == LizardForm.Giant ? "GIGANTE" : p.Form == LizardForm.Small ? "RENACUAJO" : "NORMAL";
            string pending = p.Forms.Pending.HasValue ? "  <color=#ffb040>(sin espacio para transformarse)</color>" : "";
            GUI.Label(new Rect(x, y, 600, 26), $"{p.displayName}: <b>{formName}</b>{pending}", big); y += 30;

            float food = p.Stomach.Food;
            string emptyWarn = p.Form != LizardForm.Small && food <= 0.001f ? "  ¡VACÍO!" : "";
            Bar(new Rect(x, y, w, 20), food / cfg.maxFood, new Color(0.75f, 0.9f, 0.3f), $"Comida {food:0.#}/{cfg.maxFood:0}{emptyWarn}", cfg.giantThreshold / cfg.maxFood, cfg.giantExitThreshold / cfg.maxFood); y += 26;
            float prog = p.Form == LizardForm.Giant ? 1f : Mathf.Clamp01(food / cfg.giantThreshold);
            Bar(new Rect(x, y, w, 14), prog, new Color(0.4f, 0.8f, 0.3f), p.Form == LizardForm.Giant ? $"Gigante (vuelve a normal bajo {cfg.giantExitThreshold:0})" : $"Progreso a gigante {prog * 100f:0}%"); y += 20;
            string acidState = !p.Stats.acidEnabled ? " (no disponible en esta forma)" : p.Acid.CooldownRemaining > 0f ? $" (recarga {p.Acid.CooldownRemaining:0.0}s)" : "";
            Bar(new Rect(x, y, w, 16), p.Acid.Ammo / cfg.maxAcid, new Color(0.45f, 1f, 0.2f), $"Ácido {p.Acid.Ammo:0.0}/{cfg.maxAcid:0}{acidState}"); y += 22;
            if (p.Health.Invulnerable) { GUI.Label(new Rect(x, y, w, 20), $"Protección tras golpe: {p.Health.InvulnerableRemaining:0.0}s", small); }

            // ---- Mira y objetivo de lengua ----
            var t = p.Tongue;
            Vector2 c = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Color cross = !p.Stats.tongueEnabled ? Color.gray : t.AimValid ? new Color(0.3f, 1f, 0.4f) : t.AimHasHit && t.AimLabel.Length > 0 ? new Color(1f, 0.6f, 0.1f) : Color.white;
            GUI.color = cross;
            GUI.DrawTexture(new Rect(c.x - 9, c.y - 1, 18, 3), white);
            GUI.DrawTexture(new Rect(c.x - 1, c.y - 9, 3, 18), white);
            GUI.color = Color.white;
            string tl = t.State == TongueState.Attached ? $"{t.AttachedLabel}  ·  largo {t.CurrentLength:0.0} m  ·  tensión {t.Tension01 * 100f:0}%"
                : t.AimLabel.Length > 0 ? $"{t.AimLabel}  ·  {t.AimDistance:0.0}/{p.Stats.tongueRange:0} m" : $"Alcance de la lengua: {p.Stats.tongueRange:0} m";
            var tsz = label.CalcSize(new GUIContent(tl));
            GUI.Label(new Rect(c.x - tsz.x / 2, c.y + 18, tsz.x + 10, 24), tl, label);

            // ---- Indicaciones contextuales ----
            var input = p.Input;
            string hint = "";
            var partner = p.FindSmallPartner();
            if (p.Form == LizardForm.Small)
                hint = $"Eres un renacuajo: [{input.Display(input.Help)}] pedir ayuda · come algo · o ve al punto de rescate (anillo azul)";
            else if (partner && (p.Stomach.Food > 0 || t.HeldFood))
                hint = $"[{input.Display(input.Eject)}] Alimentar a {partner.displayName}";
            else if (t.HeldFood)
                hint = t.HeldFood.FitsMouth(p.Stats.mouthCapacity) ? $"[{input.Display(input.Interact)}] Tragar" : "Comida grande: arrástrala o llévala a la meta";
            else if (t.HeldPartner)
                hint = "Suelta la lengua con el compañero cerca para lanzarlo";
            else
            {
                var f = p.FindNearestFood(out bool fits);
                if (f) hint = fits ? $"[{input.Display(input.Interact)}] Comer" : "Demasiado grande para tragar: usa la lengua para arrastrarla";
            }
            foreach (var other in LizardPlayer.All)
                if (other != p && other.IsRequestingHelp)
                {
                    Vector3 sp = rig.CameraTransform.GetComponent<Camera>().WorldToScreenPoint(other.CenterPosition + Vector3.up);
                    if (sp.z > 0) { GUI.color = new Color(0.4f, 0.9f, 1f); GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - 30, 200, 24), "¡AYUDA!", big); GUI.color = Color.white; }
                    hint = $"{other.displayName} pide ayuda: agárralo con la lengua o dale comida [{input.Display(input.Eject)}]";
                }
            if (hint.Length > 0)
            {
                var hs = big.CalcSize(new GUIContent(hint));
                GUI.color = new Color(0f, 0f, 0f, 0.45f); GUI.DrawTexture(new Rect(c.x - hs.x / 2 - 10, Screen.height - 110, hs.x + 20, 34), white); GUI.color = Color.white;
                GUI.Label(new Rect(c.x - hs.x / 2, Screen.height - 106, hs.x + 10, 30), hint, big);
            }

            // ---- Mensajes ----
            float my = Screen.height - 70;
            for (int i = p.Messages.Count - 1; i >= 0; i--)
            {
                var (text, time) = p.Messages[i];
                float age = Time.time - time;
                if (age > 4f) continue;
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(4f - age));
                GUI.Label(new Rect(Screen.width - 520, my - 160, 500, 24), text, label);
                my -= 24;
            }
            GUI.color = Color.white;

            GUI.Label(new Rect(16, Screen.height - 28, 900, 24), "[Tab] controles y reasignación · [F10] depuración · [Esc] liberar ratón · clic para capturar ratón", small);

            if (showDebug) DrawDebug(p);
            if (MenuOpen) DrawMenu(p);
        }

        void DrawDebug(LizardPlayer p)
        {
            float x = Screen.width - 330, y = 12;
            GUI.color = new Color(0f, 0f, 0f, 0.45f); GUI.DrawTexture(new Rect(x - 8, y - 4, 326, 250), white); GUI.color = Color.white;
            var lip = p.GetComponent<LizardLipSync>();
            string[] lines =
            {
                "<b>DEPURACIÓN</b>",
                "F1 Normal · F2 Renacuajo · F3 Gigante",
                "F4 +1 comida · F5 −1 comida · F6 recargar ácido",
                "F7 recibir golpe · F8 crear comida delante",
                "F9 reiniciar escena · F11 micrófono on/off",
                "F12 cambiar de personaje controlado",
                $"Comida en el mundo: {FoodItem.TotalInWorld():0.##}",
                $"Comido {p.Stomach.TotalIn:0.##} · expulsado/dado {p.Stomach.TotalOut:0.##}",
                $"Golpes: {p.Health.HitsTaken} aplicados, {p.Health.HitsIgnored} ignorados (protección)",
                $"Suelo: {(p.Motor.Grounded ? "sí" : "no")} · vel {p.Body.linearVelocity.magnitude:0.0} · masa {p.Body.mass:0.#}",
                $"Micrófono: {(lip ? lip.Status : "—")} · boca {(lip ? lip.Openness : 0f):0.00}",
            };
            foreach (var l in lines) { GUI.Label(new Rect(x, y, 330, 22), l, small); y += 21; }
        }

        void DrawMenu(LizardPlayer p)
        {
            var input = p.Input;
            float w = 560, h = 500, x = (Screen.width - w) / 2, y = (Screen.height - h) / 2;
            GUI.color = new Color(0f, 0f, 0f, 0.8f); GUI.DrawTexture(new Rect(x, y, w, h), white); GUI.color = Color.white;
            GUI.Label(new Rect(x + 16, y + 10, w, 30), "Controles (clic en un control para reasignarlo)", big);
            float yy = y + 50;
            foreach (var a in input.Rebindable)
            {
                GUI.Label(new Rect(x + 16, yy, 200, 24), a.name, label);
                int k = 0;
                for (int i = 0; i < a.bindings.Count; i++)
                {
                    var b = a.bindings[i];
                    if (b.isComposite) continue;
                    string disp = a.GetBindingDisplayString(i);
                    if (string.IsNullOrEmpty(disp)) continue;
                    string grp = b.isPartOfComposite ? b.name + ": " : "";
                    int idx = i;
                    if (GUI.Button(new Rect(x + 190 + (k % 3) * 105, yy + (k / 3) * 24, 100, 22), grp + disp) && !input.IsRebinding)
                    {
                        rebindNote = $"Pulsa una tecla para «{a.name}» (Esc cancela)…";
                        input.StartRebind(a, idx, () => rebindNote = "Guardado.");
                    }
                    k++;
                }
                yy += 24 * Mathf.Max(1, (k + 2) / 3) + 6;
            }
            GUI.Label(new Rect(x + 16, yy + 6, w - 32, 24), rebindNote, label);
            if (GUI.Button(new Rect(x + 16, y + h - 40, 200, 28), "Restablecer controles")) { input.ResetBindings(); rebindNote = "Controles restablecidos."; }
            if (GUI.Button(new Rect(x + w - 136, y + h - 40, 120, 28), "Cerrar")) { MenuOpen = false; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
    }
}
