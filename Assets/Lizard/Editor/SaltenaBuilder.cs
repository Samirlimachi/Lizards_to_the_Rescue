using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using TMPro;
using Object=UnityEngine.Object;

namespace Lizards.EditorTools
{
    public static class SaltenaBuilder
    {
        public const string Dir="Assets/Lizard/Art/Saltena";
        public const string PrefabDir="Assets/Lizard/Resources/Saltena";
        [Serializable] class PieceInfo {public string name;public float[] position;public float volume;}
        [Serializable] class Manifest {public PieceInfo[] pieces;}
        static Material crust,inside;
        static Material SaveMat(string name,Color color)
        {
            string path=Dir+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.23f);return mat;
        }
        static Texture2D Filling()
        {
            const int n=256;var texture=new Texture2D(n,n,TextureFormat.RGB24,false);var pixels=new Color[n*n];
            var random=new System.Random(524);var colors=new[]{new Color(.89f,.64f,.26f),new Color(.35f,.19f,.07f),new Color(.36f,.48f,.11f),new Color(.79f,.25f,.06f)};
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)pixels[y*n+x]=new Color(.63f,.36f,.12f)*(float)(.9+random.NextDouble()*.18);
            for(int i=0;i<125;i++)
            {
                float cx=(float)random.NextDouble()*n,cy=(float)random.NextDouble()*n,rx=3+(float)random.NextDouble()*10,ry=3+(float)random.NextDouble()*9;var c=colors[random.Next(colors.Length)];
                for(int y=Mathf.Max(0,(int)(cy-ry));y<Mathf.Min(n,cy+ry);y++)for(int x=Mathf.Max(0,(int)(cx-rx));x<Mathf.Min(n,cx+rx);x++)
                {float d=Mathf.Pow((x-cx)/rx,2)+Mathf.Pow((y-cy)/ry,2);if(d<1)pixels[y*n+x]=Color.Lerp(pixels[y*n+x],c,Mathf.Clamp01((1-d)*5));}
            }
            texture.SetPixels(pixels);texture.Apply();File.WriteAllBytes(Dir+"/Textures/Filling.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(Dir+"/Textures/Filling.png");return AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"/Textures/Filling.png");
        }
        static GameObject MakeFood(string name,float value)
        {
            var go=new GameObject(name);var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Models/"+name+".fbx"));model.transform.SetParent(go.transform,false);model.name="Modelo";
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&m.name.Contains("Filling")?inside:crust).ToArray();
            var collision=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Dir+"/Models/"+name+"_Collider.fbx"));
            var mesh=new Mesh{name=name+"_Hull"};mesh.CombineMeshes(collision.GetComponentsInChildren<MeshFilter>().Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=f.transform.localToWorldMatrix}).ToArray());
            string meshPath=Dir+"/Colliders/"+name+".asset";var existingMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(existingMesh){EditorUtility.CopySerialized(mesh,existingMesh);Object.DestroyImmediate(mesh);mesh=existingMesh;}else AssetDatabase.CreateAsset(mesh,meshPath);Object.DestroyImmediate(collision);
            go.AddComponent<MeshCollider>().sharedMesh=mesh;go.GetComponent<MeshCollider>().convex=true;
            var body=go.AddComponent<Rigidbody>();body.mass=2.4f;body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.angularDamping=.3f;
            var food=go.AddComponent<FoodItem>();food.nutrition=value;var bounds=FoodCollectionBuilder.VisualBounds(go);food.size=Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)*.5f;
            go.AddComponent<FoodCollectionIdentity>().modelId=name;
            go.AddComponent<SharedFoodSync>().variant=name=="Saltena_Whole"?0:int.Parse(name.Substring(name.Length-2));
            return go;
        }
        [MenuItem("Lizards/Salteña/Crear recursos y sala de prueba")]
        public static string Build()
        {
            foreach(string sub in new[]{"Materials","Colliders"})Directory.CreateDirectory(Dir+"/"+sub);AssetDatabase.Refresh();
            Directory.CreateDirectory(PrefabDir);AssetDatabase.Refresh();
            var normal=(TextureImporter)AssetImporter.GetAtPath(Dir+"/Textures/Normal.png");normal.textureType=TextureImporterType.NormalMap;normal.SaveAndReimport();
            crust=SaveMat("Corteza dorada",Color.white);crust.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"/Textures/Crust.png"));crust.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Dir+"/Textures/Normal.png"));crust.EnableKeyword("_NORMALMAP");
            inside=SaveMat("Relleno de salteña",Color.white);inside.SetTexture("_BaseMap",Filling());inside.SetFloat("_Smoothness",.12f);
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Dir+"/pieces.json"));var pieces=new BreakableFood.Piece[manifest.pieces.Length];float volume=manifest.pieces.Sum(p=>p.volume);
            for(int i=0;i<pieces.Length;i++)
            {
                var p=manifest.pieces[i];var go=MakeFood(p.name,4*p.volume/volume);go.GetComponent<Rigidbody>().mass=2.4f*p.volume/volume;
                var prefab=PrefabUtility.SaveAsPrefabAsset(go,PrefabDir+"/"+p.name+".prefab");pieces[i]=new BreakableFood.Piece{prefab=prefab.GetComponent<FoodItem>(),offset=new Vector3(-p.position[0],p.position[1],p.position[2]),weight=p.volume};Object.DestroyImmediate(go);
            }
            var whole=MakeFood("Saltena_Whole",4);var fracture=whole.AddComponent<BreakableFood>();fracture.pieces=pieces;fracture.visual=whole.transform.Find("Modelo");PrefabUtility.SaveAsPrefabAsset(whole,PrefabDir+"/Saltena_Whole.prefab");Object.DestroyImmediate(whole);
            AssetDatabase.SaveAssets();CreateTestScene();return "Salteña completa y seis trozos comestibles creados. Sala: Assets/Scenes/SaltenaTest.unity";
        }
        static GameObject Box(string name,Vector3 p,Vector3 size,Material material)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        static void Sign(string message,Vector3 p)
        {var go=new GameObject("Instrucciones de salteña");go.transform.position=p;var text=go.AddComponent<TextMeshPro>();text.text=message;text.fontSize=2.5f;text.color=Color.white;text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(9,2.5f);}
        public static void CreateTestScene()
        {
            var active=SceneManager.GetActiveScene();if(active.isDirty&&!string.IsNullOrEmpty(active.path))EditorSceneManager.SaveScene(active);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var floor=SaveMat("Suelo de prueba",new Color(.30f,.34f,.29f));var wall=SaveMat("Pared de prueba",new Color(.44f,.33f,.62f));var rail=SaveMat("Guía lima",new Color(.6f,1,.03f));
            Box("Suelo",new Vector3(0,-.2f,0),new Vector3(18,.4f,22),floor);
            Box("Muro para impactos",new Vector3(6,1.5f,0),new Vector3(.6f,3,14),wall);
            Box("Pared del fondo",new Vector3(0,2,10),new Vector3(18,4,.6f),wall);
            Box("Remate de impacto",new Vector3(5.65f,2.9f,0),new Vector3(.12f,.12f,14),rail);
            for(int i=0;i<3;i++)
            {var f=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir+"/Saltena_Whole.prefab"));f.name="Salteña de prueba "+(i+1);f.transform.position=new Vector3(-2+i*2,.6f,0);f.GetComponent<SharedFoodSync>().id="saltena-test-"+i;PrefabUtility.RecordPrefabInstancePropertyModifications(f.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(f.GetComponent<SharedFoodSync>());}
            Sign("SALTEÑA ROMPIBLE\nDos jugadores: sujetar y tirar en sentidos opuestos\nSolo: arrástrala y golpéala contra el muro\nClic izq: lengua · R: acortar · E: comer · G: escupir\nF4: ayudante de prueba · F5: reiniciar · F2: sala online",new Vector3(0,3.3f,5));
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Prefabs/Sunny.prefab"));player.transform.position=new Vector3(0,.1f,-5);player.GetComponent<LizardInput>().controlled=true;player.GetComponent<LizardPlayer>().startFood=0;
            var cam=new GameObject("Cámara del jugador");cam.tag="MainCamera";cam.AddComponent<Camera>();cam.AddComponent<AudioListener>();cam.AddComponent<LizardCameraRig>().target=player.GetComponent<LizardPlayer>();
            var hud=new GameObject("HUD").AddComponent<LizardHUD>();hud.rig=cam.GetComponent<LizardCameraRig>();hud.showDebug=false;
            var room=new GameObject("Sala multijugador de prueba").AddComponent<LizardLobby>();room.showOnStart=false;room.spawnPoint=new Vector3(0,.1f,-5);room.playerPrefab=AssetDatabase.LoadAssetAtPath<Fusion.NetworkObject>("Assets/Lizard/Prefabs/SunnyRed.prefab");
            var practice=new GameObject("Pruebas de salteña").AddComponent<SaltenaPractice>();practice.room=room;practice.helperPrefab=AssetDatabase.LoadAssetAtPath<LizardPlayer>("Assets/Lizard/Prefabs/Sunny.prefab");
            var light=new GameObject("Luz").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(50,-25,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.54f,.51f,.63f);RenderSettings.fog=false;
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/SaltenaTest.unity");
            var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path=="Assets/Scenes/SaltenaTest.unity"))scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/SaltenaTest.unity",true));EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
        }
        public static string AddToLevel2()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/LEVEL2.unity");
            if(GameObject.Find("Salteñas rompibles"))return "Already placed";
            var root=new GameObject("Salteñas rompibles");
            foreach(var pos in new[]{new Vector3(-5,.7f,-18),new Vector3(20,.7f,-13),new Vector3(-25,.7f,12)})
            {var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir+"/Saltena_Whole.prefab"));go.transform.SetParent(root.transform);go.transform.position=pos;go.GetComponent<SharedFoodSync>().id="saltena-level2-"+root.transform.childCount;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<SharedFoodSync>());}
            EditorSceneManager.SaveScene(scene);return "Three salteñas placed in LEVEL2";
        }
        public static void CapturePreview(string path)
        {
            var preview=new GameObject("Temporary salteña render");
            try
            {
                Vector3 center=new Vector3(100,.7f,100);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir+"/Saltena_Whole.prefab");
                var whole=Object.Instantiate(prefab,center+Vector3.left*1.05f,Quaternion.Euler(0,-15,0));whole.transform.SetParent(preview.transform,true);
                foreach(var collider in whole.GetComponentsInChildren<Collider>())collider.enabled=false;
                int index=0;
                foreach(var piece in prefab.GetComponent<BreakableFood>().pieces)
                {
                    Vector3 offset=piece.offset*1.5f;bool top=index%2==1;
                    if(top)offset.z+=.6f;
                    var go=Object.Instantiate(piece.prefab.gameObject,center+Vector3.right*1.1f+offset,top?Quaternion.Euler(55,0,0):Quaternion.identity);go.transform.SetParent(preview.transform,true);
                    foreach(var collider in go.GetComponentsInChildren<Collider>())collider.enabled=false;
                    index++;
                }
                var baseMaterial=SaveMat("Base de presentación",new Color(.23f,.24f,.26f));
                Box("Base de presentación",new Vector3(100,.1f,100),new Vector3(7,.2f,4),baseMaterial).transform.SetParent(preview.transform,true);
                LevelOneReferenceStyle.Capture(path,new Vector3(100,2.35f,96.0f),new Vector3(100,.72f,100),53);
            }
            finally{Object.DestroyImmediate(preview);}
        }
    }
}
