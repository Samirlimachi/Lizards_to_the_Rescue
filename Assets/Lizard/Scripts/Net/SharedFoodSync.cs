using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lizards
{
    /// <summary>Shared-mode authority for the authored salteña family. Player RPCs carry reliable lifecycle events.</summary>
    [RequireComponent(typeof(FoodItem))]
    [DefaultExecutionOrder(-10)]
    public sealed class SharedFoodSync : MonoBehaviour
    {
        public string id;
        [Range(0,6)] public int variant;
        public static readonly Dictionary<string,SharedFoodSync> Live=new();
        public static readonly HashSet<string> Removed=new();
        public static LizardNetSync Local=>LizardNetSync.All.FirstOrDefault(n=>n&&n.Object&&n.IsLocal&&n.Runner&&n.Runner.IsRunning);
        public static bool Online=>Local;
        public static bool Master=>Local&&Local.Runner.IsSharedModeMasterClient;
        public bool Replica=>Online&&!Master;
        public bool ApprovedEat {get;private set;}
        public bool Reserved {get;private set;}
        FoodItem food;
        readonly HashSet<LizardPlayer> remoteHolders=new();
        Vector3 targetPosition,targetVelocity,lastSent;
        Quaternion targetRotation,lastRotation;
        bool hasPose,wasReplica;
        float nextSend;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){Live.Clear();Removed.Clear();UnityEngine.SceneManagement.SceneManager.sceneLoaded-=Loaded;UnityEngine.SceneManagement.SceneManager.sceneLoaded+=Loaded;}
        static void Loaded(UnityEngine.SceneManagement.Scene scene,UnityEngine.SceneManagement.LoadSceneMode mode){Removed.Clear();}
        void Awake(){food=GetComponent<FoodItem>();}
        void OnEnable(){Bind(string.IsNullOrEmpty(id)?System.Guid.NewGuid().ToString("N"):id);}
        void OnDisable(){if(!string.IsNullOrEmpty(id)&&Live.TryGetValue(id,out var f)&&f==this)Live.Remove(id);}
        public void Bind(string key){if(!string.IsNullOrEmpty(id)&&Live.TryGetValue(id,out var old)&&old==this)Live.Remove(id);id=key;if(isActiveAndEnabled)Live[id]=this;}
        public static SharedFoodSync Find(string key)=>!string.IsNullOrEmpty(key)&&Live.TryGetValue(key,out var food)&&food?food:null;
        public static bool Pulling(LizardPlayer p,FoodItem f,out float tension)
        {
            tension=0;if(!p||!p.Tongue)return false;
            if(!p.Tongue.NetMirror){tension=p.Tongue.Tension01;return p.Tongue.HeldFood==f;}
            var n=p.GetComponent<LizardNetSync>();var sync=f.GetComponent<SharedFoodSync>();
            if(!n||!n.Object||!sync||n.TongueFoodNet.ToString()!=sync.id||n.TongueStateNet!=(int)TongueState.Attached)return false;
            tension=n.TongueTensionNet;return true;
        }
        void FixedUpdate()
        {
            if(!Online)return;
            foreach(var n in LizardNetSync.All)
            {
                if(!n||!n.Object||n.IsLocal||!n.Player)continue;
                bool holding=n.TongueFoodNet.ToString()==id&&n.TongueStateNet==(int)TongueState.Attached;
                if(holding){food.TryGrab(n.Player);remoteHolders.Add(n.Player);}
                else if(remoteHolders.Remove(n.Player))food.ReleaseGrab(n.Player);
            }
            if(Replica)
            {
                food.Body.isKinematic=true;wasReplica=true;
                if(hasPose){food.Body.MovePosition(Vector3.Lerp(food.Body.position,targetPosition,.4f));food.Body.MoveRotation(Quaternion.Slerp(food.Body.rotation,targetRotation,.4f));}
                return;
            }
            if(wasReplica){food.Body.isKinematic=false;food.Body.linearVelocity=targetVelocity;wasReplica=false;}
            foreach(var n in LizardNetSync.All)
            {
                if(!n||!n.Object||n.IsLocal||!n.Player||n.TongueFoodNet.ToString()!=id||n.TongueStateNet!=(int)TongueState.Attached)continue;
                var p=n.Player;Vector3 delta=p.MouthPosition-food.Body.worldCenterOfMass;float distance=delta.magnitude,stretch=distance-n.TongueLengthNet;
                if(distance<.001f||stretch<=0)continue;Vector3 direction=delta/distance;
                float rel=Vector3.Dot(food.Body.linearVelocity-p.Body.linearVelocity,direction);
                float force=Mathf.Clamp((p.Config.tongueSpring*stretch-p.Config.tongueDamper*rel)*food.Body.mass,0,p.Stats.tongueMaxForce);
                food.Body.AddForce(direction*force,ForceMode.Force);
            }
            if(Time.time>=nextSend&&(Vector3.Distance(lastSent,food.Body.position)>.015f||Quaternion.Angle(lastRotation,food.Body.rotation)>2||Time.time>=nextSend+1))
            {nextSend=Time.time+.10f;lastSent=food.Body.position;lastRotation=food.Body.rotation;Local.RPC_FoodPose(id,lastSent,lastRotation,food.Body.linearVelocity);}
        }
        public void Pose(Vector3 p,Quaternion q,Vector3 v){targetPosition=p;targetRotation=q;targetVelocity=v;hasPose=true;}
        public bool RequestEat(LizardPlayer player)
        {if(Reserved||!Local||Local.Player!=player)return false;Local.RPC_RequestFoodEat(id);return true;}
        public static void GrantEat(string key,Fusion.NetworkId eater)
        {
            var sync=Find(key);if(!sync)return;
            Removed.Add(key);var local=Local;
            if(local&&local.Object.Id==eater)
            {sync.ApprovedEat=true;if(!local.Player.TryEat(sync.food)){sync.ApprovedEat=false;Debug.LogWarning("Food eat approval could not be applied: "+key);}return;}
            Object.Destroy(sync.gameObject);sync.gameObject.SetActive(false);
        }
        public void Reserve()=>Reserved=true;
        public static void Delete(string key)
        {Removed.Add(key);var f=Find(key);if(f){f.gameObject.SetActive(false);Object.Destroy(f.gameObject);}}
        public static void AnnounceSpit(FoodItem food,LizardPlayer owner)
        {
            var sync=food.GetComponent<SharedFoodSync>();if(!sync)return;
            sync.Bind(System.Guid.NewGuid().ToString("N"));sync.Reserved=false;sync.ApprovedEat=false;
            if(Online&&Local.Player==owner)Local.RPC_RequestFoodSpawn(sync.id,sync.variant,food.transform.position,food.transform.rotation,food.nutrition,food.Body.mass,food.Body.linearVelocity);
        }
        public static SharedFoodSync Spawn(string key,int variant,Vector3 p,Quaternion q,float value,float mass,Vector3 velocity)
        {
            if(Removed.Contains(key))return null;
            var existing=Find(key);if(existing){existing.Pose(p,q,velocity);return existing;}
            string name=variant==0?"Saltena_Whole":"Saltena_Piece_"+variant.ToString("00");
            var prefab=Resources.Load<FoodItem>("Saltena/"+name);if(!prefab)return null;
            var food=Instantiate(prefab,p,q);var sync=food.GetComponent<SharedFoodSync>();sync.Bind(key);food.nutrition=value;food.Body.mass=mass;
            food.MarkEjected(null,.2f,0);if(Master)food.Body.linearVelocity=velocity;else{food.Body.isKinematic=true;sync.Pose(p,q,velocity);}return sync;
        }
    }
}
