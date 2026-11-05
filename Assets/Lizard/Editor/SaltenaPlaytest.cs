using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Lizards.EditorTools
{
    public static class SaltenaPlaytest
    {
        static readonly List<string> report=new();
        static LizardPlayer a,b;
        static FoodItem food,spat;
        static float value;
        static string identity;
        static int stage;
        static double next;
        static FoodItem Prefab=>AssetDatabase.LoadAssetAtPath<FoodItem>(SaltenaBuilder.PrefabDir+"/Saltena_Whole.prefab");
        static void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text);}
        public static void Begin()
        {
            if(!Application.isPlaying)throw new Exception("Enter Play in SaltenaTest first");
            report.Clear();stage=0;next=EditorApplication.timeSinceStartup+.4;Application.runInBackground=true;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Place(LizardPlayer p,Vector3 pos,Vector3 direction)
        {p.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(direction));p.Body.position=pos;p.Body.rotation=p.transform.rotation;p.Body.linearVelocity=Vector3.zero;p.Body.constraints=RigidbodyConstraints.FreezeAll;p.Motor.enabled=false;}
        static FoodItem Make(Vector3 p){return Object.Instantiate(Prefab,p,Quaternion.identity);}
        static void Tick()
        {
            if(!Application.isPlaying){Finish();return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.5;
            try
            {
                switch(stage++)
                {
                    case 0:
                        foreach(var f in FoodItem.All.ToArray())Object.Destroy(f.gameObject);
                        var prefab=AssetDatabase.LoadAssetAtPath<LizardPlayer>("Assets/Lizard/Prefabs/Sunny.prefab");
                        a=Object.Instantiate(prefab,new Vector3(-2.5f,.05f,0),Quaternion.identity);b=Object.Instantiate(prefab,new Vector3(2.5f,.05f,0),Quaternion.identity);
                        a.Input.controlled=b.Input.controlled=false;a.startFood=b.startFood=0;break;
                    case 1:
                        Place(a,new Vector3(-2.5f,.05f,-.6f),Vector3.right);Place(b,new Vector3(-2.5f,.05f,.6f),Vector3.right);
                        food=Make(new Vector3(0,.5f,0));break;
                    case 2:
                        var ordinary=new GameObject("Ordinary food ownership fixture");ordinary.transform.position=new Vector3(0,1,-8);ordinary.AddComponent<Rigidbody>();ordinary.AddComponent<SphereCollider>().radius=.18f;var apple=ordinary.AddComponent<FoodItem>();
                        Check(apple.TryGrab(a)&&!apple.TryGrab(b),"Ordinary food still has exclusive ownership");apple.ReleaseGrab(a);Object.Destroy(apple.gameObject);
                        Check(!a.TryEat(food),"Normal player cannot swallow the intact salteña");
                        Check(a.Tongue.DebugAttach(food.GetComponent<Collider>()),"First tongue attaches to salteña");next=EditorApplication.timeSinceStartup+1;break;
                    case 3:
                        Check(food&&food.GetComponent<BreakableFood>().TearProgress==0,"One player pulling does not tear food");
                        Check(b.Tongue.DebugAttach(food.GetComponent<Collider>()),"Second tongue shares the same food");
                        Check(food.GrabberCount==2&&!food.CanBeEatenBy(a),"Shared food cannot be stolen by swallowing");next=EditorApplication.timeSinceStartup+1.1;break;
                    case 4:
                        Check(food,"Same-side pulling does not tear salteña");a.Tongue.Release(false);b.Tongue.Release(false);Object.Destroy(food.gameObject);
                        Place(a,new Vector3(-2.5f,.05f,0),Vector3.right);Place(b,new Vector3(2.5f,.05f,0),Vector3.left);break;
                    case 5:food=Make(new Vector3(0,.5f,0));break;
                    case 6:
                        Check(a.Tongue.DebugAttach(food.GetComponent<Collider>())&&b.Tongue.DebugAttach(food.GetComponent<Collider>()),"Opposing players both attach through real tongue physics");next=EditorApplication.timeSinceStartup+2;break;
                    case 7:
                        Check(!food,"Opposing sustained tension breaks salteña");var pieces=FoodItem.All.ToArray();Check(pieces.Length==6,"Exactly six authored pieces replace the original");
                        Check(Mathf.Abs(pieces.Sum(f=>f.nutrition)-4)<.0001f,"Tearing conserves all four nutrition units");Check(Mathf.Abs(pieces.Sum(f=>f.Body.mass)-2.4f)<.0001f,"Tearing conserves physical mass");
                        Check(pieces.All(f=>f.FitsMouth(a.Stats.mouthCapacity)),"All six pieces fit a normal mouth");Check(pieces.Select(f=>f.GetComponent<FoodCollectionIdentity>().modelId).Distinct().Count()==6,"Six different cut meshes, rather than six copies of the whole");
                        Check(!a.Tongue.HeldFood&&!b.Tongue.HeldFood,"Both tongues release the destroyed original");
                        food=pieces[0];value=food.nutrition;identity=food.GetComponent<FoodCollectionIdentity>().modelId;food.transform.position=a.MouthPosition;food.Body.position=a.MouthPosition;Physics.SyncTransforms();
                        Check(a.TryEat(food),"Normal player eats a broken piece");Check(Mathf.Abs(a.Stomach.Food+FoodItem.TotalInWorld()-4)<.0001f,"Swallowing conserves world plus stomach nutrition");break;
                    case 8:
                        var before=new HashSet<FoodItem>(FoodItem.All);a.EjectOrFeed();spat=FoodItem.All.FirstOrDefault(f=>!before.Contains(f));
                        Check(spat&&spat.GetComponent<FoodCollectionIdentity>().modelId==identity,"Spit returns the actual broken piece");Check(Mathf.Abs(spat.nutrition-value)<.0001f,"Spat piece keeps its exact nutrition");
                        Check(Mathf.Abs(FoodItem.TotalInWorld()-4)<.0001f,"Spitting does not duplicate food");
                        foreach(var f in FoodItem.All.ToArray())Object.Destroy(f.gameObject);food=Make(new Vector3(3,.7f,6));next=EditorApplication.timeSinceStartup+1;break;
                    case 9:
                        Check(food,"Initial settling on floor does not break food");food.Body.position=new Vector3(4.2f,1.3f,6);food.Body.useGravity=false;food.Body.linearVelocity=Vector3.right*8;next=EditorApplication.timeSinceStartup+1.3;break;
                    case 10:
                        Check(!food,"Actual rigidbody impact against a wall breaks salteña");Check(FoodItem.All.Count==6&&Mathf.Abs(FoodItem.TotalInWorld()-4)<.0001f,"Wall impact also creates exactly six pieces without lost nutrition");
                        foreach(var f in FoodItem.All.ToArray())Object.Destroy(f.gameObject);food=Make(new Vector3(2,1.5f,7));food.Body.useGravity=false;next=EditorApplication.timeSinceStartup+1;break;
                    case 11:food.Body.linearVelocity=Vector3.right*2;next=EditorApplication.timeSinceStartup+2.5;break;
                    case 12:
                        Check(food&&!food.GetComponent<BreakableFood>().Broken,"Gentle wall contact does not break food");Check(a.Stomach.Food==0,"Test leaves no fabricated food in stomach");Finish();break;
                }
            }
            catch(Exception e){report.Add("FAIL stage "+(stage-1)+": "+e);Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;Directory.CreateDirectory("Captures");File.WriteAllLines("Captures/Saltena_Playtest.txt",report);Debug.Log("[SALTEÑA QA] "+string.Join(" | ",report));
        }
    }
}
