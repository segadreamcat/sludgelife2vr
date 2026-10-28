using System.Collections.Generic;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class GraffitiClimbWall {
        internal MeshCollider Collider;Vector3[] local,world;int[] triangles;Matrix4x4 matrix;
        internal static GraffitiClimbWall Create(MeshCollider c) {
            Mesh mesh=c.sharedMesh;if(mesh==null || !mesh.isReadable)return null;
            Vector3[] vertices=mesh.vertices;int[] source=mesh.triangles;Matrix4x4 m=c.transform.localToWorldMatrix;List<int> selected=new List<int>();
            for(int i=0;i<source.Length;i+=3)if(GraffitiWallMath.Overlaps(m.MultiplyPoint3x4(vertices[source[i]]),m.MultiplyPoint3x4(vertices[source[i+1]]),m.MultiplyPoint3x4(vertices[source[i+2]]))) {
                selected.Add(source[i]);selected.Add(source[i+1]);selected.Add(source[i+2]);
            }
            if(selected.Count==0)return null;
            GraffitiClimbWall result=new GraffitiClimbWall();result.Collider=c;result.local=vertices;result.triangles=selected.ToArray();return result;
        }
        internal bool Contact(Vector3 hand,float range,out Vector3 point) {
            point=hand;if(Collider==null || !Collider.enabled || !Collider.gameObject.activeInHierarchy || !GraffitiWallMath.Near(hand,range))return false;
            Matrix4x4 now=Collider.transform.localToWorldMatrix;
            if(world==null || matrix!=now){matrix=now;world=new Vector3[local.Length];for(int i=0;i<local.Length;i++)world[i]=now.MultiplyPoint3x4(local[i]);}
            return GraffitiWallMath.Contact(world,triangles,hand,range,out point);
        }
    }
}
