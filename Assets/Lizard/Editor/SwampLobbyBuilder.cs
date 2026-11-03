using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Lizards.EditorTools
{
    public static partial class SwampLobbyBuilder
    {
        const string ScenePath="Assets/Scenes/Lobby.unity", Dir="Assets/Lizard/SwampLobby";
        static Transform root;
        static Material grass,sand,rock,rockLight,trunk,leaf,leafLight,wood,dark,orange,blue,lime,green,belly,pink,white,gold,glow,water;
        static System.Random random;
        static float R(float a,float b)=>(float)(a+(b-a)*random.NextDouble());
        static Material Mat(string n,Color c,bool toon=true)
        {
            var path=Dir+"/Materials/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Lizards/SwampSurface"));m.SetColor("_BaseColor",c);AssetDatabase.CreateAsset(m,path);}return m;
        }
        static GameObject Shape(string n,PrimitiveType type,Vector3 p,Vector3 s,Material mat,Transform parent=null,bool collision=true)
        {
            var g=GameObject.CreatePrimitive(type);g.name=n;g.transform.SetParent(parent?parent:root);g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=mat;if(!collision)Object.DestroyImmediate(g.GetComponent<Collider>());else if(type==PrimitiveType.Cylinder&&s.y*2<Mathf.Max(s.x,s.z))FlatCylinderCollider(g);return g;
        }
        // Un cilindro aplastado con CapsuleCollider se vuelve una esfera invisible enorme: usa su malla convexa.
        public static void FlatCylinderCollider(GameObject g)
        {
            Object.DestroyImmediate(g.GetComponent<CapsuleCollider>());var mc=g.AddComponent<MeshCollider>();mc.sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;mc.convex=true;
        }
        static GameObject Box(string n,Vector3 p,Vector3 s,Material m,Transform t=null,bool c=true)=>Shape(n,PrimitiveType.Cube,p,s,m,t,c);
        static GameObject Sphere(string n,Vector3 p,Vector3 s,Material m,Transform t=null,bool c=true)=>Shape(n,PrimitiveType.Sphere,p,s,m,t,c);
        static Transform Group(string n){var g=new GameObject(n).transform;g.SetParent(root);return g;}
        static GameObject Tube(string n,Vector3 a,Vector3 b,float radius,Material m,Transform t=null,bool c=true)
        {
            var delta=b-a;var g=Shape(n,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(radius*2,delta.magnitude*.5f,radius*2),m,t,c);g.transform.rotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);return g;
        }
        static TextMesh Sign(string text,Vector3 p,float width=5,float height=1.7f,float yaw=0)
        {
            var group=Group("Cartel / "+text.Split('\n')[0]);group.position=p;
            Box("Tablero",p,new Vector3(width,height,.16f),dark,group);
            var g=new GameObject("Texto");g.transform.SetParent(group);g.transform.position=p+Vector3.back*.1f;g.transform.localScale=Vector3.one;
            var tm=g.AddComponent<TextMesh>();tm.fontSize=64;tm.characterSize=.045f;tm.anchor=TextAnchor.MiddleCenter;tm.alignment=TextAlignment.Center;tm.color=new Color(.96f,1,.88f);tm.text=text;
            group.rotation=Quaternion.Euler(0,yaw,0);return tm;
        }
        static GameObject MeshObject(string n,Vector3[] v,int[] triangles,Material mat,Color[] colors=null,bool collision=false)
        {
            var mesh=new Mesh{name=n};mesh.vertices=v;mesh.triangles=triangles;if(colors!=null)mesh.colors=colors;mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Dir+"/Meshes/"+n+"_"+DateTime.Now.Ticks+".asset";AssetDatabase.CreateAsset(mesh,path);
            var g=new GameObject(n);g.transform.SetParent(root);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=mat;if(collision)g.AddComponent<MeshCollider>().sharedMesh=mesh;return g;
        }
        static GameObject Ring(string n,Vector3 p,float radius,float tube,Material mat,Transform parent=null)
        {
            int steps=64,sides=10;var v=new Vector3[steps*sides];var t=new int[steps*sides*6];
            for(int j=0;j<steps;j++)for(int i=0;i<sides;i++)
            {float a=j*Mathf.PI*2/steps,b=i*Mathf.PI*2/sides;v[j*sides+i]=new Vector3(Mathf.Cos(a)*(radius+Mathf.Cos(b)*tube),Mathf.Sin(b)*tube,Mathf.Sin(a)*(radius+Mathf.Cos(b)*tube));}
            int k=0;for(int j=0;j<steps;j++)for(int i=0;i<sides;i++)
            {int a=j*sides+i,b=j*sides+(i+1)%sides,c=((j+1)%steps)*sides+i,d=((j+1)%steps)*sides+(i+1)%sides;t[k++]=a;t[k++]=b;t[k++]=c;t[k++]=b;t[k++]=d;t[k++]=c;}
            var g=MeshObject(n,v,t,mat);g.transform.position=p;if(parent)g.transform.SetParent(parent,true);return g;
        }
        static float Height(float x,float z)
        {
            float q=x*x/(34*34)+(z-3)*(z-3)/(39*39);
            float edge=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1.1f,q));
            float pond=x*x/(13*13)+(z-8)*(z-8)/(10*10);
            float depression=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.83f,1.12f,pond));
            return -edge*4-depression*2;
        }
        static void Terrain()
        {
            int nx=76,nz=84;var v=new Vector3[(nx+1)*(nz+1)];var colors=new Color[v.Length];var t=new int[nx*nz*6];
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++)
            {
                float px=x-nx*.5f,pz=z-nz*.5f+3;float y=Height(px,pz);int idx=z*(nx+1)+x;v[idx]=new Vector3(px,y,pz);
                bool path=Mathf.Abs(px)<3.2f&&pz<0 || Mathf.Abs(px)>12&&Mathf.Abs(px)<18&&pz>0&&pz<24 || pz>20&&pz<24;
                colors[idx]=(y<-.2f?new Color(.68f,.61f,.38f):path?new Color(.68f,.65f,.42f):Color.Lerp(new Color(.27f,.60f,.26f),new Color(.39f,.70f,.31f),Mathf.PerlinNoise(px*.18f,pz*.18f))).linear;
            }
            int k=0;for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x,b=a+1,c=a+nx+1,d=c+1;t[k++]=a;t[k++]=c;t[k++]=b;t[k++]=b;t[k++]=c;t[k++]=d;}
            var material=Mat("Terreno con color",Color.white);if(material.HasProperty("_VertexColorWeight"))material.SetFloat("_VertexColorWeight",1);
            MeshObject("Isla y orilla",v,t,material,colors,true);
        }
        static void Willow(Vector3 p,float scale=1)
        {
            var tree=Group("Sauce del pantano");
            Tube("Tronco",p,p+new Vector3(.4f,5*scale,0),.4f*scale,trunk,tree);
            for(int i=0;i<5;i++)
            {
                float a=i*Mathf.PI*2/5;Vector3 canopy=p+new Vector3(Mathf.Cos(a)*1.5f,5.2f,Mathf.Sin(a)*1.5f)*scale;
                Sphere("Copa",canopy,new Vector3(4,2.5f,3.8f)*scale,i%2==0?leaf:leafLight,tree,false);
                for(int j=0;j<5;j++)
                {var from=canopy+new Vector3(R(-1.5f,1.5f),-.3f,R(-1.5f,1.5f))*scale;Tube("Follaje colgante",from,from+Vector3.down*R(1.2f,3)*scale,.075f*scale,leaf,tree,false);}
            }
        }
        static GameObject Prop(string n,Vector3 p,float scale=1,bool barrel=false)
        {
            var g=Shape(n,barrel?PrimitiveType.Cylinder:PrimitiveType.Cube,p,barrel?new Vector3(.9f,.6f,.9f)*scale:Vector3.one*scale,barrel?orange:wood);var rb=g.AddComponent<Rigidbody>();rb.mass=3;rb.interpolation=RigidbodyInterpolation.Interpolate;g.AddComponent<TongueTarget>().kind=TongueTargetKind.Object;
            for(int i=-1;i<=1;i+=2)
            {var band=barrel?Shape("Banda",PrimitiveType.Cylinder,p+Vector3.up*.4f*i*scale,new Vector3(.94f,.05f,.94f)*scale,dark,g.transform,false):Box("Tabla",p+new Vector3(0,.30f*i,-.51f)*scale,new Vector3(1,.12f,.05f)*scale,orange,g.transform,false);}
            return g;
        }
        static void Raft()
        {
            var g=Group("Balsa de expedición");g.position=new Vector3(0,0,8);
            Shape("Suelo de la balsa",PrimitiveType.Cylinder,new Vector3(0,-.05f,8),new Vector3(9,.18f,9),dark,g);
            Ring("Cámara azul inferior",new Vector3(0,.05f,8),4.6f,.45f,blue,g);
            Ring("Borde naranja",new Vector3(0,.65f,8),4.6f,.42f,orange,g);
            for(int i=0;i<10;i++)
            {float a=i*Mathf.PI*2/10;var pos=new Vector3(Mathf.Cos(a)*4.65f,.72f,8+Mathf.Sin(a)*4.65f);var handle=Box("Asa",pos,new Vector3(.55f,.09f,.15f),glow,g,false);handle.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}
            for(int i=0;i<8;i++)Box("Tablón del embarcadero",new Vector3(0,.08f,-2.3f+i*.68f),new Vector3(3.5f,.25f,.58f),wood);
            foreach(float x in new[]{-2.1f,2.1f})
            {Tube("Poste muelle",new Vector3(x,-1,-2),new Vector3(x,1,-2),.14f,trunk);Tube("Amarre",new Vector3(x,.5f,-2),new Vector3(x,.4f,3.5f),.045f,glow,null,false);}
            // Low steps permit entry and exit without relying on a jump.
            Box("Escalón de acceso",new Vector3(0,.2f,3.4f),new Vector3(3,.4f,1.2f),wood);
        }
        static Transform King()
        {
            var k=Group("Guardián del pantano");k.position=new Vector3(0,0,27);
            Shape("Podio de piedra",PrimitiveType.Cylinder,new Vector3(0,.4f,27),new Vector3(13,.4f,9),rock);
            Sphere("Cuerpo del guardián",new Vector3(0,4.2f,28),new Vector3(9,8,6),green,k);
            Sphere("Barriga",new Vector3(0,3.6f,25.8f),new Vector3(6.8f,5.3f,2),belly,k,false);
            Sphere("Cabeza",new Vector3(0,7.3f,26),new Vector3(8,3.7f,5),green,k);
            Sphere("Sonrisa",new Vector3(0,6.7f,23.65f),new Vector3(5.8f,2.2f,.25f),pink,k,false);
            for(int i=0;i<7;i++)Sphere("Diente",new Vector3(-2.2f+i*.73f,6.95f+(i%2)*.15f,23.45f),new Vector3(.5f,.62f,.18f),white,k,false);
            for(int i=0;i<4;i++)
            {float x=-2.4f+i*1.6f;float y=i==1||i==2?9:8.6f;Sphere("Ojo del guardián",new Vector3(x,y,24.75f),new Vector3(1.5f,1.5f,1.2f),white,k,false);Sphere("Pupila del guardián",new Vector3(x,y,24.15f),new Vector3(.5f,.8f,.15f),dark,k,false);}
            foreach(float s in new[]{-1f,1f})
            {Sphere("Brazo",new Vector3(s*4.4f,3.8f,26),new Vector3(3,2.8f,3.6f),green,k);Sphere("Mano",new Vector3(s*5,2.8f,24.6f),new Vector3(2.8f,1.1f,2.3f),belly,k);Sphere("Pie",new Vector3(s*3.2f,1,24.9f),new Vector3(3,1.2f,3),green,k);}
            // Crown mesh with alternating peaks.
            int n=12;var v=new Vector3[n*2];var t=new int[n*6];for(int i=0;i<n;i++){float a=i*2*Mathf.PI/n;v[i]=new Vector3(Mathf.Cos(a)*1.4f,9.3f,27+Mathf.Sin(a)*1.4f);v[n+i]=new Vector3(Mathf.Cos(a)*1.4f,10.2f+(i%2==0?.9f:0),27+Mathf.Sin(a)*1.4f);int j=(i+1)%n;t[i*6]=i;t[i*6+1]=n+i;t[i*6+2]=j;t[i*6+3]=j;t[i*6+4]=n+i;t[i*6+5]=n+j;}
            MeshObject("Corona",v,t,gold).transform.SetParent(k,true);
            return k;
        }
        static void Shop()
        {
            var g=Group("Bazar del refugio");
            Box("Tarima",new Vector3(-19,.2f,-5),new Vector3(9,.4f,7),wood,g);
            foreach(float x in new[]{-22.7f,-15.3f})foreach(float z in new[]{-7.8f,-2.2f})Tube("Poste",new Vector3(x,.4f,z),new Vector3(x,4.6f,z),.15f,trunk,g);
            Box("Mostrador",new Vector3(-19,1.2f,-7.2f),new Vector3(7,.35f,1.3f),wood,g);
            for(int i=0;i<8;i++){var panel=Box("Toldo a rayas",new Vector3(-22.5f+i,4.5f,-5),new Vector3(1, .16f,7),i%2==0?orange:gold,g);panel.transform.rotation=Quaternion.Euler(-8,0,0);}
            Sign("EL BAZAR\nSombreros · [E] probar",new Vector3(-19,3,-8),6,1.3f);
            for(int i=0;i<3;i++){Shape("Sombrero en exposición",PrimitiveType.Cylinder,new Vector3(-21+i*2,1.6f,-7.2f),new Vector3(.8f,.1f,.8f),i==0?gold:i==1?blue:green,g,false);}
            Prop("Caja del bazar",new Vector3(-23,.6f,-9),1.1f);Prop("Barril del bazar",new Vector3(-14,.6f,-4),1,true);
        }
        [MenuItem("Lizards/Lobby/Construir refugio del pantano")]
        public static void Build()
        {
            if(Application.isPlaying)throw new Exception("Salir de Play antes de construir.");
            var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.isDirty)EditorSceneManager.SaveScene(current);
            Directory.CreateDirectory(Dir+"/Materials");Directory.CreateDirectory(Dir+"/Meshes");Directory.CreateDirectory("Backups/Lobby");Directory.CreateDirectory("Captures");AssetDatabase.Refresh();
            File.Copy(ScenePath,"Backups/Lobby/Lobby_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".unity");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);var prior=scene.GetRootGameObjects();var archived=new GameObject("__Original_Lobby");foreach(var g in prior)g.transform.SetParent(archived.transform);archived.SetActive(false);
            root=new GameObject("LOBBY / El refugio del pantano").transform;random=new System.Random(32);
            grass=Mat("Hierba",new Color(.37f,.69f,.3f));sand=Mat("Arena",new Color(.72f,.67f,.44f));rock=Mat("Roca",new Color(.39f,.39f,.45f));rockLight=Mat("Roca clara",new Color(.66f,.61f,.51f));trunk=Mat("Tronco",new Color(.33f,.26f,.15f));leaf=Mat("Sauce",new Color(.24f,.53f,.28f));leafLight=Mat("Sauce luz",new Color(.47f,.7f,.32f));wood=Mat("Madera",new Color(.54f,.32f,.17f));dark=Mat("Oscuro",new Color(.07f,.16f,.16f));orange=Mat("Naranja",new Color(1,.42f,.055f));blue=Mat("Azul balsa",new Color(.08f,.39f,.58f));lime=Mat("Lima",new Color(.63f,1,.08f));green=Mat("Rey piel",new Color(.34f,.7f,.13f));belly=Mat("Rey barriga",new Color(.65f,.85f,.32f));pink=Mat("Boca",new Color(.46f,.18f,.50f));white=Mat("Marfil",new Color(.93f,.94f,.83f));gold=Mat("Oro",new Color(1,.76f,.1f));glow=Mat("Turquesa",new Color(.3f,1,.83f));
            var waterPath=Dir+"/Materials/Agua animada.mat";water=AssetDatabase.LoadAssetAtPath<Material>(waterPath);if(!water){water=new Material(Shader.Find("Lizards/SwampWater"));AssetDatabase.CreateAsset(water,waterPath);}
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.74f,.86f,.88f);RenderSettings.ambientEquatorColor=new Color(.57f,.67f,.52f);RenderSettings.ambientGroundColor=new Color(.29f,.37f,.30f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.52f,.76f,.76f);RenderSettings.fogStartDistance=65;RenderSettings.fogEndDistance=170;
            var sun=new GameObject("Sol de la tarde").AddComponent<Light>();sun.transform.SetParent(root);sun.type=LightType.Directional;sun.intensity=1.8f;sun.shadows=LightShadows.Soft;sun.color=new Color(1,.94f,.78f);sun.transform.rotation=Quaternion.Euler(45,-35,0);
            Terrain();Box("Agua del pantano",new Vector3(0,-.65f,0),new Vector3(180,.05f,180),water,null,false);
            Raft();var king=King();Shop();
            // Large rocks frame the playable island without enclosing it in a box.
            for(int i=0;i<30;i++)
            {float a=i*Mathf.PI*2/30;float x=Mathf.Cos(a)*R(29,35),z=3+Mathf.Sin(a)*R(33,39);Sphere("Acantilado erosionado",new Vector3(x,R(0,2),z),new Vector3(R(5,9),R(5,11),R(4,8)),i%3==0?rockLight:rock);}
            foreach(var p in new[]{new Vector3(-25,0,12),new Vector3(25,0,16),new Vector3(-16,0,-23),new Vector3(19,0,-22),new Vector3(-16,0,29),new Vector3(16,0,33)})Willow(p,R(.8f,1.25f));
            for(int i=0;i<42;i++)
            {float x=R(-28,28),z=R(-27,32);if(Height(x,z)<-.2f||Mathf.Abs(x)<8||Vector3.Distance(new Vector3(x,0,z),new Vector3(-19,0,-5))<7)continue;Sphere("Matorral",new Vector3(x,.35f,z),new Vector3(R(.7f,1.6f),R(.6f,1.2f),R(.8f,1.5f)),i%2==0?leaf:leafLight,null,false);}
            for(int i=0;i<25;i++)
            {float a=R(0,Mathf.PI*2);float x=Mathf.Cos(a)*R(7,12),z=8+Mathf.Sin(a)*R(6,8);if(Mathf.Abs(x)<5)continue;Shape("Nenúfar",PrimitiveType.Cylinder,new Vector3(x,-.58f,z),new Vector3(R(.6f,1.1f),.025f,R(.7f,1.1f)),leafLight,null,false);}
            for(int i=0;i<35;i++)
            {float a=R(0,Mathf.PI*2);float x=Mathf.Cos(a)*R(14,16),z=8+Mathf.Sin(a)*R(10,12);if(Mathf.Abs(x)<5)continue;Tube("Junco",new Vector3(x,-.1f,z),new Vector3(x,.8f+R(0,.6f),z),.045f,leaf,null,false);}
            // Social areas and portal façade.
            var phone=Group("Teléfono de salas");phone.position=new Vector3(19,0,-6);
            Box("Base teléfono",new Vector3(19,.15f,-6),new Vector3(2.6f,.3f,2.6f),rock,phone);Box("Cabina",new Vector3(19,1.8f,-5.5f),new Vector3(2,3.5f,.4f),blue,phone);Box("Visera",new Vector3(19,3.65f,-6),new Vector3(2.6f,.3f,2),orange,phone);Box("Terminal",new Vector3(19,1.8f,-5.8f),new Vector3(.9f,1.3f,.3f),dark,phone);Tube("Auricular",new Vector3(18.8f,1.5f,-6.1f),new Vector3(18.8f,2.2f,-6.1f),.14f,gold,phone,false);
            Sign("SALA ONLINE\n[E] crear / unirse",new Vector3(19,3,-6.4f),4,1.1f);
            var board=Sign("EL REFUGIO",new Vector3(20,2.6f,5),7,3);Tube("Poste panel",new Vector3(17,0,5),new Vector3(17,2,5),.15f,trunk);Tube("Poste panel",new Vector3(23,0,5),new Vector3(23,2,5),.15f,trunk);
            var portal=Ring("Acceso a las alcantarillas",new Vector3(18,3.2f,24),3.4f,.65f,lime);portal.transform.rotation=Quaternion.Euler(90,0,0);Shape("Interior del conducto",PrimitiveType.Cylinder,new Vector3(18,3.2f,24.25f),new Vector3(6.5f,.2f,6.5f),dark).transform.rotation=Quaternion.Euler(90,0,0);
            Sign("ESTACIÓN 01\nSalida desde la balsa",new Vector3(18,7.3f,24),6,1.5f);
            for(int i=0;i<6;i++)Prop("Suministro manipulable",new Vector3(8+i%3*1.5f,.6f,-14+i/3*1.8f),1,i%2==0);
            for(int i=0;i<6;i++){var f=FoodFactory.CreateFruit(new Vector3(-8+i*.6f,.3f,-13),1,.18f,orange);f.transform.SetParent(root);}
            var supply=new GameObject("Fruta renovable");supply.transform.SetParent(root);supply.transform.position=new Vector3(-10,.1f,-11);supply.AddComponent<FoodSpawner>();
            var recovery=Shape("Fuente de recuperación",PrimitiveType.Cylinder,new Vector3(8,.12f,-20),new Vector3(2,.12f,2),glow);var rc=recovery.AddComponent<SphereCollider>();rc.isTrigger=true;rc.radius=1;recovery.AddComponent<RescuePoint>();
            Sign("LIZARDS TO THE RESCUE\nEL REFUGIO DEL PANTANO",new Vector3(0,3.2f,-21),11,2);
            Sign("EL GUARDIÁN\n[E] hablar",new Vector3(-7,1.5f,21),4,1.3f);
            var departure=Sign("EMBARCADERO",new Vector3(-6,2,-1),6,2);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Prefabs/Sunny.prefab");if(!prefab)throw new Exception("Falta Sunny.prefab");
            var player=(GameObject)PrefabUtility.InstantiatePrefab(prefab);player.name="Jugador / Sunny";player.transform.position=new Vector3(0,.2f,-14);player.GetComponent<LizardInput>().controlled=true;var pplayer=player.GetComponent<LizardPlayer>();pplayer.displayName="Sunny";
            var cameraGo=new GameObject("Cámara del refugio");cameraGo.tag="MainCamera";var camera=cameraGo.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=240;camera.backgroundColor=new Color(.60f,.83f,.87f);camera.clearFlags=CameraClearFlags.SolidColor;cameraGo.AddComponent<AudioListener>();var rig=cameraGo.AddComponent<LizardCameraRig>();rig.target=pplayer;cameraGo.transform.position=new Vector3(0,3,-19);cameraGo.transform.LookAt(new Vector3(0,1,-13));
            var system=new GameObject("Sistemas del refugio");var hud=system.AddComponent<LizardHUD>();hud.rig=rig;hud.showDebug=false;var rooms=system.AddComponent<LizardLobby>();rooms.showOnStart=false;rooms.spawnPoint=new Vector3(0,.2f,-14);rooms.maxPlayers=8;var net=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Prefabs/SunnyRed.prefab");if(net)rooms.playerPrefab=net.GetComponent<Fusion.NetworkObject>();
            var hub=system.AddComponent<SwampLobbyHub>();hub.roomMenu=rooms;hub.boat=root.Find("Balsa de expedición");hub.telephone=phone;hub.wardrobe=new GameObject("Zona de vestuario").transform;hub.wardrobe.SetParent(root);hub.wardrobe.position=new Vector3(-19,0,-8.5f);hub.king=new GameObject("Zona de conversación").transform;hub.king.SetParent(root);hub.king.position=new Vector3(0,0,21);hub.kingBelly=king.Find("Barriga");hub.departureBoard=departure;hub.playersBoard=board;
            var scenes=EditorBuildSettings.scenes.ToList();scenes.RemoveAll(s=>s.path==ScenePath);scenes.Insert(0,new EditorBuildSettingsScene(ScenePath,true));if(!scenes.Any(s=>s.path=="Assets/Scenes/LEVEL1.unity"))scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/LEVEL1.unity",true));EditorBuildSettings.scenes=scenes.ToArray();
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();SceneView.lastActiveSceneView?.LookAt(new Vector3(0,0,7),Quaternion.Euler(42,0,0),58);
            Debug.Log("[Lobby] Refugio del pantano construido en Lobby. Teléfono, vestuario y balsa interactivos.");
        }
        public static string Capture()
        {
            var g=new GameObject("Vista temporal lobby");var c=g.AddComponent<Camera>();c.transform.position=new Vector3(49,49,-57);c.transform.LookAt(new Vector3(0,2,7));c.orthographic=true;c.orthographicSize=43;c.farClipPlane=250;c.backgroundColor=new Color(.56f,.79f,.80f);c.clearFlags=CameraClearFlags.SolidColor;
            var rt=new RenderTexture(1600,1200,24);c.targetTexture=rt;RenderPipeline.SubmitRenderRequest(c,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1600,1200,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1600,1200),0,0);t.Apply();string path=Path.GetFullPath("Captures/Lobby_Overview.png");File.WriteAllBytes(path,t.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;Object.DestroyImmediate(t);Object.DestroyImmediate(rt);Object.DestroyImmediate(g);return path;
        }
    }
}
