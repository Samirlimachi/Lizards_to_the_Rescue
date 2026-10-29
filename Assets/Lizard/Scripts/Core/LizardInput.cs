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
        public InputAction Move, Look, LookStick, Jump, Tongue, Reel, ReelScroll, Interact, Eject, Acid, Help;
        public IReadOnlyList<InputAction> Rebindable { get; private set; }

        InputActionRebindingExtensions.RebindingOperation rebindOp;

        void Awake()
        {
            Build();
            try { var json = PlayerPrefs.GetString(PrefsKey, ""); if (!string.IsNullOrEmpty(json)) Asset.LoadBindingOverridesFromJson(json); }
            catch (Exception e) { Debug.LogWarning("No se pudieron cargar las reasignaciones: " + e.Message); }
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
            Reel.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/r").With("Positive", "<Keyboard>/f");
            Reel.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/rightShoulder").With("Positive", "<Gamepad>/leftShoulder");
            ReelScroll = map.AddAction("Rueda lengua", InputActionType.Value, "<Mouse>/scroll/y");

            Interact = map.AddAction("Comer/Interactuar", InputActionType.Button, "<Keyboard>/e");
            Interact.AddBinding("<Gamepad>/buttonWest");

            Eject = map.AddAction("Expulsar/Alimentar", InputActionType.Button, "<Keyboard>/g");
            Eject.AddBinding("<Gamepad>/buttonNorth");

            Acid = map.AddAction("Escupir ácido", InputActionType.Button, "<Mouse>/rightButton");
            Acid.AddBinding("<Gamepad>/leftTrigger");

            Help = map.AddAction("Pedir ayuda", InputActionType.Button, "<Keyboard>/h");
            Help.AddBinding("<Gamepad>/dpad/up");

            Rebindable = new[] { Move, Jump, Tongue, Reel, Interact, Eject, Acid, Help };
        }

        // ---- Lectura (neutra si no hay control) ----
        public Vector2 MoveValue => controlled ? Vector2.ClampMagnitude(Move.ReadValue<Vector2>(), 1f) : Vector2.zero;
        public Vector2 LookMouse => controlled ? Look.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 LookStickValue => controlled ? LookStick.ReadValue<Vector2>() : Vector2.zero;
        public float ReelValue
        {
            get
            {
                if (!controlled) return 0f;
                float scroll = ReelScroll.ReadValue<float>();
                // La rueda devuelve ±120 por muesca en Windows; la normalizamos a "pulsos".
                float s = Mathf.Abs(scroll) > 0.01f ? -Mathf.Sign(scroll) * 4f : 0f;
                return Mathf.Clamp(Reel.ReadValue<float>() + s, -4f, 4f);
            }
        }
        public bool Pressed(InputAction a) => controlled && a.WasPressedThisFrame();
        public bool Held(InputAction a) => controlled && a.IsPressed();
        public bool Released(InputAction a) => controlled && a.WasReleasedThisFrame();

        // ---- Reasignación ----
        public bool IsRebinding => rebindOp != null;

        public void StartRebind(InputAction action, int bindingIndex, Action onDone)
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
            rebindOp.Start();

            void Finish()
            {
                rebindOp?.Dispose(); rebindOp = null;
                action.Enable();
                PlayerPrefs.SetString(PrefsKey, Asset.SaveBindingOverridesAsJson());
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
