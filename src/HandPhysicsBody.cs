using System.Collections.Generic;
using UnityEngine;
namespace SludgeLife2VR {
    // A dynamic compound hand presses against kinematic copies of animated body surfaces.
    public sealed class HandPhysicsBody : MonoBehaviour {
        internal static readonly HashSet<Collider> Surfaces=new HashSet<Collider>();
        readonly HashSet<Collider> ignored=new HashSet<Collider>();
        Rigidbody body;readonly SphereCollider[] shapes=new SphereCollider[6];
        Vector3 target,normal,point;float touched,nextIgnore,scale=1;bool active;
        Collider contact;readonly Collider[] overlaps=new Collider[128];
        internal Collider Contact {get{return active && Time.unscaledTime-touched<.10f?contact:null;}}
        internal Vector3 Normal {get{return normal;}}
        internal Vector3 Point {get{return point;}}
        internal static HandPhysicsBody Create(){GameObject root=new GameObject("VR Dynamic Touch Hand");root.layer=31;HandPhysicsBody h=root.AddComponent<HandPhysicsBody>();
            h.body=root.AddComponent<Rigidbody>();h.body.useGravity=false;h.body.mass=.18f;h.body.drag=5;h.body.angularDrag=10;h.body.constraints=RigidbodyConstraints.FreezeRotation;h.body.interpolation=RigidbodyInterpolation.Interpolate;h.body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            for(int i=0;i<h.shapes.Length;i++){h.shapes[i]=root.AddComponent<SphereCollider>();h.shapes[i].radius=i==0?.026f:.008f;h.shapes[i].enabled=false;}return h;}
        internal Vector3 Follow(Hand hand,Vector3 desired,Vector3 solved,float worldScale,bool near) {
            if(!near || !hand.Valid){Disable();return solved;}
            scale=worldScale;target=desired;Quaternion rotation=hand.Visual.transform.rotation;
            if(!active || (body.position-solved).sqrMagnitude>scale*scale){body.position=solved;body.velocity=Vector3.zero;touched=0;}
            active=true;body.rotation=rotation;
            for(int i=0;i<shapes.Length;i++){shapes[i].enabled=true;shapes[i].radius=(i==0?.026f:.008f)*scale;shapes[i].center=i==0?Vector3.zero:Quaternion.Inverse(rotation)*(hand.FingerTip(i-1)-desired);}
            if(Time.unscaledTime>=nextIgnore){nextIgnore=Time.unscaledTime+.1f;
                Collider[] nearby=overlaps;int count=Physics.OverlapSphereNonAlloc(solved,.65f*scale,nearby,~0,QueryTriggerInteraction.Collide);
                if(count==nearby.Length){nearby=Physics.OverlapSphere(solved,.65f*scale,~0,QueryTriggerInteraction.Collide);count=nearby.Length;}
                for(int i=0;i<count;i++){Collider c=nearby[i];
                    if(c==null || Surfaces.Contains(c) || c.GetComponent<HandPhysicsBody>()!=null || ignored.Contains(c))continue;
                    foreach(SphereCollider shape in shapes)Physics.IgnoreCollision(shape,c,true);ignored.Add(c);
                }
            }
            if(Time.unscaledTime-touched<.10f && normal.sqrMagnitude>.5f){float separation=Vector3.Dot(body.position-solved,normal);if(separation>0)solved+=normal*Mathf.Min(separation,.12f*scale);}
            return solved;
        }
        void FixedUpdate(){if(active)body.velocity=Vector3.ClampMagnitude((target-body.position)*35,4.5f*scale);}
        void OnCollisionEnter(Collision collision){OnCollisionStay(collision);}
        void OnCollisionStay(Collision collision){if(Surfaces.Contains(collision.collider)){ContactPoint[] contacts=collision.contacts;if(contacts.Length>0){normal=contacts[0].normal;point=contacts[0].point;contact=collision.collider;touched=Time.unscaledTime;}}}
        void OnCollisionExit(Collision collision){if(collision.collider==contact)contact=null;}
        internal void Disable(){active=false;contact=null;if(body!=null)body.velocity=Vector3.zero;foreach(SphereCollider shape in shapes)if(shape!=null)shape.enabled=false;}
        internal void Clear(){Disable();foreach(Collider c in ignored)if(c!=null)foreach(SphereCollider shape in shapes)if(shape!=null)Physics.IgnoreCollision(shape,c,false);ignored.Clear();Object.Destroy(gameObject);}
    }
}
