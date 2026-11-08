using System;
using UnityEngine;

namespace RuinRelay.LandscapeKernel
{
    [Serializable]
    public sealed class PlanSettings
    {
        public string mode="corridor", intent="Lokalen Anschluss verbessern";
        public Vector2 start,end;
        public float halfWidth=4,blend=5,maxChange=1.2f,amplitude=6,influence=1,minimumUsefulChange=.25f;
        public Rect[] preserveRects=Array.Empty<Rect>();
    }
    [Serializable]
    public sealed class PlanMetrics
    {
        public int changedSamples,limitedSamples,protectedSamples,holeSamples,windowSamples;
        public float maxAbsMetres,meanAbsMetres,changedAreaM2,unlimitedMaxMetres;
        public bool weakEffect;
        public string assessment;
    }
    // The same immutable height patch feeds both preview and Apply. Arrays never escape.
    public sealed class TerrainPlan
    {
        readonly float[,] before,after;
        readonly bool[,] protectedMask;
        readonly Vector3 origin,size;
        readonly int resolution;
        public readonly int X,Z,Width,Height;
        public readonly PlanMetrics Metrics;
        public readonly string Mode,Intent;
        internal TerrainPlan(int x,int z,float[,] old,float[,] next,bool[,] mask,Vector3 position,Vector3 dimensions,int res,PlanMetrics metrics,PlanSettings settings)
        {X=x;Z=z;before=old;after=next;protectedMask=mask;Width=old.GetLength(1);Height=old.GetLength(0);origin=position;size=dimensions;resolution=res;Metrics=metrics;Mode=settings.mode;Intent=settings.intent;}
        public void Validate(Terrain t)
        {
            Kernel.RequireSupported(t);
            if(t.transform.position!=origin||t.terrainData.size!=size||t.terrainData.heightmapResolution!=resolution)throw new InvalidOperationException("Terrain transform/resolution changed; rebuild preview");
            var current=t.terrainData.GetHeights(X,Z,Width,Height);
            for(int z=0;z<Height;z++)for(int x=0;x<Width;x++)if(current[z,x]!=before[z,x])throw new InvalidOperationException("Preview is stale: terrain patch changed");
        }
        public void Apply(Terrain target)
        {
            Validate(target);if(Metrics.holeSamples>0)throw new InvalidOperationException("Plan intersects terrain holes");
            target.terrainData.SetHeights(X,Z,(float[,])after.Clone());target.Flush();
        }
        internal void RenderInto(TerrainData temporary){temporary.SetHeights(X,Z,(float[,])after.Clone());}
        public Texture2D Heatmap()
        {
            var tex=new Texture2D(Width,Height,TextureFormat.RGB24,false){name="Height delta: blue lower, orange higher",hideFlags=HideFlags.HideAndDontSave};
            var pixels=new Color[Width*Height];float scale=Mathf.Max(.001f,Metrics.maxAbsMetres);
            for(int z=0;z<Height;z++)for(int x=0;x<Width;x++){float d=(after[z,x]-before[z,x])*size.y;pixels[z*Width+x]=protectedMask[z,x]?new Color(.2f,.8f,.4f):Color.Lerp(new Color(.12f,.14f,.16f),d>=0?new Color(1,.45f,.12f):new Color(.15f,.55f,1),Mathf.Abs(d)/scale);}
            tex.SetPixels(pixels);tex.Apply();return tex;
        }
    }
    public static class Planner
    {
        static void Finite(float f){if(float.IsNaN(f)||float.IsInfinity(f))throw new ArgumentException("Finite parameters required");}
        public static TerrainPlan Build(Terrain t,PlanSettings p)
        {
            Kernel.RequireSupported(t);if(p==null)throw new ArgumentNullException(nameof(p));
            foreach(var f in new[]{p.start.x,p.start.y,p.end.x,p.end.y,p.halfWidth,p.blend,p.maxChange,p.amplitude,p.influence,p.minimumUsefulChange})Finite(f);
            if(p.mode!="corridor"&&p.mode!="ridge")throw new ArgumentException("Mode must be corridor or ridge");
            if(p.halfWidth<=0||p.blend<=0||p.maxChange<=0||p.influence<0||p.influence>1||p.minimumUsefulChange<0||(p.end-p.start).magnitude<1)throw new ArgumentException("Invalid dimensions/influence");
            var d=t.terrainData;var origin=t.transform.position;var size=d.size;int res=d.heightmapResolution;float dx=size.x/(res-1),dz=size.z/(res-1),radius=p.halfWidth+p.blend;
            Vector2 lo=Vector2.Min(p.start,p.end)-Vector2.one*radius,hi=Vector2.Max(p.start,p.end)+Vector2.one*radius;
            Kernel.Height(t,lo);Kernel.Height(t,hi);
            int x0=Mathf.Max(0,Mathf.FloorToInt((lo.x-origin.x)/dx)),z0=Mathf.Max(0,Mathf.FloorToInt((lo.y-origin.z)/dz));
            int x1=Mathf.Min(res-1,Mathf.CeilToInt((hi.x-origin.x)/dx)),z1=Mathf.Min(res-1,Mathf.CeilToInt((hi.y-origin.z)/dz));
            var old=d.GetHeights(x0,z0,x1-x0+1,z1-z0+1);var next=(float[,])old.Clone();var mask=new bool[old.GetLength(0),old.GetLength(1)];var m=new PlanMetrics{windowSamples=old.Length};
            float y0=Kernel.Height(t,p.start),y1=Kernel.Height(t,p.end);var axis=p.end-p.start;double total=0;
            for(int z=0;z<old.GetLength(0);z++)for(int x=0;x<old.GetLength(1);x++)
            {
                var q=new Vector2(origin.x+(x+x0)*dx,origin.z+(z+z0)*dz);float u=Vector2.Dot(q-p.start,axis)/axis.sqrMagnitude;if(u<=0||u>=1)continue;
                float distance=Vector2.Distance(q,p.start+u*axis);if(distance>=radius)continue;
                float lateral=1-Kernel.Threshold(p.halfWidth,radius,distance);
                float raw=p.mode=="corridor"?Mathf.Lerp(y0,y1,u)-(origin.y+old[z,x]*size.y):p.amplitude;
                float along=p.mode=="corridor"?Kernel.Threshold(0,.15f,u)*(1-Kernel.Threshold(.85f,1,u)):Mathf.Pow(Mathf.Sin(Mathf.PI*u),2);
                float weight=lateral*along*p.influence;
                if(p.preserveRects!=null&&Array.Exists(p.preserveRects,r=>r.Contains(q))){m.protectedSamples++;mask[z,x]=true;continue;}
                int hx=Mathf.Clamp((int)((q.x-origin.x)/size.x*d.holesResolution),0,d.holesResolution-1),hz=Mathf.Clamp((int)((q.y-origin.z)/size.z*d.holesResolution),0,d.holesResolution-1);
                if(d.IsHole(hx,hz)){m.holeSamples++;continue;}
                m.unlimitedMaxMetres=Mathf.Max(m.unlimitedMaxMetres,Mathf.Abs(raw*weight));if(Mathf.Abs(raw)>p.maxChange&&weight>0)m.limitedSamples++;
                next[z,x]=Mathf.Clamp01(old[z,x]+Mathf.Clamp(raw,-p.maxChange,p.maxChange)*weight/size.y);
                float actual=Mathf.Abs(next[z,x]-old[z,x])*size.y;if(actual>1e-6f){m.changedSamples++;total+=actual;m.maxAbsMetres=Mathf.Max(m.maxAbsMetres,actual);}
            }
            m.meanAbsMetres=m.changedSamples>0?(float)(total/m.changedSamples):0;m.changedAreaM2=m.changedSamples*dx*dz;m.weakEffect=m.maxAbsMetres<p.minimumUsefulChange;
            m.assessment=m.weakEffect?"Unter der gewählten Wirkungsschwelle – Zielprofil/Verfahren prüfen.":"Geometrisch sichtbare Wirkung möglich – keine gestalterische Freigabe.";
            if(m.limitedSamples==0)m.assessment+=" Änderungsgrenze nicht aktiv; höheres Limit allein verstärkt nichts.";
            if(m.holeSamples>0)m.assessment+=" Terrainlöcher betroffen: Anwendung blockiert.";
            return new TerrainPlan(x0,z0,old,next,mask,origin,size,res,m,p);
        }
    }
}
