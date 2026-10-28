using System;
using System.IO;
using System.Text;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Rewired;

namespace SludgeLife2VR {
    internal static class Diagnostics {
        internal static string PathOf(Transform t) {
            string result = t.name;
            while (t.parent != null) { t = t.parent; result = t.name + "/" + result; }
            return result;
        }
        internal static void Dump(string path) {
            StringBuilder b = new StringBuilder();
            b.AppendLine("Unity " + Application.unityVersion + " scene=" + SceneManager.GetActiveScene().name);
            b.AppendLine("Graphics=" + SystemInfo.graphicsDeviceType);
            if (VrPlugin.Instance != null) b.AppendLine(VrPlugin.Instance.DescribeView());
            if (VrPlugin.Instance != null) b.AppendLine(VrPlugin.Instance.SprayControls.Describe());
            b.AppendLine("Legacy XR devices=" + String.Join(",", UnityEngine.XR.XRSettings.supportedDevices));
            for (int i=0; i<SceneManager.sceneCountInBuildSettings; i++) b.AppendLine("SCENE " + i + " " + SceneUtility.GetScenePathByBuildIndex(i));
            foreach (HutongGames.PlayMaker.FsmString v in HutongGames.PlayMaker.FsmVariables.GlobalVariables.StringVariables) b.AppendLine("GLOBAL STRING " + v.Name + "=" + v.Value);
            if (ReInput.isReady) foreach (InputAction a in ReInput.mapping.Actions) b.AppendLine("ACTION " + a.id + " " + a.name + " type=" + a.type);
            foreach (Camera c in Resources.FindObjectsOfTypeAll<Camera>()) {
                if (!c.gameObject.scene.IsValid()) continue;
                b.AppendLine("CAMERA " + PathOf(c.transform) + " enabled=" + c.enabled + " active=" + c.gameObject.activeInHierarchy + " tag=" + c.tag + " depth=" + c.depth + " mask=" + c.cullingMask + " pos=" + c.transform.position + " near=" + c.nearClipPlane + " far=" + c.farClipPlane + " ortho=" + c.orthographic + " texture=" + c.targetTexture);
            }
            foreach (CharacterController c in Resources.FindObjectsOfTypeAll<CharacterController>()) b.AppendLine("CHARACTER " + PathOf(c.transform) + " height=" + c.height);
            foreach (PlayMakerFSM f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>()) {
                if (!f.gameObject.scene.IsValid()) continue;
                if (!f.Fsm.Initialized) continue;
                string p = PathOf(f.transform) + " [" + f.FsmName + "]";
                string lower = p.ToLowerInvariant();
                if (!(lower.StartsWith("player [") || lower.StartsWith("followcam") || lower.StartsWith("lvl_ui [") || lower.StartsWith("lvl_ui/login") || lower.StartsWith("lvl_ui/overlay/icons/play") || lower.StartsWith("graffiti/16"))) continue;
                if (f.FsmName == "LOC") continue;
                b.AppendLine("FSM " + p + " active=" + f.ActiveStateName);
                foreach (HutongGames.PlayMaker.FsmGameObject v in f.FsmVariables.GameObjectVariables) b.AppendLine("  GO " + v.Name + "=" + (v.Value == null ? "null" : PathOf(v.Value.transform)));
                foreach (HutongGames.PlayMaker.FsmFloat v in f.FsmVariables.FloatVariables) b.AppendLine("  FLOAT " + v.Name + "=" + v.Value);
                foreach (HutongGames.PlayMaker.FsmState s in f.FsmStates) {
                    b.AppendLine(" STATE " + s.Name);
                    foreach (HutongGames.PlayMaker.FsmTransition transition in s.Transitions) b.AppendLine("  TRANSITION " + transition.EventName + " -> " + transition.ToState);
                    if (!s.ActionsLoaded) continue;
                    foreach (HutongGames.PlayMaker.FsmStateAction a in s.Actions) {
                        b.AppendLine("   " + a.GetType().FullName);
                        foreach (FieldInfo field in a.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)) {
                            object val = field.GetValue(a);
                            if (val == null) continue;
                            HutongGames.PlayMaker.NamedVariable named = val as HutongGames.PlayMaker.NamedVariable;
                            if (named != null) b.AppendLine("    " + field.Name + "=" + val.ToString() + " variable=" + named.Name);
                            HutongGames.PlayMaker.FsmEvent evt = val as HutongGames.PlayMaker.FsmEvent;
                            if (evt != null) b.AppendLine("    EVENT " + field.Name + "=" + evt.Name);
                        }
                    }
                }
            }
            File.WriteAllText(path, b.ToString());
        }
    }
}
