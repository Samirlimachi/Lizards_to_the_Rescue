using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Piezas articuladas del Jefe (las rellena JefeModel y las anima JefeController).
    /// </summary>
    public class JefeRig : MonoBehaviour
    {
        public Transform torso, head, armL, armR, elbowL, elbowR, sac, crown;
        public Transform[] lids = new Transform[0], irises = new Transform[0];
        public float restOpen = 14f;
    }
}
