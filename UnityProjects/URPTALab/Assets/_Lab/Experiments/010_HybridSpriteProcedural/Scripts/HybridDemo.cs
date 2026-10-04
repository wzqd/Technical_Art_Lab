using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TechArtLab.Hybrid
{
    public sealed class HybridDemo : MonoBehaviour
    {
        public Camera sceneCamera;
        public SpriteRenderer rigidRobot, dynamicRobot;
        public Sprite[] frames;
        public LineRenderer rigidScarf, dynamicScarf;
        public Transform[] markers;
        public ScarfChain.Settings settings = new();
        public bool replay = true, paused, drive = true, showControls = true, showNodes = true;
        public ScarfChain Chain { get; private set; }
        public Vector2 Body { get; private set; }
        public double SimulationTime => ticks / (double)activeFrequency;
        public int Facing { get; private set; } = 1;
        public float BodySpeed { get; private set; }
        public static readonly Vector2 Neck = new(0, 1.25f);
        public const float UpperLane = .55f, LowerLane = -2.45f;
        double accumulator;
        long ticks;
        int activeFrequency = 60;
        float jumpVelocity;
        bool jumpQueued, running, previousBackground;
        Vector3[] linePositions;
        [NonSerialized] GUIStyle heading, label, small, button;
        float UiScale => Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            previousBackground = Application.runInBackground; Application.runInBackground = true; running = true;
            heading = label = small = button = null; ResetState();
        }
        void OnDisable()
        {
            if (running) Application.runInBackground = previousBackground;
            running = false; heading = label = small = button = null;
        }
        public void ResetState()
        {
            settings.Clamp(); activeFrequency = settings.frequency; accumulator = 0; ticks = 0;
            Body = new Vector2(-1.4f, 0); BodySpeed = 0; Facing = 1; jumpVelocity = 0; jumpQueued = false;
            Chain = new ScarfChain(settings, Body + Neck); linePositions = new Vector3[settings.segments + 1]; RenderPose();
        }
        public void ResetDemo()
        {
            settings = new(); replay = true; paused = false; showNodes = true; ResetState();
        }
        public static Vector2 ReplayPose(double time)
        {
            double t = time % 12; float x;
            if (t < 1) x = -1.4f;
            else if (t < 3) x = -1.4f + 1.4f * (float)(t - 1);
            else if (t < 4) x = 1.4f;
            else if (t < 6) x = 1.4f - 1.4f * (float)(t - 4);
            else if (t < 7) x = -1.4f;
            else if (t < 9) x = -1.4f + 1.4f * (float)(t - 7);
            else if (t < 11) x = 1.4f - 1.4f * (float)(t - 9);
            else x = -1.4f;
            float y = t >= 7.5 && t <= 8.5 ? .4f * Mathf.Sin((float)(t - 7.5) * Mathf.PI) : 0;
            return new Vector2(x, y);
        }
        public void SetReplay(bool value) { replay = value; ResetState(); }
        public void QueueJump() { if (!replay && Body.y <= 0) jumpQueued = true; }
        public void Advance(double seconds, float input)
        {
            settings.Clamp();
            if (Chain == null || Chain.Segments != settings.segments || Mathf.Abs(Chain.restLength * Chain.Segments - settings.length) > 1e-5f || activeFrequency != settings.frequency) ResetState();
            accumulator += seconds; double dt = 1.0 / activeFrequency;
            while (accumulator + 1e-9 >= dt)
            {
                Vector2 before = Body;
                if (replay) Body = ReplayPose((ticks + 1) * dt);
                else
                {
                    if (jumpQueued) { jumpVelocity = 3; jumpQueued = false; }
                    jumpVelocity -= 8 * (float)dt;
                    Body = new Vector2(Mathf.Clamp(Body.x + Mathf.Clamp(input, -1, 1) * 1.4f * (float)dt, -2.3f, 2.3f), Mathf.Max(0, Body.y + jumpVelocity * (float)dt));
                    if (Body.y == 0) jumpVelocity = 0;
                }
                BodySpeed = (Body.x - before.x) / (float)dt;
                if (Mathf.Abs(BodySpeed) > .01f) Facing = BodySpeed > 0 ? 1 : -1;
                Chain.Tick((float)dt, Body + Neck, settings); ticks++; accumulator -= dt;
            }
        }
        public void SingleStep() { paused = true; Advance(1.0 / settings.frequency, 0); RenderPose(); }
        void Update()
        {
            if (!drive) return;
            var k = Keyboard.current; float input = 0;
            if (Application.isFocused && k != null)
            {
                if (k.spaceKey.wasPressedThisFrame) paused = !paused;
                if (k.tKey.wasPressedThisFrame) SetReplay(!replay);
                if (k.rKey.wasPressedThisFrame) ResetDemo();
                if (k.nKey.wasPressedThisFrame) showNodes = !showNodes;
                if (k.hKey.wasPressedThisFrame) showControls = !showControls;
                if (k.periodKey.wasPressedThisFrame) SingleStep();
                if (k.jKey.wasPressedThisFrame) QueueJump();
                input = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
            }
            if (!paused) Advance(Math.Min(.1, Time.unscaledDeltaTime), input);
            // Apply topology/frequency changes while paused too, without advancing time.
            else Advance(0, 0);
            RenderPose();
        }
        public void RenderPose()
        {
            if (Chain == null || !rigidRobot || !dynamicRobot) return;
            int frame = Mathf.Abs(BodySpeed) < .01f && Body.y == 0 ? 0 : 1 + (int)(SimulationTime * 8) % 2;
            rigidRobot.sprite = dynamicRobot.sprite = frames[frame];
            rigidRobot.flipX = dynamicRobot.flipX = Facing < 0;
            rigidRobot.transform.position = new Vector3(Body.x, Body.y + UpperLane, 0);
            dynamicRobot.transform.position = new Vector3(Body.x, Body.y + LowerLane, 0);
            int count = Chain.positions.Length; rigidScarf.positionCount = dynamicScarf.positionCount = count;
            var direction = new Vector2(-Facing, -.25f).normalized;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = Body + Neck + direction * (i * Chain.restLength);
                linePositions[i] = new Vector3(p.x, p.y + UpperLane, 0);
            }
            rigidScarf.SetPositions(linePositions);
            for (int i = 0; i < count; i++) linePositions[i] = new Vector3(Chain.positions[i].x, Chain.positions[i].y + LowerLane, 0);
            dynamicScarf.SetPositions(linePositions);
            for (int i = 0; i < markers.Length; i++)
            {
                bool visible = showNodes && i < count; markers[i].gameObject.SetActive(visible);
                if (visible) markers[i].position = linePositions[i] + new Vector3(0, 0, -.01f);
            }
            if (sceneCamera)
            {
                float left = showControls ? Mathf.Min(.45f, 302 * UiScale / Screen.width) : 0;
                sceneCamera.rect = new Rect(left, 0, 1 - left, 1);
            }
        }
        void Styles()
        {
            if (heading != null && heading.fontSize == 22 && button != null && button.fixedHeight == 25) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            small = new GUIStyle(label) { fontSize = 11 };
            heading.normal.textColor = label.normal.textColor = small.normal.textColor = new Color(.9f, .93f, .94f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 12, fixedHeight = 25 };
        }
        float Slider(string text, float value, float min, float max) { GUILayout.Label($"{text}  {value:F2}", label); return GUILayout.HorizontalSlider(value, min, max); }
        void OnGUI()
        {
            if (!showControls || Chain == null) return;
            Styles(); float u = UiScale; var old = GUI.matrix; var color = GUI.color; GUI.matrix = Matrix4x4.Scale(Vector3.one * u);
            GUI.color = new Color(.035f, .045f, .075f); GUI.DrawTexture(new Rect(12, 12, 278, 696), Texture2D.whiteTexture); GUI.color = color;
            GUILayout.BeginArea(new Rect(27, 23, 248, 677));
            GUILayout.Label("010 / SCARF LAB", heading); GUILayout.Label("PIXEL SPRITE + DYNAMIC CHAIN", small); GUILayout.Space(8);
            if (GUILayout.Button(replay ? "T / Replay: start, stop, reverse, hop" : "T / Manual: A D move / J hop", button)) SetReplay(!replay);
            GUILayout.BeginHorizontal(); if (GUILayout.Button(paused ? "Space / Resume" : "Space / Pause", button)) paused = !paused;
            if (GUILayout.Button(". / Tick", button)) SingleStep(); GUILayout.EndHorizontal(); GUILayout.Space(6);
            GUILayout.Label("Free nodes (+ 1 pinned root)", label);
            int n = settings.segments <= 4 ? 0 : settings.segments <= 8 ? 1 : 2;
            settings.segments = new[] { 4, 8, 12 }[GUILayout.SelectionGrid(n, new[] { "4", "8", "12" }, 3, button)];
            settings.length = Slider("Rest length (m)", settings.length, .5f, 1.4f);
            settings.stiffness = Slider("Stretch stiffness (N/m)", settings.stiffness, 20, 500);
            settings.damping = Slider("Velocity damping (1/s)", settings.damping, .3f, 10);
            settings.gravity = Slider("Gravity (m/s2)", settings.gravity, 0, 10);
            GUILayout.Label("Simulation frequency", label);
            int hz = settings.frequency <= 30 ? 0 : settings.frequency <= 60 ? 1 : 2;
            settings.frequency = new[] { 30, 60, 120 }[GUILayout.SelectionGrid(hz, new[] { "30 Hz", "60 Hz", "120 Hz" }, 3, button)];
            GUILayout.Label("Node count / length / Hz restart motion.", small); GUILayout.Space(5);
            if (GUILayout.Button(showNodes ? "N / Show nodes: ON" : "N / Show nodes: OFF", button)) showNodes = !showNodes;
            if (GUILayout.Button("R / Reset baseline", button)) ResetDemo(); GUILayout.Space(8);
            GUILayout.Label($"Time {SimulationTime:F2}s / {activeFrequency} Hz\nTip speed {Chain.TipSpeed:F3} m/s\nMeasured length {Chain.ActualLength:F3} m\nExtension {(Chain.ActualLength / settings.length - 1) * 100:F2}%", label);
            GUILayout.Space(7); GUILayout.Label("A: a fixed shape follows the sprite.\nB: only the root follows; nodes retain motion.\n\nStiffness controls stretching, not bending.\nNo character, floor or self collision.\nH: hide UI", small); GUILayout.EndArea();
            GUI.Label(new Rect(320, 20, 1000, 32), "ONE SPRITE / TWO ATTACHMENT RULES", heading);
            LaneLabel("A / RIGID FOLLOW", UpperLane + 2.10f, u);
            LaneLabel("B / DYNAMIC SCARF", LowerLane + 2.10f, u);
            GUI.Label(new Rect(320, Screen.height / u - 48, Screen.width / u - 340, 44), "Stop to see the scarf continue moving. Reverse to see its shape carry across the turn.\nSprite frames use point sampling; the ribbon is continuous geometry.", small);
            GUI.matrix = old; GUI.color = color;
        }
        void LaneLabel(string text, float y, float u)
        {
            var screen = sceneCamera.WorldToScreenPoint(new Vector3(0, y, 0));
            GUI.Label(new Rect(325, (Screen.height - screen.y) / u, 800, 24), text, label);
        }
    }
}
