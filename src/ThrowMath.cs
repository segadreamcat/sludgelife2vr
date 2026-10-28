using UnityEngine;
namespace SludgeLife2VR {
    internal static class ThrowMath {
        internal static Vector3 Release(Vector3 current,Vector3 recentUp,float scale) {
            Vector3 v=recentUp.y>current.y && recentUp.y>.6f*scale?recentUp:current;
            if(v.magnitude<.4f*scale)return v;
            Vector3 result=v*1.65f;
            if(v.y>.35f*scale)result.y=v.y*2.15f+.75f*scale;
            return Vector3.ClampMagnitude(result,16*scale);
        }
    }
}
