using Fusion;
using UnityEngine;
using System.Linq;

namespace Lizards
{
    public partial class LizardNetSync
    {
        [Networked] public NetworkString<_128> TongueFoodNet {get;set;}
        public LizardPlayer Player=>player;
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        public void RPC_FoodPose(string key,Vector3 position,Quaternion rotation,Vector3 velocity)
        {if(!Runner.IsSharedModeMasterClient&&Object.StateAuthority==Runner.GetMasterClient())SharedFoodSync.Find(key)?.Pose(position,rotation,velocity);}
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        public void RPC_FoodSplit(string key,Vector3 position,Quaternion rotation,Vector3 velocity,Vector3 angular,float value,Vector3 direction)
        {
            if(Runner.IsSharedModeMasterClient||Object.StateAuthority!=Runner.GetMasterClient())return;
            SharedFoodSync.Removed.Add(key);var sync=SharedFoodSync.Find(key);if(!sync)return;
            sync.transform.SetPositionAndRotation(position,rotation);sync.GetComponent<FoodItem>().nutrition=value;
            sync.GetComponent<BreakableFood>().Split("tirón o golpe compartido",direction,true,velocity,angular);
        }
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        public void RPC_RequestFoodEat(string key)
        {
            if(!Runner.IsSharedModeMasterClient||!player)return;
            var sync=SharedFoodSync.Find(key);if(!sync||sync.Reserved)return;var food=sync.GetComponent<FoodItem>();
            if(!food.CanBeEatenBy(player)||!food.FitsMouth(player.Stats.mouthCapacity)||food.GrabberCount>1||Vector3.Distance(player.MouthPosition,food.transform.position)>player.Config.eatRadius+1||player.Stomach.Food>=player.Config.maxFood)return;
            sync.Reserve();SharedFoodSync.Local.RPC_FoodEatGranted(key,Object.Id);
        }
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        public void RPC_FoodEatGranted(string key,NetworkId eater)
        {if(Object.StateAuthority==Runner.GetMasterClient())SharedFoodSync.GrantEat(key,eater);}
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        public void RPC_RequestFoodSpawn(string key,int variant,Vector3 p,Quaternion q,float value,float mass,Vector3 velocity)
        {
            if(!Runner.IsSharedModeMasterClient||!player||variant<0||variant>6||value<=0||value>player.Config.maxFood||mass<=0||Vector3.Distance(player.MouthPosition,p)>3)return;
            SharedFoodSync.Local.RPC_FoodSpawn(key,variant,p,q,value,mass,Vector3.ClampMagnitude(velocity,30));
        }
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        public void RPC_FoodSpawn(string key,int variant,Vector3 p,Quaternion q,float value,float mass,Vector3 velocity)
        {if(Object.StateAuthority==Runner.GetMasterClient())SharedFoodSync.Spawn(key,variant,p,q,value,mass,velocity);}
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        public void RPC_FoodDelivered(string key,float delivered)
        {
            if(Object.StateAuthority!=Runner.GetMasterClient())return;
            SharedFoodSync.Delete(key);if(!Runner.IsSharedModeMasterClient)UnityEngine.Object.FindAnyObjectByType<FoodGoal>()?.SynchronizeDelivery(delivered);
        }
        [Rpc(RpcSources.StateAuthority,RpcTargets.All)]
        public void RPC_RequestFoodState()
        {
            if(!Runner.IsSharedModeMasterClient)return;
            var source=SharedFoodSync.Local;float value=UnityEngine.Object.FindAnyObjectByType<FoodGoal>()?.Delivered??0;
            foreach(string key in SharedFoodSync.Removed.ToArray())source.RPC_FoodDelivered(key,value);
            foreach(var f in SharedFoodSync.Live.Values.ToArray())if(f){var food=f.GetComponent<FoodItem>();source.RPC_FoodSpawn(f.id,f.variant,f.transform.position,f.transform.rotation,food.nutrition,food.Body.mass,food.Body.isKinematic?Vector3.zero:food.Body.linearVelocity);}
        }
    }
}

