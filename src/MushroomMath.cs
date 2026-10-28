using System;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class MushroomPhase {
        internal bool Armed;
        internal bool Twirl(string state,bool flying,bool menu) {
            if(state=="SHROOM" || state=="SHROOM2" || flying)Armed=true;
            if(!flying && (state=="OFF" || state=="ON" || String.IsNullOrEmpty(state)))Armed=false;
            return Armed && !menu && (state=="SHROOM2" || state=="WOAH");
        }
        internal static bool FlightState(string state){return state=="ON" || state=="GamePad or Mouse" || state=="FLY Mouse" || state=="FLY Gamepad" || state=="State 1" || state=="State 2";}
        internal static bool SwitchCamera(bool missing,bool menu, bool hasDesired,bool changed){return missing || (hasDesired && changed);}
        internal static Vector3 Fly(Vector2 stick,Vector3 forward,Vector3 right,float speed){return (right*stick.x+forward*stick.y)*speed;}
    }
}
