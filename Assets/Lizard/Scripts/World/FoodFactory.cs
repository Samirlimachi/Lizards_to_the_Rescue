using UnityEngine;

namespace Lizards
{
    public static class FoodFactory
    {
        static Material pelletMat, fruitMat, bigMat;

        public static FoodItem CreatePellet(Vector3 pos, float value, float radius)
        {
            pelletMat ??= LizardMaterials.Lit(new Color(0.78f, 0.93f, 0.35f), 0.4f);
            return Create("Bolita de comida", pos, value, radius, 0.2f, pelletMat);
        }

        public static FoodItem CreateFruit(Vector3 pos, float value = 1f, float radius = 0.18f, Material mat = null)
        {
            fruitMat ??= LizardMaterials.Lit(new Color(0.95f, 0.3f, 0.35f), 0.5f);
            return Create("Fruta", pos, value, radius, 0.3f, mat ? mat : fruitMat);
        }

        public static FoodItem CreateBig(Vector3 pos, float value = 4f, float radius = 0.7f, Material mat = null)
        {
            bigMat ??= LizardMaterials.Lit(new Color(0.35f, 0.75f, 0.3f), 0.3f);
            return Create("Sandía gigante", pos, value, radius, 6f, mat ? mat : bigMat);
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
