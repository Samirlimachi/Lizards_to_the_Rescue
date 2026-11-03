using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Vida ambiental del refugio: luciérnagas que flotan y brillan, peces que saltan del agua y nubes que pasan.
    /// Todo se crea al empezar (no ensucia la escena) y es puramente decorativo.
    /// </summary>
    public class LobbyAmbience : MonoBehaviour
    {
        [Header("Zona")]
        public Vector3 center = new Vector3(0f, 0f, 5f);
        public Vector2 size = new Vector2(70f, 70f);
        public float waterY = -0.65f;
        [Tooltip("Rectángulo (x, z, ancho, largo) donde pueden saltar peces.")]
        public Rect waterArea = new Rect(-30f, 20f, 60f, 30f);

        [Header("Cantidades")]
        public int fireflies = 45;
        public int clouds = 9;
        public Vector2 fishInterval = new Vector2(1.2f, 3.5f);

        struct Firefly { public Transform t; public Vector3 home; public float phase, speed; }
        struct Fish { public Transform t; public Vector3 start, end; public float age, dur, height; }
        struct Cloud { public Transform t; public float speed; }

        readonly List<Firefly> flies = new();
        readonly List<Fish> fish = new();
        readonly List<Cloud> cloudList = new();
        Material glow, fishMat, finMat, cloudMat;
        Mesh sphere;
        float nextFish;

        void Start()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            glow = new Material(LizardMaterials.Particle()); glow.SetColor("_BaseColor", new Color(1f, 0.95f, 0.45f));
            fishMat = LizardMaterials.Toon(new Color(1f, 0.55f, 0.2f), 0.003f, 0.5f);
            finMat = LizardMaterials.Toon(new Color(1f, 0.85f, 0.35f), 0.003f, 0.3f);
            cloudMat = LizardMaterials.Toon(new Color(1f, 1f, 1f), 0.0025f, 0f, new Color(0.82f, 0.88f, 1f));

            for (int i = 0; i < fireflies; i++)
            {
                Vector3 p = center + new Vector3(Random.Range(-size.x, size.x) * 0.5f, 0f, Random.Range(-size.y, size.y) * 0.5f);
                p.y = GroundY(p) + Random.Range(0.6f, 2.6f);
                var t = Blob("Luciérnaga", p, Vector3.one * 0.09f, glow);
                flies.Add(new Firefly { t = t, home = p, phase = Random.value * 10f, speed = Random.Range(0.4f, 0.9f) });
            }
            for (int i = 0; i < clouds; i++)
            {
                var root = new GameObject("Nube").transform;
                root.SetParent(transform, false);
                root.position = center + new Vector3(Random.Range(-90f, 90f), Random.Range(26f, 38f), Random.Range(-20f, 90f));
                int puffs = Random.Range(4, 7);
                for (int k = 0; k < puffs; k++)
                {
                    float s = Random.Range(3.5f, 6.5f);
                    var b = Blob("Bola de nube", root.position + new Vector3(k * 2.6f - puffs * 1.3f, Random.Range(-0.6f, 1.2f), Random.Range(-1.5f, 1.5f)), new Vector3(s, s * 0.7f, s), cloudMat);
                    b.SetParent(root, true);
                }
                cloudList.Add(new Cloud { t = root, speed = Random.Range(0.6f, 1.4f) });
            }
            nextFish = Time.time + 1f;
        }

        float GroundY(Vector3 p)
        {
            return Physics.Raycast(new Vector3(p.x, 60f, p.z), Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0f;
        }

        Transform Blob(string name, Vector3 pos, Vector3 scale, Material m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos; go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = sphere;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void SpawnFish()
        {
            Vector3 a = new Vector3(Random.Range(waterArea.xMin, waterArea.xMax), waterY, Random.Range(waterArea.yMin, waterArea.yMax));
            Vector2 dir = Random.insideUnitCircle.normalized * Random.Range(1.6f, 3.2f);
            Vector3 b = a + new Vector3(dir.x, 0f, dir.y);
            var root = new GameObject("Pez").transform;
            root.SetParent(transform, false);
            root.position = a;
            var body = Blob("Cuerpo", a, new Vector3(0.22f, 0.26f, 0.5f), fishMat); body.SetParent(root, true);
            var tail = Blob("Cola", a + new Vector3(0f, 0f, -0.3f), new Vector3(0.05f, 0.24f, 0.16f), finMat); tail.SetParent(root, true);
            tail.localPosition = new Vector3(0f, 0f, -0.3f);
            body.localPosition = Vector3.zero;
            fish.Add(new Fish { t = root, start = a, end = b, age = 0f, dur = Random.Range(0.8f, 1.2f), height = Random.Range(0.9f, 1.8f) });
            Fx.Burst(a, new Color(0.75f, 0.95f, 1f), 7, 2f, 0.1f);
        }

        void Update()
        {
            float dt = Time.deltaTime, time = Time.time;

            // Luciérnagas: vuelan en ochos alrededor de su sitio y laten.
            for (int i = 0; i < flies.Count; i++)
            {
                var f = flies[i];
                float t = time * f.speed + f.phase;
                f.t.position = f.home + new Vector3(Mathf.Sin(t) * 0.9f, Mathf.Sin(t * 1.7f) * 0.35f, Mathf.Sin(t * 2f) * 0.5f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3f + f.phase * 2f);
                f.t.localScale = Vector3.one * Mathf.Lerp(0.04f, 0.12f, pulse * pulse);
            }

            // Peces: saltan en arco, giran según la trayectoria y salpican al entrar.
            if (time >= nextFish) { nextFish = time + Random.Range(fishInterval.x, fishInterval.y); SpawnFish(); }
            for (int i = fish.Count - 1; i >= 0; i--)
            {
                var f = fish[i];
                f.age += dt;
                float k = f.age / f.dur;
                if (k >= 1f) { Fx.Burst(f.end, new Color(0.75f, 0.95f, 1f), 9, 2.2f, 0.1f); Destroy(f.t.gameObject); fish.RemoveAt(i); continue; }
                Vector3 p = Vector3.Lerp(f.start, f.end, k) + Vector3.up * (4f * f.height * k * (1f - k));
                Vector3 vel = (f.end - f.start) / f.dur + Vector3.up * (4f * f.height * (1f - 2f * k) / f.dur);
                f.t.position = p;
                if (vel.sqrMagnitude > 0.01f) f.t.rotation = Quaternion.LookRotation(vel.normalized) * Quaternion.Euler(0f, 0f, Mathf.Sin(f.age * 25f) * 15f);
                fish[i] = f;
            }

            // Nubes: pasan despacio y dan la vuelta.
            foreach (var c in cloudList)
            {
                c.t.position += Vector3.right * c.speed * dt;
                if (c.t.position.x > center.x + 110f) c.t.position += Vector3.left * 220f;
            }
        }
    }
}
