using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class PhysicalBasketball {
        readonly List<PlayMakerFSM> anchors=new List<PlayMakerFSM>(),balls=new List<PlayMakerFSM>();
        readonly Dictionary<FsmGameObject,GameObject> original=new Dictionary<FsmGameObject,GameObject>();
        PlayMakerFSM holding,shooting;GameObject handAnchor;Rigidbody shotBody;
        Vector3 previous,velocity,shotVelocity,shotSpin;Quaternion previousRotation;Vector3 spin;
        bool sampled,armed;float nextScan,waitingUntil,shotUntil;
        readonly Vector3[] recent=new Vector3[16];readonly float[] times=new float[16];int sampleIndex;
        internal bool Enabled {get{return VrPlugin.Active && VrPlugin.Instance.MotionInput.Value && VrPlugin.Instance.PhysicalInteractions.Value;}}
        internal bool Held {get{return Enabled && holding!=null && holding.ActiveStateName=="State 3";}}
        internal bool ReservesRight {get{return Held || Time.unscaledTime<waitingUntil;}}
        internal bool Owns(Fsm fsm){return Enabled && fsm!=null && fsm.Name=="FSM" && fsm.GameObject!=null && fsm.GameObject.name=="BASKETBALL_ANCHOR";}
        internal bool Blocks(string action){return ReservesRight && (action=="Trigger" || action=="AltTrigger" || action=="TWTrigger");}
        internal bool ReplaceForce(Fsm fsm) {
            if(!Enabled || shooting==null || shooting.Fsm!=fsm || shotBody==null || Time.unscaledTime>shotUntil)return false;
            shotBody.velocity=shotVelocity;shotBody.angularVelocity=shotSpin;return true;
        }
        void Set(FsmGameObject variable,GameObject target){if(variable==null)return;if(!original.ContainsKey(variable))original.Add(variable,variable.Value);variable.Value=target;}
        void Scan(){
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+2;anchors.Clear();balls.Clear();
            foreach(PlayMakerFSM f in SceneInventory.Fsms){
                if(f==null || !f.gameObject.scene.IsValid() || !f.isActiveAndEnabled)continue;
                if(f.name=="BASKETBALL_ANCHOR" && f.FsmName=="FSM")anchors.Add(f);
                if(f.name.StartsWith("PROP_BASKETBALL") && f.FsmName=="ACTIVATE")balls.Add(f);
            }
        }
        internal void Update(float scale,bool freeLook){
            if(!Enabled){Clear();return;}VrPlugin p=VrPlugin.Instance;
            if(handAnchor==null){handAnchor=new GameObject("VR Basketball Palm");handAnchor.layer=31;handAnchor.transform.SetParent(p.Right.Visual.transform,false);handAnchor.transform.localPosition=new Vector3(-.025f,0,.145f);}
            Vector3 position=handAnchor.transform.position;float dt=Time.unscaledDeltaTime;
            if(sampled && dt>0 && dt<.1f && (position-previous).magnitude<.5f*scale){
                Vector3 current=Vector3.ClampMagnitude((position-previous)/dt,10*scale);
                velocity=Vector3.Lerp(velocity,current,1-Mathf.Exp(-35*dt));
                Quaternion delta=handAnchor.transform.rotation*Quaternion.Inverse(previousRotation);float degrees;Vector3 axis;delta.ToAngleAxis(out degrees,out axis);if(degrees>180)degrees-=360;
                spin=axis.sqrMagnitude>.001f?Vector3.ClampMagnitude(axis*degrees*Mathf.Deg2Rad/dt,25):Vector3.zero;
            }else velocity=spin=Vector3.zero;
            previous=position;previousRotation=handAnchor.transform.rotation;sampled=true;Scan();
            if(p.UI.MenuOpen || !freeLook || !p.Right.Valid){Release(false,scale);return;}
            foreach(PlayMakerFSM f in anchors)if(f!=null){Set(f.FsmVariables.FindFsmGameObject("HAND"),handAnchor);Set(f.FsmVariables.FindFsmGameObject("DIRECTION"),handAnchor);}
            holding=null;
            foreach(PlayMakerFSM f in anchors)if(f!=null && f.ActiveStateName=="State 3"){
                FsmGameObject ball=f.FsmVariables.FindFsmGameObject("BALL");if(ball!=null && ball.Value!=null && ball.Value.activeInHierarchy){holding=f;break;}
            }
            if(Held){
                recent[sampleIndex]=velocity;times[sampleIndex]=Time.unscaledTime;sampleIndex=(sampleIndex+1)%recent.Length;
                GameObject ball=holding.FsmVariables.FindFsmGameObject("BALL").Value;Rigidbody body=ball.GetComponent<Rigidbody>();
                if(body!=null){body.isKinematic=true;body.velocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
                ball.transform.position=position;ball.transform.rotation=handAnchor.transform.rotation;
                if(p.Right.Grip)armed=true;
                if(armed && !p.Right.Grip && p.Right.PreviousGrip)Release(true,scale);
                return;
            }
            armed=false;
            if(!p.Right.Grip || p.Right.PreviousGrip || p.HandsUse.RightHolding || p.CameraControls.ReservesRight || p.SprayControls.HoldingCan || p.ClimbControls.Holding)return;
            foreach(PlayMakerFSM ball in balls){
                if(ball==null || !ball.isActiveAndEnabled)continue;Rigidbody body=ball.GetComponent<Rigidbody>();if(body==null)continue;
                Renderer r=null;foreach(Renderer candidate in ball.GetComponentsInChildren<Renderer>())if(!(candidate is ParticleSystemRenderer) && candidate.gameObject.layer!=5 && !Diagnostics.PathOf(candidate.transform).Contains("/UI_PRESS")){r=candidate;break;}if(r==null || !r.enabled || Vector3.Distance(r.bounds.ClosestPoint(p.Right.Visual.transform.TransformPoint(p.Right.GripLocalPoint)),p.Right.Visual.transform.TransformPoint(p.Right.GripLocalPoint))>.10f*scale)continue;
                Vector3 ray=r.bounds.center-p.HeadPosition;RaycastHit wall;
                if(Physics.Raycast(p.HeadPosition,ray.normalized,out wall,ray.magnitude,~(1<<31|1<<5|1<<2),QueryTriggerInteraction.Ignore) && wall.rigidbody!=body)continue;
                bool accepts=false;foreach(FsmTransition t in ball.Fsm.GlobalTransitions)if(t.EventName=="G1")accepts=true;
                if(!accepts)continue;ball.SendEvent("G1");waitingUntil=Time.unscaledTime+.75f;p.PulseRight();break;
            }
        }
        void Release(bool shoot,float scale){
            if(holding==null)return;FsmGameObject ball=holding.FsmVariables.FindFsmGameObject("BALL");
            shotBody=ball==null || ball.Value==null?null:ball.Value.GetComponent<Rigidbody>();
            shooting=holding;shotUntil=Time.unscaledTime+.2f;
            Vector3 peak=velocity;for(int i=0;i<recent.Length;i++)if(Time.unscaledTime-times[i]<=.085f && recent[i].y>peak.y)peak=recent[i];
            shotVelocity=shoot?ThrowMath.Release(velocity,peak,scale):Vector3.zero;shotSpin=shoot?spin:Vector3.zero;
            holding.SendEvent(shoot?"THROW":"DROP");
            if(shotBody!=null){shotBody.isKinematic=false;shotBody.velocity=shotVelocity;shotBody.angularVelocity=shotSpin;}
            holding=null;armed=false;waitingUntil=0;VrPlugin.Instance.PulseRight();
        }
        internal void PoseHeld(){
            if(!Held || handAnchor==null)return;FsmGameObject ball=holding.FsmVariables.FindFsmGameObject("BALL");
            if(ball!=null && ball.Value!=null){ball.Value.transform.position=handAnchor.transform.position;ball.Value.transform.rotation=handAnchor.transform.rotation;}
        }
        internal void Clear(){
            Release(false,1);
            foreach(KeyValuePair<FsmGameObject,GameObject> pair in original)pair.Key.Value=pair.Value;original.Clear();
            if(handAnchor!=null)Object.Destroy(handAnchor);handAnchor=null;anchors.Clear();balls.Clear();shooting=null;shotBody=null;sampled=armed=false;nextScan=waitingUntil=shotUntil=0;
        }
        internal string Describe(){return "PhysicalBasketball held="+Held+" anchors="+anchors.Count+" nativeBalls="+balls.Count+" lastReleaseVelocity="+shotVelocity.ToString();}
    }
}
