using UnityEngine;
using TMPro;
using UnityEngine.Rendering;

namespace SludgeLife2VR {
    // Mirror the live game caption without bringing back the opaque pause-menu panel.
    internal sealed class DialogueBridge {
        GameObject panel;
        TMP_Text original;
        TextMeshProUGUI caption;
        Material overlayMaterial, sourceMaterial;
        internal bool Visible { get { return panel != null && panel.activeSelf; } }
        internal string Describe() {
            return "dialogueOverlay=" + (caption != null && caption.isOverlay)
                + " dialogueZTest=" + (overlayMaterial == null ? -1 : overlayMaterial.GetFloat(ShaderUtilities.ShaderTag_ZTestMode))
                + " dialogueQueue=" + (overlayMaterial == null ? -1 : overlayMaterial.renderQueue);
        }
        internal void Update(Camera eye, Transform uiRoot, bool menuOpen) {
            if (original == null) {
                Transform dialogue = uiRoot.Find("DIALOGUE");
                if (dialogue != null) original = dialogue.GetComponent<TMP_Text>();
            }
            if (original == null) return;
            if (panel == null) {
                panel = new GameObject("SL2VR Dialogue", typeof(RectTransform), typeof(Canvas));
                panel.layer = 31; panel.transform.SetParent(eye.transform, false);
                Canvas canvas = panel.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = eye; canvas.sortingOrder = 100;
                RectTransform rect = (RectTransform)panel.transform;
                rect.sizeDelta = new Vector2(1000, 180);
                rect.localPosition = new Vector3(0, -0.35f, 1.5f);
                rect.localScale = Vector3.one * 0.0014f;
                GameObject text = new GameObject("Caption", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                text.layer = 31; text.transform.SetParent(rect, false);
                caption = text.GetComponent<TextMeshProUGUI>();
                caption.rectTransform.sizeDelta = rect.sizeDelta;
                caption.fontSize = 38; caption.alignment = TextAlignmentOptions.Center;
                caption.enableWordWrapping = true; caption.raycastTarget = false;
            }
            bool visible = !menuOpen && original.isActiveAndEnabled && original.color.a > 0.001f && !System.String.IsNullOrEmpty(original.text);
            panel.SetActive(visible);
            if (!visible) return;
            if (caption.font != original.font || sourceMaterial != original.fontSharedMaterial || overlayMaterial == null) {
                caption.isOverlay = false;
                if (overlayMaterial != null) Object.Destroy(overlayMaterial);
                caption.font = original.font;
                sourceMaterial = original.fontSharedMaterial;
                if (sourceMaterial == null) return;
                // Own the material: TMP's overlay setter changes its depth-test property.
                overlayMaterial = new Material(sourceMaterial);
                overlayMaterial.name = "SL2VR Caption Overlay";
                caption.fontSharedMaterial = overlayMaterial;
                caption.isOverlay = true;
                BepInEx.Logging.Logger.CreateLogSource("SL2VR Captions").LogInfo("Dialogue uses an owned overlay material; world geometry cannot occlude captions.");
            }
            // TMP may refresh shader depth when its canvas updates; overlay mode keeps testing disabled.
            overlayMaterial.SetFloat(ShaderUtilities.ShaderTag_ZTestMode, (float)CompareFunction.Always);
            overlayMaterial.renderQueue = 5000;
            caption.fontStyle = original.fontStyle;
            caption.color = Color.white;
            caption.text = original.text;
            caption.maxVisibleCharacters = original.maxVisibleCharacters;
            caption.maxVisibleWords = original.maxVisibleWords;
            caption.maxVisibleLines = original.maxVisibleLines;
        }
        internal void Clear() {
            if (panel != null) Object.Destroy(panel);
            if (overlayMaterial != null) Object.Destroy(overlayMaterial);
            overlayMaterial = null; sourceMaterial = null;
            panel = null; caption = null; original = null;
        }
    }
}
