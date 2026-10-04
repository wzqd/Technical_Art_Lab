using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace TechArtLab.LoFi
{
    public sealed class LoFiDemo : MonoBehaviour
    {
        [Serializable] public class Part { public MeshFilter filter; public Renderer renderer; public int kind = -1, role; }
        public Camera sceneCamera;
        public Light sun;
        public Part[] parts;
        public Mesh[] variants; // kind * 6 + subdivision * 2 + (smooth ? 1 : 0)
        [Range(0,2)] public int subdivision;
        public bool smooth, drive = true, showControls = true;
        [Range(0,2)] public int palette = 2, shadowMode = 1;
        [Range(0,3)] public int resolution, debugView;
        public float yaw = 40, pitch = 32, orthoSize = 3.75f, lightYaw = -35, lightPitch = 48;
        public RenderTexture Target { get; private set; }
        public Rect ImageRect { get; private set; }
        public float PixelScale { get; private set; }
        public int TriangleCount { get; private set; }
        public int VertexCount { get; private set; }
        public int PaletteSize => new[] {4,6,8}[Mathf.Clamp(palette,0,2)];
        static readonly string[] DetailLabels = {"1 / 20 faces", "2 / 80 faces", "3 / 320 faces"};
        static readonly string[] NormalLabels = {"FLAT", "SMOOTH"}, PaletteLabels = {"4 colors", "6 colors", "8 colors"};
        static readonly string[] ResolutionLabels = {"Native", "640 x 360", "320 x 180", "160 x 90"};
        static readonly string[] ShadowLabels = {"Off", "Hard", "Soft"}, ViewLabels = {"Beauty", "Albedo", "Normals", "Silhouette"};
        // Material-role palettes: this limits base colors, not final shaded image colors.
        static readonly Color[] Eight = {new(.34f,.22f,.15f),new(.49f,.60f,.29f),new(.43f,.30f,.19f),
            new(.19f,.37f,.26f),new(.32f,.52f,.31f),new(.57f,.68f,.36f),new(.53f,.59f,.59f),new(.79f,.73f,.57f)};
        static readonly int[][] Maps = {new[]{0,1,0,4,4,4,6,6}, new[]{0,1,2,3,5,5,6,6}, new[]{0,1,2,3,4,5,6,7}};
        public Color RoleColor(int role) => Eight[Maps[Mathf.Clamp(palette,0,2)][Mathf.Clamp(role,0,7)]];
        Canvas canvas; RawImage image;
        MaterialPropertyBlock block;
        RenderTexture previousTarget;
        bool presenting, previousBackground;
        int lastDetail = -1, lastNormal = -1, lastPalette = -1, lastView = -1;
        [NonSerialized] GUIStyle heading, label, small, button;
        float UiScale => Mathf.Max(.5f, Mathf.Min(Screen.width/1280f, Screen.height/720f));

        void OnEnable()
        {
            if (!Application.isPlaying || !sceneCamera) return;
            heading = label = small = button = null;
            previousTarget = sceneCamera.targetTexture; previousBackground = Application.runInBackground;
            Application.runInBackground = true; presenting = true;
            canvas = new GameObject("008 / Presentation", typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -100;
            image = new GameObject("Lo-fi scene", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(canvas.transform, false); image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = Vector2.zero;
            sceneCamera.allowHDR = false; sceneCamera.allowMSAA = false; sceneCamera.allowDynamicResolution = false;
            var data = sceneCamera.GetUniversalAdditionalCameraData(); data.antialiasing = AntialiasingMode.None; data.renderPostProcessing = false;
            ApplySettings(true); RefreshOutput();
        }
        public void ApplySettings(bool force = false)
        {
            subdivision = Mathf.Clamp(subdivision,0,2); palette = Mathf.Clamp(palette,0,2); debugView = Mathf.Clamp(debugView,0,3);
            if (force || lastDetail != subdivision || lastNormal != (smooth ? 1 : 0) || lastPalette != palette || lastView != debugView)
            {
                block ??= new MaterialPropertyBlock(); TriangleCount = VertexCount = 0;
                foreach (var part in parts)
                {
                    if (part.kind >= 0) part.filter.sharedMesh = variants[part.kind*6 + subdivision*2 + (smooth ? 1 : 0)];
                    var mesh = part.filter.sharedMesh;
                    TriangleCount += (int)mesh.GetIndexCount(0)/3; VertexCount += mesh.vertexCount;
                    block.Clear(); block.SetColor("_BaseColor", RoleColor(part.role)); block.SetFloat("_DebugView",debugView);
                    part.renderer.SetPropertyBlock(block);
                }
                lastDetail = subdivision; lastNormal = smooth ? 1 : 0; lastPalette = palette; lastView = debugView;
            }
            sun.transform.rotation = Quaternion.Euler(lightPitch,lightYaw,0);
            sun.shadows = shadowMode == 0 ? LightShadows.None : shadowMode == 1 ? LightShadows.Hard : LightShadows.Soft;
            ApplyPose();
        }
        public void ApplyPose()
        {
            pitch = Mathf.Clamp(pitch,12,75); orthoSize = Mathf.Clamp(orthoSize,2,7);
            var orientation = Quaternion.Euler(pitch,yaw,0);
            sceneCamera.transform.SetPositionAndRotation(new Vector3(0,.9f,0) - orientation*Vector3.forward*14,orientation);
            sceneCamera.orthographic = true; sceneCamera.orthographicSize = orthoSize; sceneCamera.aspect = 16f/9;
        }
        public static Rect Fit(Rect available,int width,int height,out float scale)
        {
            scale = Mathf.Min(available.width/width,available.height/height);
            if (scale >= 1) scale = Mathf.Floor(scale);
            return new Rect(Mathf.Floor(available.center.x-width*scale/2),Mathf.Floor(available.center.y-height*scale/2),width*scale,height*scale);
        }
        public void SetTargetSize(int width,int height)
        {
            if (!Target || Target.width != width || Target.height != height)
            {
                ReleaseTarget();
                Target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32) {name="008 / Scene",filterMode=FilterMode.Point,
                    antiAliasing=1,useMipMap=false,autoGenerateMips=false,wrapMode=TextureWrapMode.Clamp,useDynamicScale=false};
                Target.Create();
            }
            sceneCamera.targetTexture = Target; if (image) image.texture = Target;
        }
        public void RefreshOutput()
        {
            if (!image) return;
            float u = UiScale; resolution = Mathf.Clamp(resolution,0,3);
            var area = new Rect(306*u,76*u,Mathf.Max(16,Screen.width-326*u),Mathf.Max(9,Screen.height-148*u));
            int units = resolution == 0 ? Mathf.Max(1,Mathf.FloorToInt(Mathf.Min(area.width/16,area.height/9))) : new[]{0,40,20,10}[resolution];
            SetTargetSize(units*16,units*9);
            ImageRect = Fit(area,Target.width,Target.height,out float scale); PixelScale = scale;
            image.rectTransform.anchoredPosition = ImageRect.position; image.rectTransform.sizeDelta = ImageRect.size;
        }
        public void ResetDemo()
        {
            subdivision=0; smooth=false; palette=2; shadowMode=1; resolution=0; debugView=0;
            yaw=40; pitch=32; orthoSize=3.75f; lightYaw=-35; lightPitch=48; ApplySettings(true); RefreshOutput();
        }
        void Update()
        {
            if (!drive) return;
            var k = Keyboard.current;
            if (k != null && Application.isFocused)
            {
                if(k.digit1Key.wasPressedThisFrame) subdivision=0;
                if(k.digit2Key.wasPressedThisFrame) subdivision=1;
                if(k.digit3Key.wasPressedThisFrame) subdivision=2;
                if(k.nKey.wasPressedThisFrame) smooth=!smooth;
                if(k.pKey.wasPressedThisFrame) palette=(palette+1)%3;
                if(k.vKey.wasPressedThisFrame) debugView=(debugView+1)%4;
                if(k.lKey.wasPressedThisFrame) shadowMode=(shadowMode+1)%3;
                if(k.rKey.wasPressedThisFrame) ResetDemo();
                if(k.hKey.wasPressedThisFrame) showControls=!showControls;
            }
            var m = Mouse.current;
            if(m != null && Application.isFocused && ImageRect.Contains(m.position.ReadValue()))
            {
                if(m.rightButton.isPressed) {var delta=m.delta.ReadValue(); yaw+=delta.x*.2f; pitch-=delta.y*.2f;}
                orthoSize*=Mathf.Exp(-m.scroll.ReadValue().y*.001f);
            }
            ApplySettings(); RefreshOutput();
        }
        void Styles()
        {
            if(heading != null && heading.fontSize==22 && button != null && button.fixedHeight==24) return;
            heading=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold};
            label=new GUIStyle(GUI.skin.label){fontSize=13,wordWrap=true};
            heading.normal.textColor=label.normal.textColor=new Color(.85f,.90f,.88f);
            small=new GUIStyle(label){fontSize=11}; button=new GUIStyle(GUI.skin.button){fontSize=12,fixedHeight=24};
        }
        void OnGUI()
        {
            if(!showControls || !Target) return; Styles(); float u=UiScale; var old=GUI.matrix;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*u);
            var color=GUI.color; GUI.color=new Color(.045f,.065f,.06f); GUI.DrawTexture(new Rect(12,12,276,696),Texture2D.whiteTexture); GUI.color=color;
            GUILayout.BeginArea(new Rect(26,24,248,670));
            GUILayout.Label("008 / LO-FI LAB",heading); GUILayout.Space(4);
            GUILayout.Label("Canopy / rock triangles per mesh",label); subdivision=GUILayout.SelectionGrid(subdivision,DetailLabels,3,button);
            GUILayout.Space(4); GUILayout.Label("N / Normals (same geometry)",label); smooth=GUILayout.SelectionGrid(smooth?1:0,NormalLabels,2,button)==1;
            GUILayout.Space(4); GUILayout.Label("P / Material base-color palette",label); palette=GUILayout.SelectionGrid(palette,PaletteLabels,3,button);
            var swatches=GUILayoutUtility.GetRect(248,16); int slot=0;
            for(int role=0;role<8;role++) {bool first=true;for(int j=0;j<role;j++)if(RoleColor(j)==RoleColor(role))first=false;
                if(first){GUI.color=RoleColor(role);GUI.DrawTexture(new Rect(swatches.x+slot*30,swatches.y,26,12),Texture2D.whiteTexture);slot++;}}
            GUI.color=color; GUILayout.Space(4); GUILayout.Label("Output / Point sampling",label); resolution=GUILayout.SelectionGrid(resolution,ResolutionLabels,2,button);
            GUILayout.Space(4); GUILayout.Label($"Light azimuth  {lightYaw:F0} deg",small); lightYaw=GUILayout.HorizontalSlider(lightYaw,-120,120);
            GUILayout.Label($"Light elevation  {lightPitch:F0} deg",small); lightPitch=GUILayout.HorizontalSlider(lightPitch,20,80);
            GUILayout.Label("L / Shadows",label); shadowMode=GUILayout.SelectionGrid(shadowMode,ShadowLabels,3,button);
            GUILayout.Space(4); GUILayout.Label("V / Diagnostic view",label); debugView=GUILayout.SelectionGrid(debugView,ViewLabels,2,button);
            GUILayout.Space(6); if(GUILayout.Button("R / Reset baseline",button)) ResetDemo();
            GUILayout.Space(4); GUILayout.Label($"{TriangleCount:N0} triangles / {VertexCount:N0} vertices\n{Target.width} x {Target.height}  /  {PixelScale:0.##}x\n{PaletteSize} base colors; lighting stays continuous",small);
            GUILayout.Space(4); GUILayout.Label("RMB: orbit  |  Wheel: zoom\nH: hide UI\nChange one variable at a time.",small); GUILayout.EndArea();
            GUI.Label(new Rect(310,24,900,32),"SMALL WOODLAND / CONTROLLED COMPARISON",heading);
            GUI.Label(new Rect(310,Screen.height/u-58,Screen.width/u-330,50),PixelScale<1
                ? "Fractional downscale: enlarge Game view for equal pixel blocks."
                : "Flat / Smooth changes lighting, not the silhouette.\nFewer base colors does not quantize shaded colors. Native output isolates shape and shading.",small);
            GUI.matrix=old;
        }
        void ReleaseTarget()
        {
            if(sceneCamera)sceneCamera.targetTexture=previousTarget; if(image)image.texture=null;
            if(Target){Target.Release();Destroy(Target);} Target=null;
        }
        void OnDisable()
        {
            if(!presenting)return; presenting=false; ReleaseTarget(); Application.runInBackground=previousBackground;
            if(canvas)Destroy(canvas.gameObject); canvas=null; image=null; heading=label=small=button=null;
        }
    }
}
