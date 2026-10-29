using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Mantiene un alimento disponible en este punto (fuente de diseño de nivel, no duplicación).</summary>
    public class FoodSpawner : MonoBehaviour
    {
        public float respawnDelay = 3f;
        public float value = 1f;
        public float radius = 0.18f;
        FoodItem current; float nextSpawn;

        void Update()
        {
            if (current && current.State != FoodState.Consumed) { nextSpawn = Time.time + respawnDelay; return; }
            if (Time.time >= nextSpawn)
            {
                current = FoodFactory.CreateFruit(transform.position + Vector3.up * 0.4f, value, radius);
                nextSpawn = Time.time + respawnDelay;
            }
        }
    }
}
