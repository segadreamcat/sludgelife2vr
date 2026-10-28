using System.Collections.Generic;
using HutongGames.PlayMaker;
using UnityEngine;

namespace SludgeLife2VR {
    internal sealed class PhysicalSpray {
        readonly NativeSprayCan can = new NativeSprayCan();
        readonly SprayRattle rattle=new SprayRattle();
        readonly SprayPressure pressure=new SprayPressure();
        readonly Dictionary<Fsm, PaintSurface> surfaces = new Dictionary<Fsm, PaintSurface>();
        readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        readonly Dictionary<FsmGameObject, GameObject> handAnchors = new Dictionary<FsmGameObject, GameObject>();
        readonly HashSet<Fsm> unsupported = new HashSet<Fsm>();
        readonly List<PlayMakerFSM> sprayFsms = new List<PlayMakerFSM>();
        float nextScan, paintUntil, nextHaptic;
        int buttonFrame = -1;
        Fsm buttonFsm;
        bool buttonResult;
        bool consumeRelease;
        internal bool ConsumingRelease { get { return consumeRelease; } }
        internal bool HoldingCan;
        internal void Update() {
            VrPlugin plugin = VrPlugin.Instance;
            if (!plugin.Right.Trigger && !plugin.Right.PreviousTrigger) consumeRelease = false;
            RestoreRenderers(); HoldingCan = false;
            if (!VrPlugin.Active || !plugin.MotionInput.Value || !plugin.Right.Valid || plugin.UI.MenuOpen || !plugin.PhysicalSpraying.Value) { can.Show(false, false); rattle.Update(false); return; }
            if (sprayFsms.Count == 0 && Time.unscaledTime >= nextScan) {
                nextScan = Time.unscaledTime + 2;
                sprayFsms.Clear();
                foreach (PlayMakerFSM fsm in SceneInventory.Fsms)
                    if (fsm!=null && fsm.gameObject.scene.IsValid() && fsm.transform.name == "SPRAY" && fsm.FsmName == "FSM"
                        && Diagnostics.PathOf(fsm.transform).StartsWith("GRAFFITI/")) sprayFsms.Add(fsm);
            }
            foreach (PlayMakerFSM fsm in sprayFsms) {
                if (fsm == null || !fsm.isActiveAndEnabled || !fsm.gameObject.activeInHierarchy || (fsm.ActiveStateName != "GRAB" && fsm.ActiveStateName != "SPRAY")) continue;
                Transform originalCan = fsm.transform.Find("SPRAYCAN");
                if (originalCan == null || !can.Create(plugin.Right.Visual.transform, 31)) continue;
                HoldingCan = true;
                FsmGameObject hand = fsm.FsmVariables.FindFsmGameObject("HAND");
                if (hand != null) { if (!handAnchors.ContainsKey(hand)) handAnchors.Add(hand, hand.Value); hand.Value = plugin.Right.Visual; }
                foreach (Renderer renderer in originalCan.GetComponentsInChildren<Renderer>(true)) {
                    if (!hidden.ContainsKey(renderer)) hidden.Add(renderer, renderer.enabled);
                    renderer.enabled = false;
                }
            }
            rattle.Update(HoldingCan);
            pressure.Update(HoldingCan,plugin.Right.Trigger,rattle.ImpactSpeed,Time.unscaledDeltaTime);
            bool spraying=HoldingCan && plugin.Right.Trigger && pressure.Ready;
            can.Show(HoldingCan,spraying);
            if(spraying && Time.unscaledTime>=nextHaptic){nextHaptic=Time.unscaledTime+.045f;plugin.PulseHand(plugin.Right,(ushort)(Time.unscaledTime<paintUntil?550:180));}
            if(HoldingCan && plugin.Right.Trigger && pressure.NeedsShake && Time.unscaledTime>=nextHaptic){nextHaptic=Time.unscaledTime+.35f;plugin.PulseHand(plugin.Right,1000);}
        }
        internal bool HandlesTag(Fsm fsm) {
            FsmGameObject target = fsm.Variables.FindFsmGameObject("TARGET");
            if (target == null || target.Value == null) return false;
            PaintSurface surface;
            if (!surfaces.TryGetValue(fsm, out surface)) { surface = new PaintSurface(); surfaces.Add(fsm, surface); }
            bool ready = surface.Prepare(target.Value);
            if (!ready && unsupported.Add(fsm)) BepInEx.Logging.Logger.CreateLogSource("VR Spray").LogWarning("Keeping native input for an unsupported tag surface: " + Diagnostics.PathOf(target.Value.transform));
            return ready;
        }
        internal bool TagButtonDown(Fsm fsm) {
            if (buttonFrame == Time.frameCount && buttonFsm == fsm) return buttonResult;
            buttonFrame = Time.frameCount; buttonFsm = fsm; buttonResult = false;
            VrPlugin plugin = VrPlugin.Instance;
            if (!HoldingCan || !pressure.Ready || !can.Ready || !plugin.Right.Valid || !plugin.Right.Trigger || plugin.UI.MenuOpen) return false;
            FsmBool allowed = fsm.Variables.FindFsmBool("CAN_TAG");
            if (allowed == null || !allowed.Value) return false;
            FsmGameObject target = fsm.Variables.FindFsmGameObject("TARGET");
            if (target == null || target.Value == null) return false;
            PaintSurface surface;
            if (!surfaces.TryGetValue(fsm, out surface)) { surface = new PaintSurface(); surfaces.Add(fsm, surface); }
            if (surface.Completed || !surface.Prepare(target.Value)) return false;
            float scale = Mathf.Max(0.1f, plugin.Right.Visual.transform.lossyScale.x);
            Ray ray = new Ray(can.Nozzle.position, plugin.Pointer.forward); Vector2 uv; Vector3 point;
            if (!surface.Hit(ray, 2.1f * scale, out uv, out point)) return false;
            consumeRelease = true;
            paintUntil = Time.unscaledTime + 0.1f;
            bool changed = surface.Stamp(uv, point, Time.unscaledDeltaTime, scale);
            if (changed && Time.unscaledTime >= nextHaptic) { nextHaptic = Time.unscaledTime + 0.10f; plugin.PulseRight(); }
            if (surface.Completed) {
                paintUntil = 0;
                surface.ClearPreview(); RestoreRenderers(); buttonResult = true;
                BepInEx.Logging.Logger.CreateLogSource("VR Spray").LogInfo("Physical tag finished at " + Diagnostics.PathOf(fsm.GameObject.transform) + "; returning completion to the game's normal save flow.");
            }
            return buttonResult;
        }
        void RestoreRenderers() {
            foreach (KeyValuePair<Renderer,bool> pair in hidden) if (pair.Key != null) pair.Key.enabled = pair.Value;
            hidden.Clear();
        }
        internal string Describe() {
            float coverage = 0; if (buttonFsm != null && surfaces.ContainsKey(buttonFsm)) coverage = surfaces[buttonFsm].Coverage;
            return "PhysicalSpray held=" + HoldingCan + " canReady=" + can.Ready + " coverage=" + coverage+" pressure="+pressure.Amount+" needsShake="+pressure.NeedsShake+" "+rattle.Describe()+" "+can.Describe();
        }
        internal void Clear() {
            pressure.Reset();rattle.Clear();RestoreRenderers(); foreach (PaintSurface surface in surfaces.Values) surface.ClearPreview(); surfaces.Clear();
            foreach (KeyValuePair<FsmGameObject,GameObject> pair in handAnchors) pair.Key.Value = pair.Value;
            handAnchors.Clear();
            unsupported.Clear();
            sprayFsms.Clear(); can.Clear(); HoldingCan = false; nextScan = 0; paintUntil = 0; buttonFrame = -1; buttonFsm = null;
            consumeRelease = false;
        }
    }
}
