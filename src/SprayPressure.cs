using System;
namespace SludgeLife2VR {
    internal sealed class SprayPressure {
        internal float Amount=1;internal bool NeedsShake;
        internal bool Ready {get{return !NeedsShake && Amount>0;}}
        internal void Update(bool held,bool trigger,float impact,float dt){
            if(!held)return;
            if(!trigger && impact>=.55f){Amount=Math.Min(1,Amount+Math.Min(.50f,impact*.40f));if(Amount>=.6f)NeedsShake=false;}
            if(trigger && Ready){Amount=Math.Max(0,Amount-Math.Max(0,Math.Min(.05f,dt))/7.5f);if(Amount<=.00001f){Amount=0;NeedsShake=true;}}
        }
        internal void Reset(){Amount=1;NeedsShake=false;}
    }
}
