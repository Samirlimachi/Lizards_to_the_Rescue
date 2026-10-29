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

        void Awake() { rb = GetComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate; }

        void FixedUpdate()
        {
            Vector3 target = toB ? pointB : pointA;
            Vector3 next = Vector3.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
            Vector3 d = target - rb.position; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) rb.MoveRotation(Quaternion.LookRotation(d));
            rb.MovePosition(next + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 0.05f);
            if ((next - target).sqrMagnitude < 0.01f) toB = !toB;
        }
    }
}
