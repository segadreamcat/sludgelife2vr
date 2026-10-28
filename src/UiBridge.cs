using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Reflection;
using HutongGames.PlayMaker;
using UnityEngine.UI;

namespace SludgeLife2VR {
    internal sealed class UiBridge {
        internal Camera Capture;
        Camera source;
        internal Transform Panel;
        RenderTexture texture;
        RenderTexture originalTarget;
        float originalFar;
        CameraClearFlags originalClear;
        Color originalBackground;
        Canvas canvas;
        RectTransform canvasRect;
        Vector2 canvasSizeDelta,desktopSize;
        RenderMode originalMode;
        Camera originalCanvasCamera;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        bool cameraOrtho;
        float cameraSize;
        Button[] buttons;
        Image cursor;
        GameObject originalCursor;
        bool originalCursorActive;
        Material material;
        VrUiInputModule module;
        GraphicRaycaster addedRaycaster;
        readonly Dictionary<BaseInputModule, bool> previousModules = new Dictionary<BaseInputModule, bool>();
        readonly Dictionary<Behaviour, bool> effects = new Dictionary<Behaviour, bool>();
        PlayMakerFSM pause, nativeCursor;
        bool cursorFsmEnabled;
        bool panelWasVisible,panelRecenter=true;
        internal GraphicRaycaster Raycaster {get{return canvas==null?null:canvas.GetComponent<GraphicRaycaster>();}}
        FsmBool paused;
        readonly DialogueBridge dialogue = new DialogueBridge();
        internal bool MenuOpen {
            get {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LOAD") return true;
                if (paused != null) return paused.Value;
                if (pause != null) return pause.ActiveStateName.StartsWith("PAUSED") || pause.ActiveStateName == "LANGUAGE SELECT" || pause.ActiveStateName == "CREDITS";
                return false;
            }
        }
        internal string Describe() {
            string state = pause == null ? "none" : pause.ActiveStateName;
            string disabled = "";
            foreach (KeyValuePair<Behaviour,bool> pair in effects) if (pair.Key != null) disabled += pair.Key.GetType().Name + ",";
            return "UI menuOpen=" + MenuOpen + " pauseState=" + state + " panelVisible=" + (Panel != null && Panel.gameObject.activeInHierarchy)
                + " worldAnchored=True panelPosition="+(Panel==null?Vector3.zero:Panel.position)
                + " desktopSize="+desktopSize+" canvasSize="+(canvasRect==null?Vector2.zero:canvasRect.rect.size)
                + " dialogueVisible=" + dialogue.Visible + " " + dialogue.Describe() + " shader=" + (material == null ? "none" : material.shader.name) + " disabledEffects=" + disabled;
        }
        internal void Update(Camera eye) {
            if (source == null) foreach (Camera c in Camera.allCameras) if (c.name == "UICAM" && c.transform.root.name == "LVL_UI") { source = c; break; }
            if (source == null) return;
            if (Capture == null) {
                Capture = source; originalTarget = source.targetTexture; originalFar = source.farClipPlane;
                originalClear = source.clearFlags; originalBackground = source.backgroundColor;
                cameraPosition = source.transform.localPosition; cameraRotation = source.transform.localRotation;
                cameraOrtho = source.orthographic; cameraSize = source.orthographicSize;
                UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(source, true);
                foreach (Canvas c in source.transform.root.GetComponentsInChildren<Canvas>(true)) {
                    if (!c.isRootCanvas || c.worldCamera != source) continue;
                    canvas = c; canvasRect = c.transform as RectTransform;
                    canvasSizeDelta=canvasRect.sizeDelta;
                    CanvasScaler scaler=c.GetComponent<CanvasScaler>();
                    desktopSize=UiFitMath.Desktop(scaler==null?Vector2.zero:scaler.referenceResolution);
                    if(c.GetComponent<GraphicRaycaster>()==null)addedRaycaster=c.gameObject.AddComponent<GraphicRaycaster>();
                    originalMode = c.renderMode; originalCanvasCamera = c.worldCamera;
                    c.renderMode = RenderMode.WorldSpace; c.worldCamera = source;
                    buttons = c.GetComponentsInChildren<Button>(true);
                    GameObject mark = new GameObject("SL2VR Menu Cursor", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    mark.transform.SetParent(canvasRect, false); cursor = mark.GetComponent<Image>();
                    cursor.color = new Color(1f, 0.8f, 0.15f, 1f); cursor.raycastTarget = false;
                    cursor.rectTransform.sizeDelta = new Vector2(22, 22); mark.SetActive(false);
                    Transform oldCursor = source.transform.root.Find("CURSOR");
                    if (oldCursor != null) { originalCursor = oldCursor.gameObject; originalCursorActive = originalCursor.activeSelf; originalCursor.SetActive(false); }
                    break;
                }
                foreach (PlayMakerFSM f in source.transform.root.GetComponents<PlayMakerFSM>()) if (f.FsmName == "PAUSE") {
                    pause = f; paused = f.FsmVariables.FindFsmBool("PAUSE");
                    if (paused == null) paused = FsmVariables.GlobalVariables.FindFsmBool("PAUSE");
                    break;
                }
                texture = new RenderTexture(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height),24,RenderTextureFormat.ARGB32); texture.Create();
                GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.name = "SL2VR UI Panel"; quad.layer = 31;
                Panel = quad.transform; Panel.SetParent(null, false);
                Panel.gameObject.SetActive(false);panelWasVisible=false;panelRecenter=true;
                UnityEngine.Object.Destroy(quad.GetComponent<Collider>());
                Shader shader = Shader.Find("UI/Default");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new System.InvalidOperationException("No transparent UI shader is available.");
                material = new Material(shader); material.mainTexture = texture; quad.GetComponent<Renderer>().sharedMaterial = material;
                material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            }
            // Flat-screen VHS, twirl, fisheye, and grading effects corrupt RT alpha and distort menu hit targets.
            foreach (MonoBehaviour effect in source.GetComponents<MonoBehaviour>()) {
                if (effect == null || effect is PlayMakerFSM) continue;
                System.Type type = effect.GetType();
                if (type.GetMethod("OnRenderImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) == null
                    && type.Name != "PostProcessingBehaviour") continue;
                if (!effects.ContainsKey(effect)) effects.Add(effect, effect.enabled);
                effect.enabled = false;
            }
            if(nativeCursor==null)foreach(PlayMakerFSM f in source.transform.root.GetComponents<PlayMakerFSM>())if(f.FsmName=="CURSOR"){nativeCursor=f;cursorFsmEnabled=f.enabled;break;}
            if(nativeCursor!=null)nativeCursor.enabled=false;
            if(originalCursor!=null)originalCursor.SetActive(false);
            // The last screen-space layout can retain a headset eye's narrow aspect ratio.
            // Keep the native desktop's authored reference size after switching to world space.
            if(canvasRect!=null && (canvasRect.rect.size-desktopSize).sqrMagnitude>.01f) {
                canvasRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,desktopSize.x);
                canvasRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,desktopSize.y);
                Canvas.ForceUpdateCanvases();
            }
            if(texture.width!=Mathf.Max(1,Screen.width) || texture.height!=Mathf.Max(1,Screen.height)) {
                RenderTexture old=texture;texture=new RenderTexture(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height),24,RenderTextureFormat.ARGB32);texture.Create();material.mainTexture=texture;old.Release();UnityEngine.Object.Destroy(old);
            }
            bool visible = source.enabled && source.gameObject.activeInHierarchy && MenuOpen;
            if(visible && (!panelWasVisible || panelRecenter)) {
                VrPlugin p=VrPlugin.Instance;
                float worldScale=Mathf.Max(.001f,Mathf.Abs(eye.transform.lossyScale.x));
                Quaternion facing=Quaternion.Euler(0,p.HeadRotation.eulerAngles.y,0);
                // Place once on opening/recenter. The panel has no tracked-camera parent.
                Panel.position=p.HeadPosition+facing*Vector3.forward*(1.5f*worldScale);
                Vector2 panelSize=UiFitMath.Panel(desktopSize);
                Panel.rotation=facing;Panel.localScale=new Vector3(panelSize.x,panelSize.y,1)*worldScale;
                panelRecenter=false;
            }
            Panel.gameObject.SetActive(visible);panelWasVisible=visible;
            dialogue.Update(eye, source.transform.root, MenuOpen);
            // Screen-space camera canvases belong to UICAM, so render with that exact camera.
            source.targetTexture = texture;
            source.farClipPlane = Mathf.Max(originalFar, 250f);
            source.clearFlags = CameraClearFlags.SolidColor;
            source.backgroundColor = source.gameObject.scene.name == "LOAD" ? originalBackground : Color.clear;
            source.stereoTargetEye = StereoTargetEyeMask.None;
            UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(source, true);
            if (canvasRect != null && canvasRect.rect.width > 0 && canvasRect.rect.height > 0) {
                // Capture straight onto the canvas plane. Native XR camera matrices skewed the old screen-space layout.
                Rect rect = canvasRect.rect;
                source.transform.position = canvasRect.TransformPoint(new Vector3(0, 0, -100));
                source.transform.rotation = canvasRect.rotation;
                source.orthographic = true;
                Matrix4x4 view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.Translate(new Vector3(0, 0, 100)) * canvasRect.worldToLocalMatrix;
                Matrix4x4 projection = Matrix4x4.Ortho(rect.xMin, rect.xMax, rect.yMin, rect.yMax, 0.01f, 250f);
                source.worldToCameraMatrix = view; source.projectionMatrix = projection;
                source.cullingMatrix = projection * view;
            }
            if (EventSystem.current != null && module == null) module = EventSystem.current.gameObject.AddComponent<VrUiInputModule>();
            if (module != null) {
                bool useVR = VrPlugin.Active && visible;
                if (useVR) foreach (BaseInputModule other in module.GetComponents<BaseInputModule>()) {
                    if (other == module) continue;
                    if (!previousModules.ContainsKey(other)) previousModules.Add(other, other.enabled);
                    other.enabled = false;
                }
                else RestoreModules();
                module.enabled = useVR;
            }
        }
        internal void RecenterPanel(){panelRecenter=true;}
        internal void DrawDesktopMenu() {
            if (VrPlugin.Active && MenuOpen && texture != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texture, ScaleMode.StretchToFill, true);
        }
        internal Vector2 MousePoint() {
            Vector2 p=VrPlugin.Instance.Mouse.Point;
            return new Vector2(p.x*texture.width,p.y*texture.height);
        }
        internal GameObject HitAt(Vector2 pixel) {
            if(canvasRect==null)return null;
            Rect rect=canvasRect.rect;
            Vector3 world=canvasRect.TransformPoint(new Vector3(rect.xMin+pixel.x/texture.width*rect.width,rect.yMin+pixel.y/texture.height*rect.height,0));
            Graphic best=null;int depth=int.MinValue;
            foreach(Graphic g in canvas.GetComponentsInChildren<Graphic>()) {
                if(g==null || !g.isActiveAndEnabled || !g.raycastTarget || g.depth<depth || g.canvasRenderer.cull)continue;
                RectTransform r=g.rectTransform;if(Mathf.Abs(r.localToWorldMatrix.determinant)<.000001f)continue;
                Vector3 local=r.InverseTransformPoint(world);if(!r.rect.Contains(new Vector2(local.x,local.y)) || !g.Raycast(pixel,source))continue;
                // A disabled Selectable must block clicks rather than exposing controls behind it.
                best=g;depth=g.depth;
            }
            return best==null?null:best.gameObject;
        }
        internal Vector3 WorldPoint(Vector2 point) {
            if(canvasRect==null || texture==null)return Vector3.zero;Rect rect=canvasRect.rect;
            return canvasRect.TransformPoint(new Vector3(rect.xMin+point.x/texture.width*rect.width,rect.yMin+point.y/texture.height*rect.height,0));
        }
        internal void SyncCursor(Vector2 normalized) {
            if(canvasRect==null || texture==null)return;
            Vector2 point=new Vector2(normalized.x*texture.width,normalized.y*texture.height);
            ShowCursor(point,MenuOpen);
            if(originalCursor!=null)originalCursor.transform.position=WorldPoint(point);
        }
        internal void FocusCursor(Button button){Vector2 pixel=ButtonPoint(button);VrPlugin.Instance.Mouse.MoveTo(new Vector2(pixel.x/texture.width,pixel.y/texture.height));}
        internal Button TargetAt(Vector2 pixel) {
            if (canvasRect == null || buttons == null) return null;
            Rect rect = canvasRect.rect;
            Vector3 world = canvasRect.TransformPoint(new Vector3(rect.xMin + pixel.x / texture.width * rect.width, rect.yMin + pixel.y / texture.height * rect.height, 0));
            Button result = null; int depth = int.MinValue;
            foreach (Button b in buttons) {
                if (b == null || !b.IsActive() || !b.IsInteractable()) continue;
                RectTransform r = b.transform as RectTransform;
                if (r == null || Mathf.Abs(r.localToWorldMatrix.determinant) < 0.000001f) continue;
                Vector3 local = r.InverseTransformPoint(world);
                if (!r.rect.Contains(new Vector2(local.x, local.y))) continue;
                int d = b.targetGraphic == null ? 0 : b.targetGraphic.depth;
                if (d >= depth) { result = b; depth = d; }
            }
            return result;
        }
        internal Button FirstButton() {
            if (buttons == null) return null;
            Button first = null;
            foreach (Button b in buttons) {
                if (b == null || !b.IsActive() || !b.IsInteractable()) continue;
                if (first == null) first = b;
                if (Diagnostics.PathOf(b.transform).Contains("/ICONS/PLAY/")) return b;
            }
            return first;
        }
        internal Vector2 ButtonPoint(Button b) {
            RectTransform r = b.transform as RectTransform;
            Vector3 local = canvasRect.InverseTransformPoint(r.TransformPoint(r.rect.center));
            Rect rect = canvasRect.rect;
            return new Vector2((local.x - rect.xMin) / rect.width * texture.width, (local.y - rect.yMin) / rect.height * texture.height);
        }
        internal Button Navigate(Button current, Vector2 direction) {
            if (current == null) return FirstButton();
            Vector2 start = ButtonPoint(current); Button best = null; float score = float.MaxValue;
            foreach (Button b in buttons) {
                if (b == null || b == current || !b.IsActive() || !b.IsInteractable()) continue;
                Vector2 offset = ButtonPoint(b) - start;
                float along = Vector2.Dot(offset, direction.normalized);
                if (along <= 1) continue;
                float candidate = offset.sqrMagnitude / along;
                if (candidate < score) { score = candidate; best = b; }
            }
            return best == null ? current : best;
        }
        internal void ShowCursor(Vector2 point, bool visible) {
            if (cursor == null) return;
            cursor.gameObject.SetActive(visible && MenuOpen);
            Rect rect = canvasRect.rect;
            cursor.rectTransform.localPosition = new Vector3(rect.xMin + point.x / texture.width * rect.width, rect.yMin + point.y / texture.height * rect.height, -1f);
            cursor.transform.SetAsLastSibling();
        }
        internal bool ScreenPoint(out Vector2 point) {
            point = Vector2.zero;
            if (Panel == null || !Panel.gameObject.activeInHierarchy || !VrPlugin.Active || !VrPlugin.Instance.Right.Valid) return false;
            Transform hand = VrPlugin.Instance.Pointer;
            Plane plane = new Plane(Panel.forward, Panel.position); float distance;
            Ray ray = new Ray(hand.position, hand.forward);
            if (!plane.Raycast(ray, out distance) || distance < 0) return false;
            Vector3 p = Panel.InverseTransformPoint(ray.GetPoint(distance));
            if (Mathf.Abs(p.x) > 0.5f || Mathf.Abs(p.y) > 0.5f) return false;
            point = new Vector2((p.x + 0.5f) * texture.width, (p.y + 0.5f) * texture.height);
            return true;
        }
        void RestoreModules() {
            foreach (KeyValuePair<BaseInputModule,bool> pair in previousModules) if (pair.Key != null) pair.Key.enabled = pair.Value;
            previousModules.Clear();
        }
        internal void Clear() {
            dialogue.Clear();panelWasVisible=false;panelRecenter=true;
            if(nativeCursor!=null)nativeCursor.enabled=cursorFsmEnabled;nativeCursor=null;
            RestoreModules();
            foreach (KeyValuePair<Behaviour,bool> pair in effects) if (pair.Key != null) pair.Key.enabled = pair.Value;
            effects.Clear();
            if (module != null) UnityEngine.Object.Destroy(module);
            if(addedRaycaster!=null)UnityEngine.Object.Destroy(addedRaycaster);addedRaycaster=null;
            if (source != null) {
                source.targetTexture = originalTarget; source.farClipPlane = originalFar; source.clearFlags = originalClear; source.backgroundColor = originalBackground;
                source.transform.localPosition = cameraPosition; source.transform.localRotation = cameraRotation;
                source.orthographic = cameraOrtho; source.orthographicSize = cameraSize;
                source.ResetWorldToCameraMatrix(); source.ResetProjectionMatrix(); source.ResetCullingMatrix();
                UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(source, false);
            }
            if(canvasRect!=null)canvasRect.sizeDelta=canvasSizeDelta;
            if (canvas != null) { canvas.renderMode = originalMode; canvas.worldCamera = originalCanvasCamera; }
            if (cursor != null) UnityEngine.Object.Destroy(cursor.gameObject);
            if (originalCursor != null) originalCursor.SetActive(originalCursorActive);
            if (Panel != null) UnityEngine.Object.Destroy(Panel.gameObject);
            if (texture != null) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            if (material != null) UnityEngine.Object.Destroy(material);
            Capture = null; Panel = null; texture = null; material = null; module = null; source = null;
            pause = null; paused = null;
            canvas = null; canvasRect = null;
            cursor = null; originalCursor = null; buttons = null;
            desktopSize=canvasSizeDelta=Vector2.zero;
        }
    }


}
