using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace SludgeLife2VR {
    internal sealed class PhysicalHands {
        sealed class Target {
            internal PlayMakerFSM Interact, Activate;
            internal GameObject VisualSource;
            internal bool Soda, Slug, Disk, Button, Claimed, Consumed, OriginalInteract, ManagesSource;
            internal Renderer[] Renderers;
            internal Collider[] Colliders;
            internal readonly Dictionary<Renderer,bool> Hidden = new Dictionary<Renderer,bool>();
            internal readonly Dictionary<Collider,bool> Disabled = new Dictionary<Collider,bool>();
            internal HeldItemVisual Visual;
            internal float RestoreAt;
            internal bool Alive { get { return Interact != null && ((Interact.gameObject.activeInHierarchy && (ManagesSource || Interact.enabled)) || (Consumed && Visual != null && Visual.Root != null)); } }
            internal Vector3 Center {
                get {
                    if (Visual != null && Visual.Root != null) return Visual.Position;
                    foreach (Renderer r in Renderers) if (ItemRenderer(r)) return r.bounds.center;
                    return Interact.transform.position;
                }
            }
        }
        sealed class Slot {
            internal Target Item; internal float Since, Dwell, BlockUntil;
            internal Vector3 Previous, Velocity, PickupAtHead; internal bool Initialized, Raised;
            internal bool Holding { get { return Item != null; } }
        }
        sealed class ButtonContact { internal Target Target; internal WorldButtonSurface Surface;internal bool Left, Right; internal float Next; }
        readonly List<Target> targets = new List<Target>();
        readonly List<ButtonContact> buttons = new List<ButtonContact>();
        readonly Slot left = new Slot(), right = new Slot();
        Vector3 previousLeftTip, previousRightTip; bool tipsInitialized;
        readonly HashSet<PlayMakerFSM> discovered=new HashSet<PlayMakerFSM>();int scanCursor;
        float nextScan, nextConsume;string lastButton="none";
        internal bool LeftHolding { get { return left.Holding; } }
        internal bool RightHolding { get { return right.Holding; } }
        bool Enabled { get { return VrPlugin.Active && VrPlugin.Instance.MotionInput.Value && VrPlugin.Instance.PhysicalInteractions.Value; } }

        internal void BeforeClimb(CharacterController body, float scale, bool freeLook) {
            if (!Enabled) { Clear(); return; }
            VrPlugin p = VrPlugin.Instance;
            RestoreFinished();
            if (p.UI.MenuOpen || !freeLook) return;
            if (Time.unscaledTime >= nextScan) Scan();
            Grab(left,p.Left,true,body,scale,p.ClimbControls.LeftHeld);
            Grab(right,p.Right,false,body,scale,p.ClimbControls.RightHeld || p.SprayControls.HoldingCan || p.CameraControls.ReservesRight || p.Basketball.ReservesRight || p.Gesture.Active);
        }
        void Scan() {
            nextScan = Time.unscaledTime;

            PlayMakerFSM[] inventory=SceneInventory.Fsms;int end=System.Math.Min(inventory.Length,scanCursor+96);
            for(;scanCursor<end;scanCursor++) {PlayMakerFSM fsm=inventory[scanCursor];
                if (fsm==null || fsm.FsmName != "INTERACT" || !fsm.isActiveAndEnabled || !fsm.gameObject.scene.IsValid()) continue;
                if(discovered.Contains(fsm))continue;
                string path = Diagnostics.PathOf(fsm.transform), name = fsm.name.ToUpperInvariant();
                if (path.StartsWith("LVL_UI/") || path.StartsWith("NPCS/") || path.StartsWith("GRAFFITI/")) continue;
                bool soda = name.StartsWith("PROP_CAN_") && name.Contains("FULL");
                bool slug = name == "EAT" && path.Contains("FAUNA_SLUG");
                bool button = name.Contains("BTN") || name.Contains("BUTTON") || name.Contains("SWITCH");
                bool disk=fsm.transform.Find("PICKUP_DISK")!=null;
                if (!soda && !slug && !button && !disk) continue;
                PlayMakerFSM activation = null; foreach (PlayMakerFSM sibling in fsm.GetComponents<PlayMakerFSM>()) if (sibling.FsmName == "ACTIVATE") activation = sibling;
                if ((soda || slug || disk) && (activation == null || !Accepts(activation,"G1"))) continue;
                GameObject source=slug && fsm.transform.parent!=null?fsm.transform.parent.gameObject:disk?fsm.transform.Find("PICKUP_DISK").gameObject:fsm.gameObject;
                // SECRET_BTN is an invisible sibling of the moving red cap, not its parent.
                if(button && fsm.transform.parent!=null) {
                    Transform cap=fsm.transform.parent.Find("BTN");
                    if(cap!=null && cap.GetComponentInChildren<Renderer>()!=null)source=cap.gameObject;
                    else if(fsm.transform.parent.name.StartsWith("PROP_BELL"))source=fsm.transform.parent.gameObject;
                }
                Target target = new Target { VisualSource=source, Interact=fsm, Activate=activation, Soda=soda, Slug=slug, Disk=disk, Button=button,
                    Renderers=source.GetComponentsInChildren<Renderer>(true), Colliders=source.GetComponentsInChildren<Collider>(true) };
                discovered.Add(fsm);targets.Add(target); if (button) buttons.Add(new ButtonContact { Target=target,Surface=WorldButtonSurface.Create(fsm,activation,source) });
            }
            if(scanCursor>=inventory.Length){scanCursor=0;nextScan=Time.unscaledTime+5.13f;}
        }
        static bool Accepts(PlayMakerFSM fsm, string name) {
            if (fsm == null || !fsm.enabled || !fsm.gameObject.activeInHierarchy) return false;
            foreach (FsmTransition transition in fsm.Fsm.GlobalTransitions) if (transition.EventName == name) return true;
            FsmState state = fsm.Fsm.ActiveState; if (state == null) return false;
            foreach (FsmTransition transition in state.Transitions) if (transition.EventName == name) return true;
            return false;
        }
        void Grab(Slot slot, Hand hand, bool isLeft, CharacterController body, float scale, bool blocked) {
            Vector3 palm = hand.Visual == null ? Vector3.zero : hand.Visual.transform.TransformPoint(hand.GripLocalPoint);
            float dt = Mathf.Max(.001f,Time.unscaledDeltaTime);
            slot.Velocity = slot.Initialized ? Vector3.ClampMagnitude((palm-slot.Previous)/dt,6*scale) : Vector3.zero;
            slot.Previous = palm; slot.Initialized = true;
            if (slot.Holding) {
                if(slot.Item.Interact==null){if(slot.Item.Visual!=null)slot.Item.Visual.Clear();slot.Item=null;return;}
                if (!hand.Valid || !hand.Grip || blocked) {
                    Target item = slot.Item; item.Claimed = false;
                    if (item.Visual != null && item.Visual.Root != null) {
                        if(item.Disk && item.Consumed){item.Visual.Clear();item.Visual=null;Restore(item);}
                        else item.Visual.Drop(body,slot.Velocity,scale);
                    }
                    slot.Item = null; slot.BlockUntil = Time.unscaledTime+.2f;
                }
                return;
            }
            if (!hand.Valid || !hand.Grip || hand.PreviousGrip || blocked) return;
            Target selected = null; float best = .13f*scale;
            foreach (Target t in targets) {
                if (t.Button || t.Claimed || (!t.Alive && !(t.Consumed && t.Visual != null && t.Visual.Root != null))) continue;
                float distance = Distance(t,palm);
                if (distance < best && ClearReach(palm,t.Center,t.VisualSource.transform)) { selected = t; best = distance; }
            }
            if (selected == null) return;
            if (selected.Visual == null) {
                selected.Visual=HeldItemVisual.Create(selected.VisualSource,selected.Soda,selected.Disk?.10f:0);
                if (selected.Visual == null) return;
                selected.OriginalInteract = selected.Interact.enabled;
                selected.ManagesSource = true;
                selected.Interact.enabled = false;
                foreach (Renderer r in selected.Renderers) if (r != null) { selected.Hidden[r] = r.enabled; r.enabled = false; }
                foreach (Collider c in selected.Colliders) if (c != null) { selected.Disabled[c] = c.enabled; c.enabled = false; }
            }
            slot.PickupAtHead=Quaternion.Inverse(VrPlugin.Instance.HeadRotation)*(palm-VrPlugin.Instance.HeadPosition)/scale;
            slot.Raised=false;
            selected.Claimed = true; slot.Item = selected; slot.Since = Time.unscaledTime; slot.Dwell = 0;
            selected.Visual.Hold(hand,isLeft,scale); slot.BlockUntil = Time.unscaledTime+.2f;
            if(selected.Disk && !selected.Consumed){selected.Consumed=true;selected.Activate.SendEvent("G1");Log("Physical disk pickup returned to native inventory: "+selected.Interact.name);}
            VrPlugin.Instance.PulseGrip(hand);
            Log("Grabbed " + (selected.Soda ? "soda " : "slug ") + Diagnostics.PathOf(selected.Interact.transform));
        }
        static bool ItemRenderer(Renderer r) {
            if(r==null || !r.enabled || r is ParticleSystemRenderer || r.gameObject.layer==5)return false;
            string path=Diagnostics.PathOf(r.transform);return !path.Contains("/UI_PRESS") && !path.Contains("/PARTICLE");
        }
        static float Distance(Target target, Vector3 point) {
            if (target.Visual != null && target.Visual.Root != null) return Vector3.Distance(point,target.Visual.Position);
            float best = float.MaxValue;
            foreach (Renderer r in target.Renderers) if (ItemRenderer(r)) best = Mathf.Min(best,Vector3.Distance(point,r.bounds.ClosestPoint(point)));
            return best;
        }
        static bool ClearReach(Vector3 from, Vector3 to, Transform target) {
            Vector3 delta = to-from; float distance = delta.magnitude; if (distance < .01f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(from,delta/distance,distance,~(1<<31|1<<5|1<<2),QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(target) && hit.transform != target && hit.distance < distance-.015f) return false;
            return true;
        }
        internal void AfterPose(float scale, bool freeLook) {
            if (!Enabled || VrPlugin.Instance.UI.MenuOpen || !freeLook) { tipsInitialized = false; return; }
            VrPlugin p = VrPlugin.Instance;
            Vector3 mouth = HandInteractionMath.Mouth(p.HeadPosition,p.HeadRotation*Vector3.forward,p.HeadRotation*Vector3.up,scale);
            Consume(left,p.Left,mouth,scale); Consume(right,p.Right,mouth,scale);
            Vector3 l = p.Left.IndexTip, r = p.Right.IndexTip;
            foreach (ButtonContact button in buttons) {
                if (!button.Target.Alive) continue;
                if (tipsInitialized && !left.Holding && !p.ClimbControls.LeftHeld && !p.GliderGrips.LeftHeld && p.Left.Valid) Touch(button,p.Left,l,previousLeftTip,scale,ref button.Left);
                if (tipsInitialized && !right.Holding && !p.ClimbControls.RightHeld && !p.GliderGrips.RightHeld && p.Right.Valid && !p.Gesture.Active && !p.SprayControls.HoldingCan && !p.CameraControls.Held) Touch(button,p.Right,r,previousRightTip,scale,ref button.Right);
            }
            previousLeftTip = l; previousRightTip = r; tipsInitialized = true;
        }
        void Consume(Slot slot, Hand hand, Vector3 mouth, float scale) {
            if (!slot.Holding || slot.Item.Consumed || slot.Item.Visual == null || Time.unscaledTime < nextConsume) return;
            Target target = slot.Item;
            Vector3 palm=hand.Visual.transform.TransformPoint(hand.GripLocalPoint);
            Vector3 atHead=Quaternion.Inverse(VrPlugin.Instance.HeadRotation)*(palm-VrPlugin.Instance.HeadPosition)/scale;
            if ((atHead-slot.PickupAtHead).sqrMagnitude>=.08f*.08f) slot.Raised=true;
            if (!slot.Raised) return;
            float distance = target.Visual.MouthDistance(mouth)/scale;
            float tilt = Vector3.Dot(target.Visual.Root.transform.up,VrPlugin.Instance.HeadRotation*Vector3.up);
            if (!HandInteractionMath.Eating(Time.unscaledTime-slot.Since,distance,target.Soda,tilt,Time.unscaledDeltaTime,ref slot.Dwell)) return;
            if (!Accepts(target.Activate,"G1")) return;
            FsmBool busy = FsmVariables.GlobalVariables.FindFsmBool("BUSY"); if (busy != null && busy.Value) return;
            FsmBool vegan = FsmVariables.GlobalVariables.FindFsmBool("VEGAN");
            bool eaten = !target.Slug || vegan == null || !vegan.Value;
            target.Consumed = eaten; nextConsume = Time.unscaledTime+.8f;
            if (target.Soda) {
                if(target.Visual!=null){target.Visual.Clear();target.Visual=null;}
                target.Claimed=false;slot.Item=null;slot.BlockUntil=Time.unscaledTime+1;
                VrPlugin.Instance.ViewModels.PhysicalDrink(hand.Visual.transform);
            }
            target.Activate.SendEvent("G1"); VrPlugin.Instance.PulseGrip(hand);
            Log((target.Soda ? "Drink" : "Eat") + " gesture handed to native ACTIVATE/G1: " + Diagnostics.PathOf(target.Interact.transform));
            if (target.Slug) {
                target.Visual.Clear(); target.Visual = null; target.Claimed = false; target.RestoreAt = Time.unscaledTime+.5f;
                slot.Item = null; slot.BlockUntil = Time.unscaledTime+.2f;
            }
        }
        void Touch(ButtonContact button, Hand hand, Vector3 tip, Vector3 previous, float scale, ref bool latched) {
            Target target = button.Target;
            float distance=button.Surface.Distance(previous,tip,!latched)/scale;
            if (!HandInteractionMath.Press(distance,Vector3.Distance(tip,previous)/scale,ref latched) || Time.unscaledTime < button.Next) return;
            Transform assembly=button.Surface.Scope;
            if (!ClearReach(previous,tip,assembly)) {latched=false;lastButton="blocked:"+Diagnostics.PathOf(target.Interact.transform);return;}
            bool sent = false;
            if (!button.Surface.NativeInputOnly && Accepts(target.Activate,"G1")) { target.Activate.SendEvent("G1"); sent = true; }
            else {
                FsmState state = target.Interact.Fsm.ActiveState;
                if (state != null) foreach (FsmStateAction action in state.Actions) {
                    GetButtonDownIfBool2 input = action as GetButtonDownIfBool2;
                    if (input != null && input.buttonName.Value == "Trigger" && input.refBool.Value == input.matchBool.Value && input.sendEvent != null) {
                        target.Interact.Fsm.Event(input.sendEvent); sent = true; break;
                    }
                }
            }
            if(!sent){latched=false;lastButton="notReady:"+Diagnostics.PathOf(target.Interact.transform)+":"+target.Interact.ActiveStateName;}
            if (sent) { lastButton="pressed:"+Diagnostics.PathOf(target.Interact.transform);button.Next = Time.unscaledTime+.6f; VrPlugin.Instance.PulseGrip(hand); Log("Pressed native world button " + Diagnostics.PathOf(target.Interact.transform)); }
        }
        void RestoreFinished() {
            foreach (Target target in targets) if (target.RestoreAt > 0 && Time.unscaledTime >= target.RestoreAt) { Restore(target); target.RestoreAt = 0; }
        }
        static void Restore(Target target) {
            if (!target.ManagesSource) return;
            foreach (KeyValuePair<Renderer,bool> r in target.Hidden) if (r.Key != null) r.Key.enabled = r.Value;
            foreach (KeyValuePair<Collider,bool> c in target.Disabled) if (c.Key != null) c.Key.enabled = c.Value;
            if (!target.Consumed && target.Interact != null) target.Interact.enabled = target.OriginalInteract;
            target.Hidden.Clear(); target.Disabled.Clear();
            target.ManagesSource = false;
        }
        internal bool OwnsFoodTrigger(Fsm fsm) {
            if (!Enabled || fsm==null || fsm.Name!="INTERACT" || fsm.GameObject==null) return false;
            string name=fsm.GameObject.name.ToUpperInvariant();
            return name.StartsWith("PROP_CAN_") && name.Contains("FULL") || name=="EAT" && Diagnostics.PathOf(fsm.GameObject.transform).Contains("FAUNA_SLUG") || fsm.GameObject.transform.Find("PICKUP_DISK")!=null;
        }
        internal bool Blocks(string action) {
            bool rightBusy = right.Holding || Time.unscaledTime < right.BlockUntil;
            bool leftBusy = left.Holding || Time.unscaledTime < left.BlockUntil;
            return rightBusy && (action == "Trigger" || action == "TWTrigger" || action == "AltTrigger")
                || leftBusy && (action == "UseCamera" || action == "UseTelewarp" || action == "UseVandalEyes");
        }
        internal void Clear() {
            foreach (Target target in targets) { Restore(target); if (target.Visual != null) target.Visual.Clear(); }
            discovered.Clear();scanCursor=0;targets.Clear(); buttons.Clear(); left.Item = right.Item = null; left.Initialized = right.Initialized = false;
            left.BlockUntil = right.BlockUntil = 0; nextScan = nextConsume = 0; tipsInitialized = false;
        }
        static void Log(string text) { BepInEx.Logging.Logger.CreateLogSource("VR Hands Use").LogInfo(text); }
        internal string Describe() {
            int ready=0;float best=float.MaxValue;ButtonContact nearest=null;string missing="";
            VrPlugin p=VrPlugin.Instance;
            foreach(ButtonContact b in buttons){
                if(b.Surface.Count>0)ready++;else if(b.Target.Interact!=null)missing+=Diagnostics.PathOf(b.Target.Interact.transform)+",";
                if(!b.Target.Alive || p.Left.Visual==null || p.Right.Visual==null)continue;
                float d=Mathf.Min(b.Surface.Distance(p.Left.IndexTip,p.Left.IndexTip,false),b.Surface.Distance(p.Right.IndexTip,p.Right.IndexTip,false));
                if(d<best){best=d;nearest=b;}
            }
            return "PhysicalHands targets="+targets.Count+" buttons="+buttons.Count+" buttonSurfaces="+ready+" left="+(left.Holding && left.Item.Interact!=null?left.Item.Interact.name:"empty")+" right="+(right.Holding && right.Item.Interact!=null?right.Item.Interact.name:"empty")
                +" nearestButton="+(nearest==null?"none":Diagnostics.PathOf(nearest.Target.Interact.transform)+" distance="+best+" surface="+nearest.Surface.Describe())+" lastButton="+lastButton+" missing="+missing;
        }
    }
}
