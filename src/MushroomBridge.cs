using System;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class MushroomBridge {
        sealed class Effect {internal MonoBehaviour Native,Eye;internal FieldInfo[] Fields;internal bool Wanted;}
        readonly Dictionary<Type,Effect> effects=new Dictionary<Type,Effect>();
        readonly MushroomPhase phase=new MushroomPhase();
        Camera flight;PlayMakerFSM pilot,twirl;MonoBehaviour nativeTwirl;float nextScan;
        bool loggedFlight,loggedTwirl;
        internal Camera FlightCamera {
            get {
                Scan();
                return flight!=null && flight.enabled && flight.gameObject.activeInHierarchy && pilot!=null && pilot.isActiveAndEnabled && MushroomPhase.FlightState(pilot.ActiveStateName)?flight:null;
            }
        }
        void Scan(){
            if(Time.unscaledTime<nextScan)return;nextScan=Time.unscaledTime+1;
            if(flight==null || pilot==null)foreach(Camera camera in Resources.FindObjectsOfTypeAll<Camera>())if(camera.name=="MEDITATION" && camera.gameObject.scene.IsValid()){
                flight=camera;foreach(PlayMakerFSM f in camera.GetComponents<PlayMakerFSM>())if(f.FsmName=="FSM" && f.Fsm.GetState("FLY Mouse")!=null)pilot=f;break;
            }
            if(twirl==null || nativeTwirl==null)foreach(PlayMakerFSM f in SceneInventory.Fsms)if(f!=null && f.name=="LVL_UI" && f.FsmName=="TWIRL" && f.gameObject.scene.IsValid()){
                twirl=f;
                FsmState state=f.Fsm.GetState("SHROOM2");if(state!=null)foreach(FsmStateAction a in state.Actions){SetProperty action=a as SetProperty;if(action!=null && action.targetProperty!=null && action.targetProperty.TargetTypeName!=null && action.targetProperty.TargetTypeName.EndsWith(".Twirl")){nativeTwirl=action.targetProperty.TargetObject.Value as MonoBehaviour;break;}}
                break;
            }
        }
        static FieldInfo[] Settings(Type type){
            List<FieldInfo> result=new List<FieldInfo>();
            for(Type t=type;t!=null && t!=typeof(MonoBehaviour);t=t.BaseType)foreach(FieldInfo field in t.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                if(!field.IsStatic && !field.IsInitOnly && !field.IsNotSerialized && (field.IsPublic || Attribute.IsDefined(field,typeof(SerializeField))))result.Add(field);
            return result.ToArray();
        }
        void Mirror(MonoBehaviour native,Camera eye,bool enabled){
            if(native==null)return;Type type=native.GetType();Effect effect;
            if(!effects.TryGetValue(type,out effect)){
                if(!enabled)return;
                MonoBehaviour copy=eye.gameObject.AddComponent(type) as MonoBehaviour;copy.enabled=false;
                effect=new Effect{Eye=copy,Fields=Settings(type)};effects.Add(type,effect);
            }
            effect.Native=native;
            // Each eye component owns its materials and temporary textures. Copy authored settings only.
            foreach(FieldInfo field in effect.Fields)field.SetValue(effect.Eye,field.GetValue(native));
            effect.Wanted=enabled;
        }
        internal void Update(Camera eye,bool menu){
            bool flying=FlightCamera!=null;
            bool swirl=phase.Twirl(twirl==null?null:twirl.ActiveStateName,flying,menu);
            foreach(Effect effect in effects.Values)effect.Wanted=false;
            if(flying && !menu)foreach(MonoBehaviour native in flight.GetComponents<MonoBehaviour>()){
                Type type=native.GetType();string name=type.Name;
                if(type.Namespace=="UnityStandardAssets.ImageEffects" && (name=="Fisheye" || name=="Bloom" || name=="EdgeDetection" || name=="ColorCorrectionCurves"))Mirror(native,eye,native.enabled);
            }
            Mirror(nativeTwirl,eye,swirl);
            foreach(Effect effect in effects.Values)if(effect.Eye!=null && effect.Eye.enabled!=effect.Wanted)effect.Eye.enabled=effect.Wanted;
            if(flying!=loggedFlight){loggedFlight=flying;Log("Native mushroom flight="+flying+" pilot="+(pilot==null?"none":pilot.ActiveStateName));}
            if(swirl!=loggedTwirl){loggedTwirl=swirl;Log("Native mushroom eye effects twirl="+swirl);}
        }
        internal bool FlyInput(GetAxisVectorRewired action){
            if(!VrPlugin.Active || !VrPlugin.Instance.MotionInput.Value || action.Fsm.GameObject==null || action.Fsm.GameObject.name!="MEDITATION" || action.Fsm.Name!="FSM" || !MushroomPhase.FlightState(action.Fsm.ActiveStateName) || action.horizontalAxis.Value!="MoveH" || action.verticalAxis.Value!="MoveV")return false;
            VrPlugin p=VrPlugin.Instance;Vector2 stick=p.Left.Valid && !p.UI.MenuOpen?p.Left.Stick:Vector2.zero;
            Vector3 velocity=MushroomPhase.Fly(stick,p.HeadRotation*Vector3.forward,p.HeadRotation*Vector3.right,action.multiplier.Value);
            // Preserve the native smoothing, collision controller, range and 30–60 second trip timer.
            foreach(FsmStateAction a in action.Fsm.ActiveState.Actions){ControllerMove move=a as ControllerMove;if(move!=null && move.moveVector.Name=="MOVE_SET" && move.space==Space.Self){velocity=action.Fsm.GameObject.transform.InverseTransformDirection(velocity);break;}}
            action.storeVector.Value=velocity;if(!action.storeMagnitude.IsNone)action.storeMagnitude.Value=velocity.magnitude;return true;
        }
        internal void Disable(){foreach(Effect effect in effects.Values)if(effect.Eye!=null)effect.Eye.enabled=false;}
        internal void Clear(){Disable();foreach(Effect effect in effects.Values)if(effect.Eye!=null)UnityEngine.Object.Destroy(effect.Eye);effects.Clear();flight=null;pilot=twirl=null;nativeTwirl=null;nextScan=0;phase.Armed=false;loggedFlight=loggedTwirl=false;}
        internal string Describe(){return "Mushroom flight="+(flight!=null && flight.gameObject.activeInHierarchy)+" pilot="+(pilot==null?"none":pilot.ActiveStateName)+" twirl="+(twirl==null?"none":twirl.ActiveStateName)+" armed="+phase.Armed+" effects="+effects.Count;}
        static void Log(string text){BepInEx.Logging.Logger.CreateLogSource("VR Mushrooms").LogInfo(text);}
    }
}
