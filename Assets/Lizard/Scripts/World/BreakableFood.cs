using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>One physical food splits into authored, edible pieces, conserving nutrition and mass.</summary>
    [RequireComponent(typeof(FoodItem))]
    [DefaultExecutionOrder(150)]
    public sealed class BreakableFood : MonoBehaviour
    {
        [System.Serializable] public struct Piece
        {
            public FoodItem prefab;
            public Vector3 offset;
            [Min(.001f)] public float weight;
        }
        public Piece[] pieces;
        public Transform visual;
        public float impactSpeed=4.2f, normalImpactSpeed=3.2f, minimumImpulse=2.5f;
        public float pullTension=.28f, opposingDot=-.4f, pullDuration=.65f;
        public bool Broken {get;private set;}
        public float TearProgress {get;private set;}
        public string LastBreakCause {get;private set;}
        public IReadOnlyList<FoodItem> SpawnedPieces=>spawned;
        readonly List<FoodItem> spawned=new();
        FoodItem food;
        Vector3 originalVisualScale;
        float immuneUntil;
        void Awake(){food=GetComponent<FoodItem>();if(visual)originalVisualScale=visual.localScale;}
        void OnEnable(){immuneUntil=Time.time+.8f;TearProgress=0;}
        void OnDisable(){TearProgress=0;if(visual)visual.localScale=originalVisualScale;}
        void FixedUpdate()
        {
            if(Broken||food.State==FoodState.Consumed||GetComponent<SharedFoodSync>()?.Replica==true)return;
            bool opposed=false;
            for(int i=0;i<LizardPlayer.All.Count;i++)
            {
                var a=LizardPlayer.All[i];if(!SharedFoodSync.Pulling(a,food,out float ta)||ta<pullTension)continue;
                Vector3 direction=(a.MouthPosition-food.transform.position).normalized;
                for(int j=i+1;j<LizardPlayer.All.Count;j++)
                {
                    var b=LizardPlayer.All[j];if(!SharedFoodSync.Pulling(b,food,out float tb)||tb<pullTension)continue;
                    if(Vector3.Dot(direction,(b.MouthPosition-food.transform.position).normalized)<=opposingDot)opposed=true;
                }
            }
            TearProgress=Mathf.MoveTowards(TearProgress,opposed?1:0,Time.fixedDeltaTime/(opposed?pullDuration:.22f));
            if(visual)visual.localScale=Vector3.Scale(originalVisualScale,new Vector3(1+TearProgress*.15f,1-TearProgress*.05f,1-TearProgress*.05f));
            if(TearProgress>=.999f)Split("tirón de jugadores",Vector3.up);
        }
        void OnCollisionEnter(Collision collision)
        {
            if(Broken||Time.time<immuneUntil||collision.collider.GetComponentInParent<LizardPlayer>()||collision.collider.GetComponentInParent<FoodItem>())return;
            if(collision.relativeVelocity.magnitude<impactSpeed||collision.impulse.magnitude<minimumImpulse)return;
            float normalSpeed=0;
            for(int i=0;i<collision.contactCount;i++)normalSpeed=Mathf.Max(normalSpeed,Mathf.Abs(Vector3.Dot(collision.relativeVelocity,collision.GetContact(i).normal)));
            if(normalSpeed>=normalImpactSpeed)Split("golpe contra estructura",collision.GetContact(0).normal);
        }
        public bool Split(string cause,Vector3 impulseDirection,bool received=false,Vector3 replicatedVelocity=default,Vector3 replicatedAngular=default)
        {
            var shared=GetComponent<SharedFoodSync>();if(shared&&shared.Replica&&!received)return false;
            if(Broken||!food||food.State==FoodState.Consumed||pieces==null||pieces.Length==0)return false;
            float totalWeight=0;foreach(var piece in pieces){if(!piece.prefab||piece.weight<=0)return false;totalWeight+=piece.weight;}
            if(totalWeight<=0)return false;
            Broken=true;LastBreakCause=cause;
            var body=food.Body;float value=food.nutrition,mass=body.mass;
            Vector3 position=transform.position,scale=transform.lossyScale,velocity=body.isKinematic?Vector3.zero:body.linearVelocity,angular=body.isKinematic?Vector3.zero:body.angularVelocity;
            Quaternion rotation=transform.rotation;
            if(received){velocity=replicatedVelocity;angular=replicatedAngular;}
            // Release before disabling the original so carried-body physics and ignored contacts are restored.
            foreach(var p in LizardPlayer.All.ToArray())if(p&&p.Tongue&&p.Tongue.HeldFood==food)p.Tongue.Release(false);
            float remaining=value,remainingMass=mass;
            for(int i=0;i<pieces.Length;i++)
            {
                var piece=pieces[i];Vector3 offset=rotation*Vector3.Scale(piece.offset,scale);
                var child=Instantiate(piece.prefab,position+offset,rotation);
                child.transform.localScale=Vector3.Scale(child.transform.localScale,scale);
                child.nutrition=i==pieces.Length-1?remaining:value*piece.weight/totalWeight;remaining-=child.nutrition;
                child.Body.mass=i==pieces.Length-1?remainingMass:mass*piece.weight/totalWeight;remainingMass-=child.Body.mass;
                child.Body.linearVelocity=velocity+Vector3.Cross(angular,offset)+offset.normalized*.65f+impulseDirection.normalized*.4f;
                child.Body.angularVelocity=angular;spawned.Add(child);
                var childSync=child.GetComponent<SharedFoodSync>();if(shared&&childSync)childSync.Bind(shared.id+"/"+i);
                child.MarkEjected(null,.18f,0);
            }
            // Splitting replaces food; it does not award a delivery or a swallow event.
            if(shared&&SharedFoodSync.Online&&!received)
            {SharedFoodSync.Removed.Add(shared.id);SharedFoodSync.Local.RPC_FoodSplit(shared.id,position,rotation,velocity,angular,value,impulseDirection);}
            food.nutrition=0;gameObject.SetActive(false);Destroy(gameObject);
            Sfx.Play(Sfx.Break,position,.55f);Fx.Burst(position,new Color(.9f,.55f,.16f),12,1.3f,.045f);
            return true;
        }
    }
}
