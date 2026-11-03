using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Caldero burbujeante: burbujas verdes que suben por el líquido, crecen y revientan.</summary>
    public class CauldronBubbles : MonoBehaviour
    {
        public float radius = 3.6f;
        public Material bubbleMat;
        public int maxBubbles = 14;

        struct Bubble { public Transform t; public float age, life, size; }
        readonly List<Bubble> bubbles = new();
        Mesh sphere;
        float next;

        void Start()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }

        void Update()
        {
            if (Time.time >= next && bubbles.Count < maxBubbles)
            {
                next = Time.time + Random.Range(0.08f, 0.3f);
                var go = new GameObject("Burbuja");
                go.transform.SetParent(transform, false);
                Vector2 c = Random.insideUnitCircle * radius;
                go.transform.localPosition = new Vector3(c.x, 0f, c.y);
                go.AddComponent<MeshFilter>().sharedMesh = sphere;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = bubbleMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bubbles.Add(new Bubble { t = go.transform, life = Random.Range(0.6f, 1.4f), size = Random.Range(0.25f, 0.7f) });
            }
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                b.age += Time.deltaTime;
                float k = b.age / b.life;
                if (k >= 1f)
                {
                    Fx.Burst(b.t.position, new Color(0.55f, 1f, 0.3f), 4, 1.2f, 0.06f);
                    Destroy(b.t.gameObject); bubbles.RemoveAt(i); continue;
                }
                float s = b.size * Mathf.Sin(Mathf.Min(1f, k * 1.3f) * Mathf.PI * 0.5f);
                b.t.localScale = new Vector3(s, s * 0.7f, s);
                b.t.localPosition += Vector3.up * Time.deltaTime * 0.08f;
                bubbles[i] = b;
            }
        }
    }
}
