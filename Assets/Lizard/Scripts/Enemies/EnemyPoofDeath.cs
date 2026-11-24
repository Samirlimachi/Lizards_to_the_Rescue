using System.Collections;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Muerte de dibujo animado para enemigos sencillos (babosa patrullera…): se aplasta por el golpe, salta girando
    /// estirado como un muelle y revienta en una nube de su color (<see cref="EnemyPoof"/>).
    /// Lo usan el maestro (EnemyHealth) y las copias de red (LevelNet), así se ve igual en todas las pantallas.
    /// </summary>
    public sealed class EnemyPoofDeath : MonoBehaviour
    {
        Vector3 from;

        public static void Play(GameObject enemy, Vector3 from)
        {
            if (!enemy || enemy.GetComponent<EnemyPoofDeath>()) return;
            var h = enemy.GetComponent<EnemyHealth>(); if (h) h.health = Mathf.Min(h.health, 0f);
            // Fuera de combate: sin colisión, sin daño, sin patrulla ni seguimiento de red.
            foreach (var c in enemy.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (var b in enemy.GetComponents<MonoBehaviour>())
                if (b is PatrolEnemy || b is DamageSource || b is NetPoseFollower || b is SlugCrawl) b.enabled = false;
            var rb = enemy.GetComponent<Rigidbody>(); if (rb) rb.isKinematic = true;
            var d = enemy.AddComponent<EnemyPoofDeath>(); d.from = from;
        }

        IEnumerator Start()
        {
            var tf = transform;
            Vector3 p0 = tf.position, s0 = tf.localScale;
            Quaternion r0 = tf.rotation;
            Color color = ColorOf(gameObject);
            Bounds bb = WorldBounds();
            float size = Mathf.Clamp(bb.extents.magnitude, 0.4f, 1.5f);
            Vector3 away = p0 - from; away.y = 0f; away = away.sqrMagnitude > 0.01f ? away.normalized : -tf.forward;
            Fx.Burst(bb.center, color, 10, 3f, 0.08f);
            Sfx.Play(Sfx.Hurt, p0, 0.6f);

            // 1) Golpe: se aplasta y retrocede un poco.
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.1f)
            {
                float e = Mathf.Sin(t * Mathf.PI * 0.5f);
                tf.position = p0 + away * 0.25f * e;
                tf.localScale = Vector3.Scale(s0, new Vector3(1f + 0.3f * e, 1f - 0.45f * e, 1f + 0.3f * e));
                yield return null;
            }
            Vector3 p1 = tf.position;
            // 2) Salta estirado girando sobre sí mismo (como un muelle que se suelta).
            float spin = Random.value < 0.5f ? -1f : 1f;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.3f)
            {
                float e = 1f - (1f - t) * (1f - t);
                float stretch = Mathf.Sin(Mathf.Min(1f, t * 1.4f) * Mathf.PI * 0.5f);
                tf.position = p1 + Vector3.up * 0.7f * e + away * 0.2f * e;
                tf.rotation = Quaternion.AngleAxis(spin * 540f * e, Vector3.up) * Quaternion.AngleAxis(-25f * e, Vector3.Cross(Vector3.up, away)) * r0;
                tf.localScale = Vector3.Scale(s0, new Vector3(1.3f - 0.6f * stretch, 0.55f + 0.95f * stretch, 1.3f - 0.6f * stretch));
                yield return null;
            }
            // 3) ¡Pop!
            EnemyPoof.Pop(WorldBounds().center, color, size, true);
            Destroy(gameObject);
        }

        Bounds WorldBounds()
        {
            bool any = false; Bounds b = new Bounds(transform.position, Vector3.one * 0.5f);
            foreach (var r in GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b;
        }

        /// <summary>Color dominante del enemigo (media de sus colores de vértice; sin ojos blancos ni negros).</summary>
        public static Color ColorOf(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; if (!m || !m.isReadable) continue;
                var cols = m.colors; if (cols == null || cols.Length == 0) continue;
                Color sum = Color.clear; int n = 0;
                for (int i = 0; i < cols.Length; i += 3)
                {
                    Color.RGBToHSV(cols[i], out _, out float s, out float v);
                    if (s < 0.25f || v < 0.2f) continue;   // blancos de los ojos, pupilas y sombras fuera
                    sum += cols[i]; n++;
                }
                if (n > 0) { var c = (sum / n).gamma; c.a = 1f; return c; }   // los colores de vértice de los niveles están en lineal
            }
            var r = go.GetComponentInChildren<Renderer>();
            if (r && r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseColor")) return r.sharedMaterial.GetColor("_BaseColor");
            return new Color(0.6f, 0.85f, 0.25f);
        }
    }

    /// <summary>
    /// Babosa que avanza como un gusano: se estira hacia delante y se encoge al ritmo de su velocidad, y respira
    /// cuando está quieta. Mide la velocidad por el movimiento real, así funciona igual en las copias de red.
    /// Al recibir un golpe: se aplasta como gelatina, retrocede inclinándose hacia atrás, tiembla, salpica baba
    /// de su color y se enfada (roja y huyendo más rápido, retorciéndose).
    /// </summary>
    public sealed class SlugCrawl : MonoBehaviour
    {
        Transform visual; Vector3 rest, restPos, lastPos; Quaternion restRot; float phase;
        // Muelles de la reacción al golpe: aplastar, retroceso (en local) e inclinación.
        float sq, sqVel, lean, leanVel, shakeUntil, angryUntil;
        Vector3 recoil, recoilVel, leanAxis = Vector3.right;
        Color color = new Color(0.6f, 0.85f, 0.25f);

        void Start()
        {
            foreach (Transform c in transform) if (c.GetComponent<Renderer>()) { visual = c; break; }
            if (visual) { rest = visual.localScale; restPos = visual.localPosition; restRot = visual.localRotation; }
            lastPos = transform.position;
            phase = Random.value * 10f;
            color = EnemyPoofDeath.ColorOf(gameObject);
        }

        /// <summary>Reacción a un golpe (en todos los clientes: el visual es local; la huida la manda el maestro).</summary>
        public void Hit(float amount, Vector3 from)
        {
            float k = Mathf.Clamp(amount, 0.5f, 3f);
            Vector3 away = transform.position - from; away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : -transform.forward;
            Vector3 awayLocal = transform.InverseTransformDirection(away);
            sqVel -= 7f * k;                                       // ¡chof! se aplasta y rebota
            recoilVel += awayLocal * 3.5f * k;                     // sale despedida hacia atrás
            leanAxis = Vector3.Cross(Vector3.up, awayLocal).normalized;
            if (leanAxis.sqrMagnitude < 0.01f) leanAxis = Vector3.right;
            leanVel += 420f * k;                                   // se inclina alejándose del golpe
            shakeUntil = Time.time + 0.18f;
            angryUntil = Time.time + 2.2f;
            Vector3 c = visual ? visual.position + Vector3.up * 0.2f : transform.position;
            Fx.Burst(c, color, 12, 3.5f, 0.09f);
            Fx.Burst(c, Color.Lerp(color, Color.white, 0.6f), 5, 2f, 0.05f);
            Sfx.Play(Sfx.Eject, c, 0.55f);
            var h = GetComponent<EnemyHealth>(); if (h) h.Tint(new Color(1.25f, 0.45f, 0.4f), 2.2f);
            var patrol = GetComponent<PatrolEnemy>(); if (patrol && patrol.enabled) patrol.React(from, 0.09f, 2.2f);
        }

        void LateUpdate()
        {
            if (!visual) return;
            float dt = Mathf.Min(Mathf.Max(Time.deltaTime, 0.0001f), 1f / 30f);
            Vector3 d = transform.position - lastPos; d.y = 0f; lastPos = transform.position;
            float speed = Mathf.Min(d.magnitude / Mathf.Max(Time.deltaTime, 0.0001f), 6f);
            bool angry = Time.time < angryUntil;
            phase += dt * (2.2f + speed * 5f) * (angry ? 1.6f : 1f);
            float move = Mathf.Clamp01(speed / 1.2f);
            float s = Mathf.Sin(phase);

            // Muelles de la reacción.
            sqVel += (-260f * sq - 11f * sqVel) * dt; sq = Mathf.Clamp(sq + sqVel * dt, -0.45f, 0.45f);
            recoilVel += (-140f * recoil - 12f * recoilVel) * dt; recoil += recoilVel * dt;
            leanVel += (-220f * lean - 13f * leanVel) * dt; lean = Mathf.Clamp(lean + leanVel * dt, -45f, 45f);

            // Estira (largo y fino) y encoge (corto y alto); quieta, solo respira. Enfadada se retuerce más.
            float wiggle = angry ? 0.06f : 0f;
            float z = 1f + s * (0.16f * move + 0.03f + wiggle);
            float y = (1f - s * (0.1f * move + 0.025f)) * (1f + sq);
            float x = 1f / Mathf.Sqrt(Mathf.Max(0.3f, z * y));
            visual.localScale = Vector3.Scale(rest, new Vector3(x, y, z));
            Vector3 shake = Time.time < shakeUntil ? Random.insideUnitSphere * 0.04f : Vector3.zero;
            float sway = angry ? Mathf.Sin(phase * 2f) * 6f : 0f;
            visual.localPosition = restPos + recoil + shake;
            visual.localRotation = Quaternion.AngleAxis(lean, leanAxis) * Quaternion.AngleAxis(sway, Vector3.up) * restRot;
        }

        void OnDisable() { if (visual) { visual.localScale = rest; visual.localPosition = restPos; visual.localRotation = restRot; } }
    }
}
