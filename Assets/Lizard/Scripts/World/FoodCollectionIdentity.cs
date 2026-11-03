using UnityEngine;

namespace Lizards
{
    /// <summary>Identifies an imported food model without changing the shared FoodItem interaction.</summary>
    [DisallowMultipleComponent]
    public sealed class FoodCollectionIdentity : MonoBehaviour
    {
        public string modelId;
    }
}
