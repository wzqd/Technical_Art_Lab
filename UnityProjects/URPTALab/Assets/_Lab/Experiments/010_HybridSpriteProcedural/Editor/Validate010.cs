using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TechArtLab.Hybrid.Editor
{
    public static class Validate010
    {
        static readonly StringBuilder Report = new();
        static void Check(bool ok, string message) { if (!ok) throw new Exception("010: " + message); }
        static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../References/010_HybridSpriteProcedural/analysis/unity"));
        [MenuItem("TechArtLab/010/Validate Minimal")]
        public static void Run()
        {
            Report.Clear(); Report.AppendLine($"Unity {Application.unityVersion} / {SystemInfo.graphicsDeviceType} / XPBD {ScarfChain.Iterations} iterations");
            var demo = Object.FindAnyObjectByType<HybridDemo>(); Check(demo, "Open 010 Minimal first");
            bool drive = demo.drive, paused = demo.paused, replay = demo.replay; var saved = demo.settings;
            try
            {
                demo.drive = false; demo.paused = true; demo.replay = true;
                FrameRates(demo); CornerCases(); StaticPredictions(); MotionResponse(); SceneChecks(demo);
                Report.AppendLine("PASS: pinned anchor, finite corner cases, static stretch, damping, lag/overshoot, render FPS, simulation Hz, scene output.");
                Debug.Log("010 validation PASS / " + Evidence);
            }
            catch (Exception e) { Report.AppendLine("FAIL: " + e); throw; }
            finally
            {
                demo.settings = saved; demo.drive = drive; demo.paused = paused; demo.replay = replay; demo.ResetState();
                Directory.CreateDirectory(Evidence); File.WriteAllText(Path.Combine(Evidence, "validation.txt"), Report.ToString());
            }
        }
        static void FrameRates(HybridDemo demo)
        {
            var trajectories = new Vector2[3][]; int slot = 0;
            foreach (int hz in new[] { 30, 60, 120 })
            {
                Vector2[] baseline = null;
                foreach (int fps in new[] { 30, 60, 120, 144 })
                {
                    demo.settings = new ScarfChain.Settings { frequency = hz }; demo.ResetState(); var samples = new Vector2[24];
                    for (int frame = 0; frame < fps * 12; frame++)
                    {
                        demo.Advance(1.0 / fps, 0);
                        if ((frame + 1) % (fps / 2) == 0) samples[(frame + 1) / (fps / 2) - 1] = demo.Chain.positions[demo.Chain.Segments];
                    }
                    float delta = 0;
                    if (baseline == null) baseline = samples;
                    else for (int i = 0; i < samples.Length; i++) delta = Mathf.Max(delta, Vector2.Distance(samples[i], baseline[i]));
                    Check(delta < 1e-5f && Math.Abs(demo.SimulationTime - 12) < 1e-7, "render frame-rate dependence");
                    Report.AppendLine($"Simulation {hz} Hz / display {fps} FPS / 12 s: trajectory vs30 max difference {delta:E3} m.");
                }
                trajectories[slot++] = baseline;
            }
            for (int i = 0; i < 2; i++)
            {
                float sum = 0, max = 0;
                for (int j = 0; j < 24; j++) { float d = Vector2.Distance(trajectories[i][j], trajectories[2][j]); sum += d * d; max = Mathf.Max(max, d); }
                Report.AppendLine($"Simulation {new[] {30,60}[i]} vs120 Hz / 24 matched samples: tip RMS {Mathf.Sqrt(sum / 24):F5} m, max {max:F5} m (not identical trajectories).");
                Check(max < .45f, "simulation frequency difference exceeds demonstration scale");
            }
        }
        static void CornerCases()
        {
            int cases = 0; float maxRatio = 0, maxSpeed = 0, maxAnchor = 0;
            foreach (int hz in new[] { 30, 60, 120 }) foreach (int count in new[] { 4, 8, 12 }) for (int mask = 0; mask < 16; mask++)
            {
                var s = new ScarfChain.Settings { frequency = hz, segments = count, length = (mask & 1) == 0 ? .5f : 1.4f,
                    stiffness = (mask & 2) == 0 ? 20 : 500, damping = (mask & 4) == 0 ? .3f : 10, gravity = (mask & 8) == 0 ? 0 : 10 };
                var chain = new ScarfChain(s, HybridDemo.ReplayPose(0) + HybridDemo.Neck); float dt = 1f / hz;
                for (int tick = 1; tick <= hz * 12; tick++)
                {
                    Vector2 anchor = HybridDemo.ReplayPose(tick / (double)hz) + HybridDemo.Neck;
                    chain.Tick(dt, anchor, s); maxAnchor = Mathf.Max(maxAnchor, Vector2.Distance(chain.positions[0], anchor));
                    maxRatio = Mathf.Max(maxRatio, chain.ActualLength / s.length); maxSpeed = Mathf.Max(maxSpeed, chain.TipSpeed);
                    for (int j = 0; j < chain.positions.Length; j++) Check(float.IsFinite(chain.positions[j].x) && float.IsFinite(chain.positions[j].y), "nonfinite state");
                    Check(chain.ActualLength < s.length * 3 && chain.TipSpeed < 40, "unbounded chain");
                }
                cases++;
            }
            Check(maxAnchor == 0, "root drift"); Report.AppendLine($"Corners: {cases} x12s; root error {maxAnchor:E2} m; max length/rest {maxRatio:F3}, max tip speed {maxSpeed:F3} m/s. Stretch is elastic, not an exact-length assertion.");
        }
        static ScarfChain Settle(ScarfChain.Settings s, int seconds)
        {
            var chain = new ScarfChain(s, Vector2.zero);
            for (int i = 0; i < s.frequency * seconds; i++) chain.Tick(1f / s.frequency, Vector2.zero, s);
            return chain;
        }
        static void StaticPredictions()
        {
            foreach (int hz in new[] { 30, 60, 120 }) foreach (int count in new[] { 4, 8, 12 })
            {
                var s = new ScarfChain.Settings { frequency = hz, segments = count }; var c = Settle(s, 20);
                float predicted = s.gravity * (count + 1) / (2 * s.stiffness * count), measured = c.ActualLength - s.length;
                Check(Mathf.Abs(predicted - measured) < .001f && c.TipSpeed < .015f, "static stretch / settling");
                Report.AppendLine($"Static {hz} Hz / {count} nodes: extension {measured:F5} m vs spring-load prediction {predicted:F5} m; tip speed {c.TipSpeed:E2} m/s.");
            }
            float Extension(float stiffness) { var s = new ScarfChain.Settings { stiffness = stiffness }; return Settle(s, 20).ActualLength - s.length; }
            float soft = Extension(30), hard = Extension(300); Check(soft > hard * 8, "stiffness prediction");
            // With no gravity and an identical velocity on all free nodes, compare the decay of released motion.
            float Energy(float damping)
            {
                var s = new ScarfChain.Settings { gravity = 0, damping = damping }; var c = new ScarfChain(s, Vector2.zero);
                for (int i = 1; i <= c.Segments; i++) c.velocities[i] = new Vector2(0, 2);
                for (int i = 0; i < 120; i++) c.Tick(1f / 60, Vector2.zero, s);
                float sum = 0; for (int i = 1; i <= c.Segments; i++) sum += c.velocities[i].sqrMagnitude / (2 * c.Segments); return sum;
            }
            float low = Energy(.5f), high = Energy(6); Check(high < low * .2f, "damping decay prediction");
            Report.AppendLine($"Predictions: stiffness 30/300 -> extension {soft:F5}/{hard:F5} m; damping .5/6 -> kinetic energy after2s {low:E3}/{high:E3} J.");
        }
        static void MotionResponse()
        {
            var s = new ScarfChain.Settings(); var c = Settle(s, 10); Vector2 anchor = Vector2.zero;
            for (int i = 1; i <= 60; i++) { anchor = new Vector2(i / 60f * 1.4f, 0); c.Tick(1f / 60, anchor, s); }
            float lag = anchor.x - c.positions[c.Segments].x, speed = 0, overshoot = 0;
            for (int i = 0; i < 120; i++) { c.Tick(1f / 60, anchor, s); if (i == 5) speed = c.TipSpeed; overshoot = Mathf.Max(overshoot, c.positions[c.Segments].x - anchor.x); }
            Check(lag > .2f && speed > .1f && overshoot > .02f, "start lag / stop follow-through");
            for (int i = 0; i < 1200; i++) c.Tick(1f / 60, anchor, s);
            Check(c.TipSpeed < .015f, "eventual rest");
            Report.AppendLine($"Response: lag after1s motion {lag:F4} m; tip speed .1s after stop {speed:F4} m/s; maximum overshoot {overshoot:F4} m; final tip speed {c.TipSpeed:E2} m/s.");
        }
        static void SceneChecks(HybridDemo demo)
        {
            demo.settings = new(); demo.ResetState(); demo.Advance(4, 0); demo.RenderPose();
            Vector2 previousTip = demo.Chain.positions[demo.Chain.Segments];
            Vector3 rigidTip = demo.rigidScarf.GetPosition(demo.Chain.Segments);
            demo.Advance(1.0 / 60, 0); demo.RenderPose();
            float carried = Vector2.Distance(previousTip, demo.Chain.positions[demo.Chain.Segments]);
            float flipped = Vector3.Distance(rigidTip, demo.rigidScarf.GetPosition(demo.Chain.Segments));
            Check(carried < .15f && flipped > 1, "turn continuity / rigid flip contrast");
            Report.AppendLine($"Reversal first tick: dynamic tip displacement {carried:F5} m; rigid flipped tip displacement {flipped:F5} m.");
            demo.ResetState(); demo.Advance(4.2, 0); demo.RenderPose();
            Check(demo.Facing == -1 && demo.rigidRobot.flipX && demo.dynamicRobot.flipX, "sprite facing");
            Check(demo.rigidRobot.sprite == demo.dynamicRobot.sprite, "different primary animation input");
            for (int i = 0; i <= demo.Chain.Segments; i++)
            {
                Vector2 rendered = demo.dynamicScarf.GetPosition(i); rendered.y -= HybridDemo.LowerLane;
                Check(Vector2.Distance(rendered, demo.Chain.positions[i]) < 1e-5f, "rendered node mismatch");
            }
            foreach (var frame in demo.frames)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(frame));
                Check(importer.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed, "sprite import settings");
            }
            Report.AppendLine("Scene: identical sprite frames, flip on reversal, rendered line equals world nodes, sprite Point/no mip/uncompressed verified.");
        }
    }
}
