using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Patrulla entre dos puntos (Rigidbody cinemático).</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PatrolEnemy : MonoBehaviour
    {
        public Vector3 pointA, pointB;
        public float speed = 2f;
        Rigidbody rb; bool toB = true;
        Vector3 basePos; // posición sobre la ruta, sin el saltito (si se usara rb.position el saltito se acumulaba y subía al cielo)

        void Awake() { rb = GetComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate; basePos = transform.position; }

        void FixedUpdate()
        {
            Vector3 target = toB ? pointB : pointA;
            basePos = Vector3.MoveTowards(basePos, target, speed * Time.fixedDeltaTime);
            Vector3 d = target - basePos; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) rb.MoveRotation(Quaternion.LookRotation(d));
            rb.MovePosition(basePos + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.05f);
            if ((basePos - target).sqrMagnitude < 0.01f) toB = !toB;
        }
    }
}
