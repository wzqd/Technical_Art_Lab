using UnityEngine;
using UnityEngine.InputSystem;

namespace TechArtLab.Balatro.Dissolve
{
    [ExecuteAlways]
    public sealed class CardDissolveDemo : MonoBehaviour
    {
        public enum Look { OrangeExit, GreenEntry }
        public enum View { Color, Mask, Field }
        public Camera viewCamera;
        public MeshRenderer cardRenderer, shadowRenderer;
        public Texture2D aceTexture, lockTexture;
        public Look look;
        public View view;
        [Range(0,1)] public float dissolve = 0.35f;
        public float fieldTime = 766.55188f;
        public bool freezeField = true;
        [Range(0,65535)] public int patternSeed;
        public bool randomizeOnReplay = true;
        [Min(0.1f)] public float duration = 1.2f;
        public Vector2 logicalPixels = new Vector2(71,95);
        [Range(0,2)] public float edgeWidth = 1;
        [Range(0,1)] public float tintStrength = 0.6f;
        [Range(0,1)] public float shadowOpacity = 0.3f;
        public bool showShadow = true, showControls = true;
        public Color innerEdge = new Color(55/255f,66/255f,68/255f,1);
        public Color outerEdge = new Color(253/255f,162/255f,0,1);
        public bool Playing { get; private set; }
        int direction;
        readonly System.Random seedGenerator = new System.Random();
        MaterialPropertyBlock properties;

        void OnEnable() { Apply(); }
        void Update()
        {
            if (Application.isPlaying)
            {
                var keys = Keyboard.current;
                if (keys != null && GUI.GetNameOfFocusedControl() != "PatternSeed")
                {
                    if (keys.digit1Key.wasPressedThisFrame) PlayExit();
                    if (keys.digit2Key.wasPressedThisFrame) PlayEntry();
                    if (keys.spaceKey.wasPressedThisFrame) TogglePlayback();
                    if (keys.fKey.wasPressedThisFrame) freezeField = !freezeField;
                    if (keys.rKey.wasPressedThisFrame) SetProgress(0);
                    if (keys.nKey.wasPressedThisFrame) RandomizePattern();
                }
                Tick(Time.unscaledDeltaTime);
            }
            else Apply();
        }

        // Field time and reveal progress have independent clocks for inspection.
        public void Tick(float dt)
        {
            dt = Mathf.Max(0, dt);
            if (!freezeField) fieldTime += dt;
            if (Playing)
            {
                dissolve = Mathf.Clamp01(dissolve + direction * dt / Mathf.Max(0.1f,duration));
                if ((direction > 0 && dissolve >= 1) || (direction < 0 && dissolve <= 0)) Playing = false;
            }
            Apply();
        }

