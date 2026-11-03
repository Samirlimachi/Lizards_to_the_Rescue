using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Quitar contornos (solo jugadores con contorno).
    /// Pone a 0 el contorno negro de todos los materiales Lizards/Toon que no sean de un personaje jugable,
    /// y les da un sombreado más suave (estilo 3D cartoon). También repasa los materiales de la escena abierta.
    /// </summary>
    public static class OutlineCleanup
    {
        static readonly string[] PlayerPaths = { "Assets/Lizard/Models/SunnyV2/", "Assets/Lizard/Models/Sunny/", "Assets/Lizard/Materials/Rana_" };

        static bool IsPlayer(Material m)
        {
            string path = AssetDatabase.GetAssetPath(m);
            foreach (var p in PlayerPaths) if (path.StartsWith(p)) return true;
            return false;
        }

        static bool Clean(Material m)
        {
            if (!m || !m.shader || m.shader.name != "Lizards/Toon" || IsPlayer(m)) return false;
            if (m.GetFloat("_OutlineWidth") <= 0f && m.GetFloat("_ShadowSoftness") >= 0.27f) return false;
            m.SetFloat("_OutlineWidth", 0f);
            m.SetFloat("_ShadowSoftness", 0.28f);
            m.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.1f));
            EditorUtility.SetDirty(m);
            return true;
        }

        [MenuItem("Lizards/Quitar contornos (solo jugadores con contorno)", priority = 10)]
        public static string Run()
        {
            int assets = 0, scene = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
                if (Clean(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))) assets++;
            // Materiales guardados dentro de la escena (no son assets): solo los que no estén en un jugador.
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (r.GetComponentInParent<LizardPlayer>(true)) continue;
                foreach (var m in r.sharedMaterials) if (m && !AssetDatabase.Contains(m) && Clean(m)) scene++;
            }
            AssetDatabase.SaveAssets();
            if (scene > 0) { var s = EditorSceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(s); EditorSceneManager.SaveScene(s); }
            string msg = $"[Lizards] Contornos quitados: {assets} materiales del proyecto y {scene} de la escena.";
            Debug.Log(msg);
            return msg;
        }
    }
}
