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
    public static class SwampLobbyPlaytest
    {
        static int step,frame;static double next;static Vector3 start;static LizardPlayer player;static SwampLobbyHub hub;
        static readonly List<string> checks=new();
        static void Check(bool valid,string name)=>checks.Add((valid?"PASS ":"FAIL ")+name);
        public static void Begin()
        {
            if(!Application.isPlaying)throw new Exception("Play required");
            checks.Clear();hub=UnityEngine.Object.FindAnyObjectByType<SwampLobbyHub>();player=LizardPlayer.All.First(x=>x.IsControlled);start=player.transform.position;frame=Time.frameCount;step=0;next=EditorApplication.timeSinceStartup+1.2;
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));EditorApplication.update-=Tick;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!Application.isPlaying){EditorApplication.update-=Tick;return;}
            if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
            try{
                switch(step++){
                    case 0:
                        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Check(Time.frameCount>frame,"Frames advance");Check(Vector3.Distance(start,player.transform.position)>.5f,"Walking on island");Check(!LizardLobby.Visible,"Room menu initially closed");
                        hub.CycleHat(player);Check(player.transform.Find("Sombrero del refugio"),"Wardrobe equips cosmetic");hub.roomMenu.OpenRoomMenu();Check(LizardLobby.Visible,"Telephone opens room menu");hub.roomMenu.SendMessage("PlaySolo");Check(!LizardLobby.Visible,"Solo closes menu");
                        player.Body.position=new Vector3(0,.4f,-1.5f);player.Body.linearVelocity=Vector3.zero;
                        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));break;
                    case 1:
                        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Check(player.transform.position.z>-1,"Dock is walkable");
                        player.Body.position=hub.boat.position+Vector3.up*.5f;player.Body.linearVelocity=Vector3.zero;break;
                    case 2:
                        Check(player.transform.position.y>-.3f,"Raft supports player");Check(hub.BeginDeparture(player),"Departure starts countdown");next=EditorApplication.timeSinceStartup+4;break;
                    case 3:
                        Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="LEVEL1","Boat loads LEVEL1");File.WriteAllLines("Captures/Lobby_Playtest.txt",checks);Debug.Log("[LOBBY QA] "+string.Join(" | ",checks));EditorApplication.update-=Tick;break;
                }
            }catch(Exception e){checks.Add("FAIL "+e);File.WriteAllLines("Captures/Lobby_Playtest.txt",checks);Debug.LogException(e);EditorApplication.update-=Tick;}
        }
    }
}
