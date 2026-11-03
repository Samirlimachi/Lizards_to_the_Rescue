using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using Object=UnityEngine.Object;

namespace Lizards.EditorTools
{
    /// <summary>Art pass over the existing LEVEL1; does not rebuild mission or interactive objects.</summary>
    public static class LevelOneReferenceStyle
    {
        const string Dir="Assets/Lizard/LevelOne/ReferenceStyle";
        static Transform art;
        static Material violet,brick,olive,dark,steel,lime,mint,yellow,red,white,vertex;
        static System.Random random;
        static float R(float a,float b)=>a+(b-a)*(float)random.NextDouble();
        static Material Material(string name,Color color,Texture texture=null,float vertices=0)
        {
            string path=Dir+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Lizards/Toon"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetColor("_ShadowColor",new Color(.71f,.73f,.84f));
            m.SetFloat("_ShadowThreshold",-.04f);m.SetFloat("_ShadowSoftness",.15f);
            m.SetFloat("_OutlineWidth",0);m.SetFloat("_SpecSize",0);m.SetColor("_RimColor",new Color(1,1,1,0));
            m.SetFloat("_VertexColorWeight",vertices);if(texture)m.SetTexture("_BaseMap",texture);
            EditorUtility.SetDirty(m);return m;
        }
        static GameObject Shape(string name,PrimitiveType type,Vector3 p,Vector3 scale,Material mat,bool collision=false,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;
            go.transform.SetParent(parent?parent:art,false);go.transform.position=p;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=mat;if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static GameObject Box(string n,Vector3 p,Vector3 s,Material m,bool col=false)=>Shape(n,PrimitiveType.Cube,p,s,m,col);
        static GameObject Tube(string n,Vector3 a,Vector3 b,float radius,Material m,bool col=false)
        {
            var go=Shape(n,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(radius*2,(b-a).magnitude*.5f,radius*2),m,col);
            go.transform.rotation=Quaternion.FromToRotation(Vector3.up,b-a);return go;
        }
        static GameObject MeshObject(string name,List<Vector3> vertices,List<int> triangles,Material material)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Dir+"/Meshes/"+name+".asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(name);go.transform.SetParent(art,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        static GameObject Ring(string name,Vector3 center,float radius,float thick,Material m,Quaternion rotation)
        {
            var v=new List<Vector3>();var t=new List<int>();const int around=48,sides=8;
            for(int i=0;i<=around;i++)
            {
                float a=i*Mathf.PI*2/around;
                for(int j=0;j<sides;j++)
                {
                    float b=j*Mathf.PI*2/sides;
                    v.Add(new Vector3(Mathf.Cos(a)*(radius+thick*Mathf.Cos(b)),Mathf.Sin(a)*(radius+thick*Mathf.Cos(b)),thick*Mathf.Sin(b)));
                }
            }
            for(int i=0;i<around;i++)for(int j=0;j<sides;j++)
            {int a=i*sides+j,b=i*sides+(j+1)%sides,c=a+sides,d=b+sides;t.AddRange(new[]{a,b,c,b,d,c});}
            // Torus normals must point away from its centerline.
            for(int i=0;i<t.Count;i+=3){int q=t[i+1];t[i+1]=t[i+2];t[i+2]=q;}
            var go=MeshObject(name,v,t,m);go.transform.SetPositionAndRotation(center,rotation);return go;
        }
        static void Portal(string name,float z,float radius,float spring)
        {
            var v=new List<Vector3>();var t=new List<int>();var pts=new List<Vector3>{new Vector3(-radius,0,0)};
            for(int i=0;i<=32;i++){float a=Mathf.PI-i*Mathf.PI/32;pts.Add(new Vector3(Mathf.Cos(a)*radius,spring+Mathf.Sin(a)*radius,0));}
            pts.Add(new Vector3(radius,0,0));
            for(int i=0;i<pts.Count;i++)
            {
                var tangent=(pts[Mathf.Min(i+1,pts.Count-1)]-pts[Mathf.Max(0,i-1)]).normalized;
                var normal=new Vector3(-tangent.y,tangent.x,0);
                for(int j=0;j<8;j++){float a=j*Mathf.PI/4;v.Add(pts[i]+normal*(.43f*Mathf.Cos(a))+Vector3.forward*(.38f*Mathf.Sin(a)));}
            }
            for(int i=0;i<pts.Count-1;i++)for(int j=0;j<8;j++)
            {int a=i*8+j,b=i*8+(j+1)%8,c=a+8,d=b+8;t.AddRange(new[]{a,c,b,b,c,d});}
            var go=MeshObject(name,v,t,lime);go.transform.position=new Vector3(0,0,z);
            for(int s=-1;s<=1;s+=2)Box("Zapata de acceso",new Vector3(s*radius,.13f,z),new Vector3(1.15f,.26f,1.1f),steel);
        }
        static void InstallModel(GameObject owner,string modelName,Vector3 size)
        {
            foreach(var child in owner.transform.Cast<Transform>().Where(t=>t.name.StartsWith("Modelo de referencia · ")).ToArray())Object.DestroyImmediate(child.gameObject);
            foreach(var r in owner.GetComponentsInChildren<Renderer>())r.enabled=false;
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Art/StationStyle/Models/"+modelName+".fbx");
            var go=Object.Instantiate(asset);go.name="Modelo de referencia · "+modelName;go.transform.SetParent(owner.transform,false);go.transform.localPosition=Vector3.zero;
            foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterial=vertex;
            var bounds=FoodCollectionBuilder.VisualBounds(go);
            go.transform.localScale=Vector3.Scale(go.transform.localScale,new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z));
            bounds=FoodCollectionBuilder.VisualBounds(go);go.transform.position+=owner.transform.position-bounds.center;
        }

