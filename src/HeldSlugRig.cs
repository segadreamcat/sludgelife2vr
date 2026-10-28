using System.Collections.Generic;
using UnityEngine;

namespace SludgeLife2VR {
    // Copy transforms and renderers only. Instantiating the pickup would also run its eat/quest FSMs.
    internal static class HeldSlugRig {
        internal static bool Create(GameObject source,Transform geometry,List<Material> materials,out Bounds bounds) {
            bounds=new Bounds();Animator native=source.GetComponent<Animator>();
            if(native==null || native.runtimeAnimatorController==null || !source.name.StartsWith("FAUNA_SLUG"))return false;
            Dictionary<Transform,Transform> bones=new Dictionary<Transform,Transform>();
            // World size belongs outside the Animator, which may restore its own root transform.
            GameObject mount=new GameObject("Slug Grip Mount");mount.layer=31;mount.transform.SetParent(geometry,false);mount.transform.localScale=source.transform.lossyScale;
            GameObject root=new GameObject(source.name+" Held Rig");root.layer=31;root.transform.SetParent(mount.transform,false);
            bones[source.transform]=root.transform;
            CopyChildren(source.transform,root.transform,bones);
            List<SkinnedMeshRenderer> skins=new List<SkinnedMeshRenderer>();
            foreach(SkinnedMeshRenderer original in source.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if(original.sharedMesh==null || !original.enabled || !original.gameObject.activeInHierarchy)continue;
                Transform copy;if(!bones.TryGetValue(original.transform,out copy))continue;
                SkinnedMeshRenderer skin=copy.gameObject.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=original.sharedMesh;
                Transform[] nativeBones=original.bones,heldBones=new Transform[nativeBones.Length];
                for(int i=0;i<heldBones.Length;i++)if(nativeBones[i]!=null)bones.TryGetValue(nativeBones[i],out heldBones[i]);
                skin.bones=heldBones;Transform rootBone;if(original.rootBone!=null && bones.TryGetValue(original.rootBone,out rootBone))skin.rootBone=rootBone;
                skin.localBounds=original.localBounds;skin.updateWhenOffscreen=true;skin.quality=original.quality;
                Material[] originals=original.sharedMaterials,owned=new Material[originals.Length];
                for(int i=0;i<owned.Length;i++){owned[i]=new Material(originals[i]);materials.Add(owned[i]);}skin.sharedMaterials=owned;
                for(int i=0;i<original.sharedMesh.blendShapeCount;i++)skin.SetBlendShapeWeight(i,original.GetBlendShapeWeight(i));
                skins.Add(skin);
            }
            if(skins.Count==0){Object.Destroy(mount);return false;}
            Animator held=root.AddComponent<Animator>();held.avatar=native.avatar;held.runtimeAnimatorController=native.runtimeAnimatorController;
            held.applyRootMotion=false;held.cullingMode=AnimatorCullingMode.AlwaysAnimate;held.updateMode=AnimatorUpdateMode.UnscaledTime;
            held.Rebind();held.Play("Base Layer.SLUG2",0,0);held.Update(0);
            HeldSlugAnchor anchor=geometry.gameObject.AddComponent<HeldSlugAnchor>();
            if(anchor.Bind(mount.transform,skins.ToArray(),out bounds))return true;
            Object.Destroy(anchor);Object.Destroy(mount);return false;
        }
        static void CopyChildren(Transform source,Transform parent,Dictionary<Transform,Transform> bones) {
            foreach(Transform child in source) {
                GameObject part=new GameObject(child.name);part.layer=31;Transform copy=part.transform;copy.SetParent(parent,false);
                copy.localPosition=child.localPosition;copy.localRotation=child.localRotation;copy.localScale=child.localScale;bones[child]=copy;
                CopyChildren(child,copy,bones);
            }
        }
    }
}
