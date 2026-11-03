using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    public class SwampCapsule : MonoBehaviour
    {
        public int item;
        void Update(){foreach(var p in LizardPlayer.All.Where(x=>x.IsControlled)){bool close=Vector3.Distance(p.MouthPosition,transform.position)<1.4f;bool pulled=p.Tongue&&p.Tongue.State==TongueState.Attached&&Vector3.Distance(p.MouthPosition,transform.position)<.6f;if(close&&(pulled||p.Input.Pressed(p.Input.Interact))){if(SwampPocket.Ensure().Items.Count>=3){p.Message("Inventario lleno: usa 1, 2 o 3.");return;}p.Tongue.Release(false);SwampPocket.Ensure().Items.Add(item);p.Message("Recogido: "+SwampLobbyExpansion.Names[item]);Destroy(gameObject);return;}}}
    }
}
