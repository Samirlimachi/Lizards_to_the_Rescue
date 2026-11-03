using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    public class SwampPocket : MonoBehaviour
    {
        public static SwampPocket Instance;
        public int Coins=400;public readonly List<int> Items=new();
        public static SwampPocket Ensure(){if(Instance)return Instance;var go=new GameObject("Inventario de expedición");Instance=go.AddComponent<SwampPocket>();Instance.Coins=PlayerPrefs.GetInt("Lizards.HubCoins",400);DontDestroyOnLoad(go);return Instance;}
        public void Save(){PlayerPrefs.SetInt("Lizards.HubCoins",Coins);PlayerPrefs.Save();}
        void OnDestroy(){if(Instance==this)Instance=null;}
        void Update()
        {
            if(LizardLobby.Visible)return;var p=LizardPlayer.All.FirstOrDefault(x=>x.IsControlled);var k=Keyboard.current;if(!p||k==null)return;
            int slot=k.digit1Key.wasPressedThisFrame?0:k.digit2Key.wasPressedThisFrame?1:k.digit3Key.wasPressedThisFrame?2:-1;
            if(slot>=0&&slot<Items.Count)Use(slot,p);
        }
        public void Use(int slot,LizardPlayer p)
        {
            int item=Items[slot];Items.RemoveAt(slot);var buff=p.GetComponent<SwampPlayerBuff>()??p.gameObject.AddComponent<SwampPlayerBuff>();
            if(item>=6){buff.Apply(item);p.Message("Usado: "+SwampLobbyExpansion.Names[item]);return;}
            if(item==3){buff.bagUntil=Time.time+12;p.Message("Bolsa abierta: caída lenta durante 12 segundos.");return;}
            if(item==4){var shot=new GameObject("Selfie en dos segundos").AddComponent<SwampSelfie>();p.Message("Foto en dos segundos…");return;}
            var obj=GameObject.CreatePrimitive(item==5?PrimitiveType.Cylinder:PrimitiveType.Sphere);obj.name=SwampLobbyExpansion.Names[item];obj.transform.position=p.transform.position+p.transform.forward*2+Vector3.up*.8f;
            obj.transform.localScale=item==5?new Vector3(2.4f,.15f,2.4f):item==2?new Vector3(1.4f,1.8f,1.2f):Vector3.one*.5f;obj.GetComponent<Renderer>().material=LizardMaterials.Lit(item==1?new Color(.3f,.45f,.16f):item==2?new Color(.3f,.8f,.2f):new Color(.65f,.25f,.85f),.4f);
            var rb=obj.AddComponent<Rigidbody>();rb.mass=item==5?8:1;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;obj.AddComponent<TongueTarget>().kind=TongueTargetKind.Object;
            if(item==0){var mat=new PhysicsMaterial("Rebote"){bounciness=.95f,bounceCombine=PhysicsMaterialCombine.Maximum};obj.GetComponent<Collider>().material=mat;rb.linearVelocity=(Camera.main?Camera.main.transform.forward:p.transform.forward)*18;obj.AddComponent<SwampToy>().kind=0;}
            if(item==1){rb.linearVelocity=p.transform.forward*8+Vector3.up*4;obj.AddComponent<SwampToy>().kind=1;}
            if(item==2){obj.AddComponent<SwampToy>().kind=2;foreach(float x in new[]{-.35f,.35f}){var eye=GameObject.CreatePrimitive(PrimitiveType.Sphere);eye.transform.SetParent(obj.transform,false);eye.transform.localPosition=new Vector3(x,.45f,.35f);eye.transform.localScale=Vector3.one*.25f;Destroy(eye.GetComponent<Collider>());eye.GetComponent<Renderer>().material=LizardMaterials.Lit(Color.white,.1f);}}
            if(item==5){rb.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;obj.AddComponent<SwampBounce>();}
        }
    }
}
