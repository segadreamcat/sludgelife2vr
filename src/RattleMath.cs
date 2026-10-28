using System;
using UnityEngine;
namespace SludgeLife2VR {
    // A small loose ball inside a closed cylinder. Impacts, rather than hand speed, trigger knocks.
    internal sealed class RattleBall {
        internal Vector3 Position=new Vector3(0,-.065f,0),Velocity;
        internal void Reset(){Position=new Vector3(0,-.065f,0);Velocity=Vector3.zero;}
        internal float Step(Vector3 acceleration,float dt) {
            dt=HandPoseMath.Clamp(dt,0,.05f);int steps=Math.Max(1,(int)Math.Ceiling(dt*180));float h=dt/steps,impact=0;
            for(int i=0;i<steps;i++) {
                Velocity+=Vector3.ClampMagnitude(acceleration,110)*h;Velocity*=(float)Math.Exp(-2.2*h);Position+=Velocity*h;
                if(Position.y<-.065f || Position.y>.065f){
                    float normal=Position.y>0?1:-1;Position.y=normal*.065f;
                    float speed=Velocity.y*normal;if(speed>0){impact=Math.Max(impact,speed);Velocity.y=speed>.25f?-normal*speed*.48f:0;}
                }
                Vector3 radial=new Vector3(Position.x,0,Position.z);float length=radial.magnitude;
                if(length>.021f){Vector3 normal=radial/length;Position.x=normal.x*.021f;Position.z=normal.z*.021f;float speed=Vector3.Dot(Velocity,normal);if(speed>0){impact=Math.Max(impact,speed);Velocity-=normal*speed*1.45f;}}
            }
            return impact>.28f?impact:0;
        }
    }
}
