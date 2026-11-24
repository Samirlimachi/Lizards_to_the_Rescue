using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Obstáculo que solo puede derribar el gigante a suficiente velocidad.</summary>
    public class Breakable : MonoBehaviour
    {
        public float requiredSpeed = 3.5f;
        public bool requireGiant = true;
        public Color debrisColor = new Color(0.6f, 0.4f, 0.25f);
        bool broken;
        /// <summary>Se rompió aquí (lo usa la red para romperlo en los demás clientes).</summary>
        public event System.Action<Vector3> OnBroken;
        public bool Broken => broken;

        public bool TryBreak(float speed, bool isGiant, Vector3 impulse)
        {
            if (broken || (requireGiant && !isGiant) || speed < requiredSpeed) return false;
            Break(impulse);
            OnBroken?.Invoke(impulse);
            return true;
        }

        /// <summary>Rotura que llega por red (sin volver a anunciarla).</summary>
        public void NetBreak(Vector3 impulse) { if (!broken) Break(impulse); }

        void Break(Vector3 impulse)
        {
            broken = true;
            var mat = LizardMaterials.Lit(debrisColor, 0.2f);
            var b = GetComponent<Collider>().bounds;
            for (int i = 0; i < 10; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                d.name = "Escombro";
                d.transform.position = b.center + Vector3.Scale(Random.insideUnitSphere, b.extents * 0.8f);
                d.transform.localScale = Vector3.one * Random.Range(0.25f, 0.5f);
                d.GetComponent<Renderer>().sharedMaterial = mat;
                var r = d.AddComponent<Rigidbody>();
                r.mass = 0.5f;
                r.AddForce(Vector3.ClampMagnitude(impulse, 300f) * 0.004f + Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
                Destroy(d, 4f);
            }
            Sfx.Play(Sfx.Break, b.center);
            Fx.Burst(b.center, debrisColor, 20, 4f, 0.2f);
            Destroy(gameObject);
        }
    }
}
