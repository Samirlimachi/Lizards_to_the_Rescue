using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lizards.EditorTools
{
    /// <summary>Menú: Lizards → Agregar lagarto a la escena abierta. Coloca jugador, cámara y HUD (y un suelo si no hay).</summary>
    public static class LizardAddToScene
    {
        const string PrefabPath = "Assets/Lizard/Prefabs/Lagarto.prefab";

        [MenuItem("Lizards/Agregar lagarto a la escena abierta", priority = 1)]
        public static void Add()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab)
            {
                EditorUtility.DisplayDialog("Lizards", "No existe el prefab del lagarto. Usa antes «Lizards → Construir escena de prueba».", "OK");
                return;
            }

            var existing = Object.FindAnyObjectByType<LizardCameraRig>();
            Vector3 spawn = Vector3.up * 0.1f;
            if (SceneView.lastActiveSceneView)
            {
                var piv = SceneView.lastActiveSceneView.pivot;
                spawn = Physics.Raycast(piv + Vector3.up * 50f, Vector3.down, out var hit, 200f) ? hit.point + Vector3.up * 0.1f : piv;
            }

            if (!Physics.Raycast(spawn + Vector3.up, Vector3.down, 50f))
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Suelo";
                floor.transform.position = spawn + Vector3.down * 0.5f;
                floor.transform.localScale = new Vector3(40f, 1f, 40f);
                Undo.RegisterCreatedObjectUndo(floor, "Agregar lagarto");
            }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "Jugador";
            player.transform.position = spawn;
            Undo.RegisterCreatedObjectUndo(player, "Agregar lagarto");
            var lp = player.GetComponent<LizardPlayer>();

            if (existing)
            {
                // Ya hay cámara de lagarto: el nuevo será un compañero sin control (F12 para cambiar).
                player.GetComponent<LizardInput>().controlled = false;
                lp.displayName = "Compañero";
            }
            else
            {
                lp.displayName = "Sunny";
                if (!Object.FindAnyObjectByType<Light>())
                {
                    var sun = new GameObject("Sol").AddComponent<Light>();
                    sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
                    sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    Undo.RegisterCreatedObjectUndo(sun.gameObject, "Agregar lagarto");
                }
                var camGo = new GameObject("Cámara del lagarto");
                var old = Camera.main;
                if (old) { Undo.RecordObject(old.gameObject, "Agregar lagarto"); old.gameObject.SetActive(false); }
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.nearClipPlane = 0.05f;
                camGo.AddComponent<AudioListener>();
                var rig = camGo.AddComponent<LizardCameraRig>();
                rig.target = lp;
                camGo.transform.position = spawn + new Vector3(0f, 3f, -5f);
                Undo.RegisterCreatedObjectUndo(camGo, "Agregar lagarto");

                var sys = new GameObject("Sistemas (HUD y depuración)");
                sys.AddComponent<LizardHUD>().rig = rig;
                sys.AddComponent<LizardDebugTools>().rig = rig;
                Undo.RegisterCreatedObjectUndo(sys, "Agregar lagarto");
            }

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(player.scene);
            SceneView.lastActiveSceneView?.FrameSelected();
        }
    }
}
