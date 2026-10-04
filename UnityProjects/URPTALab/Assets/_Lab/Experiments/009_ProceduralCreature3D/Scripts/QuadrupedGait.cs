using System;
using UnityEngine;

namespace TechArtLab.Creature
{
    /// <summary>World-space foot anchors and a diagonal-pair gait on y=0. No physics or animation clips.</summary>
    public sealed class QuadrupedGait
    {
        [Serializable] public sealed class Settings
        {
            [Range(0, 1.4f)] public float speed = .65f;
            [Range(.08f, .5f)] public float stepLead = .24f;
            [Range(.12f, .5f)] public float triggerDistance = .28f;
            [Range(.04f, .45f)] public float lift = .24f;
            [Range(.75f, 1.35f)] public float bodyHeight = 1.05f;
            [Range(.18f, .6f)] public float swingDuration = .32f;
            public void Clamp()
            {
                speed = Mathf.Clamp(speed, 0, 1.4f); stepLead = Mathf.Clamp(stepLead, .08f, .5f);
                triggerDistance = Mathf.Clamp(triggerDistance, .12f, .5f); lift = Mathf.Clamp(lift, .04f, .45f);
                bodyHeight = Mathf.Clamp(bodyHeight, .75f, 1.35f); swingDuration = Mathf.Clamp(swingDuration, .18f, .6f);
            }
        }

        public sealed class Foot
        {
            public Vector3 position, start, goal;
            public bool swinging;
            public float phase;
        }

        public const float UpperLength = .85f, LowerLength = .85f;
        // Keep a margin from full extension. It also makes the knee direction well defined.
        const float Reach = UpperLength + LowerLength - .05f;
        public static readonly Vector3[] HipOffsets = {
            new(-.46f, 0, .53f), new(.46f, 0, .53f), new(-.46f, 0, -.53f), new(.46f, 0, -.53f) };
        public static readonly Vector3[] RestOffsets = {
            new(-.94f, 0, .70f), new(.94f, 0, .70f), new(-.94f, 0, -.70f), new(.94f, 0, -.70f) };
        public static int Pair(int leg) => leg == 0 || leg == 3 ? 0 : 1;
        public readonly Foot[] feet = { new(), new(), new(), new() };
        public Vector3 Body { get; private set; }
        public Vector3 Velocity { get; private set; }
        public double Time { get; private set; }
        public int Steps { get; private set; }
        public int ActivePair { get; private set; } = -1;
        public bool MovementLimited { get; private set; }
        public float SwingLift { get; private set; }
        public float SwingSeconds { get; private set; }
        int nextPair;
        float swingTime, settleTime;

        public QuadrupedGait(Settings settings) { Reset(settings); }
        public void Reset(Settings settings)
        {
            settings.Clamp(); Body = Vector3.up * settings.bodyHeight; Velocity = Vector3.zero;
            Time = 0; Steps = 0; ActivePair = -1; nextPair = 0; swingTime = settleTime = 0; MovementLimited = false;
            for (int i = 0; i < 4; i++)
            {
                var foot = feet[i]; foot.position = foot.start = foot.goal = RestOffsets[i]; foot.phase = 0; foot.swinging = false;
            }
        }

        public Vector3 Hip(int leg) => Body + HipOffsets[leg];
        public Vector3 Rest(int leg) => new Vector3(Body.x, 0, Body.z) + RestOffsets[leg];
        public static Vector3 ReplayInput(double time)
        {
            double t = time % 20;
            if (t < 4) return Vector3.forward;
            if (t < 6) return Vector3.zero;
            if (t < 10) return Vector3.back;
            if (t < 12) return Vector3.zero;
            if (t < 16) return Vector3.right;
            return Vector3.left;
        }

