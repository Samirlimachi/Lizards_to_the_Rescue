using UnityEngine;

namespace Lizards
{
    /// <summary>Espuma donde cae una cascada: salpicaduras continuas y un anillo de espuma que late.</summary>
    public class WaterfallSplash : MonoBehaviour
    {
        public Transform foam;
        public Color color = new Color(0.9f, 1f, 1f);
        public float radius = 0.6f;
        float next; Vector3 foamScale;

        void Start() { if (foam) foamScale = foam.localScale; }

        void Update()
        {
            float t = Time.time;
            if (t >= next)
            {
                next = t + 0.09f;
                Vector2 o = Random.insideUnitCircle * radius;
                Fx.Burst(transform.position + new Vector3(o.x, 0.05f, o.y), color, 2, 1.8f, 0.09f);
            }
            if (foam)
            {
                float k = 1f + Mathf.Sin(t * 5f) * 0.08f + Mathf.Sin(t * 8.3f) * 0.05f;
                foam.localScale = new Vector3(foamScale.x * k, foamScale.y, foamScale.z * k);
                foam.localRotation = Quaternion.Euler(0f, t * 25f, 0f);
            }
        }
    }
}
