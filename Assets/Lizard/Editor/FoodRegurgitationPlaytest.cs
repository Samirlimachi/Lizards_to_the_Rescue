using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lizards.EditorTools
{
    public static class FoodRegurgitationPlaytest
    {
        static readonly List<string> results=new();
        static LizardPlayer player;
        static void Check(bool condition,string label)
        {
            results.Add((condition?"PASS ":"FAIL ")+label);
            if(!condition)throw new Exception(label);
        }
        static FoodItem Make(string id,float value=1)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(FoodCollectionBuilder.Folder+"/Prefabs/"+id+".prefab");
            var go=UnityEngine.Object.Instantiate(prefab,player.MouthPosition+Vector3.forward,Quaternion.identity);
            var f=go.GetComponent<FoodItem>();f.nutrition=value;return f;
        }
        static void Clean(FoodItem f){if(f){f.gameObject.SetActive(false);UnityEngine.Object.Destroy(f.gameObject);}}
        static FoodItem[] Expel(Action action)
        {
            var before=new HashSet<FoodItem>(FoodItem.All);action();
            return FoodItem.All.Where(f=>f&&!before.Contains(f)).ToArray();
        }
        public static string Run()
        {
            if(!Application.isPlaying)throw new Exception("Enter Play first");
            results.Clear();player=LizardPlayer.All.First(p=>p.IsControlled);
            try
            {
                foreach(string id in new[]{"Apple","Banana","Corn","Broccoli","Avocado","Pepper"})
                {
                    player.Stomach.DebugSetFood(0);player.EvaluateForm();var f=Make(id);
                    float worldBefore=FoodItem.TotalInWorld();
                    for(int cycle=0;cycle<3;cycle++)
                    {
                        Check(player.TryEat(f),id+" swallowed, cycle "+cycle);
                        var output=Expel(()=>player.Stomach.EjectLastFood(Vector3.forward,4,0));
                        Check(output.Length==1,id+" spits one object");f=output[0];
                        Check(f.GetComponent<FoodCollectionIdentity>().modelId==id,id+" keeps original model");
                        Check(Mathf.Abs(f.nutrition-1)<.0001f&&player.Stomach.Food==0,id+" conserves nutrition");
                        Check(f.Body&&!f.Body.isKinematic&&f.GetComponent<Collider>().enabled,id+" returns as physical food");
                        Check(!f.CanBeEatenBy(player),id+" prevents instant self-pickup");
                        Check(Mathf.Abs(FoodItem.TotalInWorld()-worldBefore)<.001f,id+" cycles do not duplicate food");
                        f.MarkEjected(player,0,0);
                    }
                    Clean(f);
                }
                player.Stomach.DebugSetFood(0);
                Check(player.TryEat(Make("Apple")),"First mixed food swallowed");
                Check(player.TryEat(Make("Banana")),"Second mixed food swallowed");
                var mixed=Expel(()=>player.Stomach.EjectLastFood(Vector3.forward,4,0));
                Check(mixed.Single().GetComponent<FoodCollectionIdentity>().modelId=="Banana"&&Mathf.Approximately(player.Stomach.Food,1),"Last swallowed food comes out first");Clean(mixed[0]);
                mixed=Expel(()=>player.Stomach.EjectLastFood(Vector3.forward,4,0));
                Check(mixed.Single().GetComponent<FoodCollectionIdentity>().modelId=="Apple"&&player.Stomach.Food==0,"Earlier food remains available to spit");Clean(mixed[0]);

                Check(player.TryEat(Make("Corn",4)),"High value food swallowed");
                var whole=Expel(()=>player.EjectOrFeed());
                Check(whole.Length==1&&whole[0].nutrition==4&&player.Stomach.Food==0,"Actual spit action returns the whole item instead of splitting it into pellets");Clean(whole[0]);

                player.Stomach.DebugSetFood(player.Config.maxFood-.25f);
                Check(player.TryEat(Make("Pepper")),"Near-full stomach accepts partial nutrition");
                var partial=Expel(()=>player.Stomach.EjectLastFood(Vector3.forward,4,0));
                Check(partial.Length==1&&Mathf.Approximately(partial[0].nutrition,.25f)&&partial[0].GetComponent<FoodCollectionIdentity>().modelId=="Pepper","Overflow cannot recreate discarded nutrition");Clean(partial[0]);

                player.Stomach.DebugSetFood(0);player.EvaluateForm();
                Check(player.TryEat(Make("Avocado")),"Damage test food swallowed");
                var damaged=Expel(()=>player.Health.TakeHit(new DamageInfo{sourcePosition=player.transform.position-Vector3.forward,foodLoss=.4f,type=DamageType.Debug}));
                Check(damaged.Length==1&&damaged[0].GetComponent<FoodCollectionIdentity>().modelId=="Avocado"&&Mathf.Abs(damaged[0].nutrition-.4f)<.001f&&Mathf.Abs(player.Stomach.Food-.6f)<.001f,"Damage expels original model with exact deducted value");Clean(damaged[0]);
                var remainder=Expel(()=>player.Stomach.EjectLastFood(Vector3.forward,4,0));
                Check(remainder.Length==1&&Mathf.Abs(remainder[0].nutrition-.6f)<.001f,"Remaining nutrition is preserved after damage");Clean(remainder[0]);

                var partner=LizardPlayer.All.First(p=>p!=player);partner.Stomach.DebugSetFood(0);
                Check(player.TryEat(Make("Broccoli")),"Transfer test food swallowed");
                float transferred=player.Stomach.TransferTo(partner.Stomach,1);
                var received=Expel(()=>partner.Stomach.EjectLastFood(Vector3.forward,4,0));
                Check(transferred==1&&received.Length==1&&received[0].GetComponent<FoodCollectionIdentity>().modelId=="Broccoli","Feeding another player preserves food identity");Clean(received[0]);
                player.Stomach.DebugSetFood(0);player.EvaluateForm();
                Check(Expel(()=>player.Stomach.EjectLastFood(Vector3.forward,4,0)).Length==0,"Empty stomach cannot spawn food");
                Check(!FoodItem.All.Any(f=>f&&f.transform.IsChildOf(player.transform)),"Hidden templates never register as world food");
            }
            catch(Exception e){results.Add("FAIL EXCEPTION "+e);}
            Directory.CreateDirectory("Captures");File.WriteAllLines("Captures/FoodRegurgitation_Playtest.txt",results);
            return string.Join("\n",results);
        }
    }
}
