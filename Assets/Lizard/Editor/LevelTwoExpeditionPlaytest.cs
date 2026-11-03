using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Lizards.EditorTools
{
    public static class LevelTwoExpeditionPlaytest
    {
        static readonly List<string> checks=new();
        static int step,frame,coins,day,done,foodBefore;
        static bool hadCoins,hadDay,hadDone;
        static double next;
        static Vector3 start;
        static LizardPlayer player;
        static LevelTwoExpedition mission;
        static ExpeditionSupplyCrate crate;
        static FoodItem tongueFood;
        static string model;
        static float depositedExpected;
        static InputSettings.BackgroundBehavior background;
        static InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static void Check(bool valid,string label)
        {
            checks.Add((valid?"PASS ":"FAIL ")+label);
            if(!valid)throw new Exception(label);
        }
        public static void Begin()
        {
            if(!Application.isPlaying||SceneManager.GetActiveScene().name!="Lobby")throw new Exception("Start Play in Lobby first");
            checks.Clear();step=0;next=EditorApplication.timeSinceStartup+.5;
            hadCoins=PlayerPrefs.HasKey("Lizards.HubCoins");hadDay=PlayerPrefs.HasKey("Lizards.ExpeditionDay");hadDone=PlayerPrefs.HasKey("Lizards.Level2Done");
            coins=SwampPocket.Ensure().Coins;day=PlayerPrefs.GetInt("Lizards.ExpeditionDay",1);done=PlayerPrefs.GetInt("Lizards.Level2Done",0);
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(Keyboard.current);Application.runInBackground=true;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Keys(params Key[] keys){if(Keyboard.current!=null)InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(keys));}
        static void Teleport(Vector3 p){player.Body.position=p;player.Body.linearVelocity=Vector3.zero;}
        static void Tick()
        {
            if(!Application.isPlaying){Finish();return;}
            if(EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.6;
            try
            {
                switch(step++)
                {
                    case 0:
                        var hub=UnityEngine.Object.FindAnyObjectByType<SwampLobbyHub>();player=LizardPlayer.All.First(p=>p.IsControlled);
                        Teleport(hub.boat.position+Vector3.up*.25f);Check(hub.BeginDeparture(player),"Lobby boat accepts expedition departure");next=EditorApplication.timeSinceStartup+3.8;break;
                    case 1:
                        Check(SceneManager.GetActiveScene().name=="LEVEL2","Lobby boat loads LEVEL2");
                        mission=UnityEngine.Object.FindAnyObjectByType<LevelTwoExpedition>();Check(mission,"Collection mission exists");
                        player=LizardPlayer.All.First(p=>p.IsControlled);frame=Time.frameCount;start=player.transform.position;
                        Check(UnityEngine.Object.FindObjectsByType<ExpeditionSupplyCrate>().Length==14,"Fourteen working supply crates");
                        Check(FoodItem.All.Sum(f=>f.nutrition)>mission.RequiredFood,"Enough loose food for the quota even before opening crates");
                        Check(!mission.TryDepart(player),"Boat refuses extraction before quota");
                        Keys(Key.W);next=EditorApplication.timeSinceStartup+1;break;
                    case 2:
                        Keys();Check(Time.frameCount>frame+10,"Game loop advances");Check(Vector3.Distance(player.transform.position,start)>.6f,"Real W input moves player off arrival raft");
                        Check(player.transform.position.y>-.3f,"Arrival deck and boarding plank support the player");
                        crate=UnityEngine.Object.FindObjectsByType<ExpeditionSupplyCrate>().OrderBy(c=>Vector3.Distance(c.transform.position,new Vector3(8,.62f,-17))).First();
                        var cp=crate.transform.position+Vector3.back*1.5f;cp.y=.05f;Teleport(cp);player.transform.rotation=Quaternion.identity;foodBefore=FoodItem.All.Count;break;
                    case 3:Keys(Key.E);next=EditorApplication.timeSinceStartup+.35;break;
                    case 4:
                        Keys();Check(crate.Opened,"E interaction opens the supply crate");Check(FoodItem.All.Count>=foodBefore+3,"Opened crate releases three modeled foods");
                        Check(!crate.Open(),"Opened crate cannot duplicate its contents");
                        tongueFood=FoodItem.All.Where(f=>f.GetComponent<FoodCollectionIdentity>()&&!f.transform.parent).OrderBy(f=>Vector3.Distance(f.transform.position,crate.transform.position)).First();
                        model=tongueFood.GetComponent<FoodCollectionIdentity>().modelId;
                        Teleport(new Vector3(2,.05f,-18));player.Stomach.DebugSetFood(0);player.EvaluateForm();next=EditorApplication.timeSinceStartup+.4;break;
                    case 5:
                        var foodPosition=player.MouthPosition+player.transform.forward*1.2f;
                        // Interpolated bodies keep the old render pose on a same-frame teleport.
                        // DebugAttach computes a local anchor, so update both poses for this fixture.
                        tongueFood.transform.position=foodPosition;tongueFood.Body.position=foodPosition;tongueFood.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                        Check(player.Tongue.DebugAttach(tongueFood.GetComponent<Collider>()),"Tongue grabs a crate food");next=EditorApplication.timeSinceStartup+2;break;
                    case 6:
                        Check(!tongueFood&&player.Stomach.Food>=.9f,"Tongue reels food to mouth and swallows");
                        var previous=new HashSet<FoodItem>(FoodItem.All);player.Stomach.EjectLastFood(Vector3.forward,4,0);var spat=FoodItem.All.FirstOrDefault(f=>!previous.Contains(f));
                        Check(spat&&spat.GetComponent<FoodCollectionIdentity>().modelId==model,"Spit keeps the collected food model");
                        Teleport(new Vector3(-24,-.65f,-8));next=EditorApplication.timeSinceStartup+.9;break;
                    case 7:
                        Check(player.Motor.Swimming,"Flooded gallery activates real swimming");Check(player.transform.position.y> -2,"Swimming keeps player above the channel bottom");
                        Teleport(new Vector3(4,.05f,-18));
                        var plate=UnityEngine.Object.FindAnyObjectByType<PressurePlate>();var weight=GameObject.Find("Contrapeso marcado · 20 kg").GetComponent<Rigidbody>();weight.position=plate.transform.position+Vector3.up*.8f;weight.linearVelocity=Vector3.zero;next=EditorApplication.timeSinceStartup+1.2;break;
                    case 8:
                        Check(UnityEngine.Object.FindAnyObjectByType<PressurePlate>().Pressed,"20kg crate activates the cargo plate");Check(GameObject.Find("Compuerta de provisiones").transform.position.y>3,"Cargo cache gate opens physically");
                        var acid=UnityEngine.Object.FindAnyObjectByType<AcidVulnerable>();acid.HitByAcid();acid.HitByAcid();
                        var breakable=UnityEngine.Object.FindAnyObjectByType<Breakable>();Check(breakable.TryBreak(5,true,Vector3.forward*10),"Giant can break the optional wooden barrier");
                        depositedExpected=mission.bowl.Delivered;float needed=mission.RequiredFood-depositedExpected;int i=0;
                        foreach(var f in FoodItem.All.ToArray())
                        {
                            if(needed<=0)break;float value=f.nutrition;needed-=value;depositedExpected+=value;
                            f.Body.position=mission.bowl.transform.position+new Vector3(((i/4)%4-1.5f)*.35f,1.1f+(i/16)*.3f,(i%4-1.5f)*.35f);f.Body.linearVelocity=Vector3.zero;i++;
                        }
                        next=EditorApplication.timeSinceStartup+1.2;break;
                    case 9:
                        Check(!UnityEngine.Object.FindAnyObjectByType<AcidVulnerable>(),"Acid opens the optional reserve");
                        Check(Mathf.Abs(mission.bowl.Delivered-depositedExpected)<.001f,"Bowl counts the exact nutrition of real scene foods");Check(mission.Ready,"Collected provisions satisfy the quota");
                        Teleport(mission.boat.position+Vector3.up*.3f);next=EditorApplication.timeSinceStartup+.6;break;
                    case 10:Keys(Key.E);next=EditorApplication.timeSinceStartup+.25;break;
                    case 11:
                        Keys();Check(mission.Departing,"E on the raft starts return countdown");next=EditorApplication.timeSinceStartup+3.6;break;
                    case 12:
                        Check(SceneManager.GetActiveScene().name=="Lobby","Expedition returns to Lobby");Check(SwampPocket.Ensure().Coins==coins+250,"Reward is paid once");Check(PlayerPrefs.GetInt("Lizards.ExpeditionDay",1)==Mathf.Max(1,day)+1,"Successful trip advances the expedition day");Finish();break;
                }
            }
            catch(Exception e){checks.Add("FAIL EXCEPTION at stage "+(step-1)+": "+e);Finish();}
        }
        static void Finish()
        {
            EditorApplication.update-=Tick;Keys();InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;
            if(SwampPocket.Instance)SwampPocket.Instance.Coins=coins;
            Restore("Lizards.HubCoins",coins,hadCoins);Restore("Lizards.ExpeditionDay",day,hadDay);Restore("Lizards.Level2Done",done,hadDone);PlayerPrefs.Save();
            Directory.CreateDirectory("Captures");File.WriteAllLines("Captures/LEVEL2_Expedition_Playtest.txt",checks);Debug.Log("[LEVEL2 QA] "+string.Join(" | ",checks));
        }
        static void Restore(string key,int value,bool existed){if(existed)PlayerPrefs.SetInt(key,value);else PlayerPrefs.DeleteKey(key);}
    }
}
