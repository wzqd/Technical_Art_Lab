using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TechArtLab.Balatro.Drag;
using TechArtLab.Balatro.Particles.Editor;
namespace TechArtLab.Balatro.Integrated.Editor
{
 public static class IntegratedCardExperiment
 {
  public const string Root="Assets/_Lab/Experiments/005_BalatroCardIntegrated";
  public const string ScenePath=Root+"/Scenes/005_CardIntegrated_Minimal.unity";
  [MenuItem("TechArtLab/005/Create or Open Minimal")]
  public static void Open() {
   if(Application.isPlaying) throw new Exception("Exit Play first");
   if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
   if(File.Exists(ScenePath)) {EditorSceneManager.OpenScene(ScenePath); return;}
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   var effect=DissolveParticleExperiment.Build("TechArtLab/005/IntegratedCard",Root);
   var drag=effect.gameObject.AddComponent<CardDragController>(); drag.card=effect.card.transform;
   drag.shadow=effect.shadow.transform; drag.viewCamera=effect.viewCamera;
   var c=effect.gameObject.AddComponent<IntegratedCardController>(); c.drag=drag; c.effect=effect;
   effect.gameObject.AddComponent<IntegratedCardDemo>().controller=c;
   EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
  }
 }
}
