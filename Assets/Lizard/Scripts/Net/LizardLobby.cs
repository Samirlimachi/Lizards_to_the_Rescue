using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lizards
{
    /// <summary>
    /// Pantalla de sala: nombre del jugador, código de sala y botón "Crear / Unirse a sala".
    /// El código es el nombre de la sesión de Photon Fusion (modo Shared): si no existe se crea, si existe se entra.
    /// Al conectar, oculta los lagartos locales de la escena y aparece el lagarto en red de este jugador.
    /// </summary>
    public class LizardLobby : MonoBehaviour
    {
        public enum RoomAction { Create, Join, Random, Legacy }
        const string NameKey = "Lizards.PlayerName", RoomKey = "Lizards.RoomCode";

        [Tooltip("Prefab de red del lagarto (con NetworkObject, NetworkTransform y LizardNetSync).")]
        public NetworkObject playerPrefab;
        public int maxPlayers = 4;
        public Vector3 spawnPoint = new Vector3(0f, 0.2f, 2f);
        public bool showOnStart = true;
        public bool OnlineConnected => runner && runner.IsRunning;
        public string SessionCode => OnlineConnected ? runner.SessionInfo.Name : "";
        public void OpenRoomMenu() { Visible = true; }

        public static bool Visible { get; private set; }

        string playerName = "", roomCode = "", status = "";
        bool connecting;
        NetworkRunner runner;
        Texture2D white;
        GUIStyle label, field, button, title, small, buttonGhost;

        void Awake()
        {
            playerName = PlayerPrefs.GetString(NameKey, "");
            roomCode = PlayerPrefs.GetString(RoomKey, "");
            Visible = showOnStart;
        }

        void OnDestroy() => Visible = false;

        void Update()
        {
            if (Visible) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        async void Connect() => status = await ConnectAsync(playerName, roomCode, RoomAction.Legacy);

        public async Task<string> ConnectAsync(string name, string code, RoomAction action)
        {
            if (connecting || OnlineConnected) return "Ya hay una conexión en curso.";
            playerName = (name ?? "").Trim();
            roomCode = (code ?? "").Trim().ToUpperInvariant();
            if (playerName.Length == 0) return "Escribe tu nombre de jugador.";
            if (action != RoomAction.Random && roomCode.Length == 0) return "Escribe el código de la sala.";
            if (!playerPrefab) return "El refugio no tiene asignado el prefab de red.";
            PlayerPrefs.SetString(NameKey, playerName);
            PlayerPrefs.SetString(RoomKey, roomCode);

            connecting = true;
            status = action == RoomAction.Random ? "Buscando una partida pública…" : $"Conectando a la sala {roomCode}…";
            LizardNetSync.PendingLocalName = playerName;

            var go = new GameObject("NetworkRunner");
            runner = go.AddComponent<NetworkRunner>();
            var networkScenes = go.AddComponent<NetworkSceneManagerDefault>();
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex));

            try
            {
                var args = new StartGameArgs
                {
                    GameMode = GameMode.Shared,
                    SessionName = action == RoomAction.Random ? null : roomCode,
                    PlayerCount = maxPlayers,
                    Scene = sceneInfo,
                    SceneManager = networkScenes,
                    EnableClientSessionCreation = action == RoomAction.Create || action == RoomAction.Legacy,
                };
                if (action == RoomAction.Create || action == RoomAction.Random)
                    args.SessionProperties = new Dictionary<string, SessionProperty> { ["lizards_refugio"] = 1 };
                var result = await runner.StartGame(args);
                if (!result.Ok)
                {
                    return result.ShutdownReason switch
                    {
                        ShutdownReason.GameNotFound when action == RoomAction.Random => "No hay partidas públicas disponibles.",
                        ShutdownReason.GameNotFound => "No se encontró una partida con ese código.",
                        ShutdownReason.GameIsFull => "La partida está llena.",
                        ShutdownReason.GameClosed => "La partida ya está cerrada.",
                        _ => $"No se pudo conectar ({result.ShutdownReason})."
                    };
                }

                float deadline = Time.realtimeSinceStartup + 10;
                while (runner && runner.IsRunning && (!runner.LocalPlayer.IsRealPlayer || runner.IsSceneManagerBusy) && Time.realtimeSinceStartup < deadline)
                    await Task.Yield();
                if (!runner || !runner.IsRunning || !runner.LocalPlayer.IsRealPlayer || runner.IsSceneManagerBusy)
                {
                    await DisconnectAsync();
                    return "La sala no terminó de preparar al jugador local.";
                }

                var localPlayer = runner.LocalPlayer;
                Vector3 offset = new Vector3((localPlayer.PlayerId % 4) * 1.5f - 2.25f, 0f, 0f);
                var networkPlayer = runner.Spawn(playerPrefab, spawnPoint + offset, Quaternion.identity, localPlayer);
                var sync = networkPlayer ? networkPlayer.GetComponent<LizardNetSync>() : null;
                deadline = Time.realtimeSinceStartup + 6;
                while (networkPlayer && sync && (!networkPlayer.IsValid || !sync.Player) && Time.realtimeSinceStartup < deadline)
                    await Task.Yield();
                if (!networkPlayer || !networkPlayer.IsValid || !networkPlayer.HasStateAuthority || !sync || !sync.Player)
                {
                    await DisconnectAsync();
                    return "No se pudo crear al jugador en la sala.";
                }
                runner.SetPlayerObject(localPlayer, networkPlayer);
                foreach (var player in FindObjectsByType<LizardPlayer>(FindObjectsInactive.Exclude))
                    if (!player.GetComponent<NetworkObject>()) player.gameObject.SetActive(false);
                Visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return null;
            }
            catch (Exception e)
            {
                if (OnlineConnected) await DisconnectAsync();
                return "Error al conectar. " + e.Message;
            }
            finally
            {
                connecting = false;
                if (!OnlineConnected && go) Destroy(go);
            }
        }

        public async Task DisconnectAsync()
        {
            var current = runner;
            runner = null;
            if (current && current.IsRunning) await current.Shutdown();
            if (current) Destroy(current.gameObject);
        }

        void PlaySolo()
        {
            Visible = false;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        // ---------------- Interfaz ----------------

        void Styles()
        {
            if (label != null) return;
            white = Texture2D.whiteTexture;
            label = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.62f, 0.78f, 0.92f) } };
            title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, wordWrap = true, normal = { textColor = new Color(0.85f, 0.88f, 0.92f) } };
            field = new GUIStyle(GUI.skin.textField) { fontSize = 18, padding = new RectOffset(10, 10, 6, 6), normal = { textColor = Color.white, background = null }, focused = { textColor = Color.white, background = null }, hover = { textColor = Color.white, background = null } };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.16f, 0.1f, 0.06f), background = null }, hover = { textColor = new Color(0.1f, 0.06f, 0.03f), background = null }, active = { textColor = Color.black, background = null } };
            buttonGhost = new GUIStyle(button) { fontSize = 14, normal = { textColor = new Color(0.85f, 0.88f, 0.92f), background = null }, hover = { textColor = Color.white, background = null } };
        }

        void Box(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, white, ScaleMode.StretchToFill, false, 0, c, 0, 10f); GUI.color = Color.white; }
        void Border(Rect r, Color c) { GUI.DrawTexture(r, white, ScaleMode.StretchToFill, false, 0, c, 2f, 8f); }

        void Field(ref float y, float x, float w, string caption, ref string value, int maxLen)
        {
            var panel = new Rect(x, y, w, 74f);
            Box(panel, new Color(0.13f, 0.19f, 0.26f, 0.97f));
            GUI.Label(new Rect(x + 14f, y + 8f, w - 28f, 18f), caption, label);
            var input = new Rect(x + 12f, y + 30f, w - 24f, 34f);
            Box(input, new Color(0.17f, 0.11f, 0.06f, 1f));
            Border(input, new Color(0.95f, 0.55f, 0.2f, 0.9f));
            value = GUI.TextField(input, value, maxLen, field);
            y += 84f;
        }

        void OnGUI()
        {
            if (!Visible) return;
            Styles();
            GUI.color = new Color(0f, 0f, 0f, 0.55f); GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white); GUI.color = Color.white;

            float w = Mathf.Min(420f, Screen.width - 32f), x = (Screen.width - w) / 2f;
            float y = Screen.height / 2f - 190f;
            Box(new Rect(x - 16f, y - 16f, w + 32f, 380f), new Color(0.06f, 0.09f, 0.13f, 0.96f));
            GUI.Label(new Rect(x, y, w, 34f), "Lizards to the Rescue", title);
            y += 46f;

            GUI.enabled = !connecting;
            Field(ref y, x, w, "NOMBRE DEL JUGADOR", ref playerName, 24);
            Field(ref y, x, w, "CÓDIGO DE LA SALA", ref roomCode, 16);

            var btn = new Rect(x, y, w, 46f);
            Box(btn, connecting ? new Color(0.6f, 0.4f, 0.25f) : btn.Contains(Event.current.mousePosition) ? new Color(1f, 0.62f, 0.3f) : new Color(0.96f, 0.55f, 0.24f));
            if (GUI.Button(btn, connecting ? "CONECTANDO…" : "CREAR / UNIRSE A SALA", button)) Connect();
            y += 54f;
            if (GUI.Button(new Rect(x, y, w, 30f), "Jugar solo (sin conexión)", buttonGhost)) PlaySolo();
            GUI.enabled = true;
            y += 34f;
            if (status.Length > 0) GUI.Label(new Rect(x, y, w, 40f), status, small);
        }
    }
}
