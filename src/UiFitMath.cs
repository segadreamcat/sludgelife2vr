using UnityEngine;
namespace SludgeLife2VR {
    internal static class UiFitMath {
        internal static Vector2 Desktop(Vector2 reference) {
            return reference.x>0 && reference.y>0?reference:new Vector2(1920,1080);
        }
        internal static Vector2 Panel(Vector2 desktop) {
            desktop=Desktop(desktop);float fit=Mathf.Min(1.85f/desktop.x,1.040625f/desktop.y);
            return desktop*fit;
        }
    }
}
