using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TechArtLab.Pixelated.Editor
{
    public static class LowResPixelExperiment
    {
        public const string Root = "Assets/_Lab/Experiments/006_LowRes3DPixelated";
        public const string ScenePath = Root + "/Scenes/006_LowRes3D_Minimal.unity";
        [MenuItem("TechArtLab/006/Create or Open Minimal")]
        public static void Open()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            Build();
        }

        static Material Material(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("TechArtLab/006/SimpleDiffuse")) { name = name };
                mat.SetColor("_BaseColor", color); mat.SetFloat("_Ambient", .32f);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }
        static GameObject Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent); go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static Mesh MakeRamp()
        {
            string path = Root + "/Meshes/Ramp.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing) return existing;
            var a = new Vector3(-.7f,0,-.8f); var b = new Vector3(.7f,0,-.8f);
            var c = new Vector3(-.7f,0,.8f); var d = new Vector3(.7f,0,.8f);
            var e = new Vector3(-.7f,1.4f,.8f); var f = new Vector3(.7f,1.4f,.8f);
            // Split vertices per triangle so the wedge has intentionally flat normals.
            var verts = new[] { a,e,b, b,e,f, a,c,e, b,f,d, c,d,e, d,f,e, a,b,c, b,d,c };
            var indices = new int[verts.Length]; for (int i=0;i<indices.Length;i++) indices[i]=i;
            var mesh = new Mesh { name="Ramp", vertices=verts, triangles=indices };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,path); return mesh;
        }
        static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null; RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.gray;
            var ground = Material("Ground", new Color(.20f,.27f,.31f));
            var stone = Material("Ivory", new Color(.83f,.78f,.63f));
            var teal = Material("Teal", new Color(.22f,.67f,.61f));
            var orange = Material("Coral", new Color(.92f,.40f,.26f));
            var yellow = Material("Ochre", new Color(.92f,.65f,.24f));
            var blue = Material("Blue", new Color(.31f,.47f,.78f));
            var platform = new GameObject("Test bench").transform;
            Shape(platform,"Ground slab",PrimitiveType.Cube,new Vector3(0,-.16f,0),new Vector3(8,.3f,5.8f),ground);
            Shape(platform,"01 / Cube",PrimitiveType.Cube,new Vector3(-2.3f,.70f,.4f),Vector3.one*1.4f,teal);
            Shape(platform,"02 / Sphere",PrimitiveType.Sphere,new Vector3(0,.8f,.5f),Vector3.one*1.6f,orange);
            var ramp = new GameObject("03 / Ramp",typeof(MeshFilter),typeof(MeshRenderer));
            ramp.transform.SetParent(platform); ramp.transform.position = new Vector3(2.2f,0,.5f);
            ramp.GetComponent<MeshFilter>().sharedMesh = MakeRamp(); ramp.GetComponent<MeshRenderer>().sharedMaterial = yellow;
            for(int i=0;i<5;i++)
                Shape(platform,"04 / Step "+(i+1),PrimitiveType.Cube,new Vector3(-1.15f+i*.48f,.12f*(i+1),-1.75f),new Vector3(.44f,.24f*(i+1),.9f),stone);
            for(int i=0;i<4;i++)
            {
                float thickness = new[]{.16f,.08f,.04f,.02f}[i];
                Shape(platform,"05 / Rod "+thickness.ToString("F2"),PrimitiveType.Cube,
                    new Vector3(2.1f+i*.34f,.8f,-1.5f),new Vector3(thickness,1.6f,thickness),blue);
            }
            // A flat line and raised line separate surface detail from geometry silhouettes.
            Shape(platform,"Front baseline",PrimitiveType.Cube,new Vector3(0,.015f,-2.5f),new Vector3(6,.03f,.035f),stone);
            var sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type=LightType.Directional; sun.intensity=1.15f; sun.color=new Color(1,.94f,.84f);
            sun.transform.rotation=Quaternion.Euler(48,-35,0); sun.shadows=LightShadows.Soft;
            RenderSettings.sun=sun;
            var backdrop = new GameObject("Presentation Camera").AddComponent<Camera>();
            backdrop.cullingMask=0; backdrop.clearFlags=CameraClearFlags.SolidColor;
            backdrop.backgroundColor=new Color(.025f,.038f,.055f); backdrop.depth=-10;
            backdrop.allowHDR=false; backdrop.allowMSAA=false;
            backdrop.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var camera=new GameObject("Main Camera").AddComponent<Camera>(); camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.055f,.083f,.11f);
            camera.nearClipPlane=.1f; camera.farClipPlane=60; camera.allowHDR=false; camera.allowMSAA=false;
            var data=camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing=false; data.antialiasing=AntialiasingMode.None;
            var demo=new GameObject("006 Controls").AddComponent<LowResPixelDemo>(); demo.sceneCamera=camera; demo.ApplyPose();
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
            Selection.activeGameObject=demo.gameObject;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0,.5f,0),camera.transform.rotation,8);
        }
    }
}
