using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SludgeLife2VR {
    internal static class TagGeometry {
        sealed class Surface {
            internal Vector3 Center;
            internal Vector3[] Vertices;
            internal Vector2[] UV;
            internal int[] Triangles;
        }
        static Dictionary<string,List<Surface>> surfaces;
        internal static bool Find(Renderer renderer, out Vector3[] vertices, out Vector2[] uv, out int[] triangles) {
            vertices = null; uv = null; triangles = null;
            if (surfaces == null) Load();
            List<Surface> candidates;
            if (!surfaces.TryGetValue(Diagnostics.PathOf(renderer.transform), out candidates)) return false;
            Surface selected = null; float nearest = float.MaxValue;
            foreach (Surface candidate in candidates) {
                float distance = (renderer.bounds.center - candidate.Center).sqrMagnitude;
                if (distance < nearest) { nearest = distance; selected = candidate; }
            }
            if (selected == null || (candidates.Count > 1 && nearest > 0.25f)) return false;
            vertices = selected.Vertices; uv = selected.UV; triangles = selected.Triangles; return true;
        }
        static void Load() {
            surfaces = new Dictionary<string,List<Surface>>();
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("VRTagSurfaces");
            if (stream == null) return;
            using (BinaryReader reader = new BinaryReader(stream)) {
                if (reader.ReadInt32() != 0x31544756) throw new IOException("Invalid VR tag geometry header.");
                int count = reader.ReadInt32(); if (count < 0 || count > 10000) throw new IOException("Invalid tag count.");
                for (int i = 0; i < count; i++) {
                    string path = reader.ReadString(); Surface surface = new Surface();
                    surface.Center = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    int n = reader.ReadInt32(); if (n < 0 || n > 1000000) throw new IOException("Invalid tag vertex count.");
                    surface.Vertices = new Vector3[n]; surface.UV = new Vector2[n];
                    for (int j = 0; j < n; j++) { surface.Vertices[j] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); surface.UV[j] = new Vector2(reader.ReadSingle(), reader.ReadSingle()); }
                    n = reader.ReadInt32(); if (n < 0 || n > 3000000 || n % 3 != 0) throw new IOException("Invalid tag triangle count.");
                    surface.Triangles = new int[n];
                    for (int j = 0; j < n; j++) { int index = reader.ReadInt32(); if (index < 0 || index >= surface.Vertices.Length) throw new IOException("Invalid tag index."); surface.Triangles[j] = index; }
                    List<Surface> list; if (!surfaces.TryGetValue(path, out list)) { list = new List<Surface>(); surfaces.Add(path, list); }
                    list.Add(surface);
                }
                BepInEx.Logging.Logger.CreateLogSource("VR Spray").LogInfo("Loaded " + count + " per-decal surfaces for native static-batched tags.");
            }
        }
    }
}
