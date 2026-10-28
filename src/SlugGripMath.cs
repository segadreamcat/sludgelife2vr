using System.Collections.Generic;
using UnityEngine;
namespace SludgeLife2VR {
    internal static class SlugGripMath {
        internal static void Encapsulate(IList<Vector3> vertices,Matrix4x4 frame,ref Bounds bounds,ref bool found) {
            for(int i=0;i<vertices.Count;i++) {
                Vector3 point=frame.MultiplyPoint3x4(vertices[i]);
                if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
            }
        }
        internal static Vector3 CenterMount(Vector3 mount,Vector3 visibleCenter){return mount-visibleCenter;}
    }
}
