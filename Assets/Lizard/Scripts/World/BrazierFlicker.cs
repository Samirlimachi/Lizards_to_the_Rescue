using UnityEngine;

namespace Lizards
{
    /// <summary>Fuego de brasero: llamas que laten y suben, y una luz que parpadea.</summary>
    public class BrazierFlicker : MonoBehaviour
    {
        public Transform[] flames = new Transform[0];
        public Light glow;
        public float baseIntensity = 2.5f;
        Vector3[] baseScale;
        float seed;

        void Start()
        {
            seed = Random.value * 100f;
            baseScale = new Vector3[flames.Length];
            for (int i = 0; i < flames.Length; i++) baseScale[i] = flames[i].localScale;
        }

        void Update()
        {
            float t = Time.time + seed;
            for (int i = 0; i < flames.Length; i++)
            {
                float k = 1f + Mathf.Sin(t * (7f + i * 2.3f)) * 0.15f + Mathf.PerlinNoise(t * 3f, i) * 0.2f;
                flames[i].localScale = new Vector3(baseScale[i].x * (2f - k) * 0.9f + baseScale[i].x * 0.1f, baseScale[i].y * k, baseScale[i].z * (2f - k) * 0.9f + baseScale[i].z * 0.1f);
                flames[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 5f + i) * 6f, t * 40f * (i % 2 == 0 ? 1 : -1), Mathf.Cos(t * 4f + i) * 6f);
            }
            if (glow) glow.intensity = baseIntensity * (0.8f + Mathf.PerlinNoise(t * 6f, 0.3f) * 0.4f);
        }
    }
}
