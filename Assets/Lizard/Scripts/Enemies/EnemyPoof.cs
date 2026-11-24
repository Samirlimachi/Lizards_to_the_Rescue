using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Reventón de dibujo animado para la muerte de los enemigos: nube que se infla y se desvanece, anillo de bolitas
    /// que salen disparadas, chispas y (opcional) gotas de gel que caen en arco y salpican el suelo.
    /// Todo con mallas simples y un solo objeto por reventón (barato aunque mueran varios a la vez).
    /// </summary>
    public sealed class EnemyPoof : MonoBehaviour
    {
        static Mesh sphere;
        static Material puffMat, gelDropMat;

        sealed class Bit { public Transform t; public Renderer r; public Vector3 vel; public float size, life, age; public bool gravity, drop, puff; }
        readonly List<Bit> bits = new();
        MaterialPropertyBlock mpb;
        Color puffColor;
        float groundY;

        static Mesh Sphere
        {
            get
            {
                if (sphere) return sphere;
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = g.GetComponent<MeshFilter>().sharedMesh; Destroy(g);
                return sphere;
            }
        }

        /// <summary>Reventón en 'pos'. size ≈ tamaño del enemigo en metros. gel = gotas que caen y salpican.</summary>
        public static void Pop(Vector3 pos, Color color, float size, bool gel)
        {
            if (!Application.isPlaying) return;
            var go = new GameObject("Reventón");
            go.transform.position = pos;
            var p = go.AddComponent<EnemyPoof>();
            p.Build(color, Mathf.Max(0.3f, size), gel);
            Fx.Burst(pos, color, 18, 4.5f * size, 0.11f * size);
            Fx.Burst(pos, Color.Lerp(color, Color.white, 0.75f), 10, 3f * size, 0.06f * size);
            Sfx.Play(Sfx.Eject, pos, 0.75f);
            Sfx.Play(Sfx.Break, pos, 0.35f);
        }

        void Build(Color color, float size, bool gel)
        {
            mpb = new MaterialPropertyBlock();
            puffColor = Color.Lerp(color, Color.white, 0.55f);
            if (!puffMat) { puffMat = LizardMaterials.Transparent(Color.white, 0.2f); puffMat.name = "Nube del reventón"; }
            if (!gelDropMat) { gelDropMat = LizardMaterials.Toon(new Color(0.72f, 0.95f, 0.25f), 0f, 0.6f); gelDropMat.name = "Gota de gel"; }
            groundY = transform.position.y - size;
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) groundY = hit.point.y;

            // Nube: varias bolas que se inflan rápido y se desvanecen (aspecto de "puf" de dibujo animado).
            for (int i = 0; i < 6; i++)
            {
                Vector3 off = Random.insideUnitSphere * size * 0.35f; off.y = Mathf.Abs(off.y) * 0.6f;
                Add(off, Random.onUnitSphere * 0.6f * size + Vector3.up * 0.5f, size * Random.Range(0.55f, 0.8f), Random.Range(0.38f, 0.5f), puffMat, false, false, true);
            }
            // Anillo de bolitas que salen en horizontal y se encogen.
            int ring = 10;
            for (int i = 0; i < ring; i++)
            {
                float a = (i + Random.value * 0.4f) / ring * Mathf.PI * 2f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a));
                Add(d * size * 0.2f, d * size * Random.Range(4.5f, 6f), size * 0.13f, Random.Range(0.3f, 0.42f), puffMat, false, false, false);
            }
            // Gotas de gel en arco: al tocar el suelo dejan una salpicadura pequeña que se seca enseguida.
            if (gel)
                for (int i = 0; i < 7; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    Vector3 v = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(1.5f, 3.2f) * size + Vector3.up * Random.Range(3f, 5f) * size;
                    Add(Vector3.up * size * 0.2f, v, size * Random.Range(0.1f, 0.16f), 3f, gelDropMat, true, true, false);
                }
        }

        void Add(Vector3 offset, Vector3 vel, float size, float life, Material mat, bool gravity, bool drop, bool puff)
        {
            var g = new GameObject(drop ? "Gota" : "Nube");
            g.transform.SetParent(transform, false);
            g.transform.localPosition = offset;
            g.AddComponent<MeshFilter>().sharedMesh = Sphere;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.transform.localScale = Vector3.one * (puff ? size * 0.2f : size);
            bits.Add(new Bit { t = g.transform, r = r, vel = vel, size = size, life = life, gravity = gravity, drop = drop, puff = puff });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = bits.Count - 1; i >= 0; i--)
            {
                var b = bits[i];
                b.age += dt;
                float k = Mathf.Clamp01(b.age / b.life);
                if (b.gravity) b.vel += Physics.gravity * dt;
                else b.vel *= Mathf.Exp(-6f * dt);   // la nube y el anillo frenan en el aire
                b.t.position += b.vel * dt;

                if (b.puff)
                {
                    // Se infla con rebote y se desvanece.
                    float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(k * 2.2f), 3f);
                    b.t.localScale = Vector3.one * b.size * (0.2f + 1.1f * grow);
                    Fade(b, puffColor, 0.85f * (1f - k * k));
                }
                else if (!b.drop)
                {
                    b.t.localScale = Vector3.one * b.size * (1f - k);
                    Fade(b, puffColor, 0.9f * (1f - k));
                }
                else
                {
                    // Gota estirada en la dirección en que cae.
                    float sp = b.vel.magnitude;
                    if (sp > 0.1f) b.t.rotation = Quaternion.LookRotation(b.vel / sp);
                    b.t.localScale = new Vector3(b.size, b.size, b.size * (1f + Mathf.Min(1.2f, sp * 0.12f)));
                    if (b.t.position.y <= groundY + 0.02f && b.vel.y < 0f)
                    {
                        SlimeGelPuddle.Create(new Vector3(b.t.position.x, groundY + 0.015f, b.t.position.z), b.size * 1.6f, 0f, 1.6f, false, 1f, 0.12f);
                        k = 1f;
                    }
                }
                if (k >= 1f) { Destroy(b.t.gameObject); bits.RemoveAt(i); }
            }
            if (bits.Count == 0) Destroy(gameObject);
        }

        void Fade(Bit b, Color c, float alpha)
        {
            c.a = Mathf.Clamp01(alpha);
            b.r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            b.r.SetPropertyBlock(mpb);
        }
    }
}
