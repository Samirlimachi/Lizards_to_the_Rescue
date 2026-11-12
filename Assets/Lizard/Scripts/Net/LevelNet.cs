using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lizards
{
    /// <summary>
    /// Multijugador de los niveles (Photon Fusion, modo Shared). Se crea solo en cada escena.
    ///
    /// · Viaje en grupo: con sala abierta, cambiar de escena (balsa, regreso) lo hace el cliente maestro con
    ///   runner.LoadScene, así todos llegan juntos al mismo nivel.
    /// · Al llegar, cada jugador vuelve a aparecer con su lagarto de red (Fusion destruye los del nivel anterior).
    /// · El maestro manda la simulación del nivel: IA de enemigos, cuota del bol, cajas y balsa. Los demás reciben
    ///   posiciones y eventos por RPC (en los objetos de red de los jugadores, como la comida compartida).
    /// · Si el maestro se va, otro cliente pasa a ser maestro y retoma la IA desde donde estaba.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class LevelNet : MonoBehaviour
    {
        public static LevelNet Instance { get; private set; }
        /// <summary>Prefab de red del jugador (lo deja la sala al conectar).</summary>
        public static NetworkObject PlayerPrefab;
        public static bool Connecting;

        public static NetworkRunner Runner { get { var r = FindAnyObjectByType<NetworkRunner>(); return r && r.IsRunning ? r : null; } }
        /// <summary>Hay sala abierta (aunque el jugador local todavía no haya reaparecido).</summary>
        public static bool InSession => Runner;
        public static bool Online => SharedFoodSync.Online;
        public static bool Master => SharedFoodSync.Master;
        public static LizardNetSync Local => SharedFoodSync.Local;

        // Registro determinista: el mismo orden en todos los clientes (misma escena, mismas posiciones iniciales).
        readonly List<EnemyHealth> enemies = new();
        readonly List<ExpeditionSupplyCrate> crates = new();
        readonly HashSet<int> deadEnemies = new();
        bool registered;
        float nextPose, nextState;
        Coroutine spawning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }
        static void OnSceneLoaded(Scene s, LoadSceneMode m) => Ensure();
        static void Ensure() { if (!FindAnyObjectByType<LevelNet>()) new GameObject("Red del nivel").AddComponent<LevelNet>(); }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; EnemyHealth.RemoteDamage = null; }

        void Start()
        {
            // Todos los Awake/Start de la escena ya corrieron: se puede registrar en orden estable.
            foreach (var e in FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include))
                if (e.GetComponent<SlimeEnemy>() || e.GetComponent<PatrolEnemy>()) enemies.Add(e);
            enemies.Sort((a, b) => Key(a.transform.position).CompareTo(Key(b.transform.position)));
            crates.AddRange(FindObjectsByType<ExpeditionSupplyCrate>(FindObjectsInactive.Include));
            crates.Sort((a, b) => Key(a.transform.position).CompareTo(Key(b.transform.position)));
            for (int i = 0; i < enemies.Count; i++)
            {
                int index = i; var e = enemies[i];
                e.OnDied += from => { if (Master) Local.RPC_EnemyDied(index, from); };
            }
            for (int i = 0; i < crates.Count; i++) { int index = i; crates[i].OnOpened += () => { if (Online && !applyingRemote) Local.RPC_CrateOpened(index); }; }
            registered = true;
            EnemyHealth.RemoteDamage = ForwardDamage;
        }
        static double Key(Vector3 p) => System.Math.Round(p.x, 2) * 100000.0 + System.Math.Round(p.z, 2) + System.Math.Round(p.y, 2) * 0.001;

        void Update()
        {
            var runner = Runner;
            if (runner && spawning == null && !Connecting && !runner.IsSceneManagerBusy && !LocalPlayerAlive(runner)) spawning = StartCoroutine(SpawnLocalPlayer(runner));
            if (!registered) return;
            bool replica = Online && !Master;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i]; if (!e) continue;
                var slime = e.GetComponent<SlimeEnemy>(); if (slime) slime.SetReplica(replica);
                var patrol = e.GetComponent<PatrolEnemy>(); if (patrol) SetPatrolReplica(patrol, replica);
            }
            if (!Online || !Master) return;
            if (Time.time >= nextPose)
            {
                nextPose = Time.time + 0.1f;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i]; if (!e || e.Dead) continue;
                    var slime = e.GetComponent<SlimeEnemy>();
                    Local.RPC_EnemyPose(i, e.transform.position, e.transform.eulerAngles.y, slime ? slime.NetState : -1, slime ? slime.NetFrame : 0f, slime && slime.NetHopToPlayer);
                }
            }
            if (Time.time >= nextState)
            {
                nextState = Time.time + 0.5f;
                var exp = FindAnyObjectByType<LevelTwoExpedition>();
                if (exp) Local.RPC_ExpeditionState(exp.Day, exp.RequiredFood, exp.Departing, exp.DepartRemaining, FindAnyObjectByType<FoodGoal>()?.Delivered ?? 0f);
            }
        }

        // ------------------------------------------------------------------ jugador local
        static bool LocalPlayerAlive(NetworkRunner runner) => LizardNetSync.All.Any(n => n && n.Object && n.Object.IsValid && n.HasStateAuthority);

        IEnumerator SpawnLocalPlayer(NetworkRunner runner)
        {
            yield return null; // deja terminar el Start de la escena
            while (runner && runner.IsRunning && (runner.IsSceneManagerBusy || !runner.LocalPlayer.IsRealPlayer)) yield return null;
            if (!runner || !runner.IsRunning || !PlayerPrefab || LocalPlayerAlive(runner)) { spawning = null; yield break; }
            // Aparece donde estaba el lagarto local de la escena (la balsa en LEVEL2, el embarcadero en el refugio…).
            var solo = FindObjectsByType<LizardPlayer>(FindObjectsInactive.Exclude).FirstOrDefault(p => !p.GetComponent<NetworkObject>());
            Vector3 point = solo ? solo.transform.position : Vector3.up * 0.5f;
            var lobby = FindAnyObjectByType<LizardLobby>(); if (lobby && !solo) point = lobby.spawnPoint;
            var local = runner.LocalPlayer;
            Vector3 offset = new Vector3((local.PlayerId % 4) * 1.2f - 1.8f, 0.3f, ((local.PlayerId / 4) % 2) * 1.2f);
            var spawned = runner.Spawn(PlayerPrefab, point + offset, Quaternion.identity, local);
            float deadline = Time.realtimeSinceStartup + 6f;
            while (spawned && !spawned.IsValid && Time.realtimeSinceStartup < deadline) yield return null;
            if (spawned && spawned.IsValid) runner.SetPlayerObject(local, spawned);
            foreach (var p in FindObjectsByType<LizardPlayer>(FindObjectsInactive.Exclude))
                if (!p.GetComponent<NetworkObject>()) p.gameObject.SetActive(false);
            spawning = null;
        }

        // ------------------------------------------------------------------ viaje en grupo
        /// <summary>Con sala abierta, cambia de escena para todos. Devuelve false si no hay sala (viaje normal).</summary>
        public static bool TryTravel(string scene)
        {
            var runner = Runner; if (!runner) return false;
            int index = SceneUtility.GetBuildIndexByScenePath(ScenePath(scene));
            if (index < 0) return false;
            if (runner.IsSharedModeMasterClient) runner.LoadScene(SceneRef.FromIndex(index), LoadSceneMode.Single);
            else if (Local) Local.RPC_RequestTravel(index);
            else return false;
            return true;
        }
        static string ScenePath(string scene)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == scene) return path;
            }
            return scene;
        }
        public static async Task Disconnect()
        {
            var runner = FindAnyObjectByType<NetworkRunner>();
            if (runner && runner.IsRunning) await runner.Shutdown();
            if (runner) Destroy(runner.gameObject);
        }

        // ------------------------------------------------------------------ enemigos
        public EnemyHealth Enemy(int i) => i >= 0 && i < enemies.Count ? enemies[i] : null;
        public ExpeditionSupplyCrate Crate(int i) => i >= 0 && i < crates.Count ? crates[i] : null;
        public int IndexOf(EnemyHealth e) => enemies.IndexOf(e);

        bool ForwardDamage(EnemyHealth e, float amount, Vector3 from)
        {
            if (!Online || Master) return false;
            int i = IndexOf(e); if (i < 0) return false;
            Local.RPC_EnemyDamage(i, amount, from);
            return true;
        }

        static void SetPatrolReplica(PatrolEnemy p, bool replica)
        {
            if (p.enabled == !replica) return;
            p.enabled = !replica;
            var rb = p.GetComponent<Rigidbody>(); if (rb) rb.isKinematic = true;
        }

        public void ApplyPose(int i, Vector3 pos, float yaw, int state, float frame, bool toPlayer)
        {
            var e = Enemy(i); if (!e || e.Dead || deadEnemies.Contains(i)) return;
            var slime = e.GetComponent<SlimeEnemy>();
            if (slime) { slime.NetApply(pos, yaw, state, frame, toPlayer); return; }
            var mirror = e.GetComponent<NetPoseFollower>() ?? e.gameObject.AddComponent<NetPoseFollower>();
            mirror.Target(pos, Quaternion.Euler(0, yaw, 0));
        }

        public void ApplyDeath(int i, Vector3 from)
        {
            if (!deadEnemies.Add(i)) return;
            var e = Enemy(i); if (!e || Master) return;
            var slime = e.GetComponent<SlimeEnemy>();
            if (slime) slime.NetDie(from);
            else { Fx.Burst(e.transform.position, new Color(0.6f, 0.2f, 0.7f), 20, 4f, 0.15f); Sfx.Play(Sfx.Break, e.transform.position); Destroy(e.gameObject); }
        }

        bool applyingRemote;
        public void ApplyCrateOpened(int i)
        {
            var c = Crate(i); if (!c || c.Opened) return;
            applyingRemote = true; c.Open(); applyingRemote = false;
        }

        /// <summary>Estado completo para quien entra a mitad de nivel.</summary>
        public void SendFullState()
        {
            if (!Online || !Master) return;
            for (int i = 0; i < crates.Count; i++) if (crates[i] && crates[i].Opened) Local.RPC_CrateOpened(i);
            foreach (int d in deadEnemies) Local.RPC_EnemyDied(d, Vector3.zero);
            for (int i = 0; i < enemies.Count; i++) if (!enemies[i] && !deadEnemies.Contains(i)) Local.RPC_EnemyDied(i, Vector3.zero);
            nextPose = nextState = 0f;
        }
        public void MarkDead(int i) => deadEnemies.Add(i);
    }

    /// <summary>Copia remota de un enemigo simple (babosa): sigue suavemente la posición que manda el maestro.</summary>
    public sealed class NetPoseFollower : MonoBehaviour
    {
        Vector3 pos; Quaternion rot; bool has;
        public void Target(Vector3 p, Quaternion r) { pos = p; rot = r; if (!has) { transform.SetPositionAndRotation(p, r); has = true; } }
        void Update()
        {
            if (!has) return;
            var patrol = GetComponent<PatrolEnemy>(); if (patrol && patrol.enabled) { has = false; return; }
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, 1f - Mathf.Exp(-12f * Time.deltaTime)), Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-12f * Time.deltaTime)));
        }
    }
}
