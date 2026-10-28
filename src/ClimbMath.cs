using System;
using UnityEngine;

namespace SludgeLife2VR {
    internal static class ClimbMath {
        internal const float LedgeRange=.24f,LedgeProbeHeight=.30f,LedgeProbeDepth=.55f;
        internal static float LedgeInset(int probe){return probe==0?.04f:probe==1?.16f:.26f;}

        internal static Vector3 Pull(Vector3 previous, Vector3 current, float scale) {
            Vector3 delta = previous-current;
            // A tracking discontinuity must never move the player across the map.
            return delta.sqrMagnitude > .35f*.35f ? Vector3.zero : delta*scale;
        }
        internal static Vector3 Average(Vector3 left, bool hasLeft, Vector3 right, bool hasRight) {
            return hasLeft && hasRight ? (left+right)*.5f : hasLeft ? left : hasRight ? right : Vector3.zero;
        }
        internal static Vector3 Limit(Vector3 delta, float dt, float scale) {
            float limit = 3f*scale*Math.Min(Math.Max(dt,0),.05f);
            float length = (float)Math.Sqrt(delta.sqrMagnitude);
            return length > limit && length > 0 ? delta*(limit/length) : delta;
        }
        internal static bool EarnedMantle(float pullHeight, float headAboveEdge, float scale) {
            return pullHeight > .25f*scale && headAboveEdge > .25f*scale;
        }
        internal static bool ResumeFall(ref bool suspended,string state) {
            bool owned=suspended;suspended=false;
            return owned && (state=="MOVE" || state.StartsWith("FALL") || state.StartsWith("Climbing?"));
        }
    }
}
