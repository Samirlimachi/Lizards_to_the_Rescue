using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    /// <summary>
    /// Probador de sombreros: ← → para verlos puestos al momento, E para confirmar (se guarda),
    /// "Sin sombrero" para quitárselo. Si te alejas sin confirmar vuelves al que tenías.
    /// </summary>
    public class HatWardrobe : MonoBehaviour
    {
        public float useRadius = 3f;
        public Vector3 promptOffset = new Vector3(0f, 2.6f, 0f);

        int committed, preview;
        bool previewing;

        void Start() { committed = preview = SwampHatFactory.Load(); }

        void Show(LizardPlayer p, int index)
        {
            int n = SwampHatFactory.Names.Length;
            preview = (index % n + n) % n;
            previewing = true;
            SwampHatFactory.Equip(p, preview, true);
            Sfx.Play(Sfx.TongueOut, transform.position, 0.25f);
        }

        void Confirm(LizardPlayer p)
        {
            committed = preview; previewing = false;
            SwampHatFactory.Save(committed);
            SwampHatFactory.Equip(p, committed, false);
            Sfx.Play(Sfx.Transform, p.CenterPosition, 0.5f);
            Fx.Burst(p.CenterPosition + Vector3.up * 0.6f, new Color(1f, 0.85f, 0.4f), 14, 2.5f, 0.1f);
        }

        void Revert(LizardPlayer p)
        {
            previewing = false; preview = committed;
            SwampHatFactory.Equip(p, committed, true);
        }

        void Update()
        {
            var p = LizardPlayer.All.FirstOrDefault(x => x.IsControlled);
            if (!p || LizardLobby.Visible) return;
            float d = Vector3.Distance(p.transform.position, transform.position);
            if (d < useRadius)
            {
                string name = SwampHatFactory.Names[previewing ? preview : committed];
                string label = previewing ? $"Quedarme con: {name}   (← → probar)" : $"Probar sombreros   (← →)";
                InteractPrompts.Offer(this, transform.position + promptOffset, label, d);
            }
            else if (previewing && d > useRadius + 1f) Revert(p);

            if (!InteractPrompts.IsActive(this)) return;
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.rightArrowKey.wasPressedThisFrame) Show(p, (previewing ? preview : committed) + 1);
                if (kb.leftArrowKey.wasPressedThisFrame) Show(p, (previewing ? preview : committed) - 1);
            }
            if (p.Input.Pressed(p.Input.Interact))
            {
                if (!previewing) Show(p, committed + 1);
                else Confirm(p);
            }
        }
    }
}
