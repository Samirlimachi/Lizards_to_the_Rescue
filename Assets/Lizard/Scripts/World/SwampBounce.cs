using System.Collections.Generic;
using UnityEngine;

namespace Lizards
{
    /// <summary>
    /// Cama elástica: al caer encima te lanza hacia arriba. La lona se hunde y rebota (si hay una asignada).
    /// </summary>
    public class SwampBounce : MonoBehaviour
    {
        public Transform mat;
        public float bounceSpeed = 12f;
        readonly Dictionary<Rigidbody,float> cooldown=new();
        Vector3 matScale; float squash, squashVel;

        void Start(){ if(mat) matScale=mat.localScale; }

        void OnCollisionEnter(Collision c)
        {
            var p=c.collider.GetComponentInParent<LizardPlayer>();
            if(!p||p.transform.position.y<transform.position.y)return;
            if(cooldown.TryGetValue(p.Body,out float t)&&Time.time<t)return;
            var v=p.Body.linearVelocity;v.y=bounceSpeed;p.Body.linearVelocity=v;cooldown[p.Body]=Time.time+.65f;
            squashVel-=9f;
            Sfx.Play(Sfx.Jump,transform.position,0.9f);
            Fx.Burst(p.transform.position,new Color(1f,0.9f,0.4f),10,2.5f,0.08f);
        }

        void Update()
        {
            if(!mat)return;
            float dt=Mathf.Min(Time.deltaTime,1f/30f);
            squashVel+=(-squash*180f-squashVel*9f)*dt;squash+=squashVel*dt;
            mat.localScale=new Vector3(matScale.x*(1f-squash*0.04f),matScale.y,matScale.z*(1f-squash*0.04f));
            mat.localPosition=new Vector3(mat.localPosition.x,squash*0.25f,mat.localPosition.z);
        }
    }
}
