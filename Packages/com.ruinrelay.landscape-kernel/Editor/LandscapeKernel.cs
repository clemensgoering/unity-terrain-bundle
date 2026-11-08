using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace RuinRelay.LandscapeKernel
{
    // Editor-only prototype. Existing assets are read-only; outputs must be new.
    public static class Kernel
    {
        public static float Threshold(float low, float high, float value)
        {
            if (float.IsNaN(low)||float.IsNaN(high)||float.IsNaN(value)||float.IsInfinity(low)||float.IsInfinity(high)||float.IsInfinity(value)||!(high > low)) throw new ArgumentException("Threshold requires finite inputs and high > low");
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(low, high, value));
        }
        public static void RequireSupported(Terrain t)
        {
            if (!t || !t.terrainData) throw new ArgumentException("Explicit terrain required");
            if (t.transform.rotation != Quaternion.identity || (t.transform.lossyScale-Vector3.one).sqrMagnitude > 1e-8f)
                throw new NotSupportedException("Prototype requires unrotated, unit-scale terrain");
        }
        public static float Height(Terrain t, Vector2 world)
        {
            RequireSupported(t);
            if(float.IsNaN(world.x)||float.IsNaN(world.y)||float.IsInfinity(world.x)||float.IsInfinity(world.y))throw new ArgumentException("Finite world coordinates required");
            Vector3 p=t.transform.position, s=t.terrainData.size;
            float u=(world.x-p.x)/s.x, v=(world.y-p.z)/s.z;
            if (u<0 || u>1 || v<0 || v>1) throw new ArgumentOutOfRangeException("world", "Outside explicit terrain; clamping forbidden");
            return p.y+t.terrainData.GetInterpolatedHeight(u,v);
        }
        public static void Folder(string path)
        {
            if (!path.StartsWith("Assets/",StringComparison.Ordinal) || path.Contains("..")) throw new ArgumentException("Output must be an Assets subfolder");
            if (AssetDatabase.IsValidFolder(path)) return;
            int i=path.LastIndexOf('/'); string parent=path.Substring(0,i);
            if(parent!="Assets") Folder(parent);
            AssetDatabase.CreateFolder(parent,path.Substring(i+1));
        }
        public static Scene CloneScene(string source, string output)
        {
            if(SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save current work before cloning");
            if(!source.EndsWith(".unity") || !output.EndsWith(".unity") || source==output || File.Exists(output)) throw new ArgumentException("New distinct scene required");
            Folder(output.Substring(0,output.LastIndexOf('/')));
            var scene=EditorSceneManager.OpenScene(source,OpenSceneMode.Single);
            if(!EditorSceneManager.SaveScene(scene,output)) throw new IOException("Scene clone failed");
            return scene;
        }
        public static TerrainData CloneTerrain(Terrain t,string output)
        {
            RequireSupported(t);
            if(File.Exists(output)) throw new IOException("Terrain output already exists");
            var old=t.terrainData;
            var alpha=old.GetAlphamaps(0,0,old.alphamapWidth,old.alphamapHeight);
            if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(old),output)) throw new IOException("Terrain clone failed");
            AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
            var copy=AssetDatabase.LoadAssetAtPath<TerrainData>(output);
            copy.SetAlphamaps(0,0,alpha); // Explicit persisted paint contract.
            t.terrainData=copy;
            var collider=t.GetComponent<TerrainCollider>();
            if(!collider) collider=t.gameObject.AddComponent<TerrainCollider>();
            collider.terrainData=copy;
            EditorUtility.SetDirty(copy);
            return copy;
        }
        public static void AssertBinding(Terrain t)
        {
            var c=t.GetComponent<TerrainCollider>();
            if(!c || c.terrainData!=t.terrainData) throw new InvalidOperationException("Terrain/collider data mismatch");
        }
        // One bounded straight corridor. Ends retain their existing elevations.
        // This is not a path planner or a general road/river generator.
        public static int ProfileCorridor(Terrain t,Vector2 start,Vector2 end,float halfWidth,float blend,float maxChange)
        {
            var plan=Planner.Build(t,new PlanSettings{mode="corridor",start=start,end=end,halfWidth=halfWidth,blend=blend,maxChange=maxChange});
            plan.Apply(t); EditorUtility.SetDirty(t.terrainData); AssertBinding(t); return plan.Metrics.changedSamples;
        }
        public static Material CopyEffectiveMaterial(Renderer source,int slot,string output)
        {
            if(!source || slot<0 || slot>=source.sharedMaterials.Length || !source.sharedMaterials[slot]) throw new ArgumentException("Effective material slot required");
            if(File.Exists(output)) throw new IOException("Material output exists");
            var m=UnityEngine.Object.Instantiate(source.sharedMaterials[slot]);
            AssetDatabase.CreateAsset(m,output); return m;
        }
        public static Scene SaveAndReopen(Scene scene)
        {
            foreach(var t in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true))) AssertBinding(t);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            string path=scene.path;
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed");
            var saved=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            foreach(var t in saved.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true))) AssertBinding(t);
            return saved;
        }
    }
}

