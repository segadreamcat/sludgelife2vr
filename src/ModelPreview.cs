using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace SludgeLife2VR {
    internal static class ModelPreview {
        internal static bool Save(string directory) {
            if (!TrackedHandModel.EnsureAssets()) return false;
            GameObject root = new GameObject("VR Hand Preview Fixture"); root.transform.position = new Vector3(0, -10000, 0);
            RenderTexture target = new RenderTexture(1200, 800, 24, RenderTextureFormat.ARGB32);
            Texture2D pixels = null; RenderTexture previous = RenderTexture.active;
            try {
                GameObject left = new GameObject("Left Preview"); left.transform.SetParent(root.transform, false);
                left.transform.localPosition = new Vector3(-0.13f, 0, 0); left.transform.localRotation = Quaternion.AngleAxis(180, Vector3.up) * Quaternion.Euler(105, 0, 0);
                new TrackedHandModel(left.transform, true, 30).Update(false);
                GameObject right = new GameObject("Right Preview"); right.transform.SetParent(root.transform, false);
                right.transform.localPosition = new Vector3(0.13f, 0, 0); right.transform.localRotation = Quaternion.AngleAxis(180, Vector3.up) * Quaternion.Euler(105, 0, 0);
                new TrackedHandModel(right.transform, false, 30).Update(true);
                GameObject view = new GameObject("Preview Camera", typeof(Camera)); view.transform.SetParent(root.transform, false);
                Camera camera = view.GetComponent<Camera>(); camera.enabled = false; camera.stereoTargetEye = StereoTargetEyeMask.None;
                UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(camera, true);
                view.transform.localPosition = new Vector3(0, 0, -0.7f); view.transform.LookAt(root.transform.position);
                camera.orthographic = true; camera.orthographicSize = 0.2f; camera.nearClipPlane = 0.01f; camera.farClipPlane = 2;
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.10f, 0.11f, 0.13f);
                target.Create(); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target; pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "native-hands-preview.png"), pixels.EncodeToPNG());
            } finally {
                RenderTexture.active = previous; Object.Destroy(root); target.Release(); Object.Destroy(target); if (pixels != null) Object.Destroy(pixels);
            }
            return true;
        }
    }
}
