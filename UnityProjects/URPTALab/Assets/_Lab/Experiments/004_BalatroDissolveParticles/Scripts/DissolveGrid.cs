using System;
using UnityEngine;
using TechArtLab.Balatro.Dissolve;

namespace TechArtLab.Balatro.Particles
{
    // Row zero is the TOP of the card, matching 002's logical coordinates.
    public sealed class DissolveGrid : IDisposable
    {
        public const int Width=71, Height=95, Count=Width*Height;
        public readonly float[] signed=new float[Count];
        public readonly bool[] visible=new bool[Count], crossed=new bool[Count], consumed=new bool[Count];
        public readonly float[] alpha=new float[Count];
        readonly float[] waves=new float[Count],border=new float[Count];
        public Texture2D Texture { get; private set; }
        float cachedTime=float.NaN; int cachedSeed=-1;
        public DissolveGrid(Texture2D artwork)
        {
            Texture=new Texture2D(Width,Height,TextureFormat.RFloat,false,true) {
                name="Runtime dissolve signed field",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp };
            for(int y=0;y<Height;y++) for(int x=0;x<Width;x++) {
                int i=y*Width+x;
                Vector2 uv=UV(i);
                alpha[i]=artwork.GetPixel(Mathf.Min(artwork.width-1,(int)(uv.x*artwork.width)),
                    Mathf.Min(artwork.height-1,(int)(uv.y*artwork.height))).a;
                Vector2 q=new Vector2(x,y)/(float)Height;
                border[i]=Mathf.Max(q.x-.8f,0)+Mathf.Max(.2f-q.x,0)+Mathf.Max(q.y-.8f,0)+Mathf.Max(.2f-q.y,0);
            }
        }
        public static Vector2 UV(int i) => new Vector2((i%Width+.5f)/Width,1-(i/Width+.5f)/Height);
        public void Evaluate(float d,float time,int seed,bool reset)
        {
            if(cachedTime!=time || cachedSeed!=seed) {
                var offset=CardDissolveDemo.PatternOffset(seed); float t=time*10+2003;
                Vector2 a=50*new Vector2(Mathf.Sin(-t/143.6340f),Mathf.Cos(-t/99.4324f));
                Vector2 b=50*new Vector2(Mathf.Cos(t/53.1532f),Mathf.Cos(t/61.4532f));
                Vector2 c=50*new Vector2(Mathf.Sin(-t/87.53218f),Mathf.Sin(-t/49.0000f));
                for(int y=0;y<Height;y++) for(int x=0;x<Width;x++) {
                    Vector2 p=(new Vector2(x,y)/(float)Height-Vector2.one*.5f)*2.3f*Height+(Vector2)offset;
                    var p1=p+a; var p2=p+b; var p3=p+c;
                    waves[y*Width+x]=((1+Mathf.Cos(p1.magnitude/19.483f)+Mathf.Sin(p2.magnitude/33.155f)*Mathf.Cos(p2.y/15.73f)
                        +Mathf.Cos(p3.magnitude/27.193f)*Mathf.Sin(p3.x/21.92f))*.5f-.5f)*3.14f;
                }
                cachedSeed=seed; cachedTime=time;
            }
            float threshold=d*d*(3-2*d)*1.02f-.01f;
            for(int i=0;i<Count;i++) {
                signed[i]=.5f+.5f*Mathf.Cos(threshold/82.612f+waves[i])-border[i]*(5+5*d)*d-threshold;
                bool next=d<.001f || signed[i]>0;
                crossed[i]=!reset && visible[i] && !next && !consumed[i] && alpha[i]>.01f;
                if(reset) consumed[i]=false;
                else if(crossed[i]) consumed[i]=true;
                visible[i]=next;
            }
            Texture.SetPixelData(signed,0); Texture.Apply(false,false);
        }
        public Vector2 Outward(int i)
        {
            int x=i%Width,y=i/Width;
            // Minus gradient in local XY; texture rows run down, world Y runs up.
            return new Vector2(-(signed[y*Width+Mathf.Min(x+1,Width-1)]-signed[y*Width+Mathf.Max(x-1,0)]),
                signed[Mathf.Min(y+1,Height-1)*Width+x]-signed[Mathf.Max(y-1,0)*Width+x]).normalized;
        }
        public void Dispose() { if(Texture) UnityEngine.Object.Destroy(Texture); Texture=null; }
    }
}
