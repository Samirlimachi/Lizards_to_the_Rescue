using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Convierte la malla estática de Sunny (Meshy, sin rig) en una malla con skinning:
    /// cuerpo, tapa de la cabeza (de la boca hacia arriba), mandíbula (labio inferior),
    /// brazos de 4 huesos y cola de 4 huesos. Los pesos se calculan por regiones con transiciones suaves.
    /// Las medidas están en coordenadas de la malla original (altura 1.42, mirando a +Z).
    /// </summary>
    public static class SunnyRig
    {
        [Serializable]
        public class Settings
        {
            [Header("Encaje en el personaje")]
            public float modelScale = 0.75f;
            public Vector3 meshOffset = new Vector3(0f, 0.711f, -0.5f);

            [Header("Boca (línea pintada)")]
            public float mouthY = 0.342f;
            [Tooltip("Cuánto sube la línea hacia las comisuras: y = mouthY + curve * x².")]
            public float mouthCurve = 0.35f;
            [Tooltip("Ancho de la transición de pesos en la línea de la boca (más pequeño = corte más limpio).")]
            public float mouthBand = 0.004f;
            [Tooltip("Delante de este z se separan los labios por la dirección de su superficie.")]
            public float cutFrontZ = 0.45f;
            [Tooltip("Abrir además un corte en la malla (no hace falta si el modelo ya tiene la boca esculpida).")]
            public bool cutMesh = true;
            [Tooltip("Por delante de este z están los labios (se separan por la dirección de su superficie); por detrás, el interior de la boca.")]
            public float lipFrontZ = 0.85f;
            [Tooltip("Dentro de la boca el hueco está algo más alto que la línea de los labios.")]
            public float innerGapOffset = 0.013f;
            [Tooltip("Cavidad de la boca (coordenadas de la malla): centro y tamaño.")]
            public Vector3 cavityCenter = new Vector3(0f, 0.34f, 0.7f);
            public Vector3 cavitySize = new Vector3(0.44f, 0.16f, 0.36f);
            public Vector3 hinge = new Vector3(0f, 0.37f, 0.35f);
            [Tooltip("La mandíbula solo afecta a la parte delantera: transición entre estos dos z.")]
            public Vector2 jawFrontZ = new Vector2(0.5f, 0.62f);
            public float neckY = 0.225f;

            [Header("Brazos")]
            public float shoulderX = 0.27f;
            public float handTipX = 0.86f;
            public Vector2 armBandY = new Vector2(-0.1f, 0.3f);

            [Header("Cola")]
            public float tailStartZ = 0.18f;
            public float tailEndZ = -0.95f;
            public float tailMaxY = 0f;

            [Header("Cuerpo")]
            public Vector3 bodyCenter = new Vector3(0f, -0.1f, 0.5f);
            [Tooltip("Centro y radio de la barriga (hueso que la infla).")]
            public Vector3 bellyCenter = new Vector3(0f, -0.12f, 0.55f);
            public float bellyRadius = 0.42f;
            public float bellyMinY = -0.45f;

            [Header("Ojos")]
            public float eyeRadius = 0.17f;
        }

        const int ArmBones = 4, TailBones = 4;

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Crea la jerarquía, la malla con pesos y el SunnyVisual bajo 'parent'.</summary>
        public static SunnyVisual Build(Transform parent, Mesh source, Material material, Material bubbleMat, Material mouthMat = null, Material tongueMat = null, Settings st = null)
        {
            st ??= new Settings();
            var verts = source.vertices;
            var normals = source.normals;

            var root = new GameObject("Modelo (Sunny)").transform;
            root.SetParent(parent, false);
            var v = root.gameObject.AddComponent<SunnyVisual>();

            v.scalePivot = Node("Escala", root, Vector3.zero);
            var meshRoot = Node("Malla", v.scalePivot, st.meshOffset * st.modelScale);
            meshRoot.localScale = Vector3.one * st.modelScale;

            var rootBone = Node("Raíz", meshRoot, Vector3.zero);
            var body = Node("Cuerpo", rootBone, st.bodyCenter);
            var headBone = Node("Tapa cabeza", body, st.hinge - st.bodyCenter);
            var jawBone = Node("Mandíbula", body, st.hinge - st.bodyCenter);
            v.headBone = headBone; v.jawBone = jawBone;

            // Ojos: centro de los abultamientos superiores de cada lado.
            Transform Eye(int side)
            {
                Vector3 c = SliceCenter(verts, q => q.y > 0.5f && side * q.x > 0.08f && q.z > 0.35f, new Vector3(side * 0.2f, 0.58f, 0.6f));
                var e = Node(side < 0 ? "Ojo I" : "Ojo D", headBone, Vector3.zero);
                e.localPosition = Local(headBone, meshRoot, c);
                return e;
            }
            var eyeL = Eye(-1); var eyeR = Eye(1);
            v.eyeLeft = eyeL; v.eyeRight = eyeR;
            var bellyBone = Node("Barriga", body, Vector3.zero);
            bellyBone.localPosition = Local(body, meshRoot, st.bellyCenter);
            v.bellyBone = bellyBone;

            // Huesos de los brazos: posición media de los vértices del brazo en cada tramo.
            Transform[] ArmChain(int side, string name)
            {
                var chain = new Transform[ArmBones];
                Transform prev = body;
                for (int i = 0; i < ArmBones; i++)
                {
                    float x = Mathf.Lerp(st.shoulderX, st.handTipX, i / (float)ArmBones);
                    Vector3 c = SliceCenter(verts, q => Mathf.Abs(q.x - side * x) < 0.03f && q.y > st.armBandY.x && q.y < st.armBandY.y && Mathf.Sign(q.x) == side,
                        new Vector3(side * x, 0.1f, 0.54f));
                    c.x = side * x;
                    var b = Node($"{name} {i + 1}", prev, prev.InverseTransformPoint(meshRoot.TransformPoint(c)) );
                    b.localPosition = Local(prev, meshRoot, c);
                    chain[i] = b; prev = b;
                }
                return chain;
            }
            var armL = ArmChain(-1, "Brazo I");
            var armR = ArmChain(1, "Brazo D");

            var tailChain = new Transform[TailBones];
            {
                Transform prev = body;
                for (int i = 0; i < TailBones; i++)
                {
                    float z = Mathf.Lerp(st.tailStartZ, st.tailEndZ, i / (float)TailBones);
                    Vector3 c = SliceCenter(verts, q => Mathf.Abs(q.z - z) < 0.04f && q.y < st.tailMaxY && Mathf.Abs(q.x) < 0.3f, new Vector3(0f, -0.35f, z));
                    c.x = 0f; c.z = z;
                    var b = Node($"Cola {i + 1}", prev, Vector3.zero);
                    b.localPosition = Local(prev, meshRoot, c);
                    tailChain[i] = b; prev = b;
                }
            }

            // Lista de huesos e índices.
            var bones = new List<Transform> { rootBone, body, headBone, jawBone };
            int iBody = 1, iHead = 2, iJaw = 3;
            int iEyeL = bones.Count; bones.Add(eyeL);
            int iEyeR = bones.Count; bones.Add(eyeR);
            int iBelly = bones.Count; bones.Add(bellyBone);
            Vector3 cEyeL = meshRoot.InverseTransformPoint(eyeL.position), cEyeR = meshRoot.InverseTransformPoint(eyeR.position);
            int iArmL = bones.Count; bones.AddRange(armL);
            int iArmR = bones.Count; bones.AddRange(armR);
            int iTail = bones.Count; bones.AddRange(tailChain);

            // Pesos por vértice.
            var weights = new BoneWeight[verts.Length];
            var upper = new float[verts.Length];
            var w = new float[bones.Count];
            for (int vi = 0; vi < verts.Length; vi++)
            {
                Array.Clear(w, 0, w.Length);
                Vector3 q = verts[vi];
                float ax = Mathf.Abs(q.x);

                float headM = Smooth(st.neckY - 0.02f, st.neckY + 0.03f, q.y);
                float mouth = st.mouthY + st.mouthCurve * q.x * q.x;
                if (q.z <= st.lipFrontZ) mouth += st.innerGapOffset;
                float above = Smooth(mouth - st.mouthBand, mouth + st.mouthBand, q.y);
                // Junto a los labios el modelo tiene pliegues: lo que mira hacia abajo es del labio superior
                // (y del paladar), lo que mira hacia arriba es del labio inferior (y del suelo de la boca).
                if (q.z > st.lipFrontZ && Mathf.Abs(q.y - mouth) < 0.03f && normals.Length == verts.Length && Mathf.Abs(normals[vi].y) > 0.1f)
                    above = normals[vi].y < 0f ? 1f : 0f;
                w[iHead] = headM * above;
                upper[vi] = w[iHead];
                w[iJaw] = headM * (1f - above) * Smooth(st.jawFrontZ.x, st.jawFrontZ.y, q.z);

                float arm = (1f - headM) * Smooth(st.shoulderX - 0.03f, st.shoulderX + 0.05f, ax)
                            * Smooth(st.armBandY.x, st.armBandY.x + 0.06f, q.y) * (1f - Smooth(st.armBandY.y - 0.06f, st.armBandY.y, q.y));
                if (arm > 0f)
                {
                    float f = Mathf.Clamp((ax - st.shoulderX) / (st.handTipX - st.shoulderX) * ArmBones, 0f, ArmBones - 1f);
                    int a0 = Mathf.FloorToInt(f), a1 = Mathf.Min(a0 + 1, ArmBones - 1);
                    float fr = f - a0;
                    int baseI = q.x < 0f ? iArmL : iArmR;
                    w[baseI + a0] += arm * (1f - fr);
                    w[baseI + a1] += arm * fr;
                }

                float tl = (1f - headM) * (1f - arm) * Smooth(st.tailStartZ, st.tailStartZ - 0.14f, q.z) * Smooth(st.tailMaxY + 0.05f, st.tailMaxY - 0.05f, q.y);
                if (tl > 0f)
                {
                    float f = Mathf.Clamp((st.tailStartZ - q.z) / (st.tailStartZ - st.tailEndZ) * TailBones, 0f, TailBones - 1f);
                    int a0 = Mathf.FloorToInt(f), a1 = Mathf.Min(a0 + 1, TailBones - 1);
                    float fr = f - a0;
                    w[iTail + a0] += tl * (1f - fr);
                    w[iTail + a1] += tl * fr;
                }

                // Ojos: parte de la tapa cercana a cada ojo.
                if (w[iHead] > 0f)
                {
                    float el = Smooth(st.eyeRadius, st.eyeRadius * 0.55f, (q - cEyeL).magnitude);
                    float er = Smooth(st.eyeRadius, st.eyeRadius * 0.55f, (q - cEyeR).magnitude);
                    w[iEyeL] = w[iHead] * el; w[iEyeR] = w[iHead] * er;
                    w[iHead] *= 1f - Mathf.Min(1f, el + er);
                }
                // Barriga: torso (sin cabeza, brazos, cola ni piernas), con caída suave.
                float bel = (1f - headM) * (1f - arm) * (1f - tl) * Smooth(st.bellyRadius, st.bellyRadius * 0.45f, (q - st.bellyCenter).magnitude)
                            * Smooth(st.bellyMinY - 0.05f, st.bellyMinY + 0.08f, q.y);
                w[iBelly] = bel * 0.9f;

                float used = 0f; for (int k = 2; k < w.Length; k++) used += w[k];
                w[iBody] = Mathf.Max(0f, 1f - used);
                weights[vi] = Top4(w);
            }

            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = "Sunny_Rig";
            mesh.boneWeights = weights;
            // Corte de la boca: quita los triángulos delanteros que unen la tapa con la parte de abajo.
            for (int sm = 0; st.cutMesh && sm < mesh.subMeshCount; sm++)
            {
                var tris = mesh.GetTriangles(sm);
                var kept = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    float lo = Mathf.Min(upper[a], Mathf.Min(upper[b], upper[c]));
                    float hi = Mathf.Max(upper[a], Mathf.Max(upper[b], upper[c]));
                    bool front = verts[a].z > st.lipFrontZ && verts[b].z > st.lipFrontZ && verts[c].z > st.lipFrontZ;
                    Vector3 cen = (verts[a] + verts[b] + verts[c]) / 3f;
                    front &= Mathf.Abs(cen.y - (st.mouthY + st.mouthCurve * cen.x * cen.x)) < 0.05f && Mathf.Abs(cen.x) < 0.32f;
                    if (front && hi - lo > 0.5f) continue;
                    kept.Add(a); kept.Add(b); kept.Add(c);
                }
                mesh.SetTriangles(kept, sm);
            }
            var bind = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++) bind[i] = bones[i].worldToLocalMatrix * meshRoot.localToWorldMatrix;
            mesh.bindposes = bind;
            mesh.RecalculateBounds();

            var smr = meshRoot.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones.ToArray();
            smr.rootBone = rootBone;
            smr.sharedMaterial = material;
            smr.updateWhenOffscreen = true;
            v.skin = smr;

            v.armLeft.bones = armL; v.armLeft.tipLocal = new Vector3(-(st.handTipX - Mathf.Lerp(st.shoulderX, st.handTipX, (ArmBones - 1f) / ArmBones)), 0f, 0f);
            v.armRight.bones = armR; v.armRight.tipLocal = new Vector3(st.handTipX - Mathf.Lerp(st.shoulderX, st.handTipX, (ArmBones - 1f) / ArmBones), 0f, 0f);
            v.tail.bones = tailChain; v.tail.tipLocal = new Vector3(0f, 0f, -(Mathf.Abs(st.tailEndZ - st.tailStartZ) / TailBones));

            // Cavidad de la boca (va con el labio inferior) y lengua.
            if (mouthMat)
            {
                var cav = Sphere("Interior boca", jawBone, mouthMat);
                cav.localPosition = Local(jawBone, meshRoot, st.cavityCenter);
                cav.localScale = st.cavitySize;
                var cavTop = Sphere("Paladar", headBone, mouthMat);
                cavTop.localPosition = Local(headBone, meshRoot, st.cavityCenter + Vector3.up * 0.03f);
                cavTop.localScale = Vector3.Scale(st.cavitySize, new Vector3(0.95f, 0.8f, 0.95f));
            }
            if (tongueMat)
            {
                var tg = Sphere("Lengua (reposo)", jawBone, tongueMat);
                tg.localPosition = Local(jawBone, meshRoot, st.cavityCenter + new Vector3(0f, 0.02f, 0.04f));
                tg.localScale = new Vector3(st.cavitySize.x * 0.6f, 0.06f, st.cavitySize.z * 0.7f);
            }
            v.mouthPoint = Node("Boca (origen)", headBone, Vector3.zero);
            v.mouthPoint.localPosition = Local(headBone, meshRoot, new Vector3(0f, st.mouthY, 0.9f));

            if (bubbleMat)
            {
                v.bubble = Sphere("Burbuja", root, bubbleMat); v.bubble.gameObject.SetActive(false);
                v.ping = Sphere("Señal de ayuda", root, bubbleMat); v.ping.gameObject.SetActive(false);
            }
            return v;
        }

        static Vector3 Local(Transform parent, Transform meshRoot, Vector3 meshPoint) => parent.InverseTransformPoint(meshRoot.TransformPoint(meshPoint));

        static Vector3 SliceCenter(Vector3[] verts, Func<Vector3, bool> inSlice, Vector3 fallback)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var q in verts) if (inSlice(q)) { sum += q; n++; }
            return n > 20 ? sum / n : fallback;
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
                        for (int m = 3; m > k; m--) { val[m] = val[m - 1]; idx[m] = idx[m - 1]; }
                        val[k] = x; idx[k] = i; break;
                    }
            }
            float total = val[0] + val[1] + val[2] + val[3];
            if (total <= 0f) { val[0] = 1f; idx[0] = 1; total = 1f; }
            return new BoneWeight
            {
                boneIndex0 = idx[0], weight0 = val[0] / total,
                boneIndex1 = idx[1], weight1 = val[1] / total,
                boneIndex2 = idx[2], weight2 = val[2] / total,
                boneIndex3 = idx[3], weight3 = val[3] / total,
            };
        }

        static Transform Node(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static Transform Sphere(string name, Transform parent, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
