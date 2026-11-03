using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Bolitas de polvo estilo cartoon (con contorno) que salen de los pies: crecen un instante,
    /// suben un poco y se encogen hasta desaparecer. Usa un grupo de objetos reutilizables.
    /// </summary>
    public class DustPuffs : MonoBehaviour
    {
        struct Puff { public Transform t; public float age, life, size; public Vector3 vel; }

        static DustPuffs instance;
        static Material mat;
        static Mesh sphere;

        readonly List<Puff> live = new();
        readonly Stack<Transform> pool = new();

        static DustPuffs Get()
        {
            if (instance) return instance;
            var go = new GameObject("Polvo (efectos)");
            instance = go.AddComponent<DustPuffs>();
            return instance;
        }

        /// <summary>Lanza una bolita. 'size' es el diámetro máximo en metros.</summary>
        public static void Spawn(Vector3 pos, float size, Vector3 velocity, float life = 0.45f)
        {
            if (!Application.isPlaying) return;
            Get().Add(pos, size, velocity, life);
        }

        public static void Burst(Vector3 pos, float size, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0.25f, Mathf.Sin(a));
                Spawn(pos + dir * size * 0.3f, size * Random.Range(0.7f, 1.1f), dir * spread, Random.Range(0.4f, 0.55f));
            }
        }

        void Add(Vector3 pos, float size, Vector3 vel, float life)
        {
            if (!mat) mat = LizardMaterials.Toon(new Color(0.97f, 0.97f, 0.95f), 0.0025f, 0f, new Color(0.8f, 0.82f, 0.9f));
            if (!sphere)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
                Destroy(tmp);
            }
            Transform t;
            if (pool.Count > 0) { t = pool.Pop(); t.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("Bolita");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = sphere;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                t = go.transform;
            }
            t.position = pos;
            t.localScale = Vector3.zero;
            live.Add(new Puff { t = t, age = 0f, life = life, size = size, vel = vel });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = live[i];
                p.age += dt;
                float k = p.age / p.life;
                if (k >= 1f) { p.t.gameObject.SetActive(false); pool.Push(p.t); live.RemoveAt(i); continue; }
                // Crece rápido y se encoge despacio; sube y frena.
                float s = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
                p.t.localScale = Vector3.one * p.size * Mathf.Max(0f, s);
                p.vel *= Mathf.Exp(-dt * 5f);
                p.t.position += (p.vel + Vector3.up * 0.35f) * dt;
                live[i] = p;
            }
        }
    }
}
