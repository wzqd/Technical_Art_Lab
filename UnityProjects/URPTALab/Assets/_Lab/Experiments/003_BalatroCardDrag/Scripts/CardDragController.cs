using UnityEngine;
using UnityEngine.Events;

namespace TechArtLab.Balatro.Drag
{
    // Owns the 2D card pose; input sampling and demo UI live in CardDragDemo.
    // Contract: orthographic camera facing +Z, unparented card with only Z rotation.
    public sealed class CardDragController : MonoBehaviour
    {
        public enum Phase { Idle, Dragging, Settling }
        public Camera viewCamera;
        public Transform card, shadow;
        [Tooltip("Position follow frequency while held (1/s). Higher = less lag.")]
        [Min(1)] public float positionResponse=55;
        [Tooltip("Position return frequency after release (1/s).")]
        [Min(1)] public float returnResponse=30;
        [Tooltip("Rotation frequency when leaning further or changing direction (1/s).")]
        [Min(1)] public float rotationResponse=40;
        [Tooltip("Rotation frequency when reducing tilt, including while held (1/s).")]
        [Min(1)] public float straightenResponse=65;
        [Range(.4f,1)] public float rotationDamping=1;
        [Tooltip("Horizontal card speed needed for the maximum target tilt, in card heights/s. Higher = harder to tilt.")]
        [Min(.1f)] public float speedForMaxTilt=6;
        [Range(0,85)] public float maxTilt=60;
        public bool returnToAnchor=true;
        public Vector3 shadowOffset=new Vector3(.13f,-.16f,.1f);
        public UnityEvent dragStarted=new UnityEvent();
        public UnityEvent dragEnded=new UnityEvent();
        public Phase State { get; private set; }
        public bool IsDragging => State==Phase.Dragging;
        // A future hover adapter can gate hover while grabbed or still settling.
        public bool IsSettled => State==Phase.Idle;
        public Vector2 Position => position;
        public Vector2 Velocity => velocity;
        public Vector2 TargetPosition { get; private set; }
        public Vector2 PointerPosition => pointer;
        public Vector2 Anchor => anchor;
        public float Angle => angle;
        public float TargetAngle { get; private set; }
        public float HorizontalSpeedInCardHeights => Mathf.Abs(velocity.x)/Mathf.Max(.01f,Mathf.Abs(card.lossyScale.y));
        public bool Initialized => initialized;
        Vector2 anchor,position,velocity,pointer,previousPointer,releaseTarget;
        Vector2 grabOffset;
        float anchorZ,anchorAngle,angle,angularVelocity;
        bool initialized;

        public void Initialize()
        {
            if(initialized || !card || !viewCamera) return;
            anchor=card.position; anchorZ=card.position.z; anchorAngle=card.eulerAngles.z;
            position=anchor; TargetPosition=anchor; releaseTarget=anchor;
            initialized=true; ApplyPose();
        }
        public bool ScreenToPlane(Vector2 screen,out Vector2 world)
        {
            world=default;
            Initialize();
            if(!initialized) return false;
            Ray ray=viewCamera.ScreenPointToRay(screen);
            if(!new Plane(Vector3.forward,new Vector3(0,0,anchorZ)).Raycast(ray,out float distance)) return false;
            world=ray.GetPoint(distance); return true;
        }
        public bool HitTest(Vector2 world)
        {
            Initialize();
            if(!initialized) return false;
            Vector3 local=card.InverseTransformPoint(new Vector3(world.x,world.y,anchorZ));
            return Mathf.Abs(local.x)<=.5f && Mathf.Abs(local.y)<=.5f;
        }
        public bool TryBeginDrag(Vector2 world)
        {
            if(!isActiveAndEnabled || IsDragging || !HitTest(world)) return false;
            // Fixed world-space offset: pointer displacement drives the center
            // identically for every hit location, even when re-grabbing mid-tilt.
            grabOffset=world-position;
            pointer=previousPointer=world;
            // Preserve current pose and momentum when re-grabbing during return.
            TargetPosition=position;
            State=Phase.Dragging;
            dragStarted.Invoke(); // Immediate: integration can disable 001 this frame.
            return true;
        }
        public void MovePointer(Vector2 world) { if(IsDragging) pointer=world; }
        public void EndDrag()
        {
            if(!IsDragging) return;
            releaseTarget=returnToAnchor ? anchor : position;
            // "Stay" means stay exactly at the released center; rotation still settles.
            if(!returnToAnchor) velocity=Vector2.zero;
            TargetPosition=releaseTarget; TargetAngle=0;
            State=Phase.Settling;
            dragEnded.Invoke();
        }
        public void ResetToAnchor()
        {
            Initialize();
            if(!initialized) return;
            EndDrag();
            position=releaseTarget=anchor; velocity=Vector2.zero;
            angle=angularVelocity=0; TargetPosition=anchor; TargetAngle=0;
            pointer=previousPointer=anchor; State=Phase.Idle;
            ApplyPose();
        }
        void OnApplicationFocus(bool focused) { if(!focused) EndDrag(); }
        void OnDisable() { EndDrag(); }

