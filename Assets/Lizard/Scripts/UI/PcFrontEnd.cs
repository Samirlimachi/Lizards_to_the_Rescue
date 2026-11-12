using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UI = UnityEngine.UI;

namespace Lizards
{
    [DefaultExecutionOrder(-100)]
    public sealed partial class PcFrontEnd : MonoBehaviour
    {
        public Texture2D sewerBackground, sunnyPortrait, loadingAction, applePortrait;
        public Font displayFont, bodyFont;
        public UnityEngine.Rendering.Universal.ScriptableRendererFeature ambientOcclusion;

        public static PcFrontEnd Instance { get; private set; }
        public static bool GameplayMenuOpen => Instance && (Instance.gameplayOverlay || Instance.transitioning && Instance.IsGameplayScene());
        public const string MenuScene = "MainMenu";
        const string PlayerNameKey = "Lizards.PlayerName";

        enum Page { None, Home, Create, Join, Random, Credits, Settings, Pause, Tutorial, Loading, Exit }
        static readonly Color Ink = new(.075f, .045f, .105f);
        static readonly Color Purple = new(.42f, .22f, .74f);
        static readonly Color Lime = new(.70f, .97f, .24f);
        static readonly Color Pink = new(1f, .36f, .77f);
        RectTransform canvasRect, stage, panel;
        GameObject eventRoot;
        UI.RawImage backdrop;
        LizardInput bindings;
        Sprite ribbon;
        Page page, settingsFrom;
        bool gameplayOverlay, transitioning;
        bool initialOcclusion;
        float savedTimeScale = 1f;
        float ignoreEscapeUntil;
        string status, lastScene;
        UI.Text loadingLabel;
        RectTransform progressFill, progressFace;
        RectTransform[] flying;

        /// <summary>El nivel actual se abrió desde TUTORIAL en el menú principal: al terminarlo se vuelve al menú.</summary>
        public static bool TutorialRun { get; private set; }

