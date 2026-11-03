using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Lizards.EditorTools
{
    public static class LevelOneBuilder
    {
        const string ScenePath = "Assets/Scenes/LEVEL1.unity";
        const string AssetRoot = "Assets/Lizard/LevelOne";
        static Transform root;
        static Material floor, tile, wall, trim, orange, yellow, metal, black, mint, pink, water, glow, wood;
        static int serial;
        static GameObject Shape(string name, PrimitiveType shape, Vector3 p, Vector3 s, Material material, Transform parent = null, bool collision = true)
        {
            var g = GameObject.CreatePrimitive(shape); g.name = name;
            g.transform.SetParent(parent ? parent : root, false); g.transform.position = p; g.transform.localScale = s;
            g.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        static GameObject Box(string n, Vector3 p, Vector3 s, Material m, Transform t = null, bool c = true) => Shape(n, PrimitiveType.Cube, p, s, m, t, c);
        static Material Mat(string n, Color c, float smooth = .2f, bool emissive = false)
        {
            string path = AssetRoot + "/Materials/" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = LizardMaterials.Lit(c, smooth, emissive ? c * .6f : Color.black); AssetDatabase.CreateAsset(m, path); }
            return m;
        }
        static Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root); return g; }
        static TextMesh Sign(string text, Vector3 p, float width = 5, float size = .18f)
        {
            var frame = Box("Panel · " + text.Split('\n')[0], p, new Vector3(width, 1.5f, .18f), metal);
            var obj = new GameObject("Texto"); obj.transform.SetParent(frame.transform, false);
            // World scale independent of the board geometry.
            obj.transform.SetParent(root, true); obj.transform.localScale=Vector3.one; obj.transform.position = p + Vector3.back * .12f;
            obj.transform.rotation = Quaternion.Euler(0, 180, 0);
            var t = obj.AddComponent<TextMesh>();t.text = text;t.fontSize = 64;t.characterSize = size*.28f;t.anchor = TextAnchor.MiddleCenter;t.alignment = TextAlignment.Center;t.color = new Color(.88f, 1, .94f);
            // TextMesh front points toward -Z with identity rotation.
            obj.transform.rotation = Quaternion.identity;
            return t;
        }
        static GameObject Phys(string n, Vector3 p, Vector3 s, float mass, Material mat, bool barrel = false)
        {
            var g = Shape(n, barrel ? PrimitiveType.Cylinder : PrimitiveType.Cube, p, s, mat);
            var rb = g.AddComponent<Rigidbody>(); rb.mass = mass;rb.interpolation = RigidbodyInterpolation.Interpolate;rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;rb.linearDamping = .3f;
            g.AddComponent<TongueTarget>().kind = TongueTargetKind.Object;
            if (barrel)
                foreach (float offset in new[] { -.65f, .65f })
                {
                    var band = Shape("Aro", PrimitiveType.Cylinder, p + Vector3.up * s.y * offset, new Vector3(s.x * 1.04f, .06f, s.z * 1.04f), metal, root, false);
                    band.transform.SetParent(g.transform, true);
                }
            else
                foreach (float offset in new[] { -.32f, .32f })
                {
                    var plank = Box("Refuerzo", p + Vector3.up * s.y * offset + Vector3.back * (s.z / 2 + .02f), new Vector3(s.x * 1.02f, .12f, .06f), orange, root, false);
                    plank.transform.SetParent(g.transform, true);
                }
            return g;
        }
        static void Tube(string name, Vector3 a, Vector3 b, float radius, Material m, bool collision = true)
        {
            var v = b - a;var g = Shape(name, PrimitiveType.Cylinder, (a + b) / 2, new Vector3(radius * 2, v.magnitude / 2, radius * 2), m, null, collision);
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, v.normalized);
        }
        static GameObject Arch(string name, Vector3 center, float radius, float tubeRadius, Material material, bool full = false)
        {
            const int sides = 8;int steps = full ? 48 : 24;var vertices = new Vector3[(steps + 1) * sides];var triangles = new int[steps * sides * 6];
            for (int j = 0; j <= steps; j++)
            {
                float a = j / (float)steps * Mathf.PI * (full ? 2 : 1);var outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int i = 0; i < sides; i++) { float b = i * Mathf.PI * 2 / sides;vertices[j * sides + i] = outward * (radius + tubeRadius * Mathf.Cos(b)) + Vector3.forward * tubeRadius * Mathf.Sin(b); }
            }
            int k = 0;
            for (int j = 0; j < steps; j++) for (int i = 0; i < sides; i++)
            { int a = j*sides+i,b = j*sides+(i+1)%sides,c = (j+1)*sides+i,d = (j+1)*sides+(i+1)%sides;triangles[k++]=a;triangles[k++]=c;triangles[k++]=b;triangles[k++]=b;triangles[k++]=c;triangles[k++]=d; }
            var mesh = new Mesh { name = name };mesh.vertices = vertices;mesh.triangles = triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetRoot + "/Meshes/Arch" + serial++ + ".asset");
            var g = new GameObject(name);g.transform.SetParent(root);g.transform.position=center;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=material;
            return g;
        }
        static void CheckpointAt(float z)
        {
            var g = new GameObject("Checkpoint " + z);g.transform.SetParent(root);g.transform.position = new Vector3(0, .3f, z);
            var trigger = g.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(18,2,1);g.AddComponent<Checkpoint>();
        }
        static void Food(Vector3 p, int count, float value = 1)
        {
            for(int i=0;i<count;i++)
            {
                var f=FoodFactory.CreateFruit(p+new Vector3((i%4)*.7f, .3f, (i/4)*.7f),value,.20f,pink);f.name="Fruta · comer [E] / arrastrar";f.transform.SetParent(root);
                var leaf=Shape("Hoja",PrimitiveType.Sphere,f.transform.position+Vector3.up*.20f,new Vector3(.2f,.04f,.1f),mint,null,false);leaf.transform.SetParent(f.transform,true);
            }
        }
        static void Recovery(Vector3 p)
        {
            var g=Shape("Fuente de recuperación",PrimitiveType.Cylinder,p,new Vector3(1.5f,.12f,1.5f),mint);
            var t=g.AddComponent<SphereCollider>();t.isTrigger=true;t.radius=.8f;g.AddComponent<RescuePoint>();
            var beacon=Shape("Baliza",PrimitiveType.Sphere,p+Vector3.up*.8f,Vector3.one*.25f,glow,null,false);beacon.AddComponent<LevelOneMotion>().spin=new Vector3(0,40,0);
        }
        [MenuItem("Lizards/Level 1/Construir estación de rescate")]
        public static void Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Salir de Play antes de construir.");
            var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(active.isDirty)EditorSceneManager.SaveScene(active);
            Directory.CreateDirectory(AssetRoot+"/Materials");Directory.CreateDirectory(AssetRoot+"/Meshes");Directory.CreateDirectory("Captures");AssetDatabase.Refresh();
            // Preserve both the target scene and the scene currently being edited.
            string stamp=DateTime.Now.ToString("yyyyMMdd_HHmmss");Directory.CreateDirectory("Backups/LEVEL1");
            File.Copy(ScenePath,"Backups/LEVEL1/LEVEL1_"+stamp+".unity");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var oldRoots=scene.GetRootGameObjects();
            var archive=new GameObject("__Original_LEVEL1_"+stamp);foreach(var g in oldRoots)g.transform.SetParent(archive.transform);archive.SetActive(false);
            root=new GameObject("LEVEL 1 · Estación de bombeo / entorno").transform;serial=0;
            // Use unique geometry asset names on rebuild; preserve references of old backups.
            serial=Directory.GetFiles(AssetRoot+"/Meshes","*.asset").Length;
            floor=Mat("Basalto",new Color(.13f,.20f,.25f));tile=Mat("Baldosa",new Color(.24f,.34f,.36f));wall=Mat("Lavanda",new Color(.43f,.35f,.63f));trim=Mat("Lavanda clara",new Color(.66f,.57f,.79f));orange=Mat("Naranja",new Color(1,.39f,.08f));yellow=Mat("Amarillo",new Color(1,.78f,.14f));metal=Mat("Metal",new Color(.09f,.16f,.21f),.45f);black=Mat("Oscuro",new Color(.035f,.06f,.08f));mint=Mat("Menta",new Color(.15f,.80f,.59f));pink=Mat("Fruta",new Color(1,.23f,.38f));water=Mat("Agua",new Color(.035f,.56f,.61f),.9f);glow=Mat("Luz",new Color(.3f,1,.82f),.4f,true);wood=Mat("Madera",new Color(.63f,.37f,.19f));
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.67f,.76f,.85f);RenderSettings.ambientEquatorColor=new Color(.42f,.48f,.58f);RenderSettings.ambientGroundColor=new Color(.22f,.25f,.32f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.37f,.51f,.63f);RenderSettings.fogStartDistance=55;RenderSettings.fogEndDistance=130;
            var sun=new GameObject("Sol · luz cálida").AddComponent<Light>();sun.transform.SetParent(root);sun.type=LightType.Directional;sun.intensity=1.65f;sun.color=new Color(1,.92f,.78f);sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(42,-28,0);
            // A compact, continuous route. Floors stop at the swing canal.
            Box("Cimientos",new Vector3(0,-2.1f,17),new Vector3(31,1,84),metal);
            Box("Muelle de entrada",new Vector3(0,-.5f,-15),new Vector3(24,1,26),floor);
            Box("Almacén y rescate",new Vector3(0,-.5f,31),new Vector3(24,1,54),floor);
            Box("Canal · fondo recuperable",new Vector3(0,-1.6f,1),new Vector3(24,.3f,6),floor);
            Box("Agua del canal",new Vector3(0,-1.05f,1),new Vector3(24,.12f,5.8f),water,null,false);
            // Side walkway is an accessible alternative to swinging.
            Box("Pasarela lateral",new Vector3(-10,0,1),new Vector3(3,.35f,6.5f),tile);
            for(int z=-26;z<59;z+=3)for(int x=-9;x<=9;x+=3)
                if(z<-2||z>4)Box("Junta de baldosa",new Vector3(x,.005f,z),new Vector3(2.94f,.025f,2.94f),((x+z)%2==0)?floor:tile,null,false);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Muro lateral",new Vector3(side*12.3f,2.4f,16),new Vector3(.8f,4.8f,84),wall);
                Box("Cornisa",new Vector3(side*12.3f,4.85f,16),new Vector3(1.1f,.25f,84),trim);
                Tube("Conducto principal",new Vector3(side*10.8f,3.7f,-25),new Vector3(side*10.8f,3.7f,56),.30f,side<0?mint:orange);
                for(int z=-24;z<57;z+=6)
                {
                    Box("Pilar",new Vector3(side*11.8f,2,z),new Vector3(.7f,4,.9f),trim);
                    Tube("Abrazadera",new Vector3(side*10.8f,3.7f,z-.10f),new Vector3(side*10.8f,3.7f,z+.10f),.37f,metal);
                    Box("Luz de pared",new Vector3(side*11.75f,2.8f,z+2),new Vector3(.15f,.25f,1),glow,null,false);
                }
            }
            Box("Muro entrada",new Vector3(0,2,-28),new Vector3(25,4,.7f),wall);
            Box("Muro final",new Vector3(0,2,58),new Vector3(25,4,.7f),wall);
            for(int z=-23;z<58;z+=16)
            {
                Arch("Arco de infraestructura",new Vector3(0,4,z),12,.35f,trim);
                for(int side=-1;side<=1;side+=2)Box("Base arco",new Vector3(side*12,2,z),new Vector3(.7f,4,.7f),trim);
            }
            Sign("01 / ESTACIÓN DE BOMBEO\nRecupera provisiones. Rescata a tu compañero.",new Vector3(0,5.7f,-10),10,.22f);
            Sign("MUELLE\nWASD mover · ESPACIO saltar\nE comer · G compartir comida",new Vector3(-7,2,-17),5,.13f);
            CheckpointAt(-24);Food(new Vector3(2,0,-19),8);
            Phys("Caja ligera",new Vector3(-5,.6f,-12),Vector3.one*1.1f,3,wood);Phys("Barril",new Vector3(6,.65f,-11),new Vector3(.8f,.6f,.8f),4,orange,true);
            Recovery(new Vector3(8,.15f,-22));
            // Swing points and stepping route over a shallow, recoverable canal.
            Sign("CANAL / LENGUA\nMantén clic izquierdo en el aro amarillo\nR / F ajustan la longitud · suelta para impulsarte",new Vector3(0,3.6f,-4.5f),8,.16f);
            for(int i=0;i<3;i++)
            {
                float x=-2+i*2;var ring=Arch("Enganche amarillo",new Vector3(x,5,1+i*.7f),.45f,.09f,yellow,true);
                var c=ring.AddComponent<SphereCollider>();c.radius=.56f;ring.AddComponent<TongueTarget>().kind=TongueTargetKind.GrapplePoint;
                Tube("Soporte del enganche",new Vector3(x,5.5f,1+i*.7f),new Vector3(x,8,1+i*.7f),.05f,metal);
            }
            foreach(float x in new[]{-5f,0f,5f})Box("Piedra de salto",new Vector3(x,-.22f,1),new Vector3(2.2f,.5f,2.2f),trim);
            var ferry=Box("Plataforma móvil",new Vector3(-7,-.4f,1),new Vector3(2,.4f,2),orange);var ferryRb=ferry.AddComponent<Rigidbody>();ferryRb.isKinematic=true;ferry.AddComponent<LevelOneMotion>().travel=new Vector3(14,0,0);
            Box("Escalón de salida del canal",new Vector3(8,-.7f,3.3f),new Vector3(4,.8f,1.5f),tile);CheckpointAt(6);
            // Drag a crate onto the scale to hold the passage open.
            Sign("ALMACÉN / CONTRAPESO\nArrastra la caja naranja sobre la placa azul",new Vector3(0,3.3f,9),8,.18f);
            for(int i=0;i<5;i++)Phys("Suministros · caja",new Vector3(-8+(i%2)*1.6f,.65f+(i/4)*1.3f,9+(i/2)*1.6f),Vector3.one*1.2f,4,wood);
            var weight=Phys("Contrapeso naranja · 20 kg",new Vector3(-5,.75f,12),Vector3.one*1.4f,20,orange);
            var plateRoot=new GameObject("Placa de presión / 12 kg");plateRoot.transform.SetParent(root);plateRoot.transform.position=new Vector3(4,0,15);
            var pc=plateRoot.AddComponent<BoxCollider>();pc.isTrigger=true;pc.size=new Vector3(3,1.4f,3);pc.center=Vector3.up*.7f;
            var pv=Box("Placa azul",new Vector3(4,.08f,15),new Vector3(3,.16f,3),mint);
            for(int i=0;i<6;i++)Box("Borde de placa",new Vector3(2.7f+i*.52f,.10f,13.5f),new Vector3(.22f,.05f,.25f),yellow,null,false);
            Box("Marco puerta izquierdo",new Vector3(-8,2,20),new Vector3(8,4,.8f),wall);Box("Marco puerta derecho",new Vector3(8,2,20),new Vector3(8,4,.8f),wall);
            var gate=Box("Compuerta del almacén",new Vector3(0,2,20),new Vector3(8,4,.6f),orange);var door=gate.AddComponent<Door>();door.openOffset=new Vector3(0,5,0);
            var plate=plateRoot.AddComponent<PressurePlate>();plate.requiredMass=12;plate.doors=new[]{door};plate.plateVisual=pv.transform;
            // A line of food, breakable pallets and an optional acid cache.
            CheckpointAt(23);Food(new Vector3(-6,0,25),12);
            Sign("PATIO / CRECIMIENTO\nCome para crecer · rompe las tablas rodando\nClic derecho: ácido (en forma normal)",new Vector3(0,3.8f,25),9,.17f);
            for(int i=0;i<3;i++)
            {
                var b=Box("Pallet rompible",new Vector3(-1+i*1.1f,1.4f,33),new Vector3(1,2.8f,.4f),wood);b.AddComponent<Breakable>().requiredSpeed=2.5f;
                for(int j=0;j<3;j++)Box("Listón",new Vector3(-1+i*1.1f,.5f+j*.8f,32.75f),new Vector3(.95f,.14f,.1f),orange,b.transform,false);
            }
            // This cache is optional; the exit is reachable without acid or giant form.
            Box("Cámara de suministros / pared",new Vector3(9,1.6f,30),new Vector3(.4f,3.2f,9),trim);Box("Cámara / fondo",new Vector3(6,1.6f,34),new Vector3(6,3.2f,.4f),trim);
            var acid=Box("Membrana vulnerable al ácido",new Vector3(6,1.2f,26),new Vector3(5,2.4f,.25f),mint);acid.AddComponent<AcidVulnerable>().hitsToDissolve=2;Food(new Vector3(5,0,30),4,2);
            Phys("Barril de mantenimiento",new Vector3(-7,.65f,33),new Vector3(.9f,.6f,.9f),6,orange,true);
            var enemy=Shape("Babosa de las tuberías",PrimitiveType.Sphere,new Vector3(0,.45f,37),new Vector3(1.4f,.8f,1.8f),wall);enemy.AddComponent<Rigidbody>().isKinematic=true;var patrol=enemy.AddComponent<PatrolEnemy>();patrol.pointA=new Vector3(-5,.45f,37);patrol.pointB=new Vector3(5,.45f,37);patrol.speed=1.4f;enemy.AddComponent<DamageSource>();enemy.AddComponent<EnemyHealth>();
            foreach(float x in new[]{-.35f,.35f})
            {var eye=Shape("Ojo babosa",PrimitiveType.Sphere,new Vector3(x,.8f,36.5f),Vector3.one*.28f,glow,null,false);eye.transform.SetParent(enemy.transform,true);}
            Recovery(new Vector3(-8,.15f,39));CheckpointAt(41);
            // Rescue and food depot, reachable by normal form with renewable food.
            Sign("REFUGIO / RESCATE\nE come · acércate al compañero y pulsa G\nEntrega fruta o expulsa comida dentro del aro",new Vector3(0,3.6f,43),10,.18f);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Prefabs/Sunny.prefab")??AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Lizard/Prefabs/Lagarto.prefab");
            if(!prefab)throw new InvalidOperationException("No existe prefab de jugador.");
            var player=(GameObject)PrefabUtility.InstantiatePrefab(prefab);player.name="Jugador · Sunny";player.transform.position=new Vector3(0,.25f,-24);player.GetComponent<LizardInput>().controlled=true;
            var pp=player.GetComponent<LizardPlayer>();pp.displayName="Sunny";
            var friend=(GameObject)PrefabUtility.InstantiatePrefab(prefab);friend.name="Compañero por rescatar";friend.transform.position=new Vector3(-4,.2f,47);friend.GetComponent<LizardInput>().controlled=false;
            var survivor=friend.GetComponent<LizardPlayer>();survivor.startForm=LizardForm.Small;survivor.startFood=0;survivor.dummyCallsHelp=true;survivor.displayName="Pip";
            var spawner=new GameObject("Cesta de fruta renovable");spawner.transform.SetParent(root);spawner.transform.position=new Vector3(8,.2f,44);var fs=spawner.AddComponent<FoodSpawner>();fs.respawnDelay=2;fs.radius=.18f;
            Shape("Cesta",PrimitiveType.Cylinder,new Vector3(8,.12f,44),new Vector3(1.2f,.12f,1.2f),wood);
            Food(new Vector3(3,0,45),6);
            for(int i=0;i<2;i++)
            {
                var melon=FoodFactory.CreateBig(new Vector3(8,.75f,47+i*2),4,.65f,mint);melon.transform.SetParent(root);melon.name="Sandía · transportar al depósito";
            }
            var depotRoot=new GameObject("Depósito / entrega aquí");depotRoot.transform.SetParent(root);depotRoot.transform.position=new Vector3(5,0,49);var dc=depotRoot.AddComponent<BoxCollider>();dc.isTrigger=true;dc.size=new Vector3(3.6f,3,3.6f);dc.center=Vector3.up*1.5f;var goal=depotRoot.AddComponent<FoodGoal>();
            var depotRing=Arch("Aro de depósito",new Vector3(5,.12f,49),1.8f,.12f,yellow,true);depotRing.transform.rotation=Quaternion.Euler(90,0,0);
            var exit=Box("Puerta de salida",new Vector3(0,1.8f,54),new Vector3(5,3.6f,.6f),mint).AddComponent<Door>();exit.openOffset=new Vector3(0,4.5f,0);
            Box("Pared salida izq",new Vector3(-7,1.8f,54),new Vector3(9,3.6f,.6f),wall);Box("Pared salida der",new Vector3(7,1.8f,54),new Vector3(9,3.6f,.6f),wall);
            var missionRoot=new GameObject("Objetivo y salida");missionRoot.transform.SetParent(root);missionRoot.transform.position=new Vector3(0,0,56);var ec=missionRoot.AddComponent<BoxCollider>();ec.isTrigger=true;ec.size=new Vector3(5,4,2);
            var mission=missionRoot.AddComponent<LevelOneMission>();mission.depot=goal;mission.survivor=survivor;mission.exitDoor=exit;mission.board=Sign("CENTRO DE RESCATE",new Vector3(-5,2.5f,50),6,.17f);
            // A handful of physically interactive maintenance props and high food shelves.
            for(int z=-16;z<=40;z+=14)
            {
                Box("Banco de trabajo",new Vector3(9,.8f,z),new Vector3(3,.2f,1.2f),wood);
                foreach(float x in new[]{7.8f,10.2f})Box("Pata banco",new Vector3(x,.4f,z),new Vector3(.2f,.8f,.7f),metal);
                Phys("Bote manipulable",new Vector3(9,1.25f,z),new Vector3(.4f,.35f,.4f),.8f,mint,true);
            }
            var camGo=new GameObject("Cámara de Sunny");camGo.tag="MainCamera";var camera=camGo.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=160;camera.backgroundColor=RenderSettings.fogColor;camera.clearFlags=CameraClearFlags.SolidColor;camGo.AddComponent<AudioListener>();var rig=camGo.AddComponent<LizardCameraRig>();rig.target=pp;camGo.transform.position=new Vector3(0,3,-29);camGo.transform.LookAt(new Vector3(0,1,-23));
            var hud=new GameObject("HUD existente").AddComponent<LizardHUD>();hud.rig=rig;hud.showDebug=false;
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0,0,14),Quaternion.Euler(55,0,0),55);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var build=EditorBuildSettings.scenes.ToList();if(!build.Any(s=>s.path==ScenePath)){build.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=build.ToArray();}
            Debug.Log("[LEVEL1] Estación construida. Guarda contrapeso en placa, rescata a Pip, entrega 8 y entra en salida.");
        }
        public static string Validate()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var all=scene.GetRootGameObjects().Where(g=>g.activeInHierarchy).SelectMany(g=>g.GetComponentsInChildren<Transform>()).ToArray();
            int missing=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            if(missing>0)throw new Exception("Missing scripts: "+missing);
            var mission=Object.FindAnyObjectByType<LevelOneMission>();if(!mission||!mission.depot||!mission.survivor||!mission.exitDoor)throw new Exception("Mission links missing");
            return $"Scene={scene.path}; objects={all.Length}; rigidbodies={Object.FindObjectsByType<Rigidbody>().Length}; foods={Object.FindObjectsByType<FoodItem>().Length}; missing={missing}";
        }
        public static string Capture()
        {
            var g=new GameObject("Temporary overview camera");var c=g.AddComponent<Camera>();c.transform.position=new Vector3(41,64,-42);c.transform.LookAt(new Vector3(0,0,12));c.orthographic=true;c.orthographicSize=48;c.farClipPlane=250;c.backgroundColor=new Color(.12f,.18f,.24f);c.clearFlags=CameraClearFlags.SolidColor;
            var rt=new RenderTexture(1600,1200,24);c.targetTexture=rt;
            if(GraphicsSettings.currentRenderPipeline)
                RenderPipeline.SubmitRenderRequest(c,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            else c.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(1600,1200,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1600,1200),0,0);t.Apply();string path=Path.GetFullPath("Captures/LEVEL1_Overview.png");File.WriteAllBytes(path,t.EncodeToPNG());RenderTexture.active=old;c.targetTexture=null;Object.DestroyImmediate(t);Object.DestroyImmediate(rt);Object.DestroyImmediate(g);return path;
        }
    }
}
