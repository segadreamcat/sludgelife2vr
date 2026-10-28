using UnityEngine;

namespace SludgeLife2VR {
    internal static class MeshRaycast {
        internal static bool PaddedHit(Ray ray,float maximumDistance,Vector3[] vertices,Vector2[] uv,int[] triangles,float padding,out Vector2 pointUV,out Vector3 point) {
            if(Hit(ray,maximumDistance,vertices,uv,triangles,out pointUV,out point))return true;
            float best=padding*padding;bool found=false;
            for(int i=0;i+2<triangles.Length;i+=3){
                int ia=triangles[i],ib=triangles[i+1],ic=triangles[i+2];Vector3 a=vertices[ia],b=vertices[ib],c=vertices[ic];
                Vector3 ab=b-a,ac=c-a,n=Vector3.Cross(ab,ac);float denominator=Vector3.Dot(n,ray.direction);
                if(System.Math.Abs(denominator)<1e-7f)continue;
                float distance=Vector3.Dot(a-ray.origin,n)/denominator;if(distance<.01f || distance>maximumDistance)continue;
                Vector3 projected=ray.GetPoint(distance),closest=SurfaceContact.Triangle(projected,a,b,c);
                float miss=(closest-projected).sqrMagnitude;if(miss>best)continue;
                float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ac,ac),cc=Vector3.Dot(ab,ac),d=aa*bb-cc*cc;if(d<1e-12f)continue;
                Vector3 ap=closest-a;float u=(bb*Vector3.Dot(ap,ab)-cc*Vector3.Dot(ap,ac))/d,v=(aa*Vector3.Dot(ap,ac)-cc*Vector3.Dot(ap,ab))/d;
                pointUV=uv[ia]*(1-u-v)+uv[ib]*u+uv[ic]*v;point=closest;best=miss;found=true;
            }
            return found;
        }
        internal static bool Hit(Ray ray, float maximumDistance, Vector3[] vertices, Vector2[] uv, int[] triangles, out Vector2 pointUV, out Vector3 point) {
            pointUV = Vector2.zero; point = Vector3.zero;
            float nearest = maximumDistance; bool found = false;
            for (int i = 0; i + 2 < triangles.Length; i += 3) {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Vector3 a = vertices[ia], e1 = vertices[ib] - a, e2 = vertices[ic] - a;
                Vector3 p = Vector3.Cross(ray.direction, e2); float determinant = Vector3.Dot(e1, p);
                if (System.Math.Abs(determinant) < 0.000001f) continue;
                float inverse = 1 / determinant; Vector3 offset = ray.origin - a;
                float u = Vector3.Dot(offset, p) * inverse; if (u < 0 || u > 1) continue;
                Vector3 q = Vector3.Cross(offset, e1); float v = Vector3.Dot(ray.direction, q) * inverse;
                if (v < 0 || u + v > 1) continue;
                float distance = Vector3.Dot(e2, q) * inverse; if (distance < 0.01f || distance > nearest) continue;
                nearest = distance; pointUV = uv[ia] * (1 - u - v) + uv[ib] * u + uv[ic] * v;
                point = ray.GetPoint(distance); found = true;
            }
            return found;
        }
    }
}
