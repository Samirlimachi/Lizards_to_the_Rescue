using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    [Serializable]
    public class LizardModelMaterials
    {
        public Material skin, belly, eyeWhite, pupil, lid, mouth, tongue, tailTip, bubble, spots;

        public static LizardModelMaterials CreateRuntime() => new LizardModelMaterials
        {
            skin = LizardMaterials.Lit(new Color(0.36f, 0.74f, 0.2f), 0.45f),
            belly = LizardMaterials.Lit(new Color(0.83f, 0.91f, 0.45f), 0.4f),
            eyeWhite = LizardMaterials.Lit(Color.white, 0.8f),
            pupil = LizardMaterials.Lit(new Color(0.05f, 0.05f, 0.07f), 0.9f),
            lid = LizardMaterials.Lit(new Color(1f, 0.5f, 0.68f), 0.4f),
            mouth = LizardMaterials.Lit(new Color(0.35f, 0.08f, 0.12f), 0.3f),
            tongue = LizardMaterials.Lit(new Color(1f, 0.45f, 0.62f), 0.6f),
            tailTip = LizardMaterials.Lit(new Color(0.98f, 0.52f, 0.1f), 0.45f),
            bubble = LizardMaterials.Transparent(new Color(0.7f, 0.95f, 1f, 0.28f)),
            spots = LizardMaterials.Lit(new Color(0.27f, 0.6f, 0.15f), 0.4f),
        };
    }

    /// <summary>
    /// Construye el modelo del lagarto a partir de volúmenes redondeados (primitivas con UV) organizados
    /// en una jerarquía preparada para animar: pivote de cuerpo, cabeza, mandíbula articulada, párpados,
    /// patas y cola encadenada. Todas las piezas son editables en el prefab.
    /// </summary>
    public static class LizardModelFactory
    {
        public static LizardVisual Build(Transform parent, LizardModelMaterials m)
        {
            var root = new GameObject("Modelo").transform;
            root.SetParent(parent, false);
            var v = root.gameObject.AddComponent<LizardVisual>();

            v.scalePivot = Node("Escala", root, Vector3.zero);
            v.bodyPivot = Node("Cuerpo (pivote)", v.scalePivot, Vector3.zero);
            v.rollPivot = Node("Rodar (pivote)", v.bodyPivot, new Vector3(0f, 0.42f, 0f));
            v.body = Part("Cuerpo", v.rollPivot, new Vector3(0f, 0f, -0.02f), new Vector3(0.78f, 0.7f, 0.95f), m.skin);
            var spots = new List<Transform>();
            spots.Add(Part("Mancha 1", v.body, new Vector3(0.25f, 0.38f, -0.1f), new Vector3(0.28f, 0.1f, 0.24f), m.spots, new Vector3(0f, 0f, -35f)));
            spots.Add(Part("Mancha 2", v.body, new Vector3(-0.3f, 0.3f, 0.12f), new Vector3(0.22f, 0.1f, 0.2f), m.spots, new Vector3(0f, 0f, 45f)));
            spots.Add(Part("Mancha 3", v.body, new Vector3(0.05f, 0.25f, -0.4f), new Vector3(0.24f, 0.1f, 0.22f), m.spots, new Vector3(60f, 0f, 0f)));
            v.spots = spots.ToArray();

            v.belly = Part("Vientre", v.bodyPivot, new Vector3(0f, 0.38f, 0.12f), new Vector3(0.62f, 0.55f, 0.72f), m.belly);

            // Cabeza
            v.headPivot = Node("Cabeza (pivote)", v.bodyPivot, new Vector3(0f, 0.62f, 0.22f));
            Part("Cabeza", v.headPivot, new Vector3(0f, 0.12f, 0.08f), new Vector3(0.7f, 0.44f, 0.62f), m.skin);
            Part("Fosa nasal I", v.headPivot, new Vector3(-0.07f, 0.13f, 0.38f), Vector3.one * 0.035f, m.pupil);
            Part("Fosa nasal D", v.headPivot, new Vector3(0.07f, 0.13f, 0.38f), Vector3.one * 0.035f, m.pupil);
            Part("Interior boca", v.headPivot, new Vector3(0f, -0.05f, 0.17f), new Vector3(0.52f, 0.1f, 0.44f), m.mouth);
            Part("Lengua (reposo)", v.headPivot, new Vector3(0f, -0.06f, 0.2f), new Vector3(0.24f, 0.06f, 0.3f), m.tongue);

            v.jawPivot = Node("Mandíbula (pivote)", v.headPivot, new Vector3(0f, -0.03f, -0.1f));
            Part("Mandíbula", v.jawPivot, new Vector3(0f, -0.05f, 0.22f), new Vector3(0.64f, 0.2f, 0.58f), m.belly);

            v.mouthPoint = Node("Boca (origen)", v.headPivot, new Vector3(0f, -0.03f, 0.42f));

            var lids = new List<Transform>(); var eyes = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = Node(s < 0 ? "Ojo I" : "Ojo D", v.headPivot, new Vector3(0.17f * s, 0.3f, 0.12f));
                eyes.Add(eye);
                Part("Blanco", eye, Vector3.zero, Vector3.one * 0.26f, m.eyeWhite);
                Part("Pupila", eye, new Vector3(0.015f * s, 0.01f, 0.11f), Vector3.one * 0.1f, m.pupil);
                lids.Add(Part("Párpado", eye, new Vector3(0f, 0.09f, -0.005f), new Vector3(0.285f, 0.12f, 0.285f), m.lid));
            }
            v.eyes = eyes.ToArray();
            v.lids = lids.ToArray();

            // Patas con dedos redondeados
            var legs = new List<Transform>();
            Vector3[] hips = { new(-0.3f, 0.3f, 0.25f), new(0.3f, 0.3f, 0.25f), new(-0.32f, 0.28f, -0.2f), new(0.32f, 0.28f, -0.2f) };
            string[] names = { "Pata DI", "Pata DD", "Pata TI", "Pata TD" };
            for (int i = 0; i < 4; i++)
            {
                var leg = Node(names[i], v.bodyPivot, hips[i]);
                float sx = Mathf.Sign(hips[i].x);
                Part("Muslo", leg, new Vector3(0.02f * sx, -0.1f, 0f), new Vector3(0.14f, 0.2f, 0.14f), m.skin);
                var foot = Part("Pie", leg, new Vector3(0.03f * sx, -hips[i].y + 0.04f, 0.05f), new Vector3(0.2f, 0.08f, 0.24f), m.skin);
                for (int t = -1; t <= 1; t++)
                    Part("Dedo", foot, new Vector3(0.32f * t, 0f, 0.48f - Mathf.Abs(t) * 0.08f), new Vector3(0.35f, 0.9f, 0.3f), m.belly);
                legs.Add(leg);
            }
            v.legs = legs.ToArray();

            // Cola curvada con punta naranja
            var tail = new List<Transform>();
            Transform prev = v.bodyPivot;
            Vector3 pos = new Vector3(0f, 0.28f, -0.42f);
            float[] sizes = { 0.26f, 0.22f, 0.18f, 0.15f, 0.13f };
            for (int i = 0; i < sizes.Length; i++)
            {
                var seg = Node("Cola " + (i + 1), prev, pos);
                seg.localRotation = Quaternion.Euler(18f, 0f, 0f);
                Part("Segmento", seg, Vector3.zero, new Vector3(sizes[i], sizes[i], sizes[i] * 1.3f), i == sizes.Length - 1 ? m.tailTip : m.skin);
                tail.Add(seg);
                prev = seg;
                pos = new Vector3(0f, 0f, -sizes[i] * 0.75f);
            }
            v.tail = tail.ToArray();

            v.bubble = Part("Burbuja", root, new Vector3(0f, 0.25f, 0f), Vector3.one * 0.6f, m.bubble);
            v.bubble.gameObject.SetActive(false);
            v.ping = Part("Señal de ayuda", root, Vector3.up, Vector3.one, m.bubble);
            v.ping.gameObject.SetActive(false);

            return v;
        }

        static Transform Node(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static Transform Part(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return t;
        }
    }
}
