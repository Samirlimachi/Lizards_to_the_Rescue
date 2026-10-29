using System;
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

        public float Food { get; private set; }
        public float TotalIn { get; private set; }
        public float TotalOut { get; private set; }
        public float WastedOverflow { get; private set; }
        public string LastSource { get; private set; } = "";
        public event Action<float, float> OnChanged; // (antes, después)

        void Awake() => p = GetComponent<LizardPlayer>();

        public void Add(float amount, string source)
        {
            if (amount <= 0f) return;
            float before = Food;
            float room = p.Config.maxFood - Food;
            float accepted = Mathf.Min(amount, room);
            WastedOverflow += amount - accepted;
            Food += accepted;
            TotalIn += accepted;
            LastSource = source;
            OnChanged?.Invoke(before, Food);
        }

        public float Remove(float amount, string reason)
        {
            float before = Food;
            float r = Mathf.Clamp(amount, 0f, Food);
            Food -= r;
            if (Food < 0.0001f) Food = 0f;
            TotalOut += r;
            LastSource = reason;
            if (r > 0f) OnChanged?.Invoke(before, Food);
            return r;
        }

        /// <summary>Expulsa comida como bolitas físicas. El total de las bolitas es exactamente lo restado.</summary>
        public int EjectPellets(float amount, Vector3 dir, float speed, float spread)
        {
            float removed = Remove(amount, "expulsión");
            if (removed <= 0f) return 0;
            var cfg = p.Config;
            int count = Mathf.Max(1, Mathf.CeilToInt(removed / cfg.pelletValue - 0.001f));
            float left = removed;
            for (int i = 0; i < count; i++)
            {
                float v = i == count - 1 ? left : Mathf.Min(cfg.pelletValue, left);
                left -= v;
                Vector3 d = (dir + UnityEngine.Random.insideUnitSphere * spread).normalized;
                Vector3 pos = p.MouthPosition + d * (0.25f + cfg.pelletSize) * Mathf.Max(1f, p.Stats.visualScale * 0.5f);
                var food = FoodFactory.CreatePellet(pos, v, cfg.pelletSize);
                food.MarkEjected(p, cfg.pelletPickupDelay, cfg.pelletOwnerPickupDelay);
                food.Body.linearVelocity = p.Body.linearVelocity * 0.5f + d * speed;
            }
            return count;
        }

        public void DebugSetFood(float value)
        {
            float before = Food;
            Food = Mathf.Clamp(value, 0f, p.Config.maxFood);
            LastSource = "depuración";
            OnChanged?.Invoke(before, Food);
        }
    }
}
