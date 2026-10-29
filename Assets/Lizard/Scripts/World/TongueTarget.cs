using UnityEngine;

namespace Lizards
{
    /// <summary>Marca superficies, puntos de enganche u objetos a los que se puede pegar la lengua.
    /// La comida y los jugadores no lo necesitan.</summary>
    public class TongueTarget : MonoBehaviour
    {
        public TongueTargetKind kind = TongueTargetKind.Surface;
    }
}
