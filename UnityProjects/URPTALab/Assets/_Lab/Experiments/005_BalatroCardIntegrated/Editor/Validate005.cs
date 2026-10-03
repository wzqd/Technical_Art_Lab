using System;
using System.IO;
using System.Text;
using UnityEngine;
using TechArtLab.Balatro.Integrated;
public static class Validate005
{
 static void Check(bool x,string message) {if(!x) throw new Exception(message);}
 static void Steps(IntegratedCardController c,int count) {for(int i=0;i<count;i++) c.Advance(1/60f);}
 static Texture2D Capture(Camera cam) {
  var target=cam.targetTexture; var active=RenderTexture.active; var rt=new RenderTexture(1280,720,24);
  try {cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt;
   var t=new Texture2D(1280,720,TextureFormat.RGBA32,false); t.ReadPixels(new Rect(0,0,1280,720),0,0); t.Apply(); return t;
  } finally {cam.targetTexture=target; RenderTexture.active=active; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);}
 }
 static string dir;
 static void ValidateCorners(IntegratedCardController c,StringBuilder report) {
  var cam=c.effect.viewCamera; var original=c.effect.card.sharedMaterial;
  var source=new Material(Shader.Find("TechArtLab/001/CardHover"));
  float near=cam.nearClipPlane,far=cam.farClipPlane,strength=c.effect.hoverStrength;
  try {
   // 001 is the XY/UV reference, rendered with ample depth margin. The combined
   // shader must retain that image even with a far plane that exposed clipping.
   foreach(float s in new[]{1f,3f}) foreach(int x in new[]{-1,1}) foreach(int y in new[]{-1,1}) {
    c.effect.hoverStrength=s;
    c.Move(c.drag.card.TransformPoint(new Vector3(.49f*x,.49f*y,0))); Steps(c,60);
    cam.nearClipPlane=.3f; cam.farClipPlane=50;
    c.effect.card.sharedMaterial=source;
    var expected=Capture(cam);
    c.effect.card.sharedMaterial=original; cam.farClipPlane=1000;
    var actual=Capture(cam);
    int mismatch=0; var a=actual.GetPixels32(); var b=expected.GetPixels32();
    for(int i=0;i<a.Length;i++) if(Math.Abs(a[i].r-b[i].r)>1 || Math.Abs(a[i].g-b[i].g)>1 || Math.Abs(a[i].b-b[i].b)>1) mismatch++;
    File.WriteAllBytes(Path.Combine(dir,$"hover_corner_{x}_{y}_strength{s}.png"),actual.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(expected); UnityEngine.Object.DestroyImmediate(actual);
    Check(mismatch==0,$"Corner ({x},{y}) strength {s} clipped or changed XY/UV: {mismatch} pixels");
   }
   report.AppendLine("Four hover corners x strengths 1/3, shadow enabled: identical to 001 at safe depth, despite far plane 50 -> 1000: PASS");
  } finally {
   cam.nearClipPlane=near; cam.farClipPlane=far; c.effect.hoverStrength=strength;
   c.effect.card.sharedMaterial=original; UnityEngine.Object.DestroyImmediate(source);
   c.Move(c.drag.Anchor); Steps(c,60); c.effect.Apply();
  }
 }
 static void Save(IntegratedCardController c,string name) {var t=Capture(c.effect.viewCamera); File.WriteAllBytes(Path.Combine(dir,name+".png"),t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t);}
 [UnityEditor.MenuItem("TechArtLab/005/Validate and Capture")]
 public static void Main() {
  var demo=UnityEngine.Object.FindAnyObjectByType<IntegratedCardDemo>(); Check(demo && Application.isPlaying,"005 Play required");
  var c=demo.controller; demo.drive=false; c.randomize=false; c.effect.seed=1234;
  dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../References/005_BalatroCardIntegrated/analysis/unity")); Directory.CreateDirectory(dir);
  var report=new StringBuilder();
  try {
   Check(c.effect.card.sharedMaterial.shader.isSupported && !UnityEditor.ShaderUtil.ShaderHasError(c.effect.card.sharedMaterial.shader),"005 shader unavailable");
   c.ResetCard(); c.PointerActive=true; c.Move(c.drag.Anchor); Steps(c,60);
   Check(c.effect.hover>.99f,"Hover did not recover"); Save(c,"hover");
   // Compare combined vertex deformation with 001 on the same complete artwork.
   var current=c.effect.card.sharedMaterial; var source=new Material(Shader.Find("TechArtLab/001/CardHover"));
   c.effect.shadow.enabled=false; var expected=Capture(c.effect.viewCamera);
   c.effect.card.sharedMaterial=source; var actual=Capture(c.effect.viewCamera);
   var aa=actual.GetPixels32(); var bb=expected.GetPixels32(); int hoverMismatch=0;
   for(int i=0;i<aa.Length;i++) if(Math.Abs(aa[i].r-bb[i].r)>1 || Math.Abs(aa[i].g-bb[i].g)>1 || Math.Abs(aa[i].b-bb[i].b)>1) hoverMismatch++;
   c.effect.card.sharedMaterial=current; UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(expected); UnityEngine.Object.DestroyImmediate(actual);
   Check(hoverMismatch==0,"001 hover mismatch: "+hoverMismatch); c.effect.Apply();
   report.AppendLine("Complete-card hover vs 001: pixel-identical. Hover recovers at settled home: PASS");
   ValidateCorners(c,report);
   Check(c.Press(c.drag.Anchor),"Center press failed"); Check(c.effect.hover==0,"Hover not disabled same call");
   Steps(c,80); Check(c.State==IntegratedCardController.Phase.Dragging && c.Elapsed==0,"Stationary click armed");
   c.Move(c.drag.Anchor+Vector2.right*.04f); Steps(c,80); Check(c.Elapsed==0,"Subthreshold move armed");
   c.Release(); Steps(c,120); Check(c.State==IntegratedCardController.Phase.Ready,"Cancel did not return");

   c.Press(c.drag.Anchor); c.Move(c.drag.Anchor+Vector2.right*.5f); Steps(c,12);
   Check(c.State==IntegratedCardController.Phase.Countdown,"Departure did not arm");
   float armed=c.Elapsed; c.Move(c.drag.Anchor); Steps(c,20);
   Check(c.Elapsed>armed && c.State==IntegratedCardController.Phase.Countdown,"Returning home canceled timer");
   c.Release(); Check(c.Elapsed==0 && c.State==IntegratedCardController.Phase.Returning,"Release did not cancel immediately");
   Steps(c,120); Check(c.TriggerCount==0,"Canceled timer fired later");

   c.Press(c.drag.Position); c.Move(c.drag.Anchor+Vector2.right); Steps(c,12);
   Check(c.Elapsed<.3f,"Re-grab reused prior elapsed time");
   c.SendMessage("OnApplicationFocus",false); Check(c.Elapsed==0 && !c.drag.IsDragging,"Focus loss failed cancellation");
   c.SendMessage("OnApplicationFocus",true);
   Steps(c,120);
   c.Press(c.drag.Position); c.Move(c.drag.Anchor+Vector2.right);
   for(int i=0;i<100 && c.State!=IntegratedCardController.Phase.Countdown;i++) Steps(c,1);
   c.delay=c.Elapsed+1/60f; c.Release(); Steps(c,1);
   Check(c.TriggerCount==0,"Release at deadline fired timer"); c.delay=1;
   report.AppendLine("Same-call hover off, stationary/subthreshold clicks, latched return-home timer, release cancellation, re-grab reset, focus loss, release priority: PASS");

   c.ResetCard(); c.Press(c.drag.Anchor); c.Move(c.drag.Anchor+new Vector2(1,.3f)); Steps(c,24); Save(c,"countdown");
   while(c.State!=IntegratedCardController.Phase.Dissolving) Steps(c,1);
   Check(c.drag.IsDragging && c.TriggerCount==1,"Dissolve lost held capture");
   c.Move(c.drag.Anchor+new Vector2(1.4f,.4f)); Steps(c,24); Save(c,"dissolving_held");
   Vector2 release=c.drag.Position; c.Release(); Steps(c,12); Save(c,"dissolving_released");
   Check(Vector2.Distance(release,c.drag.Position)<.001f && c.effect.Progress>0,"Dissolve release returned home");
   Steps(c,120); Check(c.State==IntegratedCardController.Phase.Hidden && !c.Press(c.drag.Position),"Hidden hit accepted");
   Check(c.effect.emitter.system.particleCount==0 && c.TriggerCount==1,"Tail or repeated trigger failed"); Save(c,"hidden");
   c.ResetCard(); Check(c.effect.Progress==0 && c.effect.emitter.system.particleCount==0 && c.Elapsed==0,"Reset incomplete"); Save(c,"reset");
   report.AppendLine("Held dissolve, release stays put, one-shot trigger, hidden hit rejection, natural particle tail, reset: PASS");

   float min=100,max=0,maxPoseDifference=0; Vector3[] baselinePose=null;
   foreach(int fps in new[]{30,60,120}) foreach(var gripLocal in new[]{Vector2.zero,new Vector2(.45f,.45f),new Vector2(-.45f,-.45f)}) {
    c.ResetCard(); Vector2 grip=c.drag.card.TransformPoint(gripLocal); c.Press(grip);
    float trigger=-1; var samples=new Vector3[6];
    for(int i=1;i<=fps*3;i++) {
     c.Move(grip+new Vector2(Mathf.Min((float)i/fps*2,1),.1f*Mathf.Min((float)i/fps*2,1)));
     c.Advance(1f/fps);
     if(c.TriggerCount>0 && trigger<0) trigger=(float)i/fps;
     if(i%(fps/2)==0) samples[i/(fps/2)-1]=new Vector3(c.drag.Position.x,c.drag.Position.y,c.drag.Angle);
    }
    Check(trigger>0,"No timed trigger"); min=Mathf.Min(min,trigger); max=Mathf.Max(max,trigger);
    if(baselinePose==null) baselinePose=samples;
    for(int i=0;i<6;i++) maxPoseDifference=Mathf.Max(maxPoseDifference,Vector3.Distance(samples[i],baselinePose[i]));
    report.AppendLine($"fps={fps}, grip={gripLocal}: dissolve detected at {trigger:F6}s");
   }
   Check(max-min<=1/60f+.0001f,"FPS or grip timing drift");
   Check(maxPoseDifference<.01f,"FPS or grip pose drift");
   report.AppendLine("Common half-second position/angle sample maximum difference="+maxPoseDifference.ToString("F6"));
   // Input coordinates and projection both use these target viewport dimensions.
   var camera=c.effect.viewCamera; var previous=camera.targetTexture;
   Vector2 reference=Vector2.zero;
   foreach(int height in new[]{360,720,1080}) {
    var rt=new RenderTexture(height*16/9,height,24); camera.targetTexture=rt;
    try {
     Vector3 a=camera.ViewportToScreenPoint(new Vector3(.5f,.5f,0));
     Vector3 b=camera.ViewportToScreenPoint(new Vector3(.65f,.55f,0));
     c.drag.ScreenToPlane(a,out var wa); c.drag.ScreenToPlane(b,out var wb);
     if(height==360) reference=wb-wa;
     Check(Vector2.Distance(reference,wb-wa)<.0001f,"Viewport scaling changed relative input");
     c.ResetCard(); c.Press(c.drag.Anchor); c.Move(c.drag.Anchor+wb-wa); Steps(c,75);
     Check(c.TriggerCount==1 && c.State==IntegratedCardController.Phase.Dissolving,"Viewport gesture failed to dissolve");
    } finally {camera.targetTexture=previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);}
   }
   report.AppendLine("30/60/120 Hz + three grips: trigger spread="+(max-min).ToString("F6")+" s; 360/720/1080 viewport relative-input mapping: PASS");
   File.WriteAllText(Path.Combine(dir,"validation.txt"),report.ToString()); Debug.Log(report);
  } finally {c.delay=1; c.ResetCard(); demo.drive=true;}
 }
}
