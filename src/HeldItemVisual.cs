using System.Collections.Generic;
using UnityEngine;

namespace SludgeLife2VR {
    internal sealed class HeldItemVisual {
        internal GameObject Root;
        internal bool Soda;
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Material> materials = new List<Material>();
        Rigidbody rigidbody;
        BoxCollider collider;
        Vector3 size;
        internal static HeldItemVisual Create(GameObject source,bool soda){return Create(source,soda,0);}
        internal static HeldItemVisual Create(GameObject source, bool soda,float physicalSize) {
            HeldItemVisual item = new HeldItemVisual(); item.Soda = soda;
            item.Root = new GameObject("VR Held Native " + source.name); item.Root.layer = 2;
            GameObject geometry = new GameObject("Native Visual"); geometry.layer = 31; geometry.transform.SetParent(item.Root.transform,false);
            Bounds bounds = new Bounds(); bool found = !soda && HeldSlugRig.Create(source,geometry.transform,item.materials,out bounds);
            Matrix4x4 frame=Matrix4x4.TRS(source.transform.position,source.transform.rotation,Vector3.one).inverse;
            if(!found) foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true)) {
                if (Diagnostics.PathOf(renderer.transform).Contains("/UI_PRESS") || Diagnostics.PathOf(renderer.transform).Contains("/PARTICLE") || renderer is ParticleSystemRenderer || renderer.gameObject.layer == 5 || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Mesh mesh = null; SkinnedMeshRenderer skin = renderer as SkinnedMeshRenderer;
                if (skin != null) { mesh = new Mesh(); skin.BakeMesh(mesh); item.meshes.Add(mesh); }
                else { MeshFilter mf = renderer.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                if (mesh == null) continue;
                Matrix4x4 transform=frame*renderer.localToWorldMatrix;
                mesh=Object.Instantiate(mesh);item.meshes.Add(mesh);
                if(renderer.isPartOfStaticBatch) {
                    System.Reflection.PropertyInfo index=typeof(Renderer).GetProperty("staticBatchIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
                    if(index==null){item.Clear();return null;}
                    int first=(int)index.GetValue(renderer,null),count=renderer.sharedMaterials.Length;
                    if(first<0 || first+count>mesh.subMeshCount){item.Clear();return null;}
                    int[][] selected=new int[count][];for(int i=0;i<count;i++)selected[i]=mesh.GetTriangles(first+i);
                    mesh.subMeshCount=count;for(int i=0;i<count;i++)mesh.SetTriangles(selected[i],i);
                }
                Vector3[] vertices=mesh.vertices;ItemGeometryMath.Bake(vertices,transform);mesh.vertices=vertices;if(transform.determinant<0)for(int sub=0;sub<mesh.subMeshCount;sub++){int[] indices=mesh.GetTriangles(sub);for(int i=0;i<indices.Length;i+=3){int swap=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=swap;}mesh.SetTriangles(indices,sub);}mesh.RecalculateNormals();mesh.RecalculateBounds();if(renderer.isPartOfStaticBatch){int[] used=mesh.triangles;Bounds actual=new Bounds(vertices[used[0]],Vector3.zero);foreach(int i in used)actual.Encapsulate(vertices[i]);mesh.bounds=actual;}
                GameObject part = new GameObject(renderer.name,typeof(MeshFilter),typeof(MeshRenderer)); part.layer = 31;
                part.transform.SetParent(geometry.transform,false);
                part.GetComponent<MeshFilter>().sharedMesh = mesh;
                Material[] originals = renderer.sharedMaterials, owned = new Material[originals.Length];
                for (int i = 0; i < owned.Length; i++) { owned[i] = new Material(originals[i]); item.materials.Add(owned[i]); }
                part.GetComponent<MeshRenderer>().sharedMaterials = owned;
                Bounds local = mesh.bounds;
                for (int corner = 0; corner < 8; corner++) {
                    Vector3 point = local.center+Vector3.Scale(local.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    point = part.transform.localPosition+part.transform.localRotation*Vector3.Scale(point,part.transform.localScale);
                    if (!found) { bounds = new Bounds(point,Vector3.zero); found = true; } else bounds.Encapsulate(point);
                }
            }
            if (!found || bounds.size.sqrMagnitude < .000001f) { item.Clear(); return null; }
            Vector3 longest = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? Vector3.right : bounds.size.y >= bounds.size.z ? Vector3.up : Vector3.forward;
            Quaternion rotation = Quaternion.FromToRotation(longest,soda ? Vector3.up : Vector3.forward);
            if(soda)rotation=Quaternion.AngleAxis(180,Vector3.up)*rotation;
            float scale = physicalSize>0 ? physicalSize/Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z)) : 1;
            geometry.transform.localRotation = rotation; geometry.transform.localScale = Vector3.one*scale;
            geometry.transform.localPosition = -(rotation*bounds.center)*scale;
            Vector3 rotated = rotation*bounds.size*scale; item.size = new Vector3(Mathf.Abs(rotated.x),Mathf.Abs(rotated.y),Mathf.Abs(rotated.z));
            return item;
        }
        internal Vector3 Position { get { return Root.transform.position; } }
        internal Vector3 UsePoint { get { return Root.transform.position+(Soda ? Root.transform.up*(size.y*.5f)*Root.transform.lossyScale.x : Vector3.zero); } }
        internal float MouthDistance(Vector3 mouth){return Vector3.Distance(mouth,Root.transform.TransformPoint(ItemGeometryMath.MouthPoint(Root.transform.InverseTransformPoint(mouth),size,Soda)));}
        internal void Hold(Hand hand, bool left, float scale) {
            if (rigidbody != null) { rigidbody.isKinematic = true; rigidbody.velocity = Vector3.zero; }
            if (collider != null) collider.enabled = false;
            Root.transform.SetParent(hand.Visual.transform,false);
            float palmOffset=.030f+(Soda?size.x*.5f/Mathf.Max(.001f,scale):.020f);
            Root.transform.localPosition = new Vector3(left ? palmOffset : -palmOffset,Soda ? -.025f : 0,.050f);
            Root.transform.localRotation = Quaternion.identity; Root.transform.localScale = Vector3.one/Mathf.Max(.001f,hand.Visual.transform.lossyScale.x);
        }
        internal void Drop(CharacterController body, Vector3 velocity, float scale) {
            Root.transform.SetParent(null,true);
            if (collider == null) { collider = Root.AddComponent<BoxCollider>(); collider.size = size; }
            collider.enabled = true;
            if (body != null) Physics.IgnoreCollision(collider,body,true);
            if (rigidbody == null) { rigidbody = Root.AddComponent<Rigidbody>(); rigidbody.mass = .12f; rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; }
            rigidbody.isKinematic = false; rigidbody.velocity = Vector3.ClampMagnitude(velocity,6*scale);
        }
        internal void Clear() {
            if (Root != null) Object.Destroy(Root);
            foreach (Material material in materials) if (material != null) Object.Destroy(material);
            foreach (Mesh mesh in meshes) if (mesh != null) Object.Destroy(mesh);
            materials.Clear(); meshes.Clear(); Root = null;
        }
    }
}

