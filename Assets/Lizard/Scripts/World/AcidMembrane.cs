using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Membrana de gelatina ácida (la puerta de la despensa en LEVEL2). Sustituye al modelo plano por una lámina viva:
    ///  · Reposo: ondula sujeta por los bordes, con burbujas que suben, revientan arriba y gotas que caen por abajo.
    ///  · Golpe de ácido: onda desde el impacto, se hunde, chisporrotea y queda "tocada" (más amarilla, más fina,
    ///    con quemaduras, algo descolgada y burbujeando más deprisa).
    ///  · Rotura: tiembla, se hincha en el punto del golpe y revienta en trozos de gelatina con física real que rebotan,
    ///    salpican y se deshacen; del arco gotea gel y en el suelo queda un charco.
    /// La anima <see cref="AcidVulnerable"/> (que lleva la cuenta de golpes y la red).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class AcidMembrane : MonoBehaviour
    {
        const int Cols = 40, Rows = 20;
        static readonly Color Healthy = new Color(0.55f, 1f, 0.4f, 0.5f);
        static readonly Color Hurt = new Color(0.95f, 0.95f, 0.3f, 0.38f);

        BoxCollider box;
        Mesh mesh; Vector3[] baseVerts, verts; MeshRenderer sheetR;
        Material sheetMat, bubbleMat, burnMat;
        float width, height, damage, flash, shake, sag;
        Vector3 center;
        bool broken;

        sealed class Ripple { public Vector2 at; public float born, amp; }
        readonly List<Ripple> ripples = new();
        sealed class Bubble { public Transform t; public Vector2 p; public float speed, size, phase; }
        readonly List<Bubble> bubbles = new();
        readonly List<Transform> burns = new();
        float nextDrip;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            center = box.center; width = box.size.x; height = box.size.y;
            // El modelo plano original se oculta; su material (verde translúcido) es la base de la gelatina.
            Material src = null;
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) { if (!src) src = r.sharedMaterial; r.enabled = false; }
            sheetMat = src ? new Material(src) : LizardMaterials.Transparent(Healthy, 0.95f);
            sheetMat.name = "Membrana (viva)";
            if (sheetMat.HasProperty("_Cull")) sheetMat.SetFloat("_Cull", 0f);   // se ve por las dos caras
            sheetMat.SetColor("_BaseColor", Healthy);
            if (sheetMat.HasProperty("_Smoothness")) sheetMat.SetFloat("_Smoothness", 0.95f);
            bubbleMat = LizardMaterials.Transparent(new Color(0.85f, 1f, 0.75f, 0.55f), 1f);
            burnMat = LizardMaterials.Transparent(new Color(0.75f, 0.6f, 0.1f, 0.45f), 0.6f);
            BuildSheet();
            for (int i = 0; i < 22; i++) SpawnBubble(true);
        }

        // ------------------------------------------------------------------ lámina
        void BuildSheet()
        {
            var go = new GameObject("Lámina de gelatina");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh = new Mesh { name = "Membrana" };
            sheetR = go.AddComponent<MeshRenderer>(); sheetR.sharedMaterial = sheetMat;
            sheetR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mesh.MarkDynamic();
            baseVerts = new Vector3[(Cols + 1) * (Rows + 1)];
            var uv = new Vector2[baseVerts.Length];
            for (int y = 0; y <= Rows; y++)
                for (int x = 0; x <= Cols; x++)
                {
                    float u = x / (float)Cols, v = y / (float)Rows;
                    baseVerts[y * (Cols + 1) + x] = center + new Vector3((u - 0.5f) * width, (v - 0.5f) * height, 0f);
                    uv[y * (Cols + 1) + x] = new Vector2(u, v);
                }
            var tris = new int[Cols * Rows * 6]; int k = 0;
            for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    int a = y * (Cols + 1) + x, b = a + 1, c = a + Cols + 1, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b; tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            verts = (Vector3[])baseVerts.Clone();
            mesh.vertices = verts; mesh.uv = uv; mesh.triangles = tris; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            mesh.bounds = new Bounds(center, new Vector3(width, height, 2f));
        }

        /// <summary>Desplazamiento de la lámina (hacia dentro, eje Z local) en el punto (x, y) local.</summary>
        float Displacement(float x, float y, float t)
        {
            float u = (x - center.x) / width + 0.5f, v = (y - center.y) / height + 0.5f;
            float pin = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI) * Mathf.Sin(Mathf.Clamp01(v) * Mathf.PI);   // bordes sujetos
            float d = Mathf.Sin(x * 1.3f + t * 1.7f) * 0.035f + Mathf.Sin(y * 2.1f - t * 2.3f) * 0.025f + Mathf.Sin((x + y) * 0.8f + t * 1.1f) * 0.02f;
            d *= 1f + damage * 1.5f;
            foreach (var r in ripples)
            {
                float age = t - r.born; if (age < 0f) continue;
                float dist = Vector2.Distance(new Vector2(x, y), r.at);
                float front = age * 5.5f;
                if (dist > front + 0.6f) continue;
                float decay = Mathf.Exp(-age * 2.2f) * Mathf.Exp(-dist * 0.35f);
                d += Mathf.Sin(dist * 5f - age * 28f) * r.amp * decay * Mathf.Clamp01((front + 0.6f - dist) / 0.6f);
                d += r.amp * 2.2f * Mathf.Exp(-dist * dist * 2.5f) * Mathf.Exp(-age * 6f);   // abolladura del golpe
            }
            if (shake > 0f) d += Mathf.Sin(t * 70f + x * 9f) * Mathf.Sin(t * 53f + y * 7f) * shake;
            return d * pin;
        }

        void Update()
        {
            if (broken) return;
            float t = Time.time, dt = Time.deltaTime;
            ripples.RemoveAll(r => t - r.born > 2.5f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
            sag = Mathf.MoveTowards(sag, damage * 0.25f, dt * 0.4f);

            for (int i = 0; i < verts.Length; i++)
            {
                var b = baseVerts[i];
                float v = (b.y - center.y) / height + 0.5f;
                float sagY = -sag * Mathf.Sin(Mathf.Clamp01((b.x - center.x) / width + 0.5f) * Mathf.PI) * (1f - v);   // la parte baja se descuelga
                verts[i] = new Vector3(b.x, b.y + sagY, b.z + Displacement(b.x, b.y, t));
            }
            mesh.vertices = verts; mesh.RecalculateNormals();

            Color c = Color.Lerp(Healthy, Hurt, damage);
            c = Color.Lerp(c, new Color(1f, 1f, 0.85f, 0.75f), flash);
            sheetMat.SetColor("_BaseColor", c);

            UpdateBubbles(t, dt);
            // Gotas que caen del borde de abajo (más cuanto más dañada).
            if (t >= nextDrip)
            {
                nextDrip = t + Random.Range(0.9f, 2.2f) / (1f + damage * 2f);
                float x = center.x + Random.Range(-0.4f, 0.4f) * width;
                GooDrop.Spawn(transform.TransformPoint(new Vector3(x, center.y - height * 0.5f - sag * 0.5f, center.z)), Vector3.down * 0.5f, Random.Range(0.06f, 0.11f));
            }
        }

        // ------------------------------------------------------------------ burbujas
        void SpawnBubble(bool anywhere)
        {
            var go = new GameObject("Burbuja");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = GooSplat.SphereMesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = bubbleMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var b = new Bubble { t = go.transform, speed = Random.Range(0.12f, 0.35f), size = Random.Range(0.12f, 0.32f), phase = Random.value * 10f };
            ResetBubble(b, anywhere);
            bubbles.Add(b);
        }
        void ResetBubble(Bubble b, bool anywhere)
        {
            b.p = new Vector2(center.x + Random.Range(-0.45f, 0.45f) * width, center.y - height * 0.5f + (anywhere ? Random.value : Random.Range(0f, 0.1f)) * height);
        }
        void UpdateBubbles(float t, float dt)
        {
            float speedMul = 1f + damage * 1.8f;
            foreach (var b in bubbles)
            {
                b.p.y += b.speed * speedMul * dt;
                float x = b.p.x + Mathf.Sin(t * 1.5f + b.phase) * 0.08f;
                float top = center.y + height * 0.5f - 0.25f;
                if (b.p.y > top)
                {
                    Fx.Burst(transform.TransformPoint(new Vector3(x, top, center.z)), new Color(0.85f, 1f, 0.75f), 3, 1f, 0.04f);
                    ResetBubble(b, false);
                    continue;
                }
                float grow = Mathf.Clamp01((b.p.y - (center.y - height * 0.5f)) / 0.4f);
                float s = b.size * grow * (1f + Mathf.Sin(t * 6f + b.phase) * 0.06f);
                b.t.localPosition = new Vector3(x, b.p.y, center.z + Displacement(x, b.p.y, t));
                b.t.localScale = new Vector3(s, s, s * 0.55f);
            }
        }

        // ------------------------------------------------------------------ golpes
        /// <summary>Golpe de ácido en 'worldPoint'. damage01 = golpes / golpes necesarios. breaks = este golpe la rompe.</summary>
        public void Hit(Vector3 worldPoint, float damage01, bool breaks)
        {
            if (broken) return;
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            Vector2 at = new Vector2(Mathf.Clamp(local.x, center.x - width * 0.5f, center.x + width * 0.5f), Mathf.Clamp(local.y, center.y - height * 0.5f, center.y + height * 0.5f));
            if ((worldPoint - transform.position).sqrMagnitude < 0.01f) at = new Vector2(center.x + Random.Range(-0.3f, 0.3f) * width, center.y + Random.Range(-0.2f, 0.2f) * height);
            damage = Mathf.Clamp01(damage01);
            flash = 1f;
            ripples.Add(new Ripple { at = at, born = Time.time, amp = 0.22f });
            Vector3 hitWorld = transform.TransformPoint(new Vector3(at.x, at.y, center.z));
            AcidSizzle.Attach(transform, hitWorld);
            Fx.Burst(hitWorld, new Color(0.6f, 1f, 0.3f), 14, 3f, 0.08f);
            Sfx.Play(Sfx.Eject, hitWorld, 0.6f);
            // Quemadura: mancha amarillenta que se queda en la gelatina.
            var burn = new GameObject("Quemadura").transform; burn.SetParent(transform, false);
            burn.gameObject.AddComponent<MeshFilter>().sharedMesh = GooSplat.SphereMesh;
            var br = burn.gameObject.AddComponent<MeshRenderer>(); br.sharedMaterial = burnMat; br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            burn.localPosition = new Vector3(at.x, at.y, center.z); burn.localScale = new Vector3(0.9f, 0.75f, 0.06f);
            burns.Add(burn);
            if (breaks) StartCoroutine(Burst(at));
        }

        IEnumerator Burst(Vector2 at)
        {

            // 1) Tiembla y se hincha en el punto del golpe, cada vez más brillante.
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                shake = 0.04f + t * 0.35f; flash = 0.5f + t;
                if (Random.value < 0.4f) ripples.Add(new Ripple { at = at + Random.insideUnitCircle * 0.8f, born = Time.time, amp = 0.08f });
                if (Random.value < 0.5f) Fx.Burst(transform.TransformPoint(new Vector3(at.x, at.y, center.z) + (Vector3)(Random.insideUnitCircle * 1.2f)), new Color(0.6f, 1f, 0.3f), 2, 1.5f, 0.05f);
                yield return null;
            }
            broken = true;
            Sfx.Play(Sfx.Break, transform.TransformPoint(center), 0.8f);
            Sfx.Play(Sfx.Eject, transform.TransformPoint(center), 0.9f);
            Sfx.Play(Sfx.Dissolve, transform.TransformPoint(center), 0.6f);

            // 2) ¡Revienta!: la lámina se parte en trozos de gelatina con física.
            float t0 = Time.time;
            sheetR.enabled = false;
            foreach (var b in bubbles) { if (b.t) { Fx.Burst(b.t.position, new Color(0.85f, 1f, 0.75f), 3, 2f, 0.05f); Destroy(b.t.gameObject); } }
            bubbles.Clear();
            foreach (var bu in burns) if (bu) Destroy(bu.gameObject);
            int cx = 8, cy = 4;
            float cw = width / cx, ch = height / cy;
            Vector3 hitLocal = new Vector3(at.x, at.y, center.z);
            Vector3 forward = transform.forward;
            foreach (var p in LizardPlayer.All) if (p && p.IsControlled) { forward = -Vector3.ProjectOnPlane(p.transform.position - transform.position, transform.up).normalized; break; }
            for (int y = 0; y < cy; y++)
                for (int x = 0; x < cx; x++)
                {
                    float lx = center.x - width * 0.5f + (x + 0.5f + Random.Range(-0.15f, 0.15f)) * cw;
                    float ly = center.y - height * 0.5f + (y + 0.5f + Random.Range(-0.15f, 0.15f)) * ch;
                    Vector3 lp = new Vector3(lx, ly - sag * 0.5f, center.z + Displacement(lx, ly, t0));
                    Vector3 wp = transform.TransformPoint(lp);
                    Vector3 away = transform.TransformDirection(lp - hitLocal); away = away.sqrMagnitude > 0.01f ? away.normalized : Random.onUnitSphere;
                    float near = Mathf.Exp(-Vector2.Distance(new Vector2(lx, ly), at) * 0.5f);
                    Vector3 vel = away * Random.Range(1.5f, 3f) * (0.6f + near) + forward * Random.Range(0.5f, 2.5f) * near + Vector3.up * Random.Range(0.5f, 2f);
                    Vector3 size = new Vector3(cw * Random.Range(0.8f, 1.05f), ch * Random.Range(0.7f, 0.95f), Random.Range(0.12f, 0.2f));
                    GelChunk.Spawn(wp, transform.rotation, size, vel, sheetMat, Color.Lerp(Healthy, Hurt, damage));
                }
            Fx.Burst(transform.TransformPoint(hitLocal), new Color(0.6f, 1f, 0.3f), 30, 6f, 0.12f);
            Fx.Burst(transform.TransformPoint(center), new Color(0.85f, 1f, 0.7f), 24, 4f, 0.08f);
            EnemyPoof.Pop(transform.TransformPoint(hitLocal), new Color(0.6f, 1f, 0.35f), 1.4f, false);

            // 3) Charco a lo largo de la base y gotas que caen del arco.
            Vector3 floor = transform.TransformPoint(new Vector3(center.x, center.y - height * 0.5f, center.z));
            if (Physics.Raycast(floor + Vector3.up * 0.5f, Vector3.down, out var hit, 2f, ~0, QueryTriggerInteraction.Ignore)) floor = hit.point;
            for (int i = -1; i <= 1; i++)
                SlimeGelPuddle.Create(floor + transform.right * (i * width * 0.3f) + Vector3.up * 0.015f, i == 0 ? 1.6f : 1.1f, 0f, 7f, false, 1f, 0.5f);
            for (float t = 0; t < 1.6f; t += Time.deltaTime)
            {
                if (Random.value < 0.25f)
                {
                    float x = center.x + Random.Range(-0.45f, 0.45f) * width;
                    GooDrop.Spawn(transform.TransformPoint(new Vector3(x, center.y + height * 0.5f, center.z)), Vector3.down * 0.3f, Random.Range(0.07f, 0.13f));
                }
                yield return null;
            }
            yield return new WaitForSeconds(4f);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Trozo de gelatina con física: cae, rebota blandito (se aplasta y se estira al chocar), deja una salpicadura
    /// en el primer golpe fuerte y, pasado un rato, se encoge y desaparece.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class GelChunk : MonoBehaviour
    {
        static PhysicsMaterial gelPhys;
        Transform vis; Vector3 size; float squash, squashVel, born, life; bool splatted;
        Rigidbody rb;

        public static void Spawn(Vector3 pos, Quaternion rot, Vector3 size, Vector3 vel, Material mat, Color color)
        {
            var go = new GameObject("Trozo de gelatina");
            go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.25f; rb.linearDamping = 0.4f; rb.angularDamping = 1.2f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.maxDepenetrationVelocity = 2f;
            rb.linearVelocity = vel; rb.angularVelocity = Random.insideUnitSphere * 5f;
            var col = go.AddComponent<BoxCollider>(); col.size = new Vector3(size.x * 0.8f, size.y * 0.8f, Mathf.Max(0.08f, size.z));
            if (!gelPhys) gelPhys = new PhysicsMaterial("Gelatina") { bounciness = 0.25f, dynamicFriction = 0.9f, staticFriction = 0.9f, bounceCombine = PhysicsMaterialCombine.Maximum };
            col.material = gelPhys;
            // Los jugadores no chocan con los trozos (no los empujan ni tropiezan con ellos).
            foreach (var p in LizardPlayer.All) if (p) foreach (var pc in p.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(col, pc);
            var v = new GameObject("Gelatina").transform; v.SetParent(go.transform, false);
            v.gameObject.AddComponent<MeshFilter>().sharedMesh = GooSplat.SphereMesh;
            var r = v.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock(); mpb.SetColor("_BaseColor", color); r.SetPropertyBlock(mpb);
            var c = go.AddComponent<GelChunk>(); c.vis = v; c.size = size; c.rb = rb; c.born = Time.time; c.life = Random.Range(2.5f, 4f);
            v.localScale = size;
        }

        void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            if (speed < 1f) return;
            squashVel -= Mathf.Min(speed, 8f) * 1.4f;
            if (!splatted && speed > 2.5f)
            {
                splatted = true;
                var cp = c.GetContact(0);
                bool fixedSurface = !c.rigidbody || c.rigidbody.isKinematic;
                if (fixedSurface) GooSplat.Spawn(cp.point, cp.normal, Mathf.Max(size.x, size.y) * 0.55f, c.collider.transform, c.collider);
                Sfx.Play(Sfx.Land, cp.point, 0.25f);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime, age = Time.time - born;
            // Muelle de gelatina: se aplasta al chocar y vuelve con rebote; en el aire tiembla un poco.
            squashVel += (-180f * squash - 9f * squashVel) * dt;
            squash = Mathf.Clamp(squash + squashVel * dt, -0.5f, 0.4f);
            float wob = Mathf.Sin(age * 18f) * 0.05f * Mathf.Clamp01(1f - age);
            float y = 1f + squash + wob, xz = 1f / Mathf.Sqrt(Mathf.Max(0.3f, y));
            float fade = age > life ? Mathf.Clamp01(1f - (age - life) / 0.6f) : 1f;
            vis.localScale = new Vector3(size.x * xz, size.y * xz, size.z * y) * (fade * fade);
            if (age > life + 0.6f) Destroy(gameObject);
        }
    }
}
