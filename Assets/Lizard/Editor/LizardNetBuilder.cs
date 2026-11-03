using Fusion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>
    /// Menú: Lizards → Preparar multijugador en la escena abierta.
    /// Crea el prefab de red de Sunny (NetworkObject + NetworkTransform + LizardNetSync), lo registra en Fusion
    /// y añade la pantalla de sala a la escena.
    /// </summary>
    public static class LizardNetBuilder
    {
        public const string NetPrefabPath = "Assets/Lizard/Prefabs/SunnyRed.prefab";

        [MenuItem("Lizards/Preparar multijugador en la escena abierta", priority = 5)]
        public static string Setup()
        {
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SunnyBuilder.PrefabPath) ?? SunnyBuilder.BuildPrefab();
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            temp.name = "Sunny (red)";
            if (!temp.GetComponent<NetworkObject>()) temp.AddComponent<NetworkObject>();
            if (!temp.GetComponent<NetworkTransform>()) temp.AddComponent<NetworkTransform>();
            if (!temp.GetComponent<LizardNetSync>()) temp.AddComponent<LizardNetSync>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, NetPrefabPath);
            Object.DestroyImmediate(temp);

            // Fusion necesita el prefab en su tabla de prefabs de red.
            string table = "tabla de prefabs: ";
            try
            {
                var t = System.Type.GetType("Fusion.Editor.NetworkProjectConfigUtilities, Fusion.Unity.Editor");
                var m = t?.GetMethod("RebuildPrefabTable", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (m != null) { m.Invoke(null, null); table += "reconstruida"; }
                else table += "se reconstruye sola al importar";
            }
            catch (System.Exception e) { table += "aviso: " + e.Message; }

            var scene = EditorSceneManager.GetActiveScene();
            var hud = Object.FindAnyObjectByType<LizardHUD>();
            var host = hud ? hud.gameObject : new GameObject("Sistemas (HUD y depuración)");
            var lobby = host.GetComponent<LizardLobby>() ?? host.AddComponent<LizardLobby>();
            lobby.playerPrefab = prefab.GetComponent<NetworkObject>();
            var player = Object.FindAnyObjectByType<LizardCameraRig>()?.target;
            if (player) lobby.spawnPoint = player.transform.position + Vector3.up * 0.1f;
            EditorUtility.SetDirty(lobby);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // La escena debe estar en Build Settings para que Fusion la identifique.
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == scene.path)) { scenes.Insert(0, new EditorBuildSettingsScene(scene.path, true)); EditorBuildSettings.scenes = scenes.ToArray(); }

            string msg = $"[Lizards] Multijugador preparado: prefab {NetPrefabPath}, pantalla de sala en '{host.name}', {table}.";
            Debug.Log(msg);
            return msg;
        }
    }
}
