using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UI = UnityEngine.UI;

namespace Lizards
{
    public sealed partial class PcFrontEnd
    {
        int settingsTab;
        float rowY;
        static readonly Color RowColor = new(.16f, .16f, .18f);
        static readonly Color ControlColor = new(.25f, .23f, .30f);

        void Settings()
        {
            var tabs = new[] { "GRÁFICOS", "SONIDO", "CONTROLES", "REASIGNAR", "OTROS" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                Button(tabs[i], 99 + 345 * i, 66, 337, 79, () =>
                {
                    if (bindings.IsRebinding) return;
                    settingsTab = index;
                    Show(Page.Settings);
                }, 45, i == settingsTab ? Purple : RowColor);
            }
            Shade(101, 165, 1718, 2, new Color(.40f, .33f, .45f));
            rowY = 205;
            switch (settingsTab)
            {
                case 0: Graphics(); break;
                case 1:
                    Range("VOLUMEN GENERAL", "Volume", 1, 0, 1, "%", () => AudioListener.volume = PcGameSettings.Get("Volume", 1f));
                    Range("VOLUMEN DE LOS EFECTOS", "Effects", 1, 0, 1, "%");
                    Button("PROBAR SONIDO", 1305, 361, 490, 57, () => PlayPreview(), 31, ControlColor);
                    break;
                case 2:
                    Toggle("INVERTIR CÁMARA VERTICAL", "Invert", false);
                    Toggle("AUTOAJUSTE DE CÁMARA", "AutoCamera", false);
                    Range("SENSIBILIDAD DEL RATÓN", "Sensitivity", 1, .2f, 3, "%");
                    break;
                case 3: Bindings(); break;
                case 4: Toggle("MOSTRAR NOMBRES DE JUGADORES", "Names", true); break;
            }

            Button("RESTABLECER AJUSTES", 534, 939, 500, 71, () =>
            {
                if (bindings.IsRebinding) return;
                PcGameSettings.Reset();
                ApplyOcclusion();
                bindings.ResetBindings();
                RefreshPlayerBindings();
                Show(Page.Settings);
            }, 39, ControlColor);
            Button("LISTO", 1088, 939, 290, 71, () =>
            {
                if (!bindings.IsRebinding) Show(settingsFrom);
            }, 43, Purple);
        }

        void Graphics()
        {
            FullScreenMode[] modes = { FullScreenMode.Windowed, FullScreenMode.FullScreenWindow, FullScreenMode.ExclusiveFullScreen };
            int modeIndex = Array.IndexOf(modes, (FullScreenMode)PcGameSettings.Get("Window", (int)FullScreenMode.FullScreenWindow));
            Choice("MODO DE VENTANA", new[] { "Ventana", "Pantalla completa sin bordes", "Pantalla completa exclusiva" }, modeIndex,
                i => { PcGameSettings.Set("Window", (int)modes[i]); PcGameSettings.Apply(); });

            var resolutions = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct().OrderBy(r => r.x).ThenBy(r => r.y).ToList();
            var current = new Vector2Int(PcGameSettings.Get("Width", Screen.width), PcGameSettings.Get("Height", Screen.height));
            if (!resolutions.Contains(current)) resolutions.Add(current);
            Choice("RESOLUCIÓN", resolutions.Select(r => $"{r.x} × {r.y}").ToArray(), resolutions.IndexOf(current), i =>
            {
                PcGameSettings.Set("Width", resolutions[i].x);
                PcGameSettings.Set("Height", resolutions[i].y);
                PcGameSettings.Apply();
            });
            Range("CAMPO DE VISIÓN", "Fov", 60, 45, 100, "°");
            Toggle("SINCRONIZACIÓN VERTICAL", "VSync", true, PcGameSettings.Apply);
            if (ambientOcclusion) Toggle("OCLUSIÓN AMBIENTAL", "Occlusion", initialOcclusion, ApplyOcclusion);
            int[] limits = { 30, 60, 120, 144, 240, -1 };
            Choice("LÍMITE DE FPS", new[] { "30", "60", "120", "144", "240", "Sin límite" },
                Array.IndexOf(limits, PcGameSettings.Get("Fps", 120)), i => { PcGameSettings.Set("Fps", limits[i]); PcGameSettings.Apply(); });
            Toggle("EFECTOS DE MOVIMIENTO DE CÁMARA", "Shake", true);
        }

        float Row(string label)
        {
            float y = rowY;
            rowY += 67;
            Shade(100, y, 1720, 59, RowColor);
            Text(label, 135, y, 920, 59, 27, new Color(.92f, .90f, .94f));
            return y;
        }

        void Toggle(string label, string key, bool initial, Action apply = null)
        {
            bool value = PcGameSettings.Get(key, initial ? 1 : 0) == 1;
            float y = Row(label);
            var control = Button(value ? "SÍ" : "NO", 1654, y + 7, 142, 45, () =>
            {
                value = !value;
                PcGameSettings.Set(key, value ? 1 : 0);
                apply?.Invoke();
                Show(Page.Settings);
            }, 28, value ? Purple : ControlColor);
            control.name = label + " · interruptor";
        }

