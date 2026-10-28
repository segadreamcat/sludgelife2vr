using UnityEngine;
namespace SludgeLife2VR {
    internal static class RoomHoldMath {
        internal static bool Contact(Vector3[] world,int[] triangles,Matrix4x4 toBoard,Vector3 hand,float range,out Vector3 point) {
            point=hand;float best=range*range;bool found=false;
            for(int i=0;i<triangles.Length;i+=3) {
                Vector3 a=world[triangles[i]],b=world[triangles[i+1]],c=world[triangles[i+2]];
                if(!SurfaceContact.NearBounds(hand,a,b,c,range))continue;
                Vector3 hit=SurfaceContact.Triangle(hand,a,b,c);float distance=(hand-hit).sqrMagnitude;
                if(distance<=best && RoomClimbMath.InReachArea(toBoard.MultiplyPoint3x4(hit))){point=hit;best=distance;found=true;}
            }
            return found;
        }
    }
}
