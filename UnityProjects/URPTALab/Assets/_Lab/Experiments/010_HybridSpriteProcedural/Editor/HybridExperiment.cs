using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TechArtLab.Hybrid.Editor
{
    public static class HybridExperiment
    {
        public const string Root = "Assets/_Lab/Experiments/010_HybridSpriteProcedural";
        public const string ScenePath = Root + "/Scenes/010_HybridSpriteProcedural_Minimal.unity";
        [MenuItem("TechArtLab/010/Create or Open Minimal")]
        public static void Open()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null; RenderSettings.fog = false;
            var frames = new Sprite[3];
            for (int i = 0; i < 3; i++) frames[i] = PaintRobot(i);
            var whiteTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var whites = new Color[16]; for (int i = 0; i < 16; i++) whites[i] = Color.white;
            whiteTexture.SetPixels(whites); whiteTexture.Apply();
            Sprite white = SaveSprite(whiteTexture, "White", 4, new Vector2(.5f, .5f));
            var sprites = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(sprites, Root + "/Materials/Pixel sprites.mat");
            var rigidMat = Ribbon("Rigid scarf", new Color(.50f, .62f, .69f));
            var dynamicMat = Ribbon("Dynamic scarf", new Color(.96f, .34f, .21f));
            var parent = new GameObject("010 / Comparison stage").transform;
            foreach (float lane in new[] { HybridDemo.UpperLane, HybridDemo.LowerLane })
            {
                SpriteObject("Lane floor", white, sprites, new Vector3(0, lane - .08f, 0), new Vector3(10, .13f, 1), new Color(.15f, .23f, .28f), -10, parent);
                for (int i = -8; i <= 8; i++) SpriteObject("Position tick " + i, white, sprites, new Vector3(i * .5f, lane - .18f, 0), new Vector3(.018f, .12f, 1), new Color(.26f, .36f, .42f), -9, parent);
            }
            var demo = new GameObject("010 Controls").AddComponent<HybridDemo>(); demo.frames = frames;
            demo.rigidRobot = SpriteObject("A / authored robot", frames[0], sprites, Vector3.zero, Vector3.one, Color.white, 10, parent);
            demo.dynamicRobot = SpriteObject("B / authored robot", frames[0], sprites, Vector3.zero, Vector3.one, Color.white, 10, parent);
            demo.rigidScarf = Line("A / fixed local shape", rigidMat, parent);
            demo.dynamicScarf = Line("B / simulated world nodes", dynamicMat, parent);
            demo.markers = new Transform[13];
            for (int i = 0; i < 13; i++) demo.markers[i] = SpriteObject("Node " + i, white, sprites, Vector3.zero, Vector3.one * (i == 0 ? .09f : .045f),
                i == 0 ? new Color(.40f, .92f, .82f) : new Color(1, .82f, .49f), 15, parent).transform;
            var background = new GameObject("Presentation Camera").AddComponent<Camera>();
            background.cullingMask = 0; background.depth = -10; background.clearFlags = CameraClearFlags.SolidColor;
            background.backgroundColor = new Color(.025f, .035f, .06f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, .25f, -10); camera.orthographic = true; camera.orthographicSize = 3.6f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f, .095f, .14f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 30; camera.allowHDR = false; camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false; demo.sceneCamera = camera;
            demo.ResetState(); EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets(); Selection.activeGameObject = demo.gameObject;
        }
        static Material Ribbon(string name, Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1); mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Cull", 0);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(mat, Root + "/Materials/" + name + ".mat"); return mat;
        }
        static LineRenderer Line(string name, Material material, Transform parent)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent);
            line.useWorldSpace = true; line.sharedMaterial = material; line.sortingOrder = 12;
            line.widthCurve = new AnimationCurve(new Keyframe(0, .16f), new Keyframe(.8f, .13f), new Keyframe(1, .065f));
            line.numCapVertices = 0; line.numCornerVertices = 0; line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; return line;
        }
        static SpriteRenderer SpriteObject(string name, Sprite sprite, Material material, Vector3 pos, Vector3 scale, Color color, int order, Transform parent)
        {
            var r = new GameObject(name).AddComponent<SpriteRenderer>(); r.transform.SetParent(parent); r.transform.position = pos;
            r.transform.localScale = scale; r.sprite = sprite; r.sharedMaterial = material; r.color = color; r.sortingOrder = order; return r;
        }
        static Sprite PaintRobot(int frame)
        {
            // Hand-authored pixel rectangles: three discrete drawings, not a 3D render or an AI image.
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false); var pixels = new Color32[64 * 64];
            Color32 ink = new(24, 35, 47, 255), gold = new(207, 151, 63, 255), light = new(248, 202, 102, 255),
                steel = new(99, 139, 152, 255), shade = new(55, 83, 103, 255), eye = new(98, 219, 226, 255), scarf = new(242, 87, 53, 255);
            void Fill(int x, int y, int width, int height, Color32 color)
            { for (int v = y; v < y + height; v++) for (int u = x; u < x + width; u++) if (u >= 0 && u < 64 && v >= 0 && v < 64) pixels[v * 64 + u] = color; }
            void Box(int x, int y, int w, int h, Color32 color) { Fill(x, y, w, h, ink); Fill(x + 2, y + 2, w - 4, h - 4, color); }
            int stride = frame == 0 ? 0 : frame == 1 ? 4 : -4;
            Box(22 - stride, 3, 10, 15, shade); Box(34 + stride, 3, 10, 15, steel);
            Box(20 - stride, 0, 15, 7, shade); Box(33 + stride, 0, 16, 7, steel);
            Box(20, 16, 27, 23, gold); Fill(23, 33, 20, 3, light);
            Box(15, 20, 8, 19, shade); Fill(17, 30, 4, 6, steel);
            Box(25, 23, 15, 8, shade); Fill(28, 26, 3, 2, eye); Fill(34, 26, 3, 2, eye);
            Box(37 - stride / 2, 18, 9, 16, steel);
            Box(22, 41, 27, 20, gold); Fill(25, 56, 20, 3, light);
            Box(31, 45, 21, 10, shade); Fill(35, 49, 13, 3, eye);
            Fill(21, 38, 27, 5, scarf); Fill(24, 41, 19, 1, light);
            Fill(25, 60, 3, 4, ink); Fill(24, 62, 5, 2, eye);
            texture.SetPixels32(pixels); texture.Apply(); return SaveSprite(texture, "Robot_" + frame, 32, new Vector2(.5f, 0));
        }
        static Sprite SaveSprite(Texture2D texture, string name, int ppu, Vector2 pivot)
        {
            string path = Root + "/Sprites/" + name + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = ppu;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings); settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot; settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
