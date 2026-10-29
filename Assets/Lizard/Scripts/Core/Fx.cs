using UnityEngine;

namespace Lizards
{
    /// <summary>Ráfagas de partículas simples creadas por código (provisional hasta tener VFX propios).</summary>
    public static class Fx
    {
        static Mesh sphereMesh;

        static Mesh SphereMesh
        {
            get
            {
                if (sphereMesh) return sphereMesh;
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphereMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
                return sphereMesh;
            }
        }

        public static void Burst(Vector3 pos, Color color, int count, float speed, float size)
        {
            if (!Application.isPlaying || count <= 0) return;
            var go = new GameObject("Fx");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startColor = color;
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.1f;
            var sol = ps.sizeOverLifetime; sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = SphereMesh;
            r.sharedMaterial = LizardMaterials.Particle();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();
            ps.Emit(count);
            Object.Destroy(go, 1.5f);
        }
    }
}