        public void Tick(float dt, Vector3 input, Settings settings)
        {
            settings.Clamp(); input.y = 0; input = Vector3.ClampMagnitude(input, 1);
            var requestedVelocity = input * settings.speed;
            // At extreme settings slow the body instead of pulling a planted foot off its anchor.
            // An active swing's two ground endpoints also constrain reach. The elevated curve is
            // closer to the hip for the allowed lift/body-height ranges.
            Vector3 candidate = Body + requestedVelocity * dt;
            candidate.x = Mathf.Clamp(candidate.x, -2.6f, 2.6f); candidate.z = Mathf.Clamp(candidate.z, -2.6f, 2.6f);
            candidate.y = Mathf.MoveTowards(Body.y, settings.bodyHeight, .6f * dt);
            MovementLimited = !CanReach(candidate);
            if (MovementLimited)
            {
                float lo = 0, hi = 1;
                for (int j = 0; j < 14; j++) { float mid = (lo + hi) * .5f; if (CanReach(Vector3.Lerp(Body, candidate, mid))) lo = mid; else hi = mid; }
                candidate = Vector3.Lerp(Body, candidate, lo);
            }
            Velocity = (candidate - Body) / dt; Body = candidate;

            if (ActivePair >= 0)
            {
                swingTime += dt;
                float t = Mathf.Clamp01(swingTime / SwingSeconds);
                for (int i = 0; i < 4; i++) if (feet[i].swinging)
                {
                    var f = feet[i]; f.phase = t;
                    float smooth = t * t * (3 - 2 * t);
                    // Zero vertical velocity at both contacts; height at phase .5 is exactly lift.
                    float arch = 16 * t * t * (1 - t) * (1 - t);
                    f.position = Vector3.Lerp(f.start, f.goal, smooth) + Vector3.up * (SwingLift * arch);
                    if (t >= 1) { f.position = f.goal; f.swinging = false; }
                }
                if (t >= 1) { nextPair = 1 - ActivePair; ActivePair = -1; settleTime = .035f; }
            }
            else
            {
                settleTime -= dt;
                if (settleTime <= 0)
                {
                    int pair = NeedsStep(nextPair, settings) ? nextPair : NeedsStep(1 - nextPair, settings) ? 1 - nextPair : -1;
                    if (pair >= 0) BeginStep(pair, requestedVelocity, settings);
                }
            }
            Time += dt;
        }

        bool CanReach(Vector3 body)
        {
            for (int i = 0; i < 4; i++)
            {
                var hip = body + HipOffsets[i]; var f = feet[i];
                if ((hip - (f.swinging ? f.start : f.position)).sqrMagnitude > Reach * Reach) return false;
                if (f.swinging && (hip - f.goal).sqrMagnitude > Reach * Reach) return false;
            }
            return true;
        }

        bool NeedsStep(int pair, Settings settings)
        {
            for (int i = 0; i < 4; i++) if (Pair(i) == pair)
            {
                if (Vector3.Distance(Rest(i), feet[i].position) > settings.triggerDistance) return true;
                // A height change can consume extension headroom before planar error hits its threshold.
                if (Vector3.Distance(Hip(i), feet[i].position) > Reach - .07f && Vector3.Distance(Rest(i), feet[i].position) > .05f) return true;
            }
            return false;
        }

        void BeginStep(int pair, Vector3 velocity, Settings settings)
        {
            ActivePair = pair; Steps++; swingTime = 0;
            // Latch per-step settings: moving a slider must not teleport a foot in midair.
            SwingLift = settings.lift; SwingSeconds = settings.swingDuration;
            for (int i = 0; i < 4; i++) if (Pair(i) == pair)
            {
                var f = feet[i]; f.start = f.position;
                var goal = Rest(i) + velocity.normalized * (settings.stepLead * Mathf.Min(1, velocity.magnitude / .25f));
                var hipGround = Hip(i); hipGround.y = 0;
                float radius = Mathf.Sqrt(Reach * Reach - Body.y * Body.y) - .02f;
                f.goal = hipGround + Vector3.ClampMagnitude(goal - hipGround, radius);
                f.swinging = true; f.phase = 0;
            }
        }

        /// <summary>Law-of-cosines solution in the hip/foot/pole plane; all inputs and outputs are world-space.</summary>
        public static void SolveIK(Vector3 hip, Vector3 target, Vector3 pole, float upper, float lower,
            out Vector3 knee, out Vector3 foot, out bool clamped)
        {
            var delta = target - hip; float rawDistance = delta.magnitude;
            var axis = rawDistance > 1e-6f ? delta / rawDistance : Vector3.down;
            float d = Mathf.Clamp(rawDistance, Mathf.Abs(upper - lower) + .0001f, upper + lower - .0001f);
            clamped = Mathf.Abs(d - rawDistance) > 1e-5f; foot = hip + axis * d;
            var bend = Vector3.ProjectOnPlane(pole, axis);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right, axis);
            bend.Normalize();
            float along = (upper * upper - lower * lower + d * d) / (2 * d);
            float height = Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            knee = hip + axis * along + bend * height;
        }
    }
}
