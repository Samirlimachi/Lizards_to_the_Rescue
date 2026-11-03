using UnityEngine;

namespace Lizards
{
    /// <summary>Hace que un modelo siga a otro objeto (posición y giro) con un desfase fijo. Útil para visuales separadas de un cuerpo físico.</summary>
    public class FollowTransform : MonoBehaviour
    {
        public Transform target;
        public Vector3 localOffset;
        Quaternion rotOffset = Quaternion.identity;
        bool init;

        void LateUpdate()
        {
            if (!target) return;
            if (!init) { localOffset = Quaternion.Inverse(target.rotation) * (transform.position - target.position); rotOffset = Quaternion.Inverse(target.rotation) * transform.rotation; init = true; }
            transform.SetPositionAndRotation(target.position + target.rotation * localOffset, target.rotation * rotOffset);
        }
    }
}
