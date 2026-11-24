using TMPro;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Barra de vida flotante sobre un enemigo: aparece al recibir daño, baja con una estela (primero cae la parte
    /// blanca, luego la de color, como en los juegos de lucha) y se desvanece sola si no le vuelven a pegar.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        const float Width = 0.9f, Height = 0.1f, ShowTime = 2.4f;
        static Material mat;

        EnemyHealth health;
        Transform root, fill, trail, back;
        Renderer fillR, trailR, backR, frameR;
        MaterialPropertyBlock mpb;
        float max, shown = 1f, trailShown = 1f, visibleUntil, alpha, punch;
        bool built;

        public void Init(EnemyHealth h) { health = h; max = Mathf.Max(0.01f, h.health); }

        /// <summary>Al recibir daño: se muestra y da un pequeño salto.</summary>
        public void Ping() { visibleUntil = Time.time + ShowTime; punch = 1f; lastPing = Time.time; }
        float lastPing = -10f;

        void Build()
        {
            built = true;
            mpb = new MaterialPropertyBlock();
            if (!mat)
            {
                // Sin luz y transparente (para poder desvanecerse).
                mat = new Material(LizardMaterials.Particle()) { name = "Barra de vida" };
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 50;
            }
            root = new GameObject("Barra de vida").transform; root.SetParent(transform, false);
            frameR = Quad("Marco", new Vector3(Width + 0.06f, Height + 0.06f, 1f), 0.004f, 0).GetComponent<Renderer>();
            back = Quad("Fondo", new Vector3(Width, Height, 1f), 0.002f, 1); backR = back.GetComponent<Renderer>();
            trail = Quad("Estela", new Vector3(Width, Height, 1f), 0.001f, 2); trailR = trail.GetComponent<Renderer>();
            fill = Quad("Vida", new Vector3(Width, Height, 1f), 0f, 3); fillR = fill.GetComponent<Renderer>();
        }

        Transform Quad(string n, Vector3 scale, float back, int order)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = n; q.transform.SetParent(root, false);
            q.transform.localScale = scale; q.transform.localPosition = new Vector3(0f, 0f, back);
            var r = q.GetComponent<Renderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.sortingOrder = order;
            return q.transform;
        }

        void LateUpdate()
        {
            if (!health) return;
            float target = Mathf.Clamp01(health.health / max);
            bool want = Time.time < visibleUntil && target > 0f;
            alpha = Mathf.MoveTowards(alpha, want ? 1f : 0f, Time.deltaTime * (want ? 10f : 2.5f));
            if (alpha <= 0f) { if (root) root.gameObject.SetActive(false); return; }
            if (!built) Build();
            root.gameObject.SetActive(true);

            // La vida baja rápido; la estela blanca la sigue con retraso.
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * 3f);
            float trailSpeed = Time.time - lastPing > 0.35f ? 1.4f : 0f;   // la estela espera un instante y luego cae
            trailShown = Mathf.Max(shown, Mathf.MoveTowards(trailShown, shown, Time.deltaTime * trailSpeed));
            punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 5f);

            var cam = Camera.main;
            Bounds b = Bounds();
            root.position = new Vector3(b.center.x, b.max.y + 0.35f + punch * 0.08f, b.center.z);
            if (cam) root.rotation = cam.transform.rotation;
            root.localScale = Vector3.one * (1f + punch * 0.25f) / Mathf.Max(0.01f, transform.lossyScale.x);

            Bar(fill, shown); Bar(trail, trailShown);
            Color c = target > 0.6f ? new Color(0.45f, 0.95f, 0.3f) : target > 0.3f ? new Color(1f, 0.82f, 0.2f) : new Color(1f, 0.3f, 0.25f);
            Tint(fillR, Color.Lerp(c, Color.white, punch * 0.6f));
            Tint(trailR, new Color(1f, 0.95f, 0.85f));
            Tint(backR, new Color(0.12f, 0.08f, 0.14f));
            Tint(frameR, new Color(0.02f, 0.02f, 0.03f));
        }

        static void Bar(Transform t, float k)
        {
            k = Mathf.Max(0.0001f, k);
            t.localScale = new Vector3(Width * k, Height, 1f);
            t.localPosition = new Vector3(-Width * 0.5f + Width * k * 0.5f, 0f, t.localPosition.z);
        }

        void Tint(Renderer r, Color c)
        {
            c.a = alpha;
            r.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", c); r.SetPropertyBlock(mpb);
        }

        Bounds Bounds()
        {
            bool any = false; var b = new Bounds(transform.position, Vector3.one * 0.5f);
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || (root && r.transform.IsChildOf(root)) || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }

    /// <summary>Número de daño que salta del enemigo, rebota y se desvanece (letra Bangers con contorno).</summary>
    [AddComponentMenu("")]
    public sealed class DamagePopup : MonoBehaviour
    {
        static TMP_FontAsset font; static Material fontMat; static bool loaded;
        TextMeshPro text; Vector3 vel; float age; Color color;
        const float Life = 0.85f;

        public static void Spawn(Vector3 pos, float amount, bool big)
        {
            if (amount <= 0f) return;
            string n = amount >= 10f || Mathf.Approximately(amount, Mathf.Round(amount)) ? Mathf.RoundToInt(amount).ToString() : amount.ToString("0.#");
            SpawnText(pos, "-" + n, big ? new Color(1f, 0.45f, 0.2f) : new Color(1f, 0.92f, 0.3f), big ? 6.5f : 5f);
        }

        /// <summary>Texto flotante genérico (p. ej. "+10" al entrar comida en el embudo).</summary>
        public static void SpawnText(Vector3 pos, string message, Color color, float fontSize = 5f)
        {
            if (!Application.isPlaying) return;
            if (!loaded)
            {
                loaded = true;
                font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF");
                fontMat = Resources.Load<Material>("Fonts & Materials/Bangers SDF - Outline");
            }
            var go = new GameObject("Texto flotante");
            go.transform.position = pos;
            var p = go.AddComponent<DamagePopup>();
            p.text = go.AddComponent<TextMeshPro>();
            if (font) p.text.font = font;
            if (fontMat) p.text.fontSharedMaterial = fontMat;
            p.text.alignment = TextAlignmentOptions.Center;
            p.text.fontSize = fontSize;
            p.text.text = message;
            p.color = color;
            p.text.color = p.color;
            p.text.outlineWidth = 0.25f; p.text.outlineColor = new Color32(30, 10, 20, 255);
            p.vel = new Vector3(Random.Range(-0.6f, 0.6f), 3.2f, 0f);
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            float k = age / Life;
            if (k >= 1f) { Destroy(gameObject); return; }
            vel += Vector3.down * 7f * Time.deltaTime;
            var cam = Camera.main;
            Vector3 side = cam ? cam.transform.right : Vector3.right;
            transform.position += (side * vel.x + Vector3.up * vel.y) * Time.deltaTime;
            if (cam) transform.rotation = cam.transform.rotation;
            // Salto de tamaño al aparecer (pop) y se encoge al final.
            float pop = k < 0.15f ? Mathf.Lerp(0.2f, 1.35f, k / 0.15f) : Mathf.Lerp(1.35f, 1f, Mathf.Clamp01((k - 0.15f) / 0.15f));
            transform.localScale = Vector3.one * pop * (k > 0.75f ? Mathf.Lerp(1f, 0.6f, (k - 0.75f) / 0.25f) : 1f) * 0.85f;   // "-1" mide ~0,45 m
            var c = color; c.a = k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f; text.color = c;
        }
    }
}
