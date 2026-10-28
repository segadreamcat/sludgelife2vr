using UnityEngine;

namespace SludgeLife2VR {
    internal static class HandInteractionMath {
        internal static Vector3 Mouth(Vector3 head, Vector3 forward, Vector3 up, float scale) { return head+forward*.10f*scale-up*.09f*scale; }
        internal static bool Eating(float age, float distance, bool soda, float canUpDotHeadUp, float dt, ref float dwell) {
            bool ready = age >= (soda?.18f:.35f) && distance <= (soda?.14f:.12f) && (!soda || canUpDotHeadUp < .90f);
            dwell = ready ? dwell+HandPoseMath.Clamp(dt,0,.05f) : 0;
            return dwell >= (soda ? .14f : .25f);
        }
        internal static bool BackHolster(Vector3 offset) {
            return offset.x>=.04f && offset.x<=.56f && offset.y>=-.62f && offset.y<=.18f && offset.z<=-.07f && offset.z>=-.48f;
        }
        internal static bool Press(float distance, float travel, ref bool latched) {
            if (latched) { if (distance > .05f) latched = false; return false; }
            if (distance > .018f || travel < .00005f) return false;
            latched = true; return true;
        }
    }
}
