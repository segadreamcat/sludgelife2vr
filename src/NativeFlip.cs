using UnityEngine;
namespace SludgeLife2VR {
    internal static class FlipMath {
        internal static bool Animated(string state){return state=="State 1" || state=="State 5" || state=="State 2" || state=="State 13" || state=="State 16" || state=="State 17";}
        internal static Quaternion View(Quaternion heading,Quaternion animation){return heading*animation;}
    }
    internal sealed class NativeFlip {
        PlayMakerFSM flip;
        internal bool Animating {get{return flip!=null && flip.isActiveAndEnabled && FlipMath.Animated(flip.ActiveStateName);}}
        internal Quaternion Rotation {get{return Animating?flip.transform.localRotation:Quaternion.identity;}}
        internal void Attach(Camera source){
            flip=null;if(source==null)return;
            for(Transform t=source.transform;t!=null;t=t.parent)foreach(PlayMakerFSM f in t.GetComponents<PlayMakerFSM>())if(f.FsmName=="FLIP"){flip=f;return;}
        }
        internal void Clear(){flip=null;}
        internal string Describe(){return "Flip="+(flip==null?"none":flip.ActiveStateName)+" animated="+Animating;}
    }
}
