using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    public enum FoodState { Available, Grabbed, Consumed }

    /// <summary>
    /// Alimento con valor nutricional, tamaño y masa. Solo un jugador puede reclamarlo a la vez
    /// y solo puede consumirse una vez (el estado Consumed es definitivo).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FoodItem : MonoBehaviour
    {
        public float nutrition = 1f;
        [Tooltip("Radio aproximado; se compara con la capacidad de la boca.")]
        public float size = 0.2f;
        public bool edible = true;

        public static readonly List<FoodItem> All = new();
        public static event Action<FoodItem, LizardPlayer> OnConsumed;

        public FoodState State { get; private set; } = FoodState.Available;
        public LizardPlayer Holder { get; private set; }
        public Rigidbody Body { get; private set; }

        float noPickupUntil, ownerNoPickupUntil;
        LizardPlayer ejectedBy;

        void Awake() { Body = GetComponent<Rigidbody>(); }
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool FitsMouth(float capacity) => size <= capacity + 0.0001f;

        public bool CanBeEatenBy(LizardPlayer p)
        {
            if (!edible || State == FoodState.Consumed) return false;
            if (State == FoodState.Grabbed && Holder != p) return false;
            if (Time.time < noPickupUntil) return false;
            if (p == ejectedBy && Time.time < ownerNoPickupUntil) return false;
            return true;
        }

        public bool CanBeGrabbedBy(LizardPlayer p) =>
            State == FoodState.Available || (State == FoodState.Grabbed && Holder == p);

        public bool TryGrab(LizardPlayer p)
        {
            if (!CanBeGrabbedBy(p)) return false;
            State = FoodState.Grabbed; Holder = p;
            return true;
        }

        public void ReleaseGrab(LizardPlayer p)
        {
            if (State == FoodState.Grabbed && Holder == p) { State = FoodState.Available; Holder = null; }
        }

        /// <summary>Consumo atómico: devuelve el valor solo la primera vez.</summary>
        public bool TryConsume(LizardPlayer eater, out float value)
        {
            value = 0f;
            if (eater && !CanBeEatenBy(eater)) return false;
            if (!eater && State == FoodState.Consumed) return false;
            value = nutrition;
            nutrition = 0f;
            State = FoodState.Consumed;
            Holder = null;
            OnConsumed?.Invoke(this, eater);
            gameObject.SetActive(false);
            Destroy(gameObject);
            return true;
        }

        public void MarkEjected(LizardPlayer by, float delay, float ownerDelay)
        {
            ejectedBy = by;
            noPickupUntil = Time.time + delay;
            ownerNoPickupUntil = Time.time + ownerDelay;
        }

        public static float TotalInWorld()
        {
            float t = 0f;
            foreach (var f in All) if (f.State != FoodState.Consumed) t += f.nutrition;
            return t;
        }
    }
}
