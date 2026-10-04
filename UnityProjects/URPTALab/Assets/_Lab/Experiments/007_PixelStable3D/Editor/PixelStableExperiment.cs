using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TechArtLab.Pixelated;
using TechArtLab.Pixelated.Editor;

namespace TechArtLab.PixelStable.Editor
{
    public static class PixelStableExperiment
    {
        public const string Root = "Assets/_Lab/Experiments/007_PixelStable3D";
        public const string ScenePath = Root + "/Scenes/007_PixelStable3D_Minimal.unity";
        [MenuItem("TechArtLab/007/Create or Open Minimal")]
        public static void Open()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            // Copy through Unity: 006 stays independently editable, its material and mesh
            // assets are deliberately shared references; no speculative Core framework.
            if (!AssetDatabase.CopyAsset(LowResPixelExperiment.ScenePath, ScenePath))
                throw new Exception("Could not copy the 006 teaching scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var old = UnityEngine.Object.FindAnyObjectByType<LowResPixelDemo>();
            var camera = old.sceneCamera;
            UnityEngine.Object.DestroyImmediate(old.gameObject);
            camera.name = "A / Continuous Camera";
            var snapped = UnityEngine.Object.Instantiate(camera); snapped.name = "BC / Snapped Camera"; snapped.tag = "Untagged";
            var demo = new GameObject("007 Controls").AddComponent<PixelStableDemo>();
            demo.continuousCamera = camera; demo.snappedCamera = snapped; demo.sun = RenderSettings.sun;
            demo.sun.shadows = LightShadows.None;
            var additions = new GameObject("007 / Repeated stone details").transform;
            var stone = AssetDatabase.LoadAssetAtPath<Material>(LowResPixelExperiment.Root + "/Materials/Ivory.mat");
            var teal = AssetDatabase.LoadAssetAtPath<Material>(LowResPixelExperiment.Root + "/Materials/Teal.mat");
            for (int i = 0; i < 11; i++)
                Box(additions, "Paving " + i, new Vector3(-3.3f + i * .64f, .035f, 2.35f), new Vector3(.57f, .08f, .65f), stone);
            for (int i = 0; i < 3; i++)
            {
                float x = -3.45f, z = -1.6f + i * 1.65f;
                Box(additions, "Pillar base " + i, new Vector3(x, .1f, z), new Vector3(.62f, .2f, .62f), stone);
                Box(additions, "Pillar " + i, new Vector3(x, .75f, z), new Vector3(.32f, 1.3f, .32f), teal);
                Box(additions, "Pillar cap " + i, new Vector3(x, 1.45f, z), new Vector3(.56f, .14f, .56f), stone);
            }
            demo.ApplyPose();
            EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets();
            Selection.activeGameObject = demo.gameObject;
        }
        static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent); go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }
    }
}
