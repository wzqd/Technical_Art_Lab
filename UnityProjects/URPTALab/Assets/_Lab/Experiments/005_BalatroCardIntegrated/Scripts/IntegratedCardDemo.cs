using UnityEngine;
using UnityEngine.InputSystem;
namespace TechArtLab.Balatro.Integrated
{
    public sealed class IntegratedCardDemo : MonoBehaviour
    {
        public IntegratedCardController controller;
        public bool drive=true,showControls=true;
        public bool Replaying {get;private set;}
        public float ReplayTime {get;private set;}
        float Scale=>Mathf.Max(.25f,Mathf.Min(Screen.width/1280f,Screen.height/720f));
        void Start() {controller.Initialize();}
        public void Replay() {controller.ResetCard(); controller.Press(controller.drag.Anchor); ReplayTime=0; Replaying=true;}
        void Update()
        {
            if(!drive) return;
            controller.PointerActive=Application.isFocused;
            var k=Keyboard.current;
            if(k!=null) {
                if(k.rKey.wasPressedThisFrame) {Replaying=false; controller.ResetCard();}
                if(k.tKey.wasPressedThisFrame) Replay();
                if(k.spaceKey.wasPressedThisFrame) controller.paused=!controller.paused;
            }
            float dt=Mathf.Min(Time.unscaledDeltaTime,.1f);
            if(Replaying) {
                if(!controller.paused) {
                    ReplayTime+=dt; controller.Move(controller.drag.Anchor+new Vector2(Mathf.Min(ReplayTime*2,1.3f),.2f));
                    controller.Advance(dt); if(ReplayTime>3.5f) Replaying=false;
                }
                return;
            }
            var mouse=Mouse.current;
            if(mouse!=null && Application.isFocused) {
                Vector2 screen=mouse.position.ReadValue();
                bool over=showControls && new Rect(24,24,330,660).Contains(new Vector2(screen.x,Screen.height-screen.y)/Scale);
                if(controller.drag.ScreenToPlane(screen,out var world)) {
                    controller.Move(world);
                    if(mouse.leftButton.wasPressedThisFrame && !over) controller.Press(world);
                }
                // Release is processed before the fixed clock, including paused frames.
                if(!mouse.leftButton.isPressed && controller.drag.IsDragging) controller.Release();
            }
            controller.Advance(dt);
        }
        void OnApplicationFocus(bool focused) {if(!focused) {Replaying=false; controller.Release();}}
        void OnGUI()
        {
            if(!showControls) return;
            var c=controller; var old=GUI.matrix; GUI.matrix=Matrix4x4.Scale(new Vector3(Scale,Scale,1));
            GUILayout.BeginArea(new Rect(24,24,330,660),GUI.skin.box);
            GUILayout.Label("005 / HOVER - DRAG - DISSOLVE");
            GUILayout.Label("Hold and leave home to arm the timer.\nMoving back does not cancel.\nRelease before dissolve to cancel.");
            if(GUILayout.Button("R / Reset card")) {Replaying=false; c.ResetCard();}
            if(GUILayout.Button("T / Replay hold-to-dissolve")) Replay();
            if(GUILayout.Button(c.paused ? "Space / Resume" : "Space / Pause")) c.paused=!c.paused;
            GUILayout.Label($"State: {c.State}\nTimer: {c.Elapsed:F2} / {c.delay:F2} s\nDissolve: {c.effect.Progress:F3}\nParticles: {c.effect.emitter.system.particleCount}");
            GUILayout.Label($"Leave distance: {c.distanceHeights:P1} card height"); c.distanceHeights=GUILayout.HorizontalSlider(c.distanceHeights,.005f,.2f);
            GUILayout.Label($"Delay: {c.delay:F2} s"); c.delay=GUILayout.HorizontalSlider(c.delay,.1f,3);
            GUILayout.Label($"Dissolve duration: {c.effect.duration:F2} s"); c.effect.duration=GUILayout.HorizontalSlider(c.effect.duration,.2f,3);
            GUILayout.Label($"Follow speed: {c.drag.positionResponse:F0}"); c.drag.positionResponse=GUILayout.HorizontalSlider(c.drag.positionResponse,6,120);
            GUILayout.Label($"Straighten speed: {c.drag.straightenResponse:F0}"); c.drag.straightenResponse=GUILayout.HorizontalSlider(c.drag.straightenResponse,5,120);
            GUILayout.Label($"Maximum tilt: {c.drag.maxTilt:F0} deg"); c.drag.maxTilt=GUILayout.HorizontalSlider(c.drag.maxTilt,0,85);
            GUILayout.Label($"Speed for max tilt: {c.drag.speedForMaxTilt:F1} H/s"); c.drag.speedForMaxTilt=GUILayout.HorizontalSlider(c.drag.speedForMaxTilt,.25f,15);
            c.randomize=GUILayout.Toggle(c.randomize,"New pattern each dissolve");
            c.effect.emitter.emitParticles=GUILayout.Toggle(c.effect.emitter.emitParticles,"Emit edge particles");
            GUILayout.Label("Once dissolving, release keeps the card there.\nHidden cards require Reset.\nDynamic shadow / default idle tilt: pending.");
            GUILayout.EndArea(); GUI.matrix=old;
        }
    }
}
