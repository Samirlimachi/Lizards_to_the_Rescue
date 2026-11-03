using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Reserva de comida con registro contable. La comida expulsada sale de la reserva con su valor exacto,
    /// así que comer y expulsar repetidamente nunca crea comida nueva.
    /// </summary>
    public class LizardStomach : MonoBehaviour
    {
        LizardPlayer p;
        sealed class StoredFood
        {
            public GameObject template;
            public Vector3 scale;
            public float value;
        }
        readonly List<StoredFood> contents = new();
        Transform storage;

        Transform Storage
        {
            get
            {
                if (!storage)
                {
                    var go = new GameObject("Alimentos tragados (plantillas inactivas)");
                    go.SetActive(false);
                    go.hideFlags = HideFlags.HideInHierarchy;
                    go.transform.SetParent(transform, false);
                    storage = go.transform;
                }
                return storage;
            }
        }

        GameObject Remember(GameObject original)
        {
            if (!original) return null;
            // Instantiate below an inactive parent: templates never enter physics or FoodItem.All.
            var copy = Instantiate(original, Storage, false);
            copy.SetActive(false);
            copy.name = original.name;
            return copy;
        }

        public float Food { get; private set; }
        public float TotalIn { get; private set; }
        public float TotalOut { get; private set; }
        public float WastedOverflow { get; private set; }
        public string LastSource { get; private set; } = "";
        public event Action<float, float> OnChanged; // (antes, después)

        void Awake() => p = GetComponent<LizardPlayer>();

        public void Add(float amount, string source)
            => AddStored(amount, source, null, Vector3.one);

        /// <summary>Capture before consuming, so pending-destruction flags never enter the stored copy.</summary>
        public bool TryConsumeFood(FoodItem food)
        {
            if (!food || !food.CanBeEatenBy(p)) return false;
            var scale = food.transform.lossyScale;
            var appearance = Remember(food.gameObject);
            if (!food.TryConsume(p, out float value))
            {
                if (appearance) Destroy(appearance);
                return false;
            }
            AddStored(value, "comida", appearance, scale, true);
            return true;
        }

        void AddStored(float amount, string source, GameObject appearance, Vector3 scale, bool takeOwnership = false)
        {
            if (amount <= 0f)
            {
                if (takeOwnership && appearance) Destroy(appearance);
                return;
            }
            float before = Food;
            float room = p.Config.maxFood - Food;
            float accepted = Mathf.Min(amount, room);
            WastedOverflow += amount - accepted;
            if (accepted <= 0f)
            {
                if (takeOwnership && appearance) Destroy(appearance);
                return;
            }
            contents.Add(new StoredFood { template = takeOwnership ? appearance : Remember(appearance), scale = scale, value = accepted });
            Food += accepted;
            TotalIn += accepted;
            LastSource = source;
            OnChanged?.Invoke(before, Food);
        }

        public float Remove(float amount, string reason)
            => Extract(amount, reason, null);

        float Extract(float amount, string reason, Action<StoredFood, float> onPortion)
        {
            float before = Food;
            float r = Mathf.Clamp(amount, 0f, Food);
            float left = r;
            while (left > 0.00001f && contents.Count > 0)
            {
                var entry = contents[contents.Count - 1];
                float portion = Mathf.Min(left, entry.value);
                onPortion?.Invoke(entry, portion);
                entry.value -= portion;
                left -= portion;
                if (entry.value <= 0.00001f)
                {
                    if (entry.template) Destroy(entry.template);
                    contents.RemoveAt(contents.Count - 1);
                }
            }
            Food -= r;
            if (Food < 0.0001f) Food = 0f;
            TotalOut += r;
            LastSource = reason;
            if (r > 0f) OnChanged?.Invoke(before, Food);
            return r;
        }

        /// <summary>Compatibility entry point. Now returns food models instead of green pellets.</summary>
        public int EjectPellets(float amount, Vector3 dir, float speed, float spread)
            => EjectFoods(amount, dir, speed, spread);

        /// <summary>Voluntary spit: one complete stored item, last swallowed first.</summary>
        public int EjectLastFood(Vector3 dir, float speed, float spread)
        {
            if (contents.Count == 0) return 0;
            var last = contents[contents.Count - 1];
            float amount = last.template ? last.value : Mathf.Min(last.value, p.Config.voluntaryEjectAmount);
            return EjectFoods(amount, dir, speed, spread);
        }

        /// <summary>Damage can expel a fraction; world nutrition always equals the exact deducted value.</summary>
        public int EjectFoods(float amount, Vector3 dir, float speed, float spread)
        {
            int count = 0;
            Extract(amount, "expulsión", (entry, value) =>
            {
                Vector3 d = (dir + UnityEngine.Random.insideUnitSphere * spread).normalized;
                if (d.sqrMagnitude < 0.01f) d = transform.forward;
                FoodItem food;
                if (entry.template)
                {
                    var go = Instantiate(entry.template);
                    go.name = entry.template.name;
                    go.transform.localScale = entry.scale;
                    food = go.GetComponent<FoodItem>();
                    food.enabled = true;
                    food.nutrition = value;
                    var body = go.GetComponent<Rigidbody>();
                    body.isKinematic = false;
                    body.detectCollisions = true;
                    go.transform.rotation = transform.rotation;
                    go.transform.position = p.MouthPosition + d * (0.30f + food.size);
                    go.SetActive(true);
                }
                else
                {
                    // Starting/debug food has no consumed model; use an apple, never a green pellet.
                    food = FoodFactory.CreateFruit(p.MouthPosition + d * 0.5f, value, 0.18f);
                }
                var cfg = p.Config;
                food.MarkEjected(p, cfg.pelletPickupDelay, cfg.pelletOwnerPickupDelay);
                food.Body.linearVelocity = p.Body.linearVelocity * 0.5f + d * speed;
                count++;
            });
            return count;
        }

        /// <summary>Feeding transfers the matching appearance as well as the nutrition.</summary>
        public float TransferTo(LizardStomach target, float amount)
        {
            if (!target || target == this) return 0f;
            float accepted = Mathf.Min(amount, Mathf.Max(0f, target.p.Config.maxFood - target.Food));
            return Extract(accepted, "alimentar", (entry, value) =>
                target.AddStored(value, "alimentado por " + p.displayName, entry.template, entry.scale));
        }

        public void DebugSetFood(float value)
        {
            value = Mathf.Clamp(value, 0f, p.Config.maxFood);
            if (Mathf.Approximately(value, Food)) return;
            foreach (var entry in contents) if (entry.template) Destroy(entry.template);
            contents.Clear();
            float before = Food;
            Food = value;
            if (Food > 0f) contents.Add(new StoredFood { value = Food, scale = Vector3.one });
            LastSource = "depuración";
            OnChanged?.Invoke(before, Food);
        }
    }
}
