using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Crea comida física. Cada tipo lleva un modelo con detalles (rabito, hoja, rayas, brillo) hecho con piezas sin colisión,
    /// sobre una esfera física que es la que rueda, se agarra con la lengua y se come.
    /// </summary>
    public static class FoodFactory
    {
        static Material pelletMat, fruitMat, bigMat, stemMat, leafMat, stripeMat, shineMat, rindMat;
        static Mesh sphere, cylinder;

        static void EnsureMats()
        {
            pelletMat ??= LizardMaterials.Toon(new Color(0.78f, 0.93f, 0.35f), 0f, 0.4f);
            fruitMat ??= LizardMaterials.Toon(new Color(0.95f, 0.22f, 0.28f), 0f, 0.55f);
            bigMat ??= LizardMaterials.Toon(new Color(0.3f, 0.7f, 0.28f), 0f, 0.4f);
            stemMat ??= LizardMaterials.Toon(new Color(0.42f, 0.27f, 0.15f), 0f, 0.1f);
            leafMat ??= LizardMaterials.Toon(new Color(0.35f, 0.8f, 0.3f), 0f, 0.3f);
            stripeMat ??= LizardMaterials.Toon(new Color(0.16f, 0.45f, 0.17f), 0f, 0.3f);
            shineMat ??= LizardMaterials.Toon(new Color(1f, 1f, 1f), 0f, 0f);
            rindMat ??= LizardMaterials.Toon(new Color(0.85f, 0.95f, 0.65f), 0f, 0.2f);
            if (!sphere) { var t = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = t.GetComponent<MeshFilter>().sharedMesh; Destroy(t); }
            if (!cylinder) { var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cylinder = t.GetComponent<MeshFilter>().sharedMesh; Destroy(t); }
        }

        static void Destroy(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        static Transform Piece(Transform parent, Mesh mesh, Material m, Vector3 lp, Vector3 ls, Vector3 euler)
        {
            var g = new GameObject("Detalle");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = lp; g.transform.localScale = ls; g.transform.localRotation = Quaternion.Euler(euler);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }

        public static FoodItem CreatePellet(Vector3 pos, float value, float radius)
        {
            EnsureMats();
            return Create("Bolita de comida", pos, value, radius, 0.2f, pelletMat);
        }

        /// <summary>Manzana: roja, algo achatada, con rabito, hoja y brillo.</summary>
        public static FoodItem CreateFruit(Vector3 pos, float value = 1f, float radius = 0.18f, Material mat = null)
        {
            EnsureMats();
            var f = Create("Fruta", pos, value, radius, 0.3f, fruitMat);
            var t = f.transform; // escala = diámetro; las piezas van en coordenadas de la esfera unidad
            t.GetComponent<MeshRenderer>().enabled = false;
            Piece(t, sphere, fruitMat, Vector3.zero, new Vector3(1f, 0.88f, 1f), Vector3.zero);
            Piece(t, cylinder, stemMat, new Vector3(0, 0.5f, 0), new Vector3(0.07f, 0.13f, 0.07f), new Vector3(0, 0, 12));
            Piece(t, sphere, leafMat, new Vector3(0.12f, 0.55f, 0), new Vector3(0.28f, 0.05f, 0.14f), new Vector3(0, 0, -25));
            Piece(t, sphere, shineMat, new Vector3(-0.2f, 0.22f, -0.32f), new Vector3(0.14f, 0.1f, 0.06f), new Vector3(0, 30, 0));
            return f;
        }

        /// <summary>Sandía gigante: verde con rayas oscuras y un brillo.</summary>
        public static FoodItem CreateBig(Vector3 pos, float value = 4f, float radius = 0.7f, Material mat = null)
        {
            EnsureMats();
            var f = Create("Sandía gigante", pos, value, radius, 6f, bigMat);
            var t = f.transform;
            for (int i = 0; i < 6; i++)
                Piece(t, sphere, stripeMat, Vector3.zero, new Vector3(0.18f, 1.01f, 1.01f), new Vector3(0, i * 30f, 0));
            Piece(t, cylinder, stemMat, new Vector3(0, 0.5f, 0), new Vector3(0.06f, 0.06f, 0.06f), Vector3.zero);
            Piece(t, sphere, shineMat, new Vector3(-0.22f, 0.25f, -0.36f), new Vector3(0.16f, 0.1f, 0.05f), new Vector3(0, 30, 0));
            return f;
        }

        public static FoodItem Create(string name, Vector3 pos, float value, float radius, float mass, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * radius * 2f;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = 0.2f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var f = go.AddComponent<FoodItem>();
            f.nutrition = value;
            f.size = radius;
            return f;
        }
    }
}
