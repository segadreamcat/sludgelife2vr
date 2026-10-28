using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace SludgeLife2VR {
    internal sealed class PhysicalClimb {
        sealed class Ladder { internal PlayMakerFSM Fsm; internal Collider[] Volumes; }
        sealed class Grip {
            internal Collider Surface; internal Vector3 Local, HandLocal, PreviousTracking, PreviousAnchor;
            internal bool Ledge; internal float Rise, NextMantle;
            internal bool Held { get { return Surface != null; } }
            internal Vector3 Anchor { get { return Surface.transform.TransformPoint(Local); } }
            internal void Release() { Surface = null; Rise = 0; NextMantle=0; }
        }
        readonly List<Ladder> ladders = new List<Ladder>();
        readonly Grip left = new Grip(), right = new Grip();
        PlayMakerFSM move;
        CharacterController body;
        BoxCollider roomBoard;
        readonly List<Collider> roomSolids=new List<Collider>();
        readonly List<RoomHoldSurface> roomMeshes=new List<RoomHoldSurface>();
        readonly List<GraffitiClimbWall> graffitiWalls=new List<GraffitiClimbWall>();
        readonly RaycastHit[] mantleHits=new RaycastHit[24];
        bool roomScanned,supportedMantle;
        string mantleCheck="idle",lastGrab="none";
        float nextScan;
        bool mantle, mantleForward, suspended;
        float sessionRise, lastHeld, mantleSince, stalled;
        Vector3 mantleLift, mantleFinish;
        readonly int mask = ~(1 << 31 | 1 << 5 | 1 << 2);
        internal bool Holding { get { return left.Held || right.Held || mantle; } }
        internal bool LeftHeld { get { return left.Held; } }
        internal bool RightHeld { get { return right.Held; } }
        internal bool Enabled { get { return VrPlugin.Active && VrPlugin.Instance.MotionInput.Value && VrPlugin.Instance.PhysicalClimbing.Value; } }

        internal Vector3 Update(CharacterController controller, Quaternion basis, Vector3 sourcePosition, Vector3 trackingOrigin, float scale, bool freeLook) {
            VrPlugin p = VrPlugin.Instance;
            if (!Enabled) { Clear(); return Vector3.zero; }
            if (controller == null || !controller.enabled || p.UI.MenuOpen || !freeLook) { Release(); return Vector3.zero; }
            if (body != controller) {
                Release(); body = controller; move = null;
                foreach (PlayMakerFSM f in body.GetComponents<PlayMakerFSM>()) if (f.FsmName == "MOVE") { move = f; break; }
            }
            if (move == null || !Allowed(move.ActiveStateName)) { Release(); return Vector3.zero; }
            ScanLadders();
            if (p.Right.A && !p.Right.PreviousA) { Release(); return Vector3.zero; }
            if (mantle) return MantleStep(scale);
            TrackGrip(left,p.Left,basis,sourcePosition,trackingOrigin,scale,!p.HandsUse.LeftHolding);
            // The spray can stays attached while grip independently catches a ladder.
            TrackGrip(right,p.Right,basis,sourcePosition,trackingOrigin,scale,!p.HandsUse.RightHolding && !p.CameraControls.ReservesRight && !p.Basketball.ReservesRight && !p.Gesture.Active);
            if (!left.Held && !right.Held) { SetNativeClimbing(false); if(Time.unscaledTime-lastHeld>.8f)sessionRise=0; return Vector3.zero; }
            Vector3 l = Delta(left,p.Left,basis,scale), r = Delta(right,p.Right,basis,scale);
            Vector3 delta = ClimbMath.Limit(ClimbMath.Average(l,left.Held,r,right.Held),Time.unscaledDeltaTime,scale);
            Vector3 start = body.transform.position;
            body.Move(delta); Vector3 actual = body.transform.position-start;
            if (left.Held) left.Rise += actual.y; if (right.Held) right.Rise += actual.y;
            sessionRise+=actual.y;lastHeld=Time.unscaledTime;
            SetNativeClimbing(true);
            TryMantle(left,scale); if (!mantle) TryMantle(right,scale);
            return actual;
        }
        static bool Allowed(string state) { return state == "MOVE" || state == "JUMP" || state.StartsWith("FALL") || state.StartsWith("Climbing?"); }
        void ScanLadders() {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 3;
            bool registered = false;
            foreach (PlayMakerFSM fsm in SceneInventory.Fsms) {
                if (fsm==null || !fsm.isActiveAndEnabled || !fsm.gameObject.scene.IsValid() || fsm.FsmVariables.FindFsmFloat("CLIMB_ANGLE") == null) continue;
                if (fsm.Fsm.GetState("CLIMBING") == null) continue;
                Collider[] volumes = fsm.GetComponents<Collider>(); if (volumes.Length == 0) continue;
                bool found = false; foreach (Ladder existing in ladders) if (existing.Fsm == fsm) found = true;
                if (found) continue;
                ladders.Add(new Ladder { Fsm = fsm, Volumes = volumes });
                registered = true;
                // Keep the authored colliders; replace their automatic stick-driven climb FSM.
                fsm.enabled = false;
            }
            if(!roomScanned) {
                roomScanned=true;
                foreach(MeshFilter mesh in SceneInventory.Meshes) {
                    if(mesh==null || !mesh.gameObject.scene.IsValid())continue;
                    if(mesh.name!="PROP_PLANK_L_STRIPED" && mesh.name!="PROP_SUITCASE_BIGBLUE"
                        && mesh.name!="PROP_SUITCASE_DUFFLERED" && mesh.name!="handle")continue;
                    string path=Diagnostics.PathOf(mesh.transform);
                    if(RoomClimbMath.IsTarget(path)) {
                        BoxCollider candidate=mesh.GetComponent<BoxCollider>();
                        if(candidate!=null && !candidate.isTrigger && candidate.attachedRigidbody==null)roomBoard=candidate;
                    }
                    if(RoomClimbMath.IsSuitcaseTarget(path))foreach(Collider c in mesh.GetComponents<Collider>())RegisterRoomCollider(c);
                }
                if(roomBoard!=null) {
                    Transform tower=roomBoard.transform;while(tower.parent!=null)tower=tower.parent;
                    Transform frame=tower.Find("ELEVATOR/ELEVATOR_CSG");
                    if(frame!=null)foreach(MeshCollider c in frame.GetComponentsInChildren<MeshCollider>(true))RegisterRoomCollider(c);
                    Transform interior=tower.Find("ELEVATOR/_STATIC");
                    if(interior!=null)foreach(Collider c in interior.GetComponentsInChildren<Collider>(true))
                        if(RoomClimbMath.IsInteriorProp(Diagnostics.PathOf(c.transform)))RegisterRoomCollider(c);
                }
                GameObject towerObject=GameObject.Find("TOWER");
                if(towerObject!=null)foreach(string relative in new string[]{"06/06_RCSG","07/07_RCSG"}) {
                    Transform wallRoot=towerObject.transform.Find(relative);
                    if(wallRoot==null || !GraffitiWallMath.IsRoot(Diagnostics.PathOf(wallRoot)))continue;
                    foreach(MeshCollider c in wallRoot.GetComponentsInChildren<MeshCollider>(true)) {
                        if(c.isTrigger || c.attachedRigidbody!=null)continue;
                        GraffitiClimbWall wall=GraffitiClimbWall.Create(c);if(wall!=null)graffitiWalls.Add(wall);
                    }
                }
                BepInEx.Logging.Logger.CreateLogSource("VR Climb").LogInfo("Room entrance holds: board="+(roomBoard!=null)+" solids="+roomSolids.Count+" real mesh surfaces="+roomMeshes.Count+" graffiti wall sections="+graffitiWalls.Count);
            }
            FsmBool wasClimbing = move.FsmVariables.FindFsmBool("CLIMBING");
            if (registered && wasClimbing != null && wasClimbing.Value) {
                FsmVector3 platform = move.FsmVariables.FindFsmVector3("PLATFORM"); if (platform != null) platform.Value = Vector3.zero;
                FsmFloat multiplier = move.FsmVariables.FindFsmFloat("MOVE_MULT"); if (multiplier != null) multiplier.Value = 1;
            }
        }
        void RegisterRoomCollider(Collider c) {
            if(c==null || c.isTrigger || c.attachedRigidbody!=null || roomSolids.Contains(c))return;
            foreach(RoomHoldSurface existing in roomMeshes)if(existing.Collider==c)return;
            MeshCollider mc=c as MeshCollider;
            if(mc!=null && !mc.convex) {
                if(roomBoard==null)return;
                RoomHoldSurface surface=RoomHoldSurface.Create(mc,roomBoard.transform);if(surface!=null)roomMeshes.Add(surface);
            } else if(c is BoxCollider || c is SphereCollider || c is CapsuleCollider || mc!=null)roomSolids.Add(c);
        }
        bool RoomContact(Vector3 world,float range,out Collider surface,out Vector3 point) {
            surface=null;point=world;
            if(roomBoard==null || !RoomClimbMath.InReachArea(roomBoard.transform.InverseTransformPoint(world)))return false;
            float nearest=range;Vector3 hit;
            if(roomBoard.enabled && roomBoard.gameObject.activeInHierarchy
                && RoomClimbMath.Contact(roomBoard.transform.localToWorldMatrix,roomBoard.transform.worldToLocalMatrix,world,nearest,out hit)) {
                surface=roomBoard;point=hit;nearest=Vector3.Distance(world,hit);
            }
            foreach(Collider c in roomSolids) {
                if(c==null || !c.enabled || !c.gameObject.activeInHierarchy || (c.bounds.ClosestPoint(world)-world).sqrMagnitude>nearest*nearest)continue;
                hit=c.ClosestPoint(world);float distance=Vector3.Distance(hit,world);
                if(distance<=nearest){nearest=distance;surface=c;point=hit;}
            }
            foreach(RoomHoldSurface mesh in roomMeshes)if(mesh.Contact(roomBoard.transform,world,nearest,out hit)) {
                nearest=Vector3.Distance(world,hit);surface=mesh.Collider;point=hit;
            }
            return surface!=null;
        }
        void TrackGrip(Grip grip, Hand hand, Quaternion basis, Vector3 sourcePosition, Vector3 origin, float scale, bool available) {
            if (!hand.Valid || !hand.Grip || !available) { grip.Release(); return; }
            Vector3 tracking=hand.TrackingPoint(grip.Held?grip.HandLocal:hand.GripLocalPoint);
            Vector3 world=sourcePosition+basis*(tracking-origin)*scale;
            if (grip.Held) {
                if (!grip.Surface.enabled || !grip.Surface.gameObject.activeInHierarchy || (world-grip.Anchor).sqrMagnitude > .8f*.8f*scale*scale
                    || (tracking-grip.PreviousTracking).sqrMagnitude > .35f*.35f) grip.Release();
                return;
            }
            // At this entrance, a squeezed grip may catch a real surface as the hand reaches it.
            bool roomReach=roomBoard!=null && RoomClimbMath.InReachArea(roomBoard.transform.InverseTransformPoint(world));
            bool wallReach=graffitiWalls.Count>0 && GraffitiWallMath.Near(world,.20f*scale);
            if(hand.PreviousGrip && !roomReach && !wallReach)return;
            Collider surface = null; Vector3 point = world, handLocal=hand.GripLocalPoint; bool ledge = false; float nearest = .14f*scale;
            foreach (Ladder ladder in ladders) foreach (Collider volume in ladder.Volumes) {
                if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(volume.ClosestPoint(world),world) > .15f*scale) continue;
                Vector3 closest;
                // Native climb volumes include deliberate invisible continuations above some ladders.
                if (!RungGeometry.Contact(volume.transform,world,.055f*scale,out closest))closest=volume.ClosestPoint(world);
                float distance = Vector3.Distance(closest,world);
                if (distance <= nearest) { nearest = distance; surface = volume; point = closest; }
            }
            if(roomBoard!=null && roomBoard.enabled && roomBoard.gameObject.activeInHierarchy) {
                Vector3 contact;Transform t=roomBoard.transform;
                if(RoomClimbMath.Contact(t.localToWorldMatrix,t.worldToLocalMatrix,world,nearest,out contact)) {
                    nearest=Vector3.Distance(contact,world);surface=roomBoard;point=contact;ledge=true;
                }
            }
            if(roomReach || wallReach)for(int sample=0;sample<3;sample++) {
                Vector3 sampleLocal=sample==0?hand.GripLocalPoint:hand.Visual.transform.InverseTransformPoint(hand.FingerTip(sample));
                Vector3 sampleWorld=sourcePosition+basis*(hand.TrackingPoint(sampleLocal)-origin)*scale;
                Collider candidate;Vector3 contact;float reach=(sample==0?.18f:.10f)*scale;
                if(RoomContact(sampleWorld,reach,out candidate,out contact)) {
                    float distance=Vector3.Distance(contact,sampleWorld);
                    if(surface==null || distance<=nearest){nearest=distance;surface=candidate;point=contact;handLocal=sampleLocal;ledge=true;}
                }
                foreach(GraffitiClimbWall wall in graffitiWalls)if(wall.Contact(sampleWorld,reach,out contact)) {
                    float distance=Vector3.Distance(contact,sampleWorld);
                    if(surface==null || distance<=nearest){nearest=distance;surface=wall.Collider;point=contact;handLocal=sampleLocal;ledge=true;}
                }
            }
            if (surface == null && world.y > VrPlugin.Instance.HeadPosition.y-.55f*scale) {
                Vector3 into = world-VrPlugin.Instance.HeadPosition; into.y = 0;
                if (into.sqrMagnitude > .0025f*scale*scale) {
                    into.Normalize();
                    for(int probe=0;probe<3;probe++) {
                        RaycastHit top;
                        if(!Physics.Raycast(world+Vector3.up*ClimbMath.LedgeProbeHeight*scale+into*ClimbMath.LedgeInset(probe)*scale,
                            Vector3.down,out top,ClimbMath.LedgeProbeDepth*scale,mask,QueryTriggerInteraction.Ignore)
                            || top.normal.y<=.65f || !Structural(top.collider,scale) || top.collider.transform.IsChildOf(body.transform)
                            || Vector3.Distance(top.point,world)>ClimbMath.LedgeRange*scale)continue;
                        RaycastHit near;
                        // Reach under a real lip, but do not catch broad floors away from their edge.
                        if(Physics.Raycast(top.point-into*.30f*scale+Vector3.up*.04f*scale,Vector3.down,out near,.10f*scale,mask,QueryTriggerInteraction.Ignore))continue;
                        surface=top.collider;point=top.point;ledge=true;break;
                    }
                }
            }
            if (surface == null) return;
            grip.Surface = surface; grip.Local = surface.transform.InverseTransformPoint(point);grip.HandLocal=handLocal; grip.Ledge = ledge;
            grip.PreviousTracking = hand.TrackingPoint(grip.HandLocal); grip.PreviousAnchor = grip.Anchor; grip.Rise = 0;
            lastGrab=Diagnostics.PathOf(surface.transform);
            VrPlugin.Instance.PulseGrip(hand);
            BepInEx.Logging.Logger.CreateLogSource("VR Climb").LogInfo("Grabbed " + (ledge ? "ledge " : "ladder/pipe ") + Diagnostics.PathOf(surface.transform));
        }
        static bool Structural(Collider surface,float scale) {
            if(surface.attachedRigidbody!=null)return false;
            string path=Diagnostics.PathOf(surface.transform).ToUpperInvariant();
            if(path.Contains("MPC") || path.Contains("BTN") || path.Contains("BUTTON") || path.Contains("PROP_") || path.StartsWith("NPCS/") || path.Contains("FAUNA_"))return false;
            Bounds b=surface.bounds;
            return Mathf.Max(b.size.x,b.size.z)>.45f*scale && b.size.y>.12f*scale;
        }
        static Vector3 Delta(Grip grip, Hand hand, Quaternion basis, float scale) {
            if (!grip.Held) return Vector3.zero;
            Vector3 anchor = grip.Anchor;
            Vector3 result = basis*ClimbMath.Pull(grip.PreviousTracking,hand.TrackingPoint(grip.HandLocal),scale)+(anchor-grip.PreviousAnchor);
            grip.PreviousTracking = hand.TrackingPoint(grip.HandLocal); grip.PreviousAnchor = anchor;
            return result;
        }
        void TryMantle(Grip grip, float scale) {
            if(!grip.Held || !ClimbMath.EarnedMantle(Mathf.Max(grip.Rise,sessionRise),VrPlugin.Instance.HeadPosition.y-grip.Anchor.y,scale))return;
            bool room=roomBoard!=null && RoomClimbMath.InOpening(roomBoard.transform.InverseTransformPoint(grip.Anchor));
            bool wall=graffitiWalls.Count>0 && GraffitiWallMath.Contains(grip.Anchor);
            bool supported=room || wall;
            if(supported && Time.unscaledTime<grip.NextMantle)return;
            if(supported)grip.NextMantle=Time.unscaledTime+.12f;
            Vector3 into=room?roomBoard.transform.TransformDirection(Vector3.up):wall?Vector3.left:grip.Anchor-body.bounds.center;
            into.y=0;if(into.sqrMagnitude<.0001f)return;into.Normalize();
            float top;bool upperRung=!grip.Ledge && RungGeometry.Top(grip.Surface.transform,out top) && top-grip.Anchor.y<.38f*scale;
            Vector3 sideways=Vector3.Cross(Vector3.up,into);
            for(int candidate=0;candidate<(room?RoomClimbMath.LandingCount:1);candidate++) {
                Vector2 offset=room?RoomClimbMath.LandingOffset(candidate):new Vector2(.20f,0);
                Vector3 over=grip.Anchor+into*(body.radius+offset.x*scale)+sideways*offset.y*scale;
                if(TryMantleAt(grip,scale,over,upperRung,room,supported)) {
                    mantleCheck="started candidate "+candidate;
                    BepInEx.Logging.Logger.CreateLogSource("VR Climb").LogInfo("Mantle from "+Diagnostics.PathOf(grip.Surface.transform)+" to "+mantleFinish);
                    if(!supported){left.Release();right.Release();}
                    return;
                }
            }
        }
        bool TryMantleAt(Grip grip,float scale,Vector3 over,bool upperRung,bool room,bool supported) {
            RaycastHit landing;float reach=room?.70f:upperRung?1.15f:.25f;
            float below=room?1.15f:upperRung?1f:.20f;
            if(!Physics.Raycast(over+Vector3.up*reach*scale,Vector3.down,out landing,(reach+below)*scale,mask,QueryTriggerInteraction.Ignore)
                || landing.normal.y<.7f){mantleCheck="no floor";return false;}
            float rise=landing.point.y-grip.Anchor.y;
            if(room?!RoomClimbMath.AllowsLandingHeight(rise,scale):rise>(upperRung?1.10f:.22f)*scale || rise<-(upperRung?1f:.22f)*scale){mantleCheck="floor height";return false;}
            if(VrPlugin.Instance.HeadPosition.y<landing.point.y-.20f*scale){mantleCheck="head below floor";return false;}
            if(supported && !RoomClimbMath.AssistHeight(landing.point.y-body.bounds.min.y,scale)){mantleCheck="keep climbing to lip";return false;}
            Vector3 displacement=landing.point-body.bounds.center;displacement.y=landing.point.y-body.bounds.min.y+.06f*scale;
            Vector3 low=body.bounds.center+displacement-Vector3.up*(body.bounds.extents.y-body.radius);
            Vector3 high=body.bounds.center+displacement+Vector3.up*(body.bounds.extents.y-body.radius);
            foreach(Collider obstacle in Physics.OverlapCapsule(low,high,body.radius*.95f,mask,QueryTriggerInteraction.Ignore))
                if(obstacle!=body && !obstacle.transform.IsChildOf(body.transform)){mantleCheck="landing blocked";return false;}
            if(displacement.y<-.1f*scale || displacement.y>Mathf.Min(2.2f*scale,body.bounds.size.y+.35f*scale)){mantleCheck="body height";return false;}
            if(supported && (!MantlePathClear(Vector3.zero,Vector3.up*displacement.y)
                || !MantlePathClear(Vector3.up*displacement.y,new Vector3(displacement.x,0,displacement.z))))return false;
            mantleFinish=body.transform.position+displacement;
            mantleLift=body.transform.position+Vector3.up*displacement.y;
            mantle=true;supportedMantle=supported;mantleForward=false;mantleSince=Time.unscaledTime;stalled=0;
            return true;
        }
        bool MantlePathClear(Vector3 offset,Vector3 delta) {
            float length=delta.magnitude;if(length<.001f)return true;
            Vector3 low=body.bounds.center+offset-Vector3.up*(body.bounds.extents.y-body.radius);
            Vector3 high=body.bounds.center+offset+Vector3.up*(body.bounds.extents.y-body.radius);
            int count=Physics.CapsuleCastNonAlloc(low,high,body.radius,delta/length,mantleHits,length,mask,QueryTriggerInteraction.Ignore);
            RaycastHit[] hits=mantleHits;
            if(count==mantleHits.Length){hits=Physics.CapsuleCastAll(low,high,body.radius,delta/length,length,mask,QueryTriggerInteraction.Ignore);count=hits.Length;}
            for(int i=0;i<count;i++) {
                Collider obstacle=hits[i].collider;
                if(obstacle==null || obstacle==body || obstacle.transform.IsChildOf(body.transform))continue;
                mantleCheck="path blocked "+Diagnostics.PathOf(obstacle.transform);return false;
            }
            return true;
        }
        void KeepMantleGrip(Grip grip,Hand hand) {
            if(!grip.Held)return;
            if(!hand.Valid || !hand.Grip || !grip.Surface.enabled || !grip.Surface.gameObject.activeInHierarchy){grip.Release();return;}
            grip.PreviousTracking=hand.TrackingPoint(grip.HandLocal);grip.PreviousAnchor=grip.Anchor;
        }
        Vector3 MantleStep(float scale) {
            if(supportedMantle) {
                KeepMantleGrip(left,VrPlugin.Instance.Left);KeepMantleGrip(right,VrPlugin.Instance.Right);
                if(!left.Held && !right.Held){mantle=false;supportedMantle=false;mantleCheck="released";SetNativeClimbing(false);return Vector3.zero;}
            }
            Vector3 start=body.transform.position,target=mantleForward?mantleFinish:mantleLift;
            body.Move(Vector3.ClampMagnitude(target-start,1.7f*scale*Mathf.Min(Time.unscaledDeltaTime,.05f)));
            Vector3 actual=body.transform.position-start;SetNativeClimbing(true);
            float remaining=Vector3.Distance(body.transform.position,target);
            stalled=Vector3.Distance(start,target)-remaining<.0001f?stalled+Time.unscaledDeltaTime:0;
            if(remaining<.025f*scale) {
                if(mantleForward){mantle=false;supportedMantle=false;left.Release();right.Release();mantleCheck="completed";SetNativeClimbing(false);}
                else mantleForward=true;
            } else if(stalled>.20f || Time.unscaledTime-mantleSince>3) {
                bool retain=supportedMantle;mantle=false;supportedMantle=false;
                mantleCheck="stalled "+(mantleForward?"forward":"lift")+" remaining="+remaining;
                if(retain){left.NextMantle=float.PositiveInfinity;right.NextMantle=float.PositiveInfinity;}
                SetNativeClimbing(left.Held || right.Held);
                BepInEx.Logging.Logger.CreateLogSource("VR Climb").LogInfo(mantleCheck+" retaining grips="+(left.Held || right.Held));
            }
            return actual;
        }
        void SetNativeClimbing(bool value) {
            if (move == null) return;
            FsmBool flag = move.FsmVariables.FindFsmBool("CLIMBING"); if (flag != null) flag.Value = value;
            if (value) {
                suspended=true;
                foreach (string name in new string[] {"Y","Y_VEL","TIME"}) { FsmFloat v = move.FsmVariables.FindFsmFloat(name); if (v != null) v.Value = 0; }
            } else if(suspended) {
                // MOVE's one-shot WaitSetFloat has already finished while we held Y at zero.
                // Re-enter native fall easing exactly once; do not replace jumps or scripted states.
                string state=move.ActiveStateName;
                if(ClimbMath.ResumeFall(ref suspended,state)) {
                    FsmBool hover=move.FsmVariables.FindFsmBool("HOVER");if(hover!=null)hover.Value=false;
                    foreach(string name in new string[]{"Y","Y_VEL"}){FsmFloat v=move.FsmVariables.FindFsmFloat(name);if(v!=null)v.Value=-2;}
                    if(move.Fsm.GetState("FALL no VAULT")!=null)move.Fsm.SetState("FALL no VAULT");
                }
            }
        }
        internal void LockHands(Hand l, Hand r, float scale) {
            if (left.Held && l.Visual != null) l.Visual.transform.position = left.Anchor-l.Visual.transform.rotation*left.HandLocal*scale;
            if (right.Held && r.Visual != null) r.Visual.transform.position = right.Anchor-r.Visual.transform.rotation*right.HandLocal*scale;
        }
        internal bool OwnsMove(Fsm fsm) { return Enabled && Holding && move != null && fsm == move.Fsm; }
        void Release() { left.Release(); right.Release(); mantle = false;supportedMantle=false; SetNativeClimbing(false); }
        internal void Clear() {
            Release(); foreach (Ladder ladder in ladders) if (ladder.Fsm != null) ladder.Fsm.enabled = true;
            ladders.Clear(); roomBoard=null;roomSolids.Clear();roomMeshes.Clear();graffitiWalls.Clear();roomScanned=false;mantleCheck="idle";lastGrab="none"; body = null; move = null; nextScan = 0;sessionRise=0;lastHeld=0;suspended=false;
        }
        internal string Describe() { return "PhysicalClimb graffitiWalls="+graffitiWalls.Count+" lastGrab="+lastGrab+" realRoomMeshes="+roomMeshes.Count+" roomSolids="+roomSolids.Count+" mantleCheck="+mantleCheck+" roomBoard="+(roomBoard!=null)+" nativeLadderZones=True ladders=" + ladders.Count + " left=" + left.Held + " right=" + right.Held + " mantle=" + mantle
            + " leftAnchor=" + (left.Held ? left.Anchor.ToString() : "none") + " rightAnchor=" + (right.Held ? right.Anchor.ToString() : "none")
            + " body="+(body==null?"none":body.bounds.ToString())+" radius="+(body==null?0:body.radius)+" move="+(move==null?"none":move.ActiveStateName)+" Y="+(move==null || move.FsmVariables.FindFsmFloat("Y")==null?"?":move.FsmVariables.FindFsmFloat("Y").Value.ToString())+" suspended="+suspended; }
    }
}
