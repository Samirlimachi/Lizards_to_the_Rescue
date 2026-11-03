using UnityEngine;

namespace Lizards
{
    public class LevelOneMotion : MonoBehaviour
    {
        public Vector3 travel = new Vector3(5, 0, 0);
        public float period = 6;
        public Vector3 spin;
        Rigidbody body;
        Vector3 origin;
        void Awake() { origin = transform.position; body = GetComponent<Rigidbody>(); }
        void FixedUpdate()
        {
            if (spin.sqrMagnitude > 0) transform.Rotate(spin * Time.fixedDeltaTime, Space.Self);
            if (body && body.isKinematic)
                body.MovePosition(origin + travel * (.5f - .5f * Mathf.Cos(Time.time * Mathf.PI * 2 / Mathf.Max(1, period))));
        }
    }
}
