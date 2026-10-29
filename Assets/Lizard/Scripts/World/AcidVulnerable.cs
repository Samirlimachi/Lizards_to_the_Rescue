using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>Obstáculo que se disuelve con ácido.</summary>
    public class AcidVulnerable : MonoBehaviour
    {
        public int hitsToDissolve = 2;
        public float dissolveTime = 0.8f;
        int hits; bool dissolving; float t; Vector3 startScale;
        Renderer rend; MaterialPropertyBlock mpb;

        void Awake() { rend = GetComponent<Renderer>(); mpb = new MaterialPropertyBlock(); startScale = transform.localScale; }

        public void HitByAcid()
        {
            if (dissolving) return;
            hits++;
            if (rend)
            {
                mpb.SetColor("_BaseColor", Color.Lerp(new Color(0.75f, 0.7f, 0.55f), new Color(0.4f, 0.9f, 0.2f), hits / (float)hitsToDissolve));
                rend.SetPropertyBlock(mpb);
            }
            if (hits >= hitsToDissolve) { dissolving = true; foreach (var c in GetComponents<Collider>()) c.enabled = false; }
        }

        void Update()
        {
            if (!dissolving) return;
            t += Time.deltaTime / dissolveTime;
            transform.localScale = Vector3.Scale(startScale, new Vector3(1f, 1f - t, 1f));
            if (Random.value < 0.3f) Fx.Burst(transform.position + Random.insideUnitSphere, new Color(0.45f, 1f, 0.15f), 2, 1f, 0.08f);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
