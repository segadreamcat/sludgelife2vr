using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class SprayRattle {
        readonly RattleBall ball=new RattleBall();
        readonly SprayRattleSound sound=new SprayRattleSound();
        Vector3 previous,velocity;bool initialized;float quietUntil,nextPulse;
        internal float ImpactSpeed;
        internal void Update(bool held) {
            ImpactSpeed=0;
            Hand hand=VrPlugin.Instance.Right;float dt=Time.unscaledDeltaTime;
            sound.Update(held && hand.Valid && dt>0 && dt<=.1f,dt);
            if(!held || !hand.Valid || dt<=0 || dt>.1f){initialized=false;ball.Reset();return;}
            Vector3 position=hand.GripTrackingPoint+hand.TrackingRotation*new Vector3(-.005f,-.060f,.040f);
            if(!initialized || (position-previous).sqrMagnitude>.35f*.35f){previous=position;velocity=Vector3.zero;initialized=true;quietUntil=Time.unscaledTime+.20f;ball.Reset();sound.Update(false,0);return;}
            Vector3 current=Vector3.ClampMagnitude((position-previous)/dt,8);previous=position;
            Vector3 filtered=Vector3.Lerp(velocity,current,1-Mathf.Exp(-32*dt));
            Vector3 acceleration=Vector3.ClampMagnitude((filtered-velocity)/dt,90);velocity=filtered;
            Vector3 local=Quaternion.Inverse(hand.TrackingRotation)*(Vector3.down*9.81f-acceleration);
            float impact=ball.Step(local,dt);
            if(impact>0 && Time.unscaledTime>=quietUntil && Time.unscaledTime>=nextPulse){
                nextPulse=Time.unscaledTime+.035f;
                VrPlugin.Instance.PulseHand(hand,(ushort)Mathf.Clamp(280+impact*850,350,2900));
                ImpactSpeed=impact;sound.Impact(impact);
            }
        }
        internal void Clear(){initialized=false;ball.Reset();quietUntil=nextPulse=0;sound.Clear();}
        internal string Describe(){return sound.Describe();}
    }
}
