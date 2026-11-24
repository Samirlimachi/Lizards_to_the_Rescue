using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Muerte gelatinosa del slime, en cuatro tiempos:
    ///  1. Golpe: se apaga el Animator y los huesos pasan a la física. Body es un cuerpo libre y Mid/Top cuelgan de él
    ///     con articulaciones elásticas. La gelatina rebota (estirar/aplastar conservando volumen) desde la pose en la
    ///     que murió, y vuelve a rebotar cada vez que cae al suelo. La tapa sale despedida, suena al chocar y se asienta.
    ///  2. Aturdido: se endereza tambaleándose y tirita con los ojos mareados; luego se hincha como un globo, cada vez
    ///     más rápido, con los mofletes inflados.
    ///  3. ¡Pop!: revienta en una nube de gel con un anillo de bolitas y gotas que salpican el suelo (<see cref="EnemyPoof"/>);
    ///     la tapa salta con la explosión.
    ///  4. La tapa se queda un momento y desaparece con su propio "pop".
    ///
    /// Pensado para muchos slimes a la vez: la malla recortada (sin las caras que unen tapa y cuerpo) se calcula una
    /// sola vez y la comparten todos; los trozos no chocan con los jugadores (la bola gigante no los desgarra).
    /// </summary>
    public class SlimeRagdoll : MonoBehaviour
    {
        [Header("Huesos")]
        public Animator animator;
        public Transform root, body, mid, top, lid;
        public Transform[] skirts;
        public SkinnedMeshRenderer skin;
        [Header("Física")]
        public float bodyMass = 3f, midMass = 1.2f, topMass = 0.8f, lidMass = 0.6f;
        public float knockForce = 4.5f, wobbleSpring = 80f, wobbleDamper = 1.2f;
        [Tooltip("Velocidad máxima heredada al morir (un golpe de gigante no debe lanzarlo como un proyectil).")]
        public float maxInheritedSpeed = 5f;
        [Header("Gelatina (estirar/aplastar)")]
        [Tooltip("Rigidez del rebote de gelatina (más = tiembla más rápido).")]
        public float jiggleStiffness = 240f;
        [Tooltip("Amortiguación del rebote (más = se calma antes).")]
        public float jiggleDamping = 7f;
        [Range(0.1f, 0.5f)] public float jiggleMax = 0.32f;
        [Header("Desvanecerse (aturdido → se hincha → ¡pop!)")]
        [Tooltip("Segundos que se queda aturdido tiritando antes de hincharse.")] public float shiverTime = 0.5f;
        [Tooltip("Segundos que tarda en hincharse como un globo antes de reventar.")] public float inflateTime = 0.28f;
        [Tooltip("Segundos que la tapa se queda en el suelo después del reventón.")] public float lidLinger = 2.5f;

        static readonly Color Gel = new Color(0.72f, 0.95f, 0.25f);
        static readonly Dictionary<Mesh, Mesh> detachedMeshCache = new();
        static readonly Dictionary<Mesh, Bounds> lidBoundsCache = new();

        public bool Active { get; private set; }
        Rigidbody rbBody, rbMid, rbTop, rbLid;
        Transform midParent, topParent;
        Vector3 bodyRestScale = Vector3.one, midRestScale = Vector3.one, topRestScale = Vector3.one;
        Vector3 midRestPos, topRestPos;
        SlimeFace face;

        // Rebote de gelatina: desplazamiento (estiramiento vertical relativo) y su velocidad.
        bool jiggling;
        float jig, jigVel, prevBodyVy, nextLandKick;

        void Awake()
        {
            // Pose de reposo de los huesos (antes de que la animación los aplaste o estire).
            if (body) bodyRestScale = body.localScale;
            if (mid) { midRestScale = mid.localScale; midRestPos = mid.localPosition; }
            if (top) { topRestScale = top.localScale; topRestPos = top.localPosition; }
            face = GetComponentInChildren<SlimeFace>(true);   // la cara vive en el hijo "Cara", no en la raíz
        }

        // Se prepara la malla de la muerte al cargar el nivel (una vez para todos): sin tirón al morir el primero.
        void Start() { if (skin && skin.sharedMesh) DetachedMesh(skin, lid, top); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { detachedMeshCache.Clear(); lidBoundsCache.Clear(); }

        public void Activate(Vector3 from)
        {
            if (Active) return; Active = true;
            if (animator) animator.enabled = false;
            var main = GetComponent<Rigidbody>();
            Vector3 inherited = main && !main.isKinematic ? Vector3.ClampMagnitude(main.linearVelocity, maxInheritedSpeed) : Vector3.zero;
            if (main) { main.isKinematic = true; }
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            if (skin)
            {
                skin.updateWhenOffscreen = true;
                var m = DetachedMesh(skin, lid, top); if (m) skin.sharedMesh = m;
            }

            // La gelatina arranca desde la deformación en la que murió (aplastado en un salto, estirado al escupir…)
            // y rebota hasta su forma: sin saltos bruscos de escala.
            float startJig = body && bodyRestScale.y > 0.0001f ? body.localScale.y / bodyRestScale.y - 1f : 0f;
            jig = Mathf.Clamp(startJig, -jiggleMax, jiggleMax);
            jigVel = 2.6f;   // el golpe lo estira hacia arriba
            // El esqueleto vuelve a la pose de reposo (faldas, cuerpo, tapa): si murió aplastado en un salto, el charco de
            // la animación no debe quedarse congelado. La deformación la pone el rebote de gelatina, que vuelve suave.
            var idle = RestClip(animator);
            if (idle) idle.SampleAnimation(animator.gameObject, 0f);
            mid.localPosition = midRestPos; top.localPosition = topRestPos;
            ApplyJiggle();

            // Las faldas siguen al cuerpo (si no, el borde se quedaría estirado en el sitio original).
            if (skirts != null) foreach (var s in skirts) if (s) s.SetParent(body, true);

            // Cuerpos rígidos anidados (Top dentro de Mid dentro de Body) se mueven dos veces y se estiran:
            // durante la física cada hueso cuelga directamente del Root y solo los une la articulación.
            midParent = mid.parent; topParent = top.parent;
            Transform flat = root ? root : transform;
            mid.SetParent(flat, true); top.SetParent(flat, true);

            rbBody = Part(body, bodyMass, 0.34f, new Vector3(0, 0.22f, 0));
            rbMid = Part(mid, midMass, 0.28f, new Vector3(0, 0.08f, 0));
            rbTop = Part(top, topMass, 0.24f, new Vector3(0, 0.04f, 0));
            Spring(rbMid, rbBody); Spring(rbTop, rbMid);
            if (lid)
            {
                rbLid = lid.gameObject.AddComponent<Rigidbody>(); rbLid.mass = lidMass; rbLid.interpolation = RigidbodyInterpolation.Interpolate;
                rbLid.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rbLid.angularDamping = 0.4f; rbLid.maxDepenetrationVelocity = 3f;
                if (skin && lidBoundsCache.TryGetValue(skin.sharedMesh, out var lb))
                {
                    // Colisión a la medida real de la tapa: el ala (disco) y el asa encima.
                    float wing = Mathf.Max(0.02f, lb.size.y * 0.45f);
                    var box = lid.gameObject.AddComponent<BoxCollider>();
                    box.size = new Vector3(lb.size.x * 0.96f, wing, lb.size.z * 0.96f); box.center = new Vector3(lb.center.x, lb.min.y + wing * 0.5f, lb.center.z);
                    var handle = lid.gameObject.AddComponent<BoxCollider>();
                    handle.size = new Vector3(lb.size.x * 0.4f, lb.size.y - wing, lb.size.z * 0.12f); handle.center = new Vector3(lb.center.x, lb.min.y + wing + (lb.size.y - wing) * 0.5f, lb.center.z);
                }
                else
                {
                    var box = lid.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(0.82f, 0.09f, 0.82f); box.center = new Vector3(0, 0.14f, 0);
                    var handle = lid.gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(0.36f, 0.12f, 0.08f); handle.center = new Vector3(0, 0.25f, 0);
                }
                lid.gameObject.AddComponent<SlimeLidClatter>();
                AddLidCap();
            }
            var parts = new[] { rbBody, rbMid, rbTop, rbLid };
            foreach (var a in parts) foreach (var b in parts)
                if (a && b && a != b) foreach (var ca in a.GetComponents<Collider>()) foreach (var cb in b.GetComponents<Collider>()) Physics.IgnoreCollision(ca, cb);
            // Los jugadores (sobre todo la bola gigante, 60 kg) no chocan con la gelatina: si no, la aplastan y la desgarran.
            foreach (var p in LizardPlayer.All)
                if (p) foreach (var pc in p.GetComponentsInChildren<Collider>(true))
                    foreach (var r in new[] { rbBody, rbMid, rbTop }) foreach (var rc in r.GetComponents<Collider>()) Physics.IgnoreCollision(pc, rc);

            Vector3 dir = transform.position - from; dir.y = 0;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : -transform.forward;
            Vector3 push = dir * knockForce + Vector3.up * knockForce * 0.55f;
            foreach (var r in new[] { rbBody, rbMid, rbTop }) r.linearVelocity = inherited + push * Random.Range(0.9f, 1.05f);
            rbTop.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
            if (rbLid)
            {
                rbLid.linearVelocity = inherited + dir * 2.2f + Vector3.up * 5f + Vector3.Cross(Vector3.up, dir) * Random.Range(-1.5f, 1.5f);
                rbLid.AddTorque(Random.insideUnitSphere * 9f, ForceMode.VelocityChange);
            }
            prevBodyVy = rbBody.linearVelocity.y;
            jiggling = true;

            // Reventón: salpicadura de gel en dos tonos y un "plop".
            Vector3 c0 = body.position + Vector3.up * 0.4f;
            Fx.Burst(c0, Gel, 26, 5f, 0.13f);
            Fx.Burst(c0, new Color(0.45f, 0.8f, 0.18f), 12, 3f, 0.09f);
            Sfx.Play(Sfx.Eject, c0, 0.7f);
            StartCoroutine(Sequence());
        }

        static AnimationClip RestClip(Animator a)
        {
            if (!a || !a.runtimeAnimatorController) return null;
            foreach (var c in a.runtimeAnimatorController.animationClips) if (c && c.name == "Idle") return c;
            return null;
        }

        /// <summary>
        /// Al separarse, la tapa queda hueca por debajo (ahí estaba unida al cuerpo). Un disco del mismo color la cierra,
        /// así se ve bien cuando cae boca arriba.
        /// </summary>
        static Material lidCapMat;
        void AddLidCap()
        {
            if (!skin || !lidBoundsCache.TryGetValue(skin.sharedMesh, out var b)) return;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cap.name = "Base de la tapa";
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(lid, false);
            // Disco plano en la cara inferior de la tapa, un poco más pequeño que el ala para que no asome.
            float r = Mathf.Min(b.extents.x, b.extents.z) * 0.92f;
            cap.transform.localPosition = new Vector3(b.center.x, b.min.y + b.size.y * 0.12f, b.center.z);
            cap.transform.localScale = new Vector3(r * 2f, Mathf.Max(0.004f, b.size.y * 0.06f), r * 2f);
            if (!lidCapMat) lidCapMat = LizardMaterials.Toon(new Color(0.58f, 0.5f, 0.66f), 0f, 0.35f);
            var rend = cap.GetComponent<Renderer>(); rend.sharedMaterial = lidCapMat; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // ------------------------------------------------------------------ flotar si muere en el agua
        // Sin esto el cuerpo y la tapa se hundían hasta el fondo (el agua es opaca) y el reventón pasaba sin verse.
        void FixedUpdate()
        {
            if (!Active) return;
            Buoy(rbBody, 0.35f); Buoy(rbMid, 0.25f); Buoy(rbTop, 0.15f); Buoy(rbLid, 0f);
        }
        static void Buoy(Rigidbody r, float sink)
        {
            if (!r || r.isKinematic) return;
            if (!SwimZone.Find(r.position, out float surface, out _)) return;
            float depth = surface - sink - r.position.y;
            if (depth <= 0f) return;
            // Empuje hacia arriba según lo hundido, con freno (como un corcho en gelatina).
            r.AddForce(Vector3.up * (-Physics.gravity.y * 1.2f + depth * 18f), ForceMode.Acceleration);
            Vector3 v = r.linearVelocity; v.y *= 0.9f; v.x *= 0.97f; v.z *= 0.97f; r.linearVelocity = v;
        }

        // ------------------------------------------------------------------ gelatina
        void LateUpdate()
        {
            if (!jiggling || !rbBody) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            // Al caer al suelo se aplasta un poco y vuelve a rebotar.
            float vy = rbBody.linearVelocity.y;
            if (prevBodyVy < -1.5f && vy > prevBodyVy + 1.5f && Time.time >= nextLandKick)
            {
                float k = Mathf.Clamp01(-prevBodyVy / 8f);
                jigVel -= 3.2f * k;
                nextLandKick = Time.time + 0.15f;
                if (k > 0.2f) { Fx.Burst(body.position + Vector3.up * 0.1f, Gel, 6 + Mathf.RoundToInt(8 * k), 2.5f, 0.08f); Sfx.Play(Sfx.Land, body.position, 0.35f * k); }
            }
            prevBodyVy = vy;
            jigVel += (-jiggleStiffness * jig - jiggleDamping * jigVel) * dt;
            jig = Mathf.Clamp(jig + jigVel * dt, -jiggleMax, jiggleMax);
            ApplyJiggle();
        }

        /// <summary>Estirar/aplastar conservando el volumen; la cabeza va un poco a destiempo (como gelatina de verdad).</summary>
        void ApplyJiggle()
        {
            Jiggle(body, bodyRestScale, jig);
            Jiggle(mid, midRestScale, jig * 0.8f);
            Jiggle(top, topRestScale, jig * 0.55f);
        }
        static void Jiggle(Transform t, Vector3 rest, float a)
        {
            if (!t) return;
            float y = 1f + a, xz = 1f / Mathf.Sqrt(Mathf.Max(0.2f, y));
            t.localScale = Vector3.Scale(rest, new Vector3(xz, y, xz));
        }

        // ------------------------------------------------------------------ malla sin unión tapa-cuerpo
        /// <summary>
        /// La franja del cuerpo justo bajo el borde de la tapa tiene un peso parcial a la tapa (para que el borde no haga
        /// picos al aplastarse). Si la tapa sale volando, esa franja se estiraría: se pasa su peso a la cabeza, y se quitan
        /// los triángulos que unen tapa y cuerpo para que la tapa salga limpia. Se calcula una vez por malla y se comparte.
        /// (El FBX necesita Read/Write: lo deja así Lizards → Enemigos → Preparar slime.)
        /// </summary>
        static Mesh DetachedMesh(SkinnedMeshRenderer skin, Transform lid, Transform top)
        {
            var src = skin.sharedMesh;
            if (!src) return null;
            if (detachedMeshCache.TryGetValue(src, out var cached)) return cached;
            if (src.name.EndsWith(" (ragdoll)")) return src;
            int li = System.Array.IndexOf(skin.bones, lid), ti = System.Array.IndexOf(skin.bones, top);
            if (li < 0 || ti < 0) return null;
            if (!src.isReadable)
            {
                Debug.LogWarning("SlimeRagdoll: la malla del slime no tiene Read/Write; la tapa estirará la malla al morir. Usa Lizards → Enemigos → Preparar slime.");
                detachedMeshCache[src] = null; return null;
            }
            var mesh = Instantiate(src); mesh.name = src.name + " (ragdoll)";
            var bw = mesh.boneWeights;
            var isLid = new bool[bw.Length];
            for (int i = 0; i < bw.Length; i++)
            {
                var w = bw[i]; float lw = LidWeight(w, li);
                if (lw > 0f && lw < 0.99f)
                {
                    if (w.boneIndex0 == li) w.boneIndex0 = ti; if (w.boneIndex1 == li) w.boneIndex1 = ti;
                    if (w.boneIndex2 == li) w.boneIndex2 = ti; if (w.boneIndex3 == li) w.boneIndex3 = ti;
                    bw[i] = w; lw = 0f;
                }
                isLid[i] = lw >= 0.5f;
            }
            mesh.boneWeights = bw;
            // Medidas de la tapa en el espacio de su hueso (para cerrarla por debajo con un disco del tamaño justo).
            var bp = mesh.bindposes[li]; var verts = mesh.vertices; bool any = false; Bounds lb = default;
            for (int i = 0; i < verts.Length; i++)
            {
                if (!isLid[i]) continue;
                Vector3 p = bp.MultiplyPoint3x4(verts[i]);
                if (!any) { lb = new Bounds(p, Vector3.zero); any = true; } else lb.Encapsulate(p);
            }
            if (any) lidBoundsCache[mesh] = lb;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s); var keep = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    bool a = isLid[tris[t]], b = isLid[tris[t + 1]], c = isLid[tris[t + 2]];
                    if (a == b && b == c) { keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
                }
                mesh.SetTriangles(keep, s);
            }
            mesh.UploadMeshData(false);
            detachedMeshCache[src] = mesh;
            return mesh;
        }
        static float LidWeight(BoneWeight w, int li)
        {
            float lw = 0f;
            if (w.boneIndex0 == li) lw += w.weight0; if (w.boneIndex1 == li) lw += w.weight1;
            if (w.boneIndex2 == li) lw += w.weight2; if (w.boneIndex3 == li) lw += w.weight3;
            return lw;
        }

        // ------------------------------------------------------------------ física
        Rigidbody Part(Transform t, float mass, float radius, Vector3 center)
        {
            var rb = t.gameObject.AddComponent<Rigidbody>(); rb.mass = mass; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rb.linearDamping = 0.2f; rb.angularDamping = 0.8f;
            rb.maxDepenetrationVelocity = 3f;   // si nace solapado con algo, sale suave en vez de saltar disparado
            var c = t.gameObject.AddComponent<SphereCollider>(); c.radius = radius; c.center = center;
            return rb;
        }

        // Articulación de gelatina: permite estirarse un poco y doblarse, y vuelve a su forma con un muelle blando.
        void Spring(Rigidbody child, Rigidbody parent)
        {
            var j = child.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = parent; j.autoConfigureConnectedAnchor = true;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Limited;
            j.linearLimit = new SoftJointLimit { limit = 0.05f };
            j.linearLimitSpring = new SoftJointLimitSpring { spring = 900f, damper = 10f };
            j.projectionDistance = 0.03f; j.projectionAngle = 12f;   // nunca se separa más de un poquito (no se "rompe")
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
            j.lowAngularXLimit = new SoftJointLimit { limit = -35f }; j.highAngularXLimit = new SoftJointLimit { limit = 35f };
            j.angularYLimit = new SoftJointLimit { limit = 25f }; j.angularZLimit = new SoftJointLimit { limit = 35f };
            j.angularXLimitSpring = j.angularYZLimitSpring = new SoftJointLimitSpring { spring = 120f, damper = 2f };
            j.rotationDriveMode = RotationDriveMode.Slerp;
            j.slerpDrive = new JointDrive { positionSpring = wobbleSpring, positionDamper = wobbleDamper, maximumForce = float.MaxValue };
            j.targetRotation = Quaternion.identity;
            j.enableCollision = false; j.projectionMode = JointProjectionMode.PositionAndRotation;
        }

        // ------------------------------------------------------------------ secuencia
        static float EaseOutBack(float t, float s = 1.6f) { t -= 1f; return 1f + (s + 1f) * t * t * t + s * t * t; }
        static float EaseInBack(float t, float s = 1.7f) => (s + 1f) * t * t * t - s * t * t;

        IEnumerator Sequence()
        {
            // 1) Vuelo y tambaleo físico hasta que el cuerpo se asienta.
            float t0 = Time.time;
            yield return new WaitForSeconds(0.75f);
            while (Time.time - t0 < 2f && rbBody.linearVelocity.magnitude > 0.35f) yield return null;
            if (rbLid) rbLid.angularDamping = 2.5f;   // la tapa deja de girar como una moneda y se asienta

            // 2) Los huesos vuelven a manos del código (ya casi en reposo: escala limpia antes de reunir la jerarquía).
            jiggling = false;
            jig = 0f; ApplyJiggle();
            foreach (var r in new[] { rbTop, rbMid, rbBody }) { var j = r.GetComponent<Joint>(); if (j) Destroy(j); r.isKinematic = true; foreach (var c in r.GetComponents<Collider>()) c.enabled = false; }
            if (midParent) mid.SetParent(midParent, true);
            if (topParent) top.SetParent(topParent, true);
            Quaternion r0 = body.rotation, r1 = Quaternion.FromToRotation(body.up, Vector3.up) * body.rotation;
            Vector3 p0 = body.position, s0 = body.localScale;
            Vector3 midS0 = mid.localScale, topS0 = top.localScale;
            Quaternion m0 = mid.localRotation, tp0 = top.localRotation;

            // 2a) Aturdido: se endereza tambaleándose y tirita (ojos mareados).
            if (face) face.Set(SlimeFace.Expr.Dizzy, 8f);
            float nextBubble = 0f;
            for (float t = 0; t < 1f; t += Time.deltaTime / shiverTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                float sway = Mathf.Sin(t * 14f) * (1f - t) * 8f;
                body.SetPositionAndRotation(p0 + Vector3.up * 0.04f * e, Quaternion.AngleAxis(sway, body.forward) * Quaternion.Slerp(r0, r1, e));
                float shiver = Mathf.Sin(Time.time * 55f) * 0.035f * e;
                body.localScale = Vector3.Scale(s0, new Vector3(1f + shiver, 1f - shiver, 1f + shiver));
                mid.localRotation = Quaternion.Slerp(m0, Quaternion.identity, e); top.localRotation = Quaternion.Slerp(tp0, Quaternion.identity, e);
                mid.localScale = Vector3.Lerp(midS0, midRestScale, e); top.localScale = Vector3.Lerp(topS0, topRestScale, e);
                if (Time.time >= nextBubble)
                {
                    nextBubble = Time.time + 0.12f;
                    Fx.Burst(body.position + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f, Color.Lerp(Gel, Color.white, 0.4f), 1, 0.8f, 0.05f);
                }
                yield return null;
            }

            // 2b) Se hincha como un globo, cada vez más rápido y temblando, con los mofletes inflados.
            if (face) face.Set(SlimeFace.Expr.Puffed, 16f);
            Sfx.Play(Sfx.Transform, body.position, 0.35f);
            Vector3 pB = body.position; Vector3 sB = body.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / inflateTime)
            {
                float e = t * t;
                float inflate = 1f + 0.42f * e;
                float shake = Mathf.Sin(Time.time * 75f) * 0.05f * e;
                body.position = pB + Vector3.up * 0.12f * e;
                body.localScale = Vector3.Scale(sB, new Vector3(inflate + shake, inflate * 1.05f - shake, inflate + shake));
                if (face) face.Visibility = 1f + 0.3f * e;   // los ojos se abren de golpe antes de reventar
                yield return null;
            }

            // 3) ¡Pop!: nube de gel, anillo de bolitas y gotas que salpican el suelo. La tapa salta con la explosión.
            Vector3 center = skin ? skin.bounds.center : body.position + Vector3.up * 0.5f;
            if (skin) skin.enabled = false;
            // Ojos y boca desaparecen con el reventón (nada se queda flotando en el aire).
            if (face) { face.Visibility = 0f; foreach (var r in face.GetComponentsInChildren<Renderer>(true)) if (r != skin) r.enabled = false; face.enabled = false; }
            EnemyPoof.Pop(center, Gel, 1.1f, true);
            if (rbLid && !rbLid.isKinematic && (lid.position - center).sqrMagnitude < 6.25f)
            {
                Vector3 kick = lid.position - center; kick.y = 0f;
                kick = (kick.sqrMagnitude > 0.01f ? kick.normalized : Random.onUnitSphere) * 1.5f + Vector3.up * 3.5f;
                rbLid.angularDamping = 0.6f;
                rbLid.AddForce(kick, ForceMode.VelocityChange);
                rbLid.AddTorque(Random.insideUnitSphere * 8f, ForceMode.VelocityChange);
            }

            // 4) La tapa se queda un momento y desaparece con su propio "pop".
            yield return new WaitForSeconds(lidLinger);
            if (lid)
            {
                if (rbLid) { rbLid.isKinematic = true; foreach (var c in lid.GetComponentsInChildren<Collider>()) c.enabled = false; }
                Vector3 ls = lid.localScale;
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.3f)
                {
                    float k = 1f - EaseInBack(t);   // crece un poquito y se encoge
                    lid.localScale = ls * Mathf.Max(0f, k);
                    yield return null;
                }
                lid.localScale = Vector3.zero;
                EnemyPoof.Pop(lid.position + Vector3.up * 0.15f, new Color(0.7f, 0.6f, 0.8f), 0.45f, false);
            }
            Destroy(gameObject);
        }
    }

    /// <summary>Sonido de la tapa del slime al chocar contra el suelo o las paredes (metal hueco).</summary>
    [AddComponentMenu("")]
    public sealed class SlimeLidClatter : MonoBehaviour
    {
        float next;
        void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v < 1.2f || Time.time < next) return;
            next = Time.time + 0.08f;
            float k = Mathf.Clamp01(v / 7f);
            Sfx.Play(Sfx.Stick, transform.position, 0.25f + 0.45f * k);
            if (k > 0.35f) Fx.Burst(c.GetContact(0).point, new Color(0.8f, 0.78f, 0.75f), 4, 1.5f, 0.05f);
        }
    }
}
