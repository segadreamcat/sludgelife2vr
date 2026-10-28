using System;
namespace SludgeLife2VR {
    internal static class ShipButtonMath {
        internal static bool LaunchOwner(string name){return name=="ESCAPESHIP_BTN";}
        internal static bool Cap(string meshName){return meshName=="PROP_CNTRLS_BTN_RED";}
        internal static bool Candidate(float distanceSquared,bool launch){return launch || distanceSquared<=.65f*.65f;}
    }
}
