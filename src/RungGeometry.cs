using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SludgeLife2VR {
    internal static class RungGeometry {
        sealed class Surface { internal Vector3 RootPosition; internal Vector3[] Vertices, World; internal int[] Triangles; internal Transform Root; internal Matrix4x4 Matrix; }
        static Dictionary<string,List<Surface>> surfaces;
        internal static bool Contact(Transform marker, Vector3 world, float range, out Vector3 point) {
            point = world; Surface selected=Resolve(marker);if(selected==null)return false;
            float best = range*range; bool found = false;
            for (int i = 0; i < selected.Triangles.Length; i += 3) {
                Vector3 a = selected.World[selected.Triangles[i]], b = selected.World[selected.Triangles[i+1]], c = selected.World[selected.Triangles[i+2]];
                if (!SurfaceContact.NearBounds(world,a,b,c,range)) continue;
                Vector3 hit = SurfaceContact.Triangle(world,a,b,c); float distance = (world-hit).sqrMagnitude;
                if (distance <= best) { best = distance; point = hit; found = true; }
            }
            return found;
        }
        internal static bool Top(Transform marker,out float height) {
            height=float.MinValue;Surface selected=Resolve(marker);if(selected==null)return false;
            foreach(Vector3 point in selected.World)height=Mathf.Max(height,point.y);return true;
        }
        static Surface Resolve(Transform marker) {
            if (surfaces == null) Load();
            Transform root = marker; List<Surface> candidates = null;
            while (root != null) { if (surfaces.TryGetValue(Diagnostics.PathOf(root),out candidates)) break; root = root.parent; }
            if (root == null || candidates == null) return null;
            Surface selected = null; float nearest = float.MaxValue;
            foreach (Surface s in candidates) { float d = (root.position-s.RootPosition).sqrMagnitude; if (d < nearest) { nearest = d; selected = s; } }
            if (selected == null || (candidates.Count > 1 && nearest > .25f)) return null;
            Matrix4x4 matrix = root.localToWorldMatrix;
            if (selected.Root != root || selected.World == null || selected.Matrix != matrix) {
                selected.Root = root; selected.Matrix = matrix; selected.World = new Vector3[selected.Vertices.Length];
                for (int i = 0; i < selected.World.Length; i++) selected.World[i] = matrix.MultiplyPoint3x4(selected.Vertices[i]);
            }
            return selected;
        }
        static float Float(BinaryReader r) { float x = r.ReadSingle(); if (Single.IsInfinity(x) || Single.IsNaN(x)) throw new IOException("Invalid climb geometry."); return x; }
        static Vector3 Vector(BinaryReader r) { return new Vector3(Float(r),Float(r),Float(r)); }
        static void Load() {
            surfaces = new Dictionary<string,List<Surface>>();
            using (Stream stream = typeof(RungGeometry).Assembly.GetManifestResourceStream("VRClimbSurfaces")) {
                if (stream == null) return;
                using (BinaryReader r = new BinaryReader(stream)) {
                    if (r.ReadUInt32() != 0x31474E52) throw new IOException("Invalid climb surface header.");
                    int count = r.ReadInt32(); if (count < 0 || count > 10000) throw new IOException("Invalid climb count.");
                    for (int i = 0; i < count; i++) {
                        string path = r.ReadString(); Surface s = new Surface(); s.RootPosition = Vector(r);
                        int v = r.ReadInt32(), t = r.ReadInt32();
                        if (v < 0 || v > 1000000 || t < 0 || t > 3000000 || t%3 != 0) throw new IOException("Invalid climb mesh size.");
                        s.Vertices = new Vector3[v]; s.Triangles = new int[t];
                        for (int j = 0; j < v; j++) s.Vertices[j] = Vector(r);
                        for (int j = 0; j < t; j++) { int index = r.ReadInt32(); if (index < 0 || index >= v) throw new IOException("Invalid climb index."); s.Triangles[j] = index; }
                        List<Surface> list; if (!surfaces.TryGetValue(path,out list)) { list = new List<Surface>(); surfaces.Add(path,list); } list.Add(s);
                    }
                    if (stream.Position != stream.Length) throw new IOException("Unexpected climb geometry data.");
                    BepInEx.Logging.Logger.CreateLogSource("VR Climb").LogInfo("Loaded " + count + " visible rung/pipe meshes. Trigger volumes are broad-phase only.");
                }
            }
        }
    }
}
