using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Lizards.EditorTools
{
    public static class SwampLobbyDetailedTest
    {
        static LizardPlayer p;static SwampLobbyExpansion vendor;static SwampLobbyHub hub;static SwampPocket pocket;
        static int step,frame,coins,foodCount;static Vector3 start;static double next;static float speed;static readonly List<string> checks=new();
        static InputSettings.BackgroundBehavior background;
        static InputSettings.EditorInputBehaviorInPlayMode editorInput;
        static void Check(bool ok,string label)=>checks.Add((ok?"PASS ":"FAIL ")+label);
        public static void Begin()
        {
            if(!Application.isPlaying)throw new Exception("Play required");p=LizardPlayer.All.First(x=>x.IsControlled);vendor=UnityEngine.Object.FindAnyObjectByType<SwampLobbyExpansion>();hub=UnityEngine.Object.FindAnyObjectByType<SwampLobbyHub>();pocket=SwampPocket.Ensure();coins=pocket.Coins;pocket.Coins=1000;checks.Clear();step=0;frame=Time.frameCount;start=p.transform.position;next=EditorApplication.timeSinceStartup+1;
            background=InputSystem.settings.backgroundBehavior;editorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(Keyboard.current);
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!Application.isPlaying){Restore();EditorApplication.update-=Tick;return;}if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
            try{
                switch(step++){
                    case 0:
                        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Check(Time.frameCount>frame,"Frames advance");Check(Vector3.Distance(start,p.transform.position)>.3f,"Walking in spawn pool");Check(!LizardLobby.Visible,"Local hub starts without blocking menu");
                        Check(UnityEngine.Object.FindObjectsByType<SwampVendorButton>().All(b=>b.vendor==vendor),"Tongue buttons linked to vending machine");
                        hub.CycleHat(p);Check(p.transform.Find("Sombrero del refugio"),"Detailed hat equips");vendor.ChangeColor(p);hub.roomMenu.OpenRoomMenu();Check(LizardLobby.Visible,"Phone opens room menu");hub.roomMenu.SendMessage("PlaySolo");
                        vendor.Browse(5);p.Body.position=vendor.vending.position+new Vector3(0,.2f,-3);p.Body.linearVelocity=Vector3.zero;break;
                    case 1:InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.E));break;
                    case 2:
                        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Check(pocket.Coins==950,"Physical E purchase subtracts 50 coins");var capsule=UnityEngine.Object.FindAnyObjectByType<SwampCapsule>();Check(capsule&&capsule.item==5,"Machine dispenses selected trampoline capsule");
                        if(capsule){p.Body.position=new Vector3(0,.3f,-10);capsule.GetComponent<Rigidbody>().position=new Vector3(0,1,-9.5f);capsule.GetComponent<Rigidbody>().linearVelocity=Vector3.zero;}
                        break;
                    case 3:InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.E));break;
                    case 4:
                        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Check(pocket.Items.Contains(5),"E picks up capsule into inventory");
                        if(pocket.Items.Contains(5))pocket.Use(pocket.Items.IndexOf(5),p);Check(UnityEngine.Object.FindObjectsByType<SwampBounce>().Length>=2,"Inventory deploys usable trampoline");
                        speed=p.Config.normal.moveSpeed;pocket.Items.Add(8);pocket.Use(pocket.Items.Count-1,p);Check(p.Config.normal.moveSpeed>speed,"Caffeine gum changes only player configuration");
                        p.Body.position=hub.boat.position+Vector3.up*.5f;p.Body.linearVelocity=Vector3.zero;break;
                    case 5:Check(p.transform.position.y>-.3f,"New raft supports player");Check(hub.BeginDeparture(p),"New raft starts departure");next=EditorApplication.timeSinceStartup+4;break;
                    case 6:Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="LEVEL1","Departure loads LEVEL1");Check(SwampPocket.Instance==pocket,"Inventory manager survives level transition");LevelOnePlaytest.Begin();next=EditorApplication.timeSinceStartup+18;break;
                    case 7:Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Lobby","Completed expedition returns to Lobby");Check(pocket.Coins==1100,"Level reward grants 150 coins once");Restore();File.WriteAllLines("Captures/Lobby_V2_Playtest.txt",checks);Debug.Log("[LOBBY V2 QA] "+string.Join(" | ",checks));EditorApplication.update-=Tick;break;
                }
            }catch(Exception e){checks.Add("FAIL exception "+e);Restore();File.WriteAllLines("Captures/Lobby_V2_Playtest.txt",checks);EditorApplication.update-=Tick;Debug.LogException(e);}
        }
        static void Restore(){if(pocket){pocket.Coins=coins;pocket.Save();}InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInput;}
    }
}
