using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RuinRelay.LandscapeKernel
{
    public static class Bridge
    {
        public const string Version="0.3.0-preview.1";
        static readonly JsonSerializerSettings Strict=new JsonSerializerSettings {MissingMemberHandling=MissingMemberHandling.Error};
        public sealed class View { public string name; public float[] position; public float[] target; }
        public sealed class Request
        {
            public int schemaVersion; public string operation,operationId,scenePath,objectId,evidenceDirectory,expectedSceneHash;
            public string nameFilter;
            public string planMode="corridor",intent="Lokalen Anschluss verbessern";
            public float amplitude=6,influence=1,minimumUsefulChange=.25f;
            public float[] start,end; public float halfWidth=2,blend=3,maxChange=1;
            public float[][] preserveRects; public View[] cameras;
            public string[] retireInvisibleColliderIds;
            public string donorScenePath,donorObjectId,rollbackManifest; public int donorSlot,slot;
        }
        static string Id(UnityEngine.Object o)=>GlobalObjectId.GetGlobalObjectIdSlow(o).ToString();
        public static string Hash(string p){using(var sha=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
        static T Resolve<T>(string id,Scene scene) where T:Component
        {
            if(!GlobalObjectId.TryParse(id,out var gid))throw new ArgumentException("Invalid explicit objectId");
            var obj=GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as T;
            if(!obj||obj.gameObject.scene!=scene)throw new ArgumentException("Object not present in requested scene/type");return obj;
        }
        static void Finite(float f){if(float.IsNaN(f)||float.IsInfinity(f))throw new ArgumentException("Non-finite coordinate/parameter");}
        static Vector2 V2(float[] a){if(a==null||a.Length!=2)throw new ArgumentException("World XZ requires two numbers");foreach(var f in a)Finite(f);return new Vector2(a[0],a[1]);}
        static Vector3 V3(float[] a){if(a==null||a.Length!=3)throw new ArgumentException("World XYZ requires three numbers");foreach(var f in a)Finite(f);return new Vector3(a[0],a[1],a[2]);}
        static float[] V(Vector3 a)=>new[]{a.x,a.y,a.z};
        static IEnumerable<T> All<T>(Scene s) where T:Component=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true));
        public static object RunFile(string path)
        {
            Request r=null;string backup=null,folder=null,before=null;bool mutated=false;Scene scene=default;
            try
            {
                r=JsonConvert.DeserializeObject<Request>(File.ReadAllText(path),Strict);
                if(r==null||r.schemaVersion!=1)throw new ArgumentException("schemaVersion must be 1");
                if(string.IsNullOrEmpty(r.operationId)||!System.Text.RegularExpressions.Regex.IsMatch(r.operationId,"^[A-Za-z0-9_-]{1,64}$"))throw new ArgumentException("Invalid operationId");
                if(!new[]{"inspect","preview","corridor","material","rollback"}.Contains(r.operation))throw new ArgumentException("Unsupported operation");
                if(string.IsNullOrEmpty(r.evidenceDirectory)||!Path.IsPathRooted(r.evidenceDirectory))throw new ArgumentException("Absolute evidenceDirectory required");
                Directory.CreateDirectory(r.evidenceDirectory);
                if(File.Exists(Path.Combine(r.evidenceDirectory,"report.json")))throw new IOException("Evidence report already exists; use unique operation");
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode required");
                for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Unsaved scene; no changes made");
                if(r.operation=="rollback")return Rollback(r);
                if(string.IsNullOrEmpty(r.scenePath)||!r.scenePath.StartsWith("Assets/",StringComparison.Ordinal)||r.scenePath.Contains("..")||!r.scenePath.EndsWith(".unity")||!File.Exists(r.scenePath))throw new ArgumentException("Existing project scene required");
                before=Hash(r.scenePath);
                if(r.operation!="inspect" && r.operation!="preview" && !string.Equals(before,r.expectedSceneHash,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Scene hash changed/missing; inspect current state");
                scene=EditorSceneManager.OpenScene(r.scenePath,OpenSceneMode.Single);
                if(r.operation=="inspect")return Write(r,new {success=true,version=Version,operation=r.operation,scenePath=r.scenePath,sceneHash=before,
                    terrains=All<Terrain>(scene).Select(t=>new{id=Id(t),name=t.name,active=t.gameObject.activeInHierarchy,position=V(t.transform.position),size=V(t.terrainData.size),data=AssetDatabase.GetAssetPath(t.terrainData),colliderMatches=t.GetComponent<TerrainCollider>()&&t.GetComponent<TerrainCollider>().terrainData==t.terrainData}).ToArray(),
                    roots=scene.GetRootGameObjects().Select(g=>new{name=g.name,active=g.activeSelf,position=V(g.transform.position)}).ToArray(),
                    renderers=All<Renderer>(scene).Where(x=>!string.IsNullOrEmpty(r.nameFilter)&&x.name.IndexOf(r.nameFilter,StringComparison.OrdinalIgnoreCase)>=0).Take(100).Select(x=>new{id=Id(x),name=x.name,enabled=x.enabled,center=V(x.bounds.center),size=V(x.bounds.size),materials=x.sharedMaterials.Select(m=>m?AssetDatabase.GetAssetPath(m):null).ToArray()}).ToArray(),
                    colliders=All<Collider>(scene).Where(x=>!string.IsNullOrEmpty(r.nameFilter)&&x.name.IndexOf(r.nameFilter,StringComparison.OrdinalIgnoreCase)>=0).Take(100).Select(x=>new{id=Id(x),name=x.name,enabled=x.enabled,center=V(x.bounds.center),size=V(x.bounds.size)}).ToArray()});
                if(r.cameras==null||r.cameras.Length==0)throw new ArgumentException("Comparison cameras required");
                foreach(var c in r.cameras){V3(c.position);V3(c.target);if(string.IsNullOrEmpty(c.name)||!System.Text.RegularExpressions.Regex.IsMatch(c.name,"^[A-Za-z0-9_-]+$"))throw new ArgumentException("Invalid camera name");}
                if(r.operation=="preview")
                {
                    var previewTerrain=Resolve<Terrain>(r.objectId,scene);var settings=Settings(r);var views=new List<object>();
                    foreach(var c in r.cameras)using(var frame=PreviewEngine.Render(previewTerrain,settings,new PreviewCamera{position=V3(c.position),target=V3(c.target)}))
                    {frame.Export(Path.Combine(r.evidenceDirectory,c.name),settings,new PreviewCamera{position=V3(c.position),target=V3(c.target)});views.Add(new{name=c.name,metrics=frame.plan.Metrics,frame.persistedPreviewMaxMetres,frame.milliseconds});}
                    return Write(r,new{success=true,version=Version,operation="preview",scenePath=r.scenePath,sceneHash=before,views,worldCommitted=false,artAccepted=false});
                }
                folder="Assets/LandscapeKernelRuns/"+r.operationId;
                if(AssetDatabase.IsValidFolder(folder))throw new IOException("Operation output already exists");
                Terrain terrain=null;float[,,] paint=null;float[,] heights=null;Renderer target=null;
                if(r.operation=="corridor")
                {
                    terrain=Resolve<Terrain>(r.objectId,scene);Kernel.RequireSupported(terrain);Kernel.AssertBinding(terrain);
                    Finite(r.halfWidth);Finite(r.blend);Finite(r.maxChange);if(r.halfWidth<=0||r.blend<=0||r.maxChange<=0)throw new ArgumentException("Positive profile dimensions required");
                    Preflight(terrain,r,scene);var d=terrain.terrainData;
                    paint=d.GetAlphamaps(0,0,d.alphamapWidth,d.alphamapHeight);heights=d.GetHeights(0,0,d.heightmapResolution,d.heightmapResolution);
                }
                else {target=Resolve<Renderer>(r.objectId,scene);if(r.slot<0||r.slot>=target.sharedMaterials.Length)throw new ArgumentException("Target slot invalid");}
                backup=Path.Combine(r.evidenceDirectory,"Scene.before.unity");File.Copy(r.scenePath,backup,false);
                Capture(r,"before");Kernel.Folder(folder);mutated=true;
                int changed=0;
                if(terrain){Kernel.CloneTerrain(terrain,folder+"/Terrain.asset");changed=Kernel.ProfileCorridor(terrain,V2(r.start),V2(r.end),r.halfWidth,r.blend,r.maxChange);
                    if(r.retireInvisibleColliderIds!=null)foreach(var id in r.retireInvisibleColliderIds){var old=Resolve<MeshCollider>(id,scene);old.enabled=false;PrefabUtility.RecordPrefabInstancePropertyModifications(old);}}
                else
                {
                    Scene donorScene=scene;bool extra=false;
                    try{if(!string.IsNullOrEmpty(r.donorScenePath)&&r.donorScenePath!=r.scenePath){donorScene=EditorSceneManager.OpenScene(r.donorScenePath,OpenSceneMode.Additive);extra=true;}
                        var donor=Resolve<Renderer>(r.donorObjectId,donorScene);var material=Kernel.CopyEffectiveMaterial(donor,r.donorSlot,folder+"/Reference.mat");var mats=target.sharedMaterials;mats[r.slot]=material;target.sharedMaterials=mats;PrefabUtility.RecordPrefabInstancePropertyModifications(target);changed=1;
                    }finally{if(extra)EditorSceneManager.CloseScene(donorScene,true);SceneManager.SetActiveScene(scene);}
                }
                scene=Kernel.SaveAndReopen(scene);Physics.SyncTransforms();object checks;
                if(r.operation=="corridor")
                {
                    terrain=Resolve<Terrain>(r.objectId,scene);var d=terrain.terrainData;var a=d.GetAlphamaps(0,0,d.alphamapWidth,d.alphamapHeight);double paintError=0;
                    for(int y=0;y<a.GetLength(0);y++)for(int x=0;x<a.GetLength(1);x++)for(int k=0;k<a.GetLength(2);k++)paintError=Math.Max(paintError,Math.Abs(a[y,x,k]-paint[y,x,k]));
                    var h=d.GetHeights(0,0,d.heightmapResolution,d.heightmapResolution);int outside=0;float max=0;var p=terrain.transform.position;var size=d.size;
                    for(int z=0;z<h.GetLength(0);z++)for(int x=0;x<h.GetLength(1);x++){float delta=Mathf.Abs(h[z,x]-heights[z,x])*size.y;max=Mathf.Max(max,delta);if(delta>1e-5f&&!Inside(new Vector2(p.x+x*size.x/(h.GetLength(1)-1),p.z+z*size.z/(h.GetLength(0)-1)),r))outside++;}
                    int hits=0;float error=0;for(int i=0;i<=20;i++){var q=Vector2.Lerp(V2(r.start),V2(r.end),i/20f);float y=Kernel.Height(terrain,q);if(terrain.GetComponent<TerrainCollider>().Raycast(new Ray(new Vector3(q.x,y+20,q.y),Vector3.down),out var hit,40)){hits++;error=Mathf.Max(error,Mathf.Abs(y-hit.point.y));}}
                    if(paintError>.004||outside!=0||hits!=21)throw new Exception("Persistence/contact/preservation check failed");
                    checks=new{paintMaxError=paintError,outsideChanges=outside,maxChangeMetres=max,terrainHits=hits,sampleCount=21,maxRayDifference=error,colliderMatches=true};
                }
                else {target=Resolve<Renderer>(r.objectId,scene);if(AssetDatabase.GetAssetPath(target.sharedMaterials[r.slot])!=folder+"/Reference.mat")throw new Exception("Material did not persist");checks=new{materialPersisted=true};}
                Capture(r,"after");string after=Hash(r.scenePath);
                var receipt=new{scenePath=r.scenePath,backupPath=backup,beforeHash=before,afterHash=after,operationId=r.operationId,createdFolder=folder};
                File.WriteAllText(Path.Combine(r.evidenceDirectory,"rollback.json"),JsonConvert.SerializeObject(receipt,Formatting.Indented));
                return Write(r,new{success=true,version=Version,operation=r.operation,scenePath=r.scenePath,changed,checks,rollbackManifest=Path.Combine(r.evidenceDirectory,"rollback.json"),artAccepted=false,traversalTested=false});
            }
            catch(Exception ex)
            {
                string recovery="not_required";
                if(mutated&&backup!=null){try{File.Copy(backup,r.scenePath,true);AssetDatabase.ImportAsset(r.scenePath,ImportAssetOptions.ForceSynchronousImport);EditorSceneManager.OpenScene(r.scenePath,OpenSceneMode.Single);recovery="scene_restored_private_outputs_retained";}catch(Exception restore){recovery="RESTORE_FAILED: "+restore.Message;}}
                var failure=new{success=false,version=Version,error=ex.Message,recovery,createdFolder=folder};
                if(r!=null&&!string.IsNullOrEmpty(r.evidenceDirectory)&&Directory.Exists(r.evidenceDirectory)&&!File.Exists(Path.Combine(r.evidenceDirectory,"report.json")))return Write(r,failure);return failure;
            }
        }
        static bool Inside(Vector2 p,Request r){var a=V2(r.start);var b=V2(r.end);var axis=b-a;float u=Vector2.Dot(p-a,axis)/axis.sqrMagnitude;return u>0&&u<1&&Vector2.Distance(p,a+u*axis)<r.halfWidth+r.blend;}
        static PlanSettings Settings(Request r)=>new PlanSettings{mode=r.planMode,intent=r.intent,start=V2(r.start),end=V2(r.end),halfWidth=r.halfWidth,blend=r.blend,maxChange=r.maxChange,amplitude=r.amplitude,influence=r.influence,minimumUsefulChange=r.minimumUsefulChange,preserveRects=r.preserveRects==null?Array.Empty<Rect>():r.preserveRects.Select(a=>{if(a==null||a.Length!=4)throw new ArgumentException("Invalid preserve rectangle");foreach(var v in a)Finite(v);if(a[2]<a[0]||a[3]<a[1])throw new ArgumentException("Inverted preserve rectangle");return Rect.MinMaxRect(a[0],a[1],a[2],a[3]);}).ToArray()};
        static void Preflight(Terrain t,Request r,Scene s)
        {
            var a=V2(r.start);var b=V2(r.end);if(Vector2.Distance(a,b)<1)throw new ArgumentException("Route too short");
            var side=new Vector2(-(b-a).y,(b-a).x).normalized;float radius=r.halfWidth+r.blend;
            var bounds=new Bounds(new Vector3(a.x,0,a.y),Vector3.zero);bounds.Encapsulate(new Vector3(b.x,0,b.y));bounds.Expand(new Vector3(radius*2,t.terrainData.size.y*4,radius*2));bounds.center=new Vector3(bounds.center.x,t.transform.position.y,bounds.center.z);
            for(int i=0;i<=Mathf.CeilToInt(Vector2.Distance(a,b)*2);i++)for(int j=-4;j<=4;j++)
            {float u=i/(float)Mathf.CeilToInt(Vector2.Distance(a,b)*2);var q=Vector2.Lerp(a,b,u)+side*radius*j/4;Kernel.Height(t,q);var d=t.terrainData;int hx=Mathf.Clamp((int)((q.x-t.transform.position.x)/d.size.x*d.holesResolution),0,d.holesResolution-1);int hz=Mathf.Clamp((int)((q.y-t.transform.position.z)/d.size.z*d.holesResolution),0,d.holesResolution-1);if(d.IsHole(hx,hz))throw new InvalidOperationException("Terrain hole in edit footprint");
                if(r.preserveRects!=null)foreach(var rect in r.preserveRects){if(rect==null||rect.Length!=4)throw new ArgumentException("preserveRects: xmin,zmin,xmax,zmax");foreach(var f in rect)Finite(f);if(q.x>=rect[0]&&q.y>=rect[1]&&q.x<=rect[2]&&q.y<=rect[3])throw new InvalidOperationException("Preserved region intersects footprint");}}
            var retire=new HashSet<Collider>();
            if(r.retireInvisibleColliderIds!=null)foreach(var id in r.retireInvisibleColliderIds)
            {
                var c=Resolve<MeshCollider>(id,s);var renderer=c.GetComponent<Renderer>();
                if(!renderer||renderer.enabled||!c.sharedMesh||!c.enabled)throw new InvalidOperationException("Retirement requires an enabled mesh collider with disabled renderer");
                // Only obsolete, terrain-following meshes qualify. No bridges or unsupported spans.
                foreach(var v in c.sharedMesh.vertices){var p=c.transform.TransformPoint(v);float y=Kernel.Height(t,new Vector2(p.x,p.z));
                    if(Mathf.Abs(p.y-y)>.5f)throw new InvalidOperationException("Invisible collider is not redundant with terrain: "+c.name);
                    if(!t.GetComponent<TerrainCollider>().Raycast(new Ray(p+Vector3.up*10,Vector3.down),out var hit,20))throw new InvalidOperationException("Terrain missing beneath retired mesh");}
                retire.Add(c);
            }
            // Conservative blocker policy: no collider-bearing props/water/other carrier in footprint.
            foreach(var c in All<Collider>(s).Where(c=>c.enabled&&c.gameObject.activeInHierarchy&&c!=t.GetComponent<TerrainCollider>()&&!c.isTrigger))
                if(!retire.Contains(c)&&c.bounds.Intersects(bounds))throw new InvalidOperationException("Collider intersects edit envelope: "+c.name+"; choose a clear footprint or resolve explicitly");
        }
        static object Rollback(Request r)
        {
            var m=JObject.Parse(File.ReadAllText(r.rollbackManifest));string scene=(string)m["scenePath"],backup=(string)m["backupPath"];
            if(Hash(scene)!=(string)m["afterHash"]||Hash(backup)!=(string)m["beforeHash"])throw new InvalidOperationException("Rollback hash mismatch; newer changes must be preserved");
            File.Copy(backup,scene,true);AssetDatabase.ImportAsset(scene,ImportAssetOptions.ForceSynchronousImport);EditorSceneManager.OpenScene(scene,OpenSceneMode.Single);
            return Write(r,new{success=true,operation="rollback",scenePath=scene,restoredHash=Hash(scene),privateOutputsRetained=true});
        }
        static object Write(Request r,object value){File.WriteAllText(Path.Combine(r.evidenceDirectory,"report.json"),JsonConvert.SerializeObject(value,Formatting.Indented));return value;}
        static void Capture(Request r,string prefix)
        {
            foreach(var v in r.cameras){var go=new GameObject("KernelCapture"){hideFlags=HideFlags.HideAndDontSave};var c=go.AddComponent<Camera>();var rt=new RenderTexture(1440,900,24);var old=RenderTexture.active;Texture2D tex=null;
                try{c.fieldOfView=60;c.farClipPlane=10000;c.nearClipPlane=.1f;c.transform.position=V3(v.position);c.transform.LookAt(V3(v.target));c.targetTexture=rt;c.Render();c.Render();RenderTexture.active=rt;tex=new Texture2D(1440,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(r.evidenceDirectory,prefix+"_"+v.name+".png"),tex.EncodeToPNG());}
                finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(rt);if(tex)UnityEngine.Object.DestroyImmediate(tex);}}
        }
    }
}
