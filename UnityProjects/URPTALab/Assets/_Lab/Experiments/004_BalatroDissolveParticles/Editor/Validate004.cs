using System;
using System.IO;
using System.Text;
using UnityEngine;
using TechArtLab.Balatro.Particles;
using TechArtLab.Balatro.Dissolve;
public static class Validate004
{
 static void Check(bool ok,string msg) {if(!ok) throw new Exception(msg);}
 public static Texture2D Capture(Camera cam) {
  var old=cam.targetTexture; var active=RenderTexture.active; var rt=new RenderTexture(1280,720,24);
  try {cam.targetTexture=rt; cam.Render(); RenderTexture.active=rt;
   var t=new Texture2D(1280,720,TextureFormat.RGBA32,false); t.ReadPixels(new Rect(0,0,1280,720),0,0); t.Apply(); return t;
  } finally {cam.targetTexture=old; RenderTexture.active=active; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);}
 }
 [UnityEditor.MenuItem("TechArtLab/004/Validate and Capture")]
 public static void Main() {
  var demo=UnityEngine.Object.FindAnyObjectByType<DissolveParticleDemo>(); Check(demo && Application.isPlaying,"004 Play required");
  var e=demo.effect; demo.drive=false;
  string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../References/004_BalatroDissolveParticles/analysis/unity")); Directory.CreateDirectory(dir);
  var report=new StringBuilder();
  try {
   var original=new Material(Shader.Find("TechArtLab/002/CardDissolve")); var current=e.card.sharedMaterial;
   var pr=e.emitter.system.GetComponent<Renderer>(); pr.enabled=false; e.shadow.enabled=false;
   int totalMismatch=0;
   Check(current.shader.isSupported && !UnityEditor.ShaderUtil.ShaderHasError(current.shader),"004 shader unavailable");
   foreach(bool entry in new[]{false,true}) foreach(int mode in new[]{0,1})
   foreach(int seed in new[]{0,1234,5678}) foreach(float d in new[]{0f,.25f,.5f,.75f,1f}) {
    if(entry) e.BeginEntry(seed); else e.BeginExit(seed);
    e.SetProgress(d); e.viewMode=mode; e.Apply(); e.shadow.enabled=false;
    var a=Capture(e.viewCamera); var block=new MaterialPropertyBlock(); e.card.GetPropertyBlock(block);
    block.SetFloat("_FieldTime",e.fieldTime); block.SetVector("_PatternOffset",CardDissolveDemo.PatternOffset(seed));
    block.SetVector("_PixelSize",new Vector4(71,95,0,0));
    e.card.sharedMaterial=original; e.card.SetPropertyBlock(block); var b=Capture(e.viewCamera);
    var aa=a.GetPixels32(); var bb=b.GetPixels32(); int mismatch=0;
    for(int i=0;i<aa.Length;i++) if(Math.Abs(aa[i].r-bb[i].r)>1 || Math.Abs(aa[i].g-bb[i].g)>1 || Math.Abs(aa[i].b-bb[i].b)>1) mismatch++;
    totalMismatch+=mismatch; Check(mismatch<100,"CPU/002 mask differs: "+seed+"/"+d+" pixels="+mismatch);
    UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); e.card.sharedMaterial=current;
   }
   UnityEngine.Object.DestroyImmediate(original); pr.enabled=true; e.viewMode=0;
   report.AppendLine("002 vs 004 color + mask, exit + entry, seeds 0/1234/5678 and d=0/.25/.5/.75/1 (60 comparisons): mismatched pixels="+totalMismatch);
   uint hash=0; int count=0;
   for(int repeat=0;repeat<2;repeat++) {
    e.BeginExit(1234); var seen=new bool[DissolveGrid.Count];
    for(int frame=0;frame<72;frame++) {
     e.StepFixed(1/60f);
     foreach(int i in e.emitter.lastCells) {Check(e.Grid.crossed[i] && e.Grid.alpha[i]>.01f && !seen[i],"Invalid or repeat birth"); seen[i]=true;}
     if(frame==35 && repeat==0) {var png=Capture(e.viewCamera); File.WriteAllBytes(Path.Combine(dir,"exit_mid.png"),png.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(png);}
    }
    Check(e.IsComplete && e.emitter.TotalEmitted>50,"No completed exit or particles");
    if(repeat==0) {hash=e.emitter.BirthHash; count=e.emitter.TotalEmitted;}
    else Check(hash==e.emitter.BirthHash && count==e.emitter.TotalEmitted,"Seed non-determinism");
   }
   report.AppendLine("Birth crossing, alpha, one-shot, reproducibility: PASS; count="+count+" hash="+hash);
   e.BeginExit(1234); for(int i=0;i<30;i++) e.Tick(1/60f);
   int alive=e.emitter.system.particleCount; float progress=e.Progress;
   e.paused=true; e.Tick(.1f); Check(e.Progress==progress && alive==e.emitter.system.particleCount,"Pause failed");
   e.SetProgress(.5f); Check(e.emitter.system.particleCount==0 && e.emitter.TotalEmitted==0,"Scrub left particles");
   e.BeginEntry(1234); for(int i=0;i<73;i++) e.Tick(1/60f);
   Check(e.Progress==0 && e.emitter.TotalEmitted==0,"Entry emitted");
   e.BeginExit(1234); for(int i=0;i<30;i++) e.StepFixed(1/60f);
   var p=new ParticleSystem.Particle[512]; int n=e.emitter.system.GetParticles(p); Check(n>0,"No live particles");
   var position=p[0].position; var pose=e.card.transform.position;
   e.card.transform.position+=Vector3.right*2; e.card.transform.rotation=Quaternion.Euler(0,0,35);
   e.emitter.system.GetParticles(p); Check(Vector3.Distance(p[0].position,position)<1e-5,"Particle followed card");
   e.StepFixed(1/60f);
   for(int i=0;i<e.emitter.lastCells.Count;i++) {Vector2 uv=DissolveGrid.UV(e.emitter.lastCells[i]);
    Check(Vector3.Distance(e.emitter.lastBirths[i],e.card.transform.TransformPoint(new Vector3(uv.x-.5f,uv.y-.5f,-.015f)))<1e-5,"Birth transform wrong");}
   e.card.transform.position=pose; e.card.transform.rotation=Quaternion.identity;
   report.AppendLine("Pause, scrub, entry suppression, world simulation, rotated birth positions: PASS");
   e.ResetVisible(); var watch=System.Diagnostics.Stopwatch.StartNew();
   for(int i=0;i<600;i++) e.StepFixed(1/60f); watch.Stop();
   report.AppendLine("Frozen field + texture upload + particle step average="+(watch.Elapsed.TotalMilliseconds/600).ToString("F3")+" ms (Editor, single card, not standalone GPU benchmark)");
   File.WriteAllText(Path.Combine(dir,"validation.txt"),report.ToString()); Debug.Log(report);
  } finally {e.ResetVisible(); demo.drive=true;}
 }
}
