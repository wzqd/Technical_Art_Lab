using UnityEngine;
using UnityEngine.Events;

namespace TechArtLab.Balatro.Particles
{
    // No Update: exactly one owner supplies time. Also owns all card property writes.
    public sealed class DissolveParticleController : MonoBehaviour
    {
        public MeshRenderer card,shadow;
        public Texture2D ace,locked;
        public EdgeParticleEmitter emitter;
        public Camera viewCamera;
        public float duration=1.2f,fieldTime=766.55188f,edgeWidth=1,tintStrength=.6f;
        public bool freezeField=true,paused;
        public int seed=1234,viewMode;
        public float hover,hoverStrength=1;
        public Vector2 mousePixels;
        public UnityEvent completed=new UnityEvent();
        public float Progress { get; private set; }
        public bool Playing { get; private set; }
        public bool IsComplete => !Playing && Progress>=1;
        public bool Entry { get; private set; }
        public DissolveGrid Grid { get; private set; }
        public const float FixedStep=1/60f;
        float accumulator;
        Texture2D artwork;
        MaterialPropertyBlock block;
        public Color Inner => Entry ? new Color(75/255f,194/255f,146/255f,1) : new Color(55/255f,66/255f,68/255f,1);
        public Color Outer => Entry ? Color.clear : new Color(253/255f,162/255f,0,1);
        public void Initialize()
        {
            if(Grid!=null) return;
            block=new MaterialPropertyBlock(); artwork=Entry ? locked : ace;
            Grid=new DissolveGrid(artwork); Grid.Evaluate(Progress,fieldTime,seed,true); Apply();
        }
        void Select(bool entry,int pattern)
        {
            Entry=entry; seed=pattern; fieldTime=entry ? 286.88257f : 766.55188f;
            Grid?.Dispose(); Grid=null; Initialize();
        }
        public void BeginExit(int pattern) { Select(false,pattern); SetProgress(0); Playing=true; }
        public void BeginEntry(int pattern) { Select(true,pattern); SetProgress(1); Playing=true; }
        public void ResetVisible() { Select(false,seed); SetProgress(0); hover=0; Apply(); }
        public void SetProgress(float value)
        {
            Initialize(); Playing=false; paused=false; accumulator=0; Progress=Mathf.Clamp01(value);
            emitter.ResetParticles(seed); Grid.Evaluate(Progress,fieldTime,seed,true); Apply();
        }
        public void Tick(float dt)
        {
            if(paused) return;
            accumulator+=Mathf.Clamp(dt,0,.1f);
            while(accumulator+1e-7f>=FixedStep) { accumulator-=FixedStep; StepFixed(FixedStep); }
        }
        public void StepFixed(float dt)
        {
            Initialize(); if(paused) return;
            bool wasPlaying=Playing;
            if(!freezeField) fieldTime+=dt;
            if(Playing) {
                Progress=Mathf.Clamp01(Progress+(Entry ? -1 : 1)*dt/Mathf.Max(.1f,duration));
                if(Progress>=.999999f && !Entry) Progress=1;
                if(Progress<=.000001f && Entry) Progress=0;
                if(Entry ? Progress<=0 : Progress>=1) Playing=false;
            }
            Grid.Evaluate(Progress,fieldTime,seed,false);
            emitter.Step(dt,Grid,card.transform,wasPlaying && !Entry,Outer,Inner);
            Apply(); if(wasPlaying && !Playing) completed.Invoke();
        }
        public void Apply()
        {
            if(Grid==null || !card) return;
            block.Clear(); block.SetTexture("_BaseMap",artwork); block.SetTexture("_SignedField",Grid.Texture);
            block.SetFloat("_Dissolve",Progress); block.SetFloat("_EdgeWidth",edgeWidth); block.SetFloat("_TintStrength",tintStrength);
            block.SetVector("_BurnColor1",Inner.linear); block.SetVector("_BurnColor2",Outer.linear);
            block.SetFloat("_ViewMode",viewMode); block.SetFloat("_Hover",hover); block.SetFloat("_Strength",hoverStrength);
            if(viewCamera) {
                block.SetVector("_MousePixels",new Vector4(mousePixels.x-viewCamera.pixelRect.x,mousePixels.y-viewCamera.pixelRect.y,0,0));
                block.SetVector("_ViewportSize",new Vector4(viewCamera.pixelWidth,viewCamera.pixelHeight,0,0));
                block.SetFloat("_ScreenScale",Mathf.Max(1,Mathf.Abs(card.transform.lossyScale.y)/(2*viewCamera.orthographicSize)*viewCamera.pixelHeight*.8f));
            }
            block.SetFloat("_Shadow",0); card.SetPropertyBlock(block);
            if(shadow) { block.SetFloat("_Shadow",1); block.SetFloat("_Hover",0); shadow.SetPropertyBlock(block); shadow.enabled=viewMode==0; }
        }
        void OnDestroy() { Grid?.Dispose(); }
    }
}
