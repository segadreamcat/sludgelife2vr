using UnityEngine;
namespace SludgeLife2VR {
    internal static class GraffitiWallMath {
        // The complete west graffiti wall shown in the spraying captures, including its trim.
        internal static bool IsRoot(string path){return path=="TOWER/06/06_RCSG" || path=="TOWER/07/07_RCSG";}
        internal static bool Contains(Vector3 p){return Near(p,0);}
        internal static bool Near(Vector3 p,float range){return p.x>=7.55f-range && p.x<=8.50f+range && p.y>=39.0f-range && p.y<=51.95f+range && p.z>=-65.40f-range && p.z<=-54.0f+range;}
        internal static bool Overlaps(Vector3 a,Vector3 b,Vector3 c) {
            return Mathf.Min(a.x,Mathf.Min(b.x,c.x))<=8.50f && Mathf.Max(a.x,Mathf.Max(b.x,c.x))>=7.55f
                && Mathf.Min(a.y,Mathf.Min(b.y,c.y))<=51.95f && Mathf.Max(a.y,Mathf.Max(b.y,c.y))>=39.0f
                && Mathf.Min(a.z,Mathf.Min(b.z,c.z))<=-54.0f && Mathf.Max(a.z,Mathf.Max(b.z,c.z))>=-65.40f;
        }
        internal static bool Contact(Vector3[] world,int[] triangles,Vector3 hand,float range,out Vector3 point) {
            point=hand;float best=range*range;bool found=false;
            for(int i=0;i<triangles.Length;i+=3) {
                Vector3 a=world[triangles[i]],b=world[triangles[i+1]],c=world[triangles[i+2]];
                if(!SurfaceContact.NearBounds(hand,a,b,c,range))continue;
                Vector3 hit=SurfaceContact.Triangle(hand,a,b,c);float distance=(hand-hit).sqrMagnitude;
                if(distance<=best && Contains(hit)){point=hit;best=distance;found=true;}
            }
            return found;
        }
    }
}
