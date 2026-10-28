using UnityEngine;
namespace SludgeLife2VR {
    internal static class GliderGripMath {
        internal const float NativeRadius=.045f;
        internal static void Frame(Matrix4x4 transform,Vector3 center,Vector3 axis,float radius,out Vector3 point,out Vector3 direction,out float worldRadius){
            point=transform.MultiplyPoint3x4(center);direction=transform.MultiplyVector(axis).normalized;
            Vector3 a=axis==Vector3.forward?Vector3.right:Vector3.forward,b=Vector3.Cross(axis,a);
            worldRadius=radius*Mathf.Max(transform.MultiplyVector(a).magnitude,transform.MultiplyVector(b).magnitude);
        }
        internal static bool Cylinder(Vector3[] vertices,out Vector3 center,out Vector3 axis,out float radius){
            center=axis=Vector3.zero;radius=0;if(vertices==null || vertices.Length<8)return false;
            Vector3 min=vertices[0],max=min;foreach(Vector3 v in vertices){min=Vector3.Min(min,v);max=Vector3.Max(max,v);}
            center=(min+max)*.5f;Vector3 size=max-min;
            if(size.z>=size.x && size.z>=size.y){axis=Vector3.forward;radius=Mathf.Max(size.x,size.y)*.5f;return size.z>radius*3;}
            if(size.y>=size.x){axis=Vector3.up;radius=Mathf.Max(size.x,size.z)*.5f;return size.y>radius*3;}
            axis=Vector3.right;radius=Mathf.Max(size.y,size.z)*.5f;return size.x>radius*3;
        }
        internal static Vector3 Wrap(Vector3 pose,Vector3 center,float radius,float fingerWeight){
            radius=Mathf.Max(.025f,radius);float delta=radius-NativeRadius;
            Vector3 d=pose-center;float r=Mathf.Sqrt(d.y*d.y+d.z*d.z),angle=Mathf.Atan2(d.y,d.z);
            if(angle< -Mathf.PI*.5f)angle+=Mathf.PI*2;
            float fitted=Mathf.PI+(angle-Mathf.PI)*(NativeRadius/radius),rr=Mathf.Max(radius+.004f,r+delta);
            Vector3 curved=new Vector3(pose.x,center.y+Mathf.Sin(fitted)*rr,center.z+Mathf.Cos(fitted)*rr);
            Vector3 result=Vector3.Lerp(pose-Vector3.forward*delta,curved,fingerWeight);
            d=result-center;r=Mathf.Sqrt(d.y*d.y+d.z*d.z);
            if(r<radius+.004f && r>.00001f){result.y=center.y+d.y*(radius+.004f)/r;result.z=center.z+d.z*(radius+.004f)/r;}
            return result;
        }
    }
}
