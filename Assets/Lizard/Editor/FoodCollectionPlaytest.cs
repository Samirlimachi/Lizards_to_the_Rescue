using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Lizards.EditorTools
{
    public static class FoodCollectionPlaytest
    {
        static readonly string[] Ids={"Apple","Banana","Corn","Broccoli","Avocado","Pepper"};
        static readonly List<string> checks=new();
        static LizardPlayer player;
        static FoodItem current;
        static int index,frame;
        static double next;
        static bool waiting;
        static Vector3 mouthStart;
        static void Check(bool condition,string name)=>checks.Add((condition?"PASS ":"FAIL ")+name);
        public static void Begin()
        {
            if(!Application.isPlaying)throw new Exception("Enter Play mode first");
            player=LizardPlayer.All.First(p=>p.IsControlled);checks.Clear();index=0;waiting=false;frame=Time.frameCount;
            var foods=UnityEngine.Object.FindObjectsByType<FoodCollectionIdentity>();
            Check(foods.Length==30,"LEVEL1 contains 30 imported food instances");
            foreach(var id in Ids)Check(foods.Count(f=>f.modelId==id)==5,id+" has five scene instances");
            Check(foods.All(f=>f.GetComponent<Rigidbody>() && f.GetComponent<Collider>() && f.GetComponent<FoodItem>()),"All foods have physics, colliders and FoodItem");
            Check(foods.All(f=>f.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterial && r.sharedMaterial.mainTexture)),"All foods have textured materials");
            Check(foods.All(f=>f.GetComponent<FoodItem>().FitsMouth(player.Stats.mouthCapacity)),"All six models fit the normal mouth");
            next=EditorApplication.timeSinceStartup+1;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!Application.isPlaying){EditorApplication.update-=Tick;return;}
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                if(waiting)
                {
                    Check(!current || current.State==FoodState.Consumed,"Tongue reels and automatically swallows "+Ids[index]);
                    Check(player.Stomach.Food>.5f,"Nutrition increases for "+Ids[index]);
                    if(current)UnityEngine.Object.Destroy(current.gameObject);
                    player.Tongue.Release(false);index++;waiting=false;next=EditorApplication.timeSinceStartup+.5;return;
                }
                if(index==Ids.Length)
                {
                    Check(Time.frameCount>frame+20,"Game simulation advances");
                    var placed=UnityEngine.Object.FindObjectsByType<FoodCollectionIdentity>();
                    Check(placed.All(f=>f.transform.position.y>-1),"Placed food stays above the level floor");
                    player.Stomach.DebugSetFood(0);
                    Directory.CreateDirectory("Captures");File.WriteAllLines("Captures/FoodCollection_Playtest.txt",checks);
                    Debug.Log("[Food Collection QA] "+string.Join(" | ",checks));EditorApplication.update-=Tick;return;
                }
                if(player.Tongue.State!=TongueState.Idle){next=EditorApplication.timeSinceStartup+.2;return;}
                player.Stomach.DebugSetFood(0);player.EvaluateForm();
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(FoodCollectionBuilder.Folder+"/Prefabs/"+Ids[index]+".prefab");
                var go=UnityEngine.Object.Instantiate(prefab,player.MouthPosition+player.transform.forward*1.2f,Quaternion.identity);
                current=go.GetComponent<FoodItem>();mouthStart=go.transform.position;
                Check(player.Tongue.DebugAttach(go.GetComponent<Collider>()),"Tongue attaches to "+Ids[index]);
                waiting=true;next=EditorApplication.timeSinceStartup+2.5;
            }
            catch(Exception e)
            {
                checks.Add("FAIL "+e);File.WriteAllLines("Captures/FoodCollection_Playtest.txt",checks);EditorApplication.update-=Tick;Debug.LogException(e);
            }
        }
    }
}
