using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lizards.EditorTools
{
    public static class LevelTwoExpeditionValidation
    {
        public static string Paths()
        {
            if(Application.isPlaying)throw new Exception("Run path validation outside Play mode");
            Physics.SyncTransforms();var sources=new List<NavMeshBuildSource>();var marks=new List<NavMeshBuildMarkup>();
            var bounds=new Bounds(new Vector3(0,3,5),new Vector3(90,30,100));
            NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,marks,sources);
            sources=sources.Where(s=>!s.component||(!s.component.GetComponentInParent<Rigidbody>()&&(!(s.component is Collider c)||!c.isTrigger))).ToList();
            var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.30f;settings.agentHeight=1.0f;settings.agentClimb=.35f;settings.agentSlope=45;settings.overrideVoxelSize=true;settings.voxelSize=.10f;
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            var instance=NavMesh.AddNavMeshData(data);var lines=new List<string>();
            try
            {
                void Path(string label,Vector3 start,Vector3 end,float maxLength=500)
                {
                    var path=new NavMeshPath();bool a=NavMesh.SamplePosition(start,out var from,1,NavMesh.AllAreas),b=NavMesh.SamplePosition(end,out var to,1,NavMesh.AllAreas);
                    bool complete=a&&b&&NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                    float distance=0;for(int i=1;i<path.corners.Length;i++)distance+=Vector3.Distance(path.corners[i-1],path.corners[i]);
                    lines.Add((complete&&distance<maxLength?"PASS ":"FAIL ")+label+" · "+distance.ToString("0.0")+"m · "+path.status);
                }
                var start=new Vector3(0,.3f,-31);
                Path("Raft to central bowl",start,new Vector3(0,0,-17));
                Path("Raft to west bank",start,new Vector3(-34,0,-12));
                Path("Raft to damp stores",start,new Vector3(-25,0,12));
                Path("Raft to cargo warehouse",start,new Vector3(22,0,-10));
                Path("Raft to filter balcony",start,new Vector3(32,2.8f,17));
                Path("Raft to raised pump bridge",start,new Vector3(0,2.6f,16));
                Path("Raft to northern pantry",start,new Vector3(0,1.2f,34));
                Path("West gallery to stores direct connection",new Vector3(-28,0,-3),new Vector3(-28,0,7),15);
                Path("Warehouse to filters direct connection",new Vector3(28,0,0),new Vector3(28,0,7),12);
                Path("West outside circuit is open",new Vector3(-25,0,24),new Vector3(-25,0,37),23);
                Path("East outside circuit is open",new Vector3(25,0,24),new Vector3(25,0,37),23);
                Path("West circuit enters pantry",new Vector3(-25,0,37),new Vector3(-8,1.2f,36),26);
                Path("East circuit enters pantry",new Vector3(25,0,37),new Vector3(8,1.2f,36),26);
            }
            finally{instance.Remove();UnityEngine.Object.DestroyImmediate(data);}
            Directory.CreateDirectory("Captures");File.WriteAllLines("Captures/LEVEL2_Paths.txt",lines);return string.Join("\n",lines);
        }

        public static void CaptureMap(string destination)
        {
            var roofs=UnityEngine.Object.FindObjectsByType<Renderer>().Where(r=>r.name.StartsWith("Bóveda de ")).ToArray();
            var state=roofs.Select(r=>r.enabled).ToArray();foreach(var r in roofs)r.enabled=false;
            var go=new GameObject("Temporary map camera");
            try
            {
                var camera=go.AddComponent<Camera>();camera.transform.position=new Vector3(0,115,-28);camera.transform.LookAt(new Vector3(0,0,4));camera.orthographic=true;camera.orthographicSize=50;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.10f,.18f);camera.farClipPlane=220;
                var rt=new RenderTexture(1600,1600,24);rt.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1600,1600,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1600),0,0);texture.Apply();File.WriteAllBytes(destination,texture.EncodeToPNG());RenderTexture.active=previous;
                UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);for(int i=0;i<roofs.Length;i++)if(roofs[i])roofs[i].enabled=state[i];}
        }
    }
}
