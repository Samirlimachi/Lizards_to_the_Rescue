using System.Collections;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Ragdoll gelatinoso del slime. Al morir se apaga el Animator y los huesos pasan a la física:
    /// Body es un cuerpo libre, Mid y Top cuelgan de él con articulaciones elásticas (rebotan y tiemblan),
    /// y la tapa sale despedida como un objeto aparte. Cuando el cuerpo se asienta, se derrite en un charco
    /// y, al final, desaparece (la tapa se queda un rato en el suelo).
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
        [Header("Charco final")]
        public float meltTime = 0.9f, puddleStay = 6f, lidStay = 9f;

        public bool Active { get; private set; }
        Rigidbody rbBody, rbMid, rbTop, rbLid;

        public void Activate(Vector3 from)
        {
            if (Active) return; Active = true;
            if (animator) animator.enabled = false;
            var main = GetComponent<Rigidbody>();
            Vector3 inherited = main ? main.linearVelocity : Vector3.zero;
            if (main) { main.isKinematic = true; }
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            if (skin) { skin.updateWhenOffscreen = true; DetachLidSkin(); }

            // Las faldas siguen al cuerpo (si no, el borde se quedaría estirado en el sitio original).
            if (skirts != null) foreach (var s in skirts) if (s) s.SetParent(body, true);

            rbBody = Part(body, bodyMass, 0.34f, new Vector3(0, 0.22f, 0));
            rbMid = Part(mid, midMass, 0.28f, new Vector3(0, 0.08f, 0));
            rbTop = Part(top, topMass, 0.24f, new Vector3(0, 0.04f, 0));
            Spring(rbMid, rbBody); Spring(rbTop, rbMid);
            if (lid)
            {
                rbLid = lid.gameObject.AddComponent<Rigidbody>(); rbLid.mass = lidMass; rbLid.interpolation = RigidbodyInterpolation.Interpolate;
                rbLid.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rbLid.angularDamping = 0.4f;
                var box = lid.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(0.82f, 0.09f, 0.82f); box.center = new Vector3(0, 0.14f, 0);
                var handle = lid.gameObject.AddComponent<BoxCollider>(); handle.size = new Vector3(0.36f, 0.12f, 0.08f); handle.center = new Vector3(0, 0.25f, 0);
            }
            var parts = new[] { rbBody, rbMid, rbTop, rbLid };
            foreach (var a in parts) foreach (var b in parts)
                if (a && b && a != b) foreach (var ca in a.GetComponents<Collider>()) foreach (var cb in b.GetComponents<Collider>()) Physics.IgnoreCollision(ca, cb);

            Vector3 dir = transform.position - from; dir.y = 0;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : -transform.forward;
            Vector3 push = dir * knockForce + Vector3.up * knockForce * 0.55f;
            foreach (var r in new[] { rbBody, rbMid, rbTop }) r.linearVelocity = inherited + push * Random.Range(0.85f, 1.1f);
            rbTop.AddTorque(Random.insideUnitSphere * 3f, ForceMode.VelocityChange);
            if (rbLid)
            {
                rbLid.linearVelocity = inherited + dir * 2.2f + Vector3.up * 5f + Vector3.Cross(Vector3.up, dir) * Random.Range(-1.5f, 1.5f);
                rbLid.AddTorque(Random.insideUnitSphere * 9f, ForceMode.VelocityChange);
            }
            Fx.Burst(body.position + Vector3.up * 0.4f, new Color(0.72f, 0.95f, 0.25f), 30, 5f, 0.13f);
            StartCoroutine(Sequence());
        }

        /// <summary>
        /// La franja del cuerpo justo bajo el borde de la tapa tiene un peso parcial a la tapa (para que el borde no haga
        /// picos al aplastarse). Si la tapa sale volando, esa franja se estiraría: se pasa su peso a la cabeza en una copia de la malla.
        /// </summary>
        void DetachLidSkin()
        {
            int li = System.Array.IndexOf(skin.bones, lid), ti = System.Array.IndexOf(skin.bones, top);
            if (li < 0 || ti < 0) return;
            var mesh = Instantiate(skin.sharedMesh); mesh.name = skin.sharedMesh.name + " (ragdoll)";
            var bw = mesh.boneWeights;
            for (int i = 0; i < bw.Length; i++)
            {
                var w = bw[i]; float lw = 0f;
                if (w.boneIndex0 == li) lw += w.weight0; if (w.boneIndex1 == li) lw += w.weight1;
                if (w.boneIndex2 == li) lw += w.weight2; if (w.boneIndex3 == li) lw += w.weight3;
                if (lw <= 0f || lw >= 0.99f) continue;
                if (w.boneIndex0 == li) w.boneIndex0 = ti; if (w.boneIndex1 == li) w.boneIndex1 = ti;
                if (w.boneIndex2 == li) w.boneIndex2 = ti; if (w.boneIndex3 == li) w.boneIndex3 = ti;
                bw[i] = w;
            }
            mesh.boneWeights = bw;
            // En la malla original la tapa está fundida con la cabeza: se quitan los triángulos que unen tapa y cuerpo,
            // para que la tapa salga limpia en vez de estirar una lámina de gelatina.
            var isLid = new bool[bw.Length];
            for (int i = 0; i < bw.Length; i++)
            {
                var w = bw[i]; float lw = 0f;
                if (w.boneIndex0 == li) lw += w.weight0; if (w.boneIndex1 == li) lw += w.weight1;
                if (w.boneIndex2 == li) lw += w.weight2; if (w.boneIndex3 == li) lw += w.weight3;
                isLid[i] = lw >= 0.5f;
            }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s); var keep = new System.Collections.Generic.List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    bool a = isLid[tris[t]], b = isLid[tris[t + 1]], c = isLid[tris[t + 2]];
                    if (a == b && b == c) { keep.Add(tris[t]); keep.Add(tris[t + 1]); keep.Add(tris[t + 2]); }
                }
                mesh.SetTriangles(keep, s);
            }
            skin.sharedMesh = mesh;
        }

        Rigidbody Part(Transform t, float mass, float radius, Vector3 center)
        {
            var rb = t.gameObject.AddComponent<Rigidbody>(); rb.mass = mass; rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; rb.linearDamping = 0.2f; rb.angularDamping = 0.8f;
            var c = t.gameObject.AddComponent<SphereCollider>(); c.radius = radius; c.center = center;
            return rb;
        }

        // Articulación de gelatina: permite estirarse un poco y doblarse, y vuelve a su forma con un muelle blando.
        void Spring(Rigidbody child, Rigidbody parent)
        {
            var j = child.gameObject.AddComponent<ConfigurableJoint>();
            j.connectedBody = parent; j.autoConfigureConnectedAnchor = true;
            j.xMotion = j.yMotion = j.zMotion = ConfigurableJointMotion.Limited;
            j.linearLimit = new SoftJointLimit { limit = 0.07f };
            j.linearLimitSpring = new SoftJointLimitSpring { spring = 600f, damper = 6f };
            j.angularXMotion = j.angularYMotion = j.angularZMotion = ConfigurableJointMotion.Limited;
            j.lowAngularXLimit = new SoftJointLimit { limit = -40f }; j.highAngularXLimit = new SoftJointLimit { limit = 40f };
            j.angularYLimit = new SoftJointLimit { limit = 30f }; j.angularZLimit = new SoftJointLimit { limit = 40f };
            j.angularXLimitSpring = j.angularYZLimitSpring = new SoftJointLimitSpring { spring = 120f, damper = 2f };
            j.rotationDriveMode = RotationDriveMode.Slerp;
            j.slerpDrive = new JointDrive { positionSpring = wobbleSpring, positionDamper = wobbleDamper, maximumForce = float.MaxValue };
            j.targetRotation = Quaternion.identity;
            j.enableCollision = false; j.projectionMode = JointProjectionMode.PositionAndRotation;
        }

        IEnumerator Sequence()
        {
            // 1) Vuelo y tambaleo físico hasta que el cuerpo se asienta.
            float t0 = Time.time;
            yield return new WaitForSeconds(0.6f);
            while (Time.time - t0 < 1.8f && rbBody.linearVelocity.magnitude > 0.35f) yield return null;

            // 2) Se derrite: los huesos vuelven a manos del código y se aplastan en un charco.
            foreach (var r in new[] { rbTop, rbMid, rbBody }) { var j = r.GetComponent<Joint>(); if (j) Destroy(j); r.isKinematic = true; foreach (var c in r.GetComponents<Collider>()) c.enabled = false; }
            Vector3 ground = body.position;
            if (Physics.Raycast(body.position + Vector3.up * 0.6f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != rbLid) ground = hit.point;
            Quaternion r0 = body.rotation, r1 = Quaternion.FromToRotation(body.up, Vector3.up) * body.rotation;
            Vector3 p0 = body.position, s0 = body.localScale, s1 = Vector3.Scale(s0, new Vector3(1.85f, 0.12f, 1.85f));
            Quaternion m0 = mid.localRotation, tp0 = top.localRotation;
            SlimeGelPuddle.Create(ground + Vector3.up * 0.02f, 0.85f, 0f, puddleStay + meltTime, false, 1f, meltTime);
            for (float t = 0; t < 1f; t += Time.deltaTime / meltTime)
            {
                float e = 1f - (1f - t) * (1f - t);
                float wob = Mathf.Sin(t * 22f) * (1f - t) * 0.08f;
                body.SetPositionAndRotation(Vector3.Lerp(p0, ground, e), Quaternion.Slerp(r0, r1, e));
                body.localScale = Vector3.Lerp(s0, s1, e) + new Vector3(wob, -wob, wob);
                mid.localRotation = Quaternion.Slerp(m0, Quaternion.identity, e); top.localRotation = Quaternion.Slerp(tp0, Quaternion.identity, e);
                yield return null;
            }
            body.localScale = s1;

            // 3) Charco quieto un rato y desaparece; la tapa se queda un poco más.
            yield return new WaitForSeconds(puddleStay);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.6f) { body.localScale = Vector3.Lerp(s1, new Vector3(s1.x * 0.6f, 0.001f, s1.z * 0.6f), t); yield return null; }
            if (skin) skin.enabled = false;
            yield return new WaitForSeconds(Mathf.Max(0f, lidStay - puddleStay));
            if (lid) { Vector3 ls = lid.localScale; for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f) { lid.localScale = Vector3.Lerp(ls, Vector3.zero, t); yield return null; } }
            Destroy(gameObject);
        }
    }
}
