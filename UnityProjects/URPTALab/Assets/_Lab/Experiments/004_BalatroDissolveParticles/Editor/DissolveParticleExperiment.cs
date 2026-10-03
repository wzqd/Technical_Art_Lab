using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace TechArtLab.Balatro.Particles.Editor
{
    public static class DissolveParticleExperiment
    {
        public const string Root="Assets/_Lab/Experiments/004_BalatroDissolveParticles";
        public const string ScenePath=Root+"/Scenes/004_DissolveParticles_Minimal.unity";
        [MenuItem("TechArtLab/004/Create or Open Minimal")]
        public static void Open()
        {
            if(Application.isPlaying) throw new Exception("Exit Play first");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(File.Exists(ScenePath)) {EditorSceneManager.OpenScene(ScenePath); return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var effect=Build("TechArtLab/004/DissolveParticles");
            effect.gameObject.AddComponent<DissolveParticleDemo>().effect=effect;
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
        }
        public static DissolveParticleController Build(string shaderName,string materialRoot=Root)
        {
            var camera=new GameObject("Main Camera",typeof(Camera)).GetComponent<Camera>();
            camera.tag="MainCamera"; camera.transform.position=new Vector3(0,0,-10); camera.orthographic=true; camera.orthographicSize=3.6f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.19f,.22f); camera.allowHDR=false; camera.allowMSAA=false;
            Texture2D ace=CopyTexture("AceTestCard"),locked=CopyTexture("LockTestCard");
            string materialPath=materialRoot+"/Materials/"+(shaderName.Contains("005") ? "IntegratedCard" : "DissolveCard")+".mat";
            var mat=MaterialAt(materialPath,shaderName); mat.SetTexture("_BaseMap",ace);
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Lab/Experiments/003_BalatroCardDrag/Models/CardQuad.asset");
            var card=Quad("Card",mesh,mat,1,new Vector3(1.5f,0,0));
            var shadow=Quad("Shadow",mesh,mat,0,new Vector3(1.63f,-.16f,.1f));
            var go=new GameObject("Dissolve controls"); var effect=go.AddComponent<DissolveParticleController>();
            effect.card=card; effect.shadow=shadow; effect.ace=ace; effect.locked=locked; effect.viewCamera=camera;
            var emitter=go.AddComponent<EdgeParticleEmitter>(); effect.emitter=emitter;
            var particles=new GameObject("Edge particles",typeof(ParticleSystem)); emitter.system=particles.GetComponent<ParticleSystem>();
            ConfigureParticles(emitter.system,MaterialAt(Root+"/Materials/SquareParticle.mat","TechArtLab/004/SquareParticle"));
            return effect;
        }
        public static void ConfigureParticles(ParticleSystem ps,Material mat)
        {
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.playOnAwake=false; main.loop=false; main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.maxParticles=512; main.startSpeed=0; main.gravityModifier=0; main.useUnscaledTime=true;
            var emission=ps.emission; emission.enabled=false; var shape=ps.shape; shape.enabled=false;
            var size=ps.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            var color=ps.colorOverLifetime; color.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,.5f),new GradientAlphaKey(0,1)}); color.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=mat; renderer.sortingOrder=2; renderer.renderMode=ParticleSystemRenderMode.Billboard;
            ps.useAutoRandomSeed=false; ps.randomSeed=1;
        }
        static Texture2D CopyTexture(string name)
        {
            string path=Root+"/Textures/"+name+".png";
            if(!File.Exists(path)) AssetDatabase.CopyAsset("Assets/_Lab/Experiments/002_BalatroCardDissolve/Textures/"+name+".png",path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            if(!importer.isReadable) {importer.isReadable=true; importer.SaveAndReimport();}
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        public static Material MaterialAt(string path,string shader)
        {
            var s=Shader.Find(shader); if(!s || !s.isSupported || ShaderUtil.ShaderHasError(s)) throw new Exception("Shader error: "+shader);
            var m=AssetDatabase.LoadAssetAtPath<Material>(path); if(!m) {m=new Material(s); AssetDatabase.CreateAsset(m,path);} return m;
        }
        static MeshRenderer Quad(string name,Mesh mesh,Material m,int order,Vector3 p)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer)); go.transform.position=p;
            go.transform.localScale=new Vector3(3.2f*71/95,3.2f,1); go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.GetComponent<MeshRenderer>(); r.sharedMaterial=m; r.sortingOrder=order; return r;
        }
    }
}