        public void SelectLook(Look value)
        {
            look = value;
            innerEdge = value == Look.OrangeExit ? new Color(55/255f,66/255f,68/255f,1) : new Color(75/255f,194/255f,146/255f,1);
            outerEdge = value == Look.OrangeExit ? new Color(253/255f,162/255f,0,1) : Color.clear;
            fieldTime = value == Look.OrangeExit ? 766.55188f : 286.88257f;
            Playing = false;
            Apply();
        }
        public void PlayExit() { SelectLook(Look.OrangeExit); BeginPlayback(1); }
        public void PlayEntry() { SelectLook(Look.GreenEntry); BeginPlayback(-1); }
        void BeginPlayback(int playbackDirection)
        {
            direction = playbackDirection;
            dissolve = direction > 0 ? 0 : 1;
            if (randomizeOnReplay) RandomizePattern();
            Playing = true;
            Apply();
        }
        public void RandomizePattern()
        {
            int next;
            do { next = seedGenerator.Next(1,65536); } while(next == patternSeed);
            patternSeed = next;
            Apply();
        }
        // Explicit integer mixing keeps seeds deterministic without consuming Unity's
        // global random stream. Seed 0 is the unmodified capture-derived pattern.
        public static Vector4 PatternOffset(int seed)
        {
            if (seed == 0) return Vector4.zero;
            uint x = MixSeed(unchecked((uint)seed));
            uint y = MixSeed(x ^ 0x9e3779b9u);
            return new Vector4((x & 65535u)/65535f*400f-200f,
                (y & 65535u)/65535f*400f-200f,0,0);
        }
        static uint MixSeed(uint value)
        {
            unchecked
            {
                value ^= value >> 16; value *= 0x7feb352du;
                value ^= value >> 15; value *= 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }
        public void SetProgress(float value) { Playing = false; dissolve = Mathf.Clamp01(value); Apply(); }
        public void TogglePlayback()
        {
            if (Playing) { Playing = false; return; }
            direction = look == Look.OrangeExit ? 1 : -1;
            if ((direction > 0 && dissolve >= 1) || (direction < 0 && dissolve <= 0))
            { BeginPlayback(direction); return; }
            Playing = true;
        }

        public void Apply()
        {
            if (!cardRenderer || !shadowRenderer) return;
            properties ??= new MaterialPropertyBlock();
            properties.Clear();
            properties.SetTexture("_BaseMap", look == Look.OrangeExit ? aceTexture : lockTexture);
            properties.SetFloat("_Dissolve", dissolve);
            properties.SetFloat("_FieldTime", fieldTime);
            properties.SetVector("_PatternOffset", PatternOffset(patternSeed));
            properties.SetVector("_PixelSize", new Vector4(Mathf.Max(1,logicalPixels.x),Mathf.Max(1,logicalPixels.y),0,0));
            // Linear project: interpret presets as display RGB. This is a Unity choice,
            // not a claim to reproduce the original game's complete color pipeline.
            // SetVector avoids the additional sRGB conversion performed by SetColor.
            properties.SetVector("_BurnColor1", (Vector4)(QualitySettings.activeColorSpace == ColorSpace.Linear ? innerEdge.linear : innerEdge));
            properties.SetVector("_BurnColor2", (Vector4)(QualitySettings.activeColorSpace == ColorSpace.Linear ? outerEdge.linear : outerEdge));
            properties.SetFloat("_EdgeWidth", edgeWidth);
            properties.SetFloat("_TintStrength", tintStrength);
            properties.SetFloat("_ShadowOpacity", shadowOpacity);
            properties.SetFloat("_ViewMode", (float)view);
            properties.SetFloat("_Shadow", 0);
            cardRenderer.SetPropertyBlock(properties);
            properties.SetFloat("_Shadow", 1);
            shadowRenderer.SetPropertyBlock(properties);
            shadowRenderer.enabled = showShadow && view == View.Color;
        }

        void OnGUI()
        {
            if (!showControls) return;
            var old = GUI.matrix;
            float scale = Mathf.Max(0.25f, Mathf.Min(Screen.width/1280f,Screen.height/720f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUILayout.BeginArea(new Rect(24,24,320,672),GUI.skin.box);
            GUILayout.Label("002  /  CARD DISSOLVE");
            GUILayout.Label("One field. Two directions.");
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1  Orange exit",GUILayout.Height(32))) PlayExit();
            if (GUILayout.Button("2  Green entry",GUILayout.Height(32))) PlayEntry();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Playing ? "Pause [Space]" : "Play [Space]")) TogglePlayback();
            if (GUILayout.Button("Visible [R]")) SetProgress(0);
            if (GUILayout.Button("Hidden")) SetProgress(1);
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label($"Dissolve: {dissolve:F3}   (0 visible / 1 hidden)");
            float next = GUILayout.HorizontalSlider(dissolve,0,1);
            if (!Mathf.Approximately(next,dissolve)) SetProgress(next);
            GUILayout.BeginHorizontal();
            foreach(float d in new[] {0f,0.25f,0.5f,0.75f,1f})
                if (GUILayout.Button(d.ToString("0.##"))) SetProgress(d);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Duration: {duration:F2} s");
            duration = GUILayout.HorizontalSlider(duration,0.2f,3f);
            GUILayout.Space(8);
            randomizeOnReplay = GUILayout.Toggle(randomizeOnReplay,"New pattern each replay");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Seed");
            GUI.SetNextControlName("PatternSeed");
            string seedText = GUILayout.TextField(patternSeed.ToString(),5,GUILayout.Width(70));
            if (int.TryParse(seedText,out int editedSeed)) patternSeed = Mathf.Clamp(editedSeed,0,65535);
            if (GUILayout.Button("New [N]")) RandomizePattern();
            if (GUILayout.Button("Original"))
            { patternSeed = 0; randomizeOnReplay = false; Apply(); }
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            freezeField = GUILayout.Toggle(freezeField,"Freeze field time [F]");
            GUILayout.Label($"Field time: {fieldTime:F3}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Field -0.1")) fieldTime -= 0.1f;
            if (GUILayout.Button("Field +0.1")) fieldTime += 0.1f;
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Inspect");
            view = (View)GUILayout.Toolbar((int)view,new[] {"Color","Mask","Field"});
            showShadow = GUILayout.Toggle(showShadow,"Show synchronized shadow");
            GUILayout.Label($"Edge width: {edgeWidth:F2}");
            edgeWidth = GUILayout.HorizontalSlider(edgeWidth,0,2);
            GUILayout.Label($"Card tint: {tintStrength:F2}");
            tintStrength = GUILayout.HorizontalSlider(tintStrength,0,1);
            GUILayout.Space(12);
            GUILayout.Label("Orange: dark + orange edges\nGreen: one green edge\nField view shows the value before threshold.");
            GUILayout.EndArea();
            GUI.matrix = old;
        }
    }
}
