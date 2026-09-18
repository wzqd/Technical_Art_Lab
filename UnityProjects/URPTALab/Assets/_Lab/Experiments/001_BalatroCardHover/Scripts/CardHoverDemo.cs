using UnityEngine;
using UnityEngine.InputSystem;

namespace TechArtLab.Balatro
{
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public sealed class CardHoverDemo : MonoBehaviour
    {
        public Camera viewCamera;
        [Range(0, 3)] public float strength = 1;
        [Min(0.1f)] public float distanceScaleInCardHeights = 0.8f;
        [Min(0)] public float response = 12;
        public bool fixedInput;
        public Vector2 fixedCardUV = new Vector2(0.5f, 0.5f);
        [Range(0, 1)] public float fixedHover = 1;
        public bool showControls = true;
        float hover;
        MaterialPropertyBlock properties;
        MeshRenderer cardRenderer;

        void OnEnable()
        {
            properties = new MaterialPropertyBlock();
            cardRenderer = GetComponent<MeshRenderer>();
            if (!viewCamera) viewCamera = Camera.main;
        }

        void Update()
        {
            if (!viewCamera) return;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SetState(false, new Vector2(0.5f, 0.5f), 0);
                if (keyboard.digit2Key.wasPressedThisFrame) SetState(true, new Vector2(0.5f, 0.5f), 1);
                if (keyboard.digit3Key.wasPressedThisFrame) SetState(true, new Vector2(0.05f, 0.95f), 1);
                if (keyboard.digit4Key.wasPressedThisFrame) SetState(true, new Vector2(0.95f, 0.05f), 1);
                if (keyboard.digit0Key.wasPressedThisFrame) SetState(true, new Vector2(0.5f, 0.5f), 0);
            }
            Apply(Time.unscaledDeltaTime);
        }

        public void SetState(bool useFixed, Vector2 uv, float amount)
        {
            fixedInput = useFixed; fixedCardUV = uv; fixedHover = amount;
        }

        public void Apply(float dt)
        {
            if (!viewCamera || !cardRenderer) return;
            // Demo contract: axis-aligned unit quad, orthographic full-viewport camera.
            Vector2 bottom = viewCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(-0.5f, -0.5f, 0)));
            Vector2 top = viewCamera.WorldToScreenPoint(transform.TransformPoint(new Vector3(0.5f, 0.5f, 0)));
            var rect = Rect.MinMaxRect(bottom.x, bottom.y, top.x, top.y);
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            float target = Mouse.current != null && rect.Contains(mouse) && Application.isFocused ? 1 : 0;
            if (fixedInput)
            {
                mouse = bottom + Vector2.Scale(top - bottom, fixedCardUV);
                target = fixedHover;
            }
            // Fixed states bypass easing so reference captures are deterministic.
            hover = fixedInput || response <= 0 ? target : Mathf.Lerp(hover, target, 1 - Mathf.Exp(-response * dt));
            mouse = new Vector2(Mathf.Clamp(mouse.x, bottom.x, top.x), Mathf.Clamp(mouse.y, bottom.y, top.y));
            cardRenderer.GetPropertyBlock(properties);
            properties.SetFloat("_Hover", hover);
            properties.SetFloat("_Strength", strength);
            properties.SetFloat("_ScreenScale", Mathf.Max(1, rect.height * distanceScaleInCardHeights));
            properties.SetVector("_MousePixels", new Vector4(mouse.x - viewCamera.pixelRect.x, mouse.y - viewCamera.pixelRect.y, 0, 0));
            properties.SetVector("_ViewportSize", new Vector4(viewCamera.pixelWidth, viewCamera.pixelHeight, 0, 0));
            cardRenderer.SetPropertyBlock(properties);
        }

        void OnGUI()
        {
            if (!showControls) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            float uiScale = Mathf.Max(0.5f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
            GUILayout.BeginArea(new Rect(20, 20, 370, 240), GUI.skin.box);
            GUILayout.Label("001 / BALATRO CARD HOVER");
            GUILayout.Label("4 vertices, 2 triangles. Per-vertex perspective.");
            GUILayout.Label("1 Mouse   0 Idle   2 Center   3 Top-left   4 Bottom-right");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Mouse")) SetState(false, Vector2.one * 0.5f, 0);
            if (GUILayout.Button("Idle")) SetState(true, Vector2.one * 0.5f, 0);
            if (GUILayout.Button("Center")) SetState(true, Vector2.one * 0.5f, 1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Top-left")) SetState(true, new Vector2(0.05f, 0.95f), 1);
            if (GUILayout.Button("Bottom-right")) SetState(true, new Vector2(0.95f, 0.05f), 1);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Strength: {strength:F2}  |  Hover: {hover:F2}");
            strength = GUILayout.HorizontalSlider(strength, 0, 3);
            GUILayout.Label("Test artwork. Source timing/coordinates not matched.");
            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }
    }
}
