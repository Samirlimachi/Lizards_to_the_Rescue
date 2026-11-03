using System;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Cadena de huesos blanda ("fideo"): cada hueso apunta a un punto con inercia que sigue a su pose de
    /// reposo mediante un muelle amortiguado. Se balancea con el movimiento y se queda quieta al parar.
    /// Es solo visual; usa subpasos fijos y un ángulo máximo para que nunca se descontrole.
    /// </summary>
    [Serializable]
    public class NoodleChain
    {
        public Transform[] bones;
        [Tooltip("Punta virtual de la cadena, en el espacio del último hueso.")]
        public Vector3 tipLocal = new Vector3(0.15f, 0f, 0f);
        [Tooltip("Rotación extra de reposo del primer hueso (p. ej. brazos colgando).")]
        public Vector3 restEuler;
        [Tooltip("Frecuencia del muelle en Hz: más baja = más blando y lento.")]
        public float frequency = 2.4f;
        [Tooltip("0 = rebota sin fin, 1 = sin rebote. Entre 0.4 y 0.7 queda natural.")]
        [Range(0.05f, 1.5f)] public float dampingRatio = 0.55f;
        [Tooltip("Caída extra hacia abajo cuando se balancea.")]
        public float gravity = 1.5f;
        [Tooltip("Empujón por paso al correr (bamboleo cómico).")]
        public float wobble = 0.6f;
        [Tooltip("Ángulo máximo que puede separarse de la pose de reposo.")]
        public float maxAngle = 70f;

        Quaternion[] restLocal;
        Vector3[] restTip, pos, vel;
        bool ready;

        public void Init(Transform frameUnused = null)
        {
            if (bones == null || bones.Length == 0) return;
            int n = bones.Length;
            if (restLocal == null || restLocal.Length != n)
            {
                restLocal = new Quaternion[n];
                for (int i = 0; i < n; i++) restLocal[i] = bones[i].localRotation;
            }
            restTip = new Vector3[n]; pos = new Vector3[n]; vel = new Vector3[n];
            for (int i = 0; i < n; i++) restTip[i] = i < n - 1 ? bones[i + 1].localPosition : tipLocal;
            ApplyRest();
            for (int i = 0; i < n; i++) { pos[i] = bones[i].TransformPoint(restTip[i]); vel[i] = Vector3.zero; }
            ready = true;
        }

        /// <summary>Pone la cadena en su pose de reposo sin simular (para vista previa).</summary>
        public void ApplyRest()
        {
            for (int i = 0; i < bones.Length; i++)
                bones[i].localRotation = restLocal[i] * (i == 0 ? Quaternion.Euler(restEuler) : Quaternion.identity);
        }

        public void Reset()
        {
            if (!ready) return;
            ApplyRest();
            for (int i = 0; i < bones.Length; i++) { pos[i] = bones[i].TransformPoint(restTip[i]); vel[i] = Vector3.zero; }
        }

        /// <summary>Avanza la simulación. 'kick' es una aceleración extra (m/s²) que crece hacia la punta.</summary>
        public void Step(float dt, Vector3 kick)
        {
            if (!ready || dt <= 0f) return;
            dt = Mathf.Min(dt, 1f / 20f);
            int sub = Mathf.Clamp(Mathf.CeilToInt(dt * 120f), 1, 8);
            float h = dt / sub;
            float w = 2f * Mathf.PI * frequency;
            int n = bones.Length;

            ApplyRest();
            for (int i = 0; i < n; i++)
            {
                Transform b = bones[i];
                Vector3 origin = b.position;
                Vector3 target = b.TransformPoint(restTip[i]);
                Vector3 restVec = target - origin;
                float len = restVec.magnitude;
                if (len < 1e-5f) continue;
                Vector3 restDir = restVec / len;

                // Si algo teletransportó al personaje, recoloca sin latigazo.
                if ((pos[i] - target).sqrMagnitude > len * len * 9f) { pos[i] = target; vel[i] = Vector3.zero; }

                float along = (i + 1f) / n;
                for (int s = 0; s < sub; s++)
                {
                    Vector3 acc = w * w * (target - pos[i]) - 2f * dampingRatio * w * vel[i]
                                  + Vector3.down * gravity + kick * along;
                    vel[i] += acc * h;
                    pos[i] += vel[i] * h;
                }

                Vector3 dir = pos[i] - origin;
                if (dir.sqrMagnitude < 1e-8f) continue;
                dir.Normalize();
                float ang = Vector3.Angle(restDir, dir);
                if (ang > maxAngle) dir = Vector3.Slerp(restDir, dir, maxAngle / ang);
                pos[i] = origin + dir * len;
                b.rotation = Quaternion.FromToRotation(restDir, dir) * b.rotation;
            }
        }
    }
}