        public void Step(float dt)
        {
            Initialize();
            if(!initialized || dt<=0) return;
            // Bound long stalls and interpolate pointer samples within each frame.
            dt=Mathf.Min(dt,.1f);
            int steps=Mathf.Max(1,Mathf.CeilToInt(dt*120-0.0001f));
            float h=dt/steps;
            Vector2 startPointer=previousPointer;
            for(int i=1;i<=steps;i++)
            {
                if(State==Phase.Idle) break;
                float normalizedSpeed=velocity.x/Mathf.Max(.01f,Mathf.Abs(card.lossyScale.y));
                TargetAngle=IsDragging ? -Mathf.Clamp(normalizedSpeed/Mathf.Max(.1f,speedForMaxTilt),-1,1)*maxTilt : 0;
                // Decelerating and releasing can straighten quickly without changing
                // the response when leaning into a new direction. No angle snapping.
                bool straightening=Mathf.Abs(TargetAngle)<.001f ||
                    (TargetAngle*angle>0 && Mathf.Abs(TargetAngle)<Mathf.Abs(angle));
                float angleResponse=straightening ? straightenResponse : rotationResponse;
                Spring(ref angle,ref angularVelocity,TargetAngle,angleResponse,rotationDamping,h);
                angle=Mathf.Clamp(angle,-maxTilt,maxTilt);
                // Do not rotate the offset. Doing so feeds the visual lean back
                // into translation and makes edge/corner grabs behave differently.
                TargetPosition=IsDragging ? Vector2.Lerp(startPointer,pointer,(float)i/steps)
                    -grabOffset : releaseTarget;
                float response=IsDragging ? positionResponse : returnResponse;
                float x=position.x,y=position.y,vx=velocity.x,vy=velocity.y;
                Spring(ref x,ref vx,TargetPosition.x,response,1,h);
                Spring(ref y,ref vy,TargetPosition.y,response,1,h);
                position=new Vector2(x,y); velocity=new Vector2(vx,vy);
                if(State==Phase.Settling && (position-releaseTarget).sqrMagnitude<.000001f
                    && velocity.sqrMagnitude<.0001f && Mathf.Abs(angle)<.02f && Mathf.Abs(angularVelocity)<.1f)
                {
                    position=releaseTarget; velocity=Vector2.zero; angle=angularVelocity=0; State=Phase.Idle;
                }
            }
            previousPointer=pointer; ApplyPose();
        }

        // Exact damped spring solution for a target held constant over one substep.
        // response is angular frequency (1/s); damping=1 is critical, <1 can rebound.
        static void Spring(ref float x,ref float v,float target,float response,float damping,float dt)
        {
            float w=Mathf.Max(1,response),z=Mathf.Clamp(damping,.4f,1),y=x-target;
            float e=Mathf.Exp(-z*w*dt);
            if(z>=.9999f)
            {
                float j=v+w*y;
                x=target+(y+j*dt)*e; v=(v-w*j*dt)*e;
            }
            else
            {
                float wd=w*Mathf.Sqrt(1-z*z),c=Mathf.Cos(wd*dt),s=Mathf.Sin(wd*dt);
                x=target+e*(y*c+(v+z*w*y)/wd*s);
                v=e*(v*c-(z*w*v+w*w*y)/wd*s);
            }
        }
        public void ApplyPose()
        {
            if(!initialized) return;
            card.SetPositionAndRotation(new Vector3(position.x,position.y,anchorZ),Quaternion.Euler(0,0,anchorAngle+angle));
            if(shadow)
            {
                shadow.SetPositionAndRotation(card.position+shadowOffset,card.rotation);
                shadow.localScale=card.localScale;
            }
        }
    }
}
