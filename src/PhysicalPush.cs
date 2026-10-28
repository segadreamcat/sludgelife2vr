using System.Collections.Generic;
using UnityEngine;
namespace SludgeLife2VR {
    // Apply contact force to the original hinged body. Never synthesize player tags or unlock events.
    internal sealed class PhysicalPush {
        sealed class Palm {internal readonly Vector3[] Previous=new Vector3[6];internal bool Valid;internal float NextHaptic;internal readonly Vector3[] Points=new Vector3[6];internal readonly HashSet<Rigidbody> Pushed=new HashSet<Rigidbody>();internal readonly RaycastHit[] Hits=new RaycastHit[64];internal readonly Collider[] Overlaps=new Collider[64];}
        readonly Palm left=new Palm(),right=new Palm();
        readonly Dictionary<Rigidbody,bool> known=new Dictionary<Rigidbody,bool>();
        int pushes;float nextPrune;
        internal void Update(float scale,bool freeLook) {
            VrPlugin p=VrPlugin.Instance;
            if(!VrPlugin.Active || !p.MotionInput.Value || !p.PhysicalInteractions.Value || p.UI.MenuOpen || !freeLook){left.Valid=right.Valid=false;return;}
            if(Time.unscaledTime>=nextPrune){known.Clear();nextPrune=Time.unscaledTime+5;}
            Push(left,p.Left,scale,p.ClimbControls.LeftHeld || p.GliderGrips.LeftHeld || p.HandsUse.LeftHolding);
            Push(right,p.Right,scale,p.ClimbControls.RightHeld || p.GliderGrips.RightHeld || p.HandsUse.RightHolding || p.SprayControls.HoldingCan || p.CameraControls.ReservesRight || p.Basketball.ReservesRight || p.Gesture.Active);
        }
        bool Eligible(Collider collider) {
            if(collider==null || collider.isTrigger || !collider.enabled || !collider.gameObject.activeInHierarchy)return false;
            Rigidbody body=collider.attachedRigidbody;if(body==null || body.isKinematic)return false;
            bool allowed;if(!known.TryGetValue(body,out allowed)){
                allowed=HandPushMath.Target(body.name,Diagnostics.PathOf(body.transform),body.GetComponent<HingeJoint>()!=null,body.isKinematic);known[body]=allowed;
            }
            return allowed;
        }
        void Push(Palm palm,Hand hand,float scale,bool occupied) {
            if(!hand.Valid || occupied || hand.Visual==null){palm.Valid=false;return;}
            Vector3[] points=palm.Points;points[0]=hand.Visual.transform.TransformPoint(hand.GripLocalPoint);
            for(int i=1;i<6;i++)points[i]=hand.FingerTip(i-1);
            Vector3 correction=Vector3.zero;HashSet<Rigidbody> pushed=palm.Pushed;pushed.Clear();
            for(int i=0;i<6;i++){
                Vector3 current=points[i],previous=palm.Previous[i];palm.Previous[i]=current;
                if(!palm.Valid || !HandPushMath.Stroke(previous,current,Time.unscaledDeltaTime,scale))continue;
                float radius=(i==0?.026f:.009f)*scale;Vector3 travel=current-previous;float distance=travel.magnitude;
                Collider contact=null;Vector3 point=Vector3.zero,normal=Vector3.zero;float nearest=distance;
                // All world solids occlude the sweep, including walls in front of a pushable door.
                RaycastHit[] hits=palm.Hits;int hitCount=distance>.00001f?Physics.SphereCastNonAlloc(previous,radius,travel/distance,hits,distance,~(1<<31|1<<5|1<<2),QueryTriggerInteraction.Ignore):0;
                if(hitCount==hits.Length){hits=Physics.SphereCastAll(previous,radius,travel/distance,distance,~(1<<31|1<<5|1<<2),QueryTriggerInteraction.Ignore);hitCount=hits.Length;}
                for(int hitIndex=0;hitIndex<hitCount;hitIndex++){RaycastHit hit=hits[hitIndex];
                    if(hit.collider.GetComponent<CharacterController>()!=null || hit.distance>nearest)continue;
                    nearest=hit.distance;contact=hit.collider;point=hit.point;normal=hit.normal;
                }
                if(contact!=null && !Eligible(contact))continue;
                Collider[] overlaps=palm.Overlaps;int overlapCount=contact==null?Physics.OverlapSphereNonAlloc(current,radius,overlaps,~(1<<31|1<<5|1<<2),QueryTriggerInteraction.Ignore):0;
                if(overlapCount==overlaps.Length){overlaps=Physics.OverlapSphere(current,radius,~(1<<31|1<<5|1<<2),QueryTriggerInteraction.Ignore);overlapCount=overlaps.Length;}
                for(int overlapIndex=0;overlapIndex<overlapCount;overlapIndex++){Collider candidate=overlaps[overlapIndex];
                    if(!Eligible(candidate))continue;
                    Vector3 closest=candidate.ClosestPoint(current),offset=current-closest;
                    if(offset.sqrMagnitude<.00000001f) {closest=candidate.ClosestPoint(previous);offset=previous-closest;}
                    if(offset.sqrMagnitude<.00000001f)continue;
                    contact=candidate;point=closest;normal=offset.normalized;break;
                }
                if(contact==null)continue;
                Rigidbody body=contact.attachedRigidbody;
                float impulse=HandPushMath.Impulse(travel/Mathf.Max(.001f,Time.unscaledDeltaTime),body.GetPointVelocity(point),normal,Time.unscaledDeltaTime,scale);
                if(impulse>0 && !pushed.Contains(body)) {
                    body.AddForceAtPosition(-normal*impulse,point,ForceMode.Impulse);pushed.Add(body);pushes++;
                    if(Time.unscaledTime>=palm.NextHaptic){VrPlugin.Instance.PulseGrip(hand);palm.NextHaptic=Time.unscaledTime+.12f;}
                }
                float penetration=radius-Vector3.Dot(current-point,normal);
                if(penetration>0)correction+=normal*Mathf.Min(penetration,.12f*scale);
            }
            hand.Visual.transform.position+=Vector3.ClampMagnitude(correction,.12f*scale);palm.Valid=true;
        }
        internal void Clear(){known.Clear();left.Valid=right.Valid=false;pushes=0;nextPrune=0;}
        internal string Describe(){return "PhysicalPush known="+known.Count+" strokes="+pushes;}
    }
}
