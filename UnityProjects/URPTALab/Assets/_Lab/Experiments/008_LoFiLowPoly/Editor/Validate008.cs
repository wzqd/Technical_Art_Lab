using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TechArtLab.LoFi.Editor
{
    public static class Validate008
    {
        static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../../../References/008_LoFiLowPoly/analysis/unity"));
        static bool running;
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
        static Texture2D Read(RenderTexture rt)
        {
            var old=RenderTexture.active;
            try {RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
                t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();return t;}
            finally {RenderTexture.active=old;}
        }
        static Texture2D Render(LoFiDemo d,int width=640,int height=360)
        {d.SetTargetSize(width,height);d.ApplySettings(true);d.sceneCamera.Render();return Read(d.Target);}
        static int Difference(Texture2D a,Texture2D b)
        {
            Check(a.width==b.width && a.height==b.height,"Image dimensions differ");var x=a.GetPixels32();var y=b.GetPixels32();int n=0;
            for(int i=0;i<x.Length;i++)if(Math.Abs(x[i].r-y[i].r)>1 || Math.Abs(x[i].g-y[i].g)>1 || Math.Abs(x[i].b-y[i].b)>1)n++;
            return n;
        }
        static int Colors(Texture2D t)
        {
            var colors=new HashSet<Color32>(t.GetPixels32()); colors.Remove(t.GetPixels32()[0]); return colors.Count;
        }
        static void Save(Texture2D t,string name)=>File.WriteAllBytes(Path.Combine(Output,name+".png"),t.EncodeToPNG());
        static void Free(params Texture2D[] images){foreach(var t in images)Object.DestroyImmediate(t);}
        static void MeshChecks(LoFiDemo d,StringBuilder report)
        {
            for(int kind=0;kind<3;kind++)for(int level=0;level<3;level++)
            {
                var flat=d.variants[kind*6+level*2];var smooth=d.variants[kind*6+level*2+1];
                var f=flat.vertices;var s=smooth.vertices;var fi=flat.triangles;var si=smooth.triangles;var fn=flat.normals;
                Check(fi.Length==si.Length && f.Length> s.Length,"Normal variants must preserve triangle count and split flat vertices.");
                for(int i=0;i<fi.Length;i++)Check((f[fi[i]]-s[si[i]]).sqrMagnitude<1e-12f,"Flat/smooth geometry mismatch.");
                for(int i=0;i<fi.Length;i+=3)
                {
                    var face=Vector3.Cross(f[fi[i+1]]-f[fi[i]],f[fi[i+2]]-f[fi[i]]).normalized;
                    Check(face.sqrMagnitude>.99f,"Degenerate triangle.");
                    for(int j=0;j<3;j++)Check(Vector3.Dot(face,fn[fi[i+j]])>.9999f,"Flat normals are not face normals.");
                }
                foreach(var n in smooth.normals)Check(Mathf.Abs(n.magnitude-1)<.0001f,"Invalid smooth normal.");
                int expected=kind<2?20*(1<<(2*level)):4*(6<<level);
                Check(fi.Length/3==expected,"Unexpected subdivision triangle count.");
                report.AppendLine($"Mesh kind={kind}, level={level}: {expected} triangles, flat/smooth vertices={flat.vertexCount}/{smooth.vertexCount}; identical ordered triangle positions, valid normals.");
            }
        }
        [MenuItem("TechArtLab/008/Validate and Capture")]
        public static void Main()
        {
            var d=Object.FindAnyObjectByType<LoFiDemo>();Check(Application.isPlaying && d,"Open 008 and enter Play.");
            Check(!running,"Validation already running");running=true;d.StartCoroutine(Run(d));
        }
        static IEnumerator Run(LoFiDemo d)
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"validation.txt"),"IN PROGRESS\n");
            string state=JsonUtility.ToJson(d);bool passed=false;
            var report=new StringBuilder($"Unity {Application.unityVersion}; {SystemInfo.graphicsDeviceType}; {QualitySettings.activeColorSpace}\n");
            try
            {
                d.drive=false;d.ResetDemo();d.shadowMode=0;MeshChecks(d,report);
                var shader=Shader.Find("TechArtLab/008/LoFiDiffuse");Check(shader && shader.isSupported && !ShaderUtil.ShaderHasError(shader),"Shader error/unsupported.");
                for(int level=0;level<3;level++)
                {
                    d.subdivision=level;d.debugView=3;d.smooth=false;var flatMask=Render(d);int flatVertices=d.VertexCount,triangles=d.TriangleCount;
                    d.smooth=true;var smoothMask=Render(d);
                    Check(Difference(flatMask,smoothMask)==0,"Normals altered the silhouette.");
                    Check(d.TriangleCount==triangles,"Normals altered triangle count.");
                    d.debugView=0;d.smooth=false;var flat=Render(d);d.smooth=true;var smooth=Render(d);
                    int changed=Difference(flat,smooth);Check(changed>100,"Normals did not change lighting.");
                    report.AppendLine($"Level {level}: scene triangles={triangles}, flat/smooth vertices={flatVertices}/{d.VertexCount}; silhouette differences=0, beauty differences={changed}.");
                    Save(flat,$"level_{level}_flat");Save(smooth,$"level_{level}_smooth");Save(flatMask,$"level_{level}_silhouette");Free(flatMask,smoothMask,flat,smooth);
                }
                d.debugView=3;d.smooth=false;d.subdivision=0;var coarse=Render(d);d.subdivision=2;var fine=Render(d);
                int silhouetteChange=Difference(coarse,fine);Check(silhouetteChange>100,"Subdivision did not change silhouette.");
                report.AppendLine($"Level 0 vs 2 silhouette differences={silhouetteChange}.");Free(coarse,fine);
                d.subdivision=0;d.debugView=1;
                for(int p=0;p<3;p++)
                {
                    d.palette=p;var albedo=Render(d);int colors=Colors(albedo);
                    Check(colors==d.PaletteSize,$"Albedo has {colors} colors, expected {d.PaletteSize}.");Save(albedo,$"palette_{d.PaletteSize}_albedo");Free(albedo);
                    d.debugView=0;var beauty=Render(d);int shaded=Colors(beauty);
                    Check(shaded>d.PaletteSize,"Beauty should not be limited to the material palette.");
                    report.AppendLine($"Palette {d.PaletteSize}: observed albedo colors={colors}, beauty colors={shaded} (background excluded).");
                    Save(beauty,$"palette_{d.PaletteSize}_beauty");Free(beauty);d.debugView=1;
                }
                d.palette=2;d.debugView=0;d.shadowMode=0;var off=Render(d);d.shadowMode=1;var hard=Render(d);d.shadowMode=2;var soft=Render(d);
                int shadowChange=Difference(off,hard),softChange=Difference(hard,soft);
                Check(shadowChange>100 && softChange>0,"Shadow modes did not change rendered output.");
                report.AppendLine($"Shadows off/hard differences={shadowChange}; hard/soft differences={softChange}.");
                Save(off,"shadow_off");Save(hard,"shadow_hard");Save(soft,"shadow_soft");Free(off,soft);
                d.shadowMode=1;d.lightYaw=65;var movedLight=Render(d);int lightChange=Difference(hard,movedLight);
                Check(lightChange>100,"Light direction did not alter shading.");report.AppendLine($"Light yaw -35 -> 65: {lightChange} changed pixels.");Save(movedLight,"light_65");Free(hard,movedLight);
                d.lightYaw=-35;d.debugView=2;var normals=Render(d);Save(normals,"normal_view");Free(normals);d.debugView=0;
                foreach(int height in new[]{90,180,360})
                {
                    var low=Render(d,height*16/9,height);Save(low,$"source_{height}");Free(low);
                    Check(d.Target.antiAliasing==1 && d.Target.filterMode==FilterMode.Point && !d.Target.useMipMap,"Invalid output sampling settings.");
                    var enlarged=RenderTexture.GetTemporary(1280,720,0,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
                    Texture2D output;
                    try {Graphics.Blit(d.Target,enlarged);output=Read(enlarged);}
                    finally {RenderTexture.active=old;RenderTexture.ReleaseTemporary(enlarged);}
                    var pixels=output.GetPixels32();int scale=720/height,bad=0;
                    for(int y=0;y<720;y++)for(int x=0;x<1280;x++)if(!pixels[y*1280+x].Equals(pixels[(y/scale*scale)*1280+x/scale*scale]))bad++;
                    Check(bad==0,"Point enlargement mixed source texels.");report.AppendLine($"{height*16/9}x{height} -> 1280x720: uniform {scale}x{scale} blocks.");Save(output,$"point_{height}");Free(output);
                }
                d.resolution=0;d.RefreshOutput();d.ApplySettings(true);Check(d.PixelScale==1,"Native reference is not 1:1.");
                yield return null;yield return null;yield return null;
                var automatic=Read(d.Target);d.sceneCamera.Render();var manual=Read(d.Target);
                int frameDifference=Difference(automatic,manual);Check(frameDifference==0,"Automatic frame differs from explicit render.");
                report.AppendLine($"Native {d.Target.width}x{d.Target.height}, 1:1; automatic/explicit differences={frameDifference}.");Save(automatic,"native_frame");Free(automatic,manual);
                foreach(var area in new[]{new Rect(0,0,960,592),new Rect(0,0,1280,720),new Rect(0,0,100,50)})
                {
                    var rect=LoFiDemo.Fit(area,320,180,out float scale);Check(rect.width<=area.width+.001f && rect.height<=area.height+.001f,"Layout overflow");
                    Check(scale<1 || scale==Mathf.Floor(scale),"Noninteger enlargement");
                }
                report.AppendLine("PASS. Geometry, normals, palette, light/shadows, sampling and automatic frames verified. No game-source match or performance benchmark.");
                File.WriteAllText(Path.Combine(Output,"validation.txt"),report.ToString());Debug.Log(report.ToString());passed=true;
            }
            finally
            {
                if(!passed)File.WriteAllText(Path.Combine(Output,"validation.txt"),report+"\nFAILED / interrupted; see Console.\n");
                JsonUtility.FromJsonOverwrite(state,d);d.ApplySettings(true);d.RefreshOutput();running=false;
            }
        }
    }
}
