using TMPro;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Rótulo con letra de juego (TextMesh Pro) que copia el texto de un TextMesh oculto.
    /// Así los scripts que ya escriben en el TextMesh (expendedora, salida, jugadores) siguen funcionando.
    /// </summary>
    public class SignLabel : MonoBehaviour
    {
        public TextMesh source;
        public TextMeshPro target;
        string last;

        void LateUpdate()
        {
            if (!source || !target) return;
            if (source.text != last) { last = source.text; target.text = last; }
        }
    }
}
