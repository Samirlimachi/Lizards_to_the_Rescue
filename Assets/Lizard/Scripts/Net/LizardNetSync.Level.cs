using Fusion;
using UnityEngine;

namespace Lizards
{
    /// <summary>Mensajes de red de los niveles (ver <see cref="LevelNet"/>). Solo cuentan los que manda el cliente maestro.</summary>
    public partial class LizardNetSync
    {
        bool FromMaster => Object.StateAuthority == Runner.GetMasterClient();

        // ---------------- viaje en grupo ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_RequestTravel(int buildIndex)
        {
            if (!Runner.IsSharedModeMasterClient || buildIndex < 0 || buildIndex >= UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings) return;
            Runner.LoadScene(SceneRef.FromIndex(buildIndex), UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        // ---------------- enemigos ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Unreliable)]
        public void RPC_EnemyPose(int index, Vector3 position, float yaw, int state, float frame, bool toPlayer)
        { if (FromMaster && !Runner.IsSharedModeMasterClient && LevelNet.Instance) LevelNet.Instance.ApplyPose(index, position, yaw, state, frame, toPlayer); }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_EnemyDamage(int index, float amount, Vector3 from)
        {
            if (!Runner.IsSharedModeMasterClient || !LevelNet.Instance) return;
            var e = LevelNet.Instance.Enemy(index); if (e && !e.Dead) e.Damage(Mathf.Clamp(amount, 0f, 50f), from);
        }

        /// <summary>Golpe aplicado por el maestro: los demás muestran la reacción y la vida que le queda.</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_EnemyHit(int index, float amount, Vector3 from, float healthLeft)
        {
            if (!FromMaster || Runner.IsSharedModeMasterClient || !LevelNet.Instance) return;
            var e = LevelNet.Instance.Enemy(index); if (e && !e.Dead) e.NetHit(amount, from, healthLeft);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_EnemyDied(int index, Vector3 from)
        { if (FromMaster && LevelNet.Instance) LevelNet.Instance.ApplyDeath(index, from); }

        /// <summary>El slime aterriza: cada cliente comprueba solo a su propio jugador (el único sobre el que tiene autoridad).</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_SlimeImpact(int index, Vector3 position)
        {
            if (!FromMaster || Runner.IsSharedModeMasterClient || !LevelNet.Instance) return;
            var e = LevelNet.Instance.Enemy(index); var s = e ? e.GetComponent<SlimeEnemy>() : null;
            if (s) s.NetImpact(position);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_SlimeExpel(int index, Vector3 aim, Vector3 forward)
        {
            if (!FromMaster || Runner.IsSharedModeMasterClient || !LevelNet.Instance) return;
            var e = LevelNet.Instance.Enemy(index); var s = e ? e.GetComponent<SlimeEnemy>() : null;
            if (s) s.NetExpel(aim, forward);
        }

        // ---------------- cajas de provisiones ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_CrateOpened(int index)
        { if (!HasStateAuthority && LevelNet.Instance) LevelNet.Instance.ApplyCrateOpened(index); }

        // ---------------- objetos físicos compartidos (cajas, contrapesos) ----------------
        /// <summary>El que envía pasa a simular este objeto (lo ha tocado o agarrado).</summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_PropClaim(int index, int seq)
        { LevelNet.Instance?.Prop(index)?.ApplyClaim(Object.StateAuthority, seq); }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Unreliable)]
        public void RPC_PropPose(int index, int seq, Vector3 position, Quaternion rotation, Vector3 velocity)
        { if (!HasStateAuthority) LevelNet.Instance?.Prop(index)?.ApplyPose(Object.StateAuthority, seq, position, rotation, velocity); }

        // ---------------- placas, tablones y ácido ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_PlateState(int index, bool pressed)
        { if (FromMaster && !Runner.IsSharedModeMasterClient) LevelNet.Instance?.ApplyPlate(index, pressed); }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_Broken(int index, Vector3 impulse)
        { if (!HasStateAuthority) LevelNet.Instance?.ApplyBroken(index, impulse); }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_AcidHit(int index, Vector3 point)
        { if (!HasStateAuthority) LevelNet.Instance?.ApplyAcidHit(index, point); }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_AcidAtLeast(int index, int hits)
        { if (!HasStateAuthority) LevelNet.Instance?.ApplyAcidAtLeast(index, hits); }

        // ---------------- expedición (cuota y balsa) ----------------
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_ExpeditionState(int day, float required, bool departing, float remaining, float delivered)
        {
            if (!FromMaster || Runner.IsSharedModeMasterClient) return;
            var exp = UnityEngine.Object.FindAnyObjectByType<LevelTwoExpedition>(); if (exp) exp.NetApply(day, required, departing, remaining);
            UnityEngine.Object.FindAnyObjectByType<FoodGoal>()?.SynchronizeDelivery(delivered);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_RequestDepart()
        {
            if (!Runner.IsSharedModeMasterClient) return;
            var exp = UnityEngine.Object.FindAnyObjectByType<LevelTwoExpedition>(); if (exp) exp.MasterStartDeparture();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_ExpeditionComplete()
        {
            if (!FromMaster) return;
            var exp = UnityEngine.Object.FindAnyObjectByType<LevelTwoExpedition>(); if (exp) exp.NetComplete();
        }
    }
}
