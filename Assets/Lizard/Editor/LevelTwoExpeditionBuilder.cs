using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace Lizards.EditorTools
{
    public static class LevelTwoExpeditionBuilder
    {
        const string Dir="Assets/Lizard/LevelTwo/Expedition";
        const string ScenePath="Assets/Scenes/LEVEL2.unity";
        static Transform root,sceneRoot;
        static Material violet,stone,stone2,trim,mint,lime,yellow,red,orange,dark,wood,steel,white,vertex,blue,water;
        static Mesh block;
        static string meshDir;
        static int serial,foodSerial;
        static System.Random random;
        static FoodItem[] foods;
        static TMP_FontAsset font;
        static readonly List<ExpeditionSupplyCrate> caches=new();
        static float R(float min,float max)=>min+(max-min)*(float)random.NextDouble();
        struct Gap
        {
            public char side;public float center,width;
            public Gap(char s,float c,float w=6){side=s;center=c;width=w;}
        }
        static Material Mat(string name,Color c,float vertexWeight=0)
        {
            string path=Dir+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Lizards/Toon"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",c);m.SetFloat("_OutlineWidth",0);m.SetFloat("_SpecSize",0);
            m.SetFloat("_ShadowThreshold",.08f);m.SetFloat("_ShadowSoftness",.28f);
            m.SetColor("_ShadowColor",new Color(.68f,.71f,.83f));m.SetColor("_RimColor",new Color(1,1,1,0));
            m.SetFloat("_VertexColorWeight",vertexWeight);EditorUtility.SetDirty(m);return m;
        }
        static Transform Group(string name)
        {var t=new GameObject(name).transform;t.SetParent(root,false);return t;}
        static GameObject Shape(string name,PrimitiveType type,Vector3 p,Vector3 size,Material material,bool collision=false,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent?parent:root,false);go.transform.position=p;go.transform.localScale=size;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            if(type==PrimitiveType.Cube&&block)go.GetComponent<MeshFilter>().sharedMesh=block;
            if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());
            else if(type==PrimitiveType.Cylinder)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
            }
            return go;
        }
        static GameObject Box(string n,Vector3 p,Vector3 s,Material m,bool col=false,Transform parent=null)=>Shape(n,PrimitiveType.Cube,p,s,m,col,parent);
        static GameObject Tube(string n,Vector3 a,Vector3 b,float radius,Material m,bool col=false,Transform parent=null)
        {
            var go=Shape(n,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(radius*2,(b-a).magnitude*.5f,radius*2),m,col,parent);
            go.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);return go;
        }
        static GameObject MeshObject(string name,List<Vector3> vertices,List<int> triangles,Material material,bool collider=false)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,meshDir+"/Mesh_"+(serial++)+".asset");
            var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
            if(collider)go.AddComponent<MeshCollider>().sharedMesh=mesh;return go;
        }
        static GameObject Ring(string name,Vector3 p,float radius,float thickness,Material material,Quaternion rotation,bool collider=false)
        {
            const int slices=40,sides=8;var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=slices;i++)for(int j=0;j<sides;j++)
            {float a=i*Mathf.PI*2/slices,b=j*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*(radius+Mathf.Cos(b)*thickness),Mathf.Sin(a)*(radius+Mathf.Cos(b)*thickness),Mathf.Sin(b)*thickness));}
            for(int i=0;i<slices;i++)for(int j=0;j<sides;j++){int a=i*sides+j,b=i*sides+(j+1)%sides,c=a+sides,d=b+sides;t.AddRange(new[]{a,c,b,b,c,d});}
            var go=MeshObject(name,v,t,material,collider);go.transform.SetPositionAndRotation(p,rotation);return go;
        }
        static void Portal(Vector3 p,float width,Quaternion rotation)
        {
            float radius=width*.5f-.20f,spring=1.35f;var path=new List<Vector3>{new Vector3(-radius,0,0)};
            for(int i=0;i<=28;i++){float a=Mathf.PI-i*Mathf.PI/28;path.Add(new Vector3(Mathf.Cos(a)*radius,spring+Mathf.Sin(a)*radius,0));}
            path.Add(new Vector3(radius,0,0));var v=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<path.Count;i++)
            {
                var direction=(path[Mathf.Min(i+1,path.Count-1)]-path[Mathf.Max(0,i-1)]).normalized;
                var side=Vector3.Cross(direction,Vector3.forward).normalized;
                for(int j=0;j<8;j++){float a=j*Mathf.PI/4;v.Add(path[i]+side*(Mathf.Cos(a)*.27f)+Vector3.forward*(Mathf.Sin(a)*.26f));}
            }
            for(int i=0;i<path.Count-1;i++)for(int j=0;j<8;j++){int a=i*8+j,b=i*8+(j+1)%8,c=a+8,d=b+8;t.AddRange(new[]{a,c,b,b,c,d});}
            var go=MeshObject("Boca de paso verde lima",v,t,lime,true);go.transform.SetPositionAndRotation(p,rotation);go.AddComponent<TongueTarget>().kind=TongueTargetKind.Surface;
        }
        static void Floor(Rect rect,float y=0,string name="Suelo")
        {
            var slab=Box(name,new Vector3(rect.center.x,y-.25f,rect.center.y),new Vector3(rect.width,.5f,rect.height),stone,true);
            slab.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            int nx=Mathf.CeilToInt(rect.width/2.7f),nz=Mathf.CeilToInt(rect.height/2.7f);
            float sx=rect.width/nx,sz=rect.height/nz;
            for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
            {
                var tile=Box("Losa biselada",new Vector3(rect.xMin+(x+.5f)*sx,y+.012f,rect.yMin+(z+.5f)*sz),new Vector3(sx-.035f,.025f,sz-.035f),(x+z)%3==0?stone2:stone);
                tile.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        static void Wall(bool alongX,float fixedCoordinate,float from,float to,float y,float height,string name)
        {
            if(to-from<.05f)return;
            Vector3 p=alongX?new Vector3((from+to)*.5f,y+height*.5f,fixedCoordinate):new Vector3(fixedCoordinate,y+height*.5f,(from+to)*.5f);
            Vector3 size=alongX?new Vector3(to-from,height,.50f):new Vector3(.50f,height,to-from);
            var wall=Box(name,p,size,violet,true);wall.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            if(height<3)return;
            for(float u=from+.8f;u<to-.4f;u+=2.3f)
                for(int row=0;row<4;row++)
                    if(random.NextDouble()>.35)
                    {
                        float h=y+.8f+row*1.45f;Vector3 c=alongX?new Vector3(u,h,fixedCoordinate):new Vector3(fixedCoordinate,h,u);
                        var brick=Box("Relieve de mampostería",c,alongX?new Vector3(R(.8f,1.7f),R(.24f,.38f),.57f):new Vector3(.57f,R(.24f,.38f),R(.8f,1.7f)),trim);
                        brick.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                    }
        }
        static Transform Room(string name,Rect rect,float y,Gap[] gaps,float height=7.5f)
        {
            var parent=root;root=Group(name);var result=root;
            foreach(char side in new[]{'N','S','W','E'})
            {
                bool alongX=side=='N'||side=='S';float fixedC=side=='N'?rect.yMax:side=='S'?rect.yMin:side=='W'?rect.xMin:rect.xMax;
                float min=alongX?rect.xMin:rect.yMin,max=alongX?rect.xMax:rect.yMax,cursor=min;
                foreach(var gap in gaps.Where(g=>g.side==side).OrderBy(g=>g.center))
                {
                    float a=Mathf.Max(min,gap.center-gap.width*.5f),b=Mathf.Min(max,gap.center+gap.width*.5f);
                    Wall(alongX,fixedC,cursor,a,y,height,"Muro "+side);Wall(alongX,fixedC,a,b,y+5.6f,height-5.6f,"Dintel "+side);
                    cursor=b;
                    // Only north and east doorways draw frames; south/west frames are added when needed below.
                    if(side=='N'||side=='E')Portal(alongX?new Vector3(gap.center,y,fixedC):new Vector3(fixedC,y,gap.center),gap.width,alongX?Quaternion.identity:Quaternion.Euler(0,90,0));
                }
                Wall(alongX,fixedC,cursor,max,y,height,"Muro "+side);
            }
            // Interior-facing barrel vault, orient its axis along the longest room dimension.
            bool longitudinal=rect.height>=rect.width*.8f;float half=(longitudinal?rect.width:rect.height)*.5f;
            float length=longitudinal?rect.height:rect.width;var vertices=new List<Vector3>();var indices=new List<int>();
            for(int i=0;i<=24;i++)
            {
                float a=i*Mathf.PI/24,cross=Mathf.Cos(a)*half,ceiling=y+height+Mathf.Sin(a)*Mathf.Min(2.2f,half*.23f);
                vertices.Add(longitudinal?new Vector3(rect.center.x+cross,ceiling,rect.yMin):new Vector3(rect.xMin,ceiling,rect.center.y+cross));
                vertices.Add(longitudinal?new Vector3(rect.center.x+cross,ceiling,rect.yMax):new Vector3(rect.xMax,ceiling,rect.center.y+cross));
            }
            for(int i=0;i<24;i++){int a=i*2;indices.AddRange(longitudinal?new[]{a,a+1,a+2,a+1,a+3,a+2}:new[]{a,a+2,a+1,a+1,a+2,a+3});}
            MeshObject("Bóveda de "+name,vertices,indices,violet).GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            for(int s=-1;s<=1;s+=2)
            {
                if(longitudinal)Pipe(new Vector3(rect.center.x+s*(half-.65f),y+5.2f,rect.yMin+.5f),new Vector3(rect.center.x+s*(half-.65f),y+5.2f,rect.yMax-.5f),.22f);
                else Pipe(new Vector3(rect.xMin+.5f,y+5.2f,rect.center.y+s*(half-.65f)),new Vector3(rect.xMax-.5f,y+5.2f,rect.center.y+s*(half-.65f)),.22f);
            }
            root=parent;return result;
        }
        static void Pipe(Vector3 a,Vector3 b,float radius,bool anchor=false)
        {
            var go=Tube("Conducto menta",a,b,radius,mint,anchor);if(anchor)go.AddComponent<TongueTarget>().kind=TongueTargetKind.Surface;
            int n=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/3.5f));
            for(int i=0;i<=n;i++){var p=Vector3.Lerp(a,b,i/(float)n);var d=(b-a).normalized;Tube("Unión de conducto",p-d*.07f,p+d*.07f,radius*1.13f,mint);}
        }
        static void Water(Rect rect,float surface=-.35f)
        {
            Box("Fondo sumergido",new Vector3(rect.center.x,surface-2.15f,rect.center.y),new Vector3(rect.width,.3f,rect.height),dark,true);
            var plane=Box("Agua turquesa",new Vector3(rect.center.x,surface,rect.center.y),new Vector3(rect.width,.025f,rect.height),water);plane.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            float width=Mathf.Min(rect.width,rect.height),length=Mathf.Max(rect.width,rect.height);bool alongX=rect.width>rect.height;
            int count=Mathf.Max(2,Mathf.CeilToInt(length/(width*.55f)));
            for(int i=0;i<count;i++)
            {
                float u=count==1?0:Mathf.Lerp(-(length-width)*.5f,(length-width)*.5f,i/(float)(count-1));
                var go=new GameObject("Zona de nado");go.transform.SetParent(root,false);go.transform.position=new Vector3(rect.center.x+(alongX?u:0),surface,rect.center.y+(alongX?0:u));
                var zone=go.AddComponent<SwimZone>();zone.radius=width*.51f;zone.surfaceY=0;zone.depth=2.6f;
            }
            foreach(float z in new[]{rect.yMin,rect.yMax})Box("Espuma de borde",new Vector3(rect.center.x,surface+.025f,z),new Vector3(rect.width,.018f,.045f),white);
            foreach(float x in new[]{rect.xMin,rect.xMax})Box("Espuma de borde",new Vector3(x,surface+.025f,rect.center.y),new Vector3(.045f,.018f,rect.height),white);
        }
        static void Ramp(string name,Vector3 start,Vector3 end,float width)
        {
            var direction=end-start;var horizontal=new Vector3(direction.x,0,direction.z);float length=horizontal.magnitude,h=direction.y;
            var vertices=new List<Vector3>{new Vector3(-width/2,0,0),new Vector3(width/2,0,0),new Vector3(-width/2,0,length),new Vector3(width/2,0,length),new Vector3(-width/2,h,length),new Vector3(width/2,h,length)};
            var tri=new List<int>{0,1,2,1,3,2,0,4,1,1,4,5,2,3,4,3,5,4,1,5,3,0,2,4};
            var go=MeshObject(name,vertices,tri,stone,true);go.transform.SetPositionAndRotation(start,Quaternion.LookRotation(horizontal));
        }
        static GameObject Model(string name,string asset,Vector3 position,Vector3 size,Transform parent=null)
        {
            var owner=new GameObject(name);owner.transform.SetParent(parent?parent:root,false);owner.transform.position=position;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Art/StationStyle/Models/"+asset+".fbx");
            var visual=Object.Instantiate(prefab,owner.transform);visual.name="Modelo";visual.transform.localPosition=Vector3.zero;
            foreach(var r in visual.GetComponentsInChildren<Renderer>())r.sharedMaterial=vertex;
            var b=FoodCollectionBuilder.VisualBounds(visual);visual.transform.localScale=Vector3.Scale(visual.transform.localScale,new Vector3(size.x/b.size.x,size.y/b.size.y,size.z/b.size.z));
            b=FoodCollectionBuilder.VisualBounds(visual);visual.transform.position+=position-b.center;return owner;
        }
        static GameObject PhysicsProp(string asset,Vector3 p,Vector3 size,float mass=3)
        {
            var go=Model(asset,asset,p,size);var c=go.AddComponent<BoxCollider>();c.size=size;
            var rb=go.AddComponent<Rigidbody>();rb.mass=mass;rb.linearDamping=.35f;rb.angularDamping=.6f;rb.interpolation=RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<TongueTarget>().kind=TongueTargetKind.Object;return go;
        }
        static void MakeFoodPrefabs()
        {
            string[] ids={"Apple","Banana","Corn","Broccoli","Avocado","Pepper"};float[] sizes={.34f,.47f,.4f,.43f,.4f,.39f};foods=new FoodItem[6];
            for(int i=0;i<6;i++)
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(FoodCollectionBuilder.Folder+"/Prefabs/"+ids[i]+".prefab");
                var go=Object.Instantiate(source);go.name=ids[i];var b=FoodCollectionBuilder.VisualBounds(go);go.transform.localScale*=sizes[i]/Mathf.Max(b.size.x,b.size.y,b.size.z);
                foreach(var r in go.GetComponentsInChildren<Renderer>())
                {
                    var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/Lizard/LevelOne/ReferenceStyle/Materials/Comida "+ids[i]+".mat");if(m)r.sharedMaterial=m;
                }
                var food=go.GetComponent<FoodItem>();food.size=sizes[i]*.5f;food.nutrition=1;
                string path=Dir+"/FoodPrefabs/"+ids[i]+".prefab";foods[i]=PrefabUtility.SaveAsPrefabAsset(go,path).GetComponent<FoodItem>();Object.DestroyImmediate(go);
            }
        }
        static FoodItem Food(Vector3 p,int index=-1,float value=1,float scale=1)
        {
            int kind=index<0?foodSerial++%foods.Length:index;var go=(GameObject)PrefabUtility.InstantiatePrefab(foods[kind].gameObject);
            go.transform.SetParent(root,true);go.transform.position=p;go.transform.rotation=Quaternion.Euler(0,R(0,360),0);go.transform.localScale*=scale;
            var b=FoodCollectionBuilder.VisualBounds(go);go.transform.position+=Vector3.up*(p.y+.025f-b.min.y);
            var food=go.GetComponent<FoodItem>();food.nutrition=value;food.size=foods[kind].size*scale;
            if(scale>1.2f)food.GetComponent<Rigidbody>().mass=3;
            go.name=(scale>1.2f?"Provisión grande · arrastrar":"Comida · recoger / tragar")+" · "+go.GetComponent<FoodCollectionIdentity>().modelId;
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(food);
            PrefabUtility.RecordPrefabInstancePropertyModifications(food.GetComponent<Rigidbody>());
            return food;
        }
        static void Cluster(Vector3 p,int count,float step=.75f)
        {
            for(int i=0;i<count;i++)Food(p+new Vector3((i%3)*step+R(-.12f,.12f),0,(i/3)*step+R(-.1f,.1f)));
        }
        static ExpeditionSupplyCrate Cache(Vector3 p,int count=3)
        {
            var go=PhysicsProp("SupplyCrate",p,new Vector3(1.2f,1.2f,1.2f));go.name="Caja de provisiones · abrir [E]";
            var cache=go.AddComponent<ExpeditionSupplyCrate>();cache.closedVisual=go.transform.Find("Modelo").gameObject;cache.closedCollider=go.GetComponent<Collider>();cache.count=count;
            cache.foods=Enumerable.Range(0,foods.Length).Select(i=>foods[(i+caches.Count)%foods.Length]).ToArray();
            var open=new GameObject("Caja abierta");open.transform.SetParent(go.transform,false);
            Box("Fondo de la caja",p+Vector3.down*.5f,new Vector3(1.15f,.10f,1.15f),wood,true,open.transform);
            for(int side=-1;side<=1;side+=2)
                for(int row=0;row<3;row++)
                {
                    float y=-.35f+row*.27f;
                    Box("Tablón lateral",p+new Vector3(side*.55f,y,0),new Vector3(.09f,.23f,1.15f),wood,true,open.transform);
                    Box("Tablón frontal",p+new Vector3(0,y,side*.55f),new Vector3(1.05f,.23f,.09f),wood,true,open.transform);
                }
            cache.openVisual=open;open.SetActive(false);caches.Add(cache);return cache;
        }
        static void Anchor(Vector3 p)
        {
            var go=Ring("Enganche de lengua",p,.40f,.08f,yellow,Quaternion.identity);
            go.AddComponent<SphereCollider>().radius=.5f;go.AddComponent<TongueTarget>().kind=TongueTargetKind.GrapplePoint;
            Tube("Suspensión del enganche",p+Vector3.up*.4f,p+Vector3.up*2,.045f,steel);
        }
        static void Recovery(Vector3 p)
        {
            var go=Shape("Punto de recuperación",PrimitiveType.Cylinder,p,new Vector3(1.5f,.08f,1.5f),mint,true);
            var sphere=go.AddComponent<SphereCollider>();sphere.radius=.8f;sphere.isTrigger=true;go.AddComponent<RescuePoint>();
        }
        static TMP_Text Sign(string text,Vector3 p,float width=3,float yaw=0)
        {
            var owner=Group("Señal · "+text.Split('\n')[0]);owner.position=p;owner.rotation=Quaternion.Euler(0,yaw,0);
            var back=Box("Marco rojo",p,new Vector3(width+.14f,1.03f,.12f),red,false,owner);
            back.transform.rotation=owner.rotation;
            var face=Box("Pizarra",p+owner.forward*-.07f,new Vector3(width,.89f,.04f),dark,false,owner);face.transform.rotation=owner.rotation;
            var label=new GameObject("Texto").AddComponent<TextMeshPro>();label.transform.SetParent(owner,false);label.transform.localPosition=new Vector3(0,0,-.10f);
            label.rectTransform.sizeDelta=new Vector2(width-.15f,.80f);label.font=font;label.text=text;label.alignment=TextAlignmentOptions.Center;label.enableAutoSizing=true;label.fontSizeMin=.4f;label.fontSizeMax=3;label.color=Color.white;
            foreach(float x in new[]{-.3f,.3f})
            {
                var point=p+owner.right*(x*width);Tube("Poste de señal",new Vector3(point.x,Mathf.Max(0,p.y-.5f),point.z),new Vector3(point.x,0,point.z),.045f,steel,false,owner);
            }
            return label;
        }
        static void Slug(Vector3 a,Vector3 b)
        {
            var go=new GameObject("Babosa patrullera");go.transform.SetParent(root,false);go.transform.position=a;
            var rb=go.AddComponent<Rigidbody>();rb.isKinematic=true;var col=go.AddComponent<CapsuleCollider>();col.direction=2;col.radius=.38f;col.height=1.3f;
            var patrol=go.AddComponent<PatrolEnemy>();patrol.pointA=a;patrol.pointB=b;patrol.speed=1.15f;go.AddComponent<DamageSource>();go.AddComponent<EnemyHealth>();
            Shape("Cuerpo de babosa",PrimitiveType.Sphere,a,new Vector3(.95f,.65f,1.35f),lime,false,go.transform);
            for(int side=-1;side<=1;side+=2)
            {
                var eye=a+new Vector3(side*.25f,.52f,.43f);Tube("Pedúnculo",eye-Vector3.up*.25f,eye,.055f,lime,false,go.transform);
                Shape("Ojo",PrimitiveType.Sphere,eye,Vector3.one*.24f,white,false,go.transform);Shape("Pupila",PrimitiveType.Sphere,eye+Vector3.forward*.10f,Vector3.one*.13f,dark,false,go.transform);
            }
        }
        static Transform Boat()
        {
            var owner=Group("Balsa de expedición");owner.position=new Vector3(0,.05f,-31);
            Box("Cubierta de la balsa",new Vector3(0,.08f,-31),new Vector3(4.5f,.30f,3),dark,true,owner);
            foreach(float x in new[]{-2.3f,2.3f})Tube("Flotador naranja",new Vector3(x,.20f,-32.7f),new Vector3(x,.20f,-29.3f),.32f,orange,false,owner);
            foreach(float z in new[]{-32.7f,-29.3f})Tube("Flotador naranja",new Vector3(-2.3f,.20f,z),new Vector3(2.3f,.20f,z),.32f,orange,false,owner);
            foreach(float x in new[]{-2.3f,2.3f})foreach(float z in new[]{-32.7f,-29.3f})Shape("Esquina inflable",PrimitiveType.Sphere,new Vector3(x,.20f,z),Vector3.one*.64f,orange,false,owner);
            foreach(float x in new[]{-1.2f,1.2f})foreach(float z in new[]{-32.7f,-29.3f})Box("Asa negra",new Vector3(x,.53f,z),new Vector3(.38f,.08f,.15f),dark,false,owner);
            var can=Shape("Bombona de la bocina",PrimitiveType.Cylinder,new Vector3(1.7f,.55f,-31.8f),new Vector3(.35f,.32f,.35f),blue,false,owner);
            Tube("Bocina",new Vector3(1.7f,.95f,-31.8f),new Vector3(1.7f,.95f,-31.2f),.14f,red,false,owner);
            return owner;
        }
        static FoodGoal Bowl()
        {
            var c=new Vector3(0,0,-11);float[,] profile={{0,.03f},{2.5f,.03f},{2.9f,.22f},{3,.52f},{2.9f,.82f},{2.65f,.82f},{2.5f,.54f},{2.4f,.12f},{0,.12f}};
            const int slices=64;var v=new List<Vector3>();var groups=new[]{new List<int>(),new List<int>(),new List<int>()};
            for(int i=0;i<profile.GetLength(0);i++)for(int j=0;j<slices;j++){float a=j*Mathf.PI*2/slices;v.Add(new Vector3(Mathf.Cos(a)*profile[i,0],profile[i,1],Mathf.Sin(a)*profile[i,0]));}
            for(int i=0;i<profile.GetLength(0)-1;i++)for(int j=0;j<slices;j++)
            {int a=i*slices+j,b=i*slices+(j+1)%slices,d=b+slices,e=a+slices;groups[i<3?0:i<6?1:2].AddRange(new[]{a,e,b,b,e,d});}
            var mesh=new Mesh{name="Bol de recolección"};mesh.SetVertices(v);mesh.subMeshCount=3;for(int i=0;i<3;i++)mesh.SetTriangles(groups[i],i);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,meshDir+"/Bowl.asset");
            var bowl=new GameObject("Bol central de provisiones");bowl.transform.SetParent(root,false);bowl.transform.position=c;bowl.AddComponent<MeshFilter>().sharedMesh=mesh;bowl.AddComponent<MeshRenderer>().sharedMaterials=new[]{blue,orange,dark};bowl.AddComponent<MeshCollider>().sharedMesh=mesh;
            var trigger=new GameObject("Entrega de provisiones");trigger.transform.SetParent(root,false);trigger.transform.position=c;
            var collider=trigger.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=new Vector3(6.3f,2.4f,6.3f);collider.center=Vector3.up*1.0f;
            return trigger.AddComponent<FoodGoal>();
        }

        [MenuItem("Lizards/Level 2/Construir expedición de recolección")]
        public static string Build()
        {
            if(Application.isPlaying)throw new Exception("Stop Play first.");
            var active=EditorSceneManager.GetActiveScene();if(active.isDirty)EditorSceneManager.SaveScene(active);
            string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory("Backups/Level2Expedition");
            if(File.Exists(ScenePath))File.Copy(ScenePath,"Backups/Level2Expedition/LEVEL2_"+stamp+".unity");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var originals=scene.GetRootGameObjects();var archive=new GameObject("__LEVEL2 anterior "+stamp);foreach(var original in originals)original.transform.SetParent(archive.transform,true);archive.SetActive(false);
            Directory.CreateDirectory(Dir+"/Materials");Directory.CreateDirectory(Dir+"/FoodPrefabs");meshDir=Dir+"/Meshes/"+stamp;Directory.CreateDirectory(meshDir);AssetDatabase.Refresh();
            sceneRoot=new GameObject("LEVEL2 · Expedición de provisiones").transform;root=sceneRoot;serial=0;foodSerial=0;random=new System.Random(273);caches.Clear();
            block=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Lizard/LevelOne/ReferenceStyle/Meshes/Bloque biselado unitario.asset");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset");
            violet=Mat("Mampostería violeta",new Color(.49f,.41f,.66f));trim=Mat("Relieve lila",new Color(.53f,.45f,.70f));stone=Mat("Pavimento oliva",new Color(.36f,.39f,.31f));stone2=Mat("Pavimento variación",new Color(.385f,.415f,.335f));
            mint=Mat("Tubería menta",new Color(.27f,.95f,.65f));lime=Mat("Lima de accesos",new Color(.61f,1,.025f));yellow=Mat("Amarillo de seguridad",new Color(1,.77f,.06f));red=Mat("Rojo de señal",new Color(.96f,.10f,.07f));orange=Mat("Naranja inflable",new Color(1,.36f,.045f));
            dark=Mat("Acero oscuro",new Color(.11f,.13f,.14f));steel=Mat("Metal gris",new Color(.29f,.34f,.31f));wood=Mat("Tablones cálidos",new Color(.75f,.48f,.26f));white=Mat("Marfil",new Color(.90f,1,.96f));blue=Mat("Azul del bol",new Color(.07f,.25f,.65f));vertex=Mat("Atrezo pintado",Color.white,1);
            water=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Agua.mat");if(!water){water=new Material(Shader.Find("Lizards/SwampWater"));AssetDatabase.CreateAsset(water,Dir+"/Materials/Agua.mat");}water.SetColor("_BaseColor",new Color(.035f,.61f,.50f));
            MakeFoodPrefabs();

            // Arrival quay: orange raft, boarding planks and two dry banks.
            root=Room("01 · Muelle de llegada",new Rect(-10,-38,20,16),0,new[]{new Gap('N',0,8)});
            Floor(new Rect(-10,-38,6,16));Floor(new Rect(4,-38,6,16));Floor(new Rect(-10,-26,20,4));Water(new Rect(-4,-38,8,12),-.4f);
            var boat=Boat();Box("Pasarela de desembarco",new Vector3(0,.08f,-27.6f),new Vector3(2.3f,.16f,3.6f),wood,true);Box("Tablón lateral",new Vector3(-3.6f,.08f,-31),new Vector3(2.8f,.16f,1.4f),wood,true);
            Cluster(new Vector3(-8,0,-29),4);PhysicsProp("MaintenanceDrum",new Vector3(7,.6f,-29),new Vector3(.8f,1.2f,.8f),5);
            Portal(new Vector3(0,0,-37.7f),7,Quaternion.identity);Box("Oscuridad del túnel",new Vector3(0,2.7f,-37.8f),new Vector3(6.4f,5.4f,.06f),dark);
            Sign("ALCANTARILLAS\nRECOGE · ENTREGA · REGRESA",new Vector3(6.5f,1.8f,-25),3.5f);

            // Central collection chamber, four accessible exits.
            root=sceneRoot;root=Room("02 · Cámara del bol",new Rect(-12,-22,24,24),0,new[]{new Gap('S',0,8),new Gap('N',0,8),new Gap('W',-8),new Gap('E',-8)});
            Floor(new Rect(-12,-22,24,24));var goal=Bowl();var board=Sign("BOL DE PROVISIONES",new Vector3(-5.4f,2,-7),3.2f);
            Sign("GALERÍAS ←",new Vector3(-8,1.7f,-5),2.5f);Sign("→ ALMACÉN",new Vector3(8,1.7f,-5),2.5f);Sign("COLECTORES ↑",new Vector3(-5,1.7f,.2f),2.7f);
            Pipe(new Vector3(-11,6,-18),new Vector3(11,6,-18),.4f,true);Pipe(new Vector3(-11,6,-1),new Vector3(11,6,-1),.4f,true);
            Cluster(new Vector3(-8,0,-18),4);Cache(new Vector3(8,.62f,-17));PhysicsProp("TrafficBarrier",new Vector3(-8,.48f,-2),new Vector3(1.9f,.95f,.8f),7);
            var emergency=new GameObject("Cesta de provisiones renovable");emergency.transform.SetParent(root,false);emergency.transform.position=new Vector3(-8,0,-14);var spawner=emergency.AddComponent<FoodSpawner>();spawner.prefab=foods[0];spawner.respawnDelay=10;spawner.value=1;
            Box("Cesta",new Vector3(-8,.1f,-14),new Vector3(1,.2f,1),wood);
            Recovery(new Vector3(8,.08f,-14));

            // West loop: dry banks, water passage, tongue anchors and a return route through stores.
            root=sceneRoot;root=Room("03 · Galerías inundadas",new Rect(-36,-14,24,12),0,new[]{new Gap('E',-8),new Gap('N',-28)});
            Floor(new Rect(-36,-14,24,3));Floor(new Rect(-36,-5,24,3));Floor(new Rect(-36,-11,4,6));Floor(new Rect(-16,-11,4,6));Water(new Rect(-32,-11,16,6));
            foreach(float x in new[]{-18f,-23f,-28f})Anchor(new Vector3(x,4,-8));
            foreach(float x in new[]{-21f,-27f})Shape("Isleta de paso",PrimitiveType.Cylinder,new Vector3(x,-.22f,-8),new Vector3(1.8f,.15f,1.8f),lime,true);
            Ramp("Salida del agua oeste",new Vector3(-29.5f,-.7f,-8),new Vector3(-32,0,-8),2.4f);Ramp("Salida del agua este",new Vector3(-18.5f,-.7f,-8),new Vector3(-16,0,-8),2.4f);
            Cluster(new Vector3(-30,0,-13),6);Cluster(new Vector3(-35,0,-4),4);Recovery(new Vector3(-34,.08f,-12));
            root=sceneRoot;root=Room("Paso oeste",new Rect(-31,-2,6,7),0,new[]{new Gap('S',-28),new Gap('N',-28)},5.8f);Floor(new Rect(-31,-2,6,7));

            root=sceneRoot;root=Room("04 · Almacén húmedo",new Rect(-36,5,22,20),0,new[]{new Gap('S',-28),new Gap('E',16,8),new Gap('N',-25)});Floor(new Rect(-36,5,22,20));
            foreach(var p in new[]{new Vector3(-33,.62f,9),new Vector3(-31.5f,.62f,9),new Vector3(-33,.62f,21),new Vector3(-18,.62f,22)})Cache(p);
            Cluster(new Vector3(-23,0,9),6);Food(new Vector3(-32,0,18),2,4,1.8f);
            Model("Rejilla de almacén","DrainGrate",new Vector3(-29,.035f,17),new Vector3(2,.07f,1));
            Sign("ALMACÉN HÚMEDO\nABRE LAS CAJAS [E]",new Vector3(-30,1.8f,23),3.2f);
            root=sceneRoot;root=Room("Paso lateral oeste",new Rect(-14,12,2,8),0,new[]{new Gap('W',16,8),new Gap('E',16,8)},5.8f);Floor(new Rect(-14,12,2,8));

            // East loop: a warehouse with a pressure-operated optional food cache.
            root=sceneRoot;root=Room("Paso este",new Rect(12,-11,2,6),0,new[]{new Gap('W',-8),new Gap('E',-8)},5.8f);Floor(new Rect(12,-11,2,6));
            root=sceneRoot;root=Room("05 · Almacén de carga",new Rect(14,-17,22,18),0,new[]{new Gap('W',-8),new Gap('N',28)});Floor(new Rect(14,-17,22,18));
            foreach(var p in new[]{new Vector3(17,.62f,-14),new Vector3(18.5f,.62f,-14),new Vector3(23,.62f,-2),new Vector3(25,.62f,-2)})Cache(p);
            Cluster(new Vector3(20,0,-10),6);
            var weight=PhysicsProp("SupplyCrate",new Vector3(25,.72f,-13),Vector3.one*1.4f,20);weight.name="Contrapeso marcado · 20 kg";
            Box("Marca amarilla",weight.transform.position+Vector3.back*.72f,new Vector3(.6f,.45f,.025f),yellow,false,weight.transform);
            var plate=new GameObject("Placa de carga · 12 kg");plate.transform.SetParent(root,false);plate.transform.position=new Vector3(28,0,-12);
            var pc=plate.AddComponent<BoxCollider>();pc.isTrigger=true;pc.size=new Vector3(2.6f,1.3f,2.6f);pc.center=Vector3.up*.65f;
            var pv=Box("Placa azul",new Vector3(28,.07f,-12),new Vector3(2.6f,.14f,2.6f),blue,true);
            Wall(false,31,-17,-11.5f,0,7.5f,"Cierre del depósito");Wall(false,31,-6.5f,1,0,7.5f,"Cierre del depósito");Wall(false,31,-11.5f,-6.5f,4.2f,3.3f,"Dintel de depósito");
            var gate=Box("Compuerta de provisiones",new Vector3(31,2,-9),new Vector3(.45f,4,5),dark,true).AddComponent<Door>();gate.openOffset=Vector3.up*4.5f;
            var pp=plate.AddComponent<PressurePlate>();pp.requiredMass=12;pp.doors=new[]{gate};pp.plateVisual=pv.transform;
            Cluster(new Vector3(33,0,-14),6);Food(new Vector3(33,0,-3),4,4,1.8f);Sign("CAJA MARCADA → PLACA",new Vector3(26,1.7f,-16),3.5f,180);
            root=sceneRoot;root=Room("Paso de filtros",new Rect(25,1,6,4),0,new[]{new Gap('S',28),new Gap('N',28)},5.8f);Floor(new Rect(25,1,6,4));

            root=sceneRoot;root=Room("06 · Filtros y elevador",new Rect(14,5,22,20),0,new[]{new Gap('S',28),new Gap('W',16,8),new Gap('N',25)});
            Floor(new Rect(14,5,22,6));Floor(new Rect(14,21,22,4));Floor(new Rect(14,11,6,10));Floor(new Rect(28,11,8,10));Water(new Rect(20,11,8,10),-.5f);
            Ramp("Salida del filtro",new Vector3(24,-.8f,18),new Vector3(24,0,21),2.5f);
            Box("Balcón de filtros",new Vector3(32,2.6f,17),new Vector3(5,.35f,5),steel,true);
            Ramp("Rampa de filtros",new Vector3(33,0,6),new Vector3(33,2.78f,14.5f),3.5f);
            var lift=Box("Elevador de inspección",new Vector3(21.5f,-.2f,16),new Vector3(2.2f,.25f,2.2f),yellow,true);var liftBody=lift.AddComponent<Rigidbody>();liftBody.isKinematic=true;lift.AddComponent<LevelOneMotion>().travel=new Vector3(0,3,0);
            Cluster(new Vector3(30,2.78f,16),6);Cluster(new Vector3(16,0,8),4);Cache(new Vector3(33,.62f,23),2);Cache(new Vector3(31.5f,.62f,23),2);
            Anchor(new Vector3(24,5,15));Slug(new Vector3(17,.4f,13),new Vector3(17,.4f,21));
            var pallet=Box("Tablones del rincón",new Vector3(30,1,22),new Vector3(3,2,.2f),wood,true);pallet.AddComponent<Breakable>().requiredSpeed=2.5f;
            root=sceneRoot;root=Room("Paso lateral este",new Rect(12,12,2,8),0,new[]{new Gap('W',16,8),new Gap('E',16,8)},5.8f);Floor(new Rect(12,12,2,8));

            // Central pump hall: flooded lower level, ramps and a raised food bridge.
            root=sceneRoot;root=Room("07 · Colectores elevados",new Rect(-12,3,24,24),0,new[]{new Gap('S',0,8),new Gap('W',16,8),new Gap('E',16,8),new Gap('N',0,8)});
            Floor(new Rect(-4,2,8,1));
            Floor(new Rect(-12,3,24,4));Floor(new Rect(-12,23,24,4));Floor(new Rect(-12,7,8,16));Floor(new Rect(4,7,8,16));Water(new Rect(-4,7,8,16),-.6f);
            Box("Puente de mantenimiento",new Vector3(0,2.45f,16),new Vector3(23,.30f,3),steel,true);
            foreach(int s in new[]{-1,1})
            {
                Ramp("Rampa del colector",new Vector3(s*8,0,4.5f),new Vector3(s*8,2.6f,14.5f),3.6f);
                foreach(float z in new[]{14.55f,17.45f})Tube("Barandilla del puente",new Vector3(s*3,3.5f,z),new Vector3(s*10.8f,3.5f,z),.045f,yellow);
                Pipe(new Vector3(s*5,6,11),new Vector3(s*5,9.5f,11),.55f,true);
            }
            Pipe(new Vector3(-11,6,11),new Vector3(11,6,11),.55f,true);
            Cluster(new Vector3(-9,2.6f,15.5f),4);Cluster(new Vector3(6,2.6f,15.5f),4);Cluster(new Vector3(6,0,24),4);
            Anchor(new Vector3(-1.8f,5,11));Anchor(new Vector3(1.8f,5,20));Recovery(new Vector3(-9,.08f,24));
            Ramp("Salida del agua",new Vector3(0,-1,20),new Vector3(0,0,23),3);

            // Outer circuits join the high-value northern pantry, so no wing is a dead-end.
            root=sceneRoot;root=Room("Circuito oeste vertical",new Rect(-28,25,6,11),0,new[]{new Gap('S',-25),new Gap('N',-25)},5.8f);Floor(new Rect(-28,25,6,11));
            root=sceneRoot;root=Room("Circuito oeste transversal",new Rect(-28,33,16,6),0,new[]{new Gap('S',-25),new Gap('E',36)},5.8f);Floor(new Rect(-28,33,16,6));
            Ramp("Rampa a despensa oeste",new Vector3(-18,0,36),new Vector3(-12,1.2f,36),4);
            root=sceneRoot;root=Room("Circuito este vertical",new Rect(22,25,6,11),0,new[]{new Gap('S',25),new Gap('N',25)},5.8f);Floor(new Rect(22,25,6,11));
            root=sceneRoot;root=Room("Circuito este transversal",new Rect(12,33,16,6),0,new[]{new Gap('S',25),new Gap('W',36)},5.8f);Floor(new Rect(12,33,16,6));
            Ramp("Rampa a despensa este",new Vector3(18,0,36),new Vector3(12,1.2f,36),4);
            root=sceneRoot;root=Room("08 · Despensa del fondo",new Rect(-12,29,24,18),1.2f,new[]{new Gap('S',0,8),new Gap('W',36),new Gap('E',36)});Floor(new Rect(-12,29,24,18),1.2f);
            Ramp("Acceso central a despensa",new Vector3(0,0,26),new Vector3(0,1.2f,29),7);
            foreach(var p in new[]{new Vector3(-9,1.82f,32),new Vector3(9,1.82f,32),new Vector3(-8,1.82f,42)})Cache(p);
            Cluster(new Vector3(-4,1.2f,32),6);Cluster(new Vector3(5,1.2f,40),4);
            Box("Estante del fondo",new Vector3(0,2.5f,44),new Vector3(8,.25f,3),steel,true);Cluster(new Vector3(-2,2.63f,44),6);Food(new Vector3(8,1.2f,43),2,4,1.8f);
            var acid=Box("Membrana de la reserva",new Vector3(0,2.6f,42),new Vector3(8,2.8f,.18f),mint,true);acid.AddComponent<AcidVulnerable>().hitsToDissolve=2;
            Slug(new Vector3(-8,1.6f,38),new Vector3(0,1.6f,38));Sign("DESPENSA\nPROVISIONES DE RESERVA",new Vector3(5,2.9f,45),3.2f);

            root=sceneRoot;
            var mission=new GameObject("Expedición · cuota y regreso").AddComponent<LevelTwoExpedition>();mission.transform.SetParent(root,false);mission.bowl=goal;mission.boat=boat;mission.bowlBoard=board;
            mission.extractionBeacon=Shape("Baliza de extracción",PrimitiveType.Sphere,new Vector3(0,2.5f,-31),Vector3.one*.45f,lime).transform;
            CreateHUD(mission);
            var playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Prefabs/Sunny.prefab");var player=(GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);player.name="Jugador · Sunny";player.transform.position=mission.arrival;player.GetComponent<LizardInput>().controlled=true;var playerComponent=player.GetComponent<LizardPlayer>();playerComponent.displayName="Sunny";playerComponent.startFood=0;
            var cameraGO=new GameObject("Cámara de expedición");cameraGO.tag="MainCamera";var camera=cameraGO.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=160;camera.fieldOfView=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.38f,.32f,.52f);cameraGO.AddComponent<AudioListener>();var rig=cameraGO.AddComponent<LizardCameraRig>();rig.target=playerComponent;cameraGO.transform.position=new Vector3(0,3,-36);cameraGO.transform.LookAt(new Vector3(0,1,-29));var hud=new GameObject("HUD del jugador").AddComponent<LizardHUD>();hud.rig=rig;hud.showDebug=false;
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.66f,.66f,.78f);RenderSettings.ambientEquatorColor=new Color(.48f,.45f,.58f);RenderSettings.ambientGroundColor=new Color(.25f,.26f,.30f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.38f,.32f,.52f);RenderSettings.fogStartDistance=30;RenderSettings.fogEndDistance=105;
            var sun=new GameObject("Luz de la expedición").AddComponent<Light>();sun.transform.SetParent(sceneRoot,false);sun.type=LightType.Directional;sun.color=new Color(1,.99f,.98f);sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.shadowStrength=.6f;sun.transform.rotation=Quaternion.Euler(48,-25,0);
            var build=EditorBuildSettings.scenes.ToList();if(!build.Any(x=>x.path==ScenePath))build.Add(new EditorBuildSettingsScene(ScenePath,true));else for(int i=0;i<build.Count;i++)if(build[i].path==ScenePath)build[i]=new EditorBuildSettingsScene(ScenePath,true);EditorBuildSettings.scenes=build.ToArray();
            Detail();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,4),Quaternion.Euler(65,0,0),65);
            float loose=Object.FindObjectsByType<FoodItem>().Sum(f=>f.nutrition),boxed=caches.Sum(c=>c.count);
            return $"LEVEL2 expedition built: {foodSerial} varied placements, {caches.Count} caches, {loose+boxed:0} total nutrition plus renewable supply. Eight sectors, two outer circuits, quota and raft extraction.";
        }
        public static void Detail()
        {
            sceneRoot=GameObject.Find("LEVEL2 · Expedición de provisiones").transform;
            var old=sceneRoot.Find("Ambiente de estaciones");if(old)Object.DestroyImmediate(old.gameObject);
            root=sceneRoot;root=Group("Ambiente de estaciones");
            violet=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Mampostería violeta.mat");mint=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Tubería menta.mat");yellow=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Amarillo de seguridad.mat");dark=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Acero oscuro.mat");steel=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Metal gris.mat");vertex=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Atrezo pintado.mat");wood=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Tablones cálidos.mat");
            meshDir=Dir+"/Meshes/Details_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory(meshDir);serial=0;
            // Correct the primitive cylinder's rounded capsule collider on shallow platforms.
            foreach(var capsule in sceneRoot.GetComponentsInChildren<CapsuleCollider>())
            {
                var filter=capsule.GetComponent<MeshFilter>();if(!filter||filter.sharedMesh.name!="Cylinder")continue;
                var owner=capsule.gameObject;Object.DestroyImmediate(capsule);owner.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            }
            foreach(var p in new[]{new Vector3(-9,.60f,-3),new Vector3(-9.9f,.60f,-3),new Vector3(34,.60f,-15),new Vector3(34,.60f,7),new Vector3(-34,.60f,23),new Vector3(10,1.8f,45)})PhysicsProp("MaintenanceDrum",p,new Vector3(.76f,1.2f,.76f),5);
            foreach(var p in new[]{new Vector3(9,.48f,-19),new Vector3(-34,.48f,6.5f),new Vector3(15.5f,.48f,-3),new Vector3(16,.48f,23)})PhysicsProp("TrafficBarrier",p,new Vector3(1.8f,.95f,.72f),7);
            foreach(var p in new[]{new Vector3(8,.43f,-20),new Vector3(-32,.43f,6.5f),new Vector3(15.5f,.43f,-1),new Vector3(16,.43f,21.5f),new Vector3(-10,1.63f,44),new Vector3(10,1.63f,44)})PhysicsProp("SafetyCone",p,new Vector3(.6f,.86f,.6f),1);
            foreach(var p in new[]{new Vector3(-7,.05f,-20),new Vector3(25,.05f,-5),new Vector3(-25,.05f,20),new Vector3(17,.05f,7),new Vector3(7,.05f,24),new Vector3(5,1.25f,31)})Model("Drenaje de suelo","DrainGrate",p,new Vector3(1.8f,.07f,.9f));
            for(int side=-1;side<=1;side+=2)
                for(int x=4;x<=10;x+=2)
                    foreach(float z in new[]{14.55f,17.45f})Tube("Poste de pasarela",new Vector3(side*x,2.6f,z),new Vector3(side*x,3.5f,z),.035f,yellow);
            foreach(float z in new[]{14.6f,19.4f})
            {
                Tube("Pasamanos del balcón",new Vector3(29.65f,3.75f,z),new Vector3(34.3f,3.75f,z),.045f,yellow);
                foreach(float x in new[]{29.65f,34.3f})Tube("Poste del balcón",new Vector3(x,2.78f,z),new Vector3(x,3.75f,z),.035f,yellow);
            }
            foreach(var p in new[]{new Vector3(-8,.045f,-3),new Vector3(25,.045f,-15),new Vector3(-32,.045f,22),new Vector3(17,.045f,23)})
                for(int i=0;i<6;i++)
                {var stripe=Box("Marca de seguridad",p+Vector3.right*i*.25f,new Vector3(.14f,.015f,.5f),i%2==0?yellow:dark);stripe.transform.rotation=Quaternion.Euler(0,-25,0);}
            // Reusable valve groups and cable bundles make each collection room read as a working sewer.
            foreach(var p in new[]{new Vector3(-35.6f,3.3f,14),new Vector3(35.6f,3.3f,-5),new Vector3(35.6f,3.3f,18)})
            {
                var wheel=Ring("Válvula lateral",p,.5f,.06f,yellow,Quaternion.Euler(0,90,0));
                foreach(float z in new[]{-.4f,.4f})Tube("Radio de válvula",p+new Vector3(0,0,z),p-new Vector3(0,0,z),.035f,yellow);
                Tube("Radio de válvula",p+Vector3.up*.4f,p-Vector3.up*.4f,.035f,yellow);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        static void CreateHUD(LevelTwoExpedition mission)
        {
            var canvasGO=new GameObject("Objetivo de expedición",typeof(Canvas),typeof(CanvasScaler));var canvas=canvasGO.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10;
            var scaler=canvasGO.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);
            var panel=new GameObject("Panel de cuota",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvasGO.transform,false);var rect=panel.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-24,-24);rect.sizeDelta=new Vector2(360,116);panel.GetComponent<Image>().color=new Color(.12f,.08f,.20f,.84f);
            Text Text(string n,string value,int size,float y)
            {
                var go=new GameObject(n,typeof(RectTransform),typeof(Text));go.transform.SetParent(panel.transform,false);var r=go.GetComponent<RectTransform>();r.anchorMin=new Vector2(0,1);r.anchorMax=new Vector2(1,1);r.pivot=new Vector2(.5f,1);r.anchoredPosition=new Vector2(0,y);r.sizeDelta=new Vector2(-24,28);var text=go.GetComponent<Text>();text.text=value;text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=size;text.fontStyle=FontStyle.Bold;text.color=Color.white;text.alignment=TextAnchor.MiddleLeft;return text;
            }
            mission.dayLabel=Text("Día","ALCANTARILLAS",17,-8);mission.quotaLabel=Text("Cuota","PROVISIONES 0 / 400",22,-34);mission.statusLabel=Text("Estado","BUSCA COMIDA",12,-82);
            var track=new GameObject("Progreso",typeof(RectTransform),typeof(Image));track.transform.SetParent(panel.transform,false);var tr=track.GetComponent<RectTransform>();tr.anchorMin=tr.anchorMax=new Vector2(.5f,1);tr.pivot=new Vector2(.5f,1);tr.anchoredPosition=new Vector2(0,-70);tr.sizeDelta=new Vector2(334,8);track.GetComponent<Image>().color=new Color(.2f,.22f,.19f);
            var fill=new GameObject("Relleno",typeof(RectTransform),typeof(Image));fill.transform.SetParent(track.transform,false);var fr=fill.GetComponent<RectTransform>();fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;var image=fill.GetComponent<Image>();image.color=new Color(.65f,1,.05f);image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;image.fillOrigin=0;image.fillAmount=0;mission.quotaFill=image;
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Dir+"/QuotaFill.asset");if(!sprite){sprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);AssetDatabase.CreateAsset(sprite,Dir+"/QuotaFill.asset");}image.sprite=sprite;
        }
    }
}
