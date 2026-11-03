using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Lizards.EditorTools
{
    public static class FoodCollectionBuilder
    {
        public const string Folder="Assets/Lizard/Art/FoodCollection";
        static readonly string[] Ids={"Apple","Banana","Corn","Broccoli","Avocado","Pepper"};
        static readonly string[] Names={"Manzana","Plátano","Mazorca","Brócoli","Aguacate","Pimiento"};
        static readonly float[] Length={.38f,.54f,.46f,.49f,.46f,.44f};
        static readonly float[] Mass={.3f,.24f,.42f,.3f,.28f,.25f};

        [MenuItem("Lizards/Comida/Importar modelos y colocar en LEVEL1")]
        public static void Build()
        {
            if(Application.isPlaying)throw new Exception("Stop Play mode first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.name!="LEVEL1")throw new Exception("Open LEVEL1 first. Other scenes are never modified.");
            Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Prefabs");
            AssetDatabase.Refresh();
            var prefabs=new GameObject[Ids.Length];
            for(int i=0;i<Ids.Length;i++)prefabs[i]=MakePrefab(i);
            // Archive only the old provisional food; preserve other user objects and giant watermelons.
            var existing=UnityEngine.Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None)
                .Where(f=>!f.GetComponent<FoodCollectionIdentity>() && f.size<.5f)
                .OrderBy(f=>f.transform.position.z).ThenBy(f=>f.transform.position.x).ToArray();
            var archiveTransform=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t=>t.name=="__Comida provisional archivada");
            var archive=archiveTransform?archiveTransform.gameObject:null;
            if(!archive){archive=new GameObject("__Comida provisional archivada");archive.SetActive(false);}
            var root=GameObject.Find("COMIDA / colección modelada");if(!root)root=new GameObject("COMIDA / colección modelada");
            int count=0;
            foreach(var old in existing)
            {
                int i=count++%Ids.Length;var pos=old.transform.position;float value=old.nutrition;
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[i],scene);
                instance.transform.SetParent(root.transform,true);instance.name=Names[i]+" · lengua / comer [E]";
                instance.GetComponent<FoodItem>().nutrition=value;
                float ground=pos.y-old.size;
                foreach(var hit in Physics.RaycastAll(pos+Vector3.up*2,Vector3.down,6,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                {
                    if(hit.collider.GetComponentInParent<FoodItem>() || hit.collider.GetComponentInParent<LizardPlayer>())continue;
                    ground=hit.point.y;break;
                }
                var bounds=VisualBounds(instance);pos.y=ground+bounds.extents.y+.025f;
                instance.transform.position=pos;
                old.transform.SetParent(archive.transform,true);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Food Collection] Six FBX/materials/prefabs ready. Replaced "+count+" provisional food items. Nutrition preserved.");
        }

        static GameObject MakePrefab(int i)
        {
            string id=Ids[i],path=Folder+"/Models/"+id+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.globalScale=1;importer.importCameras=false;importer.importLights=false;
            importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.meshCompression=ModelImporterMeshCompression.Off;importer.isReadable=false;
            importer.SaveAndReimport();
            string texPath=Folder+"/Textures/"+id+"_BaseColor.png";
            var ti=(TextureImporter)AssetImporter.GetAtPath(texPath);ti.textureType=TextureImporterType.Default;
            ti.sRGBTexture=true;ti.mipmapEnabled=true;ti.maxTextureSize=1024;ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.SaveAndReimport();
            string mp=Folder+"/Materials/"+id+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(mp);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,mp);}
            material.SetColor("_BaseColor",Color.white);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            material.SetFloat("_Smoothness",i==0||i==5?.3f:.12f);material.SetFloat("_Metallic",0);
            var root=new GameObject(Names[i]);
            var visual=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));visual.name="Modelo · "+id;
            visual.transform.SetParent(root.transform,false);
            if(i==4)visual.transform.localRotation=Quaternion.Euler(0,180,0)*visual.transform.localRotation;
            foreach(var r in visual.GetComponentsInChildren<Renderer>())r.sharedMaterial=material;
            var b=VisualBounds(root);float scale=Length[i]/Mathf.Max(b.size.x,b.size.y,b.size.z);
            visual.transform.localScale*=scale;b=VisualBounds(root);visual.transform.position-=b.center;
            b=VisualBounds(root);
            var rb=root.AddComponent<Rigidbody>();rb.mass=Mass[i];rb.linearDamping=.25f;rb.angularDamping=.6f;
            rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            if(i==0){var c=root.AddComponent<SphereCollider>();c.radius=b.extents.x*.98f;c.center=new Vector3(0,b.min.y+c.radius,0);}
            else if(i==1)
            {
                // Small overlapping convex volumes follow the banana curve without mesh-collider costs.
                for(int j=-1;j<=1;j++)
                {
                    var c=root.AddComponent<SphereCollider>();c.center=new Vector3(j*b.extents.x*.68f,j==0?-b.extents.y*.62f:b.extents.y*.48f,0);
                    c.radius=j==0?b.size.z*.57f:b.size.z*.48f;
                }
            }
            else if(i==4){var c=root.AddComponent<BoxCollider>();c.size=Vector3.Scale(b.size,new Vector3(.9f,1f,.9f));}
            else
            {
                var c=root.AddComponent<CapsuleCollider>();c.direction=1;c.radius=Mathf.Max(b.extents.x,b.extents.z)*.93f;c.height=b.size.y;
            }
            var food=root.AddComponent<FoodItem>();food.nutrition=1;food.size=Length[i]*.5f;food.edible=true;
            root.AddComponent<FoodCollectionIdentity>().modelId=id;
            string pp=Folder+"/Prefabs/"+id+".prefab";var prefab=PrefabUtility.SaveAsPrefabAsset(root,pp);
            UnityEngine.Object.DestroyImmediate(root);return prefab;
        }

        public static Bounds VisualBounds(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;
            foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }

        // Actual Unity render of exported assets, independent of the Blender studio preview.
        public static void CaptureModels(string destination)
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var objects=new GameObject[6];
            for(int i=0;i<6;i++)
            {
                var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Prefabs/"+Ids[i]+".prefab"));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
                go.transform.localScale=Vector3.one*2.6f;go.transform.position=new Vector3((i%3-1)*1.8f,0,i/3*1.65f);
                go.GetComponent<Rigidbody>().isKinematic=true;
                var b=VisualBounds(go);go.transform.position+=Vector3.up*(-b.min.y+.03f);objects[i]=go;
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor,scene);
            floor.transform.position=new Vector3(0,-.1f,.8f);floor.transform.localScale=new Vector3(30,.15f,30);
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.65f,.69f,.72f));floor.GetComponent<Renderer>().sharedMaterial=mat;
            var light=new GameObject("Preview light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light,scene);
            var l=light.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.7f;light.transform.rotation=Quaternion.Euler(48,-35,0);
            var camera=new GameObject("Preview camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera,scene);
            var c=camera.AddComponent<Camera>();c.transform.position=new Vector3(2.8f,5,-6);c.transform.LookAt(new Vector3(0,.35f,.85f));
            c.orthographic=true;c.orthographicSize=3.3f;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.65f,.69f,.72f);
            c.scene=scene;c.AdditionalData().renderPostProcessing=false;
            var rt=new RenderTexture(1600,1100,24);rt.Create();
            RenderPipelineRender(c,rt);var previous=RenderTexture.active;RenderTexture.active=rt;
            var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();
            File.WriteAllBytes(destination,tex.EncodeToPNG());RenderTexture.active=previous;
            UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(mat);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        static void RenderPipelineRender(Camera camera,RenderTexture rt)
        {
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
        }
    }
    static class FoodCameraExtensions
    {
        public static UniversalAdditionalCameraData AdditionalData(this Camera camera)=>camera.GetUniversalAdditionalCameraData();
    }
}
