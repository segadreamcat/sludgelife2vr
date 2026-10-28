using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class PhysicalFeedback {
        bool initialized, grounded;
        float airborne, downward, until, nextPulse, lastJump;
        ushort strength;
        internal void State(string state) {
            if((state=="JUMP" || state=="SUPERJUMP") && Time.unscaledTime-lastJump>.2f){lastJump=Time.unscaledTime;until=Time.unscaledTime+.09f;strength=1100;}
        }
        internal void Update(CharacterController body) {
            if(body==null || !body.enabled || VrPlugin.Instance.UI.MenuOpen){initialized=false;return;}
            bool now=body.isGrounded;
            if(initialized && !now){airborne+=Mathf.Min(.05f,Time.unscaledDeltaTime);downward=Mathf.Min(downward,body.velocity.y);}
            if(initialized && now && !grounded && airborne>.12f && downward<-.8f && !VrPlugin.Instance.ClimbControls.Holding){
                until=Time.unscaledTime+.13f;strength=(ushort)Mathf.Clamp(1000-Mathf.RoundToInt(downward*180),1200,3300);
            }
            if(now){airborne=0;downward=0;}grounded=now;initialized=true;
            if(Time.unscaledTime<until && Time.unscaledTime>=nextPulse){nextPulse=Time.unscaledTime+.025f;VrPlugin.Instance.PulseBoth(strength);}
        }
        internal void Clear(){initialized=false;airborne=downward=until=nextPulse=0;}
    }
}
