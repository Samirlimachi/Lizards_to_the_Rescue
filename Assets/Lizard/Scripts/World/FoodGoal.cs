using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Meta de entrega de comida. Dos modos:
    ///  · Consumir (por defecto, LEVEL1): la comida que entra desaparece y suma para siempre.
    ///  · Acumular (<see cref="accumulate"/>, el embudo de LEVEL2): la comida se queda dentro, apilada, y la cuenta es
    ///    lo que hay dentro en cada momento (si alguien saca o se come una fruta, se resta). La que alguien sujeta con
    ///    la lengua no cuenta. En multijugador cuenta el maestro y los demás reciben el total.
    /// </summary>
    public class FoodGoal : MonoBehaviour
    {
        [Tooltip("La comida se queda dentro y cuenta mientras esté dentro (no se consume).")]
        public bool accumulate;
        [Tooltip("Con 'accumulate': radio del hueco (cilindro vertical centrado en la meta). 0 = usar la caja de colisión.")]
        public float holeRadius;

        public float Delivered { get; private set; }
        /// <summary>Acumular: entra (true) o sale (false) una comida del hueco (en todos los clientes, para efectos).</summary>
        public event Action<FoodItem, bool> OnFoodMoved;

        readonly HashSet<FoodItem> inside = new();
        readonly List<FoodItem> scratch = new();
        BoxCollider box;

        void Awake() { box = GetComponent<BoxCollider>(); }

        void OnTriggerEnter(Collider c)
        {
            if (accumulate) return;
            var f = c.GetComponentInParent<FoodItem>();
            // En multijugador cuenta solo el maestro (los demás ven copias de la comida y reciben el total por red).
            if (f && SharedFoodSync.Online && !SharedFoodSync.Master && f.GetComponent<SharedFoodSync>()) return;
            if (f && f.TryConsume(null, out float v))
            {
                Delivered += v;
                var shared=f.GetComponent<SharedFoodSync>();
                if(shared&&SharedFoodSync.Online&&SharedFoodSync.Master)SharedFoodSync.Local.RPC_FoodDelivered(shared.id,Delivered);
                Fx.Burst(transform.position + Vector3.up, new Color(1f, 0.85f, 0.2f), 25, 4f, 0.15f);
                Sfx.Play(Sfx.Help, transform.position);
                foreach (var p in LizardPlayer.All) if (p.IsControlled) p.Message($"¡Entregado! Total en la meta: {Delivered:0}");
            }
        }

        /// <summary>¿Está este punto dentro del hueco de la meta?</summary>
        public bool Contains(Vector3 world)
        {
            if (!box) return false;
            Vector3 l = transform.InverseTransformPoint(world) - box.center;
            Vector3 h = box.size * 0.5f;
            if (l.y < -h.y || l.y > h.y) return false;
            if (holeRadius > 0f) return new Vector2(l.x, l.z).magnitude <= holeRadius;
            return Mathf.Abs(l.x) <= h.x && Mathf.Abs(l.z) <= h.z;
        }

        void FixedUpdate()
        {
            if (!accumulate) return;
            // Quién está dentro ahora (todos los clientes lo calculan para los efectos; el total lo manda el maestro).
            float sum = 0f;
            scratch.Clear();
            foreach (var f in FoodItem.All)
            {
                if (!f || f.State == FoodState.Consumed || !f.edible) continue;
                bool held = f.State == FoodState.Grabbed;
                bool isIn = !held && Contains(f.Body ? f.Body.worldCenterOfMass : f.transform.position);
                if (isIn) { sum += f.nutrition; scratch.Add(f); }
            }
            foreach (var f in scratch) if (inside.Add(f)) OnFoodMoved?.Invoke(f, true);
            inside.RemoveWhere(f =>
            {
                if (f && scratch.Contains(f)) return false;
                if (f) OnFoodMoved?.Invoke(f, false);
                return true;
            });
            bool authority = !SharedFoodSync.Online || SharedFoodSync.Master;
            if (authority) Delivered = sum;
        }

        public IEnumerable<FoodItem> Inside => inside;

        /// <summary>Total que manda el maestro. Consumir: solo sube. Acumular: es exactamente lo que hay dentro.</summary>
        public void SynchronizeDelivery(float value) { Delivered = accumulate ? value : Mathf.Max(Delivered, value); }
    }
}
