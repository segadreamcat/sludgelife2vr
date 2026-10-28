using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class VirtualMouse {
        readonly CursorState cursor=new CursorState();
        int frame=-1;
        Vector3 hardware;
        bool initialized;
        internal bool Active {get{return VrPlugin.Active && VrPlugin.Instance.MotionInput.Value && VrPlugin.Instance.UI.MenuOpen;}}
        internal void Poll() {
            if(frame==Time.frameCount)return;frame=Time.frameCount;
            VrPlugin p=VrPlugin.Instance;bool active=Active;
            Vector3 mouse=Input.mousePosition;Vector3 moved=initialized?mouse-hardware:Vector3.zero;hardware=mouse;initialized=true;
            cursor.Step(active,p.Right.Valid?p.Right.Stick:Vector2.zero,Time.unscaledDeltaTime,(float)Math.Max(1,Screen.width)/Math.Max(1,Screen.height),p.Left.Valid&&p.Left.Grip,
                p.Right.Valid&&(p.Right.Trigger||p.Right.A),p.Right.Valid&&p.Right.Grip);
            if(active && (moved.sqrMagnitude>1 || Input.GetMouseButtonDown(0)))cursor.Hardware(new Vector2(mouse.x/Math.Max(1,Screen.width),mouse.y/Math.Max(1,Screen.height)));
            if(active)p.UI.SyncCursor(Point);
        }
        internal Vector2 Point {get{Poll();return cursor.Position;}}
        internal Vector2 Delta {get{Poll();return cursor.Delta;}}
        internal bool Button(int button,string kind){Poll();return Active && cursor.CombinedButton(button,kind,Input.GetMouseButton(button),Input.GetMouseButtonDown(button),Input.GetMouseButtonUp(button));}
        internal void MoveTo(Vector2 point){Poll();cursor.Hardware(point);VrPlugin.Instance.UI.SyncCursor(cursor.Position);}
        internal void Clear(){frame=-1;initialized=false;cursor.Step(false,Vector2.zero,0,1,false,false,false);}
        internal static Vector3 MousePosition(){VirtualMouse m=VrPlugin.Instance==null?null:VrPlugin.Instance.Mouse;if(m==null || !m.Active)return Input.mousePosition;Vector2 p=m.Point;return new Vector3(p.x*Screen.width,p.y*Screen.height,0);}
        internal static Vector2 Scroll(){VirtualMouse m=VrPlugin.Instance==null?null:VrPlugin.Instance.Mouse;return Input.mouseScrollDelta+(m!=null&&m.Active&&VrPlugin.Instance.Left.Valid?new Vector2(0,VrPlugin.Instance.Left.Stick.y*12*Mathf.Min(Time.unscaledDeltaTime,.05f)):Vector2.zero);}
        internal static bool Present(){return VrPlugin.Instance!=null&&VrPlugin.Instance.Mouse.Active || Input.mousePresent;}
        internal static bool Held(int b){return VrPlugin.Instance!=null&&VrPlugin.Instance.Mouse.Active?VrPlugin.Instance.Mouse.Button(b,""):Input.GetMouseButton(b);}
        internal static bool Down(int b){return VrPlugin.Instance!=null&&VrPlugin.Instance.Mouse.Active?VrPlugin.Instance.Mouse.Button(b,"Down"):Input.GetMouseButtonDown(b);}
        internal static bool Up(int b){return VrPlugin.Instance!=null&&VrPlugin.Instance.Mouse.Active?VrPlugin.Instance.Mouse.Button(b,"Up"):Input.GetMouseButtonUp(b);}
        internal static float Axis(string name){return AxisValue(name,false);}
        internal static float AxisRaw(string name){return AxisValue(name,true);}
        static float AxisValue(string name,bool raw) {
            VirtualMouse m=VrPlugin.Instance==null?null:VrPlugin.Instance.Mouse;
            if(m!=null&&m.Active){
                if(name=="Mouse X")return m.Delta.x*Screen.width*.1f;
                if(name=="Mouse Y")return m.Delta.y*Screen.height*.1f;
                if(name=="Mouse ScrollWheel")return Scroll().y*.1f;
            }
            return raw?Input.GetAxisRaw(name):Input.GetAxis(name);
        }
    }
    internal static class MouseInputPatches {
        static MethodInfo Replacement(MethodInfo method) {
            if(method==null || method.DeclaringType!=typeof(Input))return null;
            string name=method.Name=="get_mousePosition"?"MousePosition":method.Name=="get_mouseScrollDelta"?"Scroll":method.Name=="get_mousePresent"?"Present"
                :method.Name=="GetMouseButton"?"Held":method.Name=="GetMouseButtonDown"?"Down":method.Name=="GetMouseButtonUp"?"Up":method.Name=="GetAxis"?"Axis":method.Name=="GetAxisRaw"?"AxisRaw":null;
            return name==null?null:AccessTools.Method(typeof(VirtualMouse),name);
        }
        internal static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions) {
            foreach(CodeInstruction code in instructions){MethodInfo replacement=Replacement(code.operand as MethodInfo);if(replacement!=null){code.opcode=OpCodes.Call;code.operand=replacement;}yield return code;}
        }
        internal static void Install(Harmony harmony) {
            harmony.Patch(AccessTools.Method(typeof(HutongGames.PlayMaker.Actions.GetMouseSpeedXY),"OnUpdate"),prefix:new HarmonyMethod(typeof(MouseInputPatches),"MouseSpeed"));
            int count=0;
            using(Stream stream=typeof(MouseInputPatches).Assembly.GetManifestResourceStream("VRMouseMethods")) {
                if(stream==null)throw new IOException("Missing native mouse input catalog.");
                using(StreamReader reader=new StreamReader(stream))while(!reader.EndOfStream) {
                    string line=reader.ReadLine();if(String.IsNullOrEmpty(line))continue;string[] parts=line.Split('|');Assembly assembly=null;
                    foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name==parts[0]){assembly=a;break;}
                    if(assembly==null)assembly=Assembly.Load(parts[0]);Type type=assembly.GetType(parts[1],true);
                    foreach(MethodInfo method in type.GetMethods(BindingFlags.Instance|BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)) {
                        if(method.Name!=parts[2] || method.GetMethodBody()==null)continue;
                        harmony.Patch(method,transpiler:new HarmonyMethod(typeof(MouseInputPatches),"Rewrite"));count++;
                    }
                }
            }
            BepInEx.Logging.Logger.CreateLogSource("VR Cursor").LogInfo("Controller mouse connected to "+count+" native input methods. Right stick / trigger, no laser.");
        }
        static bool MouseSpeed(HutongGames.PlayMaker.Actions.GetMouseSpeedXY __instance) {
            if(VrPlugin.Instance==null || !VrPlugin.Instance.Mouse.Active)return true;
            Vector2 delta=VrPlugin.Instance.Mouse.Delta;
            if(__instance.mouseSpeedX!=null)__instance.mouseSpeedX.Value=delta.x*(__instance.normalized?1:Screen.width);
            if(__instance.mouseSpeedY!=null)__instance.mouseSpeedY.Value=delta.y*(__instance.normalized?1:Screen.height);
            return false;
        }
    }
}
