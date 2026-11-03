using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    public class SwampVendorButton : MonoBehaviour
    {
        public SwampLobbyExpansion vendor;public int action;float next;
        void Update(){if(Time.time<next)return;foreach(var p in LizardPlayer.All)if(p.Tongue&&p.Tongue.State==TongueState.Attached&&Vector3.Distance(p.Tongue.TipPosition,transform.position)<.55f){if(action==0)vendor.Buy(p);else vendor.Browse(action);p.Tongue.Release(false);next=Time.time+.6f;break;}}
    }
}
