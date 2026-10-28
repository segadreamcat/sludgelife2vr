namespace SludgeLife2VR {
    internal static class GestureInputMath {
        internal static bool Active(bool available,bool leftX,bool leftGrip,bool leftClick,bool indexTouch,bool rightTrigger,bool wasActive) {
            return available && ((leftX && !leftGrip) || leftClick && (wasActive || indexTouch || rightTrigger));
        }
    }
}
