using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TechArtLab.PixelStable.Editor
{
    public static class Validate007
    {
        static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../References/007_PixelStable3D/analysis/unity"));
        static bool running;
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        static Texture2D Read(RenderTexture rt)
        {
            var old = RenderTexture.active;
            try
            {
                RenderTexture.active = rt;
                var t = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                t.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); t.Apply(); return t;
            }
            finally { RenderTexture.active = old; }
        }
        static void Render(PixelStableDemo d, Vector3 position)
        {
            d.ApplyPosition(position); d.continuousCamera.Render(); d.snappedCamera.Render();
        }
        static Texture2D Present(PixelStableDemo d, int mode, int scale)
        {
            var uv = d.SampleRect(mode);
            var rt = RenderTexture.GetTemporary(d.Width * scale, d.Height * scale, 0, RenderTextureFormat.ARGB32);
            var old = RenderTexture.active;
            try { Graphics.Blit(d.Source(mode), rt, uv.size, uv.position); return Read(rt); }
            finally { RenderTexture.active = old; RenderTexture.ReleaseTemporary(rt); }
        }
        // Positive dx means the new image moves LEFT: new[x] should equal old[x+dx].
        static int Difference(Texture2D before, Texture2D after, int dx = 0, int dy = 0)
        {
            Check(before.width == after.width && before.height == after.height, "Image sizes differ.");
            var a = before.GetPixels32(); var b = after.GetPixels32(); int count = 0, w = before.width, h = before.height;
            for (int y = 2; y < h - 2; y++) for (int x = 2; x < w - 2; x++)
            {
                if (x + dx < 2 || x + dx >= w - 2 || y + dy < 2 || y + dy >= h - 2) continue;
                var p = a[(y + dy) * w + x + dx]; var q = b[y * w + x];
                if (Math.Abs(p.r - q.r) > 1 || Math.Abs(p.g - q.g) > 1 || Math.Abs(p.b - q.b) > 1) count++;
            }
            return count;
        }
        static int Coral(Texture2D t)
        {
            int n = 0; foreach (var p in t.GetPixels32()) if (p.r > 60 && p.r > p.g * 1.4f && p.r > p.b * 1.4f) n++; return n;
        }
        static void Save(Texture2D t, string name) => File.WriteAllBytes(Path.Combine(Output, name + ".png"), t.EncodeToPNG());
        static void Free(params Texture2D[] images) { foreach (var t in images) Object.DestroyImmediate(t); }
        [MenuItem("TechArtLab/007/Validate and Capture")]
        public static void Main()
        {
            var d = Object.FindAnyObjectByType<PixelStableDemo>();
            Check(Application.isPlaying && d, "Open 007 and enter Play mode.");
            Check(!running, "Validation already running."); running = true; d.StartCoroutine(Run(d));
        }
        static IEnumerator Run(PixelStableDemo d)
        {
            Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "validation.txt"), "IN PROGRESS\n");
            string state = JsonUtility.ToJson(d);
            var report = new StringBuilder($"Unity {Application.unityVersion}; {SystemInfo.graphicsDeviceType}; {QualitySettings.activeColorSpace}\n");
            bool passed = false;
            try
            {
                d.drive = false; d.ResetView(); d.replay = false; d.shadows = false; d.focusMode = -1;
                for (int resolution = 0; resolution < 4; resolution++)
                {
                    d.resolutionIndex = resolution; d.Refresh();
                    var basis = d.Orientation; float step = d.WorldPerPixel;
                    var right = basis * Vector3.right; var up = basis * Vector3.up;
                    var origin = PixelStableDemo.Snap(d.BasePosition, basis, step, out _);
                    Render(d, origin + right * step * .10f + up * step * .10f);
                    var rawA = Read(d.ContinuousTarget); var rawB = Read(d.SnappedTarget);
                    var c0 = Present(d, 2, 4); var b0 = Present(d, 1, 4);
                    Check(Coral(rawA) > 10, "Coral sphere missing or material rendering broken.");
                    Check(d.SnappedTarget.filterMode == FilterMode.Point && d.SnappedTarget.antiAliasing == 1 && !d.SnappedTarget.useMipMap, "Unexpected filtering/AA/mipmaps.");
                    Render(d, origin + right * step * .35f + up * step * .35f);
                    var rawA1 = Read(d.ContinuousTarget); var rawB1 = Read(d.SnappedTarget);
                    var c1 = Present(d, 2, 4); var b1 = Present(d, 1, 4);
                    int continuous = Difference(rawA, rawA1), stable = Difference(rawB, rawB1);
                    int compensated = Difference(c0, c1, 1, 1);
                    report.AppendLine($"{d.Width}x{d.Height}: +0.25 texel XY: continuous changed={continuous}, snapped changed={stable}, compensated aligned by 1 display px at 4x={compensated}.");
                    Check(continuous > 0 && stable == 0 && Difference(b0, b1) == 0, "Within-cell sampling stability failed.");
                    Check(compensated == 0 && Difference(c0, c1) > 0, "Compensation sign, axis, or Point presentation failed.");
                    if (resolution == 1)
                    {
                        Save(rawA, "continuous_source_010"); Save(rawA1, "continuous_source_035");
                        Save(rawB, "snapped_source_010"); Save(rawB1, "snapped_source_035");
                        Save(c0, "compensated_010"); Save(c1, "compensated_035");
                        Save(b1, "snap_only_035");
                    }
                    Free(rawA, rawA1, rawB, rawB1, c0, c1, b0, b1);
                    // Crossing a rounding boundary: snapped source jumps, compensation should cancel it.
                    Render(d, origin + right * step * .49f + up * step * .20f);
                    var boundary0 = Present(d, 2, 4); var jump0 = Present(d, 1, 4);
                    Render(d, origin + right * step * .51f + up * step * .20f);
                    var boundary1 = Present(d, 2, 4); var jump1 = Present(d, 1, 4);
                    int residualChange = Difference(boundary0, boundary1), snapChange = Difference(jump0, jump1);
                    report.AppendLine($"  boundary .49 -> .51 X: snap-only changed={snapChange}, compensated changed={residualChange} at 4x (1/255 RGB tolerance).");
                    // Numerical rasterization can touch a few exact silhouette ties after a full-pixel translation.
                    Check(snapChange > 0 && residualChange <= 64, "Snap boundary compensation discontinuity.");
                    if (resolution == 1) { Save(boundary0, "boundary_049"); Save(boundary1, "boundary_051"); }
                    Free(boundary0, boundary1, jump0, jump1);
                    foreach (var fraction in new[] {new Vector2(-.49f, -.49f), new Vector2(.49f, .49f), new Vector2(.49f, -.49f)})
                    {
                        var desired = origin + right * step * fraction.x + up * step * fraction.y;
                        d.ApplyPosition(desired); var uv = d.SampleRect(2);
                        Check(uv.xMin >= 0 && uv.yMin >= 0 && uv.xMax <= 1 && uv.yMax <= 1, "Guard border insufficient.");
                        // Verify projection + correction for a real world marker, independently of image alignment.
                        var marker = new Vector3(1.2f, .8f, -.6f);
                        var a = d.continuousCamera.WorldToViewportPoint(marker);
                        var b = d.snappedCamera.WorldToViewportPoint(marker);
                        var error = new Vector2((b.x - a.x) * (d.Width + 2), (b.y - a.y) * (d.Height + 2)) - d.ResidualPixels;
                        Check(error.magnitude < .002f, "World-to-camera correction mismatch.");
                    }
                    Check(Mathf.Abs(2 * d.snappedCamera.orthographicSize / d.SnappedTarget.height - step) < 1e-6f, "Guard changed pixel footprint.");
                }
                foreach (float angle in new[] {0f, 45f, 83f}) foreach (float size in new[] {2f, 4.1f, 8f})
                {
                    d.yaw = angle; d.orthoSize = size; d.Refresh();
                    var snapped = PixelStableDemo.Snap(d.BasePosition, d.Orientation, d.WorldPerPixel, out var residual);
                    var normal = d.Orientation * Vector3.forward;
                    Check(Mathf.Abs(Vector3.Dot(snapped - d.BasePosition, normal)) < 1e-5f, "Snap changed camera depth.");
                    Check(Mathf.Abs(residual.x) <= .5001f && Mathf.Abs(residual.y) <= .5001f, "Residual bound failed.");
                }
                foreach (var area in new[] {new Rect(0, 0, 960, 592), new Rect(0, 0, 1280, 720), new Rect(0, 0, 100, 50)})
                {
                    var rect = PixelStableDemo.Fit(area, 320, 180, out float scale);
                    Check(rect.width <= area.width + .001f && rect.height <= area.height + .001f, "Display overflows.");
                    Check(scale < 1 || scale == Mathf.Floor(scale), "Noninteger enlargement.");
                }
                d.ResetView(); d.replay = false; d.resolutionIndex = 1; d.focusMode = -1; d.Refresh(); d.ApplyPose();
                yield return null; yield return null; yield return null;
                var automatic = Read(d.SnappedTarget); d.snappedCamera.Render(); var explicitFrame = Read(d.SnappedTarget);
                int automaticDifference = Difference(automatic, explicitFrame);
                report.AppendLine($"Automatic vs explicit snapped frame: {automaticDifference} changed pixels.");
                Check(automaticDifference == 0 && Coral(automatic) > 10, "Automatic frame mismatch.");
                Save(automatic, "automatic_frame"); Free(automatic, explicitFrame);
                for (int i = 0; i < 3; i++)
                {
                    d.focusMode = i; d.Refresh();
                    Check(d.continuousCamera.enabled == (i == 0) && d.snappedCamera.enabled == (i != 0), "Focus renders unused camera.");
                }
                // Verify the actual RawImage/Canvas path, not only Graphics.Blit's crop math.
                d.focusMode = 2; d.resolutionIndex = 0; d.showControls = false; d.Refresh();
                var screenOrigin = PixelStableDemo.Snap(d.BasePosition, d.Orientation, d.WorldPerPixel, out _);
                d.ApplyPosition(screenOrigin + d.Orientation * new Vector3(.35f, -.35f, 0) * d.WorldPerPixel);
                yield return null; yield return new WaitForEndOfFrame();
                var screen = ScreenCapture.CaptureScreenshotAsTexture();
                var region = d.ImageRects[2]; int magnification = Mathf.RoundToInt(d.Scales[2]);
                Check(magnification >= 1, "Enlarge Game view for presentation validation.");
                var crop = new Texture2D((int)region.width, (int)region.height, TextureFormat.RGBA32, false);
                crop.SetPixels(screen.GetPixels((int)region.x, (int)region.y, crop.width, crop.height)); crop.Apply();
                var expected = Present(d, 2, magnification);
                int uiDifference = Difference(expected, crop);
                report.AppendLine($"Actual Canvas crop {crop.width}x{crop.height} ({magnification}x) vs Point crop reference: {uiDifference} changed pixels.");
                Save(crop, "canvas_compensated"); Free(screen, crop, expected);
                Check(uiDifference == 0, "Canvas presentation differs from verified crop.");
                d.focusMode = -1; d.shadows = true; d.Refresh(); d.ApplyPose();
                yield return null; yield return null;
                var shadow = Read(d.SnappedTarget); Check(Coral(shadow) > 10, "Shadow toggle broke rendering."); Save(shadow, "shadows_on"); Free(shadow);
                report.AppendLine("Guard footprint/crop bounds; projection sign/axes; 9 angle/zoom grid conditions; layout; focus camera selection; shadow rendering: PASS.");
                report.AppendLine("PASS. Static geometry, orthographic fixed angle/zoom, shadows OFF for stability assertions. No performance benchmark.");
                passed = true; File.WriteAllText(Path.Combine(Output, "validation.txt"), report.ToString()); Debug.Log(report.ToString());
            }
            finally
            {
                if (!passed) File.WriteAllText(Path.Combine(Output, "validation.txt"), report + "\nFAILED / interrupted. See Unity Console.\n");
                JsonUtility.FromJsonOverwrite(state, d); d.Refresh(); d.ApplyPose(); running = false;
            }
        }
    }
}
