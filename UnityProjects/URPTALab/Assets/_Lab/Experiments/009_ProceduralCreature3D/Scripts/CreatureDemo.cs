using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TechArtLab.Creature
{
    public sealed class CreatureDemo : MonoBehaviour
    {
        [Serializable] public sealed class LegView
        {
            public Transform upper, lower, hip, knee, foot, target, rest;
            public Renderer footRenderer;
        }
        public Camera sceneCamera;
        public Transform body;
        public LegView[] legs;
        public Material stanceMaterial, swingMaterial;
        public QuadrupedGait.Settings settings = new();
        public bool replay = true, paused, drive = true, showControls = true, showTargets = true;
        public float yaw = 145, pitch = 32, zoom = 3.65f;
        public QuadrupedGait Model { get; private set; }
        public float MaxIKError { get; private set; }
        public const double TickSeconds = 1.0 / 120;
        double accumulator;
        bool running, previousBackground;
        [NonSerialized] GUIStyle heading, label, small, button;
        static readonly string[] Names = { "FL / A", "FR / B", "RL / B", "RR / A" };
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
            Model = new QuadrupedGait(settings); accumulator = 0; RenderPose();
        }
        public void ResetDemo()
        {
            settings = new(); replay = true; paused = false; showTargets = true;
            yaw = 145; pitch = 32; zoom = 3.65f; ResetState();
        }
        // The same public entry is used by frame-rate validation. Replay input is sampled per tick,
        // rather than per rendered frame, so a reversal on a frame boundary has one meaning.
        public void Advance(double seconds, Vector3 manualInput)
        {
            if (Model == null) ResetState();
            accumulator += seconds;
            while (accumulator + 1e-9 >= TickSeconds)
            {
                Model.Tick((float)TickSeconds, replay ? QuadrupedGait.ReplayInput(Model.Time) : manualInput, settings);
                accumulator -= TickSeconds;
            }
        }
        public void SingleStep()
        {
            paused = true; Advance(TickSeconds, Vector3.zero); RenderPose();
        }
        void Update()
        {
            if (!drive) return;
            var k = Keyboard.current; Vector3 input = Vector3.zero;
            if (Application.isFocused && k != null)
            {
                if (k.spaceKey.wasPressedThisFrame) paused = !paused;
                if (k.tKey.wasPressedThisFrame) replay = !replay;
                if (k.rKey.wasPressedThisFrame) ResetDemo();
                if (k.gKey.wasPressedThisFrame) showTargets = !showTargets;
                if (k.hKey.wasPressedThisFrame) showControls = !showControls;
                if (k.periodKey.wasPressedThisFrame) SingleStep();
                input = new Vector3((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), 0,
                    (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
            }
            var mouse = Mouse.current;
            if (Application.isFocused && mouse != null && (!showControls || mouse.position.ReadValue().x > 306 * UiScale))
            {
                if (mouse.rightButton.isPressed) { var d = mouse.delta.ReadValue(); yaw += d.x * .2f; pitch -= d.y * .2f; }
                zoom *= Mathf.Exp(-mouse.scroll.ReadValue().y * .001f);
            }
            if (!paused) Advance(Math.Min(Time.unscaledDeltaTime, .1), input);
            RenderPose();
        }
        public void RenderPose()
        {
            if (Model == null || !body || legs == null || legs.Length != 4) return;
            body.position = Model.Body; MaxIKError = 0;
            for (int i = 0; i < 4; i++)
            {
                var f = Model.feet[i]; var view = legs[i]; var hip = Model.Hip(i);
                var pole = new Vector3(QuadrupedGait.HipOffsets[i].x * 2, .15f, QuadrupedGait.HipOffsets[i].z * .35f);
                QuadrupedGait.SolveIK(hip, f.position, pole, QuadrupedGait.UpperLength, QuadrupedGait.LowerLength,
                    out var knee, out var foot, out _);
                MaxIKError = Mathf.Max(MaxIKError, Vector3.Distance(foot, f.position));
                view.hip.position = hip; view.knee.position = knee;
                // The foot's sole, not its center, touches y=0.
                view.foot.position = foot + Vector3.up * .065f;
                view.footRenderer.sharedMaterial = f.swinging ? swingMaterial : stanceMaterial;
                Rod(view.upper, hip, knee, .15f); Rod(view.lower, knee, foot, .105f);
                view.target.gameObject.SetActive(showTargets); view.rest.gameObject.SetActive(showTargets);
                view.target.position = f.goal + Vector3.up * .009f;
                view.rest.position = Model.Rest(i) + Vector3.up * .014f;
            }
            if (sceneCamera)
            {
                pitch = Mathf.Clamp(pitch, 12, 75); zoom = Mathf.Clamp(zoom, 2.6f, 6);
                var rotation = Quaternion.Euler(pitch, yaw, 0);
                var focus = new Vector3(Model.Body.x, .55f, Model.Body.z);
                sceneCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * 18, rotation);
                float left = showControls ? Mathf.Min(.45f, 302 * UiScale / Screen.width) : 0;
                sceneCamera.rect = new Rect(left, 0, 1 - left, 1);
                sceneCamera.orthographic = true; sceneCamera.orthographicSize = zoom;
            }
        }
        public static void Rod(Transform rod, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 delta = b - a; rod.position = (a + b) * .5f;
            rod.rotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            rod.localScale = new Vector3(thickness, delta.magnitude * .5f, thickness);
        }
        void Styles()
        {
            if (heading != null && heading.fontSize == 22 && button != null && button.fixedHeight == 27) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            small = new GUIStyle(label) { fontSize = 11 };
            heading.normal.textColor = label.normal.textColor = small.normal.textColor = new Color(.87f, .91f, .90f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 12, fixedHeight = 27 };
        }
        float Slider(string title, float value, float min, float max)
        {
            GUILayout.Label($"{title}  {value:F2}", label); return GUILayout.HorizontalSlider(value, min, max);
        }
        void OnGUI()
        {
            if (!showControls || Model == null) return;
            Styles(); float u = UiScale; var matrix = GUI.matrix; var color = GUI.color;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * u);
            GUI.color = new Color(.035f, .055f, .06f); GUI.DrawTexture(new Rect(12, 12, 278, 696), Texture2D.whiteTexture); GUI.color = color;
            GUILayout.BeginArea(new Rect(27, 23, 248, 675));
            GUILayout.Label("009 / WALK LAB", heading);
            GUILayout.Label("WORLD-SPACE FEET + TWO-BONE IK", small); GUILayout.Space(8);
            if (GUILayout.Button(replay ? "T / Replay: forward, stop, reverse" : "T / Manual: WASD (world XZ)", button)) replay = !replay;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(paused ? "Space / Resume" : "Space / Pause", button)) paused = !paused;
            if (GUILayout.Button(". / Tick", button)) SingleStep(); GUILayout.EndHorizontal(); GUILayout.Space(6);
            settings.speed = Slider("Body speed (m/s)", settings.speed, 0, 1.4f);
            settings.stepLead = Slider("Landing lead (m)", settings.stepLead, .08f, .5f);
            settings.triggerDistance = Slider("Step trigger (m)", settings.triggerDistance, .12f, .5f);
            settings.lift = Slider("Foot lift (m)", settings.lift, .04f, .45f);
            settings.bodyHeight = Slider("Body height (m)", settings.bodyHeight, .75f, 1.35f);
            settings.swingDuration = Slider("Swing time (s)", settings.swingDuration, .18f, .6f);
            GUILayout.Space(6);
            if (GUILayout.Button(showTargets ? "G / Targets: ON" : "G / Targets: OFF", button)) showTargets = !showTargets;
            if (GUILayout.Button("R / Reset parameters + replay", button)) ResetDemo();
            GUILayout.Space(8);
            for (int i = 0; i < 4; i++)
            {
                var f = Model.feet[i]; GUI.contentColor = f.swinging ? new Color(1, .68f, .22f) : new Color(.38f, .9f, .73f);
                GUILayout.Label($"{Names[i]}    {(f.swinging ? $"SWING  {f.phase:P0}" : "PLANTED")}", label);
            }
            GUI.contentColor = Color.white;
            GUILayout.Space(4);
            GUILayout.Label($"Time {Model.Time:F2}s   /   {Model.Steps} pair steps\nActual speed {new Vector2(Model.Velocity.x, Model.Velocity.z).magnitude:F2} m/s\nFoot / IK error {MaxIKError:E1} m", small);
            GUILayout.Label(Model.MovementLimited ? "REACH LIMIT: body motion reduced" : "120 Hz gait / constant segment lengths", small);
            GUILayout.Space(5); GUILayout.Label("Amber rings: landing anchors\nBlue crosses: body-relative rest positions\nRMB: orbit | Wheel: zoom | H: hide UI", small);
            GUILayout.EndArea();
            GUI.Label(new Rect(320, 22, 880, 32), "FOUR-LEG EXPLORER / FLAT-GROUND GAIT", heading);
            GUI.Label(new Rect(320, Screen.height / u - 56, Screen.width / u - 340, 48),
                "Green feet stay in world space while the body moves. Orange feet are in flight.\nDiagonal pairs alternate; this demo animates support, without simulating physical balance.", small);
            GUI.matrix = matrix; GUI.color = color;
        }
    }
}