        void Choice(string label, string[] values, int current, Action<int> apply)
        {
            int index = Mathf.Clamp(current, 0, values.Length - 1);
            float y = Row(label);
            var value = Text(values[index], 1221, y, 502, 59, 25, Color.white, TextAnchor.MiddleCenter);
            void Step(int direction)
            {
                index = (index + direction + values.Length) % values.Length;
                value.text = values[index];
                apply(index);
            }
            Button("◀", 1153, y + 7, 66, 45, () => Step(-1), 23, ControlColor).GetComponentInChildren<UI.Text>().font = bodyFont;
            Button("▶", 1731, y + 7, 66, 45, () => Step(1), 23, ControlColor).GetComponentInChildren<UI.Text>().font = bodyFont;
        }

        void Range(string label, string key, float initial, float min, float max, string unit, Action apply = null)
        {
            float y = Row(label);
            var rect = NewRect(label + " · deslizador", panel, 1250, y + 12, 477, 35);
            var slider = rect.gameObject.AddComponent<UI.Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = unit == "°";
            Shade(0, 10, 477, 16, new Color(.34f, .33f, .36f), parent: rect);
            var fillArea = NewRect("Área de relleno", rect, 0, 10, 477, 16);
            var fill = Shade(0, 0, 477, 16, Purple, parent: fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            slider.fillRect = fill;
            var handleArea = NewRect("Área de control", rect, 13, 0, 451, 35);
            var handle = Shade(0, 0, 25, 34, Lime, true, handleArea);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = Vector2.up;
            handle.pivot = new Vector2(.5f, .5f);
            handle.anchoredPosition = Vector2.zero;
            handle.sizeDelta = new Vector2(25, -6);
            var image = handle.GetComponent<UI.Image>();
            image.raycastTarget = true;
            slider.handleRect = handle;
            slider.targetGraphic = image;
            var raycast = rect.gameObject.AddComponent<UI.Image>();
            raycast.color = Color.clear;
            string Format(float value) => Mathf.RoundToInt(unit == "%" ? value * 100 : value) + unit;
            slider.SetValueWithoutNotify(PcGameSettings.Get(key, initial));
            var number = Text(Format(slider.value), 1084, y, 143, 59, 26, Color.white, TextAnchor.MiddleRight);
            slider.onValueChanged.AddListener(value =>
            {
                number.text = Format(value);
                PcGameSettings.Set(key, value);
                apply?.Invoke();
            });
        }

        void PlayPreview()
        {
            var source = GetComponent<AudioSource>();
            if (!source) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.PlayOneShot(Sfx.Eat, PcGameSettings.EffectsVolume);
        }

        void Bindings()
        {
            var options = new List<(InputAction action, int index, string label)>();
            foreach (var action in bindings.Rebindable)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isComposite || !binding.path.StartsWith("<Keyboard>") && !binding.path.StartsWith("<Mouse>")) continue;
                    string suffix = binding.name switch
                    {
                        "Up" => " adelante", "Down" => " atrás", "Left" => " izquierda", "Right" => " derecha",
                        "Negative" => " · acortar", "Positive" => " · alargar", _ => ""
                    };
                    options.Add((action, i, (action.name + suffix).ToUpperInvariant()));
                }
            }
            var scrollRoot = NewRect("Controles reasignables", panel, 100, 205, 1720, 620);
            var scroll = scrollRoot.gameObject.AddComponent<UI.ScrollRect>();
            var viewport = Shade(0, 0, 1710, 620, new Color(.09f, .09f, .10f), parent: scrollRoot);
            viewport.GetComponent<UI.Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<UI.RectMask2D>();
            var content = NewRect("Asignaciones", viewport, 0, 0, 1692, options.Count * 68);
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 52;
            var message = Text("Pulsa una asignación y luego la tecla o el botón del ratón.", 100, 843, 1718, 44, 24, new Color(.79f, .75f, .84f));

            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                float y = i * 68;
                Shade(0, y, 1690, 60, RowColor, parent: content);
                Text(option.label, 35, y, 1000, 60, 26, Color.white, parent: content);
                UI.Text keyLabel = null;
                var button = Button(option.action.GetBindingDisplayString(option.index), 1314, y + 7, 342, 46, () =>
                {
                    if (bindings.IsRebinding) return;
                    message.text = "Pulsa la nueva tecla o botón. Esc cancela.";
                    keyLabel.text = "ESPERANDO…";
                    EventSystem.current?.SetSelectedGameObject(null);
                    bindings.StartRebind(option.action, option.index, () =>
                    {
                        ignoreEscapeUntil = Time.unscaledTime + .15f;
                        RefreshPlayerBindings();
                        if (keyLabel) keyLabel.text = option.action.GetBindingDisplayString(option.index);
                        if (message) message.text = "Asignación guardada.";
                    }, pcOnly: true);
                }, 29, ControlColor, parent: content);
                keyLabel = button.GetComponentInChildren<UI.Text>();
                keyLabel.font = bodyFont;
            }
        }

        static void RefreshPlayerBindings()
        {
            foreach (var player in LizardPlayer.All)
                if (player && player.Input) player.Input.ReloadBindings();
        }
    }
}
