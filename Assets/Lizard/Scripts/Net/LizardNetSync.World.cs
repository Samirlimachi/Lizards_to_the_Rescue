using Fusion;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Mensajes de red del mundo: refugio (fuente, campana, balsa, juguetes), reloj compartido de plataformas,
    /// compañeros NPC (Pip en LEVEL1) y final de misión.
    /// </summary>
    public partial class LizardNetSync
    {
        // ---------------- escupitajo de ácido y gestos ----------------
        /// <summary>Otro jugador escupe ácido: aquí vuela una copia que se ve y salpica igual, pero no hace daño doble.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_AcidSpit(Vector3 origin, Vector3 velocity)
        {
            if (HasStateAuthority || !player) return;
            AcidProjectile.Spawn(player, origin, velocity, true);
            player.Visual?.PlaySpit();
            Sfx.Play(Sfx.Acid, origin);
        }

        /// <summary>Gestos de la boca del otro jugador: 0 = tragar, 1 = escupir comida.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Unreliable)]
        public void RPC_Gesture(int kind)
        {
            if (HasStateAuthority || !player) return;
            if (kind == 0) { player.Visual?.PlayGulp(); Sfx.Play(Sfx.Eat, player.MouthPosition, 0.6f); }
            else { player.Visual?.PlayEject(); Sfx.Play(Sfx.Eject, player.MouthPosition, 0.6f); }
        }

        /// <summary>Para el jugador local: avisa a los demás de un gesto de la boca.</summary>
        public static void SendGesture(LizardPlayer p, int kind)
        {
            var n = p ? p.GetComponent<LizardNetSync>() : null;
            if (n && n.Object && n.IsLocal) n.RPC_Gesture(kind);
        }

        // ---------------- reloj compartido (elevadores y plataformas móviles) ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Unreliable)]
        public void RPC_Clock(float masterTime)
        { if (FromMaster && !Runner.IsSharedModeMasterClient) LevelNet.Instance?.ApplyClock(masterTime); }

        // ---------------- fuente de color ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_FountainColor(int index)
        { if (!HasStateAuthority) UnityEngine.Object.FindAnyObjectByType<ColorFountain>()?.NetSet(index); }

        // ---------------- campana de la tienda ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_BellRing()
        { if (!HasStateAuthority) UnityEngine.Object.FindAnyObjectByType<SwampLobbyExpansion>()?.RingBell(null); }

        // ---------------- balsa del refugio ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_LobbyDeparture(bool departing, float remaining, string who)
        { if (!HasStateAuthority) UnityEngine.Object.FindAnyObjectByType<SwampLobbyHub>()?.NetDeparture(departing, remaining, who); }

        // ---------------- juguetes de la expendedora ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_ToySpawn(int item, Vector3 position, Vector3 velocity)
        { if (!HasStateAuthority) SwampPocket.SpawnToy(item, position, velocity, true); }

        // ---------------- compañeros NPC (el maestro manda) ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_NpcState(int index, int form, float food)
        { if (FromMaster && !Runner.IsSharedModeMasterClient) LevelNet.Instance?.ApplyNpcState(index, form, food); }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_FeedNpc(int index, float amount, string fromName)
        {
            if (!Runner.IsSharedModeMasterClient) return;
            var npc = LevelNet.Instance ? LevelNet.Instance.Npc(index) : null;
            if (npc && amount > 0f) npc.ReceiveFood(Mathf.Min(amount, npc.Config.maxFood), fromName);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_FeedNpcHeld(int index, string foodKey)
        {
            if (!Runner.IsSharedModeMasterClient) return;
            var npc = LevelNet.Instance ? LevelNet.Instance.Npc(index) : null;
            var sync = SharedFoodSync.Find(foodKey);
            if (npc && sync) SharedFoodSync.NpcEat(npc, sync.GetComponent<FoodItem>());
        }

        // ---------------- final de misión (LEVEL1) ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_MissionComplete(string who)
        { if (!HasStateAuthority) UnityEngine.Object.FindAnyObjectByType<LevelOneMission>()?.NetComplete(who); }
    }
}
