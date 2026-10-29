using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Recuperación individual: devuelve a la forma normal al renacuajo que entra.</summary>
    public class RescuePoint : MonoBehaviour
    {
        void OnTriggerEnter(Collider c) => Try(c);
        void OnTriggerStay(Collider c) => Try(c);
        void Try(Collider c)
        {
            var p = c.GetComponentInParent<LizardPlayer>();
            if (p && p.Form == LizardForm.Small) p.Rescue("punto de rescate");
        }
        void Update() => transform.Rotate(0f, 60f * Time.deltaTime, 0f);
    }
}
