using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TechArtLab.Pixelated
{
    /// <summary>A low-resolution camera target plus an unfiltered overlay presentation.
    /// No full-resolution world render is performed before pixelation.</summary>
    public sealed class LowResPixelDemo : MonoBehaviour
    {
        public Camera sceneCamera;
        public int resolutionIndex = 1;
        public bool bilinear, nativeReference, orthographic = true;
        public bool showControls = true, drive = true, replay, paused;
        public float yaw = 45, pitch = 35.26439f, distance = 12, orthoSize = 4.1f;
        public Vector3 focus = new Vector3(0, 0.65f, 0);
        public float replayTime;
        public static readonly int[] Heights = { 90, 180, 270, 360 };
        public RenderTexture Target { get; private set; }
        public Rect ImageRect { get; private set; } // bottom-left screen coordinates
        public float PixelScale { get; private set; }
        public bool FractionalFit => !nativeReference && PixelScale < 1;

        Canvas canvas;
        RawImage image;
        RenderTexture previousTarget;
        bool previousRunInBackground;
        Texture2D panelTexture;
        GUIStyle heading, label, small, button;
        float UiScale => Mathf.Max(.5f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));

        void OnEnable()
        {
            if (!Application.isPlaying || !sceneCamera) return;
            previousTarget = sceneCamera.targetTexture;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            var go = new GameObject("006 / Presentation", typeof(Canvas));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -100;
            var output = new GameObject("Low-res image", typeof(RectTransform), typeof(RawImage));
            output.transform.SetParent(canvas.transform, false);
            image = output.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            image.rectTransform.pivot = Vector2.zero;
            var data = sceneCamera.GetUniversalAdditionalCameraData();
            data.antialiasing = AntialiasingMode.None;
            data.renderPostProcessing = false;
            sceneCamera.allowMSAA = false;
            sceneCamera.allowHDR = false;
            sceneCamera.allowDynamicResolution = false;
            RefreshOutput();
            ApplyPose();
        }

        public static Rect FitImage(Rect available, int width, int height, bool integer, out float scale)
        {
            scale = Mathf.Min(available.width / width, available.height / height);
            if (integer && scale >= 1) scale = Mathf.Floor(scale);
            return new Rect(Mathf.Floor(available.center.x - width * scale * .5f),
                Mathf.Floor(available.center.y - height * scale * .5f), width * scale, height * scale);
        }

        public void RefreshOutput()
        {
            if (!image) return;
            resolutionIndex = Mathf.Clamp(resolutionIndex, 0, Heights.Length - 1);
            float ui = UiScale;
            var available = new Rect(302 * ui, 66 * ui,
                Mathf.Max(16, Screen.width - 320 * ui), Mathf.Max(9, Screen.height - 128 * ui));
            int height = Heights[resolutionIndex], width = height * 16 / 9;
            if (nativeReference)
            {
                // Native reference uses one target texel per display pixel, same 16:9 framing.
                int units = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(available.width / 16, available.height / 9)));
                width = units * 16; height = units * 9;
            }
            SetTargetSize(width, height);
            ImageRect = FitImage(available, width, height, !nativeReference, out float scale);
            PixelScale = scale;
            if (nativeReference) { PixelScale = 1; ImageRect = FitImage(available, width, height, true, out _); }
            image.rectTransform.anchoredPosition = ImageRect.position;
            image.rectTransform.sizeDelta = ImageRect.size;
        }

        public void SetTargetSize(int width, int height)
        {
            if (!Target || Target.width != width || Target.height != height)
            {
                ReleaseTarget();
                Target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = $"006 / {width}x{height}", antiAliasing = 1,
                    useMipMap = false, autoGenerateMips = false,
                    wrapMode = TextureWrapMode.Clamp, useDynamicScale = false
                };
                Target.Create();
            }
            Target.filterMode = bilinear ? FilterMode.Bilinear : FilterMode.Point;
            sceneCamera.targetTexture = Target;
            sceneCamera.aspect = 16f / 9;
            if (image) image.texture = Target;
        }

        public void ApplyPose()
        {
            if (!sceneCamera) return;
            pitch = Mathf.Clamp(pitch, 8, 80);
            distance = Mathf.Clamp(distance, 5, 22);
            orthoSize = Mathf.Clamp(orthoSize, 2, 8);
            var orientation = Quaternion.Euler(pitch, yaw, 0);
            // Deliberately continuous: 007 will compare snapping on this kind of motion.
            var offset = replay ? Vector3.right * Mathf.Sin(replayTime * .65f) * .65f : Vector3.zero;
            sceneCamera.transform.SetPositionAndRotation(focus + offset - orientation * Vector3.forward * distance, orientation);
            sceneCamera.orthographic = orthographic;
            sceneCamera.orthographicSize = orthoSize;
            sceneCamera.fieldOfView = 40;
        }

        public void ResetView()
        {
            yaw = 45; pitch = 35.26439f; distance = 12; orthoSize = 4.1f;
            orthographic = true; replay = false; paused = false; replayTime = 0;
            ApplyPose();
        }

        void Update()
        {
            if (!drive) return;
            var keys = Keyboard.current;
            if (keys != null && Application.isFocused)
            {
                if (keys.digit1Key.wasPressedThisFrame) { resolutionIndex = 0; nativeReference = false; }
                if (keys.digit2Key.wasPressedThisFrame) { resolutionIndex = 1; nativeReference = false; }
                if (keys.digit3Key.wasPressedThisFrame) { resolutionIndex = 2; nativeReference = false; }
                if (keys.digit4Key.wasPressedThisFrame) { resolutionIndex = 3; nativeReference = false; }
                if (keys.bKey.wasPressedThisFrame) bilinear = !bilinear;
                if (keys.nKey.wasPressedThisFrame) nativeReference = !nativeReference;
                if (keys.oKey.wasPressedThisFrame) orthographic = !orthographic;
                if (keys.tKey.wasPressedThisFrame) { replay = !replay; replayTime = 0; }
                if (keys.spaceKey.wasPressedThisFrame) paused = !paused;
                if (keys.rKey.wasPressedThisFrame || keys.fKey.wasPressedThisFrame) ResetView();
                if (keys.hKey.wasPressedThisFrame) showControls = !showControls;
            }
            var mouse = Mouse.current;
            if (mouse != null && Application.isFocused && ImageRect.Contains(mouse.position.ReadValue()))
            {
                if (mouse.rightButton.isPressed)
                {
                    var d = mouse.delta.ReadValue();
                    yaw += d.x * .2f; pitch -= d.y * .2f; replay = false;
                }
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f)
                {
                    float factor = Mathf.Exp(-scroll * .001f);
                    if (orthographic) orthoSize *= factor; else distance *= factor;
                }
            }
            if (replay && !paused) replayTime += Mathf.Min(Time.unscaledDeltaTime, .1f);
            RefreshOutput();
            ApplyPose();
        }

        void InitStyles()
        {
            // Managed GUIStyles may survive an Editor transition after their Texture2D is gone.
            if (heading != null && panelTexture) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            heading.normal.textColor = new Color(.85f, .94f, .94f);
            label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            label.normal.textColor = new Color(.8f, .86f, .9f);
            small = new GUIStyle(label) { fontSize = 12 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 13, fixedHeight = 32 };
            panelTexture = new Texture2D(1, 1) { name = "006 GUI panel", hideFlags = HideFlags.HideAndDontSave };
            panelTexture.SetPixel(0, 0, new Color(.055f, .078f, .11f, .98f)); panelTexture.Apply();
        }

        void OnGUI()
        {
            if (!showControls || !Target) return;
            InitStyles();
            var old = GUI.matrix; float ui = UiScale;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * ui);
            GUI.DrawTexture(new Rect(12, 12, 276, 696), panelTexture);
            GUILayout.BeginArea(new Rect(26, 24, 248, 670));
            GUILayout.Label("006 / PIXEL LAB", heading);
            GUILayout.Label("LOW-RES REALTIME 3D", small);
            GUILayout.Space(16);
            GUILayout.Label("Internal resolution", label);
            int next = GUILayout.SelectionGrid(resolutionIndex, new[] { "1  /  160 x 90", "2  /  320 x 180", "3  /  480 x 270", "4  /  640 x 360" }, 1, button);
            if (next != resolutionIndex) { resolutionIndex = next; nativeReference = false; }
            GUILayout.Space(8);
            if (GUILayout.Button(nativeReference ? "N / Native reference : ON" : "N / Native reference : OFF", button)) nativeReference = !nativeReference;
            if (GUILayout.Button(bilinear ? "B / Filter : BILINEAR" : "B / Filter : POINT", button)) bilinear = !bilinear;
            GUILayout.Space(12);
            if (GUILayout.Button(orthographic ? "O / Orthographic" : "O / Perspective", button)) orthographic = !orthographic;
            if (GUILayout.Button("F / Fixed isometric view", button)) ResetView();
            if (GUILayout.Button(replay ? "T / Stop slow pan" : "T / Replay slow pan", button)) { replay = !replay; replayTime = 0; }
            if (GUILayout.Button(paused ? "Space / Resume pan" : "Space / Pause pan", button)) paused = !paused;
            GUILayout.Space(14);
            GUILayout.Label($"Target   {Target.width} x {Target.height}\nDisplay  {ImageRect.width:0} x {ImageRect.height:0}\nScale     {PixelScale:0.##}x   |   MSAA off", label);
            GUILayout.Space(12);
            GUILayout.Label("RMB drag: orbit\nWheel: zoom   |   R: reset\nH: hide / show controls", small);
            GUILayout.Space(12);
            GUILayout.Label(FractionalFit ? "Viewport is smaller than the target. Fractional fit; enlarge Game view for integer pixels." : "Point + integer scale preserves blocks.\nBilinear blends their boundaries.", small);
            GUILayout.EndArea();
            float x = 310;
            GUI.Label(new Rect(x, 22, 850, 30), "SHAPE  /  LIGHT  /  SAMPLING", heading);
            GUI.Label(new Rect(x, Screen.height / ui - 49, 930, 42),
                "Cube: hard edges   |   Sphere: curved edge   |   Ramp: diagonal\nSteps: repeated detail   |   Thin rods: subpixel disappearance", small);
            GUI.matrix = old;
        }

        void ReleaseTarget()
        {
            if (!Target) return;
            if (sceneCamera && sceneCamera.targetTexture == Target) sceneCamera.targetTexture = previousTarget;
            if (image) image.texture = null;
            Target.Release(); Destroy(Target); Target = null;
        }
        void OnDisable()
        {
            ReleaseTarget();
            if (canvas) Application.runInBackground = previousRunInBackground;
            if (canvas) Destroy(canvas.gameObject);
            if (panelTexture) Destroy(panelTexture);
            canvas = null; image = null; panelTexture = null;
            heading = label = small = button = null;
        }
    }
}
