using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Placa de peso: se activa cuando la masa total encima supera el umbral.</summary>
    public class PressurePlate : MonoBehaviour
    {
        public float requiredMass = 40f;
        public Door[] doors;
        public Transform plateVisual;
        readonly HashSet<Rigidbody> bodies = new();
        public float CurrentMass { get; private set; }
        public bool Pressed { get; private set; }
        Renderer plateRend; MaterialPropertyBlock mpb; Vector3 plateRest;

        void Awake() { if (plateVisual) { plateRend = plateVisual.GetComponent<Renderer>(); plateRest = plateVisual.localPosition; } mpb = new MaterialPropertyBlock(); }

        void FixedUpdate()
        {
            CurrentMass = 0f;
            foreach (var b in bodies) if (b) CurrentMass += b.mass;
            bodies.Clear();
            bool now = CurrentMass >= requiredMass;
            if (now != Pressed)
            {
                Pressed = now;
                foreach (var d in doors) if (d) d.SetOpen(now);
                Sfx.Play(now ? Sfx.Stick : Sfx.Land, transform.position);
            }
            if (plateVisual)
            {
                plateVisual.localPosition = plateRest + Vector3.down * (Pressed ? 0.12f : Mathf.Clamp01(CurrentMass / requiredMass) * 0.05f);
                if (plateRend) { mpb.SetColor("_BaseColor", Pressed ? new Color(0.3f, 1f, 0.4f) : new Color(0.3f, 0.5f, 1f)); plateRend.SetPropertyBlock(mpb); }
            }
        }

        void OnTriggerStay(Collider c)
        {
            var b = c.attachedRigidbody;
            if (b && !b.isKinematic) bodies.Add(b);
        }
    }
}
