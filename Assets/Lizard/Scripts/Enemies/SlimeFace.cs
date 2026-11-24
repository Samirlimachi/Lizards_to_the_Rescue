using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Cara animada del slime: ojos (esclerótica, iris, pupila con brillos, párpados de gelatina y cejas) y una boca
    /// que se genera cada fotograma (apertura, sonrisa, colmillos, lengua). Va pegada a la superficie del cuerpo:
    /// sigue a los huesos Body/Mid/Top con los mismos pesos que la piel, así que se aplasta y se estira con él.
    /// Las expresiones se mezclan suavemente; el slime elige la expresión según su estado.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class SlimeFace : MonoBehaviour
    {
        public enum Expr { Neutral, Happy, Surprised, Angry, Charging, Yell, Dizzy, Puffed, Spit, Hurt, Dead }

        [Header("Huesos")]
        public SkinnedMeshRenderer skin;
        public Transform body, mid, top;
        public Transform anchorEyeL, anchorEyeR, anchorMouth;
        [Header("Colores")]
        public Color jelly = new(0.70f, 0.86f, 0.18f);
        public Color jellyDark = new(0.36f, 0.52f, 0.08f);
        public Color iris = new(0.98f, 0.80f, 0.16f);
        public Color mouthInside = new(0.30f, 0.05f, 0.14f);
        public Color tongue = new(1f, 0.46f, 0.58f);

        // ----- parámetros de la expresión (actual → objetivo) -----
        struct P
        {
            public float lidUp, lidLow, brow, browY, eyeSize, pupil, open, smile, width, teeth, tongue;
            public static P Lerp(P a, P b, float t) => new P
            {
                lidUp = Mathf.Lerp(a.lidUp, b.lidUp, t), lidLow = Mathf.Lerp(a.lidLow, b.lidLow, t), brow = Mathf.Lerp(a.brow, b.brow, t),
                browY = Mathf.Lerp(a.browY, b.browY, t), eyeSize = Mathf.Lerp(a.eyeSize, b.eyeSize, t), pupil = Mathf.Lerp(a.pupil, b.pupil, t),
                open = Mathf.Lerp(a.open, b.open, t), smile = Mathf.Lerp(a.smile, b.smile, t), width = Mathf.Lerp(a.width, b.width, t),
                teeth = Mathf.Lerp(a.teeth, b.teeth, t), tongue = Mathf.Lerp(a.tongue, b.tongue, t)
            };
        }
        static P Preset(Expr e) => e switch
        {
            //                       párpado sup / inf, ceja(°), ceja alto, ojo, pupila, boca abierta, sonrisa, ancho, dientes, lengua
            Expr.Happy => new P { lidUp = 0.18f, lidLow = 0.32f, brow = -6, browY = 0.01f, eyeSize = 1f, pupil = 1.05f, open = 0.12f, smile = 0.85f, width = 0.9f, teeth = 0, tongue = 0.2f },
            Expr.Surprised => new P { lidUp = 0f, lidLow = 0f, brow = -14, browY = 0.035f, eyeSize = 1.22f, pupil = 0.62f, open = 0.65f, smile = 0f, width = 0.48f, teeth = 0, tongue = 0 },
            Expr.Angry => new P { lidUp = 0.36f, lidLow = 0.12f, brow = 26, browY = -0.012f, eyeSize = 1f, pupil = 0.85f, open = 0.22f, smile = 0.35f, width = 1f, teeth = 1, tongue = 0 },
            Expr.Charging => new P { lidUp = 0.5f, lidLow = 0.3f, brow = 34, browY = -0.02f, eyeSize = 0.95f, pupil = 0.75f, open = 0.14f, smile = 0.5f, width = 1.18f, teeth = 1, tongue = 0 },
            Expr.Yell => new P { lidUp = 0.05f, lidLow = 0f, brow = 22, browY = 0.01f, eyeSize = 1.15f, pupil = 0.7f, open = 1f, smile = 0.1f, width = 0.85f, teeth = 1, tongue = 0.5f },
            Expr.Dizzy => new P { lidUp = 0.3f, lidLow = 0.05f, brow = -16, browY = 0.02f, eyeSize = 1.05f, pupil = 0.8f, open = 0.38f, smile = -0.35f, width = 0.75f, teeth = 0, tongue = 1f },
            Expr.Puffed => new P { lidUp = 0.52f, lidLow = 0.45f, brow = 10, browY = 0f, eyeSize = 0.95f, pupil = 0.9f, open = 0f, smile = 0.15f, width = 0.32f, teeth = 0, tongue = 0 },
            Expr.Spit => new P { lidUp = 0.1f, lidLow = 0f, brow = 16, browY = 0.015f, eyeSize = 1.12f, pupil = 0.75f, open = 1f, smile = 0f, width = 0.62f, teeth = 0, tongue = 0.3f },
            Expr.Hurt => new P { lidUp = 0.94f, lidLow = 0.55f, brow = -20, browY = 0.015f, eyeSize = 0.9f, pupil = 1f, open = 0.45f, smile = -0.6f, width = 0.8f, teeth = 1, tongue = 0 },
            Expr.Dead => new P { lidUp = 1f, lidLow = 0.2f, brow = -18, browY = -0.005f, eyeSize = 0.9f, pupil = 1f, open = 0.3f, smile = -0.5f, width = 0.7f, teeth = 0, tongue = 0.9f },
            _ => new P { lidUp = 0.22f, lidLow = 0.06f, brow = 6, browY = 0f, eyeSize = 1f, pupil = 1f, open = 0.05f, smile = 0.3f, width = 0.8f, teeth = 0, tongue = 0 },
        };

        public Expr Current { get; private set; } = Expr.Neutral;
        P cur, goal; float blendSpeed = 10f;
        Vector3? lookTarget; Vector2 look, lookGoal; float nextSaccade, nextBlink, blinkT = -1f, shake, talkPhase;

        // ----- piezas -----
        class Eye { public Transform root, sclera, irisT, pupil, glint, glint2, lidUp, lidLow, brow; public float side; }
        Eye eyeL, eyeR;
        Transform mouthRoot; Mesh mouthMesh;
        Material matWhite, matBlack, matIris, matJelly, matBrow, matVertex;
        static Mesh sphere, upperShell, lowerShell;

        // anclajes en reposo, en el espacio local de cada hueso
        struct Anchor { public Transform t; public Vector3[] pos, nrm, upv; public float[] w; }
        Anchor aL, aR, aM;
        Transform[] bones;

        void Awake() { Build(); }

        // ------------------------------------------------------------------ API
        public void Set(Expr e, float speed = 10f) { Current = e; goal = Preset(e); blendSpeed = speed; }
        /// <summary>0–1: tamaño de ojos y boca (al derretirse el slime se hunden en el charco).</summary>
        public float Visibility { get; set; } = 1f;
        public void LookAt(Vector3? worldPoint) { lookTarget = worldPoint; }
        public void Blink() { if (blinkT < 0f) blinkT = 0f; }
        /// <summary>Temblor de la cara (cargando un ataque).</summary>
        public void Shake(float amount) { shake = Mathf.Max(shake, amount); }
        /// <summary>Movimiento rápido de la boca (balbucear / bufar).</summary>
        public void Chatter(float seconds) { talkPhase = seconds; }

        // ------------------------------------------------------------------ construcción
        void Build()
        {
            if (!sphere) { var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = g.GetComponent<MeshFilter>().sharedMesh; Destroy(g); }
            if (!upperShell) upperShell = Shell(true);
            if (!lowerShell) lowerShell = Shell(false);
            matWhite = Toon(new Color(0.98f, 0.98f, 0.95f), 0.5f);
            matBlack = Toon(new Color(0.06f, 0.05f, 0.08f), 0.6f);
            matIris = Toon(iris, 0.4f);
            matJelly = Toon(jelly, 0.3f);
            matBrow = Toon(jellyDark, 0.1f);
            matVertex = Toon(Color.white, 0.15f); matVertex.SetFloat("_VertexColorWeight", 1f);

            bones = new[] { body, mid, top };
            aL = MakeAnchor(anchorEyeL, new[] { 0f, 0.45f, 0.55f });
            aR = MakeAnchor(anchorEyeR, new[] { 0f, 0.45f, 0.55f });
            aM = MakeAnchor(anchorMouth, new[] { 0.2f, 0.8f, 0f });
            eyeL = MakeEye("Ojo izquierdo", 1f);
            eyeR = MakeEye("Ojo derecho", -1f);
            mouthRoot = new GameObject("Boca").transform; mouthRoot.SetParent(transform, false);
            mouthMesh = new Mesh { name = "Boca del slime" }; mouthMesh.MarkDynamic();
            mouthRoot.gameObject.AddComponent<MeshFilter>().sharedMesh = mouthMesh;
            var mr = mouthRoot.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = matVertex; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Set(Expr.Neutral); cur = goal;
            nextBlink = Time.time + Random.Range(1f, 3f);
        }

        static Material Toon(Color c, float spec)
        {
            var m = LizardMaterials.Toon(c, 0f, spec);
            if (m.HasProperty("_OutlineWidth")) m.SetFloat("_OutlineWidth", 0f);
            return m;
        }

        Anchor MakeAnchor(Transform t, float[] w)
        {
            var a = new Anchor { t = t, w = w, pos = new Vector3[3], nrm = new Vector3[3], upv = new Vector3[3] };
            if (!t) return a;
            var bp = skin.sharedMesh.bindposes; var sb = skin.bones;
            Vector3 pMesh = skin.transform.InverseTransformPoint(t.position);
            Vector3 nMesh = skin.transform.InverseTransformDirection(t.up);
            Vector3 uMesh = skin.transform.InverseTransformDirection(transform.up);
            for (int i = 0; i < 3; i++)
            {
                int bi = System.Array.IndexOf(sb, bones[i]); if (bi < 0) { a.w[i] = 0; continue; }
                a.pos[i] = bp[bi].MultiplyPoint3x4(pMesh); a.nrm[i] = bp[bi].MultiplyVector(nMesh); a.upv[i] = bp[bi].MultiplyVector(uMesh);
            }
            return a;
        }

        Eye MakeEye(string name, float side)
        {
            var e = new Eye { side = side };
            e.root = new GameObject(name).transform; e.root.SetParent(transform, false);
            e.sclera = Part("Esclerótica", e.root, sphere, matWhite);
            e.irisT = Part("Iris", e.sclera, sphere, matIris);
            e.pupil = Part("Pupila", e.irisT, sphere, matBlack);
            e.glint = Part("Brillo", e.pupil, sphere, matWhite);
            e.glint2 = Part("Brillo pequeño", e.pupil, sphere, matWhite);
            e.lidUp = Part("Párpado superior", e.root, upperShell, matJelly);
            e.lidLow = Part("Párpado inferior", e.root, lowerShell, matJelly);
            e.brow = Part("Ceja", e.root, sphere, matBrow);
            return e;
        }
        static Transform Part(string n, Transform parent, Mesh m, Material mat)
        {
            var g = new GameObject(n); g.transform.SetParent(parent, false);
            g.AddComponent<MeshFilter>().sharedMesh = m;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g.transform;
        }
        /// <summary>Media esfera (párpado). upper: la mitad con y ≥ 0.</summary>
        static Mesh Shell(bool upper)
        {
            const int seg = 20, rings = 10;
            var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float a = (upper ? 1 : -1) * Mathf.PI * 0.5f * r / rings; // desde el ecuador hacia el polo
                for (int s = 0; s <= seg; s++)
                {
                    float b = Mathf.PI * 2 * s / seg;
                    v.Add(new Vector3(Mathf.Cos(a) * Mathf.Sin(b), Mathf.Sin(a), Mathf.Cos(a) * Mathf.Cos(b)) * 0.5f);
                }
            }
            for (int r = 0; r < rings; r++) for (int s = 0; s < seg; s++)
            {
                int i0 = r * (seg + 1) + s, i1 = i0 + 1, i2 = i0 + seg + 1, i3 = i2 + 1;
                if (upper) t.AddRange(new[] { i0, i2, i1, i1, i2, i3 }); else t.AddRange(new[] { i0, i1, i2, i1, i3, i2 });
            }
            // Doble cara: el párpado se ve desde fuera y también por dentro cuando gira.
            int n0 = v.Count; var nr = new System.Collections.Generic.List<Vector3>();
            foreach (var p in v) nr.Add(p.normalized);
            for (int i = 0; i < n0; i++) { v.Add(v[i]); nr.Add(-nr[i]); }
            int tc = t.Count;
            for (int i = 0; i < tc; i += 3) { t.Add(t[i] + n0); t.Add(t[i + 2] + n0); t.Add(t[i + 1] + n0); }
            var m = new Mesh { name = upper ? "Párpado sup" : "Párpado inf" }; m.SetVertices(v); m.SetNormals(nr); m.SetTriangles(t, 0); m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------ animación
        void LateUpdate()
        {
            float dt = Time.deltaTime;
            cur = P.Lerp(cur, goal, 1f - Mathf.Exp(-blendSpeed * dt));
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);

            // Parpadeo automático (no cuando ya tiene los ojos cerrados).
            if (Time.time >= nextBlink && Current != Expr.Dead && Current != Expr.Hurt) { Blink(); nextBlink = Time.time + Random.Range(1.8f, 4.5f) * (Random.value < 0.18f ? 0.25f : 1f); }
            float blink = 0f;
            if (blinkT >= 0f) { blinkT += dt / 0.16f; blink = Mathf.Sin(Mathf.Clamp01(blinkT) * Mathf.PI); if (blinkT >= 1f) blinkT = -1f; }

            // Mirada: al objetivo, o vistazos al azar.
            if (Current == Expr.Dizzy) { float a = Time.time * 9f; lookGoal = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.9f; }
            else if (lookTarget.HasValue && aL.t)
            {
                Vector3 d = Place(aL, out var rot, out _) ; d = Quaternion.Inverse(rot) * (lookTarget.Value - d);
                lookGoal = new Vector2(Mathf.Clamp(d.x / Mathf.Max(0.3f, d.z) * 1.4f, -1f, 1f), Mathf.Clamp(d.y / Mathf.Max(0.3f, d.z) * 1.4f, -1f, 1f));
            }
            else if (Time.time >= nextSaccade) { lookGoal = Random.insideUnitCircle * 0.8f; nextSaccade = Time.time + Random.Range(0.6f, 2.2f); }
            look = Vector2.Lerp(look, lookGoal, 1f - Mathf.Exp(-18f * dt));

            UpdateEye(eyeL, aL, blink); UpdateEye(eyeR, aR, blink);
            UpdateMouth(dt);
        }

        /// <summary>Posición y orientación del anclaje según los huesos (como la piel).</summary>
        Vector3 Place(Anchor a, out Quaternion rot, out Vector3 squash)
        {
            Vector3 p = Vector3.zero, n = Vector3.zero, u = Vector3.zero; float ws = 0f;
            for (int i = 0; i < 3; i++)
            {
                if (a.w[i] <= 0f || !bones[i]) continue;
                p += bones[i].TransformPoint(a.pos[i]) * a.w[i]; n += bones[i].TransformDirection(a.nrm[i]) * a.w[i]; u += bones[i].TransformDirection(a.upv[i]) * a.w[i]; ws += a.w[i];
            }
            if (ws <= 0f) { rot = a.t ? a.t.rotation : transform.rotation; squash = Vector3.one; return a.t ? a.t.position : transform.position; }
            p /= ws;
            rot = Quaternion.LookRotation(n.normalized, u.normalized);
            var bs = body ? body.localScale : Vector3.one;           // aplastar/estirar del cuerpo (Y del hueso = arriba)
            squash = new Vector3(bs.x, bs.y, 1f);
            return p;
        }

        void UpdateEye(Eye e, Anchor a, float blink)
        {
            if (!a.t) return;
            Vector3 jitter = shake > 0f ? Random.insideUnitSphere * shake * 0.006f : Vector3.zero;
            e.root.SetPositionAndRotation(Place(a, out var rot, out var sq) + jitter, rot);
            e.root.localScale = sq * Visibility;
            float es = cur.eyeSize;
            e.sclera.localPosition = new Vector3(0, 0, 0.004f); e.sclera.localScale = new Vector3(0.15f * es, 0.175f * es, 0.07f);
            // Iris y pupila (dentro de la esclerótica, coordenadas relativas a ella).
            e.irisT.localPosition = new Vector3(look.x * 0.2f, look.y * 0.16f, 0.4f); e.irisT.localScale = new Vector3(0.66f, 0.66f, 0.3f);
            float pu = cur.pupil;
            e.pupil.localPosition = new Vector3(0, 0, 0.35f); e.pupil.localScale = new Vector3(0.62f * pu, 0.66f * pu, 0.6f);
            e.glint.localPosition = new Vector3(-0.22f, 0.25f, 0.42f); e.glint.localScale = Vector3.one * 0.34f;
            e.glint2.localPosition = new Vector3(0.2f, -0.2f, 0.42f); e.glint2.localScale = Vector3.one * 0.16f;
            // Párpados: medias esferas de gelatina que giran sobre el ojo (abren hacia atrás, cierran hacia delante).
            float up = Mathf.Clamp01(Mathf.Max(cur.lidUp, blink)), low = Mathf.Clamp01(cur.lidLow);
            Vector3 lidScale = new Vector3(0.168f * es, 0.192f * es, 0.078f);
            e.lidUp.localPosition = new Vector3(0, 0, -0.002f); e.lidUp.localScale = lidScale;
            e.lidUp.localRotation = Quaternion.Euler(Mathf.Lerp(-62f, 92f, EaseLid(up)), 0, -e.side * cur.brow * 0.35f);
            e.lidLow.localPosition = new Vector3(0, 0, -0.002f); e.lidLow.localScale = lidScale;
            e.lidLow.localRotation = Quaternion.Euler(Mathf.Lerp(66f, -40f, low), 0, 0);
            // Ceja: más baja y girada hacia dentro = enfado; alta y hacia fuera = sorpresa o pena.
            e.brow.localPosition = new Vector3(0.006f * e.side, 0.118f * es + cur.browY, 0.028f);
            e.brow.localRotation = Quaternion.Euler(0, 0, -e.side * cur.brow);
            e.brow.localScale = new Vector3(0.14f, 0.034f, 0.04f);
        }
        static float EaseLid(float x) => x * x * (3f - 2f * x);

        void UpdateMouth(float dt)
        {
            if (!aM.t) return;
            mouthRoot.SetPositionAndRotation(Place(aM, out var rot, out var sq), rot);
            mouthRoot.localScale = sq * Visibility;
            float open = cur.open, smile = cur.smile, width = cur.width;
            if (talkPhase > 0f) { talkPhase -= dt; open += Mathf.Abs(Mathf.Sin(Time.time * 26f)) * 0.35f; }
            if (Current == Expr.Dizzy) smile += Mathf.Sin(Time.time * 7f) * 0.25f;
            if (shake > 0f) width += Random.Range(-0.04f, 0.04f) * shake;
            BuildMouth(Mathf.Clamp01(open), Mathf.Clamp(smile, -1f, 1f), width, cur.teeth, cur.tongue);
        }

        // Boca: interior oscuro entre el labio de arriba y el de abajo, borde de gelatina, colmillos y lengua.
        readonly System.Collections.Generic.List<Vector3> mv = new();
        readonly System.Collections.Generic.List<Color> mc = new();
        readonly System.Collections.Generic.List<int> mt = new();
        void BuildMouth(float open, float smile, float width, float teeth, float tongueAmt)
        {
            mv.Clear(); mc.Clear(); mt.Clear();
            const int N = 18;
            float hw = 0.12f * width, depth = 0.01f + open * 0.11f, corner = smile * 0.035f;
            Vector3 Pt(float x, float y, float z) => new Vector3(x, y, z - (x * x) * 1.6f);   // sigue la curva del cuerpo
            float Upper(float t) => corner * t * t + (1 - t * t) * (open * 0.032f * (1f - Mathf.Clamp01(smile)) + Mathf.Max(0, -smile) * 0.01f);
            float Lower(float t) => corner * t * t - (1 - t * t) * depth - (1 - t * t) * Mathf.Max(0, smile) * 0.012f;
            // Borde (un poco mayor y detrás) para que la boca se lea bien.
            Strip(N, t => Pt(t * (hw + 0.008f), Upper(t) + 0.006f, 0.004f), t => Pt(t * (hw + 0.008f), Lower(t) - 0.007f, 0.004f), Lin(jellyDark));
            Strip(N, t => Pt(t * hw, Upper(t), 0.01f), t => Pt(t * hw, Lower(t), 0.01f), Lin(mouthInside));
            // Lengua: asoma desde abajo cuando la boca está abierta.
            if (tongueAmt > 0.01f && open > 0.15f)
            {
                float tw = hw * 0.55f, th = depth * 0.55f * tongueAmt + 0.01f, ty = Lower(0) + th * 0.45f;
                Ellipse(Pt(0, ty, 0.014f), tw, th, Lin(tongue), 14);
            }
            // Colmillos: dos dientes blancos que cuelgan del labio de arriba.
            if (teeth > 0.05f)
            {
                foreach (float s in new[] { -0.45f, 0.45f })
                {
                    float x = s * hw, y = Upper(s); float len = (0.012f + open * 0.012f) * teeth, w = 0.009f * teeth + 0.003f;
                    int i = mv.Count; var c = Lin(new Color(1f, 1f, 0.95f));
                    mv.Add(Pt(x - w, y + 0.001f, 0.016f)); mv.Add(Pt(x + w, y + 0.001f, 0.016f)); mv.Add(Pt(x, y - len, 0.016f));
                    mc.Add(c); mc.Add(c); mc.Add(c); mt.AddRange(new[] { i, i + 2, i + 1 });
                }
            }
            mouthMesh.Clear(); mouthMesh.SetVertices(mv); mouthMesh.SetColors(mc); mouthMesh.SetTriangles(mt, 0); mouthMesh.RecalculateNormals(); mouthMesh.RecalculateBounds();
        }
        void Strip(int n, System.Func<float, Vector3> top, System.Func<float, Vector3> bottom, Color c)
        {
            int s = mv.Count;
            for (int i = 0; i <= n; i++) { float t = -1f + 2f * i / n; mv.Add(top(t)); mv.Add(bottom(t)); mc.Add(c); mc.Add(c); }
            for (int i = 0; i < n; i++) { int a = s + i * 2; mt.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
        }
        void Ellipse(Vector3 c, float rx, float ry, Color col, int n)
        {
            int s = mv.Count; mv.Add(c); mc.Add(col);
            for (int i = 0; i <= n; i++) { float a = Mathf.PI * 2 * i / n; mv.Add(c + new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0)); mc.Add(col); }
            for (int i = 0; i < n; i++) mt.AddRange(new[] { s, s + 1 + i, s + 2 + i });
        }
        static Color Lin(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }
}
