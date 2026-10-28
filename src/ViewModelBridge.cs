using UnityEngine;
using System.Collections.Generic;

namespace SludgeLife2VR {
    internal sealed class ViewModelBridge {
        Transform drink, originalParent;
        int sibling;
        internal void PhysicalDrink(Transform hand) {
            if (drink == null || hand == null) return;
            // The original FSM owns the visible drink tilt, can toss and burp sequence.
            // Leave it attached to the tracked view and stop hiding its mesh.
            foreach(Renderer renderer in drink.GetComponentsInChildren<Renderer>(true))renderer.enabled=true;
        }
        internal void UpdatePhysicalDrink() { }
        void EndPhysical() { }
        internal void Attach(Camera source, Camera eye) {
            Clear();
            if (source == null || source.name != "Main Camera") return;
            drink = source.transform.Find("CANDRINK");
            if (drink == null) return;
            originalParent = drink.parent; sibling = drink.GetSiblingIndex();
            // The animated mesh uses its authored axes. Held-prop yaw corrections do not apply here.
            // Keep the authored local drinking animation, but run it around the tracked head.
            // Existing FSM/object references still point to the same object and its throw origin.
            drink.SetParent(eye.transform, false);
            BepInEx.Logging.Logger.CreateLogSource("SL2VR Viewmodel").LogInfo("Drinking animation attached to tracked view; local position=" + drink.localPosition);
        }
        internal string Describe() {
            return "Drink parent=" + (drink == null ? "none" : Diagnostics.PathOf(drink.parent))
                + " active=" + (drink != null && drink.gameObject.activeInHierarchy)
                + " localPosition=" + (drink == null ? Vector3.zero : drink.localPosition);
        }
        internal void Clear() {
            EndPhysical();
            if (drink != null) {
                if (originalParent != null) {
                    drink.SetParent(originalParent, false); drink.SetSiblingIndex(sibling);
                } else {
                    // A level unload destroyed the original camera while the prop lived on the VR rig.
                    drink.gameObject.SetActive(false); Object.Destroy(drink.gameObject);
                }
            }
            drink = null; originalParent = null;
        }
    }
}
