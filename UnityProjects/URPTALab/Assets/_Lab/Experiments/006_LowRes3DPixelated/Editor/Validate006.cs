using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TechArtLab.Pixelated.Editor
{
    public static class Validate006
    {
        static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../../References/006_LowRes3DPixelated/analysis/unity"));
        static bool running;
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static Texture2D Read(RenderTexture rt)
        {
            var old = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); t.Apply(); return t;
            }
            finally { RenderTexture.active = old; }
        }
        static Texture2D Render(LowResPixelDemo d, int width, int height, bool linear = false)
        {
            d.bilinear = linear; d.SetTargetSize(width,height); d.ApplyPose(); d.sceneCamera.Render();
            return Read(d.Target);
        }
        static void Save(Texture2D image, string name) => File.WriteAllBytes(Path.Combine(DirectoryPath,name+".png"),image.EncodeToPNG());
        static Texture2D Upscale(RenderTexture source, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
            var old = RenderTexture.active;
            try { Graphics.Blit(source,rt); return Read(rt); }
            finally { RenderTexture.active=old; RenderTexture.ReleaseTemporary(rt); }
        }
        static int Difference(Texture2D a, Texture2D b)
        {
            var x=a.GetPixels32(); var y=b.GetPixels32(); Check(x.Length==y.Length,"Image sizes differ");
            int count=0;
            for(int i=0;i<x.Length;i++) if(Math.Abs(x[i].r-y[i].r)>1 || Math.Abs(x[i].g-y[i].g)>1 || Math.Abs(x[i].b-y[i].b)>1) count++;
            return count;
        }
        static int CountColor(Texture2D t, int channel)
        {
            int count=0;
            foreach(var c in t.GetPixels32())
                if(channel==0 ? c.r>c.g*1.4f && c.r>c.b*1.4f && c.r>60 : c.b>c.r*1.25f && c.b>c.g*1.15f && c.b>60) count++;
            return count;
        }
        static int BlockViolations(Texture2D t, int block)
        {
            var p=t.GetPixels32(); int bad=0;
            for(int y=0;y<t.height;y+=block) for(int x=0;x<t.width;x+=block)
            {
                var first=p[y*t.width+x];
                for(int j=0;j<block;j++) for(int i=0;i<block;i++)
                {
                    var c=p[(y+j)*t.width+x+i];
                    if(Math.Abs(c.r-first.r)>1 || Math.Abs(c.g-first.g)>1 || Math.Abs(c.b-first.b)>1) bad++;
                }
            }
            return bad;
        }
        [MenuItem("TechArtLab/006/Validate and Capture")]
        public static void Main()
        {
            var d=Object.FindAnyObjectByType<LowResPixelDemo>();
            Check(Application.isPlaying && d,"Open 006 and enter Play first.");
            Check(!running,"Validation already running.");
            running=true; d.StartCoroutine(Run(d));
        }
        static IEnumerator Run(LowResPixelDemo d)
        {
            Directory.CreateDirectory(DirectoryPath);
            File.WriteAllText(Path.Combine(DirectoryPath,"validation.txt"),"IN PROGRESS\n");
            bool passed=false;
            var report=new StringBuilder();
            report.AppendLine($"Unity {Application.unityVersion}; {SystemInfo.graphicsDeviceType}; {QualitySettings.activeColorSpace}; {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            bool oldDrive=d.drive, oldFilter=d.bilinear, oldNative=d.nativeReference, oldOrtho=d.orthographic;
            bool oldReplay=d.replay, oldPause=d.paused;
            int oldResolution=d.resolutionIndex;
            float oldYaw=d.yaw,oldPitch=d.pitch,oldDistance=d.distance,oldSize=d.orthoSize,oldTime=d.replayTime;
            try
            {
                d.drive=false; d.ResetView(); d.nativeReference=false; d.bilinear=false; d.resolutionIndex=1; d.RefreshOutput();
                // Read automatic frame output before any explicit Camera.Render, to catch batch-state bugs.
                yield return null; yield return null; yield return null;
                var automatic=Read(d.Target); Save(automatic,"automatic_frame");
                Check(CountColor(automatic,0)>50,"Runtime frame lost the coral material (batching or rendering problem).");
                var manual=Render(d,320,180);
                int automaticDifference=Difference(automatic,manual);
                report.AppendLine($"Automatic frame vs explicit camera render: {automaticDifference} differing pixels (1/255 tolerance).");
                Check(automaticDifference==0,"Automatic and explicit rendering differ.");
                Object.DestroyImmediate(automatic); Object.DestroyImmediate(manual);
                var shader=Shader.Find("TechArtLab/006/SimpleDiffuse");
                Check(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader),"Shader unsupported or compile error.");
                foreach(int height in LowResPixelDemo.Heights)
                {
                    var source=Render(d,height*16/9,height);
                    Check(CountColor(source,0)>10,"Coral sphere missing.");
                    Check(d.Target.antiAliasing==1 && !d.Target.useMipMap,"Unexpected AA or mipmaps.");
                    Save(source,$"source_{height}");
                    var enlarged=Upscale(d.Target,1920,1080); Save(enlarged,$"point_{height}");
                    int block=1080/height;
                    Check(BlockViolations(enlarged,block)==0,$"Point {height}: nonuniform integer blocks.");
                    report.AppendLine($"{source.width}x{source.height} -> 1920x1080 Point: {block}x{block} blocks uniform. Blue rod pixels={CountColor(source,1)}.");
                    Object.DestroyImmediate(source); Object.DestroyImmediate(enlarged);
                }
                var raw=Render(d,320,180); Object.DestroyImmediate(raw);
                var point=Upscale(d.Target,1280,720);
                d.bilinear=true; d.SetTargetSize(320,180);
                var linear=Upscale(d.Target,1280,720);
                int changed=Difference(point,linear), mixed=BlockViolations(linear,4);
                Check(changed>1000 && mixed>1000,"Bilinear did not mix image boundaries.");
                report.AppendLine($"320x180 ->1280x720: Point/Bilinear difference={changed}; Bilinear nonuniform block pixels={mixed}.");
                Save(point,"point_compare"); Save(linear,"bilinear_compare"); Object.DestroyImmediate(point); Object.DestroyImmediate(linear);
                d.replay=true; d.replayTime=0; var first=Render(d,320,180); Save(first,"pan_0");
                d.replayTime=1; var moved=Render(d,320,180); Save(moved,"pan_1");
                int motion=Difference(first,moved); Check(motion>100,"Pan did not alter the image.");
                d.replayTime=0; var repeat=Render(d,320,180); Check(Difference(first,repeat)==0,"Fixed replay time is not reproducible.");
                report.AppendLine($"Fixed pan t=0 vs 1: {motion} pixels changed; repeated t=0 pixel-identical.");
                Object.DestroyImmediate(first); Object.DestroyImmediate(moved); Object.DestroyImmediate(repeat);
                d.replay=false; d.orthographic=false; var perspective=Render(d,320,180); Save(perspective,"perspective");
                Check(CountColor(perspective,0)>10,"Perspective view lost the sphere.");
                d.yaw=105; var orbit=Render(d,320,180); Save(orbit,"orbit_105");
                Check(Difference(perspective,orbit)>1000,"Orbit did not change the view.");
                Object.DestroyImmediate(perspective); Object.DestroyImmediate(orbit);
                d.ResetView(); d.nativeReference=true; d.RefreshOutput();
                Check(Mathf.Abs(d.PixelScale-1)<.001f,"Native reference not 1:1.");
                var native=Render(d,d.Target.width,d.Target.height); Save(native,"native_reference"); Object.DestroyImmediate(native);
                foreach(var available in new[]{new Rect(0,0,960,592),new Rect(0,0,1600,900),new Rect(0,0,200,100)})
                {
                    var rect=LowResPixelDemo.FitImage(available,640,360,true,out float scale);
                    Check(rect.width<=available.width+.01f && rect.height<=available.height+.01f,"Image overflow.");
                    Check(Mathf.Abs(rect.width/rect.height-16f/9)<.001f,"Aspect changed.");
                    Check(scale<1 || scale==Mathf.Floor(scale),"Noninteger enlargement.");
                }
                report.AppendLine("Perspective/orbit image changes, native 1:1, letterbox/aspect/integer/fractional-fit layout: PASS.");
                d.bilinear=false; d.nativeReference=false; d.resolutionIndex=1; d.RefreshOutput();
                // Sample real continuous rendering after the target/filter/camera transitions.
                yield return null; yield return null; yield return null;
                var final=Read(d.Target); Check(CountColor(final,0)>50,"Runtime color lost after switching."); Object.DestroyImmediate(final);
                report.AppendLine("Automatic runtime frame after switching: PASS. No game-source implementation or performance claim.");
                report.AppendLine("PASS");
                passed=true;
                File.WriteAllText(Path.Combine(DirectoryPath,"validation.txt"),report.ToString()); Debug.Log(report.ToString());
            }
            finally
            {
                if(!passed) File.WriteAllText(Path.Combine(DirectoryPath,"validation.txt"),report+"\nFAILED or interrupted; see Unity Console.\n");
                d.bilinear=oldFilter; d.nativeReference=oldNative; d.orthographic=oldOrtho; d.resolutionIndex=oldResolution;
                d.yaw=oldYaw; d.pitch=oldPitch; d.distance=oldDistance; d.orthoSize=oldSize;
                d.replay=oldReplay; d.paused=oldPause; d.replayTime=oldTime; d.RefreshOutput(); d.ApplyPose(); d.drive=oldDrive;
                running=false;
            }
        }
    }
}
