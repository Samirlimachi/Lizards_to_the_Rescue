using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// El Jefe del refugio, diseño propio: un sapo rey enorme y regordete apoyado en un tronco.
    /// - Cuerpo y cabeza de una sola superficie suave (SDF → Surface Nets) con barriga crema, manchas y mejillas.
    /// - Tapa de la cabeza articulada en la nuca: al abrirse enseña boca y lengua; dientecitos (uno de oro).
    /// - Dos ojos saltones con iris dorado y pupila horizontal, párpados que parpadean.
    /// - Saco bucal que se infla al croar, brazos gordos con dedos redondos, corona dorada con bolitas y gema.
    /// Medidas en metros; modelado mirando a +Z (la raíz se gira para mirar a -Z como el Jefe de la escena).
    /// </summary>
    public static class JefeModel
    {
        public class Materials { public Material skin, eye, iris, pupil, lid, tooth, gold, gem, wood; }

        static readonly Color Green = new(0.33f, 0.86f, 0.18f), Spot = new(0.16f, 0.55f, 0.12f), Cream = new(0.74f, 0.96f, 0.38f),
            Cheek = new(0.48f, 0.22f, 0.42f), MouthRed = new(0.28f, 0.03f, 0.09f), Tongue = new(0.62f, 0.2f, 0.36f), Lip = new(0.14f, 0.2f, 0.08f),
            Wart = new(0.52f, 0.95f, 0.3f), Vein = new(0.22f, 0.62f, 0.14f);

        const float Cell = 0.065f;
        static readonly Vector3 HeadHinge = new(0f, 5.9f, -2.9f);
        static readonly Vector3 TorsoPivot = new(0f, 2.4f, -0.4f);

        static float MouthY(Vector3 p) => 5.3f + 0.1f * p.x * p.x + Mathf.Max(0f, -p.z - 1f) * 0.35f;
        static Vector3 Eye(int s) => s < 0 ? new(-1.95f, 8.35f, 1.85f) : new(1.75f, 7.75f, 2.15f);
        static float EyeSize(int s) => s < 0 ? 1.5f : 0.95f;
        static Vector3 Shoulder(int s) => new(s * 3.9f, 5.0f, 0.6f);
        static Vector3 Elbow(int s) => new(s * 5.1f, 3.3f, 1.3f);
        static Vector3 Wrist(int s) => new(s * 4.15f, 2.25f, 2.6f);

        static readonly List<Vector3> spots = MakeSpots();
        static List<Vector3> MakeSpots()
        {
            var r = new System.Random(7); var l = new List<Vector3>();
            for (int i = 0; i < 26; i++)
            {
                float a = (float)(r.NextDouble() * Math.PI * 2), y = 2.5f + (float)r.NextDouble() * 6f;
                var v = new Vector3(Mathf.Cos(a) * 4.5f, y, Mathf.Sin(a) * 3.8f);
                if (v.z > 1.2f && Mathf.Abs(v.x) < 3.2f) v.z = -v.z; // la barriga no lleva manchas
                l.Add(v);
            }
            return l;
        }

        struct WartInfo { public Vector3 c; public float r; }
        static readonly List<WartInfo> warts = MakeWarts();

        /// <summary>Coloca las verrugas sobre la superficie: las grandes donde estaban los bultos y otras pequeñas repartidas.</summary>
        static List<WartInfo> MakeWarts()
        {
            var l = new List<WartInfo>();
            var r = new System.Random(21);
            Vector3 OnSurface(Vector3 dirFrom, Vector3 center)
            {
                Vector3 q = center;
                for (int k = 0; k < 40; k++) { float d = BaseBody(q); if (d > -0.01f) break; q += dirFrom * Mathf.Max(0.05f, -d); }
                return q;
            }
            // Las grandes (antes bultos): se pegan a la piel en vez de flotar como bolas.
            for (int i = 0; i < spots.Count; i += 2)
            {
                var c = new Vector3(0f, spots[i].y, 0f);
                var q = OnSurface((spots[i] - c).normalized, c);
                l.Add(new WartInfo { c = q, r = 0.3f + (i % 5) * 0.05f });
            }
            // Pequeñas: lomo, hombros, cabeza y brazos de arriba; nunca en barriga ni boca.
            for (int i = 0; i < 90; i++)
            {
                float a = (float)(r.NextDouble() * Math.PI * 2), y = 1.5f + (float)r.NextDouble() * 7.5f;
                var dir = new Vector3(Mathf.Cos(a), (float)(r.NextDouble() - 0.3) * 0.8f, Mathf.Sin(a)).normalized;
                if (dir.z > 0.35f && Mathf.Abs(dir.x) < 0.75f && y < 6.3f) continue;     // barriga
                var q = OnSurface(dir, new Vector3(0f, y, 0.3f));
                if (Mathf.Abs(q.y - MouthY(q)) < 0.45f) continue;                         // labios
                l.Add(new WartInfo { c = q, r = 0.1f + (float)r.NextDouble() * 0.14f });
            }
            return l;
        }

        static float BaseBody(Vector3 p)
        {
            float body = SdfMesher.Ellipsoid(p, new Vector3(0f, 3.5f, 0f), new Vector3(4.7f, 3.4f, 3.5f));
            float hips = SdfMesher.Ellipsoid(p, new Vector3(0f, 1.9f, 0.2f), new Vector3(4.9f, 1.9f, 3.6f));
            float head = SdfMesher.Ellipsoid(p, new Vector3(0f, 6.0f, 0.6f), new Vector3(4.2f, 2.3f, 3.2f));
            float d = SdfMesher.SMin(SdfMesher.SMin(body, hips, 0.8f), head, 1.0f);
            for (int s = -1; s <= 1; s += 2) // hombros anchos
                d = SdfMesher.SMin(d, SdfMesher.Ellipsoid(p, new Vector3(s * 3.7f, 5.3f, 0.2f), new Vector3(1.9f, 1.6f, 1.7f)), 0.8f);
            return SdfMesher.SMin(d, SdfMesher.Ellipsoid(p, new Vector3(2.9f, 6.3f, -1.4f), new Vector3(1.5f, 1.3f, 1.4f)), 0.7f);
        }

        static float Full(Vector3 p)
        {
            float d = BaseBody(p);
            if (d < 0.6f)
                foreach (var w in warts)
                {
                    float dw = (p - w.c).magnitude - w.r;
                    if (dw < 0.4f) d = SdfMesher.SMin(d, dw, w.r * 0.45f);
                }
            for (int s = -1; s <= 1; s += 2)
            {
                d = SdfMesher.SMin(d, SdfMesher.Ellipsoid(p, Eye(s) + new Vector3(0f, -0.6f, -0.75f), new Vector3(1.25f, 1.0f, 1.1f) * EyeSize(s)), 0.5f);
                // Ceja: arco carnoso por encima del ojo, inclinado hacia dentro (mirada malvada).
                Vector3 bc = Eye(s) + new Vector3(-s * 0.15f, 0.62f, -0.35f) * EyeSize(s);
                Vector3 bq = Quaternion.Euler(0f, 0f, s * 14f) * (p - bc);
                d = SdfMesher.SMin(d, SdfMesher.Ellipsoid(bq, Vector3.zero, new Vector3(1.0f, 0.3f, 0.75f) * EyeSize(s)), 0.3f);
                // Fosa nasal.
                d = Mathf.Max(d, -((p - new Vector3(s * 0.55f, 6.75f, 3.7f)).magnitude - 0.17f));
                float thigh = SdfMesher.Ellipsoid(p, new Vector3(s * 3.3f, 1.2f, 1.3f), new Vector3(1.6f, 1.2f, 2.0f));
                float foot = SdfMesher.Ellipsoid(p, new Vector3(s * 3.0f, 0.35f, 3.1f), new Vector3(1.2f, 0.35f, 1.0f));
                for (int f = -1; f <= 1; f++)
                    foot = SdfMesher.SMin(foot, SdfMesher.Ellipsoid(p, new Vector3(s * 3.0f + f * 0.55f, 0.3f, 4.0f), new Vector3(0.3f, 0.28f, 0.32f)), 0.15f);
                d = SdfMesher.SMin(d, SdfMesher.SMin(thigh, foot, 0.3f), 0.4f);
            }
            return d;
        }
        // Labio grueso: una franja de piel algo más hinchada a cada lado de la línea de la boca.
        static float LipBand(Vector3 p, float side) => p.z < -0.5f ? 9f : Mathf.Max(Full(p) - 0.14f, Mathf.Abs(p.y - MouthY(p) - side * 0.16f) - 0.16f);
        // Corte de la boca con borde redondeado (unión suave): sin dientes de sierra en el labio.
        static float SMax(float a, float b, float k) => -SdfMesher.SMin(-a, -b, k);
        static float Lower(Vector3 p) => SMax(SdfMesher.SMin(Full(p), LipBand(p, -1f), 0.08f), p.y - MouthY(p), 0.14f);
        static float Cap(Vector3 p) => SMax(SdfMesher.SMin(Full(p), LipBand(p, 1f), 0.08f), MouthY(p) - p.y, 0.14f);

        // Mano apoyada en la rodilla: palma, cuatro dedos que caen por delante con yemas redondas, y pulgar.
        static Vector3 W(int s) => Wrist(s);
        static float Hand(Vector3 p, int s)
        {
            Vector3 w = W(s);
            float hand = SdfMesher.Ellipsoid(p, w + new Vector3(-s * 0.05f, 0.02f, 0.32f), new Vector3(0.78f, 0.34f, 0.62f));
            for (int f = 0; f < 4; f++)
            {
                float dx = (f - 1.5f) * 0.4f * s;
                float len = f == 0 || f == 3 ? 0.85f : 1f;
                Vector3 k = w + new Vector3(dx, 0.08f, 0.78f);
                Vector3 m = w + new Vector3(dx * 1.12f, -0.02f, 0.78f + 0.38f * len);
                Vector3 t = m + new Vector3(dx * 0.15f, -0.42f * len, 0.2f * len);
                float finger = SdfMesher.SMin(SdfMesher.Capsule(p, k, m, 0.2f, 0.17f, out _), SdfMesher.Capsule(p, m, t, 0.17f, 0.14f, out _), 0.06f);
                finger = SdfMesher.SMin(finger, (p - (t + new Vector3(0f, -0.04f, 0.03f))).magnitude - 0.21f, 0.06f); // yema
                hand = SdfMesher.SMin(hand, finger, 0.12f);
            }
            Vector3 t0 = w + new Vector3(-s * 0.6f, 0f, 0.35f), t1 = w + new Vector3(-s * 0.9f, -0.18f, 0.72f);
            float thumb = SdfMesher.SMin(SdfMesher.Capsule(p, t0, t1, 0.21f, 0.16f, out _), (p - t1).magnitude - 0.2f, 0.05f);
            return SdfMesher.SMin(hand, thumb, 0.12f);
        }

        /// <summary>Brazo de arriba (hombro → codo), con verrugas por fuera.</summary>
        static float UpperArm(Vector3 p, int s)
        {
            float arm = SdfMesher.Capsule(p, Shoulder(s), Elbow(s), 1.1f, 0.85f, out _);
            for (int i = 0; i < 5; i++)
            {
                Vector3 c = Vector3.Lerp(Shoulder(s), Elbow(s), i / 5f) + new Vector3(s * 0.85f, 0.4f + (i % 3) * 0.1f, -0.2f + (i % 2) * 0.3f);
                float dw = (p - c).magnitude - (0.13f + (i % 3) * 0.04f);
                if (dw < 0.3f) arm = SdfMesher.SMin(arm, dw, 0.06f);
            }
            return arm;
        }

        /// <summary>Antebrazo + mano (cuelga del codo). La bola del codo se solapa con el brazo para que no se vea la unión.</summary>
        static float Forearm(Vector3 p, int s)
        {
            float fore = SdfMesher.Capsule(p, Elbow(s), Wrist(s), 0.86f, 0.62f, out _);
            return SdfMesher.SMin(fore, Hand(p, s), 0.25f);
        }

        static Color ArmColor(Vector3 p, Vector3 n, int s)
        {
            Vector3 w = W(s);
            if (p.z > w.z + 0.95f && p.y < w.y - 0.15f) return Color.Lerp(Green, Cream, 0.55f);   // yemas
            if (n.y < -0.4f) return Color.Lerp(Green, Cream, 0.3f);                                 // por debajo, más claro
            return Mathf.PerlinNoise(p.x * 2.5f, p.y * 2.5f + p.z) > 0.68f ? Color.Lerp(Green, Spot, 0.6f) : Green;
        }

        static Color SkinColor(Vector3 p, Vector3 n, bool cap)
        {
            float my = MouthY(p);
            // Interior de la boca: la cara del corte (normal casi vertical, junto a la línea de la boca).
            if (Mathf.Abs(p.y - my) < Cell * 1.6f && Mathf.Abs(n.y) > 0.6f)
            {
                if (cap) return MouthRed * 0.8f;
                float tq = Mathf.Pow(p.x / 1.7f, 2) + Mathf.Pow((p.z - 1.3f) / 1.5f, 2);
                return tq < 1f ? Tongue : MouthRed;
            }
            if (Mathf.Abs(p.y - my) < 0.3f && p.z > -0.5f) return Color.Lerp(Lip, Green, Mathf.Abs(p.y - my) / 0.3f * 0.6f);
            Color c = Green;
            // Barriga y papada crema por delante.
            float belly = Mathf.Pow(p.x / 3.1f, 2) + Mathf.Pow((p.y - 3.1f) / 2.7f, 2);
            if (n.z > 0.15f && belly < 1f && p.y < my - 0.25f) c = Color.Lerp(Green, Cream, Mathf.Clamp01((1f - belly) * 6f));
            // Mejillas.
            for (int s = -1; s <= 1; s += 2)
                if ((p - new Vector3(s * 3.0f, my + 0.5f, 2.2f)).sqrMagnitude < 0.55f) c = Color.Lerp(c, Cheek, 0.85f);
            // Manchas moradas en lomo y cabeza.
            for (int i = 1; i < spots.Count; i += 2)
            {
                float d2 = (p - spots[i]).sqrMagnitude;
                if (d2 < 0.5f) { c = Color.Lerp(c, Spot, d2 < 0.32f ? 1f : 0.5f); break; }
            }
            // Verrugas: punta clara y amarillenta, base más oscura (se leen bien desde lejos).
            foreach (var w in warts)
            {
                float k = (p - w.c).magnitude / (w.r * 1.35f);
                if (k < 1f) { c = k < 0.55f ? Color.Lerp(Wart, Cream, 0.35f * (1f - k / 0.55f)) : Color.Lerp(c * 0.75f, Wart, (1f - k) * 2f); break; }
            }
            // Venas: líneas onduladas sobre lomo y cabeza.
            float vein = Mathf.Abs(Mathf.Sin(p.x * 2.1f + Mathf.Sin(p.y * 1.7f) * 1.5f) * Mathf.Sin(p.y * 1.3f + p.z * 0.9f));
            if (n.z < 0.5f && vein < 0.05f) c = Color.Lerp(c, Vein, 0.75f);
            return c;
        }

        static Mesh Sdf(Func<Vector3, float> f, Bounds b, string name, Vector3 origin, Func<Vector3, Vector3, Color> color)
        {
            var mesh = SdfMesher.Build(f, b, Cell, name);
            var v = mesh.vertices; var cols = new Color[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 n = SdfMesher.Gradient(f, v[i], Cell * 0.5f).normalized;
                cols[i] = (color != null ? color(v[i], n) : Color.white).linear;
                v[i] -= origin;
            }
            mesh.vertices = v; mesh.colors = cols;
            mesh.RecalculateBounds();
            return mesh;
        }

        static Transform Node(string name, Transform parent, Vector3 modelPos, Dictionary<Transform, Vector3> at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = modelPos - (at.TryGetValue(parent, out var pp) ? pp : Vector3.zero);
            at[t] = modelPos;
            return t;
        }

        static GameObject Mesh(string name, Transform parent, Mesh mesh, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            return go;
        }

        static Mesh sphere;
        static Transform Ball(string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            if (!sphere) { var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = tmp.GetComponent<MeshFilter>().sharedMesh; UnityEngine.Object.DestroyImmediate(tmp); }
            var go = Mesh(name, parent, sphere, m);
            go.transform.localPosition = localPos; go.transform.localScale = scale;
            return go.transform;
        }

        public static JefeRig Build(Transform parent, Materials m)
        {
            var at = new Dictionary<Transform, Vector3>();
            var root = new GameObject("Rey sapo (modelo)").transform;
            root.SetParent(parent, false);
            root.localRotation = Quaternion.Euler(0f, 180f, 0f); // la escena espera que mire a -Z
            at[root] = Vector3.zero;
            var rig = root.gameObject.AddComponent<JefeRig>();

            rig.torso = Node("Torso", root, TorsoPivot, at);
            var big = new Bounds(new Vector3(0f, 4.8f, 0.1f), new Vector3(12.6f, 10.4f, 10.2f)); // holgado: nada se corta
            Mesh("Cuerpo", rig.torso, Sdf(Lower, big, "Jefe_Cuerpo", TorsoPivot, (p, n) => SkinColor(p, n, false)), m.skin);

            rig.head = Node("Cabeza (bisagra)", rig.torso, HeadHinge, at);
            Mesh("Tapa de la cabeza", rig.head, Sdf(Cap, big, "Jefe_Cabeza", HeadHinge, (p, n) => SkinColor(p, n, true)), m.skin);

            // Ojos: globo, iris dorado con pupila horizontal y párpado (cúpula que baja al parpadear).
            var lidMesh = Sdf(p => Mathf.Max(Mathf.Abs(p.magnitude - 1.08f) - 0.07f, 0.12f - p.y),
                new Bounds(Vector3.zero, Vector3.one * 2.6f), "Jefe_Parpado", Vector3.zero, null);
            var lids = new List<Transform>(); var irises = new List<Transform>();
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = Node(s < 0 ? "Ojo I" : "Ojo D", rig.head, Eye(s), at);
                eye.localScale = Vector3.one * 1.25f * EyeSize(s);
                Ball("Globo", eye, Vector3.zero, Vector3.one * 2.0f, m.eye);
                var iris = new GameObject("Mirada").transform; iris.SetParent(eye, false);
                Vector3 fwd = new Vector3(s * 0.25f, 0.05f, 1f).normalized;
                iris.localRotation = Quaternion.LookRotation(fwd);
                Ball("Iris", iris, new Vector3(0f, 0f, 0.86f), new Vector3(1.05f, 0.95f, 0.35f), m.iris);
                Ball("Pupila", iris, new Vector3(0f, 0f, 1.0f), new Vector3(0.16f, 0.8f, 0.12f), m.pupil);
                Ball("Brillo", iris, new Vector3(0.22f, 0.22f, 1.05f), Vector3.one * 0.18f, m.eye);
                irises.Add(iris);
                var lid = new GameObject("Párpado").transform; lid.SetParent(eye, false);
                Mesh("Malla", lid, lidMesh, m.lid);
                lid.localRotation = Quaternion.Euler(s < 0 ? 4f : 30f, 0f, s < 0 ? 0f : -12f); // el grande, abierto de par en par; el pequeño, caído
                lids.Add(lid);
            }
            var extra = Node("Ojo extra", rig.head, new Vector3(0.35f, 8.75f, 1.35f), at);
            extra.localScale = Vector3.one * 0.55f;
            Ball("Globo", extra, Vector3.zero, Vector3.one * 2.0f, m.eye);
            var extraIris = new GameObject("Mirada").transform; extraIris.SetParent(extra, false);
            extraIris.localRotation = Quaternion.LookRotation(new Vector3(0.1f, 0.35f, 1f));
            Ball("Iris", extraIris, new Vector3(0f, 0f, 0.86f), new Vector3(1.05f, 0.95f, 0.35f), m.iris);
            Ball("Pupila", extraIris, new Vector3(0f, 0f, 1.0f), new Vector3(0.16f, 0.8f, 0.12f), m.pupil);
            irises.Add(extraIris);
            rig.lids = lids.ToArray(); rig.irises = irises.ToArray();

            // Corona: aro dorado con bolitas y gema rosa.
            var crownMesh = Sdf(p =>
            {
                float rr = new Vector2(p.x, p.z).magnitude;
                float band = Mathf.Max(Mathf.Abs(rr - 0.95f) - 0.13f, Mathf.Abs(p.y - 0.28f) - 0.28f);
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2f; var c = new Vector3(Mathf.Sin(a) * 0.95f, 0.85f, Mathf.Cos(a) * 0.95f);
                    band = SdfMesher.SMin(band, SdfMesher.Capsule(p, new Vector3(c.x, 0.4f, c.z), c, 0.16f, 0.1f, out _), 0.08f);
                    band = SdfMesher.SMin(band, (p - c).magnitude - 0.2f, 0.06f);
                }
                return band;
            }, new Bounds(new Vector3(0f, 0.6f, 0f), new Vector3(2.8f, 1.6f, 2.8f)), "Jefe_Corona", Vector3.zero, null);
            rig.crown = Node("Corona", rig.head, new Vector3(0.3f, 8.75f, -0.3f), at);
            var crownBody = Mesh("Aro", rig.crown, crownMesh, m.gold).transform;
            crownBody.localRotation = Quaternion.Euler(-10f, 20f, 16f); crownBody.localScale = Vector3.one * 2.1f;
            Ball("Gema", crownBody, new Vector3(0f, 0.3f, 1.08f), new Vector3(0.36f, 0.42f, 0.2f), m.gem);

            // Dientecitos en el labio de abajo (uno de oro).
            float[] tooth = { 0.55f, 0.9f, 0.4f, 1.1f, 0.6f, 0.35f, 0.85f };
            for (int i = 0; i < tooth.Length; i++)
            {
                float x = -2.1f + i * 0.7f;
                var pos = new Vector3(x, MouthY(new Vector3(x, 0, 3)) + tooth[i] * 0.35f, 3.4f - 0.08f * x * x);
                var t = Ball("Diente", rig.torso, pos - TorsoPivot, new Vector3(0.3f, tooth[i], 0.28f), i == 4 ? m.gold : m.tooth);
                t.localRotation = Quaternion.Euler(8f, 0f, (i * 37 % 23) - 11f);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 1.3f;
                var pos = new Vector3(x, MouthY(new Vector3(x, 0, 3)) - 0.45f, 3.3f - 0.08f * x * x);
                var f = Ball("Colmillo", rig.head, pos - HeadHinge, new Vector3(0.38f, 1.3f, 0.36f), m.tooth);
                f.localRotation = Quaternion.Euler(-8f, 0f, s * 8f);
            }

            // Saco bucal (se infla al croar).
            rig.sac = Ball("Saco bucal", rig.torso, new Vector3(0f, 4.95f, 3.05f) - TorsoPivot, new Vector3(2.6f, 1.4f, 1.4f), m.lid);
            rig.sac.GetComponent<Renderer>().sharedMaterial = m.skin;
            var sacMesh = Sdf(p => p.magnitude - 0.5f, new Bounds(Vector3.zero, Vector3.one * 1.2f), "Jefe_Saco", Vector3.zero, (p, n) => Cream);
            rig.sac.GetComponent<MeshFilter>().sharedMesh = sacMesh;

            // Brazos con codo: hombro → (brazo) → codo → (antebrazo + mano), manos apoyadas en las rodillas.
            for (int s = -1; s <= 1; s += 2)
            {
                int side = s; string n = s < 0 ? "I" : "D";
                var sh = Node("Hombro " + n, rig.torso, Shoulder(s), at);
                var upB = new Bounds((Shoulder(s) + Elbow(s)) * 0.5f, new Vector3(4.2f, 4.4f, 4.0f));
                Mesh("Brazo", sh, Sdf(p => UpperArm(p, side), upB, "Jefe_Brazo_" + n, Shoulder(s), (p, nn) => ArmColor(p, nn, side)), m.skin);
                var el = Node("Codo " + n, sh, Elbow(s), at);
                var foB = new Bounds((Elbow(s) + Wrist(s)) * 0.5f + new Vector3(0f, -0.2f, 0.7f), new Vector3(4.4f, 3.8f, 5.2f));
                Mesh("Antebrazo y mano", el, Sdf(p => Forearm(p, side), foB, "Jefe_Antebrazo_" + n, Elbow(s), (p, nn) => ArmColor(p, nn, side)), m.skin);
                if (s < 0) { rig.armL = sh; rig.elbowL = el; } else { rig.armR = sh; rig.elbowR = el; }
            }
            // Colisiones simples y fiables (esferas y cápsulas): el jugador no puede atravesar al Jefe.
            void Hit(Transform t, Vector3 aModel, Vector3 bModel, float r)
            {
                var go = new GameObject("Colisión"); go.transform.SetParent(t, false);
                Vector3 a = aModel - at[t], b = bModel - at[t];
                go.transform.localPosition = (a + b) * 0.5f;
                if ((b - a).sqrMagnitude > 1e-4f) go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
                var c = go.AddComponent<CapsuleCollider>(); c.radius = r; c.height = (b - a).magnitude + r * 2f;
            }
            Hit(rig.torso, new Vector3(0f, 1.6f, 0.2f), new Vector3(0f, 4.6f, 0.0f), 3.6f);        // barriga y cadera
            Hit(rig.torso, new Vector3(-3.2f, 5.2f, 0.1f), new Vector3(3.2f, 5.2f, 0.1f), 1.9f);   // hombros
            Hit(rig.head, new Vector3(-2.6f, 6.5f, 0.6f), new Vector3(2.6f, 6.5f, 0.6f), 2.2f);    // cabeza
            for (int s = -1; s <= 1; s += 2)
            {
                var sh = s < 0 ? rig.armL : rig.armR; var el = s < 0 ? rig.elbowL : rig.elbowR;
                Hit(sh, Shoulder(s), Elbow(s), 0.95f);
                Hit(el, Elbow(s), Wrist(s) + new Vector3(0f, 0f, 0.6f), 0.75f);
            }
            foreach (var e in rig.lids) { var sc = e.parent.gameObject.AddComponent<SphereCollider>(); sc.radius = 1.05f; }
            var body = root.gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; // colisiones que se mueven con la animación
            rig.head.localRotation = Quaternion.Euler(-rig.restOpen, 0f, 0f); // sonrisa entreabierta en reposo
            return rig;
        }
    }
}
