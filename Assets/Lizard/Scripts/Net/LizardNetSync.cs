using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Jugador en red (Photon Fusion, modo Shared): cada cliente tiene autoridad sobre su propio lagarto.
    /// Sincroniza nombre y forma; la posición la sincroniza el NetworkTransform del mismo objeto.
    /// En las copias remotas desactiva el control y la física local para que solo sigan lo que llega por red.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public partial class LizardNetSync : NetworkBehaviour
    {
        public static readonly List<LizardNetSync> All = new();
        /// <summary>Nombre que pondrá el jugador local al aparecer (lo rellena la pantalla de sala).</summary>
        public static string PendingLocalName = "Jugador";

        [Networked] public NetworkString<_32> PlayerName { get; set; }
        [Networked] public int FormNet { get; set; }
        [Networked] public float FoodNet { get; set; }
        // Lengua: estado, punta, longitud de cuerda, tensión, carga de catapulta y compañero sujetado.
        [Networked] public int TongueStateNet { get; set; }
        [Networked] public Vector3 TongueTipNet { get; set; }
        [Networked] public float TongueLengthNet { get; set; }
        [Networked] public float TongueTensionNet { get; set; }
        [Networked] public float TongueChargeNet { get; set; }
        [Networked] public NetworkId TonguePartnerNet { get; set; }
        // Movimiento para animar las copias: velocidad real y suelo/nado/correr/deslizar (bits 0–3).
        [Networked] public Vector3 VelocityNet { get; set; }
        [Networked] public int MotionFlagsNet { get; set; }
        // Personalización: color de piel (RGBA empaquetado; alfa 0 = sin tinte) y sombrero (índice de SwampHatFactory).
        [Networked] public int SkinNet { get; set; }
        [Networked] public int HatNet { get; set; }
        int appliedSkin = -1, appliedHat = -1;
        SunnyVisual sunny;

        static int PackColor(Color c) { Color32 k = c; return k.r | (k.g << 8) | (k.b << 16) | (255 << 24); }
        static Color UnpackColor(int v) => new Color32((byte)(v & 255), (byte)((v >> 8) & 255), (byte)((v >> 16) & 255), 255);

        LizardPlayer player;
        int appliedForm = -1;
        GUIStyle tagStyle, tagShadow;

        public bool IsLocal => Object && Object.HasStateAuthority;
        public string DisplayName => PlayerName.ToString();

        public override void Spawned()
        {
            player = GetComponent<LizardPlayer>();
            All.Add(this);
            var input = GetComponent<LizardInput>();
            if (HasStateAuthority)
            {
                PlayerName = string.IsNullOrWhiteSpace(PendingLocalName) ? "Jugador" : PendingLocalName.Trim();
                if (input) input.controlled = true;
                // Nuestro lagarto de red lleva el color y el sombrero que elegimos (se guardan en PlayerPrefs).
                if (player)
                {
                    var sv = GetComponentInChildren<SunnyVisual>();
                    if (sv && ColorWheelStation.TryLoad(out var skin)) sv.SetSkinColor(skin);
                    int hat = SwampHatFactory.Load();
                    if (hat > 0 && SwampHatFactory.Current(player) != hat) SwampHatFactory.Equip(player, hat, true);
                }
                var rig = FindAnyObjectByType<LizardCameraRig>();
                if (rig && player) rig.SetTarget(player);
            }
            else
            {
                // Copia de otro jugador: sin control ni física propia.
                if (input) input.controlled = false;
                var rb = GetComponent<Rigidbody>();
                if (rb) { rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.None; }
                var motor = GetComponent<LizardMotor>();
                if (motor) motor.enabled = false;
                var lip = GetComponent<LizardLipSync>();
                if (lip) lip.enabled = false;
            }
            if (player) player.displayName = DisplayName;
            if (player && player.Tongue)
            {
                player.Tongue.NetMirror = !HasStateAuthority;
                player.Tongue.NetLaunchPartner = (target, v) => { var ns = target.GetComponent<LizardNetSync>(); if (ns && ns.Object) ns.RPC_Launch(v); };
                player.Tongue.NetRequestRelease = () => RPC_RequestRelease();
            }
            if(HasStateAuthority)RPC_RequestFoodState();
        }

        static LizardPlayer PlayerFromId(NetworkRunner runner, NetworkId id)
        {
            if (!id.IsValid || runner == null) return null;
            var no = runner.FindObject(id);
            return no ? no.GetComponent<LizardPlayer>() : null;
        }

        /// <summary>El compañero sujetado pide soltarse: lo ejecuta el dueño de la lengua.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestRelease()
        {
            if (player && player.Tongue) player.Tongue.Release(false);
        }

        /// <summary>Catapulta desde otro cliente: el dueño de este cuerpo aplica la velocidad.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_Launch(Vector3 velocity)
        {
            if (!player) return;
            if (player.Tongue && player.Tongue.State == TongueState.Attached) player.Tongue.Release(false);
            player.Body.linearVelocity = velocity;
            player.Motor.MarkTugged(0.8f);
            player.Message("¡Te han catapultado!");
        }

        /// <summary>Otro jugador nos alimenta desde su estómago: la comida entra en nuestro cuerpo real (no en su copia).</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_ReceiveFood(float amount, string fromName)
        {
            if (!player || amount <= 0f) return;
            player.ReceiveFood(Mathf.Min(amount, player.Config.maxFood), fromName);
        }

        /// <summary>Otro jugador nos da la comida que sujetaba con la lengua: la pedimos al maestro para tragarla.</summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_FeedHeld(string foodKey, string fromName)
        {
            if (!player) return;
            var sync = SharedFoodSync.Find(foodKey);
            if (sync && player.TryEat(sync.GetComponent<FoodItem>())) player.Message($"{fromName} te ha dado comida");
        }

        /// <summary>
        /// Si un jugador remoto nos sujeta con la lengua, el tirón lo resolvemos aquí, sobre nuestro propio cuerpo
        /// (así cada cuerpo tiene una sola autoridad y no se aplican fuerzas duplicadas).
        /// </summary>
        void ApplyRemoteTugs()
        {
            if (!player || !Object) return;
            var cfg = player.Config; var rb = player.Body;
            foreach (var other in All)
            {
                if (other == this || !other.Object || other.HasStateAuthority) continue;
                if (other.TongueStateNet != (int)TongueState.Attached || other.TonguePartnerNet != Object.Id || !other.player) continue;
                Vector3 mouth = other.player.MouthPosition;
                Vector3 delta = mouth - player.CenterPosition;
                float dist = delta.magnitude;
                float stretch = dist - other.TongueLengthNet;
                if (stretch <= 0f || dist < 0.001f) continue;   // floja: no empuja
                Vector3 dir = delta / dist;
                float relVel = Vector3.Dot(rb.linearVelocity, -dir);
                float accel = Mathf.Clamp(cfg.tongueSpring * stretch - cfg.tongueDamper * relVel, 0f, other.player.Stats.tongueMaxForce / rb.mass);
                rb.AddForce(dir * accel, ForceMode.Acceleration);
                rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, cfg.tongueMaxBodySpeed);
                player.Motor.MarkTugged();
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            All.Remove(this);
            // Si se desconecta alguien, nadie se queda enganchado a él.
            foreach (var other in All)
                if (other.HasStateAuthority && other.player && other.player.Tongue && other.player.Tongue.PartnerTarget == player)
                    other.player.Tongue.Release(false);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || !player) return;
            FormNet = (int)player.Form;
            FoodNet = player.Stomach.Food;
            if (!sunny) sunny = GetComponentInChildren<SunnyVisual>();
            SkinNet = sunny && sunny.HasSkinTint ? PackColor(sunny.SkinColor) : 0;
            HatNet = SwampHatFactory.Current(player);
            var m = player.Motor;
            if (m)
            {
                VelocityNet = player.Body.linearVelocity;
                MotionFlagsNet = (m.Grounded ? 1 : 0) | (m.Swimming ? 2 : 0) | (m.Sprinting ? 4 : 0) | (m.Sliding ? 8 : 0);
            }
            var t = player.Tongue;
            if (t)
            {
                TongueStateNet = (int)t.State;
                TongueTipNet = t.TipPosition;
                TongueLengthNet = t.CurrentLength;
                TongueTensionNet = t.Tension01;
                TongueChargeNet = t.CatapultCharge;
                var sharedFood=t.HeldFood?t.HeldFood.GetComponent<SharedFoodSync>():null;
                TongueFoodNet=sharedFood?sharedFood.id:"";
                var held = t.PartnerTarget ? t.PartnerTarget.GetComponent<NetworkObject>() : null;
                TonguePartnerNet = held && t.State == TongueState.Attached ? held.Id : default;
            }
            ApplyRemoteTugs();
        }

        public override void Render()
        {
            if (!player) return;
            player.displayName = DisplayName;
            if (HasStateAuthority) return;
            if (appliedForm != FormNet)
            {
                appliedForm = FormNet;
                player.Forms.ApplyImmediate((LizardForm)FormNet);
                player.Visual?.OnFormChanged(player.Form, (LizardForm)FormNet, true);
            }
            player.Stomach.DebugSetFood(FoodNet);
            // Color y sombrero que eligió el otro jugador (fuente, ruleta, probador).
            if (!sunny) sunny = GetComponentInChildren<SunnyVisual>();
            if (sunny && appliedSkin != SkinNet)
            {
                appliedSkin = SkinNet;
                if (SkinNet == 0) sunny.ClearSkinColor(); else sunny.SetSkinColor(UnpackColor(SkinNet));
            }
            if (appliedHat != HatNet)
            {
                appliedHat = HatNet;
                SwampHatFactory.Equip(player, HatNet, true);
            }
            int mf = MotionFlagsNet;
            player.Motor.SetNetState(VelocityNet, (mf & 1) != 0, (mf & 2) != 0, (mf & 4) != 0, (mf & 8) != 0);
            if (player.Tongue)
                player.Tongue.SetFromNetwork((TongueState)TongueStateNet, TongueTipNet, TongueLengthNet, TongueTensionNet, TongueChargeNet,
                    PlayerFromId(Runner, TonguePartnerNet));
        }

        void OnGUI()
        {
            if (!Object || !player || !PcGameSettings.PlayerNames || PcFrontEnd.GameplayMenuOpen) return;
            var cam = Camera.main;
            if (!cam) return;
            float h = (player.Form == LizardForm.Giant ? 3.2f : player.Form == LizardForm.Small ? 0.9f : 1.45f);
            Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * h);
            if (sp.z <= 0f) return;
            tagStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            tagShadow ??= new GUIStyle(tagStyle) { normal = { textColor = new Color(0f, 0f, 0f, 0.75f) } };
            string text = DisplayName + (HasStateAuthority ? " (tú)" : "");
            var r = new Rect(sp.x - 120f, Screen.height - sp.y - 14f, 240f, 28f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, tagShadow);
            if (HasStateAuthority) tagStyle.normal.textColor = new Color(1f, 0.75f, 0.35f);
            else tagStyle.normal.textColor = Color.white;
            GUI.Label(r, text, tagStyle);
        }
    }
}
