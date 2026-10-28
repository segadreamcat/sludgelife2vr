using System.Collections.Generic;
using UnityEngine;
namespace SludgeLife2VR {
    // A small cache of real collision triangles for the opening frame. No added colliders.
    internal sealed class RoomHoldSurface {
        internal MeshCollider Collider;
        Vector3[] local,world;int[] triangles;Matrix4x4 matrix;
        internal static RoomHoldSurface Create(MeshCollider c,Transform board) {
            Mesh mesh=c.sharedMesh;if(mesh==null || !mesh.isReadable)return null;
            Vector3[] vertices=mesh.vertices;int[] source=mesh.triangles;List<int> selected=new List<int>();
            Matrix4x4 toBoard=board.worldToLocalMatrix*c.transform.localToWorldMatrix;
            for(int i=0;i<source.Length;i+=3)if(RoomClimbMath.OverlapsReach(toBoard.MultiplyPoint3x4(vertices[source[i]]),
                toBoard.MultiplyPoint3x4(vertices[source[i+1]]),toBoard.MultiplyPoint3x4(vertices[source[i+2]]))) {
                selected.Add(source[i]);selected.Add(source[i+1]);selected.Add(source[i+2]);
            }
            if(selected.Count==0)return null;
            RoomHoldSurface result=new RoomHoldSurface();result.Collider=c;result.local=vertices;result.triangles=selected.ToArray();return result;
        }
        internal bool Contact(Transform board,Vector3 hand,float range,out Vector3 point) {
            point=hand;if(Collider==null || !Collider.enabled || !Collider.gameObject.activeInHierarchy)return false;
            Matrix4x4 now=Collider.transform.localToWorldMatrix;
            if(world==null || matrix!=now){matrix=now;world=new Vector3[local.Length];for(int i=0;i<local.Length;i++)world[i]=now.MultiplyPoint3x4(local[i]);}
            return RoomHoldMath.Contact(world,triangles,board.worldToLocalMatrix,hand,range,out point);
        }
    }
}
