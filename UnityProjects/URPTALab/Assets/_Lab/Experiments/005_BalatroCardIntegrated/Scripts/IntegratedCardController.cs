using UnityEngine;
using TechArtLab.Balatro.Drag;
using TechArtLab.Balatro.Particles;
namespace TechArtLab.Balatro.Integrated
{
    public sealed class IntegratedCardController : MonoBehaviour
    {
        public enum Phase { Ready,Dragging,Countdown,Returning,Dissolving,Hidden }
        public CardDragController drag;
        public DissolveParticleController effect;
        [Min(0)] public float distanceHeights=.03f,delay=1;
        public bool randomize=true,paused;
        public Phase State {get;private set;}
        public float Elapsed {get;private set;}
        public int TriggerCount {get;private set;}
        public bool Initialized {get;private set;}
        public bool PointerActive {get;set;}=true;
        Vector2 pointer,lastFixedPointer;
        float accumulator;
        readonly System.Random random=new System.Random();
        public void Initialize()
        {
            if(Initialized) return;
            drag.Initialize(); effect.Initialize(); drag.dragEnded.AddListener(OnReleased);
            Initialized=true; ResetCard();
        }
        public void ResetCard()
        {
            if(!Initialized) {Initialize(); return;}
            State=Phase.Ready; drag.ResetToAnchor(); drag.returnToAnchor=true; effect.ResetVisible();
            effect.paused=false; paused=false; Elapsed=0; TriggerCount=0; accumulator=0;
            pointer=lastFixedPointer=drag.Anchor;
        }
        public bool Press(Vector2 world)
        {
            Initialize();
            if(paused || State==Phase.Dissolving || State==Phase.Hidden || drag.IsDragging) return false;
            if(!drag.TryBeginDrag(world)) return false;
            State=Phase.Dragging; Elapsed=0; pointer=lastFixedPointer=world; accumulator=0;
            effect.hover=0; effect.Apply(); return true;
        }
        public void Move(Vector2 world) {pointer=world;}
        public void Release()
        {
            if(!Initialized) return;
            drag.returnToAnchor=State!=Phase.Dissolving && State!=Phase.Hidden;
            drag.EndDrag();
        }
        void OnReleased()
        {
            if(State==Phase.Dragging || State==Phase.Countdown) {Elapsed=0; State=Phase.Returning;}
        }
        void OnApplicationFocus(bool focused) {PointerActive=focused; if(!focused) Release();}
        void OnDisable() {Release();}
        void OnDestroy() {if(drag) drag.dragEnded.RemoveListener(OnReleased);}
        public void Advance(float dt)
        {
            Initialize(); if(paused) return;
            const float h=DissolveParticleController.FixedStep;
            accumulator+=Mathf.Clamp(dt,0,.1f);
            int steps=Mathf.FloorToInt((accumulator+1e-7f)/h);
            Vector2 from=lastFixedPointer;
            for(int i=1;i<=steps;i++) {
                accumulator-=h;
                if(drag.IsDragging) drag.MovePointer(Vector2.Lerp(from,pointer,(float)i/steps));
                drag.Step(h);
                if(State==Phase.Returning && drag.IsSettled) State=Phase.Ready;
                if(State==Phase.Dragging && Vector2.Distance(drag.Position,drag.Anchor)>distanceHeights*Mathf.Abs(drag.card.lossyScale.y))
                    State=Phase.Countdown;
                if(State==Phase.Countdown) {
                    Elapsed+=h;
                    if(Elapsed+1e-6f>=Mathf.Max(0,delay)) {
                        State=Phase.Dissolving; TriggerCount++; drag.returnToAnchor=false;
                        effect.BeginExit(randomize ? random.Next(1,65536) : effect.seed);
                    }
                }
                effect.StepFixed(h);
                if(State==Phase.Dissolving && effect.IsComplete) {State=Phase.Hidden; drag.EndDrag();}
                float target=State==Phase.Ready && PointerActive && drag.HitTest(pointer) ? 1 : 0;
                effect.hover=State==Phase.Ready ? Mathf.Lerp(effect.hover,target,1-Mathf.Exp(-12*h)) : 0;
            }
            if(steps>0) lastFixedPointer=pointer;
            // Match 001: leaving the card fades hover with a clamped input, rather
            // than letting a distant pointer amplify the residual deformation.
            var local=drag.card.InverseTransformPoint(new Vector3(pointer.x,pointer.y,drag.card.position.z));
            local.x=Mathf.Clamp(local.x,-.5f,.5f); local.y=Mathf.Clamp(local.y,-.5f,.5f); local.z=0;
            effect.mousePixels=effect.viewCamera.WorldToScreenPoint(drag.card.TransformPoint(local));
            effect.Apply();
        }
    }
}
