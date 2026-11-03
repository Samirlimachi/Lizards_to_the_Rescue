using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Mallas generadas por torneado (perfil girado alrededor del eje Y), con UV y un blend shape
    /// "Inflado" que transforma la pera en esfera para la forma gigante.
    /// </summary>
    public static class FrogMeshes
    {
        public const string InflateShape = "Inflado";

        /// <summary>Cuerpo de pera de altura 1 y centrado en el origen (y de -0.5 a 0.5).</summary>
        public static Mesh Body(int rings = 36, int segments = 48, float depth = 0.86f)
        {
            int cols = segments + 1;
            var verts = new Vector3[(rings + 1) * cols];
            var sphere = new Vector3[verts.Length];
            var uv = new Vector2[verts.Length];

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float s = Mathf.Sin(Mathf.PI * t);
                // Pera: base ancha y blanda, cabeza algo más estrecha arriba.
                float pearR = 0.46f * Mathf.Pow(s, 0.55f) * (1f - 0.24f * t);
                float pearY = t - 0.5f;
                float sphR = 0.5f * s;
                float sphY = -0.5f * Mathf.Cos(Mathf.PI * t);
                for (int c = 0; c <= segments; c++)
                {
                    float a = c / (float)segments * Mathf.PI * 2f;
                    float x = Mathf.Sin(a), z = Mathf.Cos(a);
                    int i = r * cols + c;
                    verts[i] = new Vector3(x * pearR, pearY, z * pearR * depth);
                    sphere[i] = new Vector3(x * sphR, sphY, z * sphR);
                    uv[i] = new Vector2(c / (float)segments, t);
                }
            }

            var tris = new int[rings * segments * 6];
            int k = 0;
            for (int r = 0; r < rings; r++)
                for (int c = 0; c < segments; c++)
                {
                    int a = r * cols + c, b = a + cols;
                    tris[k++] = a; tris[k++] = a + 1; tris[k++] = b;
                    tris[k++] = a + 1; tris[k++] = b + 1; tris[k++] = b;
                }

            var mesh = new Mesh { name = "Rana_Cuerpo" };
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.triangles = tris;
            var normals = SmoothNormals(mesh, rings, cols);

            // Blend shape: deltas de posición y normal hacia la esfera.
            var tmp = new Mesh { vertices = sphere, triangles = tris };
            var sphereNormals = SmoothNormals(tmp, rings, cols);
            Object.DestroyImmediate(tmp);
            var dv = new Vector3[verts.Length]; var dn = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++) { dv[i] = sphere[i] - verts[i]; dn[i] = sphereNormals[i] - normals[i]; }
            mesh.AddBlendShapeFrame(InflateShape, 100f, dv, dn, null);

            mesh.RecalculateBounds();
            var b2 = mesh.bounds; b2.Encapsulate(new Bounds(Vector3.zero, Vector3.one)); mesh.bounds = b2;
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Normales suaves que unen la costura de UV y los polos.</summary>
        static Vector3[] SmoothNormals(Mesh m, int rings, int cols)
        {
            m.RecalculateNormals();
            var n = m.normals;
            for (int r = 0; r <= rings; r++)
            {
                int a = r * cols, b = a + cols - 1;
                var avg = (n[a] + n[b]).normalized;
                n[a] = n[b] = avg;
            }
            for (int c = 0; c < cols; c++) { n[c] = Vector3.down; n[rings * cols + c] = Vector3.up; }
            m.normals = n;
            return n;
        }
    }
}
