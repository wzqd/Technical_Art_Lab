using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace TechArtLab.LoFi.Editor
{
    public static class LoFiExperiment
    {
        public const string Root="Assets/_Lab/Experiments/008_LoFiLowPoly";
        public const string ScenePath=Root+"/Scenes/008_LoFiLowPoly_Minimal.unity";
        [MenuItem("TechArtLab/008/Create or Open Minimal")]
        public static void Open()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(File.Exists(ScenePath)){EditorSceneManager.OpenScene(ScenePath);return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.skybox=null; RenderSettings.fog=false; RenderSettings.ambientMode=AmbientMode.Flat;
            var variants=new Mesh[18];
            for(int kind=0;kind<3;kind++)for(int detail=0;detail<3;detail++)
            {
                var smooth=kind==2?LoFiMeshes.Trunk(6<<detail):LoFiMeshes.Rounded(detail,kind==1);
                var flat=LoFiMeshes.Flat(smooth); flat.RecalculateBounds();
                variants[kind*6+detail*2]=SaveMesh(flat); variants[kind*6+detail*2+1]=SaveMesh(smooth);
            }
            string materialPath=Root+"/Materials/LoFiDiffuse.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("TechArtLab/008/LoFiDiffuse")); material.SetFloat("_Ambient",.32f); AssetDatabase.CreateAsset(material,materialPath);}
            var parts=new List<LoFiDemo.Part>(); var parent=new GameObject("008 / Woodland").transform;
            GameObject Add(string name,Vector3 position,Vector3 scale,int role,int kind=-1)
            {
                GameObject go;
                if(kind<0){go=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(go.GetComponent<Collider>());}
                else go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
                go.name=name; go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=scale;
                var filter=go.GetComponent<MeshFilter>();var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=material;
                if(kind>=0)filter.sharedMesh=variants[kind*6];
                parts.Add(new LoFiDemo.Part{filter=filter,renderer=renderer,kind=kind,role=role});return go;
            }
            Add("Soil slab",new Vector3(0,-.28f,0),new Vector3(7,.5f,5),0);
            Add("Grass surface",new Vector3(0,-.005f,0),new Vector3(6.95f,.08f,4.95f),1);
            for(int tree=0;tree<2;tree++)
            {
                float x=tree==0?-1.65f:1.0f,z=tree==0?.5f:1.25f,k=tree==0?1: .8f;
                Add("Trunk "+tree,new Vector3(x,.025f,z),new Vector3(k,1.8f*k,k),2,2);
                Add("Canopy dark "+tree,new Vector3(x-.30f*k,1.85f*k,z+.12f),Vector3.one*.86f*k,3,0);
                Add("Canopy main "+tree,new Vector3(x+.39f*k,2.10f*k,z),new Vector3(.83f,.93f,.83f)*k,4,0);
                Add("Canopy light "+tree,new Vector3(x-.12f*k,2.65f*k,z+.10f),new Vector3(.75f,.80f,.72f)*k,5,0);
            }
            Add("Rock large",new Vector3(1.6f,.50f,-.9f),new Vector3(.92f,.85f,.85f),6,1).transform.rotation=Quaternion.Euler(0,20,0);
            Add("Rock small",new Vector3(2.6f,.25f,-.45f),Vector3.one*.46f,6,1).transform.rotation=Quaternion.Euler(0,-25,0);
            Add("Rock back",new Vector3(-2.5f,.25f,1.8f),Vector3.one*.48f,6,1);
            for(int i=0;i<7;i++)
                Add("Path slab "+i,new Vector3(-2.7f+i*.6f,.06f,-1.50f+Mathf.Sin(i*.6f)*.14f),new Vector3(.46f,.10f,.47f),7).transform.rotation=Quaternion.Euler(0,i%2==0?10:-8,0);
            for(int i=0;i<3;i++)Add("Step "+i,new Vector3(2.9f,.12f+i*.1f,.4f+i*.45f),new Vector3(.65f,.24f+i*.2f,.42f),7);
            var sun=new GameObject("Directional Light").AddComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.96f,.88f);
            sun.intensity=1.08f; sun.shadows=LightShadows.Hard; RenderSettings.sun=sun;
            var backdrop=new GameObject("Presentation Camera").AddComponent<Camera>();backdrop.cullingMask=0;backdrop.depth=-10;
            backdrop.clearFlags=CameraClearFlags.SolidColor;backdrop.backgroundColor=new Color(.024f,.038f,.034f);backdrop.allowHDR=false;backdrop.allowMSAA=false;
            backdrop.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.nearClipPlane=.1f;camera.farClipPlane=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.15f,.14f);
            camera.allowHDR=false;camera.allowMSAA=false;camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            var demo=new GameObject("008 Controls").AddComponent<LoFiDemo>();demo.sceneCamera=camera;demo.sun=sun;demo.parts=parts.ToArray();demo.variants=variants;
            demo.ApplySettings(true);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Selection.activeGameObject=demo.gameObject;
        }
        static Mesh SaveMesh(Mesh mesh)
        {
            string path=Root+"/Meshes/"+mesh.name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){Object.DestroyImmediate(mesh);return existing;}AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
