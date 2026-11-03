using System;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Pieza blanda con muelle (bracito, patita, colita, cara de la bola): reacciona a la inercia del cuerpo
    /// y puede tener un movimiento propio periódico (aletear, pedalear, menear). Al pararse vuelve a su sitio.
    /// </summary>
    [Serializable]
    public class SoftPart
    {
        public Transform target;
        [Tooltip("Frecuencia del muelle (Hz).")]
        public float frequency = 3f;
        [Range(0.05f, 1.5f)] public float dampingRatio = 0.35f;
        [Tooltip("Grados por m/s² de aceleración del cuerpo.")]
        public float inertiaGain = 3.5f;
        [Tooltip("Movimiento propio al rodar: eje local, amplitud (grados) y velocidad.")]
        public Vector3 flapAxis = Vector3.forward;
        public float flapDegrees = 25f;
        public float flapSpeed = 14f;
        public float flapPhase;
        public float maxDegrees = 55f;

        Quaternion rest; Vector3 restScale;
        Vector3 angle, angVel;
        bool ready;

        public void Init()
        {
            if (!target) return;
            rest = target.localRotation; restScale = target.localScale;
            angle = angVel = Vector3.zero;
            ready = true;
        }

        /// <summary>'accelWorld' = aceleración del cuerpo (m/s²); 'activity' 0..1 = cuánto se mueve (rodando).</summary>
        public void Step(float dt, Vector3 accelWorld, float activity, float time, float squash = 0f)
        {
            if (!ready || !target || dt <= 0f) return;
            dt = Mathf.Min(dt, 1f / 30f);
            // La inercia empuja la pieza en sentido contrario a la aceleración (se queda atrás al arrancar).
            Vector3 local = target.parent ? target.parent.InverseTransformDirection(-accelWorld) : -accelWorld;
            Vector3 goal = new Vector3(local.z, 0f, -local.x) * inertiaGain;
            goal += flapAxis.normalized * Mathf.Sin(time * flapSpeed + flapPhase) * flapDegrees * activity;

            float w = 2f * Mathf.PI * frequency;
            int sub = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 6);
            float h = dt / sub;
            for (int i = 0; i < sub; i++)
            {
                Vector3 acc = w * w * (goal - angle) - 2f * dampingRatio * w * angVel;
                angVel += acc * h;
                angle += angVel * h;
            }
            angle = Vector3.ClampMagnitude(angle, maxDegrees);
            target.localRotation = rest * Quaternion.Euler(angle);
            if (squash != 0f) target.localScale = new Vector3(restScale.x * (1f + squash * 0.5f), restScale.y * (1f - squash), restScale.z * (1f + squash * 0.5f));
        }

        /// <summary>Rotación extra actual (para piezas cuya rotación base calcula otro sistema, como la cabeza).</summary>
        public Quaternion Offset => Quaternion.Euler(angle);

        /// <summary>Igual que Step pero sin escribir en el transform (la cabeza la combina con la boca).</summary>
        public void Simulate(float dt, Vector3 accelLocal, float activity, float time)
        {
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 1f / 30f);
            Vector3 goal = new Vector3(accelLocal.z, 0f, -accelLocal.x) * inertiaGain + flapAxis.normalized * Mathf.Sin(time * flapSpeed + flapPhase) * flapDegrees * activity;
            float w = 2f * Mathf.PI * frequency;
            int sub = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 6);
            float h = dt / sub;
            for (int i = 0; i < sub; i++)
            {
                Vector3 acc = w * w * (goal - angle) - 2f * dampingRatio * w * angVel;
                angVel += acc * h;
                angle += angVel * h;
            }
            angle = Vector3.ClampMagnitude(angle, maxDegrees);
        }

        public void Kick(Vector3 degreesPerSecond) => angVel += degreesPerSecond;
    }
}
