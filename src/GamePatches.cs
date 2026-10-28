using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

namespace SludgeLife2VR {
    internal static class GamePatches {
        internal static Fsm TagContext;
        internal static void Install(Harmony harmony) {
            MouseInputPatches.Install(harmony);
            harmony.Patch(AccessTools.Method(typeof(GetAxisVectorRewired),"OnUpdate"),prefix:new HarmonyMethod(typeof(GamePatches),"MushroomFly"));
            harmony.Patch(AccessTools.Method(typeof(LoadSceneAsynch), "DoLoadAsynch"), prefix: new HarmonyMethod(typeof(GamePatches), "LoadScene"));
            harmony.Patch(AccessTools.Method(typeof(Raycast), "DoRaycast"), prefix: new HarmonyMethod(typeof(GamePatches), "AimRay"), finalizer: new HarmonyMethod(typeof(GamePatches), "RestoreRay"));
            harmony.Patch(AccessTools.Method(typeof(GetAngleToTargetBool), "DoGetAngleToTarget"), prefix: new HarmonyMethod(typeof(GamePatches), "HeadLookAngle"));
            harmony.Patch(AccessTools.Method(typeof(GetButtonDownIfBool2), "OnUpdate"), prefix: new HarmonyMethod(typeof(GamePatches), "TagAction"), finalizer: new HarmonyMethod(typeof(GamePatches), "RestoreTagAction"));
            harmony.Patch(AccessTools.Method(typeof(ControllerMove), "OnUpdate"), prefix: new HarmonyMethod(typeof(GamePatches), "ClimbMove"));
            harmony.Patch(AccessTools.Method(typeof(TakeScreenshot),"OnEnter"),prefix:new HarmonyMethod(typeof(GamePatches),"HandheldPhoto"));
            harmony.Patch(AccessTools.Method(typeof(GetPosition),"DoGetPosition"),prefix:new HarmonyMethod(typeof(GamePatches),"PhotoPosition"));
            harmony.Patch(AccessTools.Method(typeof(GetMouseButton),"OnEnter"),postfix:new HarmonyMethod(typeof(GamePatches),"CameraMouse"));
            harmony.Patch(AccessTools.Method(typeof(GetMouseButton),"OnUpdate"),postfix:new HarmonyMethod(typeof(GamePatches),"CameraMouse"));
            harmony.Patch(AccessTools.Method(typeof(GetButton),"DoGetButton"),postfix:new HarmonyMethod(typeof(GamePatches),"PetButton"));
            harmony.Patch(AccessTools.Method(typeof(GetButtonUp),"OnUpdate"),prefix:new HarmonyMethod(typeof(GamePatches),"BallRelease"));
            harmony.Patch(AccessTools.Method(typeof(AddForce),"DoAddForce"),prefix:new HarmonyMethod(typeof(GamePatches),"BallForce"));
            harmony.Patch(AccessTools.Method(typeof(Fsm), "SwitchState"), prefix: new HarmonyMethod(typeof(GamePatches), "PhysicalVault"),postfix:new HarmonyMethod(typeof(GamePatches),"MoveFeedback"));
        }
        static bool MushroomFly(GetAxisVectorRewired __instance){return VrPlugin.Instance==null || !VrPlugin.Instance.Mushrooms.FlyInput(__instance);}
        static void PetButton(GetButton __instance) {
            bool touch;
            if(VrPlugin.Active && __instance.buttonName.Value=="Trigger" && VrPlugin.Instance.Touch.PetContact(__instance.Fsm,out touch)) __instance.storeResult.Value=touch;
        }
        static bool BallRelease(GetButtonUp __instance) {return !VrPlugin.Active || !VrPlugin.Instance.Basketball.Owns(__instance.Fsm);}
        static bool BallForce(AddForce __instance) {return !VrPlugin.Active || !VrPlugin.Instance.Basketball.ReplaceForce(__instance.Fsm);}
        static void CameraMouse(GetMouseButton __instance) {
            if(VrPlugin.Active && VrPlugin.Instance.Basketball.Owns(__instance.Fsm) && __instance.storeResult.Name=="CAM_SHOT")__instance.storeResult.Value=false;
            if(VrPlugin.Active && VrPlugin.Instance.CameraControls.Enabled && __instance.Fsm.Name=="BRAIN"
                && __instance.Fsm.GameObject.name=="PLAYER" && __instance.storeResult.Name=="CAM_CLICK")
                __instance.storeResult.Value=VrPlugin.Instance.CameraControls.EquipButton("GetButton");
        }
        static bool HandheldPhoto(TakeScreenshot __instance) {
            if(!VrPlugin.Active) return true;
            try { if(VrPlugin.Instance.CameraControls.Capture(__instance)) {__instance.Finish();return false;} }
            catch(Exception e) {
                BepInEx.Logging.Logger.CreateLogSource("VR Camera").LogError("Handheld photo failed: "+e);
                __instance.Finish(); return false;
            }
            return true;
        }
        static bool PhotoPosition(GetPosition __instance) {
            if(!VrPlugin.Active || !VrPlugin.Instance.CameraControls.PhotoTarget(__instance.Fsm)) return true;
            GameObject source=__instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
            if(source==null || (source!=VrPlugin.Instance.Source.gameObject && source.transform!=VrPlugin.Instance.Source.transform.root)) return true;
            Vector3 position=VrPlugin.Instance.CameraControls.Lens.position;
            if(__instance.space==Space.Self && source.transform.parent!=null) position=source.transform.parent.InverseTransformPoint(position);
            if(!__instance.vector.IsNone)__instance.vector.Value=position;
            if(!__instance.x.IsNone)__instance.x.Value=position.x;
            if(!__instance.y.IsNone)__instance.y.Value=position.y;
            if(!__instance.z.IsNone)__instance.z.Value=position.z;
            return false;
        }
        static void MoveFeedback(Fsm __instance) {
            if(VrPlugin.Active && __instance.Name=="MOVE" && __instance.GameObject!=null && __instance.GameObject.name=="PLAYER")
                VrPlugin.Instance.Feedback.State(__instance.ActiveStateName);
        }
        static bool ClimbMove(ControllerMove __instance) {
            return VrPlugin.Instance == null || !VrPlugin.Instance.ClimbControls.OwnsMove(__instance.Fsm);
        }
        static void PhysicalVault(Fsm __instance, ref FsmState toState) {
            if (VrPlugin.Instance == null || !VrPlugin.Instance.ClimbControls.Enabled || toState == null) return;
            if (__instance.Name != "MOVE" || __instance.GameObject == null || __instance.GameObject.name != "PLAYER") return;
            if (toState.Name != "VAULT" && toState.Name != "BACKVAULT" && toState.Name != "VAULT TYPE") return;
            FsmState fall = __instance.GetState("FALL no VAULT"); if (fall != null) toState = fall;
        }
        static bool TagAction(GetButtonDownIfBool2 __instance, out Fsm __state) {
            __state = TagContext;
            if (VrPlugin.Active && __instance.buttonName.Value=="Trigger" && VrPlugin.Instance.HandsUse.OwnsFoodTrigger(__instance.Fsm)) return false;
            if (!VrPlugin.Active || !VrPlugin.Instance.MotionInput.Value || !VrPlugin.Instance.PhysicalSpraying.Value) return true;
            if (__instance.buttonName.Value != "Trigger" || __instance.Fsm.GameObject == null) return true;
            if (Diagnostics.PathOf(__instance.Fsm.GameObject.transform).StartsWith("GRAFFITI/")) TagContext = __instance.Fsm;
            return true;
        }
        static Exception RestoreTagAction(Fsm __state, Exception __exception) { TagContext = __state; return __exception; }
        static bool loggedHeadLook;
        static bool HeadLookAngle(GetAngleToTargetBool __instance) {
            if (!VrPlugin.Active || VrPlugin.Instance.Source == null || VrPlugin.Instance.Source.name != "Main Camera") return true;
            GameObject viewer = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
            // Replace player viewing checks only; keep NPC facing and distance gates intact.
            if (viewer == null || (viewer != VrPlugin.Instance.Source.gameObject && viewer.transform != VrPlugin.Instance.Source.transform.root)) return true;
            GameObject target = __instance.targetObject.Value;
            if (target == null && __instance.targetPosition.IsNone) return true;
            Vector3 point = target == null ? __instance.targetPosition.Value : __instance.targetPosition.IsNone
                ? target.transform.position : target.transform.TransformPoint(__instance.targetPosition.Value);
            bool photo=VrPlugin.Instance.CameraControls.PhotoTarget(__instance.Fsm);
            Vector3 position = photo?VrPlugin.Instance.CameraControls.Lens.position:VrPlugin.Instance.HeadPosition;
            Vector3 forward = photo?VrPlugin.Instance.CameraControls.Lens.forward:VrPlugin.Instance.HeadRotation*Vector3.forward;
            if (__instance.ignoreHeight.Value) point.y = position.y;
            float angle = Vector3.Angle(point - position, forward);
            __instance.storeAngle.Value = angle;
            __instance.withinRange.Value = angle <= (photo?Mathf.Min(__instance.maxAngle.Value,VrPlugin.Instance.CameraControls.FieldOfView*.5f):__instance.maxAngle.Value);
            if (!loggedHeadLook) {
                loggedHeadLook = true;
                BepInEx.Logging.Logger.CreateLogSource("SL2VR Interaction").LogInfo("Player look-angle checks now use the tracked head pose.");
            }
            return false;
        }
        static bool LoadScene(LoadSceneAsynch __instance, ref bool __result) {
            if (VrPlugin.Instance == null || !VrPlugin.Instance.RecoverEmptyLoad.Value) return true;
            if (SceneManager.GetActiveScene().name != "LOAD") return true;
            bool invalid = __instance.sceneReference == GetSceneActionBase.SceneSimpleReferenceOptions.SceneByName
                ? __instance.sceneByName == null || String.IsNullOrEmpty(__instance.sceneByName.Value)
                : __instance.sceneAtIndex == null || __instance.sceneAtIndex.Value < 0 || __instance.sceneAtIndex.Value >= SceneManager.sceneCountInBuildSettings;
            if (!invalid) return true;
            // The valid LVL loader already exists in LVL_UI. An extra empty loader must not enqueue another load.
            __result = false;
            __instance.Finish();
            BepInEx.Logging.Logger.CreateLogSource("SL2VR Compatibility").LogWarning("Skipped an empty optional scene request in LOAD.");
            return false;
        }
        internal sealed class RayState {
            internal FsmOwnerDefault From;
            internal FsmVector3 Position, Direction;
            internal Space Space;
        }
        static void AimRay(Raycast __instance, out RayState __state) {
            __state = null;
            if (!VrPlugin.Active || !VrPlugin.Instance.Right.Valid || VrPlugin.Instance.Source == null) return;
            GameObject target = __instance.Fsm.GetOwnerDefaultTarget(__instance.fromGameObject);
            if (target == null || target != VrPlugin.Instance.Source.gameObject) return;
            __state = new RayState { From = __instance.fromGameObject, Position = __instance.fromPosition, Direction = __instance.direction, Space = __instance.space };
            __instance.fromGameObject = new FsmOwnerDefault { OwnerOption = OwnerDefaultOption.SpecifyGameObject, GameObject = new FsmGameObject(VrPlugin.Instance.Pointer.gameObject) };
            __instance.fromPosition = new FsmVector3(Vector3.zero);
            __instance.direction = new FsmVector3(Vector3.forward);
            __instance.space = Space.Self;
        }
        static Exception RestoreRay(Raycast __instance, RayState __state, Exception __exception) {
            if (__state != null) {
                __instance.fromGameObject = __state.From; __instance.fromPosition = __state.Position;
                __instance.direction = __state.Direction; __instance.space = __state.Space;
            }
            return __exception;
        }
    }
}
