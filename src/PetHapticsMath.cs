using UnityEngine;
namespace SludgeLife2VR {
    internal static class PetHapticsMath {
        // Native CAT_MESH bind space has +Z up and -Y toward the nose.
        internal static bool UnderChin(Vector3 point,Vector3 normal,Bounds head) {
            Vector3 size=head.size,min=head.min,max=head.max;
            float margin=Mathf.Max(.015f,Mathf.Min(size.x,size.z)*.12f);
            return normal.z<-.18f && point.x>=min.x-margin && point.x<=max.x+margin
                && point.y>=min.y-margin && point.y<=min.y+size.y*.78f
                && point.z>=min.z-margin && point.z<=min.z+size.z*.43f;
        }
    }
    internal sealed class CatPurr {
        float strength,phase,age;
        internal bool Active {get{return strength>0;}}
        internal void Reset(){strength=phase=age=0;}
        internal ushort Step(bool contact,float delta) {
            delta=Mathf.Clamp(delta,0,.05f);
            strength=Mathf.MoveTowards(strength,contact?1:0,delta/(contact?.16f:.09f));
            if(strength<=0){phase=age=0;return 0;}
            age+=delta;phase+=delta;
            if(phase<1f/30)return 0;
            phase%=1f/30;
            // A 30 Hz rasp swells gently with the cat's breathing.
            float breath=.74f+.26f*Mathf.Sin(age*Mathf.PI*2*1.15f);
            return (ushort)Mathf.RoundToInt(strength*(360+360*breath));
        }
    }
}