        public static void Travel(string scene)
        {
            // Con sala abierta, los cambios de nivel se hacen para todo el grupo a través de Fusion.
            if (scene != MenuScene && LevelNet.TryTravel(scene)) { if (Instance) Instance.Show(Page.Loading); return; }
            if (Instance) Instance.BeginLoad(scene);
            else SceneManager.LoadScene(scene);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallForDirectPlay()
        {
            if (Instance) return;
            string scene = SceneManager.GetActiveScene().name;
            if (scene != MenuScene && scene != "Lobby" && scene != "LEVEL1" && scene != "LEVEL2") return;
            var prefab = Resources.Load<PcFrontEnd>("PcFrontEnd");
            if (prefab) Instantiate(prefab);
        }

        void Awake()
        {
            if (Instance && Instance != this) { enabled = false; Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            PcGameSettings.Apply();
            if (ambientOcclusion)
            {
                initialOcclusion = ambientOcclusion.isActive;
                ApplyOcclusion();
            }
            if (!bodyFont) bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!displayFont) displayFont = bodyFont;
            ribbon = MakeRibbon();
            bindings = gameObject.AddComponent<LizardInput>();
            bindings.controlled = false;
            var canvas = new GameObject("Interfaz PC", typeof(RectTransform), typeof(Canvas), typeof(UI.CanvasScaler), typeof(UI.GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 400;
            var scaler = canvas.GetComponent<UI.CanvasScaler>();
            scaler.uiScaleMode = UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            canvasRect = canvas.GetComponent<RectTransform>();
            backdrop = canvas.AddComponent<UI.RawImage>();
            stage = NewRect("Área de diseño", canvasRect, 0, 0, 1920, 1080);
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f);
            stage.anchoredPosition = Vector2.zero;
            eventRoot = new GameObject("Entrada de menú", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventRoot.transform.SetParent(transform, false);
            SceneManager.sceneLoaded += OnSceneLoaded;
            lastScene = SceneManager.GetActiveScene().name;
            Show(lastScene == MenuScene ? Page.Home : Page.None);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SetGameplayOverlay(false);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
            if (Application.isEditor && ambientOcclusion) ambientOcclusion.SetActive(initialOcclusion);
            if (ribbon) { Destroy(ribbon.texture); Destroy(ribbon); }
        }

        void ApplyOcclusion()
        {
            if (ambientOcclusion) ambientOcclusion.SetActive(PcGameSettings.Get("Occlusion", initialOcclusion ? 1 : 0) == 1);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            lastScene = scene.name;
            if (!transitioning) Show(scene.name == MenuScene ? Page.Home : Page.None);
            RefreshInputSystem();
        }

        void RefreshInputSystem()
        {
            bool another = FindObjectsByType<EventSystem>().Any(e => e.gameObject != eventRoot && e.isActiveAndEnabled);
            eventRoot.SetActive(!another);
        }

        void Update()
        {
            if (page == Page.Home && flying != null)
            {
                float t = Time.unscaledTime;
                for (int i = 0; i < flying.Length; i++)
                {
                    float phase = t * (.46f + i * .13f) + i * 2.4f;
                    flying[i].anchoredPosition = new Vector2(630 + i * 246 + Mathf.Sin(phase) * 165, -370 - i * 105 + Mathf.Cos(phase * 1.8f) * 150);
                    flying[i].localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase * 1.3f) * 32 + i * 14 - 15);
                }
            }

            var keyboard = Keyboard.current;
            if (keyboard == null || transitioning || !keyboard.escapeKey.wasPressedThisFrame || bindings.IsRebinding || Time.unscaledTime < ignoreEscapeUntil) return;
            if (page == Page.None && IsGameplayScene() && !LizardLobby.Visible) Show(Page.Pause);
            else if (page == Page.Settings) Show(settingsFrom);
            else if (page == Page.Pause || page == Page.Tutorial) Show(Page.None);
            else if (lastScene == MenuScene && page != Page.Home) Show(Page.Home);
        }

        void LateUpdate()
        {
            var size = canvasRect.rect.size;
            stage.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
            if (!backdrop.texture || size.y <= 0) return;
            float aspect = size.x / size.y;
            float imageAspect = (float)backdrop.texture.width / backdrop.texture.height;
            float width = Mathf.Min(1, aspect / imageAspect);
            float height = Mathf.Min(1, imageAspect / aspect);
            backdrop.uvRect = new Rect((1 - width) / 2, (1 - height) / 2, width, height);
        }

        bool IsGameplayScene() => lastScene == "Lobby" || lastScene == "LEVEL1" || lastScene == "LEVEL2";

        void SetGameplayOverlay(bool value)
        {
            if (gameplayOverlay == value) return;
            gameplayOverlay = value;
            if (value)
            {
                savedTimeScale = Time.timeScale;
                if (!LizardNetSync.All.Any(p => p && p.Object)) Time.timeScale = 0;
            }
            else Time.timeScale = savedTimeScale;
        }

        void Show(Page destination)
        {
            page = destination;
            if (destination == Page.Pause) LizardHUD.CloseLegacyMenu();
            SetGameplayOverlay(IsGameplayScene() && destination != Page.None && destination != Page.Loading);
            if (panel) { panel.gameObject.SetActive(false); Destroy(panel.gameObject); }
            stage.gameObject.SetActive(destination != Page.None);
            backdrop.enabled = destination != Page.None;
            backdrop.raycastTarget = destination != Page.None;
            backdrop.texture = destination == Page.Home || destination == Page.Loading || destination == Page.Create || destination == Page.Join || destination == Page.Random || destination == Page.Exit || destination == Page.Credits ? sewerBackground : null;
            backdrop.color = destination == Page.Settings ? new Color(.09f, .09f, .10f) : backdrop.texture ? Color.white : new Color(0, 0, 0, .72f);
            flying = null;
            Cursor.lockState = destination == Page.None ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = destination != Page.None;
            if (destination == Page.None) return;
            panel = NewRect(destination.ToString(), stage, 0, 0, 1920, 1080);
            switch (destination)
            {
                case Page.Home: Home(); break;
                case Page.Settings: Settings(); break;
                case Page.Loading: Loading(); break;
                case Page.Pause: Pause(); break;
                case Page.Tutorial: Tutorial(); break;
                default: Dialog(destination); break;
            }
            var firstButton = panel.GetComponentInChildren<UI.Button>();
            if (firstButton && EventSystem.current) EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
        }

        void Home()
        {
            Shade(0, 0, 1920, 1080, new Color(.10f, .025f, .17f, .25f));
            flying = new RectTransform[3];
            var colors = new[] { Color.white, new Color(.75f, .9f, 1f), new Color(1f, .76f, .82f) };
            for (int i = 0; i < flying.Length; i++)
            {
                int size = 275 - 36 * i;
                var art = Art(sunnyPortrait, 685 + i * 205, 315 + i * 100, size, size);
                art.color = colors[i];
                flying[i] = art.rectTransform;
            }

            Logo(74, 32, 550, 304);
            Heading("EL REFUGIO", 785, 27, 350, 54, 31, Lime, TextAnchor.MiddleCenter);
            string name = PlayerPrefs.GetString(PlayerNameKey, "Sunny");
            Shade(1502, 31, 376, 77, Purple, true);
            Art(sunnyPortrait, 1505, 25, 85, 85);
            Heading(name.ToUpperInvariant(), 1600, 35, 257, 68, 32, Color.white);

            Shade(1510, 158, 360, 300, new Color(.075f, .045f, .105f, .90f));
            Shade(1502, 148, 376, 64, Lime, true);
            Heading("EXPEDICIÓN", 1530, 148, 308, 64, 40, Ink, TextAnchor.MiddleCenter);
            Text("Reúne a tu equipo, explora las alcantarillas y vuelve al refugio con provisiones.", 1540, 226, 300, 170, 27, Color.white, TextAnchor.UpperLeft);

            MenuButton("CREAR PARTIDA", 385, () => OpenDialog(Page.Create), true);
            MenuButton("UNIRSE A PARTIDA", 462, () => OpenDialog(Page.Join), true);
            MenuButton("PARTIDA ALEATORIA", 539, () => OpenDialog(Page.Random), true);
            MenuButton("TUTORIAL", 616, () => BeginLoad("LEVEL1", tutorial: true), true);
            MenuButton("AJUSTES", 699, () => OpenSettings(Page.Home), false);
            MenuButton("CRÉDITOS", 755, () => OpenDialog(Page.Credits), false);
            MenuButton("SALIR", 811, () => OpenDialog(Page.Exit), false);
            if (!string.IsNullOrWhiteSpace(status))
            {
                Shade(75, 939, 1030, 79, Ink);
                Text(status, 100, 946, 980, 63, 25, Lime);
            }
            Text("LIZARDS TO THE RESCUE", 1407, 1012, 456, 32, 19, new Color(.86f, .79f, .91f), TextAnchor.MiddleRight);
        }

        void MenuButton(string label, float y, Action action, bool large)
        {
            var button = Button(label, 80, y, large ? 495 : 248, large ? 68 : 49, action, large ? 48 : 34, Ink, TextAnchor.MiddleLeft);
            var colors = button.colors;
            colors.normalColor = new Color(.20f, .09f, .32f, .64f);
            colors.highlightedColor = Purple;
            colors.selectedColor = Purple;
            colors.pressedColor = new Color(.57f, .30f, .84f);
            button.colors = colors;
        }

        void OpenDialog(Page destination) { status = null; Show(destination); }
        void OpenSettings(Page from) { settingsFrom = from; settingsTab = 0; Show(Page.Settings); }

        void Dialog(Page destination)
        {
            Shade(0, 0, 1920, 1080, new Color(.03f, .015f, .065f, .66f));
            Shade(495, 217, 930, 646, Ink);
            Shade(487, 207, 946, 102, Purple, true);
            string title = destination switch
            {
                Page.Create => "CREAR PARTIDA", Page.Join => "UNIRSE A PARTIDA", Page.Random => "PARTIDA ALEATORIA",
                Page.Credits => "CRÉDITOS", _ => "SALIR DEL JUEGO"
            };
            Heading(title, 520, 214, 880, 84, 62, Color.white, TextAnchor.MiddleCenter);
            if (destination == Page.Credits)
            {
                Logo(660, 326, 600, 240);
                Text("Lizards to the Rescue\nDesarrollo y dirección · Samir\nCreado con Unity y Photon Fusion", 580, 568, 760, 151, 30, Color.white, TextAnchor.MiddleCenter);
            }
            else if (destination == Page.Exit)
            {
                Text("¿Quieres cerrar Lizards to the Rescue?", 563, 390, 794, 135, 37, Color.white, TextAnchor.MiddleCenter);
                Button("SALIR", 1000, 749, 340, 66, () => Application.Quit(), 35, Purple);
            }
            else
            {
                Text("NOMBRE DEL JUGADOR", 574, 349, 770, 36, 23, Lime);
                var name = Field(PlayerPrefs.GetString(PlayerNameKey, "Sunny"), "Tu nombre", 573, 388, 774, 60, 22);
                UI.InputField code = null;
                if (destination == Page.Join)
                {
                    Text("CÓDIGO DE LA PARTIDA", 574, 477, 770, 36, 23, Lime);
                    code = Field("", "Código que te compartió el anfitrión", 573, 515, 774, 60, 24);
                }
                else Text(destination == Page.Create ? "Crea una sala pública y comparte su código desde el menú de pausa." : "Busca una sala pública con espacio disponible.", 574, 483, 774, 93, 27, Color.white);
                var feedback = Text("", 574, 605, 774, 75, 25, Lime);
                var action = destination == Page.Create ? LizardLobby.RoomAction.Create : destination == Page.Join ? LizardLobby.RoomAction.Join : LizardLobby.RoomAction.Random;
                Button(destination == Page.Random ? "BUSCAR" : "ACEPTAR", 1000, 749, 340, 66, () =>
                {
                    string player = name.text.Trim();
                    string room = action == LizardLobby.RoomAction.Create ? Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() : code ? code.text.Trim().ToUpperInvariant() : "";
                    if (player.Length == 0) { feedback.text = "Escribe tu nombre."; return; }
                    if (action == LizardLobby.RoomAction.Join && room.Length == 0) { feedback.text = "Introduce el código de la partida."; return; }
                    BeginLoad("Lobby", new RoomRequest(player, room, action));
                }, 35, Purple);
                if (destination == Page.Create)
                    Button("JUGAR LOCAL", 578, 656, 289, 49, () => BeginLoad("Lobby"), 27, new Color(.25f, .16f, .35f));
            }
            Button("VOLVER", 573, 749, 300, 66, () => Show(Page.Home), 34, new Color(.28f, .22f, .34f));
        }

        void Pause()
        {
            Logo(707, 134, 506, 267);
            Heading("JUGADORES", 110, 110, 420, 65, 43, Color.white);
            Shade(545, 111, 2, 803, new Color(1, 1, 1, .38f));
            Shade(1373, 111, 2, 803, new Color(1, 1, 1, .38f));
            string players = string.Join("\n\n", LizardPlayer.All.Where(p => p).Select(p => p.displayName));
            Text(players, 112, 200, 394, 500, 30, Color.white, TextAnchor.UpperLeft);
            Heading("PARTIDA", 1422, 110, 392, 65, 43, Color.white);
            Text(lastScene == "Lobby" ? "Refugio" : lastScene == "LEVEL1" ? "Estación de entrenamiento" : "Alcantarillas", 1422, 205, 410, 66, 28, Color.white);
            var lobby = FindAnyObjectByType<LizardLobby>();
            string code = lobby ? lobby.SessionCode : LevelNet.Runner ? LevelNet.Runner.SessionInfo.Name : "";
            var codeLabel = Text(code.Length > 0 ? "Sala en línea" : "Partida local", 1422, 277, 410, 58, 28, Lime);
            Button("CONTINUAR", 718, 440, 484, 69, () => Show(Page.None), 42, Purple);
            Button("AJUSTES", 718, 520, 484, 69, () => OpenSettings(Page.Pause), 42, Ink);
            if (code.Length > 0)
                Button("COPIAR CÓDIGO DE SALA", 718, 600, 484, 69, () => { GUIUtility.systemCopyBuffer = code; codeLabel.text = code + " · copiado"; }, 35, Ink);
            Button("VOLVER AL MENÚ PRINCIPAL", 660, 695, 600, 72, () => BeginLoad(MenuScene), 35, Ink);
        }

        void Tutorial()
        {
            Shade(450, 240, 1020, 585, Ink);
            Shade(443, 231, 1034, 89, Purple, true);
            Heading("TUTORIAL", 485, 231, 950, 89, 59, Color.white, TextAnchor.MiddleCenter);
            string Control(InputAction action) => bindings.Display(action);
            string instructions = $"{Control(bindings.Move)}   Moverse\n{Control(bindings.Jump)}   Saltar\n{Control(bindings.Tongue)}   Usar la lengua\n{Control(bindings.Interact)}   Comer o interactuar\n{Control(bindings.Eject)}   Expulsar comida";
            Text(instructions, 535, 359, 850, 304, 32, Color.white, TextAnchor.UpperLeft);
            Button("EMPEZAR", 755, 711, 410, 72, () => Show(Page.None), 43, Purple);
        }

        sealed class RoomRequest
        {
            public readonly string name, code;
            public readonly LizardLobby.RoomAction action;
            public RoomRequest(string name, string code, LizardLobby.RoomAction action)
            { this.name = name; this.code = code; this.action = action; }
        }

        void BeginLoad(string scene, RoomRequest room = null, bool tutorial = false)
        {
            if (transitioning) return;
            TutorialRun = tutorial;
            StartCoroutine(Load(scene, room, tutorial));
        }

        IEnumerator Load(string scene, RoomRequest room, bool tutorial)
        {
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                status = "La escena " + scene + " no está incluida en la compilación.";
                Show(lastScene == MenuScene ? Page.Home : Page.Pause);
                yield break;
            }
            transitioning = true;
            status = null;
            Show(Page.Loading);
            SetProgress(0);
            yield return null;
            if (scene == MenuScene && LevelNet.InSession)
            {
                // Volver al menú cierra la sala (también desde los niveles, donde no hay controlador de salas).
                var disconnect = LevelNet.Disconnect();
                while (!disconnect.IsCompleted) yield return null;
                if (disconnect.IsFaulted) Debug.LogException(disconnect.Exception);
            }
            float shownSince = Time.unscaledTime;
            var operation = SceneManager.LoadSceneAsync(scene);
            operation.allowSceneActivation = false;
            while (operation.progress < .9f || Time.unscaledTime - shownSince < .85f)
            {
                SetProgress(Mathf.Min(operation.progress / .9f, (Time.unscaledTime - shownSince) / .85f) * (room == null ? 1f : .88f));
                yield return null;
            }
            operation.allowSceneActivation = true;
            while (!operation.isDone) yield return null;
            if (room != null)
            {
                loadingLabel.text = room.action == LizardLobby.RoomAction.Random ? "BUSCANDO PARTIDA…" : "CONECTANDO AL REFUGIO…";
                SetProgress(.9f);
                var lobby = FindAnyObjectByType<LizardLobby>();
                if (!lobby) status = "El refugio no tiene controlador de salas.";
                else
                {
                    var task = lobby.ConnectAsync(room.name, room.code, room.action);
                    while (!task.IsCompleted) yield return null;
                    status = task.IsFaulted ? "Falló la conexión con el servicio de partidas." : task.Result;
                }
                if (status != null)
                {
                    var back = SceneManager.LoadSceneAsync(MenuScene);
                    while (!back.isDone) yield return null;
                    scene = MenuScene;
                }
            }
            SetProgress(1);
            yield return null;
            transitioning = false;
            Show(scene == MenuScene ? Page.Home : tutorial ? Page.Tutorial : Page.None);
        }

