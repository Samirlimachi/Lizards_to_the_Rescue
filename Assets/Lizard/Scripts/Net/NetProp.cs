using Fusion;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Objeto físico compartido (cajas, contrapesos, juguetes…). Lo añade <see cref="LevelNet"/> a los Rigidbody de la escena.
    ///
    /// · Un solo cliente simula la física de cada objeto (el "dueño"); al empezar es el cliente maestro.
    /// · Quien lo toca con su lagarto o lo agarra con la lengua pasa a ser el dueño (lo anuncia por RPC con un número
    ///   de turno; si dos lo piden a la vez gana el de menor id de jugador). Así el empujón se siente sin retraso.
    /// · El dueño manda la posición ~10 veces por segundo; los demás lo tienen cinemático y lo siguen suavemente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetProp : MonoBehaviour
    {
        const float SendInterval = 0.1f, Heartbeat = 1f, ClaimLock = 0.6f;

        public int Index { get; set; } = -1;
        public Rigidbody Body { get; private set; }
        public int Seq { get; private set; }
        /// <summary>Dueño actual. None = el cliente maestro.</summary>
        public PlayerRef Owner { get; private set; } = PlayerRef.None;

        bool authoredKinematic;
        bool hasTarget, wasReplica;
        Vector3 targetPos, targetVel, lastSentPos;
        Quaternion targetRot, lastSentRot;
        float targetTime, nextSend, lastForeignClaim = -10f, lastSendTime;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            authoredKinematic = Body.isKinematic;
            motor = GetComponent<LizardMotor>();   // NPC (Pip): su motor solo corre donde se simula
        }
        LizardMotor motor;

        static NetworkRunner Runner => LevelNet.Local ? LevelNet.Local.Runner : null;

        /// <summary>¿Simulo yo la física de este objeto?</summary>
        public bool IsMine
        {
            get
            {
                var r = Runner; if (!r) return true;
                if (Owner == PlayerRef.None || !OwnerPresent(r)) return r.IsSharedModeMasterClient;
                return Owner == r.LocalPlayer;
            }
        }
        bool OwnerPresent(NetworkRunner r) { foreach (var pl in r.ActivePlayers) if (pl == Owner) return true; return false; }
        /// <summary>Quién simula de verdad: el dueño anunciado, o el maestro si no hay o se fue.</summary>
        PlayerRef EffectiveOwner(NetworkRunner r) => Owner == PlayerRef.None || !OwnerPresent(r) ? r.GetMasterClient() : Owner;

        /// <summary>El cuerpo es dinámico "de verdad" aunque aquí esté cinemático por ser copia.</summary>
        public static bool IsNetDynamic(Rigidbody b) { var np = b ? b.GetComponent<NetProp>() : null; return np && !np.authoredKinematic; }

        /// <summary>El jugador local quiere mover este objeto: pide ser el dueño.</summary>
        public static void ClaimLocal(Rigidbody b) { var np = b ? b.GetComponent<NetProp>() : null; if (np) np.TryClaim(); }

        public void TryClaim()
        {
            var r = Runner; var local = LevelNet.Local;
            if (!r || !local || Index < 0 || IsMine) return;
            if (Time.time - lastForeignClaim < ClaimLock) return;   // otro lo acaba de coger: evita el ping-pong
            Seq++; Owner = r.LocalPlayer;
            BecomeOwner();
            local.RPC_PropClaim(Index, Seq);
        }

        /// <summary>Llega un anuncio de dueño (de nosotros mismos o de otro).</summary>
        public void ApplyClaim(PlayerRef claimer, int seq)
        {
            var r = Runner; if (!r) return;
            if (seq < Seq) return;
            if (seq == Seq && Owner != PlayerRef.None && claimer != Owner && claimer.PlayerId > Owner.PlayerId) return;
            bool wasMine = IsMine;
            Seq = seq; Owner = claimer;
            if (claimer != r.LocalPlayer) lastForeignClaim = Time.time;
            if (IsMine && !wasMine) BecomeOwner();
        }

        /// <summary>Pose mandada por el dueño.</summary>
        public void ApplyPose(PlayerRef sender, int seq, Vector3 p, Quaternion q, Vector3 v)
        {
            if (seq < Seq) return;
            if (seq > Seq) ApplyClaim(sender, seq);   // el anuncio aún no llegó (o entramos tarde)
            var r = Runner; if (!r || IsMine || sender != EffectiveOwner(r)) return;
            targetPos = p; targetRot = q; targetVel = v; targetTime = Time.time;
            if (!hasTarget) { Body.position = p; Body.rotation = q; transform.SetPositionAndRotation(p, q); }
            hasTarget = true;
        }

        void BecomeOwner()
        {
            if (!wasReplica) return;
            wasReplica = false;
            Body.isKinematic = authoredKinematic;
            if (motor) motor.enabled = true;
            if (!Body.isKinematic) { Body.linearVelocity = hasTarget ? targetVel : Vector3.zero; Body.WakeUp(); }
        }

        void FixedUpdate()
        {
            var r = Runner; var local = LevelNet.Local;
            if (!r || !local || Index < 0)
            {
                if (wasReplica) { wasReplica = false; Body.isKinematic = authoredKinematic; if (motor) motor.enabled = true; }
                return;
            }
            if (!IsMine)
            {
                if (!wasReplica) { wasReplica = true; Body.isKinematic = true; if (motor) motor.enabled = false; }
                if (!hasTarget) return;
                float age = Mathf.Min(Time.time - targetTime, 0.25f);
                Vector3 goal = targetPos + targetVel * age;
                Body.MovePosition(Vector3.Lerp(Body.position, goal, 0.35f));
                Body.MoveRotation(Quaternion.Slerp(Body.rotation, targetRot, 0.35f));
                return;
            }
            if (wasReplica) BecomeOwner();
            if (Time.time < nextSend) return;
            bool moved = Vector3.Distance(lastSentPos, Body.position) > 0.01f || Quaternion.Angle(lastSentRot, Body.rotation) > 1.5f;
            if (!moved && Time.time - lastSendTime < Heartbeat) return;
            nextSend = Time.time + SendInterval; lastSendTime = Time.time;
            lastSentPos = Body.position; lastSentRot = Body.rotation;
            local.RPC_PropPose(Index, Seq, lastSentPos, lastSentRot, Body.isKinematic ? Vector3.zero : Body.linearVelocity);
        }

        /// <summary>Tocarlo con el lagarto local (empujar, subirse, chocar de gigante) lo hace nuestro.</summary>
        void OnCollisionStay(Collision c) => TouchedBy(c.rigidbody);
        void OnCollisionEnter(Collision c) => TouchedBy(c.rigidbody);
        void TouchedBy(Rigidbody other)
        {
            if (!other || IsMine) return;
            var local = LevelNet.Local;
            if (local && local.Player && other == local.Player.Body) { TryClaim(); return; }
            // Una caja nuestra en movimiento empuja a esta: también la cogemos (cadena de cajas).
            var np = other.GetComponent<NetProp>();
            if (np && np.IsMine && other.linearVelocity.sqrMagnitude > 0.25f) TryClaim();
        }
    }
}
