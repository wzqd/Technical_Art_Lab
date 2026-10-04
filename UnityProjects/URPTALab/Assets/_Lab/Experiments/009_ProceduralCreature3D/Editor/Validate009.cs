using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TechArtLab.Creature.Editor
{
    public static class Validate009
    {
        static readonly StringBuilder Report = new();
        static void Require(bool condition, string message) { if (!condition) throw new Exception("009: " + message); }
        static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../References/009_ProceduralCreature3D/analysis/unity"));
        [MenuItem("TechArtLab/009/Validate Minimal")]
        public static void Run()
        {
            Report.Clear(); Report.AppendLine($"Unity {Application.unityVersion} / {SystemInfo.graphicsDeviceType}");
            try
            {
                CheckIK();
                Simulate(new QuadrupedGait.Settings(), 7200, true, "Default / 60 seconds");
                for (int mask = 0; mask < 64; mask++)
                {
                    var s = new QuadrupedGait.Settings { speed = (mask & 1) == 0 ? .2f : 1.4f,
                        bodyHeight = (mask & 2) == 0 ? .75f : 1.35f, lift = (mask & 4) == 0 ? .04f : .45f,
                        swingDuration = (mask & 8) == 0 ? .18f : .6f, triggerDistance = (mask & 16) == 0 ? .12f : .5f,
                        stepLead = (mask & 32) == 0 ? .08f : .5f };
                    Simulate(s, 2400, false, "Corner " + mask);
                }
                CheckPredictions(); CheckFrameRates();
                Report.AppendLine("PASS: IK, world anchors, swing endpoints, diagonal support, parameter corners, stop/reverse, 30/60/120 FPS.");
                Debug.Log("009 validation PASS. " + DirectoryPath);
            }
            catch (Exception e) { Report.AppendLine("FAIL " + e); throw; }
            finally { Directory.CreateDirectory(DirectoryPath); File.WriteAllText(Path.Combine(DirectoryPath, "validation.txt"), Report.ToString()); }
        }
        static void CheckIK()
        {
            float error = 0; int count = 0;
            foreach (float upper in new[] { .85f, 1.1f }) foreach (float lower in new[] { .6f, .85f })
            foreach (Vector3 target in new[] { Vector3.zero, Vector3.down * .001f, new Vector3(.7f, -.9f, .4f), Vector3.down * 3, Vector3.right })
            foreach (Vector3 pole in new[] { Vector3.right, Vector3.down, Vector3.zero })
            {
                QuadrupedGait.SolveIK(Vector3.zero, target, pole, upper, lower, out var knee, out var foot, out _);
                float e = Mathf.Max(Mathf.Abs(knee.magnitude - upper), Mathf.Abs(Vector3.Distance(knee, foot) - lower));
                Require(!float.IsNaN(e) && e < .0001f, "IK segment length / degenerate pole"); error = Mathf.Max(error, e); count++;
            }
            Report.AppendLine($"IK: {count} cases, max length error {error:E4} m; includes coincident, unreachable, parallel/zero pole, unequal segments.");
        }
        static void Simulate(QuadrupedGait.Settings settings, int ticks, bool changeHeight, string name)
        {
            var model = new QuadrupedGait(settings); float maxSlip = 0, maxError = 0, maxLength = 0, maxLift = 0;
            var previous = new Vector3[4]; var goals = new Vector3[4]; var wasSwinging = new bool[4];
            int limited = 0, landed = 0;
            for (int tick = 0; tick < ticks; tick++)
            {
                for (int i = 0; i < 4; i++) { previous[i] = model.feet[i].position; wasSwinging[i] = model.feet[i].swinging; goals[i] = model.feet[i].goal; }
                if (changeHeight && tick == 3600) settings.bodyHeight = 1.35f;
                if (changeHeight && tick == 4800) settings.bodyHeight = .75f;
                model.Tick((float)CreatureDemo.TickSeconds, QuadrupedGait.ReplayInput(model.Time), settings);
                if (model.MovementLimited) limited++;
                int planted = 0;
                for (int i = 0; i < 4; i++)
                {
                    var f = model.feet[i];
                    if (!f.swinging) { planted++; Require(Mathf.Abs(f.position.y) < 1e-6f, name + " ground contact"); }
                    if (!wasSwinging[i] && !f.swinging) maxSlip = Mathf.Max(maxSlip, Vector3.Distance(previous[i], f.position));
                    if (wasSwinging[i]) Require(f.goal == goals[i], name + " swing target changed");
                    if (wasSwinging[i] && !f.swinging) { landed++; Require(f.position == f.goal, name + " landing endpoint"); }
                    if (f.swinging) Require(QuadrupedGait.Pair(i) == model.ActivePair, name + " diagonal pairing");
                    Require(f.position.y >= -1e-6f && f.position.y <= model.SwingLift + .00001f, name + " lift range");
                    maxLift = Mathf.Max(maxLift, f.position.y);
                    QuadrupedGait.SolveIK(model.Hip(i), f.position, new Vector3(i % 2 == 0 ? -1 : 1, .15f, .2f),
                        QuadrupedGait.UpperLength, QuadrupedGait.LowerLength, out var knee, out var foot, out bool clamped);
                    maxError = Mathf.Max(maxError, Vector3.Distance(foot, f.position));
                    maxLength = Mathf.Max(maxLength, Mathf.Abs(Vector3.Distance(knee, foot) - QuadrupedGait.LowerLength),
                        Mathf.Abs(Vector3.Distance(model.Hip(i), knee) - QuadrupedGait.UpperLength));
                    Require(!clamped, name + " unreachable leg " + i + " tick " + tick);
                }
                Require(planted == 2 || planted == 4, name + " support count");
            }
            Require(maxSlip < 1e-6f && maxError < .0001f && maxLength < .0001f, name + " anchor / segment error");
            Require(landed > 0, name + " no steps");
            Report.AppendLine($"{name}: {model.Steps} pairs / {landed} foot contacts, slip {maxSlip:E2}, IK {maxError:E2}, length {maxLength:E2} m; peak lift {maxLift:F4}; reach-limited ticks {limited}.");
        }
        static void CheckPredictions()
        {
            float Peak(float lift)
            {
                var settings = new QuadrupedGait.Settings { lift = lift }; var model = new QuadrupedGait(settings); float peak = 0;
                for (int i = 0; i < 600; i++) { model.Tick((float)CreatureDemo.TickSeconds, Vector3.forward, settings); foreach (var f in model.feet) peak = Mathf.Max(peak, f.position.y); }
                return peak;
            }
            float low = Peak(.10f), high = Peak(.30f); Require(Mathf.Abs(high / low - 3) < .002f, "lift scaling");
            int Count(float threshold)
            {
                var s = new QuadrupedGait.Settings { triggerDistance = threshold }; var m = new QuadrupedGait(s);
                for (int i = 0; i < 2400; i++) m.Tick((float)CreatureDemo.TickSeconds, QuadrupedGait.ReplayInput(m.Time), s);
                return m.Steps;
            }
            int small = Count(.15f), large = Count(.45f); Require(small > large, "trigger distance prediction");
            var settings = new QuadrupedGait.Settings(); var model = new QuadrupedGait(settings);
            for (int i = 0; i < 300; i++) model.Tick((float)CreatureDemo.TickSeconds, Vector3.forward, settings);
            for (int i = 0; i < 240; i++) model.Tick((float)CreatureDemo.TickSeconds, Vector3.zero, settings);
            int before = model.Steps; var position = model.Body;
            for (int i = 0; i < 240; i++) model.Tick((float)CreatureDemo.TickSeconds, Vector3.zero, settings);
            Require(before == model.Steps && model.ActivePair == -1 && model.Body == position, "stationary settling");
            Report.AppendLine($"Predictions: lift 0.10/0.30 -> peaks {low:F5}/{high:F5}; trigger 0.15/0.45 -> {small}/{large} pairs in 20s; stop settles with no continued stepping.");
        }
        static void CheckFrameRates()
        {
            var demo = Object.FindAnyObjectByType<CreatureDemo>(); Require(demo, "open 009 Minimal first");
            bool drive = demo.drive, replay = demo.replay; var saved = demo.settings;
            var reference = new Vector3[5]; int referenceSteps = 0;
            try
            {
                demo.drive = false; demo.replay = true; demo.settings = new();
                foreach (int fps in new[] { 30, 60, 120 })
                {
                    demo.ResetState(); for (int i = 0; i < 20 * fps; i++) demo.Advance(1.0 / fps, Vector3.zero);
                    var m = demo.Model; float error = 0;
                    if (fps == 30) { reference[0] = m.Body; for (int j = 0; j < 4; j++) reference[j + 1] = m.feet[j].position; referenceSteps = m.Steps; }
                    else { error = Vector3.Distance(reference[0], m.Body); for (int j = 0; j < 4; j++) error = Mathf.Max(error, Vector3.Distance(reference[j + 1], m.feet[j].position)); }
                    Require(error < 1e-6f && referenceSteps == m.Steps, "frame-rate dependence");
                    demo.RenderPose(); Require(demo.MaxIKError < .0001f, "scene pose IK error");
                    for (int j = 0; j < 4; j++)
                    {
                        var view = demo.legs[j];
                        Require(Vector3.Distance(view.foot.position - Vector3.up * .065f, m.feet[j].position) < .0001f, "visible foot sole differs from anchor");
                        Require(Mathf.Abs(view.upper.localScale.y * 2 - QuadrupedGait.UpperLength) < .0001f
                            && Mathf.Abs(view.lower.localScale.y * 2 - QuadrupedGait.LowerLength) < .0001f, "visible rod length");
                    }
                    Report.AppendLine($"Actual CreatureDemo.Advance / {fps} FPS / 20s: {m.Steps} pairs; vs30 body/foot delta {error:E2} m.");
                }
            }
            finally { demo.settings = saved; demo.drive = drive; demo.replay = replay; demo.ResetState(); }
        }
    }
}
