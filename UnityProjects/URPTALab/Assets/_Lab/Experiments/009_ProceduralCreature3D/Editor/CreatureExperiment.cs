using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TechArtLab.Creature.Editor
{
    public static class CreatureExperiment
    {
        public const string Root = "Assets/_Lab/Experiments/009_ProceduralCreature3D";
        public const string ScenePath = Root + "/Scenes/009_ProceduralCreature3D_Minimal.unity";
        [MenuItem("TechArtLab/009/Create or Open Minimal")]
        public static void Open()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null; RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
            var ground = Material("Ground", new Color(.22f, .29f, .28f));
            var grid = Material("Grid", new Color(.33f, .42f, .39f));
            var shell = Material("Shell", new Color(.84f, .62f, .28f));
            var metal = Material("Leg metal", new Color(.40f, .52f, .55f));
            var dark = Material("Joints", new Color(.10f, .17f, .19f));
            var green = Material("Planted feet", new Color(.26f, .81f, .63f));
            var orange = Material("Swing feet", new Color(1, .53f, .13f));
            var target = Material("Landing rings", new Color(.97f, .71f, .29f), true);
            var rest = Material("Rest crosses", new Color(.39f, .70f, .92f), true);
            var environment = new GameObject("009 / Ground grid").transform;
            Add("Ground", PrimitiveType.Cube, new(0, -.15f, 0), new(11, .3f, 11), ground, environment);
            for (int i = -10; i <= 10; i++)
            {
                Add("Grid X " + i, PrimitiveType.Cube, new(0, .003f, i * .5f), new(10, .006f, .012f), grid, environment);
                Add("Grid Z " + i, PrimitiveType.Cube, new(i * .5f, .003f, 0), new(.012f, .006f, 10), grid, environment);
            }
            var robot = new GameObject("009 / Four-leg explorer").transform;
            var body = new GameObject("Body / +Z forward").transform; body.SetParent(robot);
            Add("Chassis", PrimitiveType.Cube, Vector3.zero, new(.98f, .33f, 1.38f), shell, body);
            Add("Top panel", PrimitiveType.Cube, new(0, .22f, -.05f), new(.72f, .12f, .83f), metal, body);
            Add("Visor", PrimitiveType.Cube, new(0, .03f, .704f), new(.75f, .21f, .035f), dark, body);
            for (int sign = -1; sign <= 1; sign += 2)
                Add("Eye " + sign, PrimitiveType.Cube, new(sign * .20f, .04f, .731f), new(.12f, .07f, .027f), rest, body);
            var demo = new GameObject("009 Controls").AddComponent<CreatureDemo>();
            demo.body = body; demo.stanceMaterial = green; demo.swingMaterial = orange; demo.legs = new CreatureDemo.LegView[4];
            for (int i = 0; i < 4; i++)
            {
                var group = new GameObject(new[] { "Front left / A", "Front right / B", "Rear left / B", "Rear right / A" }[i]).transform;
                group.SetParent(robot); var view = new CreatureDemo.LegView(); demo.legs[i] = view;
                view.upper = Add("Upper / 0.85 m", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, shell, group);
                view.lower = Add("Lower / 0.85 m", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, metal, group);
                view.hip = Add("Hip", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .23f, dark, group);
                view.knee = Add("Knee", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .23f, dark, group);
                view.foot = Add("Foot", PrimitiveType.Cube, Vector3.zero, new(.25f, .13f, .31f), green, group);
                view.footRenderer = view.foot.GetComponent<Renderer>();
                view.target = new GameObject("Landing anchor (world)").transform; view.target.SetParent(group);
                var ring = view.target.gameObject.AddComponent<LineRenderer>(); ring.sharedMaterial = target;
                ring.useWorldSpace = false; ring.loop = true; ring.positionCount = 32; ring.widthMultiplier = .018f;
                ring.shadowCastingMode = ShadowCastingMode.Off; ring.receiveShadows = false;
                for (int j = 0; j < 32; j++) ring.SetPosition(j, new Vector3(Mathf.Cos(j * Mathf.PI / 16), 0, Mathf.Sin(j * Mathf.PI / 16)) * .23f);
                view.rest = new GameObject("Rest cross (body-relative)").transform; view.rest.SetParent(group);
                Add("Cross X", PrimitiveType.Cube, Vector3.zero, new(.14f, .009f, .025f), rest, view.rest);
                Add("Cross Z", PrimitiveType.Cube, Vector3.zero, new(.025f, .009f, .14f), rest, view.rest);
            }
            var sun = new GameObject("Directional Light").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0); sun.color = new Color(1, .96f, .89f); sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft; RenderSettings.sun = sun;
            var background = new GameObject("Presentation Camera").AddComponent<Camera>(); background.cullingMask = 0; background.depth = -10;
            background.clearFlags = CameraClearFlags.SolidColor; background.backgroundColor = new Color(.022f, .036f, .041f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; demo.sceneCamera = camera;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.10f, .16f, .17f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 60; camera.allowHDR = false; camera.allowMSAA = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            demo.ResetState(); EditorSceneManager.SaveScene(scene, ScenePath); AssetDatabase.SaveAssets(); Selection.activeGameObject = demo.gameObject;
        }
        static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material) return material;
            // 008's readable Lambert material is shared directly; 009 studies motion, not a new shader.
            material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "TechArtLab/008/LoFiDiffuse"));
            material.SetColor("_BaseColor", color); if (!unlit) material.SetFloat("_Ambient", .38f);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static Transform Add(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
        }
    }
}
