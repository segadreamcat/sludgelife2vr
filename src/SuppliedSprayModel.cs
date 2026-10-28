using System;
using System.IO;
using UnityEngine;

namespace SludgeLife2VR {
    internal static class SuppliedSprayModel {
        internal static Mesh Load() {
            using (Stream stream = typeof(SuppliedSprayModel).Assembly.GetManifestResourceStream("VRSuppliedCan")) {
                if (stream == null) return null;
                using (BinaryReader reader = new BinaryReader(stream)) {
                    if (reader.ReadUInt32() != 0x314E4356) throw new IOException("Invalid supplied can header.");
                    int count = reader.ReadInt32(), indices = reader.ReadInt32();
                    if (count < 3 || count > 60000 || indices < 3 || indices > 180000 || indices % 3 != 0) throw new IOException("Invalid supplied can size.");
                    Vector3[] vertices = new Vector3[count], normals = new Vector3[count]; Color[] colors = new Color[count];
                    for (int i = 0; i < count; i++) {
                        vertices[i] = new Vector3(Read(reader), Read(reader), Read(reader));
                        normals[i] = new Vector3(Read(reader), Read(reader), Read(reader));
                        colors[i] = new Color(Read(reader), Read(reader), Read(reader), Read(reader));
                    }
                    int[] triangles = new int[indices];
                    for (int i = 0; i < indices; i++) { int index = reader.ReadInt32(); if (index < 0 || index >= count) throw new IOException("Invalid supplied can index."); triangles[i] = index; }
                    if (stream.Position != stream.Length) throw new IOException("Unexpected supplied can data.");
                    Mesh mesh = new Mesh(); mesh.name = "Supplied Spray Can - Sludge Palette";
                    mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors; mesh.triangles = triangles; mesh.RecalculateBounds();
                    return mesh;
                }
            }
        }
        static float Read(BinaryReader reader) {
            float value = reader.ReadSingle();
            if (Single.IsNaN(value) || Single.IsInfinity(value)) throw new IOException("Non-finite can geometry.");
            return value;
        }
    }
}
