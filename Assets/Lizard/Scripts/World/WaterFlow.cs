using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Agua que se mueve: deforma la malla cada frame.
    /// - Cortina: ondas que bajan (parece que cae).
    /// - Lámina: ondas circulares que se abren desde el centro.
    /// </summary>
    public class WaterFlow : MonoBehaviour
    {
        public enum Kind { Curtain, Surface }
        public Kind kind = Kind.Surface;
        public float amplitude = 0.03f, frequency = 4f, speed = 3f;

        Mesh mesh; Vector3[] baseVerts, verts;

        void Start()
        {
            var mf = GetComponent<MeshFilter>();
            if (!mf || !mf.sharedMesh) { enabled = false; return; }
            mesh = Instantiate(mf.sharedMesh);
            mf.sharedMesh = mesh;
            baseVerts = mesh.vertices; verts = new Vector3[baseVerts.Length];
        }

        readonly System.Collections.Generic.List<Vector4> swimmers = new();

        void Update()
        {
            float t = Time.time;
            // Quien nada en esta lámina la agita: ondas que salen de su posición (más fuertes si se mueve).
            swimmers.Clear();
            if (kind == Kind.Surface)
                foreach (var pl in LizardPlayer.All)
                    if (pl && pl.Motor && pl.Motor.Swimming)
                    {
                        Vector3 l = transform.InverseTransformPoint(pl.transform.position);
                        swimmers.Add(new Vector4(l.x, l.z, pl.Motor.PlanarSpeed > 0.4f ? 0.09f : 0.04f, 0f));
                    }
            for (int i = 0; i < baseVerts.Length; i++)
            {
                Vector3 v = baseVerts[i];
                float r = new Vector2(v.x, v.z).magnitude;
                if (kind == Kind.Surface)
                {
                    v.y += Mathf.Sin(r * frequency - t * speed) * amplitude * Mathf.Clamp01(r);
                    foreach (var sw in swimmers)
                    {
                        float d = new Vector2(v.x - sw.x, v.z - sw.y).magnitude;
                        if (d < 3f) v.y += Mathf.Sin(d * 7f - t * 9f) * sw.z * Mathf.Exp(-d * 1.1f);
                    }
                }
                else if (r > 0.001f)
                {
                    float w = Mathf.Sin(v.y * frequency + t * speed + Mathf.Atan2(v.z, v.x) * 3f) * amplitude;
                    v.x += v.x / r * w; v.z += v.z / r * w;
                }
                verts[i] = v;
            }
            mesh.vertices = verts;
        }
    }
}
