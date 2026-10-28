using UnityEngine;
namespace SludgeLife2VR {
    internal static class BodyContactMath {
        internal static bool TriangleRay(Vector3 from,Vector3 direction,float length,Vector3 a,Vector3 b,Vector3 c,out float distance) {
            distance=0;Vector3 e1=b-a,e2=c-a,p=Vector3.Cross(direction,e2);float d=Vector3.Dot(e1,p);
            if(Mathf.Abs(d)<1e-9f)return false;float inv=1/d;Vector3 s=from-a;
            float u=Vector3.Dot(s,p)*inv;if(u<0 || u>1)return false;Vector3 q=Vector3.Cross(s,e1);
            float v=Vector3.Dot(direction,q)*inv;if(v<0 || u+v>1)return false;
            distance=Vector3.Dot(e2,q)*inv;return distance>=0 && distance<=length;
        }
        internal static bool Resolve(Vector3 previous,Vector3 desired,float radius,Vector3[] vertices,int[] triangles,Vector3[] normals,out Vector3 result,out Vector3 contactNormal) {
            return Resolve(previous,desired,radius,vertices,triangles,normals,null,out result,out contactNormal);
        }
        internal static bool Resolve(Vector3 previous,Vector3 desired,float radius,Vector3[] vertices,int[] triangles,Vector3[] normals,Bounds[] groups,out Vector3 result,out Vector3 contactNormal) {
            result=desired;contactNormal=Vector3.zero;Vector3 delta=desired-previous;float length=delta.magnitude;
            float first=length,nearest=float.MaxValue;Vector3 surface=Vector3.zero,nearNormal=Vector3.zero;
            bool crossed=false;Vector3 swept=(previous+desired)*.5f;float reach=length*.5f+radius*2;
            for(int block=0;block<triangles.Length;block+=ContactBoundsMath.Block){
              if(groups!=null && !ContactBoundsMath.Near(groups[block/ContactBoundsMath.Block],previous,desired,radius*2))continue;
              for(int i=block;i<System.Math.Min(block+ContactBoundsMath.Block,triangles.Length);i+=3){
                Vector3 a=vertices[triangles[i]],b=vertices[triangles[i+1]],c=vertices[triangles[i+2]],normal=normals[i/3];
                if(!SurfaceContact.NearBounds(swept,a,b,c,reach))continue;
                if(length>1e-6f && Vector3.Dot(delta,normal)<0){
                    float hit;if(TriangleRay(previous,delta/length,first,a,b,c,out hit)){first=hit;contactNormal=normal;crossed=true;}
                }
                if(!SurfaceContact.NearBounds(desired,a,b,c,radius*2))continue;
                Vector3 closest=SurfaceContact.Triangle(desired,a,b,c);float distance=(desired-closest).sqrMagnitude;
                if(distance<nearest){nearest=distance;surface=closest;nearNormal=normal;}
            }
            }
            if(crossed){Vector3 hit=previous+delta.normalized*first;result=desired+contactNormal*(radius-Vector3.Dot(desired-hit,contactNormal));return true;}
            if(nearest<radius*radius){contactNormal=nearNormal;result=surface+nearNormal*radius;return true;}
            return false;
        }
    }
}
