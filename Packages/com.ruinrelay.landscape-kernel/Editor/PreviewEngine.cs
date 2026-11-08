using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;

namespace RuinRelay.LandscapeKernel
{
    [Serializable] public sealed class PreviewCamera
    {
        public Vector3 position,target; public float fieldOfView=60;
    }
    public sealed class PreviewFrame:IDisposable
    {
        public TerrainPlan plan;public Texture2D before,after,delta;
        public double milliseconds;public float persistedPreviewMaxMetres;
        public void Dispose(){foreach(var t in new[]{before,after,delta})if(t)UnityEngine.Object.DestroyImmediate(t);before=after=delta=null;}
        public void Export(string folder,PlanSettings settings,PreviewCamera camera)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder,"before.png"),before.EncodeToPNG());File.WriteAllBytes(Path.Combine(folder,"preview.png"),after.EncodeToPNG());File.WriteAllBytes(Path.Combine(folder,"delta.png"),delta.EncodeToPNG());
            File.WriteAllText(Path.Combine(folder,"preview.json"),JsonConvert.SerializeObject(new{settings=new{settings.mode,settings.intent,start=new[]{settings.start.x,settings.start.y},end=new[]{settings.end.x,settings.end.y},settings.halfWidth,settings.blend,settings.amplitude,settings.maxChange,settings.influence,settings.minimumUsefulChange,preserveRects=(settings.preserveRects??Array.Empty<Rect>()).Select(r=>new[]{r.xMin,r.yMin,r.xMax,r.yMax}).ToArray()},camera=new{position=new[]{camera.position.x,camera.position.y,camera.position.z},target=new[]{camera.target.x,camera.target.y,camera.target.z},camera.fieldOfView},metrics=plan.Metrics,renderedMaxChangeMetres=persistedPreviewMaxMetres,milliseconds,worldCommitted=false,artAccepted=false},Formatting.Indented));
        }
    }
    public static class PreviewEngine
    {
        public static PreviewFrame Render(Terrain terrain,PlanSettings settings,PreviewCamera camera,int width=960,int height=600)
        {
            if(!terrain||camera==null)throw new ArgumentException("Terrain and camera required");
            var scene=terrain.gameObject.scene;
            if(scene.isDirty)throw new InvalidOperationException("Save scene first; preview never discards existing edits");
            var watch=System.Diagnostics.Stopwatch.StartNew();var frame=new PreviewFrame();var original=terrain.terrainData;TerrainData temporary=null;
            try
            {
                frame.plan=Planner.Build(terrain,settings);frame.before=Shot(camera,width,height);
                temporary=UnityEngine.Object.Instantiate(original);temporary.hideFlags=HideFlags.HideAndDontSave;
                frame.plan.RenderInto(temporary);
                var a=original.GetHeights(frame.plan.X,frame.plan.Z,frame.plan.Width,frame.plan.Height);var b=temporary.GetHeights(frame.plan.X,frame.plan.Z,frame.plan.Width,frame.plan.Height);
                for(int z=0;z<a.GetLength(0);z++)for(int x=0;x<a.GetLength(1);x++)frame.persistedPreviewMaxMetres=Mathf.Max(frame.persistedPreviewMaxMetres,Mathf.Abs(a[z,x]-b[z,x])*original.size.y);
                // Swap only during synchronous rendering. No collider changes or persistent assets.
                terrain.terrainData=temporary;terrain.Flush();frame.after=Shot(camera,width,height);frame.delta=frame.plan.Heatmap();
                watch.Stop();frame.milliseconds=watch.Elapsed.TotalMilliseconds;return frame;
            }
            catch{frame.Dispose();throw;}
            finally{terrain.terrainData=original;terrain.Flush();if(temporary)UnityEngine.Object.DestroyImmediate(temporary);}
        }
        static Texture2D Shot(PreviewCamera pose,int width,int height)
        {
            var go=new GameObject("KernelPreviewCamera"){hideFlags=HideFlags.HideAndDontSave};var camera=go.AddComponent<Camera>();var rt=new RenderTexture(width,height,24);var previous=RenderTexture.active;Texture2D texture=null;
            try{camera.fieldOfView=pose.fieldOfView;camera.nearClipPlane=.1f;camera.farClipPlane=10000;camera.transform.position=pose.position;camera.transform.LookAt(pose.target);camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;texture=new Texture2D(width,height,TextureFormat.RGB24,false){hideFlags=HideFlags.HideAndDontSave};texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();return texture;}
            catch{if(texture)UnityEngine.Object.DestroyImmediate(texture);throw;}
            finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
