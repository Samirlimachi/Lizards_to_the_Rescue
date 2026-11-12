using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    /// <summary>
    /// Acciones reasignables (Input System). Las reasignaciones se guardan en PlayerPrefs.
    /// Un jugador sin control (compañero de prueba) devuelve siempre valores neutros.
    /// </summary>
    public class LizardInput : MonoBehaviour
    {
        const string PrefsKey = "Lizards.BindingOverrides";

        public bool controlled = true;

        public InputActionAsset Asset { get; private set; }
        public InputAction Move, Look, LookStick, Jump, Tongue, Reel, ReelScroll, Launch, Sprint, Interact, Eject, Acid, Help, Slide;
        public IReadOnlyList<InputAction> Rebindable { get; private set; }

        InputActionRebindingExtensions.RebindingOperation rebindOp;

        [Serializable]
        sealed class BindingProfile
        {
            public int version;
            public List<SavedBinding> bindings = new();
        }

        [Serializable]
        sealed class SavedBinding
        {
            public string action, path, processors, interactions;
            public int index;
        }

        void Awake()
        {
            Build();
            ReloadBindings();
        }

        public void ReloadBindings()
        {
            if (!Asset) return;
            foreach (var map in Asset.actionMaps) map.RemoveAllBindingOverrides();
            try
            {
                string json = PlayerPrefs.GetString(PrefsKey, "");
                if (string.IsNullOrEmpty(json)) return;
                var profile = JsonUtility.FromJson<BindingProfile>(json);
                if (profile?.bindings == null) return;
                if (profile.version == 0)
                {
                    LoadLegacyBindings(profile);
                    return;
                }
                foreach (var saved in profile.bindings)
                {
                    var action = Asset.FindAction(saved.action);
                    if (action != null && saved.index >= 0 && saved.index < action.bindings.Count)
                        ApplySavedBinding(action, saved.index, saved);
                }
            }
            catch (Exception e) { Debug.LogWarning("No se pudieron cargar las reasignaciones: " + e.Message); }
        }

        void LoadLegacyBindings(BindingProfile profile)
        {
            foreach (var saved in profile.bindings)
            {
                var action = Asset.FindAction(saved.action);
                if (action == null || string.IsNullOrEmpty(saved.path)) continue;
                int layoutEnd = saved.path.IndexOf('>');
                if (layoutEnd < 0) continue;
                string device = saved.path.Substring(0, layoutEnd + 1);
                int match = -1;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (!action.bindings[i].path.StartsWith(device, StringComparison.OrdinalIgnoreCase)) continue;
                    if (match >= 0) { match = -1; break; }
                    match = i;
                }
                if (match >= 0) ApplySavedBinding(action, match, saved);
            }
        }

        static void ApplySavedBinding(InputAction action, int index, SavedBinding saved)
        {
            action.ApplyBindingOverride(index, new InputBinding
            {
                overridePath = saved.path,
                overrideProcessors = saved.processors,
                overrideInteractions = saved.interactions,
            });
        }

        void SaveBindings()
        {
            // Each player builds its own asset, so Unity's generated binding GUIDs differ.
            var profile = new BindingProfile { version = 1 };
            foreach (var map in Asset.actionMaps)
                foreach (var action in map.actions)
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        var binding = action.bindings[i];
                        if (!binding.hasOverrides) continue;
                        profile.bindings.Add(new SavedBinding
                        {
                            action = map.name + "/" + action.name,
                            index = i,
                            path = binding.overridePath,
                            processors = binding.overrideProcessors,
                            interactions = binding.overrideInteractions,
                        });
                    }
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        void OnEnable() { Asset?.Enable(); }
        void OnDisable() { Asset?.Disable(); rebindOp?.Dispose(); rebindOp = null; }
        void OnDestroy() { if (Asset) Destroy(Asset); }

        void Build()
        {
            Asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = Asset.AddActionMap("Lizard");

            Move = map.AddAction("Mover", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");

            Look = map.AddAction("Mirar (ratón)", InputActionType.Value, "<Mouse>/delta");
            LookStick = map.AddAction("Mirar (mando)", InputActionType.Value, "<Gamepad>/rightStick");

            Jump = map.AddAction("Saltar", InputActionType.Button, "<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");

            Tongue = map.AddAction("Lengua", InputActionType.Button, "<Mouse>/leftButton");
            Tongue.AddBinding("<Gamepad>/rightTrigger");

            Reel = map.AddAction("Acortar/Alargar lengua", InputActionType.Value);
            Reel.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/r").With("Positive", "<Keyboard>/t");
            Reel.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/rightShoulder").With("Positive", "<Gamepad>/leftShoulder");
            ReelScroll = map.AddAction("Rueda lengua", InputActionType.Value, "<Mouse>/scroll/y");

            Sprint = map.AddAction("Correr", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");

            Launch = map.AddAction("Lanzar compañero", InputActionType.Button, "<Keyboard>/q");
            Launch.AddBinding("<Gamepad>/dpad/down");

            Interact = map.AddAction("Comer/Interactuar", InputActionType.Button, "<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonWest");

            Eject = map.AddAction("Expulsar/Alimentar", InputActionType.Button, "<Keyboard>/g");
            Eject.AddBinding("<Gamepad>/buttonNorth");

            Acid = map.AddAction("Escupir ácido", InputActionType.Button, "<Mouse>/rightButton");
            Acid.AddBinding("<Gamepad>/leftTrigger");

            Help = map.AddAction("Pedir ayuda", InputActionType.Button, "<Keyboard>/h");
            Help.AddBinding("<Gamepad>/dpad/up");

            Slide = map.AddAction("Deslizarse", InputActionType.Button, "<Keyboard>/c");
            Slide.AddBinding("<Keyboard>/leftCtrl");
            Slide.AddBinding("<Gamepad>/buttonEast");

            Rebindable = new[] { Move, Jump, Tongue, Reel, Launch, Sprint, Interact, Eject, Acid, Help, Slide };
        }

        // ---- Lectura (neutra si no hay control) ----
        bool GameplayInput => controlled && !PcFrontEnd.GameplayMenuOpen;
        public Vector2 MoveValue => GameplayInput ? Vector2.ClampMagnitude(Move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public Vector2 LookMouse => controlled ? Look.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 LookStickValue => controlled ? LookStick.ReadValue<Vector2>() : Vector2.zero;
        public float ReelValue
        {
            get
            {
                if (!GameplayInput) return 0f;
                float scroll = ReelScroll.ReadValue<float>();
                // La rueda devuelve ±120 por muesca en Windows; la normalizamos a "pulsos".
                float s = Mathf.Abs(scroll) > 0.01f ? -Mathf.Sign(scroll) * 4f : 0f;
                return Mathf.Clamp(Reel.ReadValue<float>() + s, -4f, 4f);
            }
        }
        public bool Pressed(InputAction a) => GameplayInput && a.WasPressedThisFrame();
        public bool Held(InputAction a) => GameplayInput && a.IsPressed();
        public bool Released(InputAction a) => GameplayInput && a.WasReleasedThisFrame();

        // ---- Reasignación ----
        public bool IsRebinding => rebindOp != null;

        public void StartRebind(InputAction action, int bindingIndex, Action onDone, bool pcOnly = false)
        {
            if (rebindOp != null) return;
            action.Disable();
            rebindOp = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ => Finish())
                .OnCancel(_ => Finish());
            if (pcOnly) rebindOp.WithControlsExcluding("<Gamepad>").WithControlsExcluding("<Joystick>");
            rebindOp.Start();

            void Finish()
            {
                rebindOp?.Dispose(); rebindOp = null;
                action.Enable();
                SaveBindings();
                onDone?.Invoke();
            }
        }

        public void ResetBindings()
        {
            foreach (var m in Asset.actionMaps) m.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(PrefsKey);
        }

        public string Display(InputAction a) => a.GetBindingDisplayString();
    }
}
