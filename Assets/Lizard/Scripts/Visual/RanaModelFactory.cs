using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Diseño "rana" v2: estilo cartoon con contorno, cuerpo de pera de una sola pieza (con blend shape de inflado),
    /// barriga y cara verde amarillenta, sonrisa ancha con labio inferior articulado, ojos grandes con párpados
    /// rosados, bracitos en aleta, patitas y colita con punta naranja (rasgos propios del personaje).
    /// </summary>
    public static class RanaModelFactory
    {
        static readonly Color Skin = new Color(0.42f, 0.76f, 0.24f);
        static readonly Color Belly = new Color(0.88f, 0.93f, 0.42f);

        public static LizardModelMaterials CreateRuntimeMaterials(Texture2D skinTex = null)
        {
            var m = new LizardModelMaterials
            {
                skin = LizardMaterials.ToonCharacter(Skin, 0.0035f, 0.1f),
                belly = LizardMaterials.ToonCharacter(Belly, 0.0035f, 0.08f),
                eyeWhite = LizardMaterials.ToonCharacter(Color.white, 0.003f, 0.3f, new Color(0.78f, 0.84f, 0.95f)),
                pupil = LizardMaterials.ToonCharacter(new Color(0.05f, 0.05f, 0.07f), 0f, 0.6f),
                lid = LizardMaterials.ToonCharacter(new Color(1f, 0.56f, 0.72f)),
                mouth = LizardMaterials.ToonCharacter(new Color(0.6f, 0.14f, 0.3f), 0.0025f, 0f, new Color(0.7f, 0.6f, 0.8f)),
                tongue = LizardMaterials.ToonCharacter(new Color(1f, 0.58f, 0.72f), 0.002f, 0.3f),
                tailTip = LizardMaterials.ToonCharacter(new Color(1f, 0.55f, 0.12f)),
                spots = LizardMaterials.ToonCharacter(new Color(0.32f, 0.62f, 0.18f), 0f, 0f),
                bubble = LizardMaterials.Transparent(new Color(0.85f, 0.97f, 1f, 0.22f), 0.97f),
            };
            if (skinTex) foreach (var mat in new[] { m.skin, m.belly }) mat.SetTexture("_BaseMap", skinTex);
            return m;
        }

        public static LizardVisual Build(Transform parent, LizardModelMaterials m, Mesh bodyMesh = null)
        {
            var root = new GameObject("Modelo (rana)").transform;
            root.SetParent(parent, false);
            var v = root.gameObject.AddComponent<LizardVisual>();

            v.scalePivot = Node("Escala", root, Vector3.zero);
            v.bodyPivot = Node("Cuerpo (pivote)", v.scalePivot, Vector3.zero);
            v.rollPivot = Node("Rodar (pivote)", v.bodyPivot, new Vector3(0f, 0.5f, 0f));

            // Cuerpo de una pieza con blend shape "Inflado".
            var bodyGo = new GameObject("Cuerpo");
            bodyGo.transform.SetParent(v.rollPivot, false);
            var smr = bodyGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = bodyMesh ? bodyMesh : FrogMeshes.Body();
            smr.sharedMaterial = m.skin;
            smr.updateWhenOffscreen = true;
            v.body = bodyGo.transform;
            v.inflateRenderer = smr;
            v.inflateShapeIndex = smr.sharedMesh.GetBlendShapeIndex(FrogMeshes.InflateShape);

            v.spots = new[]
            {
                Part("Mancha 1", v.body, new Vector3(0.3f, 0.28f, -0.25f), new Vector3(0.2f, 0.06f, 0.17f), m.spots, new Vector3(0f, 0f, -45f)),
                Part("Mancha 2", v.body, new Vector3(-0.36f, 0.05f, -0.2f), new Vector3(0.17f, 0.06f, 0.15f), m.spots, new Vector3(0f, 0f, 70f)),
                Part("Mancha 3", v.body, new Vector3(0.05f, -0.15f, -0.47f), new Vector3(0.2f, 0.06f, 0.17f), m.spots, new Vector3(80f, 0f, 0f)),
            };

            v.belly = Part("Barriga", v.bodyPivot, new Vector3(0f, 0.34f, 0.12f), new Vector3(0.66f, 0.56f, 0.42f), m.belly);

            // Cara: interior rosa, labio inferior articulado, ojos con párpados rosados.
            v.headPivot = Node("Cabeza (pivote)", v.bodyPivot, new Vector3(0f, 0.62f, 0f));
            Part("Interior boca", v.headPivot, new Vector3(0f, 0.02f, 0.17f), new Vector3(0.56f, 0.24f, 0.2f), m.mouth);
            Part("Lengua (reposo)", v.headPivot, new Vector3(0f, -0.035f, 0.22f), new Vector3(0.36f, 0.1f, 0.16f), m.tongue);
            v.jawPivot = Node("Labio inferior (bisagra)", v.headPivot, new Vector3(0f, 0f, 0.02f));
            Part("Labio inferior", v.jawPivot, new Vector3(0f, -0.07f, 0.17f), new Vector3(0.6f, 0.13f, 0.22f), m.belly);
            v.mouthPoint = Node("Boca (origen)", v.headPivot, new Vector3(0f, 0.02f, 0.4f));
            Part("Fosa nasal I", v.headPivot, new Vector3(-0.06f, 0.2f, 0.25f), new Vector3(0.035f, 0.025f, 0.03f), m.pupil);
            Part("Fosa nasal D", v.headPivot, new Vector3(0.06f, 0.2f, 0.25f), new Vector3(0.035f, 0.025f, 0.03f), m.pupil);

            var eyes = new List<Transform>(); var lids = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = Node(s < 0 ? "Ojo I" : "Ojo D", v.headPivot, new Vector3(0.165f * s, 0.33f, 0.07f));
                Part("Cuenca", eye, new Vector3(0f, -0.08f, -0.01f), new Vector3(0.27f, 0.2f, 0.25f), m.skin);
                Part("Blanco", eye, Vector3.zero, Vector3.one * 0.28f, m.eyeWhite);
                Part("Pupila", eye, new Vector3(0.012f * s, -0.005f, 0.115f), new Vector3(0.15f, 0.165f, 0.07f), m.pupil);
                Part("Brillo", eye, new Vector3(0.04f * s, 0.045f, 0.145f), Vector3.one * 0.04f, m.eyeWhite);
                lids.Add(Part("Párpado", eye, new Vector3(0f, 0.085f, -0.035f), new Vector3(0.295f, 0.15f, 0.295f), m.lid));
                eyes.Add(eye);
            }
            v.eyes = eyes.ToArray();
            v.lids = lids.ToArray();
            v.lidClosedY = 0f;
            v.lidClosedScale = new Vector3(0.36f, 0.34f, 0.36f);

            // Bracitos en aleta, inclinados hacia abajo y afuera.
            var arms = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var arm = Node(s < 0 ? "Brazo I" : "Brazo D", v.bodyPivot, new Vector3(0.33f * s, 0.52f, 0.04f));
                arm.localRotation = Quaternion.Euler(0f, -12f * s, 42f * s);
                Part("Aleta", arm, new Vector3(0f, -0.15f, 0f), new Vector3(0.15f, 0.34f, 0.13f), m.skin);
                Part("Mano", arm, new Vector3(0f, -0.3f, 0.01f), new Vector3(0.14f, 0.12f, 0.14f), m.skin);
                arms.Add(arm);
            }
            v.arms = arms.ToArray();

            // Patitas.
            var legs = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var leg = Node(s < 0 ? "Pata I" : "Pata D", v.bodyPivot, new Vector3(0.18f * s, 0.2f, 0.02f));
                Part("Muslo", leg, new Vector3(0f, -0.07f, 0f), new Vector3(0.21f, 0.22f, 0.21f), m.skin);
                var foot = Part("Pie", leg, new Vector3(0.02f * s, -0.155f, 0.07f), new Vector3(0.21f, 0.09f, 0.3f), m.skin);
                for (int t = -1; t <= 1; t++)
                    Part("Dedo", foot, new Vector3(0.3f * t, -0.05f, 0.45f - Mathf.Abs(t) * 0.08f), new Vector3(0.33f, 0.85f, 0.28f), m.skin);
                legs.Add(leg);
            }
            v.legs = legs.ToArray();

            // Colita curvada con punta naranja.
            var tail = new List<Transform>();
            Transform prev = v.bodyPivot; Vector3 pos = new Vector3(0f, 0.17f, -0.33f);
            float[] sizes = { 0.17f, 0.13f, 0.1f };
            for (int i = 0; i < sizes.Length; i++)
            {
                var seg = Node("Cola " + (i + 1), prev, pos);
                seg.localRotation = Quaternion.Euler(30f, 0f, 0f);
                Part("Segmento", seg, Vector3.zero, new Vector3(sizes[i], sizes[i], sizes[i] * 1.35f), i == sizes.Length - 1 ? m.tailTip : m.skin);
                tail.Add(seg); prev = seg; pos = new Vector3(0f, 0f, -sizes[i] * 0.8f);
            }
            v.tail = tail.ToArray();

            v.bubble = Part("Burbuja", root, new Vector3(0f, 0.25f, 0f), Vector3.one * 0.6f, m.bubble);
            v.bubble.gameObject.SetActive(false);
            v.ping = Part("Señal de ayuda", root, Vector3.up, Vector3.one, m.bubble);
            v.ping.gameObject.SetActive(false);

            v.jawSign = 1f;
            v.jawRestAngle = 4f;

            v.normalShape = new LizardVisual.Shape
            {
                bodyScale = new Vector3(0.9f, 1f, 0.84f), rollY = 0.5f, inflate = 0f,
                bellyPos = new Vector3(0f, 0.34f, 0.12f), bellyScale = new Vector3(0.66f, 0.56f, 0.42f),
                headPos = new Vector3(0f, 0.62f, 0.02f), headScale = 1f, legScale = 1f, tailScale = 1f, armScale = 1f,
            };
            v.smallShape = new LizardVisual.Shape
            {
                bodyScale = new Vector3(0.86f, 0.78f, 0.86f), rollY = 0.42f, inflate = 35f,
                bellyPos = new Vector3(0f, 0.3f, 0.16f), bellyScale = new Vector3(0.56f, 0.46f, 0.36f),
                headPos = new Vector3(0f, 0.5f, 0.03f), headScale = 1.12f, legScale = 0f, tailScale = 1.7f, armScale = 0f,
            };
            v.giantShape = new LizardVisual.Shape
            {
                bodyScale = Vector3.one, rollY = 0.5f, inflate = 100f,
                bellyPos = new Vector3(0f, 0.38f, 0.2f), bellyScale = new Vector3(0.86f, 0.62f, 0.66f),
                headPos = new Vector3(0f, 0.68f, 0.17f), headScale = 1f, legScale = 0.8f, tailScale = 0.6f, armScale = 0.95f,
                armOffset = new Vector2(0.14f, 0f), legOffset = new Vector2(0.14f, 0.04f),
            };
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
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.SetLocalPositionAndRotation(localPos, Quaternion.Euler(euler));
            t.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return t;
        }
    }
}
