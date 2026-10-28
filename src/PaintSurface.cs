using UnityEngine;

namespace SludgeLife2VR {
    internal sealed class PaintSurface {
        Vector3[] vertices;
        Vector2[] uv;
        int[] triangles;
        Renderer original;
        GameObject preview;
        Mesh previewMesh;
        Material material;
        Texture2D texture;
        Color32[] source, painted;
        int width, height;
        PaintCoverage progress;
        Vector3 firstPoint;
        bool started;
        internal bool Completed;
        internal float Coverage { get { return progress == null ? 0 : progress.Coverage; } }
        internal bool Prepare(GameObject target) {
            if (vertices != null) return true;
            if (target == null) return false;
            MeshFilter filter = target.GetComponent<MeshFilter>();
            original = target.GetComponent<Renderer>();
            if (filter == null) filter = target.GetComponentInChildren<MeshFilter>(true);
            if (original == null && filter != null) original = filter.GetComponent<Renderer>();
            if (filter == null || original == null || filter.sharedMesh == null) return false;
            if (original.sharedMaterial == null || original.sharedMaterial.mainTexture == null) return false;
            if (!filter.sharedMesh.isReadable || original.isPartOfStaticBatch)
                return TagGeometry.Find(original, out vertices, out uv, out triangles);
            Mesh mesh = filter.sharedMesh; Vector3[] local = mesh.vertices;
            uv = mesh.uv; triangles = mesh.triangles;
            if (uv.Length != local.Length || original.sharedMaterial == null || original.sharedMaterial.mainTexture == null) return false;
            vertices = new Vector3[local.Length];
            for (int i = 0; i < local.Length; i++) vertices[i] = filter.transform.TransformPoint(local[i]);
            return true;
        }
        internal bool Hit(Ray ray, float maximumDistance, out Vector2 pointUV, out Vector3 point) {
            pointUV = Vector2.zero; point = Vector3.zero;
            if (vertices == null) return false;
            if (!MeshRaycast.PaddedHit(ray, maximumDistance, vertices, uv, triangles, .14f*Mathf.Max(.1f,VrPlugin.Instance.Right.Visual.transform.lossyScale.x), out pointUV, out point)) return false;
            float nearest = Vector3.Distance(ray.origin, point);
            RaycastHit obstruction;
            // Keep the game world's colliders authoritative; hands/UI have no role in wall occlusion.
            if (Physics.Raycast(ray, out obstruction, Mathf.Max(0, nearest - 0.10f), ~((1 << 31) | (1 << 5)), QueryTriggerInteraction.Ignore)) return false;
            return true;
        }
        void CreatePreview() {
            Texture originalTexture = original.sharedMaterial.mainTexture;
            float factor = Mathf.Min(1f, 512f / Mathf.Max(originalTexture.width, originalTexture.height));
            width = Mathf.Max(16, Mathf.RoundToInt(originalTexture.width * factor)); height = Mathf.Max(16, Mathf.RoundToInt(originalTexture.height * factor));
            RenderTexture buffer = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try {
                Graphics.Blit(originalTexture, buffer); RenderTexture.active = buffer;
                texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply(); source = texture.GetPixels32();
            } finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(buffer); }
            painted = new Color32[source.Length];
            byte[] alpha = new byte[source.Length]; for (int i = 0; i < source.Length; i++) alpha[i] = source[i].a;
            progress = new PaintCoverage(width, height, alpha);
            texture.SetPixels32(painted); texture.Apply(); texture.wrapMode = TextureWrapMode.Clamp;
            material = new Material(original.sharedMaterial); material.name = "VR Partial Native Tag"; material.mainTexture = texture;
            if (material.HasProperty("_Color")) { Color tint = material.color; tint.a = 1; material.color = tint; }
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.05f);
            previewMesh = new Mesh(); previewMesh.name = "VR Tag Surface";
            Vector3[] points = (Vector3[])vertices.Clone();
            Vector3 normal = triangles.Length >= 3 ? Vector3.Cross(points[triangles[1]] - points[triangles[0]], points[triangles[2]] - points[triangles[0]]).normalized : Vector3.forward;
            if (VrPlugin.Instance != null && Vector3.Dot(normal, VrPlugin.Instance.HeadPosition - points[0]) < 0) normal = -normal;
            for (int i = 0; i < points.Length; i++) points[i] += normal * 0.003f;
            previewMesh.vertices = points; previewMesh.uv = uv; previewMesh.triangles = triangles; previewMesh.RecalculateNormals(); previewMesh.RecalculateBounds();
            preview = new GameObject("VR Painted Native Tag", typeof(MeshFilter), typeof(MeshRenderer)); preview.layer = 31;
            preview.GetComponent<MeshFilter>().sharedMesh = previewMesh; preview.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
        internal bool Stamp(Vector2 pointUV, Vector3 point, float delta, float worldScale) {
            if (Completed) return false;
            if (texture == null) CreatePreview();
            if (!started) { firstPoint = point; started = true; }
            System.Collections.Generic.List<int> indices = progress.Stamp(pointUV.x, pointUV.y, delta, Vector3.Distance(firstPoint, point), worldScale);
            foreach (int index in indices) painted[index] = source[index];
            bool changed = indices.Count > 0;
            if (changed) { texture.SetPixels32(painted); texture.Apply(false, false); }
            Completed = progress.Completed;
            return changed;
        }
        internal void ClearPreview() {
            if (preview != null) Object.Destroy(preview); if (previewMesh != null) Object.Destroy(previewMesh);
            if (material != null) Object.Destroy(material); if (texture != null) Object.Destroy(texture);
            preview = null; previewMesh = null; material = null; texture = null;
        }
    }
}
