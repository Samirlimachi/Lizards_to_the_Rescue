using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    /// <summary>
    /// Fuente de color del refugio. Junto a la fuente eliges el color del agua (← → o E);
    /// al meterte en el agua de la pila, tu personaje se tiñe de ese color y se guarda.
    /// </summary>
    public class ColorFountain : MonoBehaviour
    {
        [Header("Piezas")]
        public Renderer[] water = new Renderer[0];
        public Light glow;
        public Transform spout;

        [Header("Medidas")]
        [Tooltip("Radio interior de la pila (dentro = mojado).")]
        public float poolRadius = 2.6f;
        public float waterTopY = 0.4f;
        public float useRadius = 5.5f;
        [Tooltip("Círculos (radio, altura) donde cae el agua y salpica.")]
        public Vector2[] splashRings = new Vector2[0];

        public Color[] palette =
        {
            new(0.36f, 0.76f, 0.2f), new(0.2f, 0.85f, 0.55f), new(0.25f, 0.8f, 0.95f), new(0.25f, 0.45f, 0.95f),
            new(0.55f, 0.35f, 0.95f), new(0.95f, 0.4f, 0.8f), new(0.95f, 0.3f, 0.3f), new(1f, 0.55f, 0.15f),
            new(1f, 0.85f, 0.2f), new(0.95f, 0.95f, 0.92f), new(0.35f, 0.33f, 0.4f), new(0.55f, 0.36f, 0.22f),
        };
        static readonly string[] Names = { "verde", "menta", "celeste", "azul", "violeta", "rosa", "rojo", "naranja", "amarillo", "blanco", "gris", "marrón" };

        int index;
        Color shown;
        Material mat;
        float nextSpray, pulse;
        bool wasInside;

        void Start()
        {
            if (ColorWheelStation.TryLoad(out var saved)) index = Nearest(saved);
            shown = palette[index];
            if (water.Length > 0 && water[0])
            {
                mat = new Material(water[0].sharedMaterial);
                foreach (var r in water) if (r) r.sharedMaterial = mat;
            }
        }

        int Nearest(Color c)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < palette.Length; i++) { float d = Vector4.Distance(palette[i], c); if (d < bd) { bd = d; best = i; } }
            return best;
        }

        void Pick(int i)
        {
            index = (i % palette.Length + palette.Length) % palette.Length;
            pulse = 1f;
            Sfx.Play(Sfx.Eject, transform.position + Vector3.up, 0.35f);
            Fx.Burst(spout ? spout.position : transform.position + Vector3.up * 2f, palette[index], 12, 2.5f, 0.1f);
        }

        void Update()
        {
            float dt = Time.deltaTime, t = Time.time;
            shown = Color.Lerp(shown, palette[index], 1f - Mathf.Exp(-dt * 6f));
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 2f);
            if (mat)
            {
                // Brillo que ondula, más fuerte justo al cambiar de color.
                float shimmer = 0.06f * Mathf.Sin(t * 3f) + pulse * 0.25f;
                Color c = Color.Lerp(shown, Color.white, Mathf.Clamp01(0.08f + shimmer));
                c.a = 0.8f;
                mat.SetColor("_BaseColor", c);
            }
            if (glow) { glow.color = shown; glow.intensity = 1.4f + Mathf.Sin(t * 2.2f) * 0.2f + pulse; }

            // Chorro arriba y salpicaduras donde cae cada cortina de agua.
            if (t >= nextSpray)
            {
                nextSpray = t + 0.07f;
                if (spout) Fx.Burst(spout.position, Color.Lerp(shown, Color.white, 0.3f), 2, 2.2f, 0.08f);
                foreach (var ring in splashRings)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    Vector3 at = transform.position + new Vector3(Mathf.Cos(a) * ring.x, ring.y + 0.05f, Mathf.Sin(a) * ring.x);
                    Fx.Burst(at, Color.Lerp(shown, Color.white, 0.45f), 2, 1.2f, 0.07f);
                }
            }

            var p = LizardPlayer.All.FirstOrDefault(x => x.IsControlled);
            if (!p || LizardLobby.Visible) return;
            Vector3 flat = p.transform.position - transform.position; flat.y = 0f;
            float d = flat.magnitude;
            bool inside = d < poolRadius && p.transform.position.y < transform.position.y + waterTopY + 0.25f;

            // Dentro del agua: te tiñe (una vez por color) con chapoteo.
            if (inside)
            {
                var v = p.GetComponentInChildren<SunnyVisual>();
                if (!wasInside) { Fx.Burst(p.transform.position + Vector3.up * 0.3f, shown, 16, 3f, 0.1f); Sfx.Play(Sfx.Land, p.transform.position, 0.7f); }
                if (v && Vector4.Distance(v.SkinColor, palette[index]) > 0.02f)
                {
                    v.SetSkinColor(palette[index]);
                    ColorWheelStation.Save(palette[index]);
                    Fx.Burst(p.CenterPosition, palette[index], 22, 3f, 0.12f);
                    Sfx.Play(Sfx.Transform, p.transform.position, 0.6f);
                    p.Message($"¡Te has teñido de {Names[index % Names.Length]}!");
                }
            }
            wasInside = inside;

            // Fuera, cerca: elegir el color del agua.
            if (!inside && d < useRadius)
                InteractPrompts.Offer(this, transform.position + Vector3.up * 5.8f, $"Agua {Names[index % Names.Length]} · cambiar (← →)   ·   métete para teñirte", d);
            if (!InteractPrompts.IsActive(this)) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame) Pick(index + 1);
                if (kb.leftArrowKey.wasPressedThisFrame) Pick(index - 1);
            }
            if (p.Input.Pressed(p.Input.Interact)) Pick(index + 1);
        }
    }
}
