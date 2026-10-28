using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class PhysicalTouch {
        sealed class Skin {
            internal Renderer Renderer;internal Mesh Baked,SourceMesh;internal readonly List<Vector3> Local=new List<Vector3>();internal Vector3[] World,Normals;internal int[] Triangles;internal float Next,Seen,NextCollider;internal Bounds[] Groups;internal Matrix4x4 PreviousMatrix;internal GameObject Proxy;internal Mesh CollisionMesh;internal MeshCollider Collider;
            internal void Clear(){if(Collider!=null){Collider.enabled=false;HandPhysicsBody.Surfaces.Remove(Collider);}if(Proxy!=null)Object.Destroy(Proxy);if(CollisionMesh!=null)Object.Destroy(CollisionMesh);if(Baked!=null)Object.Destroy(Baked);}
            Transform Head;Matrix4x4 RestFromHead,WorldToRest,NormalToRest;int[] HeadVertices;Bounds HeadBounds;bool HasHead;
            internal void FindHead(SkinnedMeshRenderer renderer,Mesh mesh){
                HeadVertices=new int[0];if(renderer==null || !mesh.name.StartsWith("CAT_MESH"))return;
                Transform[] bones=renderer.bones;int head=-1;for(int i=0;i<bones.Length;i++)if(bones[i]!=null && bones[i].name=="Bone_004"){head=i;break;}
                if(head<0 || head>=mesh.bindposes.Length)return;Head=bones[head];RestFromHead=mesh.bindposes[head].inverse;
                bool[] part=new bool[bones.Length];for(int i=0;i<bones.Length;i++)part[i]=bones[i]!=null && (bones[i]==Head || bones[i].IsChildOf(Head));
                BoneWeight[] weights=mesh.boneWeights;List<int> selected=new List<int>();
                for(int i=0;i<weights.Length;i++){BoneWeight b=weights[i];float w=HeadWeight(part,b.boneIndex0,b.weight0)+HeadWeight(part,b.boneIndex1,b.weight1)+HeadWeight(part,b.boneIndex2,b.weight2)+HeadWeight(part,b.boneIndex3,b.weight3);if(w>=.45f)selected.Add(i);}
                HeadVertices=selected.ToArray();
            }
            static float HeadWeight(bool[] part,int index,float weight){return index>=0 && index<part.Length && part[index]?weight:0;}
            internal bool Chin(Vector3 point,Vector3 normal){return HasHead && Head!=null && PetHapticsMath.UnderChin(WorldToRest.MultiplyPoint3x4(point),NormalToRest.MultiplyVector(normal).normalized,HeadBounds);}
            internal bool Refresh(Vector3 palm,Vector3 tip,float scale){
                if(Renderer==null || !Renderer.enabled || !Renderer.gameObject.activeInHierarchy)return false;
                Bounds bounds=Renderer.bounds;
                if(bounds.SqrDistance(palm)>.35f*.35f*scale*scale && bounds.SqrDistance(tip)>.35f*.35f*scale*scale)return false;
                Seen=Time.unscaledTime;
                if(World!=null && Time.unscaledTime<Next){if(Collider!=null)Collider.enabled=true;return true;}if(remainingBakes<=0){if(Collider!=null)Collider.enabled=true;return World!=null;}remainingBakes--;Next=Time.unscaledTime+.05f;
                Mesh mesh;SkinnedMeshRenderer skinned=Renderer as SkinnedMeshRenderer;
                if(skinned!=null){if(Baked==null)Baked=new Mesh();skinned.BakeMesh(Baked);mesh=Baked;}
                else{MeshFilter filter=Renderer.GetComponent<MeshFilter>();mesh=filter==null?null:filter.sharedMesh;}
                if(mesh==null || !mesh.isReadable)return false;
                Mesh sourceMesh=skinned!=null?skinned.sharedMesh:mesh;
                if(Triangles==null || SourceMesh!=sourceMesh){
                    if(Renderer.isPartOfStaticBatch){System.Reflection.PropertyInfo field=typeof(Renderer).GetProperty("staticBatchIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);if(field==null)return false;int first=(int)field.GetValue(Renderer,null);List<int> indices=new List<int>();for(int part=0;part<Renderer.sharedMaterials.Length && first+part<mesh.subMeshCount;part++)indices.AddRange(mesh.GetTriangles(first+part));Triangles=indices.ToArray();}
                    else Triangles=mesh.triangles;SourceMesh=sourceMesh;FindHead(skinned,sourceMesh);
                }
                Matrix4x4 matrix=Renderer.localToWorldMatrix;
                if(skinned==null && World!=null && matrix==PreviousMatrix){if(Collider!=null)Collider.enabled=true;return true;}PreviousMatrix=matrix;
                mesh.GetVertices(Local);List<Vector3> local=Local;
                if(World==null || World.Length!=local.Count)World=new Vector3[local.Count];
                for(int i=0;i<local.Count;i++)World[i]=matrix.MultiplyPoint3x4(local[i]);
                HasHead=false;if(Head!=null && HeadVertices!=null && HeadVertices.Length>0){WorldToRest=RestFromHead*Head.worldToLocalMatrix;NormalToRest=WorldToRest.inverse.transpose;HeadBounds=new Bounds(WorldToRest.MultiplyPoint3x4(World[HeadVertices[0]]),Vector3.zero);foreach(int vertex in HeadVertices)if(vertex<World.Length)HeadBounds.Encapsulate(WorldToRest.MultiplyPoint3x4(World[vertex]));HasHead=true;}
                if(Normals==null || Normals.Length!=Triangles.Length/3)Normals=new Vector3[Triangles.Length/3];
                float sign=matrix.determinant<0?-1:1;
                for(int i=0;i<Triangles.Length;i+=3)Normals[i/3]=Vector3.Cross(World[Triangles[i+1]]-World[Triangles[i]],World[Triangles[i+2]]-World[Triangles[i]]).normalized*sign;
                Groups=ContactBoundsMath.Groups(World,Triangles,Groups);
                if(Proxy==null){Proxy=new GameObject("VR Body Surface");Proxy.layer=31;Rigidbody anchor=Proxy.AddComponent<Rigidbody>();anchor.isKinematic=true;anchor.useGravity=false;Collider=Proxy.AddComponent<MeshCollider>();CollisionMesh=new Mesh();HandPhysicsBody.Surfaces.Add(Collider);}
                Collider.enabled=true;if(Time.unscaledTime<NextCollider)return true;NextCollider=Time.unscaledTime+.08f;Collider.sharedMesh=null;CollisionMesh.Clear();CollisionMesh.vertices=World;if(sign<0){int[] reverse=(int[])Triangles.Clone();for(int i=0;i<reverse.Length;i+=3){int saved=reverse[i+1];reverse[i+1]=reverse[i+2];reverse[i+2]=saved;}CollisionMesh.triangles=reverse;}else CollisionMesh.triangles=Triangles;CollisionMesh.bounds=Renderer.bounds;Collider.sharedMesh=CollisionMesh;
                return true;
            }
        }
        sealed class Actor {internal Transform Root;internal AudioSource Sound;internal bool Dog;internal PlayMakerFSM Brain,Look;internal bool Pet;internal readonly List<Skin> Skins=new List<Skin>();internal float NextReaction,TouchUntil,PetUntil;}
        sealed class Palm {internal Vector3 Previous;internal bool Valid;internal Actor Actor;internal float Time,Travel,NextHaptic;internal Vector3 LastContact;internal HandPhysicsBody Physics;internal readonly CatPurr Purr=new CatPurr();}
        readonly List<Actor> actors=new List<Actor>();readonly Palm left=new Palm(),right=new Palm();float nextScan;bool layerSaved,originalLayerIgnore;
        AudioClip[] cats,dogs;static int remainingBakes;
        readonly HashSet<Transform> knownActors=new HashSet<Transform>();readonly HashSet<Renderer> knownSkins=new HashSet<Renderer>();int fsmCursor,rendererCursor;
        bool Enabled {get{return VrPlugin.Active && VrPlugin.Instance.MotionInput.Value && VrPlugin.Instance.PhysicalInteractions.Value;}}
        void Scan(){
            if(Time.unscaledTime<nextScan)return;
            PlayMakerFSM[] fsms=SceneInventory.Fsms;int end=System.Math.Min(fsms.Length,fsmCursor+64);
            for(;fsmCursor<end;fsmCursor++){PlayMakerFSM f=fsms[fsmCursor];
                if(f==null || f.FsmName!="BRAIN" || !f.gameObject.activeInHierarchy || !f.gameObject.scene.IsValid())continue;
                string path=Diagnostics.PathOf(f.transform);bool pet=f.name.StartsWith("FAUNA_CAT") || f.name.StartsWith("FAUNA_DOG");
                if(!pet && !path.StartsWith("NPCS/"))continue;
                if(knownActors.Contains(f.transform))continue;
                Actor actor=new Actor{Root=f.transform,Brain=f,Pet=pet,Dog=f.name.StartsWith("FAUNA_DOG")};
                foreach(PlayMakerFSM sibling in f.GetComponents<PlayMakerFSM>())if(sibling.FsmName=="LOOK")actor.Look=sibling;
                foreach(Renderer r in f.GetComponentsInChildren<Renderer>(true)){
                    if(!(r is SkinnedMeshRenderer) && r.GetComponent<MeshFilter>()==null)continue;
                    string rp=Diagnostics.PathOf(r.transform);if(r is ParticleSystemRenderer || rp.Contains("/UI_PRESS") || r.gameObject.layer==5 || rp.Contains("LOOK_TARGET"))continue;
                    actor.Skins.Add(new Skin{Renderer=r});
                }
                if(actor.Skins.Count>0){actors.Add(actor);knownActors.Add(actor.Root);foreach(Skin skin in actor.Skins)knownSkins.Add(skin.Renderer);}
            }
            // Some sleeping, watching and decorative animals have no active BRAIN FSM.
            Renderer[] renderers=SceneInventory.Renderers;end=System.Math.Min(renderers.Length,rendererCursor+96);
            for(;rendererCursor<end;rendererCursor++) {Renderer renderer=renderers[rendererCursor];
                if(renderer!=null && knownSkins.Contains(renderer))continue;
                if(renderer==null || !renderer.gameObject.scene.IsValid() || !renderer.gameObject.activeInHierarchy || (!(renderer is SkinnedMeshRenderer) && renderer.GetComponent<MeshFilter>()==null))continue;
                Transform root=renderer.transform,animal=null;while(root!=null){if(root.name.StartsWith("FAUNA_CAT")||root.name.StartsWith("FAUNA_DOG"))animal=root;root=root.parent;}
                if(animal==null || knownActors.Contains(animal))continue;
                Actor actor=new Actor{Root=animal,Pet=true,Dog=animal.name.StartsWith("FAUNA_DOG")};foreach(Renderer r in animal.GetComponentsInChildren<Renderer>(true))if((r is SkinnedMeshRenderer || r.GetComponent<MeshFilter>()!=null) && r.gameObject.layer!=5 && !Diagnostics.PathOf(r.transform).Contains("/UI_PRESS"))actor.Skins.Add(new Skin{Renderer=r});actors.Add(actor);knownActors.Add(actor.Root);foreach(Skin skin in actor.Skins)knownSkins.Add(skin.Renderer);
            }
            if(fsmCursor>=fsms.Length && rendererCursor>=renderers.Length){fsmCursor=rendererCursor=0;nextScan=Time.unscaledTime+5.47f;}
        }
        internal void Update(float scale,bool freeLook){
            if(!Enabled){Clear();return;}
            if(VrPlugin.Instance.UI.MenuOpen){left.Valid=right.Valid=false;left.Purr.Reset();right.Purr.Reset();foreach(Actor a in actors)foreach(Skin s in a.Skins)if(s.Collider!=null)s.Collider.enabled=false;if(left.Physics!=null)left.Physics.Disable();if(right.Physics!=null)right.Physics.Disable();return;}
            if(!layerSaved){originalLayerIgnore=Physics.GetIgnoreLayerCollision(31,31);Physics.IgnoreLayerCollision(31,31,false);layerSaved=true;}
            remainingBakes=2;Scan();VrPlugin p=VrPlugin.Instance;
            Resolve(left,p.Left,scale,p.ClimbControls.LeftHeld || p.GliderGrips.LeftHeld);Resolve(right,p.Right,scale,p.ClimbControls.RightHeld || p.GliderGrips.RightHeld);
            foreach(Actor a in actors)foreach(Skin s in a.Skins)if(s.Collider!=null && (a.Root==null || !a.Root.gameObject.activeInHierarchy || Time.unscaledTime-s.Seen>.10f))s.Collider.enabled=false;
        }
        void Resolve(Palm palm,Hand hand,float scale,bool anchored){
            if(!hand.Valid || anchored){palm.Valid=false;palm.Purr.Reset();if(palm.Physics!=null)palm.Physics.Disable();return;}
            Vector3 raw=hand.Visual.transform.TransformPoint(hand.GripLocalPoint),start=raw;
            if(!palm.Valid || Vector3.Distance(raw,palm.Previous)>.6f*scale)palm.Previous=raw;
            Actor touched=null;Skin touchedSkin=null;Vector3 contactNormal=Vector3.zero,contactPoint=Vector3.zero;bool near=false;
            foreach(Actor actor in actors){
                if(actor.Root==null || !actor.Root.gameObject.activeInHierarchy)continue;
                foreach(Skin skin in actor.Skins){
                    if(!skin.Refresh(raw,hand.IndexTip,scale))continue;near=true;
                    Vector3 solved,n;
                    if(BodyContactMath.Resolve(palm.Previous,raw,.026f*scale,skin.World,skin.Triangles,skin.Normals,skin.Groups,out solved,out n)){
                        raw=solved;touched=actor;touchedSkin=skin;contactNormal=n;contactPoint=solved-n*.026f*scale;
                    }
                    // Resolve the actual articulated fingertips as well as the palm.
                    for(int finger=0;finger<5;finger++){
                        Vector3 tip=hand.FingerTip(finger)+(raw-start);
                        Vector3 previous=tip+(palm.Previous-raw);
                        if(BodyContactMath.Resolve(previous,tip,.008f*scale,skin.World,skin.Triangles,skin.Normals,skin.Groups,out solved,out n)){
                            raw+=solved-tip;touched=actor;touchedSkin=skin;contactNormal=n;contactPoint=solved-n*.008f*scale;
                        }
                    }
                }
            }
            if(palm.Physics==null)palm.Physics=HandPhysicsBody.Create();raw=palm.Physics.Follow(hand,start,raw,scale,near);
            hand.Visual.transform.position+=raw-start;palm.Previous=raw;palm.Valid=true;
            if(touched==null && palm.Physics.Contact!=null)foreach(Actor actor in actors)foreach(Skin skin in actor.Skins)if(skin.Collider==palm.Physics.Contact){touched=actor;touchedSkin=skin;contactNormal=palm.Physics.Normal;contactPoint=palm.Physics.Point;}
            bool chin=touched!=null && touched.Pet && !touched.Dog && touchedSkin!=null && touchedSkin.Chin(contactPoint,contactNormal);
            ushort purr=palm.Purr.Step(chin,Time.unscaledDeltaTime);if(purr>0)VrPlugin.Instance.PulseHand(hand,purr);
            if(touched==null){palm.Actor=null;palm.Time=palm.Travel=0;return;}
            touched.TouchUntil=Time.unscaledTime+.12f;
            if(!palm.Purr.Active && Time.unscaledTime>=palm.NextHaptic){palm.NextHaptic=Time.unscaledTime+.055f;VrPlugin.Instance.PulseHand(hand,(ushort)Mathf.Clamp(220+(raw-start).magnitude/scale*5500,220,1100));}
            if(palm.Actor!=touched){palm.Actor=touched;palm.Time=palm.Travel=0;palm.LastContact=raw;}
            palm.Time+=Mathf.Min(.05f,Time.unscaledDeltaTime);
            Vector3 movement=raw-palm.LastContact;palm.Travel+=Vector3.ProjectOnPlane(movement,contactNormal).magnitude/scale;palm.LastContact=raw;
            if(touched.Pet && palm.Time>.25f && palm.Travel>.065f && Time.unscaledTime>=touched.NextReaction){
                touched.PetUntil=Time.unscaledTime+.45f;
                FsmState state=touched.Brain==null?null:touched.Brain.Fsm.ActiveState;bool sent=false;
                if(state!=null)foreach(FsmTransition transition in state.Transitions)if(transition.EventName=="TRIGGER"){
                    touched.Brain.SendEvent("TRIGGER");sent=true;break;
                }
                if(!sent && touched.Look!=null && touched.Look.isActiveAndEnabled && touched.Look.Fsm.GetState("MEOW")!=null && touched.Look.ActiveStateName.StartsWith("LOOK")){touched.Look.SetState("MEOW");sent=true;}
                if(!sent)React(touched);
                touched.NextReaction=Time.unscaledTime+1.6f;palm.Travel=0;if(!chin)VrPlugin.Instance.PulseHand(hand,650);
            }
        }
        void React(Actor actor) {
            // Keep scripted behaviours intact; noninteractive animals get a local native pet sound.
            if(cats==null || dogs==null || (actor.Dog?dogs.Length==0:cats.Length==0)){List<AudioClip> c=new List<AudioClip>(),d=new List<AudioClip>();foreach(AudioSource sound in Resources.FindObjectsOfTypeAll<AudioSource>()){
                if(sound.clip==null)continue;string name=sound.name.ToLowerInvariant();if(name.Contains("dogpat"))d.Add(sound.clip);if(name.Contains("cat_meow") || name.Contains("catmeow"))c.Add(sound.clip);
            }
            if(c.Count==0 || d.Count==0)foreach(AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>()){string name=clip.name.ToLowerInvariant();if(d.Count==0 && name.Contains("dog")&&(name.Contains("pat")||name.Contains("pet")))d.Add(clip);if(c.Count==0 && name.Contains("cat")&&name.Contains("meow"))c.Add(clip);}
            cats=c.ToArray();dogs=d.ToArray();}
            AudioClip[] choices=actor.Dog?dogs:cats;
            if(choices.Length>0){if(actor.Sound==null){GameObject sound=new GameObject("VR Pet Reaction",typeof(AudioSource));sound.transform.SetParent(actor.Root,false);actor.Sound=sound.GetComponent<AudioSource>();actor.Sound.playOnAwake=false;actor.Sound.spatialBlend=1;actor.Sound.minDistance=.4f;actor.Sound.maxDistance=6;actor.Sound.dopplerLevel=0;}actor.Sound.PlayOneShot(choices[Random.Range(0,choices.Length)],.65f);}
            foreach(PlayMakerFSM blink in actor.Root.GetComponentsInChildren<PlayMakerFSM>(true))if(blink.FsmName=="BLINK" && blink.isActiveAndEnabled)foreach(FsmTransition t in blink.Fsm.GlobalTransitions)if(t.EventName=="G2"){blink.SendEvent("G2");return;}
        }
        internal bool PetContact(Fsm fsm,out bool touching){
            touching=false;if(!Enabled || fsm==null)return false;
            foreach(Actor actor in actors)if(actor.Pet && actor.Brain!=null && actor.Brain.Fsm==fsm){touching=Time.unscaledTime<actor.PetUntil;return true;}
            return false;
        }
        internal void Clear(){foreach(Actor actor in actors){foreach(Skin skin in actor.Skins)skin.Clear();if(actor.Sound!=null)Object.Destroy(actor.Sound.gameObject);}actors.Clear();knownActors.Clear();knownSkins.Clear();fsmCursor=rendererCursor=0;if(left.Physics!=null)left.Physics.Clear();if(right.Physics!=null)right.Physics.Clear();left.Physics=right.Physics=null;HandPhysicsBody.Surfaces.Clear();cats=dogs=null;if(layerSaved)Physics.IgnoreLayerCollision(31,31,originalLayerIgnore);layerSaved=false;nextScan=0;left.Valid=right.Valid=false;left.Actor=right.Actor=null;left.Purr.Reset();right.Purr.Reset();}
        internal string Describe(){return "PhysicalTouch dynamicHands=True chinPurr=True actors="+actors.Count+" left="+(left.Actor!=null)+" right="+(right.Actor!=null);}
    }
}
