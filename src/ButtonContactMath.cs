using UnityEngine;
namespace SludgeLife2VR {
    internal static class ButtonContactMath {
        internal static bool NearMarker(Vector3 previous,Vector3 point,Vector3 marker){return (point-marker).sqrMagnitude<=.20f*.20f || (previous-marker).sqrMagnitude<=.20f*.20f;}
        internal static float Distance(Vector3 previous,Vector3 point,Vector3[] vertices,int[] triangles,Matrix4x4 matrix,bool sweep) {
            Vector3 delta=point-previous;float length=delta.magnitude,nearest=float.MaxValue;
            bool cast=sweep && length>.00001f && length<.35f;
            for(int i=0;i<triangles.Length;i+=3){
                Vector3 a=matrix.MultiplyPoint3x4(vertices[triangles[i]]),b=matrix.MultiplyPoint3x4(vertices[triangles[i+1]]),c=matrix.MultiplyPoint3x4(vertices[triangles[i+2]]);
                nearest=Mathf.Min(nearest,(SurfaceContact.Triangle(point,a,b,c)-point).magnitude);
                float distance;if(cast && BodyContactMath.TriangleRay(previous,delta/length,length,a,b,c,out distance))return 0;
            }
            return nearest;
        }
    }
}
