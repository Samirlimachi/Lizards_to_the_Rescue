using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Vitrina de la expendedora: el producto elegido gira y late dentro de su cúpula, y su base brilla.
    /// Los objetos van en el mismo orden que SwampLobbyExpansion.Names.
    /// </summary>
    public class VendingDisplay : MonoBehaviour
    {
        public Transform[] items = new Transform[0];
        public Renderer[] bases = new Renderer[0];
        public Material baseIdle, baseSelected;
        SwampLobbyExpansion shop;
        Vector3[] rest;
        int last = -1;

        void Start()
        {
            shop = FindAnyObjectByType<SwampLobbyExpansion>();
            rest = new Vector3[items.Length];
            for (int i = 0; i < items.Length; i++) rest[i] = items[i].localScale;
        }

        void Update()
        {
            if (!shop) return;
            int sel = shop.Selected;
            float t = Time.time;
            for (int i = 0; i < items.Length; i++)
            {
                bool on = i == sel;
                float k = on ? 1.18f + Mathf.Sin(t * 6f) * 0.05f : 1f;
                items[i].localScale = Vector3.Lerp(items[i].localScale, rest[i] * k, 1f - Mathf.Exp(-Time.deltaTime * 12f));
                items[i].localRotation = on ? Quaternion.Euler(0f, t * 90f, 0f) : Quaternion.Slerp(items[i].localRotation, Quaternion.identity, Time.deltaTime * 4f);
            }
            if (sel != last && bases.Length == items.Length)
            {
                for (int i = 0; i < bases.Length; i++) bases[i].sharedMaterial = i == sel ? baseSelected : baseIdle;
                last = sel;
            }
        }
    }
}
