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
        [Tooltip("Optional modeled food. Existing spawners still use their original fruit when empty.")]
        public FoodItem prefab;
        FoodItem current; float nextSpawn; int serial;

        void Update()
        {
            if (current && current.State != FoodState.Consumed) { nextSpawn = Time.time + respawnDelay; return; }
            // En sala, la fruta generada a mano no existiría en los demás: se usa la manzana de red.
            if (!prefab && SharedFoodSync.Online) prefab = Resources.Load<FoodItem>("NetFood/Apple");
            // En multijugador solo el maestro hace aparecer comida compartida y la anuncia a los demás.
            var sync = prefab ? prefab.GetComponent<SharedFoodSync>() : null;
            if (sync && SharedFoodSync.Online && !SharedFoodSync.Master) { current = null; return; }
            if (Time.time >= nextSpawn)
            {
                current = prefab ? Instantiate(prefab, transform.position + Vector3.up * 0.4f, Quaternion.identity)
                    : FoodFactory.CreateFruit(transform.position + Vector3.up * 0.4f, value, radius);
                if(prefab)current.nutrition=value;
                var s = current.GetComponent<SharedFoodSync>();
                if (s)
                {
                    var p = transform.position;
                    s.Bind($"cesta{Mathf.RoundToInt(p.x * 10)}_{Mathf.RoundToInt(p.z * 10)}-{serial++}-{System.Guid.NewGuid().ToString("N").Substring(0, 6)}");
                    if (SharedFoodSync.Online && SharedFoodSync.Master)
                        SharedFoodSync.Local.RPC_FoodSpawn(s.id, s.variant, current.transform.position, current.transform.rotation, current.nutrition, current.Body.mass, Vector3.zero);
                }
                nextSpawn = Time.time + respawnDelay;
            }
        }
    }
}
