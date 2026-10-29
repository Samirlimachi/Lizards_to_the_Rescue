using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Lizards
{
    /// <summary>Atajos de depuración: cambiar estado, ajustar reservas, recibir golpes, reiniciar escena, cambiar de personaje.</summary>
    public class LizardDebugTools : MonoBehaviour
    {
        public LizardCameraRig rig;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !rig || !rig.target) return;
            var p = rig.target; var cfg = p.Config;

            if (kb.f1Key.wasPressedThisFrame) ForceForm(p, LizardForm.Normal);
            if (kb.f2Key.wasPressedThisFrame) ForceForm(p, LizardForm.Small);
            if (kb.f3Key.wasPressedThisFrame) ForceForm(p, LizardForm.Giant);
            if (kb.f4Key.wasPressedThisFrame) { p.Stomach.Add(1f, "depuración"); p.EvaluateForm(); }
            if (kb.f5Key.wasPressedThisFrame) { p.Stomach.Remove(1f, "depuración"); p.EvaluateForm(); }
            if (kb.f6Key.wasPressedThisFrame) p.Acid.Refill();
            if (kb.f7Key.wasPressedThisFrame)
                p.Health.TakeHit(new DamageInfo { type = DamageType.Debug, sourcePosition = p.transform.position + p.transform.forward, foodLoss = -1 });
            if (kb.f8Key.wasPressedThisFrame)
                FoodFactory.CreateFruit(p.transform.position + p.transform.forward * 1.5f * Mathf.Max(1f, p.Stats.visualScale) + Vector3.up);
            if (kb.f9Key.wasPressedThisFrame) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0 ? SceneManager.GetActiveScene().name : SceneManager.GetActiveScene().path);
            if (kb.f11Key.wasPressedThisFrame) p.GetComponent<LizardLipSync>()?.Toggle();
            if (kb.f12Key.wasPressedThisFrame) SwitchControl();
        }

        public static void ForceForm(LizardPlayer p, LizardForm f)
        {
            var cfg = p.Config;
            switch (f)
            {
                case LizardForm.Small:
                    p.Stomach.DebugSetFood(0f);
                    p.Forms.RequestForm(LizardForm.Small, true);
                    break;
                case LizardForm.Normal:
                    p.Stomach.DebugSetFood(Mathf.Clamp(p.Stomach.Food, cfg.smallRecoverFood, cfg.giantThreshold - 1f));
                    p.Forms.RequestForm(LizardForm.Normal);
                    break;
                case LizardForm.Giant:
                    p.Stomach.DebugSetFood(Mathf.Max(p.Stomach.Food, cfg.giantThreshold));
                    if (p.Form == LizardForm.Small) p.Forms.RequestForm(LizardForm.Normal);
                    p.Forms.RequestForm(LizardForm.Giant);
                    break;
            }
        }

        void SwitchControl()
        {
            var all = LizardPlayer.All;
            if (all.Count < 2) return;
            var cur = rig.target;
            int i = all.IndexOf(cur);
            var next = all[(i + 1) % all.Count];
            cur.Tongue.Release(false);
            cur.Input.controlled = false;
            next.Input.controlled = true;
            rig.SetTarget(next);
            next.Message("Ahora controlas a " + next.displayName);
        }
    }
}
