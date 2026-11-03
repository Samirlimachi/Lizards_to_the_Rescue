using UnityEngine;

namespace Lizards
{
    /// <summary>Campana colgante: al tocarla se balancea con muelle y el badajo va un poco detrás.</summary>
    public class BellSwing : MonoBehaviour
    {
        public Transform clapper;
        float angle, vel, clapAngle, clapVel;

        public void Ring() { vel += 260f; }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            vel += (-angle * 40f - vel * 2.2f) * dt; angle += vel * dt;
            clapVel += ((angle * 1.4f - clapAngle) * 70f - clapVel * 5f) * dt; clapAngle += clapVel * dt;
            float idle = Mathf.Sin(Time.time * 1.3f) * 1.5f;
            transform.localRotation = Quaternion.Euler(angle + idle, 0f, 0f);
            if (clapper) clapper.localRotation = Quaternion.Euler(clapAngle - angle, 0f, 0f);
        }
    }
}
