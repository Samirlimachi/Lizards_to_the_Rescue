using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    public class Checkpoint : MonoBehaviour
    {
        void OnTriggerEnter(Collider c)
        {
            var p = c.GetComponentInParent<LizardPlayer>();
            if (p) p.SetCheckpoint(transform.position);
        }
    }
}
