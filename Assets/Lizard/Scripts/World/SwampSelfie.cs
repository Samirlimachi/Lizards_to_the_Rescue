using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lizards
{
    public class SwampSelfie : MonoBehaviour
    {
        void Start(){Invoke(nameof(Shoot),2);}
        void Shoot(){string path=System.IO.Path.Combine(Application.persistentDataPath,"Selfie_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".png");ScreenCapture.CaptureScreenshot(path);foreach(var p in LizardPlayer.All.Where(p=>p.IsControlled))p.Message("Foto guardada: "+path);Destroy(gameObject);}
    }
}
