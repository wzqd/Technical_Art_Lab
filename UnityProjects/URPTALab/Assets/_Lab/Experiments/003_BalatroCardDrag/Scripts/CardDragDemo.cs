using UnityEngine;
using UnityEngine.InputSystem;

namespace TechArtLab.Balatro.Drag
{
    public sealed class CardDragDemo : MonoBehaviour
    {
        public CardDragController controller;
        public bool readMouseInput=true,showControls=true,showMarkers=true;
        public bool Replaying { get; private set; }
        public float ReplayTime { get; private set; }
        bool replayPaused,replayReleased;
        float UIScale => Mathf.Max(.25f,Mathf.Min(Screen.width/1280f,Screen.height/720f));
        void Start() { controller.Initialize(); }
        void Update()
        {
            if(!controller) return;
            var keys=Keyboard.current;
            if(readMouseInput && keys!=null)
            {
                if(keys.rKey.wasPressedThisFrame) ResetDemo();
                if(keys.tKey.wasPressedThisFrame) StartReplay();
                if(keys.spaceKey.wasPressedThisFrame && Replaying) replayPaused=!replayPaused;
            }
            float dt=Time.unscaledDeltaTime;
            if(Replaying)
            {
                if(!replayPaused) AdvanceReplay(Mathf.Min(dt,.1f));
                return;
            }
            var mouse=Mouse.current;
            if(readMouseInput && mouse!=null && Application.isFocused)
            {
                Vector2 screen=mouse.position.ReadValue();
                if(controller.ScreenToPlane(screen,out Vector2 world))
                {
                    Vector2 gui=new Vector2(screen.x,Screen.height-screen.y)/UIScale;
                    bool overPanel=showControls && new Rect(24,24,320,650).Contains(gui);
                    if(mouse.leftButton.wasPressedThisFrame && !overPanel) controller.TryBeginDrag(world);
                    if(controller.IsDragging) controller.MovePointer(world);
                }
                // Retain capture when leaving the card/panel. Missing a release event
                // must not leave the drag stuck on the next focused frame.
                if(controller.IsDragging && !mouse.leftButton.isPressed) controller.EndDrag();
            }
            controller.Step(dt);
        }
        void OnApplicationFocus(bool focused)
        {
            if(!focused)
            {
                controller?.EndDrag();
                Replaying=false; replayPaused=false;
            }
        }
        void OnDisable()
        {
            controller?.EndDrag(); Replaying=false; replayPaused=false;
        }
        public void ResetDemo()
        {
            Replaying=false; replayPaused=false; ReplayTime=0;
            controller.ResetToAnchor();
        }
        public void ApplyTuningPreset(bool fast)
        {
            controller.positionResponse=fast ? 55 : 22;
            controller.returnResponse=fast ? 30 : 12;
            controller.rotationResponse=fast ? 40 : 18;
            controller.straightenResponse=fast ? 65 : 18;
            controller.maxTilt=fast ? 60 : 35;
            controller.speedForMaxTilt=fast ? 6 : 35f/18;
            controller.rotationDamping=1;
        }
        public void StartReplay()
        {
            ResetDemo(); replayReleased=false;
            controller.TryBeginDrag(controller.Anchor);
            Replaying=true;
        }
        // Piecewise-linear deterministic input: slow right, fast reversal, stop,
        // release. Timings land on 30/60/120 Hz samples for comparative validation.
        public static Vector2 Trajectory(float t)
        {
            if(t<1) return new Vector2(1.5f*t,0);
            if(t<1.5f) return Vector2.Lerp(new Vector2(1.5f,0),new Vector2(-1.5f,.45f),(t-1)*2);
            if(t<2) return Vector2.Lerp(new Vector2(-1.5f,.45f),new Vector2(1.5f,-.2f),(t-1.5f)*2);
            return new Vector2(1.5f,-.2f);
        }
        public void AdvanceReplay(float dt)
        {
            // Split at release so the state transition does not depend on frame rate.
            float remaining=dt;
            if(!replayReleased && ReplayTime<3 && ReplayTime+dt>=3)
            {
                float before=3-ReplayTime;
                controller.MovePointer(controller.Anchor+Trajectory(3)); controller.Step(before);
                remaining-=before; ReplayTime=3; controller.EndDrag(); replayReleased=true;
            }
            ReplayTime+=remaining;
            if(!replayReleased) controller.MovePointer(controller.Anchor+Trajectory(ReplayTime));
            controller.Step(remaining);
            if(ReplayTime>=5) { Replaying=false; replayPaused=false; }
        }
        void OnGUI()
        {
            if(!controller || !controller.Initialized) return;
            var old=GUI.matrix; GUI.matrix=Matrix4x4.Scale(new Vector3(UIScale,UIScale,1));
            if(showMarkers)
            {
                Marker(controller.Anchor,new Color(.55f,.63f,.68f),"HOME",-25);
                Marker(controller.TargetPosition,new Color(.95f,.67f,.22f),"TARGET",0);
                Marker(controller.Position,new Color(.25f,.9f,.72f),"CENTER",22);
            }
            if(showControls)
            {
                GUILayout.BeginArea(new Rect(24,24,320,650),GUI.skin.box);
                GUILayout.Label("003  /  CARD DRAG");
                GUILayout.Label("Grab anywhere. Move, stop, release.");
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Reset [R]",GUILayout.Height(28))) ResetDemo();
                if(GUILayout.Button("Replay [T]",GUILayout.Height(28))) StartReplay();
                GUILayout.EndHorizontal();
                if(Replaying && GUILayout.Button(replayPaused ? "Continue [Space]" : "Pause replay [Space]")) replayPaused=!replayPaused;
                GUILayout.Label($"State: {controller.State}   Replay: {ReplayTime:F2}s");
                GUILayout.Label($"Horizontal speed: {controller.HorizontalSpeedInCardHeights:F2} H/s   Tilt: {controller.Angle:F1} deg");
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Original tuning")) ApplyTuningPreset(false);
                if(GUILayout.Button("Fast / high-speed tilt")) ApplyTuningPreset(true);
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
                controller.returnToAnchor=GUILayout.Toggle(controller.returnToAnchor,"Return home on release");
                showMarkers=GUILayout.Toggle(showMarkers,"Show target and center markers");
                GUILayout.Label($"Follow speed (held): {controller.positionResponse:F1}");
                controller.positionResponse=GUILayout.HorizontalSlider(controller.positionResponse,6,120);
                GUILayout.Label($"Return home speed (released): {controller.returnResponse:F1}");
                controller.returnResponse=GUILayout.HorizontalSlider(controller.returnResponse,4,100);
                GUILayout.Label($"Maximum tilt: {controller.maxTilt:F1} deg");
                controller.maxTilt=GUILayout.HorizontalSlider(controller.maxTilt,0,85);
                GUILayout.Label($"Speed for max tilt: {controller.speedForMaxTilt:F2} H/s");
                controller.speedForMaxTilt=GUILayout.HorizontalSlider(controller.speedForMaxTilt,.25f,15);
                GUILayout.Label($"Tilt response: {controller.rotationResponse:F1}");
                controller.rotationResponse=GUILayout.HorizontalSlider(controller.rotationResponse,5,120);
                GUILayout.Label($"Straighten speed: {controller.straightenResponse:F1}");
                controller.straightenResponse=GUILayout.HorizontalSlider(controller.straightenResponse,5,120);
                GUILayout.Label($"Rotation damping: {controller.rotationDamping:F2}");
                controller.rotationDamping=GUILayout.HorizontalSlider(controller.rotationDamping,.4f,1);
                GUILayout.Label("H/s = card heights per second.\nHigher speed for max tilt = harder to tilt.\nDamping 1 = no oscillation; lower = rebound.");
                GUILayout.Space(6);
                GUILayout.Label("Amber: target   Green: actual center\nPlay-mode tuning resets when Play ends.");
                GUILayout.EndArea();
            }
            GUI.matrix=old;
        }
        void Marker(Vector2 world,Color color,string text,float labelOffset)
        {
            Vector3 p=controller.viewCamera.WorldToScreenPoint(new Vector3(world.x,world.y,controller.card.position.z));
            Vector2 point=new Vector2(p.x,Screen.height-p.y)/UIScale;
            Color old=GUI.color; GUI.color=color;
            GUI.DrawTexture(new Rect(point.x-5,point.y-1,10,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(point.x-1,point.y-5,2,10),Texture2D.whiteTexture);
            GUI.Label(new Rect(point.x+8,point.y+labelOffset,80,20),text);
            GUI.color=old;
        }
    }
}
