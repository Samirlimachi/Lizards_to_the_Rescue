using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Patrulla entre dos puntos (Rigidbody cinemático). Se arrastra como un gusano (<see cref="SlugCrawl"/>) y gira
    /// suave al dar la vuelta. Al recibir un golpe se queda congelada un instante (pausa de impacto) y luego huye
    /// más rápido, dándose la vuelta si el golpe vino de delante.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PatrolEnemy : MonoBehaviour
    {
        public Vector3 pointA, pointB;
        public float speed = 2f;
        [Tooltip("Velocidad al huir tras un golpe (multiplicador).")]
        public float fleeMultiplier = 2f;
        [Tooltip("Velocidad de giro al darse la vuelta (grados por segundo).")]
        public float turnSpeed = 360f;
        Rigidbody rb; bool toB = true;
        Vector3 basePos; // posición sobre la ruta
        float stunUntil, fleeUntil;

        void Awake()
        {
            rb = GetComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate; basePos = transform.position;
            if (!GetComponent<SlugCrawl>()) gameObject.AddComponent<SlugCrawl>();   // avanza estirándose como un gusano
        }

        void OnEnable() { basePos = transform.position; }

        /// <summary>Golpe recibido: pausa de impacto y huida (se da la vuelta si el golpe vino de delante).</summary>
        public void React(Vector3 from, float stun, float flee)
        {
            stunUntil = Time.time + stun;
            fleeUntil = stunUntil + flee;
            Vector3 travel = (toB ? pointB : pointA) - basePos; travel.y = 0f;
            Vector3 toHit = from - basePos; toHit.y = 0f;
            if (travel.sqrMagnitude > 0.01f && Vector3.Dot(travel, toHit) > 0f) toB = !toB;
        }

        public bool Fleeing => Time.time < fleeUntil;

        void FixedUpdate()
        {
            if (Time.time < stunUntil) return;   // congelada por el impacto
            Vector3 target = toB ? pointB : pointA;
            float sp = speed * (Fleeing ? fleeMultiplier : 1f);
            basePos = Vector3.MoveTowards(basePos, target, sp * Time.fixedDeltaTime);
            Vector3 d = target - basePos; d.y = 0f;
            if (d.sqrMagnitude > 0.001f)
                rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, Quaternion.LookRotation(d), turnSpeed * (Fleeing ? 2f : 1f) * Time.fixedDeltaTime));
            rb.MovePosition(basePos);
            if ((basePos - target).sqrMagnitude < 0.01f) toB = !toB;
        }
    }
}
