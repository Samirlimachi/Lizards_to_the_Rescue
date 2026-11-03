using UnityEngine;

namespace Lizards
{
    /// <summary>Contrato entre los sistemas del personaje y cualquier modelo visual (lagarto, rana, Sunny…).</summary>
    public abstract class LizardVisualBase : MonoBehaviour
    {
        [Tooltip("Origen de la lengua, el ácido y la comida expulsada.")]
        public Transform mouthPoint;

        public abstract void PlayGulp();
        public abstract void PlayEject();
        public abstract void PlaySpit();
        public abstract void PlayHurt();
        public abstract void PlayHelp();
        public abstract void PlayCrash(float speed);
        public abstract void OnFormChanged(LizardForm prev, LizardForm now, bool animate);
    }
}
