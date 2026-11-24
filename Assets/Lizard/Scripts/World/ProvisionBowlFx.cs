using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Efectos del embudo de provisiones (LEVEL2):
    ///  · Cada comida que entra salta un "+10" verde con un destello; si sale (o se la comen), un "-10" rojo.
    ///  · Al completar la cuota: fuente de chispas por todo el aro, onda de luz que recorre el borde y se expande,
    ///    confeti, luz cálida que late, la comida de dentro da un saltito y un rótulo "¡CUOTA COMPLETA!".
    ///  · Mientras siga lleno, el embudo chispea de vez en cuando y la luz respira.
    /// Todo se calcula en cada cliente a partir del total sincronizado, así que en grupo lo ven todos a la vez.
    /// </summary>
    public sealed class ProvisionBowlFx : MonoBehaviour
    {
        public float rimRadius = 3.45f;
        public float rimHeight = 1.1f;   // altura del aro sobre el centro de la meta

        FoodGoal goal; LevelTwoExpedition exp;
        Light glow; bool wasReady; float nextCelebrate, nextAmbient;
        static readonly Color[] SparkColors = { new(1f, 0.85f, 0.2f), new(1f, 0.55f, 0.15f), new(1f, 1f, 0.85f), new(0.6f, 1f, 0.4f), new(1f, 0.45f, 0.7f) };

        Vector3 Center => transform.position + Vector3.up * rimHeight;
        Vector3 RimPoint(float a) => Center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rimRadius;

        public void Init(FoodGoal g, LevelTwoExpedition e)
        {
            goal = g; exp = e;
            goal.OnFoodMoved += OnFoodMoved;
            var lg = new GameObject("Luz del embudo"); lg.transform.SetParent(transform, false); lg.transform.position = Center + Vector3.up * 1.2f;
            glow = lg.AddComponent<Light>(); glow.type = LightType.Point; glow.range = 11f; glow.color = new Color(1f, 0.82f, 0.4f); glow.intensity = 0f; glow.shadows = LightShadows.None;
        }

        void OnDestroy() { if (goal) goal.OnFoodMoved -= OnFoodMoved; }

        readonly Dictionary<FoodItem, int> valueIn = new();   // puntos con los que entró cada comida (al comerla su valor pasa a 0)

        void OnFoodMoved(FoodItem f, bool entered)
        {
            if (!f) return;
            Vector3 p = f.transform.position + Vector3.up * 0.4f;
            if (entered)
            {
                int points = Mathf.RoundToInt(f.nutrition * 10f);
                if (points <= 0) return;
                valueIn[f] = points;
                DamagePopup.SpawnText(p, "+" + points, new Color(0.55f, 1f, 0.35f), 5f);
                Fx.Burst(p, new Color(1f, 0.9f, 0.4f), 6, 2f, 0.05f);
                Sfx.Play(Sfx.Eat, p, 0.35f);
            }
            else if (valueIn.TryGetValue(f, out int had))
            {
                valueIn.Remove(f);
                DamagePopup.SpawnText(p, "-" + had, new Color(1f, 0.4f, 0.35f), 4.5f);
            }
        }

        void Update()
        {
            if (!goal || !exp) return;
            bool ready = exp.Ready && !exp.Completed;
            if (ready && !wasReady && Time.time >= nextCelebrate) { nextCelebrate = Time.time + 4f; StartCoroutine(Celebrate()); }
            wasReady = ready;

            // Luz cálida que respira mientras está lleno; se apaga suave si se vacía.
            if (glow)
            {
                float target = ready ? 1.6f + Mathf.Sin(Time.time * 3f) * 0.5f : 0f;
                glow.intensity = Mathf.MoveTowards(glow.intensity, target, Time.deltaTime * (glow.intensity > target ? 4f : 6f));
            }

            // Chispas sueltas por el aro mientras siga completo.
            if (ready && Time.time >= nextAmbient)
            {
                nextAmbient = Time.time + 0.18f;
                Fx.Burst(RimPoint(Random.value * Mathf.PI * 2f) + Vector3.up * 0.1f, SparkColors[Random.Range(0, 3)], 3, 3f, 0.05f);
            }
        }

        IEnumerator Celebrate()
        {
            Sfx.Play(Sfx.Help, Center, 1f);
            Sfx.Play(Sfx.Transform, Center, 0.7f);
            DamagePopup.SpawnText(Center + Vector3.up * 2.4f, "¡CUOTA COMPLETA!", new Color(1f, 0.85f, 0.25f), 9f);
            if (glow) glow.intensity = 7f;
            RingWave.Spawn(Center + Vector3.up * 0.05f, rimRadius, rimRadius * 2.6f, 0.7f, new Color(1f, 0.85f, 0.35f));
            StartCoroutine(HopFood());

            // Onda de chispas que da la vuelta al aro y, a la vez, fuentes en todo el borde.
            float start = Random.value * Mathf.PI * 2f;
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                float sweep = start + t / 0.6f * Mathf.PI * 2f;
                if (t < 0.6f) for (int i = 0; i < 2; i++) Fx.Burst(RimPoint(sweep + i * Mathf.PI) + Vector3.up * 0.1f, new Color(1f, 1f, 0.8f), 4, 4f, 0.07f);
                if (Random.value < 0.7f)
                {
                    var c = SparkColors[Random.Range(0, SparkColors.Length)];
                    Fx.Burst(RimPoint(Random.value * Mathf.PI * 2f) + Vector3.up * 0.15f, c, 7, 7.5f, 0.07f);
                }
                if (t > 0.3f && Random.value < 0.25f)   // confeti sobre el centro
                    Fx.Burst(Center + Vector3.up * Random.Range(2f, 3.5f) + Random.insideUnitSphere, SparkColors[Random.Range(0, SparkColors.Length)], 10, 3f, 0.06f);
                yield return null;
            }
        }

        /// <summary>La comida de dentro da un saltito de alegría, en ola desde el centro (solo quien lleva la física).</summary>
        IEnumerator HopFood()
        {
            if (SharedFoodSync.Online && !SharedFoodSync.Master) yield break;
            var list = new List<FoodItem>(goal.Inside);
            list.Sort((a, b) => (a ? Vector3.Distance(a.transform.position, Center) : 0f).CompareTo(b ? Vector3.Distance(b.transform.position, Center) : 0f));
            float t0 = Time.time;
            foreach (var f in list)
            {
                if (!f || !f.Body || f.Body.isKinematic) continue;
                float delay = Vector3.Distance(f.transform.position, Center) * 0.05f;
                while (Time.time - t0 < delay) yield return null;
                f.Body.WakeUp();
                f.Body.AddForce(Vector3.up * Random.Range(2.2f, 3.2f) + Random.insideUnitSphere * 0.3f, ForceMode.VelocityChange);
                f.Body.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
            }
        }
    }

    /// <summary>Onda de luz: un anillo de bolitas brillantes que se expande desde el borde y se desvanece.</summary>
    [AddComponentMenu("")]
    public sealed class RingWave : MonoBehaviour
    {
        static Material mat;
        readonly List<Transform> beads = new();
        float from, to, life, age; Color color; MaterialPropertyBlock mpb;

        public static void Spawn(Vector3 center, float from, float to, float life, Color color)
        {
            if (!Application.isPlaying) return;
            var go = new GameObject("Onda de luz"); go.transform.position = center;
            var w = go.AddComponent<RingWave>(); w.from = from; w.to = to; w.life = life; w.color = color; w.mpb = new MaterialPropertyBlock();
            if (!mat) mat = LizardMaterials.Particle();
            int n = 48;
            for (int i = 0; i < n; i++)
            {
                var b = new GameObject("Destello").transform; b.SetParent(go.transform, false);
                b.gameObject.AddComponent<MeshFilter>().sharedMesh = GooSplat.SphereMesh;
                var r = b.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                b.localRotation = Quaternion.Euler(0f, -i * 360f / n, 0f);
                w.beads.Add(b);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / life);
            float e = 1f - (1f - k) * (1f - k);
            float r = Mathf.Lerp(from, to, e);
            float s = Mathf.Lerp(0.28f, 0.05f, k);
            Color c = Color.Lerp(color, Color.white, 0.4f * (1f - k));
            mpb.SetColor("_BaseColor", c);
            for (int i = 0; i < beads.Count; i++)
            {
                float a = i * Mathf.PI * 2f / beads.Count;
                beads[i].localPosition = new Vector3(Mathf.Cos(a) * r, 0.1f + Mathf.Sin(k * Mathf.PI) * 0.3f, Mathf.Sin(a) * r);
                beads[i].localScale = new Vector3(s * 2.2f, s, s);
                beads[i].GetComponent<Renderer>().SetPropertyBlock(mpb);
            }
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
