using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Avisos de interacción: cada objeto interactivo se "ofrece" cada fotograma con su texto y posición;
    /// gana solo el más cercano, que es el único que muestra el globo "[E] Acción" y el único que debe reaccionar a la E.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class InteractPrompts : MonoBehaviour
    {
        struct Entry { public object owner; public Vector3 pos; public string text; public float dist; }

        static InteractPrompts instance;
        static readonly List<Entry> offers = new();
        static Entry current;
        static bool hasCurrent;

        GUIStyle textStyle, keyStyle;
        float shown, bob;
        Vector3 shownPos;
        string shownText = "";

        static void Ensure()
        {
            if (instance || !Application.isPlaying) return;
            instance = new GameObject("Avisos de interacción").AddComponent<InteractPrompts>();
        }

        /// <summary>Ofrece una interacción este fotograma. 'dist' = distancia del jugador (gana la menor).</summary>
        public static void Offer(object owner, Vector3 worldPos, string text, float dist)
        {
            Ensure();
            offers.Add(new Entry { owner = owner, pos = worldPos, text = text, dist = dist });
        }

        /// <summary>¿Es este objeto el aviso activo? Solo el activo debe reaccionar a la tecla de interactuar.</summary>
        public static bool IsActive(object owner) => hasCurrent && ReferenceEquals(current.owner, owner);

        void LateUpdate()
        {
            hasCurrent = false;
            float best = float.MaxValue;
            foreach (var o in offers)
                if (o.dist < best) { best = o.dist; current = o; hasCurrent = true; }
            offers.Clear();
        }

        void OnGUI()
        {
            float dt = Time.unscaledDeltaTime;
            bool on = hasCurrent && !LizardLobby.Visible && !LizardHUD.MenuOpen;
            if (on) { shownPos = current.pos; shownText = current.text; }
            shown = Mathf.MoveTowards(shown, on ? 1f : 0f, dt * 6f);
            if (shown <= 0.01f) return;
            var cam = Camera.main;
            if (!cam) return;
            Vector3 sp = cam.WorldToScreenPoint(shownPos);
            if (sp.z <= 0f) return;

            textStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            keyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.15f, 0.1f, 0.05f) } };

            var player = LizardPlayer.All.Find(x => x.IsControlled);
            string key = player ? player.Input.Display(player.Input.Interact) : "E";
            if (key.Length > 3) key = key.Substring(0, 3);

            bob += dt;
            float pop = Mathf.SmoothStep(0.6f, 1f, shown);
            Vector2 size = textStyle.CalcSize(new GUIContent(shownText));
            float h = 38f * pop, keyW = 34f * pop, w = keyW + size.x + 28f;
            float x = sp.x - w / 2f, y = Screen.height - sp.y - h - 10f + Mathf.Sin(bob * 3f) * 2f;

            var old = GUI.color;
            GUI.color = new Color(0.06f, 0.09f, 0.13f, 0.88f * shown);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, GUI.color, 0, h / 2f);
            GUI.color = new Color(0.96f, 0.6f, 0.25f, shown);
            GUI.DrawTexture(new Rect(x + 5f, y + 5f, keyW, h - 10f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, GUI.color, 0, 8f);
            GUI.color = new Color(1f, 1f, 1f, shown);
            GUI.Label(new Rect(x + 5f, y + 5f, keyW, h - 10f), key, keyStyle);
            GUI.Label(new Rect(x + keyW + 14f, y, size.x + 10f, h), shownText, textStyle);
            // Pequeño piquito apuntando al objeto.
            GUI.color = new Color(0.06f, 0.09f, 0.13f, 0.88f * shown);
            GUI.DrawTexture(new Rect(sp.x - 6f, y + h - 1f, 12f, 8f), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, GUI.color, 0, 3f);
            GUI.color = old;
        }
    }
}
