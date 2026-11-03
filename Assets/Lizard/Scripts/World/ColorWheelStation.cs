using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    /// <summary>
    /// Ruleta de color del lobby: recorres las muestras con ← →, tu personaje se tiñe al momento (vista previa),
    /// E confirma y lo guarda; si te alejas sin confirmar vuelves a tu color. También se puede lamer una muestra.
    /// </summary>
    public class ColorWheelStation : MonoBehaviour
    {
        public const string PrefsKey = "Lizards.SkinColor";

        [Tooltip("Centro de la rueda (si se deja vacío, se busca un objeto 'Rueda de colores' cerca).")]
        public Transform wheel;
        public float useRadius = 3.2f;
        public float searchRadius = 3.5f;

        readonly List<Transform> swatches = new();
        readonly List<Color> colors = new();
        readonly List<Vector3> baseScales = new();
        int index = -1;
        bool previewing;
        Color committed;
        bool hasCommitted;
        float pulse, nextLick;

        // ---- Guardado del color elegido ----
        public static void Save(Color c) { PlayerPrefs.SetString(PrefsKey, ColorUtility.ToHtmlStringRGB(c)); PlayerPrefs.Save(); }
        public static bool TryLoad(out Color c)
        {
            c = Color.white;
            string hex = PlayerPrefs.GetString(PrefsKey, "");
            return hex.Length > 0 && ColorUtility.TryParseHtmlString("#" + hex, out c);
        }

        void Start()
        {
            if (!wheel)
                wheel = FindObjectsByType<Transform>(FindObjectsInactive.Exclude)
                    .Where(t => t.name == "Rueda de colores" && (t.position - transform.position).magnitude < searchRadius + 2f)
                    .OrderBy(t => (t.position - transform.position).sqrMagnitude).FirstOrDefault();
            Vector3 center = wheel ? wheel.position : transform.position + Vector3.up * 1.8f;

            // Muestras alrededor de la rueda, ordenadas en el sentido de las agujas del reloj desde arriba.
            var found = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(r => r.name.StartsWith("Muestra de color") && (r.bounds.center - center).magnitude < searchRadius)
                .Select(r => r.transform)
                .OrderBy(t => Mathf.Repeat(Mathf.Atan2(t.position.x - center.x, t.position.y - center.y) * Mathf.Rad2Deg, 360f))
                .ToList();
            foreach (var t in found)
            {
                var m = t.GetComponent<Renderer>().sharedMaterial;
                swatches.Add(t);
                colors.Add(m && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white);
                baseScales.Add(t.localScale);
                // Colisión y objetivo de lengua para poder lamerlas.
                if (!t.GetComponent<Collider>()) t.gameObject.AddComponent<SphereCollider>();
                if (!t.GetComponent<TongueTarget>()) t.gameObject.AddComponent<TongueTarget>().kind = TongueTargetKind.Surface;
            }

            hasCommitted = TryLoad(out committed);
            if (hasCommitted) index = NearestIndex(committed);
        }

        int NearestIndex(Color c)
        {
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < colors.Count; i++) { float d = Vector4.Distance(colors[i], c); if (d < bd) { bd = d; best = i; } }
            return best;
        }

        static SunnyVisual VisualOf(LizardPlayer p) => p ? p.GetComponentInChildren<SunnyVisual>() : null;

        void Preview(LizardPlayer p, int i)
        {
            index = (i + colors.Count) % colors.Count;
            var v = VisualOf(p);
            if (!v) return;
            if (!previewing) { previewing = true; if (!hasCommitted) { committed = v.SkinColor == Color.white ? new Color(0.36f, 0.76f, 0.2f) : v.SkinColor; } }
            v.SetSkinColor(colors[index]);
            pulse = 1f;
            Sfx.Play(Sfx.TongueOut, swatches[index].position, 0.25f);
        }

        void Confirm(LizardPlayer p)
        {
            if (index < 0) index = 0;
            var v = VisualOf(p);
            if (v) v.SetSkinColor(colors[index]);
            committed = colors[index]; hasCommitted = true; previewing = false;
            Save(committed);
            Fx.Burst(swatches[index].position, colors[index], 18, 3f, 0.12f);
            if (p) Fx.Burst(p.CenterPosition, colors[index], 14, 2.5f, 0.12f);
            Sfx.Play(Sfx.Transform, swatches[index].position, 0.6f);
            p?.Message("¡Nuevo color guardado!");
        }

        void Revert(LizardPlayer p)
        {
            previewing = false;
            var v = VisualOf(p);
            if (v && hasCommitted) v.SetSkinColor(committed);
            else if (v) v.SetSkinColor(new Color(0.36f, 0.76f, 0.2f));
            if (hasCommitted) index = NearestIndex(committed);
        }

        void Update()
        {
            if (colors.Count == 0) return;
            var p = LizardPlayer.All.FirstOrDefault(x => x.IsControlled);
            float dt = Time.deltaTime;

            // Resalte de la muestra elegida: más grande y latiendo.
            pulse = Mathf.MoveTowards(pulse, 0f, dt * 3f);
            for (int i = 0; i < swatches.Count; i++)
            {
                float k = i == index ? 1.28f + Mathf.Sin(Time.time * 6f) * 0.06f + pulse * 0.25f : 1f;
                swatches[i].localScale = Vector3.Lerp(swatches[i].localScale, baseScales[i] * k, 1f - Mathf.Exp(-dt * 14f));
            }

            if (!p || LizardLobby.Visible) return;
            float d = Vector3.Distance(p.transform.position, transform.position);

            // Lamer una muestra la elige y la confirma.
            if (Time.time >= nextLick && p.Tongue && p.Tongue.State == TongueState.Attached)
                for (int i = 0; i < swatches.Count; i++)
                    if (Vector3.Distance(p.Tongue.TipPosition, swatches[i].position) < 0.6f)
                    {
                        p.Tongue.Release(false);
                        Preview(p, i); Confirm(p);
                        nextLick = Time.time + 0.6f;
                        break;
                    }

            if (d < useRadius)
            {
                string label = previewing ? $"Confirmar este color ({index + 1}/{colors.Count})   (← → ver)" : "Elegir color   (← → ver)";
                InteractPrompts.Offer(this, (wheel ? wheel.position : transform.position) + Vector3.up * 2.2f, label, d);
            }
            else if (previewing && d > useRadius + 1f) Revert(p);

            if (!InteractPrompts.IsActive(this)) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame) Preview(p, (index < 0 ? -1 : index) + 1);
                if (kb.leftArrowKey.wasPressedThisFrame) Preview(p, (index < 0 ? 1 : index) - 1);
            }
            if (p.Input.Pressed(p.Input.Interact))
            {
                if (!previewing) Preview(p, index < 0 ? 0 : index + 1);
                else Confirm(p);
            }
        }
    }
}
