using UnityEngine;

namespace SludgeLife2VR {
    internal static class SurfaceContact {
        internal static bool NearBounds(Vector3 p, Vector3 a, Vector3 b, Vector3 c, float range) {
            return p.x >= System.Math.Min(a.x,System.Math.Min(b.x,c.x))-range && p.x <= System.Math.Max(a.x,System.Math.Max(b.x,c.x))+range
                && p.y >= System.Math.Min(a.y,System.Math.Min(b.y,c.y))-range && p.y <= System.Math.Max(a.y,System.Math.Max(b.y,c.y))+range
                && p.z >= System.Math.Min(a.z,System.Math.Min(b.z,c.z))-range && p.z <= System.Math.Max(a.z,System.Math.Max(b.z,c.z))+range;
        }
        static Vector3 Edge(Vector3 point, Vector3 a, Vector3 b) {
            Vector3 d = b-a; float length = d.sqrMagnitude;
            return length < .000000001f ? a : a+d*HandPoseMath.Clamp(Vector3.Dot(point-a,d)/length,0,1);
        }
        internal static Vector3 Triangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c) {
            Vector3 ab = b-a, ac = c-a, n = Vector3.Cross(ab,ac);
            float nn = n.sqrMagnitude;
            if (nn > 1e-14f) {
                Vector3 projected = point-n*(Vector3.Dot(point-a,n)/nn), ap = projected-a;
                float aa = Vector3.Dot(ab,ab), bb = Vector3.Dot(ac,ac), cc = Vector3.Dot(ab,ac);
                float d = aa*bb-cc*cc;
                if (d > 1e-14f) {
                    float v = (bb*Vector3.Dot(ap,ab)-cc*Vector3.Dot(ap,ac))/d;
                    float w = (aa*Vector3.Dot(ap,ac)-cc*Vector3.Dot(ap,ab))/d;
                    if (v >= 0 && w >= 0 && v+w <= 1) return projected;
                }
            }
            Vector3 x = Edge(point,a,b), y = Edge(point,b,c), z = Edge(point,c,a);
            if ((y-point).sqrMagnitude < (x-point).sqrMagnitude) x = y;
            return (z-point).sqrMagnitude < (x-point).sqrMagnitude ? z : x;
        }
    }
}
