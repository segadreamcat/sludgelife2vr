using System;
using UnityEngine;
namespace SludgeLife2VR {
    internal static class ContactBoundsMath {
        internal const int Block=48;
        internal static bool Near(Bounds bounds,Vector3 previous,Vector3 point,float radius){
            Vector3 lo=bounds.min,hi=bounds.max;
            return Math.Min(previous.x,point.x)<=hi.x+radius && Math.Max(previous.x,point.x)>=lo.x-radius
                && Math.Min(previous.y,point.y)<=hi.y+radius && Math.Max(previous.y,point.y)>=lo.y-radius
                && Math.Min(previous.z,point.z)<=hi.z+radius && Math.Max(previous.z,point.z)>=lo.z-radius;
        }
        internal static Bounds Transform(Bounds local,Matrix4x4 m){
            Vector3 e=local.extents;
            return new Bounds(m.MultiplyPoint3x4(local.center),new Vector3(
                Math.Abs(m.m00)*e.x+Math.Abs(m.m01)*e.y+Math.Abs(m.m02)*e.z,
                Math.Abs(m.m10)*e.x+Math.Abs(m.m11)*e.y+Math.Abs(m.m12)*e.z,
                Math.Abs(m.m20)*e.x+Math.Abs(m.m21)*e.y+Math.Abs(m.m22)*e.z)*2);
        }
        internal static Bounds[] Groups(Vector3[] vertices,int[] triangles,Bounds[] groups){
            int count=(triangles.Length+Block-1)/Block;if(groups==null || groups.Length!=count)groups=new Bounds[count];
            for(int start=0;start<triangles.Length;start+=Block){
                Bounds bounds=new Bounds(vertices[triangles[start]],Vector3.zero);
                for(int i=start+1;i<Math.Min(start+Block,triangles.Length);i++)bounds.Encapsulate(vertices[triangles[i]]);
                groups[start/Block]=bounds;
            }return groups;
        }
    }
}
