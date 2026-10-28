using UnityEngine;
namespace SludgeLife2VR {
    internal static class HandPushMath {
        internal static bool Target(string name,string path,bool hinge,bool kinematic) {
            if(!hinge || kinematic)return false;
            return name=="DOOR" && (path.Contains("LVL_DOOR") || path.Contains("LVL_LOCKER"))
                || name=="POTTY_DOOR" && path.Contains("/PORTAPOTTY")
                || name=="seat" && path.Contains("/SWING/");
        }
        internal static float Impulse(Vector3 handVelocity,Vector3 bodyVelocity,Vector3 outward,float dt,float scale) {
            float speed=-Vector3.Dot((handVelocity-bodyVelocity)/Mathf.Max(.001f,scale),outward);
            if(speed<=.035f)return 0;
            // A bounded motor impulse, applied only during a real inward hand stroke.
            return Mathf.Min(speed,4)*Mathf.Min(Mathf.Max(dt,0),.04f)*7*scale;
        }
        internal static bool Stroke(Vector3 previous,Vector3 current,float dt,float scale) {
            return dt>0 && dt<=.1f && (current-previous).sqrMagnitude<=.35f*.35f*scale*scale;
        }
    }
}
