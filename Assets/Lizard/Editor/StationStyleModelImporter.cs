using UnityEditor;
using UnityEngine;

namespace Lizards.EditorTools
{
    public sealed class StationStyleModelImporter : AssetPostprocessor
    {
        void OnPostprocessModel(GameObject model)
        {
            if(!assetPath.StartsWith("Assets/Lizard/Art/StationStyle/Models/"))return;
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var colors=filter.sharedMesh.colors;
                // FBX vertex colors arrive encoded as sRGB; shaders in this linear project need linear values.
                for(int i=0;i<colors.Length;i++)colors[i]=colors[i].linear;
                filter.sharedMesh.colors=colors;
            }
        }
    }
}
