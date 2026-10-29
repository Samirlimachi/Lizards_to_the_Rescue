using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Autoprueba en tiempo de juego. Crea una zona aislada lejos del nivel, con dos lagartos de prueba,
    /// y verifica los criterios de finalización. Resultado en consola (PASS/FAIL) y en <see cref="LastReport"/>.
    /// </summary>
    public class LizardSelfTest : MonoBehaviour
    {
        public static string LastReport { get; private set; } = "";
        public static bool Running { get; private set; }

        readonly List<string> lines = new();
        int pass, fail;
        LizardConfig cfg;

        public static void Run(LizardConfig config)
        {
            if (Running || !Application.isPlaying) return;
            var go = new GameObject("Autoprueba lagarto");
            var t = go.AddComponent<LizardSelfTest>();
            t.cfg = config ? config : LizardConfig.Fallback;
            t.StartCoroutine(t.Routine());
        }

        void Check(string name, bool ok, string detail = "")
        {
            if (ok) pass++; else fail++;
            string l = $"{(ok ? "PASS" : "FAIL")} · {name}{(detail.Length > 0 ? " — " + detail : "")}";
            lines.Add(l);
            if (ok) Debug.Log("[Autoprueba] " + l); else Debug.LogError("[Autoprueba] " + l);
        }

        static GameObject Box(Vector3 pos, Vector3 size, Transform parent)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.transform.SetParent(parent);
            b.transform.position = pos; b.transform.localScale = size;
            return b;
        }

        IEnumerator Fixed(int n) { for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate(); }

        IEnumerator Routine()
        {
            Running = true;
            Vector3 o = new Vector3(0f, 500f, 0f);
            var root = new GameObject("Zona de autoprueba").transform;
            Box(o + Vector3.down * 0.5f, new Vector3(60f, 1f, 60f), root);
            var mats = LizardModelMaterials.CreateRuntime();
            var a = LizardFactory.Create("Prueba A", o + new Vector3(0f, 0.1f, 0f), cfg, mats, null, false);
            var b = LizardFactory.Create("Prueba B", o + new Vector3(3f, 0.1f, 0f), cfg, mats, null, false);
            a.transform.SetParent(root); b.transform.SetParent(root);
            yield return Fixed(10);

            // 1. Comida: no se consume dos veces ni la reclaman dos jugadores.
            var f = FoodFactory.CreateFruit(o + new Vector3(0f, 0.3f, 1f));
            f.transform.SetParent(root);
            yield return null;
            Check("Reclamo exclusivo", f.TryGrab(a) && !f.TryGrab(b));
            f.ReleaseGrab(a);
            float before = a.Stomach.Food;
            bool first = a.TryEat(f), second = b.TryEat(f), third = a.TryEat(f);
            Check("Comida no duplicada", first && !second && !third && Mathf.Approximately(a.Stomach.Food, before + 1f),
                $"A comió {first}, B {second}, A otra vez {third}");

            // 2. Expulsar conserva el valor exacto.
            a.Stomach.DebugSetFood(5f);
            float world0 = FoodItem.TotalInWorld();
            a.Stomach.EjectPellets(3f, Vector3.up, 2f, 0.2f);
            yield return null;
            float world1 = FoodItem.TotalInWorld();
            Check("Expulsar sin crear comida", Mathf.Abs((world1 - world0) - 3f) < 0.001f && Mathf.Abs(a.Stomach.Food - 2f) < 0.001f,
                $"mundo +{world1 - world0:0.##}, estómago {a.Stomach.Food:0.##}");
            foreach (var pellet in FoodItem.All.ToArray()) if (pellet.transform.position.y > 400f) Destroy(pellet.gameObject);

            // 3. Intervalo de protección.
            a.Stomach.DebugSetFood(5f);
            var hit = new DamageInfo { type = DamageType.Debug, sourcePosition = a.transform.position + Vector3.forward, foodLoss = -1 };
            bool h1 = a.Health.TakeHit(hit), h2 = a.Health.TakeHit(hit), h3 = a.Health.TakeHit(hit);
            Check("Protección tras golpe", h1 && !h2 && !h3 && Mathf.Abs(a.Stomach.Food - (5f - cfg.damageEjectAmount)) < 0.001f,
                $"comida {a.Stomach.Food:0.##}");

            // 4. Golpe que vacía → normal vacío; golpe en vacío → renacuajo.
            yield return new WaitForSeconds(cfg.invulnerabilityTime + 0.1f);
            a.Health.TakeHit(hit);
            Check("Golpe que agota la reserva deja normal vacío", a.Form == LizardForm.Normal && a.Stomach.Food <= 0.001f, $"forma {a.Form}, comida {a.Stomach.Food:0.##}");
            yield return new WaitForSeconds(cfg.invulnerabilityTime + 0.1f);
            a.Health.TakeHit(hit);
            Check("Golpe estando vacío → renacuajo", a.Form == LizardForm.Small);
            Check("Renacuajo sin ácido", !a.Acid.CanFire(out _));
            Check("Renacuajo sin balanceo", !a.Stats.tongueGrapple);

            // 5. Recuperación: alimentar desde un compañero y punto de rescate.
            b.Stomach.DebugSetFood(3f);
            b.transform.position = a.transform.position + Vector3.right * 1.2f; b.Body.position = b.transform.position;
            yield return Fixed(3);
            b.EjectOrFeed();
            Check("Alimentar al renacuajo lo devuelve a normal", a.Form == LizardForm.Normal && Mathf.Abs(b.Stomach.Food - 2f) < 0.001f,
                $"A {a.Form}, B comida {b.Stomach.Food:0.##}");
            LizardDebugTools.ForceForm(a, LizardForm.Small);
            Check("Rescate individual", a.Rescue("autoprueba") && a.Form == LizardForm.Normal);
            yield return Fixed(5);
            a.Acid.Refill();
            Check("Ácido disponible tras volver a normal", a.Acid.CanFire(out string why), why ?? "");

            // 6. Crecer bajo techo bajo se aplaza; al salir se completa.
            yield return new WaitForSeconds(cfg.invulnerabilityTime);
            Vector3 tunnel = o + new Vector3(-15f, 0f, 0f);
            Box(tunnel + new Vector3(0f, 1.9f, 0f), new Vector3(8f, 0.6f, 4f), root);
            Box(tunnel + new Vector3(0f, 0.8f, 2.2f), new Vector3(8f, 1.6f, 0.4f), root);
            Box(tunnel + new Vector3(0f, 0.8f, -2.2f), new Vector3(8f, 1.6f, 0.4f), root);
            a.Body.position = tunnel + Vector3.up * 0.05f; a.transform.position = a.Body.position;
            yield return Fixed(5);
            a.Stomach.DebugSetFood(cfg.giantThreshold);
            a.EvaluateForm();
            yield return Fixed(5);
            Check("No crece dentro del túnel", a.Form == LizardForm.Normal && a.Forms.Pending == LizardForm.Giant);
            a.Body.position = tunnel + new Vector3(12f, 0.05f, 0f); a.transform.position = a.Body.position;
            yield return new WaitForSeconds(0.6f);
            Check("Crece al tener espacio", a.Form == LizardForm.Giant);
            Check("Gigante: ácido desactivado, lengua limitada", !a.Stats.acidEnabled && a.Stats.tongueEnabled && !a.Stats.tongueGrapple);
            a.Stomach.Remove(cfg.giantThreshold - cfg.giantExitThreshold + 0.5f, "autoprueba");
            a.EvaluateForm();
            yield return Fixed(3);
            Check("Gigante vuelve a normal al perder comida", a.Form == LizardForm.Normal);
            Check("Lengua y ácido disponibles otra vez", a.Stats.tongueEnabled && a.Stats.acidEnabled && a.Stats.tongueGrapple);

            // 7. La lengua se suelta si el objetivo desaparece o se vuelve inválido.
            var crate = Box(a.transform.position + a.transform.forward * 3f + Vector3.up * 0.5f, Vector3.one * 0.6f, root);
            var crb = crate.AddComponent<Rigidbody>(); crb.mass = 2f;
            crate.AddComponent<TongueTarget>().kind = TongueTargetKind.Object;
            yield return Fixed(3);
            bool attached = a.Tongue.DebugAttach(crate.GetComponent<Collider>());
            Check("Lengua se engancha a objeto", attached, a.Tongue.State.ToString());
            Destroy(crate);
            yield return Fixed(4);
            Check("Lengua se suelta si el objetivo desaparece", a.Tongue.State != TongueState.Attached);
            yield return new WaitForSeconds(0.3f);
            var f2 = FoodFactory.CreateFruit(a.transform.position + a.transform.forward * 4f + Vector3.up * 0.5f);
            f2.transform.SetParent(root);
            yield return Fixed(3);
            bool grabbed = a.Tongue.DebugAttach(f2.GetComponent<Collider>());
            float bFood = b.Stomach.Food;
            b.TryEat(f2); // B no puede: la tiene A
            Check("Comida agarrada no la come otro", grabbed && f2 && f2.State == FoodState.Grabbed && Mathf.Approximately(b.Stomach.Food, bFood));
            f2.TryConsume(null, out _); // desaparece (p. ej. entregada en la meta)
            yield return Fixed(3);
            Check("Lengua se suelta si la comida se consume", a.Tongue.State != TongueState.Attached);

            // 8. Cámara en los tres tamaños.
            var camGo = new GameObject("Cámara autoprueba");
            var rig = camGo.AddComponent<LizardCameraRig>();
            camGo.GetComponent<Camera>().enabled = false;
            rig.SetTarget(a);
            foreach (var form in new[] { LizardForm.Normal, LizardForm.Small, LizardForm.Giant })
            {
                LizardDebugTools.ForceForm(a, form);
                yield return new WaitForSeconds(1.5f);
                float want = cfg.Get(form).cameraDistance;
                Check($"Cámara en forma {form}", a.Form == form && Mathf.Abs(rig.CurrentDistance - want) < 0.6f && !float.IsNaN(rig.CameraTransform.position.x),
                    $"distancia {rig.CurrentDistance:0.0} / {want:0.0}");
            }
            Destroy(camGo);

            var sb = new StringBuilder();
            sb.AppendLine($"Autoprueba: {pass} PASS, {fail} FAIL");
            foreach (var l in lines) sb.AppendLine(l);
            LastReport = sb.ToString();
            if (fail == 0) Debug.Log("[Autoprueba] " + LastReport); else Debug.LogError("[Autoprueba] " + LastReport);

            Destroy(root.gameObject);
            Running = false;
            Destroy(gameObject);
        }
    }
}
