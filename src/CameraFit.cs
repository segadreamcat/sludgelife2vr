using UnityEngine;
namespace SludgeLife2VR {
    internal static class CameraFit {
        // PROP_CAM native up is +Z and the lens faces -Y. The right palm cups the side.
        internal static readonly Vector3 HandPosition=new Vector3(-.095f,-.005f,.025f);
        internal static Quaternion HandRotation {get{return Quaternion.Euler(55,0,0);}}
        internal static Vector3 ShutterLocal(float press){return new Vector3(.058f,.079f-.002f*press,-.005f);}
        internal static Vector3 ShutterHand(float press) {
            Vector3 p=ShutterLocal(press);
            return HandPosition+new Vector3(p.x,.5735764f*p.y-.8191520f*p.z,.8191520f*p.y+.5735764f*p.z);
        }
        // Native back plane Y=.102912 at model scale .30; 0.7mm prevents z fighting.
        internal static readonly Vector3 ScreenPosition=new Vector3(-.013f,.012f,-(.102912f*.30f+.0007f));
    }
}
