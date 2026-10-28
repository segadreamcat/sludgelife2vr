using System.Collections.Generic;
using UnityEngine;

namespace SludgeLife2VR {
    // Keep the visible animated body at the grip. Culling bounds and avatar/root offsets are not grip points.
    public sealed class HeldSlugAnchor : MonoBehaviour {
        Transform mount;
        SkinnedMeshRenderer[] skins;
        Mesh[] baked;
        readonly List<Vector3> vertices=new List<Vector3>();
        internal bool Bind(Transform gripMount,SkinnedMeshRenderer[] renderers,out Bounds bounds) {
            mount=gripMount;skins=renderers;baked=new Mesh[skins.Length];
            for(int i=0;i<baked.Length;i++)baked[i]=new Mesh();
            return Center(out bounds);
        }
        bool Center(out Bounds bounds) {
            bounds=new Bounds();bool found=false;
            if(mount==null || skins==null)return false;
            for(int part=0;part<skins.Length;part++) {
                SkinnedMeshRenderer skin=skins[part];if(skin==null || skin.sharedMesh==null)continue;
                skin.BakeMesh(baked[part]);baked[part].GetVertices(vertices);
                Matrix4x4 frame=transform.worldToLocalMatrix*skin.localToWorldMatrix;
                SlugGripMath.Encapsulate(vertices,frame,ref bounds,ref found);
            }
            if(!found || bounds.size.sqrMagnitude<.000001f)return false;
            // Translation only: retain the native scale, bone rotations and all local deformation.
            mount.localPosition=SlugGripMath.CenterMount(mount.localPosition,bounds.center);
            bounds=new Bounds(Vector3.zero,bounds.size);return true;
        }
        void LateUpdate(){Bounds bounds;Center(out bounds);}
        void OnDestroy(){if(baked!=null)foreach(Mesh mesh in baked)if(mesh!=null)Object.Destroy(mesh);baked=null;}
    }
}
