using UnityEngine;

namespace Lizards
{
    /// <summary>Va en el modelo (junto al Animator) y reenvía los eventos de los clips (despegue, impacto, expulsión…) al slime.</summary>
    public class SlimeAnimationEvents : MonoBehaviour
    {
        SlimeEnemy slime;
        void Awake() => slime = GetComponentInParent<SlimeEnemy>();
        public void OnSlimeEvent(string id) { if (slime) slime.OnSlimeEvent(id); }
    }
}
