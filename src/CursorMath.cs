using System;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class CursorState {
        internal Vector2 Position=new Vector2(.5f,.5f),Delta;
        internal bool Primary,PreviousPrimary,Secondary,PreviousSecondary;
        bool open,armedPrimary,armedSecondary;
        internal void Step(bool enabled,Vector2 stick,float dt,float aspect,bool precise,bool primary,bool secondary) {
            PreviousPrimary=Primary;PreviousSecondary=Secondary;Delta=Vector2.zero;
            if(!enabled){open=false;Primary=Secondary=false;return;}
            if(!open){open=true;armedPrimary=!primary;armedSecondary=!secondary;PreviousPrimary=PreviousSecondary=false;}
            if(!primary)armedPrimary=true;if(!secondary)armedSecondary=true;
            Primary=primary && armedPrimary;Secondary=secondary && armedSecondary;
            Vector2 before=Position;
            float length=stick.magnitude;
            if(length>.18f){float amount=(Math.Min(length,1)-.18f)/.82f;amount*=amount;
                float speed=(precise?.28f:1.1f)*amount*HandPoseMath.Clamp(dt,0,.05f);
                Position+=new Vector2(stick.x/length/Math.Max(.2f,aspect),stick.y/length)*speed;
                Position=new Vector2(HandPoseMath.Clamp(Position.x,0,1),HandPoseMath.Clamp(Position.y,0,1));
            }
            Delta=Position-before;
        }
        internal void Hardware(Vector2 normalized){Vector2 before=Position;Position=new Vector2(HandPoseMath.Clamp(normalized.x,0,1),HandPoseMath.Clamp(normalized.y,0,1));Delta=Position-before;}
        internal bool Button(int button,string kind) {
            bool value=button==0?Primary:button==1?Secondary:false;
            bool previous=button==0?PreviousPrimary:button==1?PreviousSecondary:false;
            return kind=="Down"?value&&!previous:kind=="Up"?!value&&previous:value;
        }
        internal static bool Combined(bool current,bool previous,bool hardware,bool hardwareDown,bool hardwareUp,string kind) {
            bool held=current||hardware;
            bool before=previous || hardware&&!hardwareDown || hardwareUp;
            return kind=="Down"?held&&!before:kind=="Up"?!held&&before:held;
        }
        internal bool CombinedButton(int button,string kind,bool hardware,bool down,bool up) {
            return Combined(button==0?Primary:button==1?Secondary:false,button==0?PreviousPrimary:button==1?PreviousSecondary:false,hardware,down,up,kind);
        }
    }
}
