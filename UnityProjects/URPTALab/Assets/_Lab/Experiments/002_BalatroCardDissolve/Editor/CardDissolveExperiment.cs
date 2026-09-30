using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TechArtLab.Balatro.Dissolve.Editor
{
    public static class CardDissolveExperiment
    {
        public const string Root = "Assets/_Lab/Experiments/002_BalatroCardDissolve";
        public const string ScenePath = Root + "/Scenes/002_CardDissolve_Minimal.unity";

        [MenuItem("TechArtLab/002/Create or Open Minimal")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before opening the scene.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            var shader = Shader.Find("TechArtLab/002/CardDissolve");
            if (!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("Dissolve shader missing or failed compilation.");
            foreach (var folder in new[] {"Scenes","Materials","Textures","Models"})
                if (!AssetDatabase.IsValidFolder(Root+"/"+folder)) AssetDatabase.CreateFolder(Root,folder);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Models/CardQuad.asset");
            if (!mesh)
            {
                mesh = new Mesh { name = "CardQuad" };
                mesh.vertices = new[] {new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                mesh.uv = new[] {Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
                mesh.triangles = new[] {0,2,1,0,3,2};
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,Root+"/Models/CardQuad.asset");
            }
            var ace = MakeTexture(false);
            var locked = MakeTexture(true);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/CardDissolve.mat");
            if (!material)
            {
                material = new Material(shader) {name="CardDissolve"};
                material.SetTexture("_BaseMap",ace);
                AssetDatabase.CreateAsset(material,Root+"/Materials/CardDissolve.mat");
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera = new GameObject("Main Camera",typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0,0,-10);
            camera.orthographic = true; camera.orthographicSize = 3.6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f,.19f,.22f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 50;
            camera.allowHDR = false; camera.allowMSAA = false;
            var controller = new GameObject("002 - Dissolve controls",typeof(CardDissolveDemo)).GetComponent<CardDissolveDemo>();
            controller.viewCamera = camera;
            controller.aceTexture = ace; controller.lockTexture = locked;
            controller.cardRenderer = MakeQuad("Card",mesh,material,new Vector3(1.35f,0,0),1);
            controller.shadowRenderer = MakeQuad("Shadow",mesh,material,new Vector3(1.49f,-.16f,.1f),0);
            controller.Apply();
            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = controller.gameObject;
            Debug.Log("002 Minimal created. Play: 1 exit, 2 entry, Space pause, F freeze field.");
        }

        static MeshRenderer MakeQuad(string name,Mesh mesh,Material material,Vector3 position,int order)
        {
            var go = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.position = position; go.transform.localScale = new Vector3(2.84f,3.8f,1);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.sortingOrder = order;
            return renderer;
        }

        // Original diagnostic artwork, generated in Unity; no extracted game texture.
        static Texture2D MakeTexture(bool locked)
        {
            string path = Root+"/Textures/"+(locked ? "LockTestCard" : "AceTestCard")+".png";
            if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var tex = new Texture2D(142,190,TextureFormat.RGBA32,false);
            Color paper = locked ? new Color(.67f,.73f,.78f) : new Color(.96f,.94f,.85f);
            Color ink = new Color(.13f,.19f,.24f);
            for(int y=0;y<190;y++) for(int x=0;x<142;x++)
            {
                float cx = Mathf.Max(Mathf.Abs(x-70.5f)-61.5f,0);
                float cy = Mathf.Max(Mathf.Abs(y-94.5f)-85.5f,0);
                bool outside = cx*cx+cy*cy>64;
                bool border = x<4 || x>137 || y<4 || y>185;
                Color color = outside ? Color.clear : border ? ink : paper;
                if (!outside && !border)
                {
                    if (locked)
                    {
                        // A chain of small outlined links on each diagonal.
                        for (int i=0;i<10;i++)
                        {
                            float linkX=17+i*12, linkY=24+i*15.5f;
                            float a = Mathf.Pow((x-linkX)/5,2)+Mathf.Pow((y-linkY)/8,2);
                            float b = Mathf.Pow((x-(142-linkX))/5,2)+Mathf.Pow((y-linkY)/8,2);
                            if ((a>.5f && a<1) || (b>.5f && b<1)) color=ink;
                        }
                        float ring = (x-71)*(x-71)+(y-116)*(y-116);
                        if (y>112 && ring<400 && ring>196) color=ink;
                        if (Mathf.Abs(x-71)<27 && y>65 && y<115) color=ink;
                        if (Mathf.Abs(x-71)<23 && y>69 && y<111) color=new Color(.44f,.52f,.60f);
                        if ((x-71)*(x-71)+(y-94)*(y-94)<36 || (Mathf.Abs(x-71)<3 && y>79 && y<94)) color=ink;
                    }
                    else
                    {
                        float px=(x-71)/31f, py=(y-99)/34f;
                        bool spade = py>=0 && py<1.15f && Mathf.Abs(px)<(1.15f-py)*.9f;
                        spade |= (px-.43f)*(px-.43f)+(py+.08f)*(py+.08f)<.34f;
                        spade |= (px+.43f)*(px+.43f)+(py+.08f)*(py+.08f)<.34f;
                        spade |= py<-.12f && py>-.95f && Mathf.Abs(px)<.12f+(-py)*.22f;
                        if (spade) color=ink;
                        int gx=(x-10)/2, gy=(178-y)/2;
                        int rx=(131-x)/2, ry=(y-11)/2;
                        if (GlyphA(gx,gy) && x>=10 && y<=178 || GlyphA(rx,ry) && x<=131 && y>=11) color=ink;
                    }
                }
                tex.SetPixel(x,y,color);
            }
            tex.Apply(); File.WriteAllBytes(path,tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode=FilterMode.Point; importer.mipmapEnabled=false; importer.wrapMode=TextureWrapMode.Clamp;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.alphaIsTransparency=true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static bool GlyphA(int x,int y)
        {
            if(x<0 || x>=5 || y<0 || y>=7) return false;
            string[] rows={"01110","11011","10001","10001","11111","10001","10001"};
            return rows[y][x]=='1';
        }

        [MenuItem("TechArtLab/002/Validate and Capture Fixed States")]
        public static void ValidateAndCapture()
        {
            var demo = UnityEngine.Object.FindAnyObjectByType<CardDissolveDemo>();
            if (!demo || !Application.isPlaying) throw new Exception("Open 002 Minimal and enter Play first.");
            if(demo.aceTexture.width!=142 || demo.aceTexture.height!=190 || demo.lockTexture.width!=142 || demo.lockTexture.height!=190)
                throw new Exception("Diagnostic artwork must retain its native 142x190 size.");
            if (ShaderUtil.ShaderHasError(demo.cardRenderer.sharedMaterial.shader)) throw new Exception("Shader compilation errors.");
            var mesh=demo.cardRenderer.GetComponent<MeshFilter>().sharedMesh;
            if(mesh.vertexCount!=4 || mesh.triangles.Length!=6) throw new Exception("Expected four vertices and six indices.");
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../References/002_BalatroDragDissolveParticles/analysis/unity"));
            Directory.CreateDirectory(output);
            // Save only fields changed by validation; never serialize scene references
            // through JSON (large runtime instance IDs can be lost on round-trip).
            var savedLook=demo.look; var savedView=demo.view;
            float savedD=demo.dissolve, savedTime=demo.fieldTime, savedDuration=demo.duration;
            bool savedFreeze=demo.freezeField, savedControls=demo.showControls;
            Color savedInner=demo.innerEdge, savedOuter=demo.outerEdge;
            int savedSeed=demo.patternSeed; bool savedRandom=demo.randomizeOnReplay;
            var camera=demo.viewCamera;
            var target=camera.targetTexture; var active=RenderTexture.active;
            var background=camera.backgroundColor;
            var shadowPosition=demo.shadowRenderer.transform.position;
            var rt=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            var report=new StringBuilder("002 validation\nUnity "+Application.unityVersion+" / "+SystemInfo.graphicsDeviceType+" / "+QualitySettings.activeColorSpace+"\n1280x720 orthographic, size 3.6; 4 vertices / 6 indices\n");
            try
            {
                camera.targetTexture=rt;
                demo.freezeField=true; demo.showControls=false;
                demo.patternSeed=0; demo.randomizeOnReplay=false;
                foreach(CardDissolveDemo.Look look in Enum.GetValues(typeof(CardDissolveDemo.Look)))
                {
                    demo.SelectLook(look); demo.view=CardDissolveDemo.View.Color;
                    foreach(float d in new[]{0f,.25f,.5f,.75f,1f})
                    {
                        demo.SetProgress(d);
                        Save(camera,rt,Path.Combine(output,look+"_"+d.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"));
                    }
                }
                // GPU evidence: compare visible mask with independently drawn shadow,
                // aligning their transforms temporarily. Card is white on black;
                // the actual shadow branch is black at opacity 1 on white.
                demo.SelectLook(CardDissolveDemo.Look.OrangeExit);
                demo.view=CardDissolveDemo.View.Mask; camera.backgroundColor=Color.black;
                int previous=int.MaxValue;
                foreach(float d in new[]{0f,.25f,.5f,.75f,1f})
                {
                    demo.SetProgress(d);
                    var pixels=Read(camera,rt);
                    int count=CountVisible(pixels);
                    if(d==0 && count<100000) throw new Exception("Visible endpoint missing card.");
                    if(d==1 && count!=0) throw new Exception("Hidden endpoint leaves pixels.");
                    if(count>previous) throw new Exception("Fixed-time sampled mask coverage increased.");
                    previous=count;
                    report.AppendLine("Orange mask d="+d+" visible pixels="+count);
                }
                demo.SetProgress(.5f);
                // Additional seed tests use the same preset, time and progress.
                var originalMask=Read(camera,rt);
                demo.patternSeed=1234; demo.Apply();
                var seedMask=Read(camera,rt,Path.Combine(output,"seed_1234_mask.png"));
                demo.patternSeed=5678; demo.Apply();
                var otherMask=Read(camera,rt,Path.Combine(output,"seed_5678_mask.png"));
                demo.patternSeed=1234; demo.Apply();
                var repeatMask=Read(camera,rt);
                int changed=0;
                for(int i=0;i<seedMask.Length;i++)
                {
                    if(!seedMask[i].Equals(repeatMask[i])) throw new Exception("Same seed did not reproduce the mask.");
                    if(!seedMask[i].Equals(otherMask[i])) changed++;
                }
                if(changed<1000) throw new Exception("Different seeds did not meaningfully change the mask.");
                foreach(int seed in new[]{0,1,1234,5678,65535})
                {
                    demo.patternSeed=seed;
                    demo.SetProgress(0);
                    if(CountVisible(Read(camera,rt))!=104912) throw new Exception("Seed changed visible endpoint.");
                    demo.SetProgress(1);
                    if(CountVisible(Read(camera,rt))!=0) throw new Exception("Seed leaves pixels at hidden endpoint.");
                }
                demo.patternSeed=0; demo.SetProgress(.5f);
                var restoredOriginal=Read(camera,rt);
                for(int i=0;i<originalMask.Length;i++)
                    if(!originalMask[i].Equals(restoredOriginal[i])) throw new Exception("Original pattern was not restored.");
                report.AppendLine("Seed reproducibility/original reset: PASS; seeds 1234/5678 differ at "+changed+" pixels; five seed endpoints: PASS.");
                // Check the real shadow with a nonzero seed too.
                demo.patternSeed=1234; demo.Apply();
                var cardMask=Read(camera,rt,Path.Combine(output,"mask_card.png"));
                demo.shadowRenderer.transform.position=demo.cardRenderer.transform.position;
                demo.shadowRenderer.enabled=true; demo.cardRenderer.enabled=false;
                var shadowBlock=new MaterialPropertyBlock();
                demo.shadowRenderer.GetPropertyBlock(shadowBlock);
                shadowBlock.SetFloat("_Shadow",1); shadowBlock.SetFloat("_ShadowOpacity",1);
                demo.shadowRenderer.SetPropertyBlock(shadowBlock);
                camera.backgroundColor=Color.white;
                var shadowMask=Read(camera,rt,Path.Combine(output,"mask_shadow.png"));
                for(int i=0;i<cardMask.Length;i++)
                    if(Mathf.Abs(cardMask[i].r-(255-shadowMask[i].r))>1) throw new Exception("Shadow/card visibility mismatch at "+i+": "+cardMask[i]+" vs "+shadowMask[i]);
                report.AppendLine("Aligned card/shadow branch masks: pixel-identical (inverted RGB, tolerance 1/255) at d=0.5, seed=1234.");
                demo.cardRenderer.enabled=true;
                demo.patternSeed=0;
                camera.backgroundColor=Color.black;
                demo.shadowRenderer.transform.position=shadowPosition;
                demo.view=CardDissolveDemo.View.Mask;
                foreach(var look in new[]{CardDissolveDemo.Look.OrangeExit,CardDissolveDemo.Look.GreenEntry})
                {
                    demo.SelectLook(look); demo.SetProgress(1);
                    if(CountVisible(Read(camera,rt))!=0) throw new Exception("Preset endpoint not hidden.");
                    demo.SetProgress(0);
                    if(CountVisible(Read(camera,rt))<100000) throw new Exception("Preset endpoint not visible.");
                }
                demo.duration=1;
                demo.PlayExit(); float frozen=demo.fieldTime;
                demo.Tick(.25f);
                if(Mathf.Abs(demo.dissolve-.25f)>.0001f || demo.fieldTime!=frozen) throw new Exception("Exit/freeze clock failed.");
                demo.TogglePlayback(); demo.Tick(.25f);
                if(Mathf.Abs(demo.dissolve-.25f)>.0001f) throw new Exception("Pause failed.");
                demo.TogglePlayback(); demo.Tick(1);
                if(demo.dissolve!=1 || demo.Playing) throw new Exception("Exit completion failed.");
                demo.PlayEntry(); demo.Tick(.25f);
                if(Mathf.Abs(demo.dissolve-.75f)>.0001f) throw new Exception("Entry direction failed.");
                demo.Tick(1);
                if(demo.dissolve!=0 || demo.Playing) throw new Exception("Entry completion failed.");
                demo.freezeField=false; frozen=demo.fieldTime; demo.Tick(.125f);
                if(Mathf.Abs(demo.fieldTime-frozen-.125f)>.0001f) throw new Exception("Live field clock failed.");
                demo.SetProgress(.37f);
                if(demo.Playing || Mathf.Abs(demo.dissolve-.37f)>.0001f) throw new Exception("Scrub failed.");
                report.AppendLine("Both preset endpoints, exit/entry clocks, pause/resume, completion, field freeze/live, scrub: PASS.");
                demo.randomizeOnReplay=true; demo.freezeField=true;
                int before=demo.patternSeed;
                demo.PlayExit();
                if(demo.patternSeed==before) throw new Exception("Exit did not pick a new seed.");
                before=demo.patternSeed; demo.Tick(.2f); demo.TogglePlayback(); demo.TogglePlayback();
                if(demo.patternSeed!=before) throw new Exception("Seed changed during playback/pause/resume.");
                demo.PlayEntry();
                if(demo.patternSeed==before) throw new Exception("Entry did not pick a new seed.");
                demo.randomizeOnReplay=false; before=demo.patternSeed; demo.PlayExit();
                if(demo.patternSeed!=before) throw new Exception("Fixed-seed playback changed seed.");
                report.AppendLine("Automatic new seeds / stable seed during playback and pause / fixed-seed playback: PASS.");
                report.AppendLine("Shader errors: false. 10 color captures saved; visual inspection required.");
                File.WriteAllText(Path.Combine(output,"validation.txt"),report.ToString());
                Debug.Log(report.ToString());
            }
            finally
            {
                camera.targetTexture=target; camera.backgroundColor=background; RenderTexture.active=active;
                demo.cardRenderer.enabled=true; demo.shadowRenderer.transform.position=shadowPosition;
                demo.SelectLook(savedLook); demo.view=savedView;
                demo.fieldTime=savedTime; demo.duration=savedDuration;
                demo.freezeField=savedFreeze; demo.showControls=savedControls;
                demo.innerEdge=savedInner; demo.outerEdge=savedOuter;
                demo.patternSeed=savedSeed; demo.randomizeOnReplay=savedRandom;
                demo.SetProgress(savedD);
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            }
        }
        static int CountVisible(Color32[] pixels)
        {
            int count=0; foreach(var p in pixels) if(p.r>127) count++; return count;
        }
        static Color32[] Read(Camera camera,RenderTexture rt,string path=null)
        {
            camera.Render(); RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply();
            if(path!=null) File.WriteAllBytes(path,image.EncodeToPNG());
            var pixels=image.GetPixels32(); UnityEngine.Object.DestroyImmediate(image); return pixels;
        }
        static void Save(Camera camera,RenderTexture rt,string path) { Read(camera,rt,path); }
    }
}
