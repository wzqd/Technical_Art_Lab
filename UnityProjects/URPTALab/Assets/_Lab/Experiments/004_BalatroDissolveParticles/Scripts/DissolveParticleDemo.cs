using UnityEngine;
using UnityEngine.InputSystem;
namespace TechArtLab.Balatro.Particles
{
    public sealed class DissolveParticleDemo : MonoBehaviour
    {
        public DissolveParticleController effect;
        public bool randomize=true,showControls=true,drive=true;
        readonly System.Random random=new System.Random();
        void Start() { effect.ResetVisible(); }
        int Seed()=>randomize ? random.Next(1,65536) : effect.seed;
        void Update()
        {
            if(!drive) return;
            var k=Keyboard.current;
            if(k!=null) {
                if(k.digit1Key.wasPressedThisFrame) effect.BeginExit(Seed());
                if(k.digit2Key.wasPressedThisFrame) effect.BeginEntry(Seed());
                if(k.rKey.wasPressedThisFrame) effect.ResetVisible();
                if(k.spaceKey.wasPressedThisFrame) effect.paused=!effect.paused;
            }
            effect.Tick(Time.unscaledDeltaTime);
        }
        void OnGUI()
        {
            if(!showControls) return;
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            var old=GUI.matrix; GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUILayout.BeginArea(new Rect(24,24,330,660),GUI.skin.box);
            GUILayout.Label("004 / DISSOLVE EDGE PARTICLES");
            if(GUILayout.Button("1 / Orange exit")) effect.BeginExit(Seed());
            if(GUILayout.Button("2 / Green entry (no particles)")) effect.BeginEntry(Seed());
            if(GUILayout.Button("R / Reset visible")) effect.ResetVisible();
            if(GUILayout.Button(effect.paused ? "Space / Resume" : "Space / Pause")) effect.paused=!effect.paused;
            GUILayout.Label($"Progress: {effect.Progress:F3}");
            float d=GUILayout.HorizontalSlider(effect.Progress,0,1);
            if(Mathf.Abs(d-effect.Progress)>.0001f) effect.SetProgress(d);
            GUILayout.Label($"Duration: {effect.duration:F2}s"); effect.duration=GUILayout.HorizontalSlider(effect.duration,.2f,3);
            randomize=GUILayout.Toggle(randomize,"New seed on replay");
            GUILayout.Label($"Seed: {effect.seed}");
            if(GUILayout.Button("Fixed seed 1234")) { randomize=false; effect.seed=1234; effect.SetProgress(effect.Progress); }
            effect.freezeField=GUILayout.Toggle(effect.freezeField,"Freeze field time");
            effect.emitter.emitParticles=GUILayout.Toggle(effect.emitter.emitParticles,"Emit particles");
            effect.emitter.showBirths=GUILayout.Toggle(effect.emitter.showBirths,"Show birth points");
            effect.viewMode=GUILayout.Toolbar(effect.viewMode,new[]{"Color","Mask","Field"});
            GUILayout.Label($"Emission probability: {effect.emitter.probability:P0}");
            effect.emitter.probability=GUILayout.HorizontalSlider(effect.emitter.probability,0,.2f);
            GUILayout.Label($"Size: 1 - {effect.emitter.sizePixels.y:F1} pixels");
            effect.emitter.sizePixels.y=GUILayout.HorizontalSlider(effect.emitter.sizePixels.y,1,6);
            GUILayout.Label($"Lifetime: {effect.emitter.lifetime.y:F2}s max");
            effect.emitter.lifetime.y=GUILayout.HorizontalSlider(effect.emitter.lifetime.y,.4f,1.5f);
            GUILayout.Label($"Speed: {effect.emitter.speedHeights.y:F2} heights/s max");
            effect.emitter.speedHeights.y=GUILayout.HorizontalSlider(effect.emitter.speedHeights.y,.1f,.8f);
            GUILayout.Label($"Alive: {effect.emitter.system.particleCount} / {effect.emitter.capacity}\nEmitted: {effect.emitter.TotalEmitted}\nScrubbing clears particles. Entry emits none.");
            GUILayout.EndArea(); GUI.matrix=old; effect.Apply();
            if(effect.emitter.showBirths) foreach(var p in effect.emitter.lastBirths) {
                Vector3 s=effect.viewCamera.WorldToScreenPoint(p); Color c=GUI.color; GUI.color=Color.magenta;
                GUI.DrawTexture(new Rect(s.x-2,Screen.height-s.y-2,4,4),Texture2D.whiteTexture); GUI.color=c;
            }
        }
    }
}
