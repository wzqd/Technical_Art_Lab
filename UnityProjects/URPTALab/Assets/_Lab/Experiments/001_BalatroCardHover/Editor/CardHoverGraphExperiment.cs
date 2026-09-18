using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TechArtLab.Balatro.Editor
{
    public static class CardHoverGraphExperiment
    {
        const string Root = "Assets/_Lab/Experiments/001_BalatroCardHover";
        public const string ScenePath = Root + "/Scenes/001_CardHover_ShaderGraph.unity";

        [MenuItem("TechArtLab/001/Open Shader Graph Comparison")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(ScenePath)) CreateAdditive();
            EditorSceneManager.OpenScene(ScenePath);
        }

        // Additive creation preserves any unsaved work in the user's current scene.
        public static void CreateAdditive()
        {
            if (File.Exists(ScenePath)) throw new InvalidOperationException("Comparison scene already exists.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/CardHoverGraph.shadergraph");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Graph has shader errors.");
            var source = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/CardHover.mat");
            var material = new Material(shader) { name = "CardHoverGraph" };
            material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
            AssetDatabase.CreateAsset(material, Root + "/Materials/CardHoverGraph.mat");
            if (!AssetDatabase.CopyAsset(Root + "/Scenes/001_CardHover_Minimal.unity", ScenePath))
                throw new IOException("Cannot copy Minimal scene.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            var card = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<CardHoverDemo>()).Single();
            card.GetComponent<MeshRenderer>().sharedMaterial = material;
            var comparison = card.gameObject.AddComponent<CardHoverComparison>();
            comparison.hlslMaterial = source;
            comparison.shaderGraphMaterial = material;
            EditorSceneManager.SaveScene(scene);
            SceneManager.SetActiveScene(previous);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("TechArtLab/001/Capture HLSL and Graph Comparison")]
        public static void Capture()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play in the comparison scene first.");
            var comparison = UnityEngine.Object.FindAnyObjectByType<CardHoverComparison>();
            if (!comparison) throw new InvalidOperationException("Comparison component not found.");
            var card = comparison.GetComponent<CardHoverDemo>();
            var renderer = card.GetComponent<MeshRenderer>();
            var camera = card.viewCamera;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var oldMaterial = renderer.sharedMaterial;
            int oldLayer = card.gameObject.layer, oldMask = camera.cullingMask;
            bool oldFixed = card.fixedInput;
            var oldUV = card.fixedCardUV;
            float oldHover = card.fixedHover, oldStrength = card.strength;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../References/001_BalatroCardHover/analysis/shadergraph"));
            Directory.CreateDirectory(output);
            var rt = new RenderTexture(1280, 720, 24);
            var png = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                // Isolate this card when the comparison is loaded additively.
                card.gameObject.layer = 31;
                camera.cullingMask = 1 << 31;
                string[] names = { "idle", "center", "top_left", "bottom_right", "zero_strength", "strong_top_left" };
                Vector2[] uvs = { Vector2.one * .5f, Vector2.one * .5f, new Vector2(.05f,.95f), new Vector2(.95f,.05f), new Vector2(.05f,.95f), new Vector2(.05f,.95f) };
                foreach (bool graph in new[] { false, true })
                {
                    renderer.sharedMaterial = graph ? comparison.shaderGraphMaterial : comparison.hlslMaterial;
                    for (int i = 0; i < names.Length; i++)
                    {
                        card.strength = i == 4 ? 0 : i == 5 ? 3 : 1;
                        card.SetState(true, uvs[i], i == 0 ? 0 : 1);
                        card.Apply(0);
                        camera.Render();
                        RenderTexture.active = rt;
                        png.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                        png.Apply();
                        File.WriteAllBytes(Path.Combine(output, (graph ? "graph_" : "hlsl_") + names[i] + ".png"), png.EncodeToPNG());
                    }
                    if (ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)) throw new Exception("Shader compilation failed.");
                }
                Debug.Log("HLSL / Shader Graph comparison saved: " + output);
            }
            finally
            {
                renderer.sharedMaterial = oldMaterial;
                card.gameObject.layer = oldLayer;
                camera.cullingMask = oldMask;
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(png);
                card.strength = oldStrength;
                card.SetState(oldFixed, oldUV, oldHover);
                card.Apply(0);
            }
        }
    }
}
