using System;
using UnityEngine;
using RuinRelay.LandscapeKernel;
public static class TerrainBundleSmokeTest {
 public static object Run() {
  TerrainData data=null; GameObject go=null;
  try {
   data=new TerrainData{heightmapResolution=65,size=new Vector3(128,40,128),hideFlags=HideFlags.HideAndDontSave};
   var heights=new float[65,65];for(int z=0;z<65;z++)for(int x=0;x<65;x++)heights[z,x]=.25f;data.SetHeights(0,0,heights);
   go=new GameObject("TerrainBundleSmokeTest"){hideFlags=HideFlags.HideAndDontSave};var terrain=go.AddComponent<Terrain>();terrain.terrainData=data;
   var p=new PlanSettings{mode="ridge",start=new Vector2(30,60),end=new Vector2(90,60),halfWidth=5,blend=10,amplitude=3,maxChange=6};
   var plan=Planner.Build(terrain,p);if(plan.Metrics.maxAbsMetres<2.9f)throw new Exception("Expected visible ridge");
   if(Mathf.Abs(data.GetHeight(30,30)-10)>0.01f)throw new Exception("Build mutated input");
   plan.Apply(terrain);bool stale=false;try{plan.Validate(terrain);}catch(InvalidOperationException){stale=true;}if(!stale)throw new Exception("Stale plan accepted");
   return new {success=true,checks=new[]{"ridge_amplitude","pure_build","apply_and_stale_rejection"}};
  } finally {if(go)UnityEngine.Object.DestroyImmediate(go);if(data)UnityEngine.Object.DestroyImmediate(data);}
 }
}
