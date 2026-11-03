using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    public class SwampToy : MonoBehaviour
    {
        public int kind;float until;
        readonly Dictionary<PatrolEnemy,(Vector3 a,Vector3 b)> routes=new();
        void Start(){until=Time.time+(kind==1?3:25);}
        void Update(){if(Time.time>until){if(kind==1||kind==2)Explode();else Destroy(gameObject);}if(kind==2)foreach(var e in FindObjectsByType<PatrolEnemy>())if(Vector3.Distance(e.transform.position,transform.position)<12){if(!routes.ContainsKey(e))routes[e]=(e.pointA,e.pointB);e.pointA=transform.position;e.pointB=transform.position;}}
        void OnDestroy(){foreach(var pair in routes)if(pair.Key){pair.Key.pointA=pair.Value.a;pair.Key.pointB=pair.Value.b;}}
        void OnCollisionEnter(Collision c){if(kind==0)c.collider.GetComponentInParent<EnemyHealth>()?.Damage(1,transform.position);}
        void Explode(){foreach(var c in Physics.OverlapSphere(transform.position,4)){c.GetComponentInParent<EnemyHealth>()?.Damage(3,transform.position);if(c.attachedRigidbody)c.attachedRigidbody.AddExplosionForce(250,transform.position,4,1);}Fx.Burst(transform.position,new Color(1,.7f,.1f),35,5,.12f);Sfx.Play(Sfx.Break,transform.position);Destroy(gameObject);}
    }
}
