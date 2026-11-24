using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// El slime y el agua (y los huecos):
    ///  · Antes de cada saltito o salto de ataque mira dónde va a caer: si es agua o un hueco, acorta el salto o no salta.
    ///  · Si aun así cae al agua (un empujón, la bola gigante…): chapuzón, flota con cara de susto, chapotea hacia la
    ///    orilla más cercana y sale de un salto a tierra firme.
    ///  · Red de seguridad: si no encuentra orilla, tarda demasiado o cae fuera del mapa, vuelve a su sitio con un "pop".
    /// Antes se hundía hasta el fondo del agua (opaca, 2,8 m bajo la superficie) y se quedaba ahí sin verse.
    /// </summary>
    public partial class SlimeEnemy
    {
        static readonly Color SlimeGreen = new Color(0.72f, 0.95f, 0.25f);
        const float EscapeFlight = 0.65f;     // duración del salto para salir del agua (arco alto: salva el bordillo)
        const float MaxSwimTime = 7f;         // si no ha salido en este tiempo, vuelve a su sitio

        bool escaping, blockedByWater, wasInWater;
        float swimStart;
        Vector3? escapePoint;

        // ------------------------------------------------------------------ ¿se puede caer aquí?
        /// <summary>¿Hay agua en este punto del suelo? (el fondo del agua queda por debajo de la superficie de una zona de nado)</summary>
        static bool IsWaterAt(Vector3 ground)
        {
            foreach (var z in SwimZone.All)
            {
                if (!z) continue;
                Vector3 d = ground - z.transform.position; d.y = 0f;
                float s = z.SurfaceWorld;
                if (d.magnitude < z.radius && s > ground.y + 0.05f && s < ground.y + z.depth + 1f) return true;
            }
            return false;
        }

        /// <summary>Suelo firme (estático) bajo un punto: ignora al propio slime, jugadores, comida y objetos sueltos.</summary>
        bool GroundBelow(Vector3 p, float from, float range, out RaycastHit ground)
        {
            ground = default; float best = float.MaxValue; bool any = false;
            foreach (var h in Physics.RaycastAll(p + Vector3.up * from, Vector3.down, range, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.rigidbody == rb || System.Array.IndexOf(ownColliders, h.collider) >= 0) continue;
                if (h.rigidbody && !h.rigidbody.isKinematic) continue;
                if (h.collider.GetComponentInParent<LizardPlayer>()) continue;
                if (h.distance < best) { best = h.distance; ground = h; any = true; }
            }
            return any;
        }

        /// <summary>¿Es seguro caer en este punto? (hay suelo, no es agua y no es una caída enorme)</summary>
        bool IsSafeLanding(Vector3 p)
        {
            if (!GroundBelow(p, 2.5f, 8f, out var g)) return false;
            if (g.point.y < transform.position.y - 3f) return false;
            return !IsWaterAt(g.point);
        }

        /// <summary>La mayor distancia (≤ dist) en esa dirección que cae en suelo seguro; 0 si ninguna.</summary>
        float SafeStep(Vector3 from, Vector3 dir, float dist)
        {
            for (float s = dist; s > 0.05f; s -= 0.25f)
                if (IsSafeLanding(from + dir * s)) return s;
            return 0f;
        }

        bool LeapLandsDry()
        {
            if (!target) return false;
            Vector3 d = Flat(target.transform.position - transform.position);
            float dist = Mathf.Clamp(d.magnitude, 0.5f, leapMax);
            return d.sqrMagnitude > 0.01f && SafeStep(transform.position, d.normalized, dist) >= dist * 0.8f;
        }

        /// <summary>Punto de paseo alrededor de su casa que no sea agua (si no encuentra, se queda en casa).</summary>
        Vector3 DryWanderTarget()
        {
            for (int i = 0; i < 6; i++)
            {
                var r = Random.insideUnitCircle * wanderRadius;
                Vector3 p = home + new Vector3(r.x, 0f, r.y);
                if (IsSafeLanding(p)) return p;
            }
            return home;
        }

        // ------------------------------------------------------------------ en el agua (solo el maestro)
        /// <summary>Devuelve true si el agua (o la vuelta a casa) toma el control este paso de física.</summary>
        bool WaterTick()
        {
            Vector3 pos = transform.position;
            if (pos.y < home.y - 6f) { RescueHome(); return true; }   // fuera del mapa
            if (escaping) return false;                               // saltando a la orilla: lo lleva el estado Hop

            bool inWater = SwimZone.Find(pos, out float surface, out _);
            if (!inWater) { if (state == State.Swim) Enter(State.Idle); return false; }

            if (state != State.Swim)
            {
                Enter(State.Swim);
                swimStart = Time.time;
                escapePoint = FindEscape(surface);
            }
            if (Time.time - swimStart > MaxSwimTime) { RescueHome(); return true; }

            // Flota medio hundido, meciéndose.
            Vector3 v = rb.linearVelocity;
            float floatY = surface - 0.5f + Mathf.Sin(Time.time * 3.2f) * 0.06f;
            v.y = Mathf.Lerp(v.y, (floatY - pos.y) * 6f, 0.25f);

            if (escapePoint.HasValue)
            {
                // Chapotea hacia la orilla y, en cuanto está cerca, salta fuera.
                Vector3 to = Flat(escapePoint.Value - pos);
                Face(escapePoint.Value, 1.6f);
                Vector3 paddle = to.sqrMagnitude > 0.01f ? to.normalized * 1.8f : Vector3.zero;
                v.x = Mathf.Lerp(v.x, paddle.x, 0.12f); v.z = Mathf.Lerp(v.z, paddle.z, 0.12f);
                if (Time.time - swimStart > 0.6f && to.magnitude < 3.5f)
                {
                    escaping = true; hopToPlayer = false; hopTarget = escapePoint.Value;
                    rb.linearVelocity = v;
                    Enter(State.Hop, null, HopTakeoff - 1);   // despega enseguida
                    return true;
                }
            }
            else
            {
                v.x *= 0.9f; v.z *= 0.9f;
                if (Time.time - swimStart > 1.5f) { RescueHome(); return true; }   // agua sin orilla alcanzable
            }
            rb.linearVelocity = v;
            return true;
        }

        /// <summary>Orilla más cercana: suelo seco algo por encima del agua, con sitio para el slime.</summary>
        Vector3? FindEscape(float surface)
        {
            Vector3 pos = transform.position;
            float[] radii = { 1.4f, 2.2f, 3f, 4f, 5.5f };
            foreach (float r in radii)
            {
                Vector3? best = null; float bestHome = float.MaxValue;
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2f;
                    Vector3 p = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!GroundBelow(new Vector3(p.x, surface, p.z), 3f, 6f, out var g)) continue;
                    if (g.point.y < surface + 0.05f || g.point.y > surface + 2.5f || IsWaterAt(g.point)) continue;
                    if (Physics.CheckSphere(g.point + Vector3.up * 0.75f, 0.4f, ~0, QueryTriggerInteraction.Ignore) && Blocked(g.point + Vector3.up * 0.75f)) continue;
                    float h = (g.point - home).sqrMagnitude;
                    if (h < bestHome) { bestHome = h; best = g.point + Vector3.up * 0.05f; }
                }
                if (best.HasValue) return best;
            }
            return null;
        }

        /// <summary>¿Hay algo sólido (que no sea el propio slime ni algo suelto) en ese hueco?</summary>
        bool Blocked(Vector3 c)
        {
            foreach (var col in Physics.OverlapSphere(c, 0.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (System.Array.IndexOf(ownColliders, col) >= 0) continue;
                if (col.attachedRigidbody && !col.attachedRigidbody.isKinematic) continue;
                if (col.GetComponentInParent<LizardPlayer>()) continue;
                return true;
            }
            return false;
        }

        /// <summary>Vuelve a su sitio con un "pop" en los dos extremos (lo ven también las copias de red).</summary>
        void RescueHome()
        {
            Vector3 from = transform.position;
            EnemyPoof.Pop(from + Vector3.up * 0.5f, SlimeGreen, 0.8f, false);
            rb.position = home + Vector3.up * 0.2f; transform.position = rb.position;
            rb.linearVelocity = Vector3.zero;
            escaping = false; blockedByWater = false;
            EnemyPoof.Pop(home + Vector3.up * 0.6f, SlimeGreen, 0.8f, false);
            Enter(State.Idle);
            nextWander = Time.time + 1.5f;
        }

        // ------------------------------------------------------------------ chapuzón (todos los clientes)
        void SplashCheck()
        {
            bool inWater = SwimZone.Find(transform.position + Vector3.up * 0.1f, out float surface, out _);
            if (inWater && !wasInWater)
            {
                Vector3 at = new Vector3(transform.position.x, surface + 0.05f, transform.position.z);
                Fx.Burst(at, new Color(0.8f, 1f, 0.95f), 22, 4.5f, 0.1f);
                Fx.Burst(at, new Color(0.35f, 0.85f, 0.9f), 10, 2.5f, 0.07f);
                Sfx.Play(Sfx.Land, at, 0.9f);
                Sfx.Play(Sfx.Eject, at, 0.5f);
            }
            wasInWater = inWater;
        }
    }
}
