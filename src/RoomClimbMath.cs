using UnityEngine;
namespace SludgeLife2VR {
    internal static class RoomClimbMath {
        // Exact visible bounds of the striped board below floor 04's elevator opening.
        // Its native box collider extends above the artwork; grip only the rendered board.
        internal static bool IsTarget(string path) { return path == "TOWER/04/04_STATIC/PROP_PLANK_L_STRIPED"; }
        internal static bool IsSuitcaseTarget(string path) {
            return path=="TOWER/ELEVATOR/_STATIC/PROP_SUITCASE_BIGBLUE"
                || path=="TOWER/ELEVATOR/_STATIC/PROP_SUITCASE_BIGBLUE/handle"
                || path=="TOWER/04/04_STATIC/PROP_SUITCASE_DUFFLERED";
        }
        internal static bool IsInteriorProp(string path) {
            const string root="TOWER/ELEVATOR/_STATIC/";
            return path!=null && (path.StartsWith(root+"PROP_SUITCASE_") || (path==root+"PROP_LUGGAGECART" || path.StartsWith(root+"PROP_LUGGAGECART/")));
        }
        internal static bool InReachArea(Vector3 p) { return p.x>=-3 && p.x<=3 && p.y>=-1.2f && p.y<=4 && p.z>=-4.5f && p.z<=2; }
        internal static bool OverlapsReach(Vector3 a,Vector3 b,Vector3 c) {
            return Mathf.Min(a.x,Mathf.Min(b.x,c.x))<=3 && Mathf.Max(a.x,Mathf.Max(b.x,c.x))>=-3
                && Mathf.Min(a.y,Mathf.Min(b.y,c.y))<=4 && Mathf.Max(a.y,Mathf.Max(b.y,c.y))>=-1.2f
                && Mathf.Min(a.z,Mathf.Min(b.z,c.z))<=2 && Mathf.Max(a.z,Mathf.Max(b.z,c.z))>=-4.5f;
        }
        internal static bool AssistHeight(float floorAboveFeet,float scale) { return floorAboveFeet>.02f*scale && floorAboveFeet<=.45f*scale; }
        internal static bool InOpening(Vector3 boardLocal) {
            return Mathf.Abs(boardLocal.x)<3 && boardLocal.y> -1.4f && boardLocal.y<2
                && boardLocal.z< -.65f && boardLocal.z> -3.7f;
        }
        internal const int LandingCount=15;
        internal static bool AllowsLandingHeight(float rise,float scale) { return rise<=.65f*scale && rise>=-1.10f*scale; }
        internal static Vector2 LandingOffset(int candidate) {
            int side=candidate%5;
            return new Vector2(.20f+.25f*(candidate/5),side==0?0:side==1?.4f:side==2?-.4f:side==3?.8f:-.8f);
        }
        internal static bool Contact(Matrix4x4 toWorld, Matrix4x4 toLocal, Vector3 hand, float range, out Vector3 point) {
            Vector3 p=toLocal.MultiplyPoint3x4(hand);
            p.x=Mathf.Clamp(p.x,-0.999997616f,0.999998569f);p.y=Mathf.Clamp(p.y,-0.049250785f,0.049250785f);p.z=Mathf.Clamp(p.z,-1.656049142f,1.656049142f);
            point=toWorld.MultiplyPoint3x4(p);
            return (hand-point).sqrMagnitude <= range*range;
        }
    }
}
