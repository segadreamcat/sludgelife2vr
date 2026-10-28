using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class PhysicalGesture {
        internal bool Active;
        float blockUntil;
        internal void Update() {
            VrPlugin p=VrPlugin.Instance;
            bool available=VrPlugin.Active && p.MotionInput.Value && !p.UI.MenuOpen && p.Left.Valid && p.Right.Valid
                && !p.HandsUse.RightHolding && !p.CameraControls.ReservesRight && !p.SprayControls.HoldingCan && !p.Basketball.ReservesRight && !p.ClimbControls.Holding && !p.GliderGrips.RightHeld;
            Active=GestureInputMath.Active(available,p.Left.A,p.Left.Grip,p.Left.StickClick,p.Right.IndexTouch,p.Right.Trigger,Active);
            if(Active)blockUntil=Time.unscaledTime+.25f;
        }
        internal bool Blocks(string action) { return (Active || Time.unscaledTime<blockUntil) && (action=="Trigger" || action=="TWTrigger" || action=="AltTrigger"); }
        internal void Clear(){Active=false;blockUntil=0;}
    }
}
