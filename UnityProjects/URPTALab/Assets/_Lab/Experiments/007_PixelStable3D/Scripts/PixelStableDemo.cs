using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TechArtLab.PixelStable
{
    // The scene stays continuous. Only the rendering camera is snapped; presentation
    // compensates its residual. The two stabilized views share one camera and RT.
    public sealed class PixelStableDemo : MonoBehaviour
    {
        public Camera continuousCamera, snappedCamera;
        public Light sun;
        public bool drive = true, replay = true, paused, shadows, showControls = true;
        public int resolutionIndex, focusMode = -1; // -1: compare, 0/1/2: A/B/C
        public float orthoSize = 4.1f, yaw = 45, pitch = 35.26439f, speed = 2, replayTime;
        public Vector3 focus = new Vector3(0, .65f, 0);
        public Vector2 manualPan;
        public static readonly int[] Heights = {90, 180, 270, 360};
        public static readonly string[] Names = {"A / CONTINUOUS", "B / SNAP", "C / SNAP + COMPENSATE"};
        public int Width => Heights[Mathf.Clamp(resolutionIndex, 0, 3)] * 16 / 9;
        public int Height => Heights[Mathf.Clamp(resolutionIndex, 0, 3)];
        public float WorldPerPixel => 2 * orthoSize / Height;
        public Quaternion Orientation => Quaternion.Euler(pitch, yaw, 0);
        public Vector3 BasePosition => focus - Orientation * Vector3.forward * 12;
        public RenderTexture ContinuousTarget { get; private set; }
        public RenderTexture SnappedTarget { get; private set; }
        public Vector2 ResidualPixels { get; private set; }
        public Vector3 DesiredPosition { get; private set; }
        public Vector3 RenderPosition { get; private set; }
        public readonly Rect[] ImageRects = new Rect[3]; // bottom-left screen space
        public readonly float[] Scales = new float[3];
        const int Border = 1;
        Canvas canvas;
        readonly RawImage[] images = new RawImage[3];
        RenderTexture previousContinuous, previousSnapped;
        bool previousBackground, previousContinuousEnabled, previousSnappedEnabled, presenting;
        LightShadows previousShadows;
        [System.NonSerialized] GUIStyle title, label, small, button;
        float UiScale => Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));

        // Absolute world origin fixes the grid phase. Never accumulate rounded motion.
        public static Vector3 Snap(Vector3 desired, Quaternion rotation, float step, out Vector2 residual)
        {
            Vector3 right = rotation * Vector3.right, up = rotation * Vector3.up;
            float x = Vector3.Dot(desired, right), y = Vector3.Dot(desired, up);
            float dx = x - Mathf.Round(x / step) * step;
            float dy = y - Mathf.Round(y / step) * step;
            residual = new Vector2(dx, dy) / step;
            return desired - right * dx - up * dy;
        }

        public Rect SampleRect(int mode)
        {
            Vector2 shift = mode == 2 ? ResidualPixels : Vector2.zero;
            // Sample towards the desired camera, equivalent to moving the image
            // oppositely. Guard texels cover the full +/- half-texel correction.
            return new Rect((Border + shift.x) / (Width + 2f * Border),
                (Border + shift.y) / (Height + 2f * Border),
                Width / (Width + 2f * Border), Height / (Height + 2f * Border));
        }
        public RenderTexture Source(int mode) => mode == 0 ? ContinuousTarget : SnappedTarget;

        void OnEnable()
        {
            if (!Application.isPlaying || !continuousCamera || !snappedCamera) return;
            title = label = small = button = null; // Rebuild native GUI styles after Editor reloads.
            previousContinuous = continuousCamera.targetTexture; previousSnapped = snappedCamera.targetTexture;
            previousContinuousEnabled = continuousCamera.enabled; previousSnappedEnabled = snappedCamera.enabled;
            previousBackground = Application.runInBackground; Application.runInBackground = true; presenting = true;
            if (sun) previousShadows = sun.shadows;
            canvas = new GameObject("007 / Presentation", typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -100;
            for (int i = 0; i < 3; i++)
            {
                images[i] = new GameObject(Names[i], typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                images[i].transform.SetParent(canvas.transform, false);
                images[i].raycastTarget = false;
                var r = images[i].rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = Vector2.zero;
            }
            Configure(continuousCamera); Configure(snappedCamera);
            Refresh(); ApplyPose();
        }
        static void Configure(Camera camera)
        {
            camera.orthographic = true; camera.allowHDR = false; camera.allowMSAA = false;
            camera.allowDynamicResolution = false;
            var data = camera.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.None; data.renderPostProcessing = false;
        }
        RenderTexture MakeTarget(string name)
        {
            var rt = new RenderTexture(Width + 2 * Border, Height + 2 * Border, 24, RenderTextureFormat.ARGB32)
            {
                name = name, antiAliasing = 1, filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, useMipMap = false, autoGenerateMips = false,
                useDynamicScale = false
            };
            rt.Create(); return rt;
        }
        public void Refresh()
        {
            if (!canvas) return;
            resolutionIndex = Mathf.Clamp(resolutionIndex, 0, 3); focusMode = Mathf.Clamp(focusMode, -1, 2);
            orthoSize = Mathf.Clamp(orthoSize, 2, 8);
            if (!ContinuousTarget || ContinuousTarget.width != Width + 2 || ContinuousTarget.height != Height + 2)
            {
                ReleaseTargets();
                ContinuousTarget = MakeTarget("007 / Continuous + guard"); SnappedTarget = MakeTarget("007 / Snapped + guard");
                continuousCamera.targetTexture = ContinuousTarget; snappedCamera.targetTexture = SnappedTarget;
            }
            // Comparison costs two world renders. Focus mode only renders its selected source.
            continuousCamera.enabled = focusMode < 0 || focusMode == 0;
            snappedCamera.enabled = focusMode != 0;
            if (sun) sun.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            float u = UiScale;
            for (int i = 0; i < 3; i++)
            {
                bool visible = focusMode < 0 || focusMode == i;
                images[i].gameObject.SetActive(visible); images[i].texture = Source(i);
                if (!visible) continue;
                float column = (Screen.width - 48 * u) / 3;
                var available = focusMode < 0
                    ? new Rect(24 * u + i * column + 8 * u, 170 * u, column - 16 * u, Mathf.Max(9, Screen.height - 380 * u))
                    : new Rect(32 * u, 140 * u, Mathf.Max(16, Screen.width - 64 * u), Mathf.Max(9, Screen.height - 356 * u));
                ImageRects[i] = Fit(available, Width, Height, out Scales[i]);
                images[i].rectTransform.anchoredPosition = ImageRects[i].position;
                images[i].rectTransform.sizeDelta = ImageRects[i].size;
            }
        }
        public static Rect Fit(Rect available, int width, int height, out float scale)
        {
            scale = Mathf.Min(available.width / width, available.height / height);
            if (scale >= 1) scale = Mathf.Floor(scale);
            return new Rect(Mathf.Floor(available.center.x - width * scale / 2),
                Mathf.Floor(available.center.y - height * scale / 2), width * scale, height * scale);
        }
        public void ApplyPose()
        {
            float phase = replayTime * speed * WorldPerPixel / .65f;
            var pan = manualPan + (replay ? new Vector2(.65f * Mathf.Sin(phase), .24f * Mathf.Sin(phase * .7f)) : Vector2.zero);
            ApplyPosition(BasePosition + Orientation * new Vector3(pan.x, pan.y, 0));
        }
        public void ApplyPosition(Vector3 desired)
        {
            if (!continuousCamera || !snappedCamera) return;
            DesiredPosition = desired;
            RenderPosition = Snap(desired, Orientation, WorldPerPixel, out var residual);
            ResidualPixels = residual;
            continuousCamera.transform.SetPositionAndRotation(desired, Orientation);
            snappedCamera.transform.SetPositionAndRotation(RenderPosition, Orientation);
            // Expand the frustum with the guard border, preserving the original pixel footprint.
            float size = orthoSize * (Height + 2f * Border) / Height;
            continuousCamera.orthographicSize = snappedCamera.orthographicSize = size;
            continuousCamera.aspect = snappedCamera.aspect = (Width + 2f * Border) / (Height + 2f * Border);
            for (int i = 0; i < 3; i++) if (images[i]) images[i].uvRect = SampleRect(i);
        }
        public void ResetView()
        {
            yaw = 45; pitch = 35.26439f; orthoSize = 4.1f; manualPan = Vector2.zero;
            replayTime = 0; replay = true; paused = false; ApplyPose();
        }
        void Update()
        {
            if (!drive) return;
            var k = Keyboard.current;
            if (k != null && Application.isFocused)
            {
                if (k.digit1Key.wasPressedThisFrame) focusMode = 0;
                if (k.digit2Key.wasPressedThisFrame) focusMode = 1;
                if (k.digit3Key.wasPressedThisFrame) focusMode = 2;
                if (k.digit0Key.wasPressedThisFrame) focusMode = -1;
                if (k.spaceKey.wasPressedThisFrame) paused = !paused;
                if (k.tKey.wasPressedThisFrame) { replay = !replay; replayTime = 0; }
                if (k.rKey.wasPressedThisFrame) ResetView();
                if (k.lKey.wasPressedThisFrame) shadows = !shadows;
                if (k.hKey.wasPressedThisFrame) showControls = !showControls;
            }
            var m = Mouse.current;
            if (m != null && Application.isFocused && m.rightButton.isPressed)
                for (int i = 0; i < 3; i++) if ((focusMode < 0 || focusMode == i) && ImageRects[i].Contains(m.position.ReadValue()))
                {
                    if (replay) { manualPan = new Vector2(Vector3.Dot(DesiredPosition - BasePosition, Orientation * Vector3.right),
                        Vector3.Dot(DesiredPosition - BasePosition, Orientation * Vector3.up)); replay = false; }
                    manualPan -= m.delta.ReadValue() * (WorldPerPixel / Scales[i]); break;
                }
            if (replay && !paused) replayTime += Mathf.Min(Time.unscaledDeltaTime, .1f);
            Refresh(); ApplyPose();
        }
        void Styles()
        {
            if (title != null && title.fontSize == 22 && button != null && button.fixedHeight == 32) return;
            title = new GUIStyle(GUI.skin.label) {fontSize = 22, fontStyle = FontStyle.Bold};
            label = new GUIStyle(GUI.skin.label) {fontSize = 15, wordWrap = true};
            label.normal.textColor = title.normal.textColor = new Color(.85f, .90f, .93f);
            small = new GUIStyle(label) {fontSize = 13};
            button = new GUIStyle(GUI.skin.button) {fontSize = 13, fixedHeight = 32};
        }
        void OnGUI()
        {
            if (!showControls || !ContinuousTarget) return;
            Styles(); float u = UiScale, w = Screen.width / u, h = Screen.height / u;
            var old = GUI.matrix; GUI.matrix = Matrix4x4.Scale(Vector3.one * u);
            GUI.Label(new Rect(28, 16, 800, 35), "007 / PIXEL STABILITY", title);
            GUI.Label(new Rect(w - 430, 25, 400, 25), "Same scene / same path / Point sampling / no AA", small);
            GUILayout.BeginArea(new Rect(28, 60, w - 56, 126));
            GUILayout.BeginHorizontal();
            focusMode = GUILayout.SelectionGrid(focusMode + 1, new[] {"0 / Compare", "1 / Continuous", "2 / Snap", "3 / Compensate"}, 4, button) - 1;
            if (GUILayout.Button(paused ? "Space / Resume" : "Space / Pause", button)) paused = !paused;
            if (GUILayout.Button("R / Reset", button)) ResetView();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Resolution", label, GUILayout.Width(90));
            resolutionIndex = GUILayout.SelectionGrid(resolutionIndex, new[] {"160 x 90", "320 x 180", "480 x 270", "640 x 360"}, 4, button);
            if (GUILayout.Button(shadows ? "L / Shadows ON" : "L / Shadows OFF", button, GUILayout.Width(180))) shadows = !shadows;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Peak X {speed:F1} px/s", small, GUILayout.Width(120)); speed = GUILayout.HorizontalSlider(speed, .25f, 8, GUILayout.Width(120));
            GUILayout.Label($"Size {orthoSize:F1}", small, GUILayout.Width(76)); orthoSize = GUILayout.HorizontalSlider(orthoSize, 2, 8, GUILayout.Width(120));
            GUILayout.Label($"Yaw {yaw:F0}", small, GUILayout.Width(76)); yaw = GUILayout.HorizontalSlider(yaw, 0, 90, GUILayout.Width(120));
            GUILayout.Label($"t = {replayTime:F2} s", small, GUILayout.Width(90));
            float nextTime = GUILayout.HorizontalSlider(replayTime, 0, Mathf.Max(10, replayTime));
            if (Mathf.Abs(nextTime - replayTime) > .001f) { replayTime = nextTime; paused = true; replay = true; }
            GUILayout.EndHorizontal(); GUILayout.EndArea();
            string[] descriptions = {"Smooth camera; edges resample every frame.", "Stable sampling; movement jumps one source pixel.", "Stable sampling; residual moves the enlarged image."};
            for (int i = 0; i < 3; i++) if (focusMode < 0 || focusMode == i)
            {
                Rect r = ImageRects[i]; float x = r.x / u, top = (Screen.height - r.yMax) / u, bottom = (Screen.height - r.yMin) / u;
                GUI.Label(new Rect(x, top - 34, Mathf.Max(r.width / u, 360), 30), Names[i], label);
                if (focusMode < 0) GUI.Label(new Rect(x, bottom + 12, r.width / u, 50), descriptions[i], small);
            }
            GUI.Label(new Rect(28, h - 115, w - 56, 50),
                $"Grid: {WorldPerPixel:F5} world units / pixel     Residual: ({ResidualPixels.x:F3}, {ResidualPixels.y:F3}) source px\nCrop: {Width} x {Height}    Guarded RT: {Width + 2} x {Height + 2}    Output: {Scales[Mathf.Max(0, focusMode)]:0.##}x    Rendered cameras: {(focusMode < 0 ? 2 : 1)}", small);
            bool fractional = false; for (int i = 0; i < 3; i++) if ((focusMode < 0 || focusMode == i) && Scales[i] < 1) fractional = true;
            GUI.Label(new Rect(28, h - 58, w - 56, 48), fractional
                ? "Fractional downscale: enlarge Game view or choose a focus view for equal-sized pixel blocks."
                : "RMB drag: pan   |   T: replay on/off   |   H: hide UI\nFixed angle + zoom + static world: translation stability only. Shadows, rotation and moving objects have separate sampling.", small);
            GUI.matrix = old;
        }
        void ReleaseTargets()
        {
            if (continuousCamera) continuousCamera.targetTexture = previousContinuous;
            if (snappedCamera) snappedCamera.targetTexture = previousSnapped;
            foreach (var image in images) if (image) image.texture = null;
            if (ContinuousTarget) { ContinuousTarget.Release(); Destroy(ContinuousTarget); }
            if (SnappedTarget) { SnappedTarget.Release(); Destroy(SnappedTarget); }
            ContinuousTarget = SnappedTarget = null;
        }
        void OnDisable()
        {
            if (!presenting) return;
            presenting = false;
            ReleaseTargets(); Application.runInBackground = previousBackground;
            if (continuousCamera) continuousCamera.enabled = previousContinuousEnabled;
            if (snappedCamera) snappedCamera.enabled = previousSnappedEnabled;
            if (sun) sun.shadows = previousShadows;
            if (canvas) Destroy(canvas.gameObject);
            canvas = null; title = label = small = button = null;
        }
    }
}
