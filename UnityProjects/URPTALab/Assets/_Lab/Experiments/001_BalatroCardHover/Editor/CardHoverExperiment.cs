using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TechArtLab.Balatro.Editor
{
    public static class CardHoverExperiment
    {
        const string Root = "Assets/_Lab/Experiments/001_BalatroCardHover";
        const string ScenePath = Root + "/Scenes/001_CardHover_Minimal.unity";

        [MenuItem("TechArtLab/001/Create or Open Minimal %#F8")]
        public static void CreateOrOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            foreach (string folder in new[] { "Scenes", "Materials", "Textures", "Models" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder);
            var shader = Shader.Find("TechArtLab/001/CardHover");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Card shader missing or has errors.");
            var mesh = new Mesh { name = "FourVertexQuad" };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0), new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            // Extra bounds prevent CPU frustum culling from cutting off GPU deformation.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(3, 3, 1));
            AssetDatabase.CreateAsset(mesh, Root + "/Models/FourVertexQuad.asset");
            var texture = new Texture2D(142, 190, TextureFormat.RGBA32, false);
            for (int y = 0; y < texture.height; y++) for (int x = 0; x < texture.width; x++)
            {
                bool edge = x < 5 || x >= 137 || y < 5 || y >= 185;
                bool grid = x % 18 < 2 || y % 18 < 2;
                bool diamond = Mathf.Abs(x - 71) + Mathf.Abs(y - 95) < 32;
                Color c = edge ? new Color(.14f,.2f,.25f) : grid ? new Color(.65f,.72f,.7f) : new Color(.94f,.91f,.8f);
                if (diamond) c = new Color(.18f,.56f,.48f);
                if (x < 22 && y > 162) c = new Color(.82f,.25f,.22f);
                if (x > 119 && y < 27) c = new Color(.2f,.4f,.75f);
                texture.SetPixel(x, y, c);
            }
            texture.Apply();
            string texturePath = Root + "/Textures/TestCard.png";
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.mipmapEnabled = false; importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var material = new Material(shader) { name = "CardHover" };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            AssetDatabase.CreateAsset(material, Root + "/Materials/CardHover.mat");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 3.5f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.075f,.095f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 100;
            camera.allowHDR = false; camera.allowMSAA = false;
            var card = new GameObject("Card - four vertices", typeof(MeshFilter), typeof(MeshRenderer), typeof(CardHoverDemo));
            card.transform.localScale = new Vector3(2.84f, 3.8f, 1);
            card.GetComponent<MeshFilter>().sharedMesh = mesh;
            card.GetComponent<MeshRenderer>().sharedMaterial = material;
            card.GetComponent<CardHoverDemo>().viewCamera = camera;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = card;
            Debug.Log("001 Minimal created. Enter Play; keys 0-4 select deterministic states.");
        }

        [MenuItem("TechArtLab/001/Capture Fixed States %#F9")]
        public static void Capture()
        {
            var card = UnityEngine.Object.FindAnyObjectByType<CardHoverDemo>();
            if (!card || !Application.isPlaying) throw new Exception("Open Minimal and enter Play first.");
            var shader = card.GetComponent<MeshRenderer>().sharedMaterial.shader;
            if (ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader compilation failed.");
            var mesh = card.GetComponent<MeshFilter>().sharedMesh;
            if (mesh.vertexCount != 4 || mesh.triangles.Length != 6) throw new Exception("Unexpected geometry.");
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../References/001_BalatroCardHover/analysis/unity"));
            Directory.CreateDirectory(output);
            var camera = card.viewCamera;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            bool oldFixed = card.fixedInput; Vector2 oldUV = card.fixedCardUV;
            float oldHover = card.fixedHover, oldStrength = card.strength;
            var rt = new RenderTexture(1280, 720, 24);
            try
            {
                camera.targetTexture = rt;
                string[] names = { "idle", "center", "top_left", "bottom_right", "zero_strength" };
                Vector2[] uv = { Vector2.one*.5f, Vector2.one*.5f, new Vector2(.05f,.95f), new Vector2(.95f,.05f), new Vector2(.05f,.95f) };
                for (int i = 0; i < names.Length; i++)
                {
                    card.strength = i == 4 ? 0 : 1;
                    card.SetState(true, uv[i], i == 0 ? 0 : 1);
                    card.Apply(0);
                    camera.Render(); RenderTexture.active = rt;
                    var png = new Texture2D(1280,720,TextureFormat.RGB24,false);
                    png.ReadPixels(new Rect(0,0,1280,720),0,0); png.Apply();
                    File.WriteAllBytes(Path.Combine(output,names[i]+".png"),png.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(png);
                }
                File.WriteAllText(Path.Combine(output,"validation.txt"), "Unity " + Application.unityVersion + "\nShader errors: false\nVertices: 4; indices: 6\nCamera: orthographic; 1280x720; size 3.5\nStrength: 1 (zero_strength: 0); normalized scale: 0.8 card heights\nCaptured idle, center, top_left, bottom_right, zero_strength. Inspect PNGs for visual assertions.\n");
                Debug.Log("001 fixed-state captures: " + output);
            }
            finally
            {
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                card.strength = oldStrength; card.SetState(oldFixed,oldUV,oldHover); card.Apply(0);
            }
        }
    }
}
