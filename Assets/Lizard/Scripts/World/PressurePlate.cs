using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Placa de peso: se activa cuando la masa total encima supera el umbral.
    /// En multijugador decide el cliente maestro (ver <see cref="LevelNet"/>) y los demás solo copian el estado,
    /// así la puerta se abre a la vez para todos. Cuenta también los cuerpos cinemáticos que son copias de red
    /// (jugadores remotos y cajas de otro dueño), que de verdad están encima.
    /// </summary>
    public class PressurePlate : MonoBehaviour
    {
        public float requiredMass = 40f;
        public Door[] doors;
        public Transform plateVisual;
        readonly HashSet<Rigidbody> bodies = new();
        public float CurrentMass { get; private set; }
        public bool Pressed { get; private set; }
        /// <summary>Copia de red: el estado lo manda el maestro con <see cref="NetSetPressed"/>.</summary>
        public bool NetReplica { get; set; }
        public event Action<bool> OnPressedChanged;
        bool netPressed;
        Renderer plateRend; MaterialPropertyBlock mpb; Vector3 plateRest;

        void Awake() { if (plateVisual) { plateRend = plateVisual.GetComponent<Renderer>(); plateRest = plateVisual.localPosition; } mpb = new MaterialPropertyBlock(); }

        public void NetSetPressed(bool pressed) => netPressed = pressed;

        void FixedUpdate()
        {
            CurrentMass = 0f;
            foreach (var b in bodies) if (b) CurrentMass += b.mass;
            bodies.Clear();
            bool now = NetReplica ? netPressed : CurrentMass >= requiredMass;
            if (!NetReplica) netPressed = now;
            if (now != Pressed)
            {
                Pressed = now;
                foreach (var d in doors) if (d) d.SetOpen(now);
                Sfx.Play(now ? Sfx.Stick : Sfx.Land, transform.position);
                OnPressedChanged?.Invoke(now);
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
            if (!b) return;
            if (!b.isKinematic || NetProp.IsNetDynamic(b) || b.GetComponent<LizardNetSync>()) bodies.Add(b);
        }
    }
}
