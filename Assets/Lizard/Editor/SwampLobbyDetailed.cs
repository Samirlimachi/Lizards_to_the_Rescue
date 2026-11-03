using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

namespace Lizards.EditorTools
{
    public static partial class SwampLobbyBuilder
    {
        static Material clay,red,purple,cream,concrete,slime;
        static Vector3[] Profile(float[] radii,float[] heights,int segments=24,float wobble=0)
        {
            var v=new Vector3[radii.Length*segments];for(int j=0;j<radii.Length;j++)for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;float r=radii[j]*(1+wobble*Mathf.Sin(i*3.8f+j*.7f));v[j*segments+i]=new Vector3(Mathf.Cos(a)*r,heights[j],Mathf.Sin(a)*r);}return v;
        }
        static GameObject Lathe(string name,Vector3 p,float[] radii,float[] heights,Material m,int segments=24,bool collision=false)
        {
            var v=Profile(radii,heights,segments);var t=new List<int>();for(int j=0;j<radii.Length-1;j++)for(int i=0;i<segments;i++){int a=j*segments+i,b=j*segments+(i+1)%segments,c=(j+1)*segments+i,d=(j+1)*segments+(i+1)%segments;t.AddRange(new[]{a,c,b,b,c,d});}var g=MeshObject(name,v,t.ToArray(),m,null,collision);g.transform.position=p;return g;
        }
        static GameObject Boulder(Vector3 p,Vector3 size,Material mat)
        {
            const int seg=12;float[] radii={.32f,.78f,1,.87f,.38f},h={-.5f,-.35f,0,.32f,.5f};var v=Profile(radii,h,seg,.14f);var t=new List<int>();for(int j=0;j<4;j++)for(int i=0;i<seg;i++){int a=j*seg+i,b=j*seg+(i+1)%seg,c=(j+1)*seg+i,d=(j+1)*seg+(i+1)%seg;t.AddRange(new[]{a,c,b,b,c,d});}t.AddRange(Enumerable.Range(1,seg-2).SelectMany(i=>new[]{48,48+i+1,48+i}));var g=MeshObject("Roca esculpida",v,t.ToArray(),mat,null,true);g.transform.position=p;g.transform.localScale=size;g.transform.rotation=Quaternion.Euler(R(-8,8),R(0,360),R(-7,7));return g;
        }
        static float DetailedHeight(float x,float z)
        {
            float h=0;
            // Hills lie around the walkable plazas, rather than a perfectly circular island.
            h+=6*Mathf.Exp(-((x+29)*(x+29)/110+(z-9)*(z-9)/300));
            h+=7*Mathf.Exp(-((x-30)*(x-30)/90+(z-13)*(z-13)/250));
            h+=4*Mathf.Exp(-((x+20)*(x+20)/80+(z+26)*(z+26)/70));
            float shopDistance=Mathf.Sqrt((x+22)*(x+22)+(z+3)*(z+3));
            h*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(8,15,shopDistance));
            float pond=x*x/(14*14)+(z-22)*(z-22)/(10*10);
            h-=2*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.75f,1.1f,pond)));
            float edge=Mathf.Max(Mathf.Abs(x)/49,Mathf.Abs(z-10)/53);h-=7*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.86f,1,edge));
            return h;
        }
        static void DetailedTerrain()
        {
            int nx=100,nz=106;var v=new Vector3[(nx+1)*(nz+1)];var cs=new Color[v.Length];var triangles=new List<int>();
            for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++)
            {float x=i-50,z=j-42,y=DetailedHeight(x,z);int index=j*(nx+1)+i;v[index]=new Vector3(x,y,z);float n=Mathf.PerlinNoise(x*.23f,z*.23f);bool path=(Mathf.Abs(x)<3&&z<13&&z>-24)||(z>-5&&z<1&&Mathf.Abs(x)<23)||(z>7&&z<13&&Mathf.Abs(x)<21);cs[index]=(y<-.25f?new Color(.52f,.51f,.36f):path?new Color(.62f,.58f,.38f):Color.Lerp(new Color(.34f,.63f,.26f),new Color(.47f,.70f,.31f),n)).linear;}
            for(int j=0;j<nz;j++)for(int i=0;i<nx;i++){int a=j*(nx+1)+i,b=a+1,c=a+nx+1,d=c+1;triangles.AddRange(new[]{a,c,b,b,c,d});}
            var m=Mat("Terreno detallado",Color.white);m.SetFloat("_VertexColorWeight",1);MeshObject("Terreno · lomas y ensenada",v,triangles.ToArray(),m,cs,true);
        }
        static void DetailedWillow(Vector3 p,float s)
        {
            var tree=Group("Sauce / tronco y cortina");
            var trunkMesh=Lathe("Tronco ensanchado",p,new[]{.9f,.48f,.30f,.20f},new[]{0f,.5f,3f,5.7f},trunk,10);trunkMesh.transform.localScale=Vector3.one*s;trunkMesh.transform.SetParent(tree);
            for(int l=0;l<5;l++)
            {float a=l*2*Mathf.PI/5;Vector3 cap=p+new Vector3(Mathf.Cos(a)*1.1f,5.5f,Mathf.Sin(a)*1.1f)*s;var foliage=Lathe("Copa acampanada",cap,new[]{1.6f,1.8f,1.45f,.8f,.06f},new[]{-1.3f,-.3f,.7f,1.35f,1.6f},l%2==0?leaf:leafLight,14);foliage.transform.localScale=Vector3.one*s;foliage.transform.SetParent(tree);
                for(int k=0;k<9;k++){float angle=k*Mathf.PI*2/9;Vector3 from=cap+new Vector3(Mathf.Cos(angle)*1.5f,-.55f,Mathf.Sin(angle)*1.5f)*s;Vector3 to=from+new Vector3(Mathf.Cos(angle)*.1f,-R(1.6f,3.6f),Mathf.Sin(angle)*.1f)*s;var strip=Tube("Rama colgante",from,to,.055f*s,k%3==0?trunk:leaf,tree,false);}
            }
        }
        static void Mushroom(Vector3 p,float s)
        {
            p.y=DetailedHeight(p.x,p.z);
            var stem=Lathe("Tallo de seta",p,new[]{.22f,.17f,.2f},new[]{0f,.8f,1.3f},cream,8);stem.transform.localScale=Vector3.one*s;
            var cap=Lathe("Seta roja",p+Vector3.up*1.25f*s,new[]{.94f,1.05f,.8f,.3f,.02f},new[]{0f,.18f,.5f,.75f,.78f},red,12);cap.transform.localScale=Vector3.one*s;
            for(int j=0;j<4;j++){float a=j*Mathf.PI*.5f;Sphere("Mota clara",p+new Vector3(Mathf.Cos(a)*.6f,1.85f,Mathf.Sin(a)*.6f)*s,new Vector3(.22f,.06f,.18f)*s,cream,null,false);}
        }
        static void DetailPool()
        {
            Vector3 p=new Vector3(0,0,-18);
            Lathe("Piscina terracota",p,new[]{4.1f,4.9f,5.1f,4.9f,4.2f,4.05f},new[]{-.3f,.1f,.8f,1.15f,1.15f,.65f},clay,48,true);
            Shape("Fondo de piscina",PrimitiveType.Cylinder,p+Vector3.down*.2f,new Vector3(8.3f,.15f,8.3f),slime);
            Shape("Agua verde de aparición",PrimitiveType.Cylinder,p+Vector3.up*.08f,new Vector3(8.1f,.02f,8.1f),slime,null,false);
            Ring("Espuma de piscina",p+Vector3.up*.11f,3.96f,.04f,white);
            Lathe("Nenúfar de aparición",p+new Vector3(0,.15f,0),new[]{1.15f,1.2f,.02f},new[]{0f,.12f,.13f},leafLight,20);
            // A flat collider makes the central pad and the shallow pool walkable.
            var pad=Shape("Soporte del nenúfar",PrimitiveType.Cylinder,p+Vector3.up*.11f,new Vector3(2.4f,.08f,2.4f),leafLight);
            Box("Salida piscina",p+new Vector3(0,.25f,4),new Vector3(2.8f,.5f,1.4f),rockLight);
            for(int i=0;i<9;i++){float a=i*2*Mathf.PI/9;Vector3 at=p+new Vector3(Mathf.Cos(a)*4.9f,.85f,Mathf.Sin(a)*4.9f);Tube("Junta del borde",at,at+Vector3.up*.18f,.035f,trunk,null,false);}
            for(int i=0;i<8;i++){var from=p+new Vector3(R(-3,3),.2f,R(-3,3));if(from.x*from.x+(from.z+18)*(from.z+18)<2)continue;Tube("Hierba de piscina",from,from+new Vector3(.4f,1.5f,.2f),.06f,leafLight,null,false);}
        }
        static void HatHouse(Vector3 p)
        {
            var shop=Group("Tienda con forma de sombrero");
            Shape("Base tienda",PrimitiveType.Cylinder,p+Vector3.up*.18f,new Vector3(10,.18f,9),rockLight,shop);
            // Leave a real doorway between side walls.
            Box("Pared izquierda",p+new Vector3(-2.4f,2,0),new Vector3(2.8f,4,5),trunk,shop);Box("Pared derecha",p+new Vector3(2.4f,2,0),new Vector3(2.8f,4,5),trunk,shop);Box("Fondo interior",p+new Vector3(0,2,2.3f),new Vector3(7.5f,4,.3f),dark,shop);
            Lathe("Tejado naranja",p+Vector3.up*4,new[]{5.3f,5.1f,4.1f,2.7f,.8f,.03f},new[]{0f,.35f,1.2f,2,2.45f,2.5f},orange,36);
            for(int row=0;row<3;row++)for(int i=0;i<18;i++){float a=i*2*Mathf.PI/18;float r=4.75f-row*.8f;var shingle=Box("Teja artesanal",p+new Vector3(Mathf.Sin(a)*r,4.8f+row*.7f,Mathf.Cos(a)*r),new Vector3(1.4f,.16f,1.5f),i%3==0?clay:orange,shop,false);shingle.transform.rotation=Quaternion.Euler(30,a*Mathf.Rad2Deg,0);}
            // Giant dark hat sits above the roof.
            Ring("Ala del gran sombrero",p+Vector3.up*6.3f,3.2f,.42f,dark,shop);
            Lathe("Copa del gran sombrero",p+Vector3.up*6.3f,new[]{2.8f,2.6f,2.4f,2.3f,.03f},new[]{0f,.4f,2,2.35f,2.4f},dark,28);
            foreach(float x in new[]{-1.6f,1.6f}){Sphere("Ventana ojo",p+new Vector3(x,5.7f,-3.2f),new Vector3(1.5f,1.6f,.3f),blue,shop);Ring("Marco ventana",p+new Vector3(x,5.7f,-3.37f),.7f,.12f,glow,shop).transform.rotation=Quaternion.Euler(90,0,0);Tube("Barrote ventana",p+new Vector3(x,5.1f,-3.55f),p+new Vector3(x,6.3f,-3.55f),.06f,glow,shop,false);}
            foreach(float x in new[]{-.7f,.7f}){var curtain=Box("Cortina violeta",p+new Vector3(x,1.9f,-2.45f),new Vector3(.65f,3.3f,.12f),purple,shop,false);Tube("Cinta dorada",p+new Vector3(x-.35f,1.5f,-2.55f),p+new Vector3(x+.35f,1.5f,-2.55f),.04f,gold,shop,false);}
            Shape("Alfombra rosa",PrimitiveType.Cylinder,p+new Vector3(0,.24f,-3.9f),new Vector3(3,.025f,3),pink,shop,false);
            foreach(float x in new[]{-3.2f,3.2f}){Shape("Pedestal de sombrero",PrimitiveType.Cylinder,p+new Vector3(x,.8f,-3.6f),new Vector3(1.25f,.55f,1.25f),gold,shop);Sphere("Cabeza expositora",p+new Vector3(x,1.7f,-3.6f),new Vector3(.8f,.65f,.7f),green,shop,false);}
            Sign("SOMBREROS\n[E] elegir al entrar",p+new Vector3(0,3.65f,-2.75f),4.2f,.8f);
            Tube("Soporte campana",p+new Vector3(-5,0,-3.4f),p+new Vector3(-5,2,-3.4f),.12f,trunk);
            Lathe("Campana dorada",p+new Vector3(-5,1.6f,-3.4f),new[]{.55f,.40f,.25f,.05f},new[]{0f,.2f,.7f,.8f},gold,16);
        }
        static SwampLobbyExpansion Vending(Vector3 p)
        {
            var machine=Group("Máquina expendedora roja");machine.position=p;
            Box("Carcasa roja",p+Vector3.up*2.8f,new Vector3(4.8f,5.6f,1.8f),red,machine);
            Box("Marco plateado",p+new Vector3(-.55f,2.9f,-.94f),new Vector3(3.25f,4.8f,.12f),cream,machine,false);
            Box("Interior lavanda",p+new Vector3(-.55f,2.9f,-1.02f),new Vector3(2.95f,4.5f,.1f),purple,machine,false);
            for(int j=0;j<3;j++)for(int i=0;i<3;i++)
            {Vector3 at=p+new Vector3(-1.5f+i*.95f,1.25f+j*1.35f,-1.15f);Shape("Base cápsula",PrimitiveType.Cylinder,at,new Vector3(.72f,.06f,.72f),leafLight,machine,false);Sphere("Artículo en exposición",at+Vector3.up*.4f,new Vector3(.38f,.55f,.35f),j==0?glow:j==1?orange:green,machine,false);}
            for(int i=0;i<3;i++)Box("Bandeja",p+new Vector3(-.55f,.95f+i*1.35f,-1.17f),new Vector3(3,.12f,.7f),rock,machine,false);
            var manager=Object.FindAnyObjectByType<SwampLobbyExpansion>();
            foreach(var spec in new[]{(-1,new Vector3(1.0f,2.25f,-1.04f)),(1,new Vector3(1.85f,2.25f,-1.04f)),(0,new Vector3(1.43f,1.05f,-1.04f))})
            {var button=Box(spec.Item1==0?"Comprar con lengua":"Seleccionar con lengua",p+spec.Item2,new Vector3(.65f,.65f,.2f),spec.Item1==0?green:gold,machine);button.AddComponent<TongueTarget>();var b=button.AddComponent<SwampVendorButton>();b.vendor=manager;b.action=spec.Item1;}
            foreach(float x in new[]{1.12f,1.75f})Sphere("Ojo de la máquina",p+new Vector3(x,3.6f,-1.06f),Vector3.one*.4f,white,machine,false);
            Box("Bandeja de entrega",p+new Vector3(-.8f,.4f,-1.35f),new Vector3(2,.25f,.8f),dark,machine);
            manager.vendingLabel=Sign("EXPENDEDORA",p+new Vector3(-.5f,6.3f,-.8f),5,1.2f);manager.vending=machine;return manager;
        }
        static void PhoneBooth(Vector3 p)
        {
            var phone=Group("Cabina telefónica violeta");phone.position=p;
            Box("Base cabina",p+Vector3.up*.1f,new Vector3(2,.2f,2),dark,phone);
            foreach(float x in new[]{-.9f,.9f})foreach(float z in new[]{-.9f,.9f})Box("Columna",p+new Vector3(x,1.8f,z),new Vector3(.15f,3.6f,.15f),purple,phone);
            Box("Techo cabina",p+Vector3.up*3.7f,new Vector3(2.2f,.25f,2.2f),purple,phone);Box("Panel teléfono",p+new Vector3(0,1.8f,.7f),new Vector3(1.7f,3.4f,.3f),purple,phone);
            for(int j=0;j<4;j++)for(int i=0;i<3;i++)Box("Ventana",p+new Vector3(-.58f+i*.58f,1+j*.55f,.49f),new Vector3(.4f,.4f,.025f),cream,phone,false);
            Shape("Cartel redondo teléfono",PrimitiveType.Cylinder,p+Vector3.up*4.3f,new Vector3(1.6f,.09f,1.6f),white,phone,false).transform.rotation=Quaternion.Euler(90,0,0);
            Sign("SALA\n[E]",p+new Vector3(0,3.6f,-1),1.7f,.65f);
        }
        static void IndustrialBackdrop()
        {
            Box("Fachada de la fábrica",new Vector3(6,7,48),new Vector3(86,14,3),concrete);
            Box("Cornisa de fábrica",new Vector3(6,14,46.6f),new Vector3(88,.8f,3),rock);
            foreach(float x in new[]{-26f,-8f,12f,31f})
            {Tube("Tubería vertical",new Vector3(x,3,45.5f),new Vector3(x,15,45.5f),.8f,blue);Tube("Tubería saliente",new Vector3(x,15,45.5f),new Vector3(x,15,39),.8f,blue);for(int j=0;j<4;j++)Ring("Abrazadera",new Vector3(x,4+j*3,45.5f),.84f,.12f,dark);}
            foreach(float x in new[]{-15f,23f})
            {var fan=Group("Ventilador industrial");var rim=Ring("Marco del ventilador",new Vector3(x,6,46.2f),3.4f,.35f,dark);rim.transform.rotation=Quaternion.Euler(90,0,0);var blades=Group("Aspas giratorias");blades.position=new Vector3(x,6,45.9f);blades.gameObject.AddComponent<LevelOneMotion>().spin=new Vector3(0,0,24);for(int j=0;j<4;j++){float a=j*90;var blade=Box("Aspa",new Vector3(x,7.5f,45.9f),new Vector3(1.2f,2.4f,.25f),blue,blades,false);blade.transform.RotateAround(blades.position,Vector3.forward,a);}Sphere("Eje",new Vector3(x,6,45.7f),Vector3.one*.8f,rockLight,blades,false);}
            // More distant angular mountain silhouettes.
            for(int i=0;i<15;i++)Boulder(new Vector3(-65+i*9,10,62+R(-3,8)),new Vector3(8,R(15,25),10),rock);
        }
        static void Trolley(Vector3 p)
        {
            var cart=new GameObject("Carrito de provisiones");cart.transform.SetParent(root);cart.transform.position=p+Vector3.up*.65f;Box("Base del carrito",cart.transform.position,new Vector3(2,.18f,1.3f),rock,cart.transform);cart.AddComponent<Rigidbody>().mass=5;cart.AddComponent<TongueTarget>().kind=TongueTargetKind.Object;
            foreach(float x in new[]{-.85f,.85f})foreach(float z in new[]{-.5f,.5f}){var wheel=Shape("Rueda",PrimitiveType.Cylinder,p+new Vector3(x,.3f,z),new Vector3(.38f,.12f,.38f),dark,cart.transform,false);wheel.transform.rotation=Quaternion.Euler(0,0,90);wheel.AddComponent<SphereCollider>().radius=.5f;}
            foreach(float x in new[]{-1f,1f}){Tube("Barra carrito",p+new Vector3(x,.6f,0),p+new Vector3(x,1.9f,0),.06f,rock,cart.transform,false);Tube("Borde carrito",p+new Vector3(x,1.5f,-.6f),p+new Vector3(x,1.5f,.6f),.06f,rock,cart.transform,false);}
            Box("Placa carrito",p+new Vector3(0,1,-.72f),new Vector3(1.6f,.5f,.06f),gold,cart.transform,false);
        }
        static void Hud(SwampLobbyExpansion expansion)
        {
            var go=new GameObject("HUD del refugio",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            UnityEngine.UI.Text Text(string name,Vector2 anchor,Vector2 pivot,Vector2 offset,Vector2 size,int font)
            {var g=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));g.transform.SetParent(go.transform,false);var rt=g.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot;rt.anchoredPosition=offset;rt.sizeDelta=size;g.GetComponent<UnityEngine.UI.Image>().color=new Color(.06f,.12f,.17f,.85f);var child=new GameObject("Texto",typeof(RectTransform),typeof(UnityEngine.UI.Text));child.transform.SetParent(g.transform,false);var cr=child.GetComponent<RectTransform>();cr.anchorMin=Vector2.zero;cr.anchorMax=Vector2.one;cr.offsetMin=new Vector2(12,8);cr.offsetMax=new Vector2(-12,-8);var text=child.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=font;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;return text;}
            expansion.coinsLabel=Text("Monedas y día",Vector2.one,Vector2.one,new Vector2(-24,-24),new Vector2(260,58),26);
            expansion.inventoryLabel=Text("Inventario / tres espacios",Vector2.right,Vector2.right,new Vector2(-24,30),new Vector2(760,58),18);
        }
        [MenuItem("Lizards/Lobby/Rehacer lobby detallado (referencias)")]
        public static void BuildDetailed()
        {
            Build();var previous=root;previous.name="__Distribución base archivada";previous.gameObject.SetActive(false);root=new GameObject("LOBBY / reconstrucción detallada").transform;random=new System.Random(72);
            clay=Mat("Terracota",new Color(.65f,.29f,.25f));red=Mat("Rojo máquina",new Color(.88f,.10f,.09f));purple=Mat("Violeta cabina",new Color(.37f,.27f,.62f));cream=Mat("Crema",new Color(.84f,.83f,.69f));concrete=Mat("Hormigón",new Color(.37f,.38f,.43f));slime=Mat("Piscina verde",new Color(.1f,.73f,.3f));
            var sun=new GameObject("Sol del pantano").AddComponent<Light>();sun.transform.SetParent(root);sun.type=LightType.Directional;sun.intensity=1.55f;sun.shadows=LightShadows.Soft;sun.color=new Color(1,.95f,.82f);sun.transform.rotation=Quaternion.Euler(44,-28,0);RenderSettings.fogStartDistance=85;RenderSettings.fogEndDistance=190;
            DetailedTerrain();Box("Agua de la ensenada",new Vector3(0,-.65f,25),new Vector3(160,.05f,150),water,null,false);DetailPool();IndustrialBackdrop();HatHouse(new Vector3(-22,0,-3));PhoneBooth(new Vector3(8,0,-5));
            var system=Object.FindAnyObjectByType<SwampLobbyHub>();var expansion=system.gameObject.AddComponent<SwampLobbyExpansion>();Vending(new Vector3(14,0,-4));expansion.colors=new GameObject("Selector de color").transform;expansion.colors.SetParent(root);expansion.colors.position=new Vector3(-10,0,-10);expansion.bell=new GameObject("Tocar campana").transform;expansion.bell.SetParent(root);expansion.bell.position=new Vector3(-27,0,-6.4f);
            // Color wheel and eight swatches near the spawn pool.
            var wheel=Shape("Rueda de colores",PrimitiveType.Cylinder,new Vector3(-10,1.8f,-10),new Vector3(3.3f,.14f,3.3f),white);wheel.transform.rotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var m=Mat("Muestra"+i,Color.HSVToRGB(i/8f,.65f,.9f));Sphere("Muestra de color",new Vector3(-10+Mathf.Cos(a)*1.2f,1.8f+Mathf.Sin(a)*1.2f,-10.2f),Vector3.one*.7f,m,null,false);}Sign("COLOR\n[E] cambiar",new Vector3(-10,3.8f,-10),3,1);
            // Orange octagonal raft in front of the large blue collection bowl.
            var boat=Group("Balsa naranja de salida");boat.position=new Vector3(2,0,18);
            var deck=Shape("Cubierta de la balsa",PrimitiveType.Cylinder,new Vector3(2,-.05f,18),new Vector3(8,.18f,6.8f),dark,boat);Ring("Borde de la balsa",new Vector3(2,.45f,18),3.8f,.48f,orange,boat).transform.localScale=new Vector3(1,1,.85f);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var at=new Vector3(2+Mathf.Cos(a)*3.85f,.8f,18+Mathf.Sin(a)*3.3f);var handle=Box("Asa negra",at,new Vector3(.6f,.12f,.17f),dark,boat,false);handle.transform.rotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,0);}
            var dock=Group("Plataforma de embarque");Box("Plataforma elevada",new Vector3(8,1.25f,13.5f),new Vector3(6,.4f,4),rock,dock);for(int i=0;i<6;i++)Box("Escalón muelle",new Vector3(8,.12f+i*.18f,9+i*.6f),new Vector3(3,.25f,.7f),rock,dock);
            foreach(float x in new[]{5.4f,10.6f}){Tube("Pata muelle",new Vector3(x,-1,13.5f),new Vector3(x,1.5f,13.5f),.17f,rock,dock);Tube("Baranda dorada",new Vector3(x,2.2f,12),new Vector3(x,2.2f,14),.09f,gold,dock);}
            Box("Tablón de acceso balsa",new Vector3(2,.05f,13),new Vector3(2.6f,.2f,3.8f),wood);
            // Mouth of the sewer and the six-metre collection bowl.
            var bowl=Group("Cuenco de provisiones");bowl.position=new Vector3(-5,0,24);Lathe("Cuenco azul",new Vector3(-5,-.4f,24),new[]{4.6f,5.1f,5.2f,5.05f,4.6f},new[]{0f,.5f,1.7f,2.2f,2.25f},blue,64);Ring("Borde naranja del cuenco",new Vector3(-5,1.8f,24),4.9f,.35f,orange);Shape("Fondo cuenco",PrimitiveType.Cylinder,new Vector3(-5,.15f,24),new Vector3(9,.15f,9),dark);
            var goal=new GameObject("Entrega al guardián");goal.transform.SetParent(root);goal.transform.position=new Vector3(-5,.4f,24);var gc=goal.AddComponent<BoxCollider>();gc.isTrigger=true;gc.size=new Vector3(8,3,8);goal.AddComponent<FoodGoal>();
            var king=King();king.position+=new Vector3(0,0,6); // geometry is parented, so throne moves as a unit
            // Throne is built around the seated body, with arms, moss and a television antenna.
            foreach(var block in new[]{new Vector3(0,5,36),new Vector3(-5,2.5f,32),new Vector3(5,2.5f,32)})Box("Trono de piedra",block,new Vector3(block.x==0?11:2,block.x==0?11:5,3),rock);
            for(int i=0;i<18;i++){float x=R(-5.5f,5.5f),z=R(29,37);if(Mathf.Abs(x)<3&&z<34)continue;Sphere("Musgo del trono",new Vector3(x,R(.5f,3),z),Vector3.one*R(.3f,.8f),leafLight,null,false);}
            Tube("Antena",new Vector3(3,10.4f,36),new Vector3(4,13,36),.07f,dark,null,false);for(int i=0;i<3;i++)Tube("Varilla antena",new Vector3(3.1f,11+i*.6f,36),new Vector3(4.8f,11+i*.6f,36),.07f,dark,null,false);
            var portal=Ring("Arco de alcantarilla",new Vector3(22,3,30),4,.5f,rock);portal.transform.rotation=Quaternion.Euler(90,0,0);for(int i=-3;i<=3;i++)Tube("Reja",new Vector3(22+i,.2f,30),new Vector3(22+i,5.7f,30),.10f,dark);
            // A service platform and the trolley visible between pool and harbour.
            Box("Plataforma de mantenimiento",new Vector3(-4,.6f,6),new Vector3(11,1.2f,6),dark);for(int i=0;i<15;i++){var stripe=Box("Franja de seguridad",new Vector3(-9+i*.7f,1.22f,3.2f),new Vector3(.35f,.025f,.5f),gold,null,false);stripe.transform.rotation=Quaternion.Euler(0,-30,0);}Trolley(new Vector3(7,0,2));
            Box("Rampa de mantenimiento",new Vector3(-4,.55f,1.5f),new Vector3(11,.25f,3.6f),dark).transform.rotation=Quaternion.Euler(-20,0,0);
            for(int i=0;i<24;i++){float a=R(0,Mathf.PI*2);float x=Mathf.Cos(a)*R(14,19),z=22+Mathf.Sin(a)*R(9,13);if(z<12&&Mathf.Abs(x)<10)continue;Boulder(new Vector3(x,DetailedHeight(x,z)+.6f,z),new Vector3(R(1.6f,4),R(1.4f,3.5f),R(1.6f,3)),i%3==0?rock:rockLight);}
            for(int i=0;i<18;i++){float x=i%2==0?R(-40,-27):R(29,42),z=R(-26,40);Boulder(new Vector3(x,DetailedHeight(x,z)+1,z),new Vector3(R(3,7),R(4,9),R(3,6)),i%2==0?rock:rockLight);}
            foreach(var p in new[]{new Vector3(-33,0,-16),new Vector3(28,0,-14),new Vector3(-32,0,16),new Vector3(32,0,12),new Vector3(-18,0,36),new Vector3(15,0,38),new Vector3(-41,0,29),new Vector3(42,0,32)}){var at=p;at.y=DetailedHeight(p.x,p.z);DetailedWillow(at,R(.95f,1.3f));}
            foreach(var p in new[]{new Vector3(21,0,-5),new Vector3(23,0,-1),new Vector3(18,0,-8),new Vector3(-16,0,16),new Vector3(-19,0,19),new Vector3(29,0,23),new Vector3(-30,0,-8)})Mushroom(p,R(.7f,1.8f));
            for(int i=0;i<70;i++){float x=R(-40,41),z=R(-28,42),y=DetailedHeight(x,z);if(y<-.3f||Mathf.Abs(x)<13&&z<12)continue;Sphere("Arbusto de ribera",new Vector3(x,y+.3f,z),new Vector3(R(.6f,1.7f),R(.5f,1),R(.6f,1.4f)),leafLight,null,false);for(int j=0;j<3;j++)Tube("Hierba",new Vector3(x+j*.16f,y,z),new Vector3(x+j*.16f+.18f,y+.7f,z),.025f,leaf,null,false);}
            // Lilypads, near-shore foam and a movable trampoline.
            for(int i=0;i<18;i++){float a=i*Mathf.PI*2/18;var at=new Vector3(Mathf.Cos(a)*12,-.59f,22+Mathf.Sin(a)*8);Shape("Nenúfar",PrimitiveType.Cylinder,at,new Vector3(.7f,.025f,.8f),leafLight,null,false);}
            var trampoline=Shape("Cama elástica del refugio",PrimitiveType.Cylinder,new Vector3(15,.35f+DetailedHeight(15,9),9),new Vector3(3,.15f,3),dark);var trb=trampoline.AddComponent<Rigidbody>();trb.mass=8;trb.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;trampoline.AddComponent<TongueTarget>().kind=TongueTargetKind.Object;trampoline.AddComponent<SwampBounce>();Ring("Borde cama elástica",new Vector3(15,.6f+DetailedHeight(15,9),9),1.4f,.08f,purple).transform.SetParent(trampoline.transform,true);
            // Keep the working social and departure systems, replacing their anchors.
            system.spawn=new Vector3(0,.35f,-18);system.roomMenu.spawnPoint=system.spawn;system.boat=boat;system.telephone=root.Find("Cabina telefónica violeta");system.wardrobe=new GameObject("Entrada del vestuario").transform;system.wardrobe.SetParent(root);system.wardrobe.position=new Vector3(-22,0,-6.4f);system.king=new GameObject("Hablar con el rey").transform;system.king.SetParent(root);system.king.position=new Vector3(0,0,27);system.kingBelly=king.Find("Barriga");system.departureBoard=Sign("BALSA DE EXPEDICIÓN",new Vector3(8,3.3f,13.5f),4.8f,1.3f);system.playersBoard=Sign("JUGADORES",new Vector3(10,2,6),5,2.5f);
            var player=LizardPlayer.All.FirstOrDefault(x=>x.IsControlled)??Object.FindObjectsByType<LizardPlayer>().First(x=>x.GetComponent<LizardInput>().controlled);player.transform.position=system.spawn;
            var cam=Object.FindAnyObjectByType<LizardCameraRig>();cam.transform.position=new Vector3(0,3,-23);cam.transform.LookAt(new Vector3(0,1,-16));
            for(int i=0;i<8;i++)Prop("Provisiones manipulables",new Vector3(-12+i%4*1.3f,.6f,-2+i/4*1.4f),1,i%3==0);
            var supply=new GameObject("Fuente de fruta");supply.transform.SetParent(root);supply.transform.position=new Vector3(-12,.2f,-4);supply.AddComponent<FoodSpawner>();
            FinishDetails();PolishDetails();Hud(expansion);EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();Debug.Log("[Lobby V2] Distribución detallada construida a partir de referencias.");
        }
        public static void FinishDetails()
        {
            root=GameObject.Find("LOBBY / reconstrucción detallada").transform;
            white=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Marfil.mat");
            var foam=Ring("Espuma de la orilla",new Vector3(0,-.60f,22),13.5f,.045f,white);foam.transform.localScale=new Vector3(1,1,.71f);
            var wake=Ring("Estela de la balsa",new Vector3(2,-.58f,18),4.2f,.04f,white);wake.transform.localScale=new Vector3(1,1,.85f);
            var obj=new GameObject("Motas del pantano");obj.transform.SetParent(root);obj.transform.position=new Vector3(0,2,8);var ps=obj.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=8;main.startSpeed=.06f;main.startSize=.045f;main.startColor=new Color(.9f,1,.85f,.55f);main.maxParticles=100;main.simulationSpace=ParticleSystemSimulationSpace.World;var emission=ps.emission;emission.rateOverTime=7;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(65,10,70);var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=LizardMaterials.Particle();r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            QualitySettings.shadowDistance=95;
        }
        public static void PolishDetails()
        {
            root=GameObject.Find("LOBBY / reconstrucción detallada").transform;
            var vendor=Object.FindAnyObjectByType<SwampLobbyExpansion>();gold=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Oro.mat");dark=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Oscuro.mat");
            foreach(var spec in new[]{(-3,new Vector3(1.43f,2.95f,-1.04f)),(3,new Vector3(1.43f,1.55f,-1.04f))})
            {
                if(Object.FindObjectsByType<SwampVendorButton>().Any(b=>b.vendor==vendor&&b.action==spec.Item1))continue;
                var button=Box("Seleccionar fila con lengua",vendor.vending.position+spec.Item2,new Vector3(.65f,.65f,.2f),gold,vendor.vending);button.AddComponent<TongueTarget>();var target=button.AddComponent<SwampVendorButton>();target.vendor=vendor;target.action=spec.Item1;
            }
            foreach(var button in Object.FindObjectsByType<SwampVendorButton>())
            {
                if(button.action==0){button.transform.position=vendor.vending.position+new Vector3(1.43f,.65f,-1.04f);continue;}
                if(button.transform.Find("Flecha"))continue;
                var arrow=MeshObject("Flecha",new[]{new Vector3(-.14f,-.13f,0),new Vector3(.14f,-.13f,0),new Vector3(0,.15f,0)},new[]{0,2,1},dark);arrow.transform.SetParent(button.transform,false);arrow.transform.localPosition=new Vector3(0,0,-.65f);arrow.transform.localScale=new Vector3(1.3f,1.3f,1);arrow.transform.localRotation=Quaternion.Euler(0,0,button.action==-3?0:button.action==3?180:button.action<0?90:-90);
            }
            var particle=GameObject.Find("Motas del pantano")?.GetComponent<ParticleSystemRenderer>();
            if(particle){string path=Dir+"/Materials/Motas.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(LizardMaterials.Particle());AssetDatabase.CreateAsset(material,path);}particle.sharedMaterial=material;}
        }
        public static void RefitGround()
        {
            var terrain=GameObject.Find("Terreno · lomas y ensenada");var mesh=terrain.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++)vertices[i].y=DetailedHeight(vertices[i].x,vertices[i].z);
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();var collider=terrain.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;EditorUtility.SetDirty(mesh);
            var platform=GameObject.Find("Cama elástica del refugio");float shift=DetailedHeight(15,9);if(platform)platform.transform.position=new Vector3(15,.35f+shift,9);
            var roots=GameObject.Find("LOBBY / reconstrucción detallada").GetComponentsInChildren<Transform>();
            foreach(var stem in roots.Where(t=>t.name=="Tallo de seta"))
            {var p=stem.position;float dy=DetailedHeight(p.x,p.z)-p.y;foreach(var part in roots.Where(t=>t.name=="Tallo de seta"||t.name=="Seta roja"||t.name=="Mota clara")){var d=part.position-p;d.y=0;if(d.sqrMagnitude<4)part.position+=Vector3.up*dy;}}
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        public static void StabilizeProps()
        {
            var trampoline=GameObject.Find("Cama elástica del refugio");if(trampoline)trampoline.GetComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            var cart=GameObject.Find("Carrito de provisiones");if(cart&&cart.GetComponent<MeshRenderer>())
            {
                var children=cart.transform.Cast<Transform>().ToArray();var positions=children.Select(t=>t.position).ToArray();cart.transform.localScale=Vector3.one;
                for(int i=0;i<children.Length;i++){children[i].position=positions[i];if(children[i].name=="Rueda"&&!children[i].GetComponent<Collider>())children[i].gameObject.AddComponent<SphereCollider>().radius=.5f;}
                cart.GetComponent<MeshRenderer>().enabled=false;cart.GetComponent<Collider>().enabled=false;
                root=GameObject.Find("LOBBY / reconstrucción detallada").transform;rock=AssetDatabase.LoadAssetAtPath<Material>(Dir+"/Materials/Roca.mat");Box("Base del carrito",cart.transform.position,new Vector3(2,.18f,1.3f),rock,cart.transform);
            }
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
    }
}