        public static void RefineSigns()
        {
            var level=GameObject.Find("LEVEL 1 · Estación de bombeo / entorno").transform;
            foreach(var source in level.GetComponentsInChildren<TextMesh>())
            {
                var link=source.GetComponent<SignLabel>();if(!link||!link.target)continue;
                if(source.transform.parent.name.StartsWith("SEÑAL /"))continue;
                var panel=level.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Panel · ")).OrderBy(r=>Vector3.Distance(r.transform.position,source.transform.position)).FirstOrDefault();
                if(!panel||Vector3.Distance(panel.transform.position,source.transform.position)>.7f)continue;
                Vector3 p=panel.transform.position;Vector3 oldSize=panel.bounds.size;
                var frame=level.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Marco").OrderBy(r=>Vector3.Distance(r.transform.position,p)).FirstOrDefault();
                var legs=level.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Pata"&&Mathf.Abs(r.transform.position.z-p.z)<.3f&&Mathf.Abs(r.transform.position.x-p.x)<oldSize.x*.5f).ToArray();
                foreach(var leg in legs)leg.gameObject.SetActive(false);
                var group=new GameObject("SEÑAL / "+source.text.Split('\n')[0]).transform;group.SetParent(level,false);group.position=p;
                source.transform.SetParent(group,true);link.target.transform.SetParent(group,true);panel.transform.SetParent(group,true);
                if(frame&&Vector3.Distance(frame.transform.position,p)<.3f)frame.transform.SetParent(group,true);
                float scale=.5f;Vector3 position=new Vector3(-7,1.7f,p.z);
                if(p.z< -8&&p.y>4){position=new Vector3(-6,1.9f,-23);scale=.46f;source.text="ESTACIÓN 01\nRECUPERA · RESCATA · ENTREGA";}
                else if(p.z< -8){position=new Vector3(-7,1.6f,-17);scale=.58f;source.text="MUELLE\nE COMER · G ESCUPIR";}
                else if(p.z<0){position=new Vector3(6.7f,1.65f,-5);source.text="CRUZA EL CANAL\nLENGUA · RECOGER · SOLTAR";}
                else if(p.z<15){position=new Vector3(7,1.7f,9);source.text="CONTRAPESO\nCAJA MARCADA → PLACA";}
                else if(p.z<35){position=new Vector3(-7,1.7f,24);source.text="COME Y CRECE\nRUEDA PARA ROMPER";}
                else if(p.z<46){position=new Vector3(-6,1.7f,42);source.text="RESCATA A PIP\nENTREGA LA COMIDA";}
                else{position=new Vector3(-5,1.7f,50);scale=.55f;}
                group.localScale=Vector3.one*scale;group.position=position;
                link.target.text=source.text;link.target.enableVertexGradient=false;link.target.color=Color.white;
                float bottom=panel.bounds.min.y;
                foreach(int side in new[]{-1,1})
                {
                    var leg=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(leg.GetComponent<Collider>());
                    leg.name="Poste corto de señal";leg.transform.SetParent(group,true);leg.transform.position=new Vector3(position.x+side*panel.bounds.size.x*.32f,bottom*.5f,position.z+.04f);
                    leg.transform.localScale=new Vector3(.10f/scale,bottom/scale,.10f/scale);leg.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Acero gris.mat");
                }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        public static void FinalDetails()
        {
            var level=GameObject.Find("LEVEL 1 · Estación de bombeo / entorno").transform;
            art=level.Find("ESTÉTICA · referencia de video");
            var prior=art.Find("Acabados y desniveles");if(prior)Object.DestroyImmediate(prior.gameObject);
            var detail=new GameObject("Acabados y desniveles").transform;detail.SetParent(art,false);art=detail;
            olive=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Pavimento oliva.mat");steel=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Acero gris.mat");yellow=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Amarillo seguridad.mat");dark=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Acero oscuro.mat");
            foreach(var r in level.GetComponentsInChildren<Renderer>())if(r.name=="Lámpara"||r.name=="Cable")r.enabled=false;
            for(int z=-20;z<=52;z+=12)
            {
                Box("Carcasa de fluorescente",new Vector3(0,10.45f,z),new Vector3(2.3f,.18f,.65f),steel);
                Box("Luz de techo",new Vector3(0,10.34f,z),new Vector3(2,.035f,.40f),AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Fluorescente.mat"));
            }
            var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Art/StationStyle/Models/BeveledBlock.fbx");
            var raw=model.GetComponentInChildren<MeshFilter>().sharedMesh;
            var mesh=Object.Instantiate(raw);mesh.name="Bloque biselado unitario";
            var rawBounds=raw.bounds;
            mesh.vertices=raw.vertices.Select(p=>new Vector3((p.x-rawBounds.center.x)/rawBounds.size.x,(p.y-rawBounds.center.y)/rawBounds.size.y,(p.z-rawBounds.center.z)/rawBounds.size.z)).ToArray();mesh.RecalculateBounds();
            string blockPath=Dir+"/Meshes/Bloque biselado unitario.asset";
            var block=AssetDatabase.LoadAssetAtPath<Mesh>(blockPath);
            if(block){EditorUtility.CopySerialized(mesh,block);Object.DestroyImmediate(mesh);mesh=block;}else AssetDatabase.CreateAsset(mesh,blockPath);
            foreach(var mf in level.GetComponentsInChildren<MeshFilter>())
                if(mf.name=="Junta de baldosa"||mf.name=="Ladrillo"||mf.name=="Relieve de mampostería"||mf.name=="Pilar")mf.sharedMesh=mesh;
            // Shallow maintenance alcove and ramp; the main route stays clear.
            var bay=Box("Plataforma de mantenimiento",new Vector3(-8.8f,.225f,-20),new Vector3(5.6f,.45f,4),olive,true);
            bay.GetComponent<MeshFilter>().sharedMesh=mesh;
            var v=new List<Vector3>{new Vector3(-1.8f,0,-.9f),new Vector3(1.8f,0,-.9f),new Vector3(-1.8f,0,.9f),new Vector3(1.8f,0,.9f),new Vector3(-1.8f,.45f,.9f),new Vector3(1.8f,.45f,.9f)};
            var t=new List<int>{0,1,2,1,3,2,0,4,1,1,4,5,2,3,4,3,5,4,1,5,3,0,2,4};
            var ramp=MeshObject("Rampa de acceso al taller",v,t,olive);ramp.transform.position=new Vector3(-8.8f,0,-22.9f);ramp.AddComponent<MeshCollider>().sharedMesh=ramp.GetComponent<MeshFilter>().sharedMesh;
            foreach(var rb in level.GetComponentsInChildren<Rigidbody>())
                if(rb.name=="Barrera roja manipulable"||rb.name=="Cono de mantenimiento")
                    if(rb.position.z< -18){var p=rb.position;p.y=(rb.name.StartsWith("Barrera")?.48f:.43f)+.45f;rb.transform.position=p;}
            for(int z=-21;z<=-19;z+=2)Tube("Poste de barandilla",new Vector3(-11.3f,.45f,z),new Vector3(-11.3f,1.5f,z),.045f,yellow);
            Tube("Barandilla del taller",new Vector3(-11.3f,1.5f,-21),new Vector3(-11.3f,1.5f,-19),.045f,yellow);
            // Model the sliding door surface; all details follow the existing moving door.
            foreach(var name in new[]{"Compuerta del almacén","Puerta de salida"})
            {
                var door=GameObject.Find(name);var bounds=door.GetComponent<Collider>().bounds;
                var old=door.transform.Find("Detalle de compuerta");if(old)Object.DestroyImmediate(old.gameObject);
                var group=new GameObject("Detalle de compuerta").transform;group.SetParent(door.transform,false);
                for(float y=bounds.min.y+.5f;y<bounds.max.y-.2f;y+=.5f)
                {
                    var rib=Box("Nervio de compuerta",new Vector3(bounds.center.x,y,bounds.min.z-.025f),new Vector3(bounds.size.x*.96f,.035f,.035f),steel);rib.transform.SetParent(group,true);
                }
                int segments=Mathf.FloorToInt(bounds.size.x/.35f);
                for(int i=0;i<segments;i++)
                {
                    var stripe=Box("Franja de seguridad",new Vector3(bounds.min.x+.2f+i*.35f,bounds.min.y+.12f,bounds.min.z-.03f),new Vector3(.27f,.16f,.04f),i%2==0?yellow:dark);stripe.transform.SetParent(group,true);
                }
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        static GameObject Prop(string name,string model,Vector3 p,Vector3 size,bool physics)
        {
            var go=new GameObject(name);go.transform.SetParent(art,false);go.transform.position=p;
            InstallModel(go,model,size);
            if(physics)
            {
                var c=go.AddComponent<BoxCollider>();c.size=size;
                var rb=go.AddComponent<Rigidbody>();rb.mass=model=="TrafficBarrier"?7:1;rb.interpolation=RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;go.AddComponent<TongueTarget>().kind=TongueTargetKind.Object;
            }
            return go;
        }
        static void Pipe(Vector3 a,Vector3 b,float radius,string name)
        {
            Tube(name,a,b,radius,mint);
            int n=Mathf.CeilToInt(Vector3.Distance(a,b)/3);
            for(int i=0;i<=n;i++)
            {
                Vector3 c=Vector3.Lerp(a,b,i/(float)n);var d=(b-a).normalized;
                Tube("Unión de tubería",c-d*.08f,c+d*.08f,radius*1.15f,mint);
            }
        }

        [MenuItem("Lizards/Level 1/Aplicar estilo del video de referencia")]
        public static string Apply()
        {
            var scene=EditorSceneManager.GetActiveScene();
            if(Application.isPlaying||scene.name!="LEVEL1")throw new Exception("Open LEVEL1 outside Play mode.");
            Directory.CreateDirectory(Dir+"/Materials");Directory.CreateDirectory(Dir+"/Meshes");AssetDatabase.Refresh();random=new System.Random(194);
            var level=GameObject.Find("LEVEL 1 · Estación de bombeo / entorno").transform;
            foreach(var previous in level.Cast<Transform>().Where(t=>t.name=="ESTÉTICA / referencia de video"||t.name=="ESTÉTICA · referencia de video").ToArray())Object.DestroyImmediate(previous.gameObject);
            art=new GameObject("ESTÉTICA · referencia de video").transform;art.SetParent(level,false);
            violet=Material("Muro violeta",new Color(.49f,.41f,.66f));brick=Material("Relieve violeta",new Color(.53f,.45f,.70f));
            olive=Material("Pavimento oliva",new Color(.36f,.39f,.31f));dark=Material("Acero oscuro",new Color(.11f,.13f,.14f));steel=Material("Acero gris",new Color(.29f,.34f,.31f));
            lime=Material("Verde lima de accesos",new Color(.61f,1,.025f));mint=Material("Tubería menta",new Color(.27f,.95f,.65f));
            yellow=Material("Amarillo seguridad",new Color(1,.76f,.055f));red=Material("Rojo señal",new Color(.98f,.09f,.07f));white=Material("Espuma",new Color(.85f,1,.94f));vertex=Material("Atrezo pintado",Color.white,null,1);
            var palette=new Dictionary<string,Material>{
                {"Basalto",olive},{"Baldosa",olive},{"Lavanda",violet},{"Lavanda clara",brick},{"Ladrillo claro",brick},
                {"Metal",steel},{"Oscuro",dark},{"Zócalo",steel},{"Menta",mint},{"Naranja",red},{"Marco rojo",red},{"Amarillo",yellow},
                {"Madera",Material("Madera cálida",new Color(.74f,.48f,.25f))},{"Pizarra",dark},{"Charco",Material("Humedad del suelo",new Color(.28f,.32f,.27f))},
                {"Luz",Material("Fluorescente",new Color(.73f,.96f,.83f))},{"Lámpara",Material("Pantalla lámpara",new Color(.39f,.52f,.44f))}
            };
            foreach(var r in level.GetComponentsInChildren<Renderer>())
            {
                if(r.transform.IsChildOf(art))continue;
                var ms=r.sharedMaterials;bool changed=false;
                for(int i=0;i<ms.Length;i++)if(ms[i]&&palette.TryGetValue(ms[i].name,out var m)){ms[i]=m;changed=true;}
                if(changed)r.sharedMaterials=ms;
                if(r.name=="Conducto principal")r.sharedMaterial=mint;
                if(r.name=="Compuerta del almacén"||r.name=="Puerta de salida")r.sharedMaterial=dark;
                if(r.name=="Arco de infraestructura"||r.name=="Base arco")r.enabled=false;
                if(r.name.StartsWith("Muro")||r.name.StartsWith("Pared")||r.name=="Marco puerta izquierdo"||r.name=="Marco puerta derecho")r.shadowCastingMode=ShadowCastingMode.Off;
                if(r.name=="Muro lateral")
                {r.transform.position=new Vector3(r.transform.position.x,3.5f,r.transform.position.z);r.transform.localScale=new Vector3(.8f,7,84);}
                if(r.name=="Cornisa")r.transform.position=new Vector3(r.transform.position.x,7.05f,r.transform.position.z);
                if(r.name=="Junta de baldosa")
                {
                    int i=Mathf.Abs(Mathf.RoundToInt(r.transform.position.x*7+r.transform.position.z*11))%6;
                    float shift=(i-2.5f)*.008f;r.sharedMaterial=Material("Baldosa variante "+i,new Color(.36f+shift,.39f+shift,.31f+shift));
                }
            }
            // Enclosed barrel vault instead of the old open sky: interior-facing normals.
            var rv=new List<Vector3>();var rt=new List<int>();
            for(int i=0;i<=32;i++){float a=i*Mathf.PI/32;float x=Mathf.Cos(a)*12.3f,y=7+Mathf.Sin(a)*3.8f;rv.Add(new Vector3(x,y,-28));rv.Add(new Vector3(x,y,58));}
            for(int i=0;i<32;i++){int a=i*2;rt.AddRange(new[]{a,a+1,a+2,a+1,a+3,a+2});}
            MeshObject("Bóveda interior",rv,rt,violet).GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            for(int side=-1;side<=1;side+=2)
            {
                // Irregular masonry relieved only slightly, as in the reference.
                for(float z=-26;z<57;z+=2.9f)
                    for(int row=0;row<3;row++)
                        if(random.NextDouble()>.35)Box("Relieve de mampostería",new Vector3(side*11.86f,4.9f+row*.65f,z+R(-.4f,.4f)),new Vector3(.12f,R(.20f,.36f),R(1.1f,2.4f)),brick);
                // Bundled service cables on a cornice.
                for(int i=0;i<3;i++)
                {
                    var cable=Material("Cable "+i,i==0?new Color(.08f,.55f,.63f):i==1?new Color(.28f,.36f,.83f):new Color(.65f,.08f,.61f));
                    Tube("Cable de servicio",new Vector3(side*(11.3f-i*.14f),6.55f,-27),new Vector3(side*(11.3f-i*.14f),6.55f,57),.045f,cable);
                }
                foreach(float z in new[]{-15f,10f,30f,46f})
                {
                    var q=Quaternion.Euler(0,90,0);Vector3 c=new Vector3(side*11.72f,2.1f,z);
                    var disk=Shape("Fondo de conducto",PrimitiveType.Cylinder,c,new Vector3(2.45f,.05f,2.45f),dark);disk.transform.rotation=Quaternion.Euler(0,0,90);
                    Ring("Boca lateral "+side+" "+z,c,1.22f,.21f,lime,q);
                    for(int j=-2;j<=2;j++)Box("Reja del conducto",c+new Vector3(-side*.08f,0,j*.36f),new Vector3(.10f,2.0f,.075f),steel);
                    Pipe(new Vector3(side*10.9f,.6f,z),new Vector3(side*10.9f,.6f,z+1.8f),.24f,"Desagüe bajo");
                    for(int j=0;j<4;j++)
                    {
                        float h=R(.25f,.7f);Box("Goteo verdín",c+new Vector3(-side*.13f,-1.3f-h*.5f,(j-1.5f)*.35f),new Vector3(.025f,h,.12f),Material("Verdín",new Color(.39f,.61f,.11f)));
                    }
                }
            }
            Portal("Acceso lima · entrada",-8,5.6f,1.8f);Portal("Acceso lima · compuerta",20,4.6f,1.5f);Portal("Acceso lima · salida",54,3.2f,1.4f);
            foreach(float z in new[]{1f,29f,46f})
            {
                Pipe(new Vector3(-11,5.55f,z),new Vector3(11,5.55f,z),.34f,"Colector transversal");
                for(int s=-1;s<=1;s+=2)Pipe(new Vector3(s*5,5.55f,z),new Vector3(s*5,10.45f,z),.36f,"Montante de colector");
                var valve=Ring("Válvula de colector "+z,new Vector3(0,4.92f,z),.6f,.07f,lime,Quaternion.Euler(90,0,0));
                Tube("Eje válvula",new Vector3(0,4.92f,z),new Vector3(0,5.5f,z),.09f,dark);
            }
            // Refine physical props in place; their Rigidbody, mass and puzzle collider are retained.
            foreach(var body in level.GetComponentsInChildren<Rigidbody>())
            {
                if(body.transform.IsChildOf(art)||body.GetComponent<FoodItem>())continue;
                string n=body.name;
                if(n.Contains("Caja")||n.Contains("caja")||n.Contains("Contrapeso"))
                {
                    var b=body.GetComponent<Collider>().bounds;InstallModel(body.gameObject,"SupplyCrate",b.size);
                    if(n.Contains("Contrapeso"))
                    {
                        var marker=Box("Identificación del contrapeso",body.position+Vector3.back*(b.extents.z+.025f),new Vector3(.6f,.45f,.035f),yellow);marker.transform.SetParent(body.transform,true);
                    }
                }
                else if(n.StartsWith("Barril")||n=="Bote manipulable")
                {var b=body.GetComponent<Collider>().bounds;InstallModel(body.gameObject,"MaintenanceDrum",b.size);}
            }
            foreach(float z in new[]{-20f,8f,28f,43f})
            {
                Prop("Barrera roja manipulable","TrafficBarrier",new Vector3(-8,.48f,z),new Vector3(1.9f,.95f,.8f),true);
                Prop("Cono de mantenimiento","SafetyCone",new Vector3(-6.4f,.43f,z+.7f),new Vector3(.65f,.86f,.65f),true);
                var grate=Prop("Rejilla de drenaje","DrainGrate",new Vector3(-9,.055f,z+2),new Vector3(1.8f,.07f,.8f),false);
                for(int i=0;i<6;i++)
                {
                    var stripe=Box("Marca amarilla de borde",new Vector3(-7+i*.48f,.045f,z+3),new Vector3(.18f,.012f,.55f),i%2==0?yellow:dark);stripe.transform.rotation=Quaternion.Euler(0,-25,0);
                }
            }
            // Teal water with white shoreline, shared shader but an independent LEVEL1 material.
            var water=GameObject.Find("Agua del canal");
            var waterMat=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Agua animada.mat");
            if(!waterMat){waterMat=new Material(Shader.Find("Lizards/SwampWater"));AssetDatabase.CreateAsset(waterMat,Dir+"/Materials/Agua animada.mat");}
            waterMat.SetColor("_BaseColor",new Color(.035f,.61f,.50f));water.GetComponent<Renderer>().sharedMaterial=waterMat;
            for(int s=-1;s<=1;s+=2)Box("Espuma del canal",new Vector3(0,-.98f,1+s*2.86f),new Vector3(23.5f,.018f,.04f),white);
            // Reference-sized food, scene overrides only. Ejection keeps this same scale automatically.
            var sizes=new Dictionary<string,float>{{"Apple",.34f},{"Banana",.47f},{"Corn",.40f},{"Broccoli",.43f},{"Avocado",.40f},{"Pepper",.39f}};
            foreach(var id in Object.FindObjectsByType<FoodCollectionIdentity>())
            {
                var b=FoodCollectionBuilder.VisualBounds(id.gameObject);float h=b.min.y;
                float desired=sizes[id.modelId];id.transform.localScale*=desired/Mathf.Max(b.size.x,b.size.y,b.size.z);
                b=FoodCollectionBuilder.VisualBounds(id.gameObject);id.transform.position+=Vector3.up*(h-b.min.y);
                id.GetComponent<FoodItem>().size=desired*.5f;
                foreach(var r in id.GetComponentsInChildren<Renderer>())
                {
                    var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(FoodCollectionBuilder.Folder+"/Textures/"+id.modelId+"_BaseColor.png");
                    var m=Material("Comida "+id.modelId,Color.white,tex);m.SetFloat("_OutlineWidth",0);m.SetFloat("_ShadowThreshold",.4f);m.SetFloat("_ShadowSoftness",.35f);m.SetColor("_ShadowColor",new Color(.58f,.63f,.76f));r.sharedMaterial=m;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(id.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(id.GetComponent<FoodItem>());
            }
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.64f,.65f,.77f);RenderSettings.ambientEquatorColor=new Color(.47f,.45f,.58f);RenderSettings.ambientGroundColor=new Color(.24f,.25f,.29f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.37f,.31f,.50f);RenderSettings.fogStartDistance=34;RenderSettings.fogEndDistance=95;
            foreach(var l in Object.FindObjectsByType<Light>())if(l.type==LightType.Directional){l.color=new Color(1,.99f,.98f);l.intensity=1.15f;l.shadows=LightShadows.Soft;l.shadowStrength=.6f;l.transform.rotation=Quaternion.Euler(48,-25,0);}
            foreach(var c in Object.FindObjectsByType<Camera>()){c.backgroundColor=RenderSettings.fogColor;c.fieldOfView=60;}
            // A warmer, less shiny food shader and clean scene material references.
            QualitySettings.shadowDistance=65;
            RefineSigns();FinalDetails();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "Applied video reference art pass to LEVEL1. Mission and existing interactions retained.";
        }

        public static void Capture(string file,Vector3 position,Vector3 target,float fov=60)
        {
            var go=new GameObject("Temporary art review camera");var c=go.AddComponent<Camera>();c.transform.position=position;c.transform.LookAt(target);c.fieldOfView=fov;c.nearClipPlane=.05f;c.farClipPlane=160;c.backgroundColor=RenderSettings.fogColor;
            var rt=new RenderTexture(1600,1000,24);rt.Create();RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            var prev=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());RenderTexture.active=prev;
            Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
        }
    }
}
