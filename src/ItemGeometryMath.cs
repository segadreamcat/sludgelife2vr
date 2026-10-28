using UnityEngine;
namespace SludgeLife2VR {
    internal static class ItemGeometryMath {
        internal static void Bake(Vector3[] vertices,Matrix4x4 frameFromMesh) {
            for(int i=0;i<vertices.Length;i++)vertices[i]=frameFromMesh.MultiplyPoint3x4(vertices[i]);
        }
        internal static Vector3 MouthPoint(Vector3 localMouth,Vector3 size,bool soda) {
            Vector3 half=size*.5f;
            return new Vector3(HandPoseMath.Clamp(localMouth.x,-half.x,half.x),soda?half.y:HandPoseMath.Clamp(localMouth.y,-half.y,half.y),HandPoseMath.Clamp(localMouth.z,-half.z,half.z));
        }
    }
}