        void Loading()
        {
            Shade(0, 0, 1920, 1080, new Color(.11f, .02f, .20f, .35f));
            var action = Art(loadingAction ? loadingAction : sunnyPortrait, 67, 51, 1240, 848);
            action.uvRect = new Rect(1, 0, -1, 1);
            action.rectTransform.localRotation = Quaternion.Euler(0, 0, 5);
            Logo(1230, 92, 604, 345);
            loadingLabel = Heading("CARGANDO…", 570, 889, 780, 70, 57, Color.white, TextAnchor.MiddleCenter);
            Shade(105, 984, 1695, 19, new Color(.92f, .89f, .93f));
            progressFill = Shade(105, 982, 1, 23, Pink);
            progressFace = Art(sunnyPortrait, 58, 942, 98, 98).rectTransform;
            Art(applePortrait, 1772, 943, 100, 100);
        }

        void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            progressFill.sizeDelta = new Vector2(Mathf.Max(10, 1695 * value), 23);
            progressFace.anchoredPosition = new Vector2(58 + 1640 * value, -942);
        }

        void Logo(float x, float y, float width, float height)
        {
            var logo = NewRect("Logotipo", panel, x, y, width, height);
            logo.localRotation = Quaternion.Euler(0, 0, 5);
            Art(sunnyPortrait, width * .39f, -height * .08f, width * .22f, height * .41f, logo);
            Heading("LIZARDS", 0, height * .28f, width, height * .44f, Mathf.RoundToInt(width * .218f), Lime, TextAnchor.MiddleCenter, logo, true);
            Heading("TO THE RESCUE", 0, height * .66f, width, height * .32f, Mathf.RoundToInt(width * .135f), new Color(.73f, .41f, 1f), TextAnchor.MiddleCenter, logo, true);
        }

