using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Meta de entrega: consume la comida que entra y lleva la cuenta.</summary>
    public class FoodGoal : MonoBehaviour
    {
        public float Delivered { get; private set; }
        void OnTriggerEnter(Collider c)
        {
            var f = c.GetComponentInParent<FoodItem>();
            if (f && f.TryConsume(null, out float v))
            {
                Delivered += v;
                Fx.Burst(transform.position + Vector3.up, new Color(1f, 0.85f, 0.2f), 25, 4f, 0.15f);
                Sfx.Play(Sfx.Help, transform.position);
                foreach (var p in LizardPlayer.All) if (p.IsControlled) p.Message($"¡Entregado! Total en la meta: {Delivered:0}");
            }
        }
    }
}
