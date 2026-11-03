using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Lizards.EditorTools
{
    public static class LevelOnePlaytest
    {
        static int step;static double next;static LizardPlayer player;static Vector3 start;static int frame;
        static readonly List<string> checks=new();
        static void Assert(bool condition,string name){checks.Add((condition?"PASS ":"FAIL ")+name);}
        public static void Begin()
        {
            if(!Application.isPlaying)throw new Exception("Enter Play first");
            player=LizardPlayer.All.First(p=>p.IsControlled);start=player.transform.position;frame=Time.frameCount;checks.Clear();step=0;next=EditorApplication.timeSinceStartup+1;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            if(Keyboard.current!=null)InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.W));
        }
        static void Tick()
        {
            if(!Application.isPlaying){EditorApplication.update-=Tick;return;}
            if(EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+1.5;
            try
            {
                switch(step++)
                {
                    case 0:
                        if(Keyboard.current!=null)InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
                        Assert(Time.frameCount>frame,"Player loop advances");Assert(Vector3.Distance(player.transform.position,start)>.5f,"W movement changes player position");
                        var crate=GameObject.Find("Contrapeso naranja · 20 kg").GetComponent<Rigidbody>();crate.position=new Vector3(4,1.1f,15);crate.linearVelocity=Vector3.zero;break;
                    case 1:
                        Assert(UnityEngine.Object.FindAnyObjectByType<PressurePlate>().Pressed,"20kg crate activates 12kg plate");
                        Assert(GameObject.Find("Compuerta del almacén").transform.position.y>3,"Pressure plate opens gate");
                        var acid=UnityEngine.Object.FindAnyObjectByType<AcidVulnerable>();acid.HitByAcid();acid.HitByAcid();
                        foreach(var b in UnityEngine.Object.FindObjectsByType<Breakable>())Assert(b.TryBreak(5,true,Vector3.forward*10),"Giant breaks pallet");
                        var f=FoodFactory.CreateFruit(player.MouthPosition,1,.15f);Assert(player.TryEat(f),"Player eats food");
                        player.Body.position=new Vector3(-4,.15f,45.7f);player.Body.linearVelocity=Vector3.zero;player.Stomach.DebugSetFood(2);break;
                    case 2:
                        Assert(!UnityEngine.Object.FindAnyObjectByType<AcidVulnerable>(),"Acid barrier dissolves after two hits");
                        player.EjectOrFeed();
                        FoodFactory.CreateFruit(new Vector3(5,1,49),8,.18f);break;
                    case 3:
                        var mission=UnityEngine.Object.FindAnyObjectByType<LevelOneMission>();
                        Assert(mission.survivor.Form!=LizardForm.Small,"Feeding rescues Pip");
                        Assert(mission.depot.Delivered>=8,"Depot consumes physical food");Assert(mission.Ready,"Rescue and quota unlock exit");
                        player.Body.position=new Vector3(0,.2f,56);player.Body.linearVelocity=Vector3.zero;break;
                    case 4:
                        Assert(UnityEngine.Object.FindAnyObjectByType<LevelOneMission>().Completed,"Entering exit completes level");
                        File.WriteAllLines("Captures/LEVEL1_Playtest.txt",checks);Debug.Log("[LEVEL1 QA] "+string.Join(" | ",checks));EditorApplication.update-=Tick;break;
                }
            }
            catch(Exception e){checks.Add("FAIL exception "+e);File.WriteAllLines("Captures/LEVEL1_Playtest.txt",checks);EditorApplication.update-=Tick;Debug.LogException(e);}
        }
    }
}
