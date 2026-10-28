using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class WorldButtonSurface {
        sealed class Part {internal Renderer Renderer;internal Vector3[] Vertices;internal int[] Triangles;internal bool Patch;internal Bounds LocalBounds,WorldBounds;internal Vector3[] World;internal Matrix4x4 Matrix;internal int Frame=-1;internal bool Prepared;internal string MeshName;}
        readonly List<Part> parts=new List<Part>();
        Transform marker;
        internal Transform Scope;
        internal bool ShipControl,NativeInputOnly;
        internal int Count {get{return parts.Count;}}
        internal static WorldButtonSurface Create(PlayMakerFSM interact,PlayMakerFSM activate,GameObject current) {
            WorldButtonSurface surface=new WorldButtonSurface();Transform parent=interact.transform.parent;
            surface.marker=interact.transform;
            surface.Scope=parent==null?interact.transform:parent;
            if(surface.BindShipCap(interact))return surface;
            List<GameObject> candidates=new List<GameObject>();
            if(parent!=null){Transform mesh=parent.Find("BTN_MESH");if(mesh!=null)candidates.Add(mesh.gameObject);}
            if(activate!=null)foreach(FsmState state in activate.Fsm.States)foreach(FsmStateAction action in state.Actions){
                SetPosition move=action as SetPosition;if(move==null)continue;
                GameObject target=activate.Fsm.GetOwnerDefaultTarget(move.gameObject);
                if(target!=null && target.transform.IsChildOf(surface.Scope))candidates.Add(target);
            }
            FsmGameObject nativeMesh=interact.FsmVariables.FindFsmGameObject("MESH");
            if(nativeMesh!=null && nativeMesh.Value!=null && nativeMesh.Value.transform.IsChildOf(surface.Scope))candidates.Add(nativeMesh.Value);
            candidates.Add(current);
            foreach(GameObject candidate in candidates){
                if(candidate==null)continue;
                foreach(Renderer r in candidate.GetComponentsInChildren<Renderer>(true))surface.Add(r,interact.transform.position,false);
                if(surface.parts.Count>0)return surface;
            }
            // Some console buttons are triangles in the console mesh, beside an invisible SECRET_BTN.
            // Use only a small patch around that native marker, never the whole console's bounds.
            if(parent!=null && parent.parent!=null && parent.name.StartsWith("INTERACT"))surface.Scope=parent.parent;
            if(surface.Scope.name.StartsWith("LVL_SHIP")) {
                Transform panel=surface.Scope.Find("SHIP_INSIDE1");
                if(panel!=null)foreach(Renderer renderer in panel.GetComponentsInChildren<Renderer>(true))surface.Add(renderer,interact.transform.position,true);
                if(surface.parts.Count>0)return surface;
            }
            foreach(Renderer renderer in surface.Scope.GetComponentsInChildren<Renderer>(true))surface.Add(renderer,interact.transform.position,true);
            return surface;
        }
        bool BindShipCap(PlayMakerFSM interact){
            NativeInputOnly=ShipButtonMath.LaunchOwner(interact.name);
            Transform ship=null;
            for(Transform p=interact.transform.parent;p!=null;p=p.parent)if(p.name.StartsWith("LVL_SHIP")){ship=p;break;}
            // The first game's launch interaction is a sibling of the moving ship, outside its model.
            if(ship==null && NativeInputOnly && interact.transform.parent!=null){Transform moving=interact.transform.parent.Find("ESCAPE_SHIP");if(moving!=null)ship=moving.Find("LVL_SHIP");}
            if(ship==null)return false;
            MeshFilter selected=null;float nearest=float.MaxValue;
            foreach(MeshFilter filter in ship.GetComponentsInChildren<MeshFilter>(true)){
                Mesh mesh=filter.sharedMesh;if(mesh==null || !ShipButtonMath.Cap(mesh.name) || !mesh.isReadable || filter.GetComponent<Renderer>()==null)continue;
                float distance=(filter.transform.TransformPoint(mesh.bounds.center)-marker.position).sqrMagnitude;
                if(ShipButtonMath.Candidate(distance,NativeInputOnly) && distance<nearest){selected=filter;nearest=distance;}
            }
            if(selected==null)return false;
            Add(selected.GetComponent<Renderer>(),marker.position,false);
            if(parts.Count==0)return false;
            Scope=ship;ShipControl=true;
            BepInEx.Logging.Logger.CreateLogSource("VR Ship Buttons").LogInfo("Bound "+Diagnostics.PathOf(interact.transform)+" to visible red cap "+Diagnostics.PathOf(selected.transform)+"; marker offset="+Mathf.Sqrt(nearest).ToString("F3")+"m nativeInputOnly="+NativeInputOnly);
            return true;
        }
        void Add(Renderer renderer,Vector3 marker,bool patch) {
            if(renderer==null || renderer is ParticleSystemRenderer || renderer.gameObject.layer==5 || renderer is SkinnedMeshRenderer)return;
            MeshFilter filter=renderer.GetComponent<MeshFilter>();Mesh mesh=filter==null?null:filter.sharedMesh;
            if(mesh==null || !mesh.isReadable)return;
            if(patch && renderer.bounds.SqrDistance(marker)>.2f*.2f)return;
            Vector3[] vertices=mesh.vertices;List<int> triangles=new List<int>();
            if(renderer.isPartOfStaticBatch){
                System.Reflection.PropertyInfo field=typeof(Renderer).GetProperty("staticBatchIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
                if(field==null)return;int first=(int)field.GetValue(renderer,null);
                for(int i=0;i<renderer.sharedMaterials.Length && first+i<mesh.subMeshCount;i++)triangles.AddRange(mesh.GetTriangles(first+i));
            }else triangles.AddRange(mesh.triangles);
            if(patch){
                Matrix4x4 matrix=renderer.localToWorldMatrix;List<int> selected=new List<int>();
                for(int i=0;i<triangles.Count;i+=3){
                    Vector3 a=matrix.MultiplyPoint3x4(vertices[triangles[i]]),b=matrix.MultiplyPoint3x4(vertices[triangles[i+1]]),c=matrix.MultiplyPoint3x4(vertices[triangles[i+2]]);
                    if((SurfaceContact.Triangle(marker,a,b,c)-marker).sqrMagnitude>.18f*.18f)continue;
                    selected.Add(triangles[i]);selected.Add(triangles[i+1]);selected.Add(triangles[i+2]);
                }
                triangles=selected;
            }
            if(triangles.Count>0){
                Bounds bounds=new Bounds(vertices[triangles[0]],Vector3.zero);foreach(int index in triangles)bounds.Encapsulate(vertices[index]);
                parts.Add(new Part{Renderer=renderer,Vertices=vertices,Triangles=triangles.ToArray(),Patch=patch,LocalBounds=bounds,MeshName=mesh.name});
            }
        }
        internal string Describe(){string result="";foreach(Part part in parts)result+=(result.Length>0?",":"")+part.MeshName+":"+(part.Triangles.Length/3);return result;}
        internal float Distance(Vector3 previous,Vector3 point,bool sweep) {
            float nearest=float.MaxValue;
            foreach(Part part in parts){
                if(part.Renderer==null || !part.Renderer.gameObject.activeInHierarchy)continue;
                if(part.Patch && marker!=null && !ButtonContactMath.NearMarker(previous,point,marker.position))continue;
                Matrix4x4 matrix=part.Renderer.localToWorldMatrix;
                if(part.Frame!=Time.frameCount){part.WorldBounds=ContactBoundsMath.Transform(part.LocalBounds,matrix);part.Frame=Time.frameCount;}
                if(!ContactBoundsMath.Near(part.WorldBounds,previous,point,.055f))continue;
                if(!part.Prepared || matrix!=part.Matrix){
                    if(part.World==null)part.World=new Vector3[part.Vertices.Length];
                    for(int i=0;i<part.World.Length;i++)part.World[i]=matrix.MultiplyPoint3x4(part.Vertices[i]);
                    part.Matrix=matrix;part.Prepared=true;
                }
                nearest=Mathf.Min(nearest,ButtonContactMath.Distance(previous,point,part.World,part.Triangles,Matrix4x4.identity,sweep));
            }
            return nearest;
        }
    }
}
