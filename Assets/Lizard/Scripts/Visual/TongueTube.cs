using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Tubo 3D que sigue una curva (la lengua). Al tener volumen real y usar el shader cartoon con contorno,
    /// se ve igual desde cualquier ángulo, sin las franjas oscuras de una cinta plana.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class TongueTube : MonoBehaviour
    {
        const int Sides = 10;
        Mesh mesh;
        Vector3[] verts; Vector3[] normals; int[] tris;
        int rings;

        public static TongueTube Create(Transform parent, Material mat, int rings)
        {
            var go = new GameObject("Lengua (tubo)");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TongueTube>();
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.Init(rings);
            return t;
        }

        void Init(int ringCount)
        {
            rings = ringCount;
            mesh = new Mesh { name = "Lengua" };
            mesh.MarkDynamic();
            verts = new Vector3[rings * Sides];
            normals = new Vector3[verts.Length];
            tris = new int[(rings - 1) * Sides * 6];
            int k = 0;
            for (int r = 0; r < rings - 1; r++)
                for (int s = 0; s < Sides; s++)
                {
                    int a = r * Sides + s, b = r * Sides + (s + 1) % Sides, c = a + Sides, d = b + Sides;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            mesh.vertices = verts; mesh.normals = normals; mesh.triangles = tris;
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        /// <summary>Actualiza el tubo con puntos en espacio de mundo y un radio que pasa de r0 a r1.</summary>
        public void SetPath(Vector3[] worldPts, float r0, float r1)
        {
            int n = Mathf.Min(worldPts.Length, rings);
            Vector3 prevNormal = Vector3.zero;
            for (int i = 0; i < rings; i++)
            {
                Vector3 p = transform.InverseTransformPoint(worldPts[Mathf.Min(i, n - 1)]);
                Vector3 next = transform.InverseTransformPoint(worldPts[Mathf.Min(i + 1, n - 1)]);
                Vector3 prev = transform.InverseTransformPoint(worldPts[Mathf.Max(i - 1, 0)]);
                Vector3 tangent = next - prev;
                if (tangent.sqrMagnitude < 1e-10f) tangent = Vector3.forward;
                tangent.Normalize();
                // Marco que se transporta a lo largo de la curva para que el tubo no se retuerza.
                Vector3 normal = i == 0 || prevNormal == Vector3.zero
                    ? Vector3.Cross(tangent, Mathf.Abs(tangent.y) < 0.9f ? Vector3.up : Vector3.right).normalized
                    : Vector3.ProjectOnPlane(prevNormal, tangent).normalized;
                if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(tangent, Vector3.right).normalized;
                prevNormal = normal;
                Vector3 binormal = Vector3.Cross(tangent, normal);
                float t = i / (rings - 1f);
                float radius = Mathf.Lerp(r0, r1, t) / Mathf.Max(0.0001f, transform.lossyScale.x);
                for (int s = 0; s < Sides; s++)
                {
                    float a = s / (float)Sides * Mathf.PI * 2f;
                    Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    verts[i * Sides + s] = p + dir * radius;
                    normals[i * Sides + s] = dir;
                }
            }
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.RecalculateBounds();
        }

        public void Show(bool on) { if (gameObject.activeSelf != on) gameObject.SetActive(on); }
    }
}
