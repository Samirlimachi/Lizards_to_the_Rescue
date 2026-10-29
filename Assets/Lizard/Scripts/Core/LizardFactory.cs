using UnityEngine;

namespace Lizards
{
    /// <summary>Ensambla un lagarto completo (física + sistemas + modelo). Lo usan el generador de escena y la autoprueba.</summary>
    public static class LizardFactory
    {
        static PhysicsMaterial slippery;

        public static PhysicsMaterial Slippery
        {
            get
            {
                if (slippery) return slippery;
                slippery = new PhysicsMaterial("Lagarto (sin fricción)")
                {
                    dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                };
                return slippery;
            }
        }

        public static LizardPlayer Create(string name, Vector3 position, LizardConfig config, LizardModelMaterials mats, PhysicsMaterial physicsMat = null, bool controlled = true)
        {
            var go = new GameObject(name);
            go.SetActive(false); // los Awake se ejecutan cuando todo está montado
            go.transform.position = position;

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 5f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var cap = go.AddComponent<CapsuleCollider>();
            cap.radius = 0.4f; cap.height = 1f; cap.center = Vector3.up * 0.5f;
            cap.sharedMaterial = physicsMat ? physicsMat : Slippery;
            var sph = go.AddComponent<SphereCollider>();
            sph.enabled = false;
            sph.sharedMaterial = physicsMat ? physicsMat : Slippery;

            var input = go.AddComponent<LizardInput>();
            input.controlled = controlled;
            go.AddComponent<LizardMotor>();
            go.AddComponent<LizardForms>();
            go.AddComponent<LizardStomach>();
            go.AddComponent<LizardHealth>();
            go.AddComponent<LizardTongue>();
            go.AddComponent<LizardAcid>();
            go.AddComponent<LizardLipSync>();
            var p = go.AddComponent<LizardPlayer>();
            p.config = config;
            p.displayName = name;

            LizardModelFactory.Build(go.transform, mats ?? LizardModelMaterials.CreateRuntime());
            go.SetActive(true);
            return p;
        }
    }
}
