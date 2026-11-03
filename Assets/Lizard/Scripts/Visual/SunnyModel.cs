using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Sunny v2: modelo propio construido con formas suaves fundidas (SDF → Surface Nets).
    /// - Cuerpo de una sola superficie: torso, mentón, brazos con dedos redondeados, piernas, pies y cola curvada.
    /// - Tapa de la cabeza como pieza aparte: al levantarse muestra paladar y lengua, sin estirar nada.
    /// - Colores planos por vértice (verde, barriga/mentón lima, cola con punta naranja, boca roja, lengua rosa).
    /// - Ojos con párpados rosados que parpadean.
    /// Medidas en metros, pies en y = 0, mirando a +Z.
    /// </summary>
    public static class SunnyModel
    {
        [Serializable]
        public class Settings
        {
            public float cellSize = 0.011f;
            public float mouthY = 0.70f;
            [Tooltip("Curva de la sonrisa: y = mouthY + smile * x².")]
            public float smile = 0.35f;
            public Vector3 headHinge = new Vector3(0f, 0.70f, -0.2f);
            public Vector3 jawHinge = new Vector3(0f, 0.6f, -0.15f);

            public Color green = new Color(0.36f, 0.76f, 0.2f);
            public Color lime = new Color(0.84f, 0.94f, 0.42f);
            public Color mouthRed = new Color(0.45f, 0.08f, 0.15f);
            public Color tongue = new Color(0.95f, 0.45f, 0.62f);
            public Color tailYellow = new Color(1f, 0.8f, 0.2f);
            public Color tailOrange = new Color(1f, 0.52f, 0.08f);
            public Color dark = new Color(0.1f, 0.12f, 0.08f);
        }

        // Índices de hueso
        const int Root = 0, Hips = 1, Belly = 2, Jaw = 3, Head = 4;
        const int ArmL = 5, ArmR = 8, LegL = 11, LegR = 13, Tail = 15, BoneCount = 19;

        static readonly Vector3[] TailPts = { new(0, 0.22f, -0.2f), new(0, 0.14f, -0.42f), new(0, 0.14f, -0.6f), new(0, 0.22f, -0.75f), new(0, 0.34f, -0.83f) };
        static readonly float[] TailR = { 0.11f, 0.085f, 0.065f, 0.048f, 0.03f };

        static Vector3 Shoulder(int s) => new(s * 0.24f, 0.52f, 0.03f);
        static Vector3 Wrist(int s) => new(s * 0.55f, 0.48f, 0.06f);
        static Vector3 Hip(int s) => new(s * 0.14f, 0.22f, 0f);
        static Vector3 Ankle(int s) => new(s * 0.16f, 0.07f, 0.03f);

        static float Mouth(Settings st, Vector3 p) => st.mouthY + st.smile * p.x * p.x;

        // ---------------- Formas ----------------

        struct Contrib { public int bone; public float d; public int bone2; public float t; }

        /// <summary>Distancia del cuerpo; si 'parts' no es null, añade la distancia de cada pieza y su hueso.</summary>
        static float BodySdf(Settings st, Vector3 p, List<Contrib> parts)
        {
            float torso = SdfMesher.Ellipsoid(p, new Vector3(0f, 0.36f, 0f), new Vector3(0.33f, 0.34f, 0.29f));
            float chin = Mathf.Max(SdfMesher.Ellipsoid(p, new Vector3(0f, 0.62f, 0.06f), new Vector3(0.33f, 0.15f, 0.3f)), p.y - Mouth(st, p));
            parts?.Add(new Contrib { bone = Belly, d = torso, bone2 = -1 });
            parts?.Add(new Contrib { bone = Jaw, d = chin, bone2 = -1 });
            float d = SdfMesher.SMin(torso, chin, 0.08f);

            for (int s = -1; s <= 1; s += 2)
            {
                int ab = s < 0 ? ArmL : ArmR;
                float arm = SdfMesher.Capsule(p, Shoulder(s), Wrist(s), 0.075f, 0.058f, out float t);
                float hand = SdfMesher.Ellipsoid(p, new Vector3(s * 0.62f, 0.475f, 0.07f), new Vector3(0.075f, 0.055f, 0.07f));
                float fingers = float.MaxValue;
                for (int f = -1; f <= 1; f++)
                    fingers = Mathf.Min(fingers, SdfMesher.Ellipsoid(p, new Vector3(s * 0.69f, 0.47f, 0.07f + f * 0.04f), new Vector3(0.035f, 0.028f, 0.028f)));
                hand = SdfMesher.SMin(hand, fingers, 0.02f);
                float f2 = t * 2f; int i0 = Mathf.Min(1, Mathf.FloorToInt(f2));
                parts?.Add(new Contrib { bone = ab + i0, bone2 = ab + i0 + 1, t = f2 - i0, d = arm });
                parts?.Add(new Contrib { bone = ab + 2, d = hand, bone2 = -1 });
                d = SdfMesher.SMin(d, SdfMesher.SMin(arm, hand, 0.03f), 0.05f);

                int lb = s < 0 ? LegL : LegR;
                float leg = SdfMesher.Capsule(p, Hip(s), Ankle(s), 0.095f, 0.075f, out _);
                float foot = SdfMesher.Ellipsoid(p, new Vector3(s * 0.165f, 0.045f, 0.09f), new Vector3(0.095f, 0.045f, 0.12f));
                for (int f = -1; f <= 1; f++)
                    foot = SdfMesher.SMin(foot, SdfMesher.Ellipsoid(p, new Vector3(s * 0.165f + f * 0.05f, 0.035f, 0.2f), new Vector3(0.032f, 0.03f, 0.032f)), 0.02f);
                parts?.Add(new Contrib { bone = lb, d = leg, bone2 = -1 });
                parts?.Add(new Contrib { bone = lb + 1, d = foot, bone2 = -1 });
                d = SdfMesher.SMin(d, SdfMesher.SMin(leg, foot, 0.03f), 0.05f);
            }

            float tail = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float c = SdfMesher.Capsule(p, TailPts[i], TailPts[i + 1], TailR[i], TailR[i + 1], out float t);
                parts?.Add(new Contrib { bone = Tail + i, bone2 = i < 3 ? Tail + i + 1 : -1, t = t, d = c });
                tail = SdfMesher.SMin(tail, c, 0.02f);
            }
            d = SdfMesher.SMin(d, tail, 0.06f);
            return d;
        }

        static Vector3 EyeCenter(int s) => new(s * 0.155f, 0.9f, 0.16f);

        static float CapSdf(Settings st, Vector3 p)
        {
            float cap = SdfMesher.Ellipsoid(p, new Vector3(0f, 0.72f, 0.05f), new Vector3(0.35f, 0.2f, 0.32f));
            for (int s = -1; s <= 1; s += 2)
                cap = SdfMesher.SMin(cap, SdfMesher.Ellipsoid(p, new Vector3(s * 0.15f, 0.86f, 0.13f), new Vector3(0.11f, 0.09f, 0.1f)), 0.05f);
            return Mathf.Max(cap, Mouth(st, p) - p.y);
        }

        // ---------------- Construcción ----------------

        public class Materials { public Material skin, eyeWhite, pupil, lid, bubble, green, lime, orange; }

        public static SunnyVisual Build(Transform parent, Materials m, Settings st = null)
        {
            st ??= new Settings();
            var root = new GameObject("Modelo (Sunny)").transform;
            root.SetParent(parent, false);
            var v = root.gameObject.AddComponent<SunnyVisual>();
            v.scalePivot = Node("Escala", root, Vector3.zero);
            var meshRoot = Node("Malla", v.scalePivot, Vector3.zero);

            // Huesos (rotación identidad en reposo).
            var bones = new Transform[BoneCount];
            bones[Root] = Node("Raíz", meshRoot, Vector3.zero);
            bones[Hips] = Node("Cadera", bones[Root], new Vector3(0f, 0.36f, 0f));
            bones[Belly] = Node("Barriga", bones[Hips], new Vector3(0f, 0.33f, 0.06f));
            bones[Jaw] = Node("Mandíbula", bones[Hips], st.jawHinge);
            bones[Head] = Node("Tapa cabeza", bones[Hips], st.headHinge);
            for (int s = -1; s <= 1; s += 2)
            {
                int ab = s < 0 ? ArmL : ArmR; string side = s < 0 ? "I" : "D";
                bones[ab] = Node($"Brazo {side} 1", bones[Hips], Shoulder(s));
                bones[ab + 1] = Node($"Brazo {side} 2", bones[ab], Vector3.Lerp(Shoulder(s), Wrist(s), 0.5f));
                bones[ab + 2] = Node($"Brazo {side} 3", bones[ab + 1], Wrist(s));
                int lb = s < 0 ? LegL : LegR;
                bones[lb] = Node($"Pierna {side}", bones[Hips], Hip(s));
                bones[lb + 1] = Node($"Pie {side}", bones[lb], Ankle(s));
            }
            Transform prev = bones[Hips];
            for (int i = 0; i < 4; i++) { bones[Tail + i] = Node($"Cola {i + 1}", prev, TailPts[i]); prev = bones[Tail + i]; }
            // Node() recibe posiciones en espacio de la malla: corrige a locales.
            foreach (var b in bones) if (b != bones[Root]) b.position = meshRoot.TransformPoint(b.localPosition);
            FixLocal(bones, meshRoot);

            // ---- Cuerpo ----
            var bodyMesh = SdfMesher.Build(p => BodySdf(st, p, null), new Bounds(new Vector3(0f, 0.36f, -0.22f), new Vector3(1.6f, 0.8f, 1.5f)), st.cellSize, "Sunny_Cuerpo");
            var bv = bodyMesh.vertices; var bn = bodyMesh.normals;
            var colors = new Color[bv.Length];
            var weights = new BoneWeight[bv.Length];
            var parts = new List<Contrib>(24);
            var acc = new float[BoneCount];
            for (int i = 0; i < bv.Length; i++)
            {
                Vector3 p = bv[i];
                parts.Clear();
                BodySdf(st, p, parts);
                float dmin = float.MaxValue; foreach (var c in parts) dmin = Mathf.Min(dmin, c.d);
                Array.Clear(acc, 0, acc.Length);
                foreach (var c in parts)
                {
                    float w = Mathf.Exp(-Mathf.Max(0f, c.d - dmin) / 0.018f);
                    if (w < 0.01f) continue;
                    if (c.bone2 >= 0) { acc[c.bone] += w * (1f - c.t); acc[c.bone2] += w * c.t; }
                    else acc[c.bone] += w;
                }
                weights[i] = Top4(acc);
                colors[i] = BodyColor(st, p, bn[i]).linear;
            }
            bodyMesh.colors = colors;
            bodyMesh.boneWeights = weights;
            var bind = new Matrix4x4[BoneCount];
            for (int i = 0; i < BoneCount; i++) bind[i] = bones[i].worldToLocalMatrix * meshRoot.localToWorldMatrix;
            bodyMesh.bindposes = bind;

            var bodyGo = new GameObject("Cuerpo");
            bodyGo.transform.SetParent(meshRoot, false);
            var smr = bodyGo.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = bodyMesh; smr.bones = bones; smr.rootBone = bones[Root];
            smr.sharedMaterial = m.skin; smr.updateWhenOffscreen = true;
            // Las bindposes se calcularon respecto a 'Malla'; el renderer está en su hijo sin desplazar.
            v.skin = smr;

            // ---- Tapa de la cabeza (rígida, sigue al hueso) ----
            var capMesh = SdfMesher.Build(p => CapSdf(st, p), new Bounds(new Vector3(0f, 0.84f, 0.06f), new Vector3(0.8f, 0.36f, 0.72f)), st.cellSize * 0.85f, "Sunny_Tapa");
            var cv = capMesh.vertices; var cn = capMesh.normals; var ccol = new Color[cv.Length];
            for (int i = 0; i < cv.Length; i++) ccol[i] = CapColor(st, cv[i], cn[i]).linear;
            capMesh.colors = ccol;
            var capGo = new GameObject("Tapa");
            capGo.transform.SetParent(bones[Head], false);
            capGo.transform.position = meshRoot.TransformPoint(Vector3.zero);
            capGo.AddComponent<MeshFilter>().sharedMesh = capMesh;
            capGo.AddComponent<MeshRenderer>().sharedMaterial = m.skin;

            // ---- Ojos con párpados ----
            var lids = new List<Transform>();
            Transform MakeEye(int s)
            {
                var e = Node(s < 0 ? "Ojo I" : "Ojo D", bones[Head], Vector3.zero);
                e.position = meshRoot.TransformPoint(EyeCenter(s));
                Sphere("Blanco", e, Vector3.zero, Vector3.one * 0.2f, m.eyeWhite);
                Sphere("Pupila", e, new Vector3(s * 0.012f, -0.005f, 0.085f), new Vector3(0.1f, 0.11f, 0.05f), m.pupil);
                Sphere("Brillo", e, new Vector3(s * 0.03f, 0.035f, 0.098f), Vector3.one * 0.03f, m.eyeWhite);
                lids.Add(Sphere("Párpado", e, new Vector3(0f, 0.062f, -0.02f), new Vector3(0.215f, 0.1f, 0.215f), m.lid));
                return e;
            }
            v.eyeLeft = MakeEye(-1); v.eyeRight = MakeEye(1);
            v.lids = lids.ToArray();
            v.lidClosedY = 0f; v.lidClosedScale = new Vector3(0.235f, 0.22f, 0.235f);
            v.eyeScale = 1f;

            v.headBone = bones[Head]; v.jawBone = bones[Jaw]; v.bellyBone = bones[Belly];
            v.legLeft = bones[LegL]; v.legRight = bones[LegR];
            v.armLeft.bones = new[] { bones[ArmL], bones[ArmL + 1], bones[ArmL + 2] }; v.armLeft.tipLocal = new Vector3(-0.15f, 0f, 0f);
            v.armRight.bones = new[] { bones[ArmR], bones[ArmR + 1], bones[ArmR + 2] }; v.armRight.tipLocal = new Vector3(0.15f, 0f, 0f);
            v.tail.bones = new[] { bones[Tail], bones[Tail + 1], bones[Tail + 2], bones[Tail + 3] };
            v.tail.tipLocal = TailPts[4] - TailPts[3];

            // Bola del gigante: esfera limpia con barriga, bracitos, patitas y colita (se activa al inflarse).
            if (m.green && m.lime && m.orange)
            {
                var ball = Sphere("Bola gigante", v.scalePivot, new Vector3(0f, 0.33f, 0.04f), Vector3.one, m.green);
                Sphere("Barriga", ball, new Vector3(0f, -0.05f, 0.2f), new Vector3(0.84f, 0.8f, 0.66f), m.lime);
                for (int s = -1; s <= 1; s += 2)
                {
                    var arm = Sphere(s < 0 ? "Bracito I" : "Bracito D", ball, new Vector3(s * 0.47f, 0.1f, 0.06f), new Vector3(0.22f, 0.11f, 0.13f), m.green);
                    arm.localRotation = Quaternion.Euler(0f, 0f, s * -25f);
                    Sphere("Manita", arm, new Vector3(s * 0.45f, 0f, 0.05f), new Vector3(0.5f, 0.9f, 0.85f), m.green);
                    Sphere(s < 0 ? "Patita I" : "Patita D", ball, new Vector3(s * 0.2f, -0.47f, 0.17f), new Vector3(0.17f, 0.08f, 0.23f), m.green);
                }
                var tailNub = Sphere("Colita", ball, new Vector3(0f, -0.22f, -0.47f), new Vector3(0.12f, 0.12f, 0.2f), m.green);
                Sphere("Punta", tailNub, new Vector3(0f, 0.05f, -0.45f), new Vector3(0.8f, 0.8f, 0.7f), m.orange);
                ball.gameObject.SetActive(false);
                v.giantBall = ball;
            }

            v.mouthPoint = Node("Boca (origen)", bones[Head], Vector3.zero);
            v.mouthPoint.position = meshRoot.TransformPoint(new Vector3(0f, st.mouthY, 0.34f));

            if (m.bubble)
            {
                v.bubble = Sphere("Burbuja", root, Vector3.zero, Vector3.one, m.bubble); v.bubble.gameObject.SetActive(false);
                v.ping = Sphere("Señal de ayuda", root, Vector3.zero, Vector3.one, m.bubble); v.ping.gameObject.SetActive(false);
            }
            return v;
        }

        static Color BodyColor(Settings st, Vector3 p, Vector3 n)
        {
            float mouth = Mouth(st, p);
            // Suelo de la boca (cara superior plana del mentón).
            // Suelo de la boca: rojo/lengua solo dentro de la boca (delante); el borde lateral y trasero queda verde
            // para que con la boca cerrada no se vea un anillo rojo alrededor de la cabeza.
            if (p.y > mouth - 0.012f && n.y > 0.55f && p.y > 0.55f)
            {
                float m = InMouth(p);
                if (m > 1f) return st.green;
                return Mathf.Abs(p.x) < 0.14f && p.z > -0.02f && m < 0.7f ? st.tongue : st.mouthRed;
            }
            // Cola: transición amarilla y punta naranja.
            if (p.z < -0.7f && p.y > 0.12f) return st.tailOrange;
            if (p.z < -0.6f && p.y > 0.1f) return st.tailYellow;
            // Barriga y mentón lima por delante.
            bool belly = SdfMesher.Ellipsoid(p, new Vector3(0f, 0.34f, 0.2f), new Vector3(0.22f, 0.27f, 0.2f)) < 0.01f && p.z > 0.1f;
            bool chin = p.y > 0.5f && p.y < mouth && p.z > 0.08f && Mathf.Abs(p.x) < 0.3f;
            return belly || chin ? st.lime : st.green;
        }

        /// <summary>Zona interior de la boca vista desde arriba (≤ 1 dentro): un óvalo delante de la cabeza.</summary>
        static float InMouth(Vector3 p)
        {
            // Dentro de la huella del mentón (que tapa la tapa con la boca cerrada), con margen.
            float x = p.x / 0.2f, z = (p.z - 0.12f) / 0.18f;
            return x * x + z * z;
        }

        static Color CapColor(Settings st, Vector3 p, Vector3 n)
        {
            // Paladar: rojo solo en la parte delantera central (la que se ve con la boca abierta de frente).
            // La tapa sobresale de la mandíbula como un alero; si el rojo llegara al borde se vería desde abajo.
            if (p.y < Mouth(st, p) + 0.012f && n.y < -0.55f)
            {
                float x = p.x / 0.15f, z = (p.z - 0.2f) / 0.12f;
                return x * x + z * z <= 1f ? st.mouthRed : st.green;
            }
            for (int s = -1; s <= 1; s += 2)
                if ((p - new Vector3(s * 0.06f, 0.8f, 0.35f)).magnitude < 0.022f) return st.dark; // fosas nasales
            return st.green;
        }

        // ---------------- Utilidades ----------------

        static void FixLocal(Transform[] bones, Transform meshRoot) { }

        static Transform Node(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static Transform Sphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = mat;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos; t.localScale = scale;
            return t;
        }

        static BoneWeight Top4(float[] w)
        {
            int[] idx = { 0, 0, 0, 0 }; float[] val = { 0, 0, 0, 0 };
            for (int i = 0; i < w.Length; i++)
            {
                float x = w[i];
                for (int k = 0; k < 4; k++)
                    if (x > val[k])
                    {
                        for (int j = 3; j > k; j--) { val[j] = val[j - 1]; idx[j] = idx[j - 1]; }
                        val[k] = x; idx[k] = i; break;
                    }
            }
            float total = val[0] + val[1] + val[2] + val[3];
            if (total <= 0f) { val[0] = 1f; idx[0] = Belly; total = 1f; }
            return new BoneWeight
            {
                boneIndex0 = idx[0], weight0 = val[0] / total, boneIndex1 = idx[1], weight1 = val[1] / total,
                boneIndex2 = idx[2], weight2 = val[2] / total, boneIndex3 = idx[3], weight3 = val[3] / total,
            };
        }
    }
}
