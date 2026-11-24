using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Obstáculo que se disuelve con ácido. Si tiene <see cref="AcidMembrane"/> (la membrana de gelatina de la
    /// despensa), es ella la que anima los golpes y la rotura; si no, se aplasta y desaparece como antes.
    /// </summary>
    public class AcidVulnerable : MonoBehaviour
    {
        public int hitsToDissolve = 2;
        public float dissolveTime = 0.8f;
        int hits; bool dissolving; float t; Vector3 startScale;
        Renderer rend; MaterialPropertyBlock mpb;
        AcidMembrane membrane;

        void Awake()
        {
            rend = GetComponentInChildren<Renderer>(); mpb = new MaterialPropertyBlock(); startScale = transform.localScale;
            // Las membranas de gelatina (una caja con colisión y un modelo hijo) se animan con AcidMembrane.
            membrane = GetComponent<AcidMembrane>();
            if (!membrane && name.Contains("Membrana") && GetComponent<BoxCollider>()) membrane = gameObject.AddComponent<AcidMembrane>();
        }

        /// <summary>Recibió ácido aquí, en 'point' (lo usa la red para contar el golpe en los demás clientes).</summary>
        public event System.Action<Vector3> OnAcidHit;
        public int Hits => hits;
        public bool Dissolving => dissolving;

        public void HitByAcid() => HitByAcid(transform.position);

        public void HitByAcid(Vector3 point)
        {
            if (dissolving) return;
            NetHit(point);
            OnAcidHit?.Invoke(point);
        }

        /// <summary>Golpe de ácido que llega por red (sin volver a anunciarlo).</summary>
        public void NetHit() => NetHit(transform.position);

        public void NetHit(Vector3 point)
        {
            if (dissolving) return;
            hits++;
            bool breaks = hits >= hitsToDissolve;
            if (breaks) { dissolving = true; foreach (var c in GetComponents<Collider>()) c.enabled = false; }
            if (membrane) { membrane.Hit(point, hits / (float)hitsToDissolve, breaks); return; }
            if (rend)
            {
                mpb.SetColor("_BaseColor", Color.Lerp(new Color(0.75f, 0.7f, 0.55f), new Color(0.4f, 0.9f, 0.2f), hits / (float)hitsToDissolve));
                rend.SetPropertyBlock(mpb);
            }
        }

        void Update()
        {
            if (!dissolving || membrane) return;   // la membrana se encarga de su propia rotura
            t += Time.deltaTime / dissolveTime;
            transform.localScale = Vector3.Scale(startScale, new Vector3(1f, 1f - t, 1f));
            if (Random.value < 0.3f) Fx.Burst(transform.position + Random.insideUnitSphere, new Color(0.45f, 1f, 0.15f), 2, 1f, 0.08f);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
