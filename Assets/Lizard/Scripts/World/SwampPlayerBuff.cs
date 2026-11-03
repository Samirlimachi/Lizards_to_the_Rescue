using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    public class SwampPlayerBuff : MonoBehaviour
    {
        public float bagUntil;LizardPlayer p;bool copied;
        void Awake(){p=GetComponent<LizardPlayer>();}
        public void Apply(int item){if(!copied){p.config=Instantiate(p.Config);copied=true;}if(item==6){p.config.maxFood+=6;p.config.giantThreshold+=4;}if(item==7){p.config.normal.tongueRange+=6;p.config.giant.tongueRange+=3;}if(item==8)p.config.normal.moveSpeed*=1.25f;}
        void FixedUpdate(){if(Time.time<bagUntil&&p.Body.linearVelocity.y < -2.5f){var v=p.Body.linearVelocity;v.y=-2.5f;p.Body.linearVelocity=v;}}
        void OnDestroy(){if(copied&&p&&p.config)Destroy(p.config);}
    }
}
