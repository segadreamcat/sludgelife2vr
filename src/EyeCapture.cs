using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace SludgeLife2VR {
    internal static class EyeCapture {
        internal static string Save(Camera vrCamera, RenderTexture uiTexture, string directory) {
            directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(directory);
            string prefix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            StringBuilder report = new StringBuilder();
            report.AppendLine("Game eye-view diagnostic capture. SteamVR/Virtual Desktop system overlays are not included.");
            report.AppendLine("UTC=" + DateTime.UtcNow.ToString("o"));
            report.AppendLine("Scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            report.AppendLine("Source=" + (VrPlugin.Instance.Source == null ? "none" : Diagnostics.PathOf(VrPlugin.Instance.Source.transform)));
            report.AppendLine("CameraPosition=" + vrCamera.transform.position + " CameraRotation=" + vrCamera.transform.eulerAngles);
            report.AppendLine("CameraMask=" + vrCamera.cullingMask);
            report.AppendLine(VrPlugin.Instance.UI.Describe());
            report.AppendLine(VrPlugin.Instance.DescribeView());
            report.AppendLine(VrPlugin.Instance.SprayControls.Describe());
            GameObject temporary = new GameObject("SL2VR Diagnostic Eye");
            Camera camera = temporary.AddComponent<Camera>(); camera.enabled = false;
            try {
                int width = Mathf.Max(16, UnityEngine.XR.XRSettings.eyeTextureWidth);
                int height = Mathf.Max(16, UnityEngine.XR.XRSettings.eyeTextureHeight);
                float factor = Mathf.Min(1f, 1600f / Mathf.Max(width, height));
                width = Mathf.Max(16, Mathf.RoundToInt(width * factor)); height = Mathf.Max(16, Mathf.RoundToInt(height * factor));
                for (int i = 0; i < 2; i++) {
                    RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
                    try {
                        camera.CopyFrom(vrCamera); camera.enabled = false;
                        camera.transform.position = vrCamera.transform.position; camera.transform.rotation = vrCamera.transform.rotation;
                        camera.stereoTargetEye = StereoTargetEyeMask.None;
                        UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(camera, true);
                        camera.targetTexture = target; camera.rect = new Rect(0, 0, 1, 1);
                        Camera.StereoscopicEye eye = (Camera.StereoscopicEye)i;
                        // Re-render using the native stereo eye's actual view and projection matrices.
                        camera.worldToCameraMatrix = vrCamera.GetStereoViewMatrix(eye);
                        camera.projectionMatrix = vrCamera.GetStereoProjectionMatrix(eye);
                        camera.Render();
                        string file = Path.Combine(directory, prefix + (i == 0 ? "-left.png" : "-right.png"));
                        WriteTexture(target, file); report.AppendLine((i == 0 ? "Left=" : "Right=") + file);
                    } finally { camera.targetTexture = null; RenderTexture.ReleaseTemporary(target); }
                }
            } finally { UnityEngine.Object.Destroy(temporary); }
            if (uiTexture != null) {
                string ui = Path.Combine(directory, prefix + "-ui.png"); WriteTexture(uiTexture, ui); report.AppendLine("UI=" + ui);
            }
            string result = Path.Combine(directory, prefix + ".txt");
            File.WriteAllText(result, report.ToString());
            File.WriteAllText(Path.Combine(directory, "latest.txt"), report.ToString());
            return result;
        }
        static void WriteTexture(RenderTexture target, string file) {
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try {
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false); pixels.Apply(false, false);
                byte[] data = pixels.EncodeToPNG();
                File.WriteAllBytes(file + ".tmp", data); File.Move(file + ".tmp", file);
            } finally { RenderTexture.active = previous; if (pixels != null) UnityEngine.Object.Destroy(pixels); }
        }
    }
}
