using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TechArtLab.Balatro.Drag.Editor
{
    public static class CardDragExperiment
    {
        const string Root="Assets/_Lab/Experiments/003_BalatroCardDrag";
        public const string ScenePath=Root+"/Scenes/003_CardDrag_Minimal.unity";
        [MenuItem("TechArtLab/003/Create or Open Minimal")]
        public static void CreateOrOpen()
        {
            if(Application.isPlaying) throw new Exception("Exit Play before opening 003.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if(File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            foreach(var folder in new[]{"Scenes","Materials","Models","Textures"})
                if(!AssetDatabase.IsValidFolder(Root+"/"+folder)) AssetDatabase.CreateFolder(Root,folder);
            var shader=Shader.Find("TechArtLab/003/CardDrag");
            if(!shader || ShaderUtil.ShaderHasError(shader)) throw new Exception("003 Shader compilation failed.");
            // Copy the lab's own diagnostic artwork. The saved scene has no dependency
            // on 002's materials, scripts or texture; no shared framework is introduced.
            string texturePath=Root+"/Textures/DragTestCard.png";
            if(!File.Exists(texturePath) && !AssetDatabase.CopyAsset("Assets/_Lab/Experiments/002_BalatroCardDissolve/Textures/LockTestCard.png",texturePath))
                throw new Exception("Could not copy the lab's lock test artwork.");
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            string meshPath=Root+"/Models/CardQuad.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!mesh)
            {
                mesh=new Mesh {name="CardQuad"};
                mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
                mesh.triangles=new[]{0,2,1,0,3,2}; mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,meshPath);
            }
            var material=MakeMaterial("CardDrag",shader,texture,Color.white);
            var shadowMaterial=MakeMaterial("CardShadow",shader,texture,new Color(0,0,0,.3f));
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera)).GetComponent<Camera>();
            camera.tag="MainCamera"; camera.transform.position=new Vector3(0,0,-10);
            camera.orthographic=true; camera.orthographicSize=3.6f;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.12f,.19f,.22f);
            camera.allowHDR=false; camera.allowMSAA=false; camera.nearClipPlane=.1f; camera.farClipPlane=50;
            var controls=new GameObject("003 - Drag controls",typeof(CardDragController),typeof(CardDragDemo));
            var controller=controls.GetComponent<CardDragController>();
            controller.viewCamera=camera;
            controller.card=MakeQuad("Card",mesh,material,1);
            controller.shadow=MakeQuad("Shadow",mesh,shadowMaterial,0);
            controller.card.position=new Vector3(1.5f,0,0);
            controller.Initialize();
            controls.GetComponent<CardDragDemo>().controller=controller;
            EditorSceneManager.SaveScene(scene,ScenePath); AssetDatabase.SaveAssets();
            Selection.activeGameObject=controls;
            Debug.Log("003 ready. Drag card; R reset; T replay; Space pause replay.");
        }
        static Material MakeMaterial(string name,Shader shader,Texture2D texture,Color color)
        {
            string path=Root+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material) return material;
            material=new Material(shader){name=name}; material.SetTexture("_BaseMap",texture); material.SetColor("_Color",color);
            AssetDatabase.CreateAsset(material,path); return material;
        }
        static Transform MakeQuad(string name,Mesh mesh,Material material,int order)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.localScale=new Vector3(3.2f*71/95,3.2f,1);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;
            go.GetComponent<MeshRenderer>().sortingOrder=order;
            return go.transform;
        }

        [MenuItem("TechArtLab/003/Validate and Capture")]
        public static void ValidateAndCapture()
        {
            var demo=UnityEngine.Object.FindAnyObjectByType<CardDragDemo>();
            if(!demo || !Application.isPlaying) throw new Exception("Open 003 and enter Play first.");
            var live=demo.controller;
            // Test an isolated controller with the same geometry/camera. The live card
            // is used only for captures, so validation cannot consume user listeners.
            var testGO=new GameObject("003 validation temporary");
            var testCard=new GameObject("Test card");
            var testShadow=new GameObject("Test shadow");
            var c=testGO.AddComponent<CardDragController>();
            testCard.transform.SetPositionAndRotation(live.card.position,Quaternion.identity);
            testCard.transform.localScale=live.card.localScale;
            c.card=testCard.transform; c.shadow=testShadow.transform; c.viewCamera=live.viewCamera; c.Initialize();
            string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../References/003_BalatroCardDrag/analysis/unity"));
            Directory.CreateDirectory(output);
            var report=new StringBuilder("003 validation\nUnity "+Application.unityVersion+" / "+SystemInfo.graphicsDeviceType+"\n");
            try
            {
                int starts=0,ends=0;
                Vector2 screen=c.viewCamera.WorldToScreenPoint(new Vector3(c.Anchor.x,c.Anchor.y,c.card.position.z));
                Check(c.ScreenToPlane(screen,out var planePoint) && Vector2.Distance(planePoint,c.Anchor)<.0001f,"Screen/world mapping failed");
                c.dragStarted.AddListener(()=>{ Check(c.IsDragging,"Start event state was late"); starts++; });
                c.dragEnded.AddListener(()=>{ Check(!c.IsDragging,"End event state was late"); ends++; });
                Check(!c.TryBeginDrag(c.Anchor+new Vector2(20,0)),"Outside hit incorrectly captured");
                Vector2 grip=(Vector2)c.card.TransformPoint(new Vector3(.4f,.3f,0));
                Vector2 initial=c.Position;
                Check(c.TryBeginDrag(grip) && c.IsDragging,"Valid grip rejected");
                Check(!c.TryBeginDrag(grip) && starts==1,"Duplicate down captured twice");
                Check(Vector2.Distance(initial,c.Position)<.000001f,"Pose jumped on pointer down");
                c.Step(1/60f);
                Check(Vector2.Distance(initial,c.Position)<.00001f,"Stationary off-center grip jumped");
                Vector2 moved=grip+new Vector2(1,.4f);
                c.MovePointer(moved); for(int i=0;i<240;i++) c.Step(1/120f);
                Vector2 settledGrip=c.card.TransformPoint(new Vector3(.4f,.3f,0));
                Check(Vector2.Distance(moved,settledGrip)<.01f,"Local grip not preserved after settling");
                Check(Mathf.Abs(c.Angle)<.2f,"Card did not straighten after stopping");
                c.EndDrag(); c.EndDrag(); Check(ends==1,"Release event count wrong");
                for(int i=0;i<360;i++) c.Step(1/120f);
                Check(c.IsSettled && Vector2.Distance(c.Anchor,c.Position)<.001f,"Return did not settle at anchor");

                c.returnToAnchor=false; c.TryBeginDrag(c.Anchor);
                for(int i=1;i<=60;i++) { c.MovePointer(c.Anchor+new Vector2(i/60f,0)); c.Step(1/60f); }
                Vector2 release=c.Position; c.EndDrag(); for(int i=0;i<180;i++) c.Step(1/60f);
                Check(Vector2.Distance(c.Position,release)<.0001f && c.IsSettled,"Stay-on-release moved center");
                c.returnToAnchor=true; c.ResetToAnchor(); c.TryBeginDrag(c.Anchor);
                c.MovePointer(c.Anchor+new Vector2(2,0)); c.Step(.1f); c.EndDrag();
                Vector2 before=c.Position;
                Check(c.TryBeginDrag(before),"Cannot re-grab during return");
                Check(Vector2.Distance(before,c.Position)<.00001f,"Re-grab snapped position");
                c.SendMessage("OnApplicationFocus",false); Check(!c.IsDragging,"Focus loss left capture active");
                c.TryBeginDrag(c.Position); c.enabled=false; Check(!c.IsDragging,"Disable left capture active"); c.enabled=true;
                c.ResetToAnchor();
                // Hit-test rotated local coordinates, not an axis-aligned screen box.
                c.card.rotation=Quaternion.Euler(0,0,30);
                Check(c.HitTest(c.card.TransformPoint(new Vector3(.4f,.4f,0))),"Rotated card inner point missed");
                Check(!c.HitTest(c.card.TransformPoint(new Vector3(.6f,0,0))),"Rotated card outer point accepted");
                c.ResetToAnchor();
                report.AppendLine("Hit/miss, immediate state/events, off-center grip, return/stay, re-grab, focus loss, disable, rotated hit test: PASS.");

                Vector3[] baseline=null;
                foreach(int fps in new[]{120,60,30})
                {
                    c.ResetToAnchor(); c.TryBeginDrag(c.Anchor);
                    var samples=new Vector3[5]; float max=0;
                    for(int i=1;i<=fps*5;i++)
                    {
                        float t=(float)i/fps;
                        if(i<=fps*3) c.MovePointer(c.Anchor+CardDragDemo.Trajectory(t));
                        c.Step(1f/fps);
                        if(i==fps*3) c.EndDrag();
                        max=Mathf.Max(max,Mathf.Abs(c.Angle));
                        if(i%fps==0) samples[i/fps-1]=new Vector3(c.Position.x,c.Position.y,c.Angle);
                    }
                    Check(max<=c.maxTilt+.001f && max>10,"Tilt clamp or response failed");
                    Check(c.IsSettled,"Replay did not finish settled");
                    if(baseline==null) baseline=samples;
                    float error=0,angular=0;
                    for(int i=0;i<5;i++)
                    { error=Mathf.Max(error,Vector2.Distance(samples[i],baseline[i])); angular=Mathf.Max(angular,Mathf.Abs(samples[i].z-baseline[i].z)); }
                    Check(error<.03f && angular<1,"Frame-rate response diverged");
                    report.AppendLine(fps+" Hz: max tilt="+max.ToString("F3")+" deg; sample error vs 120 Hz="+error.ToString("F5")+" units / "+angular.ToString("F4")+" deg.");
                }
                Check(starts==ends,"Input capture events unbalanced");
                c.rotationDamping=.6f; c.ResetToAnchor(); c.TryBeginDrag(c.Anchor);
                for(int i=1;i<=60;i++) { c.MovePointer(c.Anchor+new Vector2(i/30f,0)); c.Step(1/60f); }
                float releaseSign=Mathf.Sign(c.Angle); c.EndDrag(); bool crossed=false;
                for(int i=0;i<240;i++)
                {
                    c.Step(1/120f);
                    Check(!float.IsNaN(c.Angle) && Mathf.Abs(c.Angle)<=c.maxTilt+.001f,"Underdamped mode unstable");
                    crossed |= c.Angle*releaseSign<-.02f;
                }
                Check(crossed,"Experimental low damping did not overshoot zero");
                report.AppendLine("Optional rotation damping=0.6: bounded zero-crossing rebound observed; default remains 1.0.");
                ValidateGripIndependence(c,report);
                ValidateTuning(c,report);
                var shader=live.card.GetComponent<MeshRenderer>().sharedMaterial.shader;
                Check(!ShaderUtil.ShaderHasError(shader),"Card shader error");
                report.AppendLine("Shader compilation: PASS. Original game's spring/hover transition not verified.");
                File.WriteAllText(Path.Combine(output,"validation.txt"),report.ToString());
                Debug.Log(report.ToString());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO); UnityEngine.Object.DestroyImmediate(testCard); UnityEngine.Object.DestroyImmediate(testShadow);
            }
            CaptureReplay(demo,output);
        }
        static void Check(bool condition,string error) { if(!condition) throw new Exception(error); }
        struct DragSample
        {
            public Vector2 position,velocity,target;
            public float angle,targetAngle;
            public CardDragController.Phase state;
        }
        static void ValidateGripIndependence(CardDragController c,StringBuilder report)
        {
            c.rotationDamping=1;
            float maxPositionError=0,maxVelocityError=0,maxAngleError=0;
            foreach(int fps in new[]{30,60,120})
            foreach(bool regrab in new[]{false,true})
            foreach(bool returnHome in new[]{false,true})
            {
                c.returnToAnchor=returnHome;
                var baseline=GripTrace(c,Vector2.zero,fps,regrab);
                foreach(float x in new[]{-.45f,0,.45f})
                foreach(float y in new[]{-.45f,0,.45f})
                {
                    var samples=GripTrace(c,new Vector2(x,y),fps,regrab);
                    for(int i=0;i<samples.Length;i++)
                    {
                        maxPositionError=Mathf.Max(maxPositionError,Vector2.Distance(samples[i].position,baseline[i].position),
                            Vector2.Distance(samples[i].target,baseline[i].target));
                        maxVelocityError=Mathf.Max(maxVelocityError,Vector2.Distance(samples[i].velocity,baseline[i].velocity));
                        maxAngleError=Mathf.Max(maxAngleError,Mathf.Abs(samples[i].angle-baseline[i].angle),
                            Mathf.Abs(samples[i].targetAngle-baseline[i].targetAngle));
                        Check(samples[i].state==baseline[i].state,"Grip position changed state timing");
                    }
                }
            }
            Check(maxPositionError<.0001f && maxVelocityError<.001f && maxAngleError<.01f,
                $"Grip changed motion: position/target={maxPositionError:F6}, velocity={maxVelocityError:F6}, angle={maxAngleError:F6}");
            report.AppendLine($"9 grip locations, 30/60/120 Hz, upright/tilted re-grab, return/stay: PASS. Maximum differences: position/target={maxPositionError:F6}, velocity={maxVelocityError:F6}, angle={maxAngleError:F6} deg.");
            c.returnToAnchor=true; c.ResetToAnchor();
        }
        static DragSample[] GripTrace(CardDragController c,Vector2 local,int fps,bool regrab)
        {
            c.ResetToAnchor();
            if(regrab)
            {
                c.TryBeginDrag(c.Anchor);
                for(int i=1;i<=24;i++) {c.MovePointer(c.Anchor+new Vector2(i*.1f,0)); c.Step(1/120f);}
                c.EndDrag(); c.Step(1/120f);
                Check(Mathf.Abs(c.Angle)>1,"Re-grab test did not start tilted");
            }
            Vector2 grip=c.card.TransformPoint(new Vector3(local.x,local.y,0));
            Vector2 before=c.Position; float angleBefore=c.Angle;
            Check(c.TryBeginDrag(grip),"Grip test hit rejected");
            Check(c.Position==before && c.Angle==angleBefore,"Grip caused a pose jump");
            var samples=new DragSample[fps*5];
            for(int i=1;i<=samples.Length;i++)
            {
                if(i<=fps*3) c.MovePointer(grip+CardDragDemo.Trajectory((float)i/fps));
                c.Step(1f/fps);
                if(i==fps*3) c.EndDrag();
                samples[i-1]=new DragSample {position=c.Position,velocity=c.Velocity,target=c.TargetPosition,
                    angle=c.Angle,targetAngle=c.TargetAngle,state=c.State};
            }
            return samples;
        }
        static void ValidateTuning(CardDragController c,StringBuilder report)
        {
            c.rotationDamping=1; c.maxTilt=60; c.speedForMaxTilt=6;
            c.positionResponse=55; c.rotationResponse=40; c.straightenResponse=65;
            // Pointer and spring velocity are not identical. Use 7 H/s input to
            // put actual card velocity above the 6 H/s saturation threshold.
            float slow=RunConstantSpeed(c,1),fast=RunConstantSpeed(c,7),reverse=RunConstantSpeed(c,-7);
            Check(Mathf.Abs(slow+10)<.2f && Mathf.Abs(fast+60)<.2f && Mathf.Abs(reverse-60)<.2f,
                "Full-tilt speed did not separate low-speed tilt, maximum tilt and direction");
            c.speedForMaxTilt=12;
            float harder=RunConstantSpeed(c,6);
            Check(Mathf.Abs(harder+30)<1,"Increasing required speed did not reduce tilt at equal speed");
            report.AppendLine($"Tuning: max=60, full speed=6 H/s; actual tilt at pointer 1 / 7 / -7 H/s = {slow:F2} / {fast:F2} / {reverse:F2} deg. Full speed=12 at pointer 6 H/s = {harder:F2} deg.");

            c.maxTilt=0;
            float[] gaps=new float[2];
            for(int n=0;n<2;n++)
            {
                c.positionResponse=n==0 ? 22 : 55; c.ResetToAnchor(); c.TryBeginDrag(c.Anchor);
                c.MovePointer(c.Anchor+Vector2.right);
                for(int i=0;i<12;i++) c.Step(1/120f);
                gaps[n]=Vector2.Distance(c.Position,c.TargetPosition);
            }
            Check(gaps[1]<gaps[0]*.4f,"Faster follow did not reduce position lag");
            c.maxTilt=60; c.speedForMaxTilt=6; c.positionResponse=120;
            float[] held=new float[2],released=new float[2];
            for(int n=0;n<2;n++)
            {
                c.straightenResponse=n==0 ? 12 : 65;
                RunConstantSpeed(c,6);
                for(int i=0;i<18;i++) c.Step(1/120f);
                held[n]=Mathf.Abs(c.Angle);
                RunConstantSpeed(c,6); c.returnToAnchor=false; c.EndDrag();
                for(int i=0;i<18;i++) c.Step(1/120f);
                released[n]=Mathf.Abs(c.Angle);
            }
            Check(held[1]<held[0]*.4f && released[1]<released[0]*.4f,"Straighten speed did not improve held/released recovery");
            report.AppendLine($"Follow 22 / 55: gap after 0.1 s = {gaps[0]:F4} / {gaps[1]:F4} units. Straighten 12 / 65: residual tilt after 0.15 s held = {held[0]:F2} / {held[1]:F2}, released = {released[0]:F2} / {released[1]:F2} deg. PASS.");
        }
        static float RunConstantSpeed(CardDragController c,float heightsPerSecond)
        {
            c.ResetToAnchor(); c.TryBeginDrag(c.Anchor);
            for(int i=1;i<=120;i++)
            {
                c.MovePointer(c.Anchor+Vector2.right*(heightsPerSecond*Mathf.Abs(c.card.lossyScale.y)*i/120f));
                c.Step(1/120f);
            }
            return c.Angle;
        }
        static void CaptureReplay(CardDragDemo demo,string output)
        {
            var camera=demo.controller.viewCamera;
            var target=camera.targetTexture; var active=RenderTexture.active;
            var rt=new RenderTexture(1280,720,24);
            try
            {
                camera.targetTexture=rt; demo.StartReplay();
                int[] frames={0,90,150,210,330,366,540};
                string[] names={"idle","slow_right","fast_left","fast_right","stopped","released","home"};
                int n=0;
                for(int frame=0;frame<=540;frame++)
                {
                    if(frame>0) demo.AdvanceReplay(1/120f);
                    if(n>=frames.Length || frame!=frames[n]) continue;
                    camera.Render(); RenderTexture.active=rt;
                    var png=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    png.ReadPixels(new Rect(0,0,1280,720),0,0); png.Apply();
                    File.WriteAllBytes(Path.Combine(output,names[n]+".png"),png.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(png); n++;
                }
            }
            finally
            {
                camera.targetTexture=target; RenderTexture.active=active;
                rt.Release(); UnityEngine.Object.DestroyImmediate(rt); demo.ResetDemo();
            }
        }
    }
}