        static RectTransform NewRect(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        RectTransform Shade(float x, float y, float w, float h, Color color, bool angular = false, Transform parent = null)
        {
            var rect = NewRect("Fondo", parent ? parent : panel, x, y, w, h);
            var image = rect.gameObject.AddComponent<UI.Image>();
            image.color = color;
            if (angular) image.sprite = ribbon;
            image.raycastTarget = false;
            return rect;
        }

        UI.RawImage Art(Texture texture, float x, float y, float w, float h, Transform parent = null)
        {
            var rect = NewRect("Ilustración", parent ? parent : panel, x, y, w, h);
            var image = rect.gameObject.AddComponent<UI.RawImage>();
            image.texture = texture;
            image.enabled = texture;
            image.raycastTarget = false;
            return image;
        }

        UI.Text Text(string value, float x, float y, float w, float h, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, Transform parent = null)
        {
            var text = NewRect(value.Length > 30 ? "Texto" : value, parent ? parent : panel, x, y, w, h).gameObject.AddComponent<UI.Text>();
            text.font = bodyFont;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        UI.Text Heading(string value, float x, float y, float w, float h, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, Transform parent = null, bool logo = false)
        {
            var text = Text(value, x, y, w, h, size, color, align, parent);
            text.font = displayFont;
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = size;
            text.resizeTextMinSize = Mathf.Max(12, size / 2);
            var shadow = text.gameObject.AddComponent<UI.Shadow>();
            shadow.effectDistance = logo ? new Vector2(5, -7) : new Vector2(2, -3);
            shadow.effectColor = new Color(.10f, .03f, .17f, .94f);
            if (logo)
            {
                var outline = text.gameObject.AddComponent<UI.Outline>();
                outline.effectDistance = new Vector2(4, 4);
                outline.effectColor = Ink;
            }
            return text;
        }

        UI.Button Button(string label, float x, float y, float w, float h, Action action, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter, Transform parent = null)
        {
            var rect = Shade(x, y, w, h, Color.white, true, parent);
            rect.name = label;
            var image = rect.GetComponent<UI.Image>();
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<UI.Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, new Color(.82f, .53f, 1f), .47f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(color, Color.black, .18f);
            colors.fadeDuration = .1f;
            button.colors = colors;
            Heading(label, 20, 0, w - 40, h, size, Color.white, alignment, rect);
            button.onClick.AddListener(() => action());
            return button;
        }

        UI.InputField Field(string value, string placeholder, float x, float y, float w, float h, int limit)
        {
            var rect = Shade(x, y, w, h, new Color(.19f, .14f, .27f));
            var image = rect.GetComponent<UI.Image>();
            image.raycastTarget = true;
            var field = rect.gameObject.AddComponent<UI.InputField>();
            field.targetGraphic = image;
            field.textComponent = Text("", 18, 0, w - 36, h, 30, Color.white, parent: rect);
            field.placeholder = Text(placeholder, 18, 0, w - 36, h, 27, new Color(.74f, .68f, .81f), parent: rect);
            field.characterLimit = limit;
            field.text = value;
            return field;
        }

        static Sprite MakeRibbon()
        {
            var texture = new Texture2D(128, 32, TextureFormat.RGBA32, false) { name = "Cinta de menú", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[128 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 128; x++)
                    if (x > y / 4 && x < 126 - (31 - y) / 4 && y > x / 65 && y < 31 - (127 - x) / 65)
                        pixels[y * 128 + x] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 128, 32), Vector2.one * .5f);
        }
    }
}
