using System;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using Valve.VR;
using Rewired;

namespace SludgeLife2VR {
    [BepInPlugin("local.sludgelife2.vr", "Sludge Life 2 VR", "0.9.23")]
    public sealed class VrPlugin : BaseUnityPlugin {
        internal static VrPlugin Instance;
        internal static bool Active { get { return Instance != null && Instance.running && Instance.headValid; } }
        internal readonly Hand Left = new Hand();
        internal readonly Hand Right = new Hand();
        internal Vector3 HeadPosition;
        internal Quaternion HeadRotation = Quaternion.identity;
        internal Transform Pointer;
        internal Camera Source;
        internal readonly ViewModelBridge ViewModels = new ViewModelBridge();
        internal readonly PhysicalSpray SprayControls = new PhysicalSpray();
        internal readonly PhysicalClimb ClimbControls = new PhysicalClimb();
        internal readonly PhysicalHands HandsUse = new PhysicalHands();
        internal readonly PhysicalCamera CameraControls = new PhysicalCamera();
        internal readonly PhysicalFeedback Feedback = new PhysicalFeedback();
        internal readonly PhysicalBasketball Basketball = new PhysicalBasketball();
        internal readonly VirtualMouse Mouse = new VirtualMouse();
        internal readonly PhysicalGesture Gesture = new PhysicalGesture();
        internal readonly PhysicalDrums Drums = new PhysicalDrums();
        internal readonly PhysicalTouch Touch = new PhysicalTouch();
        internal readonly PhysicalPush PushControls = new PhysicalPush();
        internal readonly MushroomBridge Mushrooms=new MushroomBridge();
        internal readonly NativeFlip Flip=new NativeFlip();
        internal readonly FrameMonitor Performance=new FrameMonitor();
        internal readonly GliderHands GliderGrips=new GliderHands();
        Quaternion movementRotation=Quaternion.identity;
        Transform movementReference;
        Transform playerBody;
        PlayMakerFSM playerLook;
        bool bodyFollowsHead;
        CVRSystem system;
        Harmony harmony;
        bool running, starting, headValid, recenter = true, turnLatched, originalBackground, originalXR;
        float yaw, nextProbe = 12, nextStatus;
        Vector3 origin;
        Quaternion trackingRotation = Quaternion.identity;
        readonly TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        readonly HashSet<int> loggedActions = new HashSet<int>();
        GameObject rig;
        Camera vrCamera,cachedGameCamera;Camera[] cameraBuffer=new Camera[16];
        StereoTargetEyeMask sourceStereo;
        readonly Dictionary<Camera, StereoTargetEyeMask> otherCameras = new Dictionary<Camera, StereoTargetEyeMask>();
        internal UiBridge UI;
        bool menuCamera;
        Vector3 menuOrigin;
        Vector3 RenderOrigin {get{return menuCamera?menuOrigin:Source.transform.position;}}
        bool captureLatched, capturing;
        float gripHoldStarted = -1;
        bool previewRequested;
        float nextPreview;
        ConfigEntry<string> captureDirectory;
        ConfigEntry<bool> enabledVR, diagnostics;
        ConfigEntry<float> scale, turnAngle, triggerThreshold, renderScale, smoothTurnSpeed;
        ConfigEntry<bool> smoothTurning;
        internal ConfigEntry<bool> MotionInput, RecoverEmptyLoad, PhysicalSpraying, PhysicalClimbing, PhysicalInteractions, PhysicalCameraEnabled;
        internal ConfigEntry<string> MoveX, MoveY, LookX, LookY, Jump, Use, Spray, Pause, Sprint, Crouch, CameraAction, Telewarp, VandalEyes, Glider, Smoke, Fart;

        void Awake() {
            Instance = this;
            previewRequested = Array.IndexOf(Environment.GetCommandLineArgs(), "--vr-model-preview") >= 0;
            UI = new UiBridge();
            originalBackground = Application.runInBackground; originalXR = XRSettings.enabled;
            Application.runInBackground = true;
            enabledVR = Config.Bind("VR", "Enabled", true, "Enable native Unity OpenVR stereo rendering. F11 toggles at runtime.");
            diagnostics = Config.Bind("Debug", "SceneDump", true, "Write local scene/action reports after scene changes; F9 writes another.");
            captureDirectory = Config.Bind("Debug", "CaptureDirectory", Path.Combine(Paths.BepInExRootPath, "SludgeLife2VR-Captures"), "Folder for game eye-view captures. Hold both grips for one second or press F8.");
            scale = Config.Bind("VR", "WorldScale", 1f, "Game units per physical meter.");
            renderScale = Config.Bind("VR", "RenderScale", 0.8f, "SteamVR eye texture scale, clamped to 0.3-1.5.");
            turnAngle = Config.Bind("Comfort", "SnapTurnDegrees", 30f, "Right stick snap turn angle.");
            smoothTurning = Config.Bind("Comfort", "SmoothTurning", true, "Use continuous right-stick turning. Disable to use snap turning.");
            smoothTurnSpeed = Config.Bind("Comfort", "SmoothTurnDegreesPerSecond", 240f, "Maximum smooth-turn speed at full stick deflection.");
            triggerThreshold = Config.Bind("Controls", "TriggerThreshold", 0.55f, "Trigger press threshold.");
            MotionInput = Config.Bind("Controls", "MotionInput", true, "Enable the Rewired controller bridge while tracking is valid.");
            PhysicalSpraying = Config.Bind("Controls", "PhysicalSpraying", true, "Hold the right trigger and sweep the native can across a tag. Disable for the original instant-tag input.");
            PhysicalClimbing = Config.Bind("Controls", "PhysicalClimbing", true, "Grip ladders/pipes or a ledge and physically pull. Replaces automatic stick climbing and automatic vaults while VR is active.");
            PhysicalCameraEnabled=Config.Bind("Controls","PhysicalCamera",true,"Toggle the native handheld camera with left trigger. Right trigger takes a lens photo; left support grip and stick zoom.");
            PhysicalInteractions = Config.Bind("Controls", "PhysicalInteractions", true, "Poke world buttons; grip slugs/sodas and bring them to your mouth. Native game completion logic is preserved.");
            RecoverEmptyLoad = Config.Bind("Compatibility", "RecoverEmptyLoadScene", true, "Skip an invalid optional scene request in LOAD; preserve the game's valid LVL loader.");
            MoveX = BindAction("MoveX", "MoveH"); MoveY = BindAction("MoveY", "MoveV");
            LookX = BindAction("LookX", "LookH"); LookY = BindAction("LookY", "LookV");
            Jump = BindAction("Jump", "Jump"); Use = BindAction("Interact", "AltTrigger"); Spray = BindAction("Spray", "Trigger|TWTrigger");
            Pause = BindAction("Pause", "ESC"); Sprint = BindAction("Sprint", "Run"); Crouch = BindAction("Crouch", "Crouch");
            CameraAction = BindAction("Camera", "UseCamera"); Telewarp = BindAction("Telewarp", "UseTelewarp");
            VandalEyes = BindAction("VandalEyes", "UseVandalEyes"); Glider = BindAction("Glider", "UseGlider"); Smoke = BindAction("Smoke", "C"); Fart=BindAction("FartFlip","F");
            harmony = new Harmony("local.sludgelife2.vr");
            InputBridge.Install(harmony); GamePatches.Install(harmony);
            SceneManager.sceneLoaded += SceneLoaded;
            Logger.LogInfo("SludgeLife2VR prototype loaded. Both grips 1s / F8 capture; F10 recenter; F11 VR toggle; F9 diagnostics.");
            StartCoroutine(Boot());
        }
        ConfigEntry<string> BindAction(string key, string value) { return Config.Bind("Action names", key, value, "Exact Rewired action name. Separate aliases with |. See SludgeLife2VR-scene.txt."); }
        IEnumerator Boot() {
            yield return null;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--sl2vr-diagnostics") >= 0) { XRSettings.enabled = false; yield break; }
            if (enabledVR.Value) yield return StartVR();
        }
        void SceneLoaded(Scene s, LoadSceneMode mode) {
            Performance.Clear();SceneInventory.Clear();cachedGameCamera=null;RestoreSource(); recenter = true; nextProbe = Time.realtimeSinceStartup + 8;
            UI.Clear();Mushrooms.Clear();
            Left.Hide(); Right.Hide(); Logger.LogInfo("Scene loaded: " + s.name);
        }
        IEnumerator StartVR() {
            if (running || starting) yield break;
            starting = true;
            if (!String.Equals(XRSettings.loadedDeviceName, "OpenVR", StringComparison.OrdinalIgnoreCase)) {
                XRSettings.LoadDeviceByName("OpenVR"); yield return null;
            }
            XRSettings.enabled = true; yield return null;
            try {
                if (!XRSettings.isDeviceActive) throw new InvalidOperationException("SteamVR headset is inactive. Connect Quest through Virtual Desktop and start SteamVR, then press F11.");
                system = OpenVR.System;
                if (system == null) throw new InvalidOperationException("OpenVR input interface unavailable.");
                bool roomScale = XRDevice.SetTrackingSpaceType(TrackingSpaceType.RoomScale);
                XRSettings.eyeTextureResolutionScale = Mathf.Clamp(renderScale.Value, 0.3f, 1.5f);
                rig = new GameObject("SL2VR Rig"); DontDestroyOnLoad(rig);
                GameObject eye = new GameObject("SL2VR Stereo Camera"); eye.transform.SetParent(rig.transform, false);
                vrCamera = eye.AddComponent<Camera>(); vrCamera.enabled = false;
                Pointer = new GameObject("SL2VR Right Pointer").transform; DontDestroyOnLoad(Pointer.gameObject);
                Left.Create("SL2VR Left Hand", new Color(0.25f, 0.85f, 1f));
                Right.Create("SL2VR Right Hand", new Color(1f, 0.45f, 0.15f));
                running = true; recenter = true;
                Logger.LogInfo("Native OpenVR active: " + XRDevice.model + " roomscale=" + roomScale + " eye=" + XRSettings.eyeTextureWidth + "x" + XRSettings.eyeTextureHeight);
            } catch (Exception e) { Logger.LogError(e); StopVR(); }
            starting = false;
        }
        void Update() {
            if (previewRequested && Time.realtimeSinceStartup > nextPreview) {
                nextPreview = Time.realtimeSinceStartup + 2;
                try { if (ModelPreview.Save(captureDirectory.Value)) { previewRequested = false; Logger.LogInfo("Native hand model preview saved."); nextProbe = 0; } }
                catch (Exception e) { previewRequested = false; Logger.LogError("Model preview: " + e); }
            }
            if (Input.GetKeyDown(KeyCode.F11)) { if (running) StopVR(); else StartCoroutine(StartVR()); }
            if (Input.GetKeyDown(KeyCode.F10)) recenter = true;
            if (Input.GetKeyDown(KeyCode.F9)) nextProbe = 0;
            if (diagnostics.Value && Time.realtimeSinceStartup >= nextProbe) {
                nextProbe = float.MaxValue;
                try { Diagnostics.Dump(Path.Combine(Paths.BepInExRootPath, "SludgeLife2VR-scene.txt")); Logger.LogInfo("Scene/action report saved."); }
                catch (Exception e) { Logger.LogError("Scene probe: " + e); }
            }
            if (!running) return;
            SceneInventory.Tick();
            try {
                system.GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin.TrackingUniverseStanding, 0.011f, poses);
                headValid = XRSettings.isDeviceActive && poses[0].bDeviceIsConnected && poses[0].bPoseIsValid;
                bool available = headValid && system.IsInputAvailable();
                Left.Poll(system, poses, ETrackedControllerRole.LeftHand, available, triggerThreshold.Value);
                Right.Poll(system, poses, ETrackedControllerRole.RightHand, available, triggerThreshold.Value);
                Mouse.Poll();Gesture.Update();
                bool bothGrips = Left.Valid && Right.Valid && Left.Grip && Right.Grip;
                if (bothGrips) {
                    if (gripHoldStarted < 0) gripHoldStarted = Time.unscaledTime;
                    if (!captureLatched && Time.unscaledTime - gripHoldStarted >= 1f) { captureLatched = true; RequestCapture(); }
                } else { gripHoldStarted = -1; captureLatched = false; }
                if (Input.GetKeyDown(KeyCode.F8)) RequestCapture();
                if (!headValid) { Gesture.Clear();Drums.Clear();Touch.Clear();PushControls.Clear();Basketball.Clear();CameraControls.Clear(); HandsUse.Clear(); ClimbControls.Clear(); GliderGrips.Clear();Mushrooms.Disable();Left.Hide(); Right.Hide(); if (vrCamera != null) vrCamera.enabled = false; return; }
                if (Left.Grip && Right.Grip && (Left.MenuDown || Right.MenuDown)) recenter = true;
                if (Time.realtimeSinceStartup > nextStatus) {
                    nextStatus = Time.realtimeSinceStartup + 30;
                    Logger.LogInfo("Tracking: HMD=" + headValid + " left=" + Left.Valid + " right=" + Right.Valid + " source=" + (Source == null ? "none" : Diagnostics.PathOf(Source.transform)));
                }
            } catch (Exception e) { Logger.LogError("VR polling failed: " + e); StopVR(); }
        }
        void RequestCapture() {
            if (capturing || vrCamera == null || !vrCamera.enabled) return;
            capturing = true; StartCoroutine(CaptureViews());
        }
        void OnGUI() { if (UI != null) UI.DrawDesktopMenu(); }
        IEnumerator CaptureViews() {
            yield return new WaitForEndOfFrame();
            try {
                if (vrCamera == null || !running) yield break;
                string result = EyeCapture.Save(vrCamera, UI.Capture == null ? null : UI.Capture.targetTexture, captureDirectory.Value);
                Logger.LogInfo("VR capture saved: " + result);
                Left.Haptic(system, 1800); Right.Haptic(system, 1800);
            } catch (Exception e) { Logger.LogError("VR capture failed: " + e); }
            finally { capturing = false; }
        }
        void LateUpdate() {
            if (!running || !headValid) return;
            Performance.Begin();
            Camera desired = FindGameCamera();
            if (MushroomPhase.SwitchCamera(Source==null || !Source.gameObject.activeInHierarchy,menuCamera,desired!=null,desired!=Source)) {
                bool preserveHeading=Source!=null && !menuCamera && desired!=null;
                RestoreSource();
                Source = desired;
                if (Source == vrCamera) Source = null;
                menuCamera = Source == null;
                if (Source == null) foreach (Camera c in Camera.allCameras) if (c.name == "UICAM") { Source = c; break; }
                if (Source == null) return;
                menuCamera=Source.name=="UICAM" && Source.transform.root.name=="LVL_UI";
                if(menuCamera)menuOrigin=Source.transform.position;
                sourceStereo = Source.stereoTargetEye; Source.stereoTargetEye = StereoTargetEyeMask.None;
                XRDevice.DisableAutoXRCameraTracking(Source, true);
                Logger.LogInfo("Game camera: " + Diagnostics.PathOf(Source.transform)); if(!preserveHeading)recenter = true;
                if (!menuCamera) {
                    GameObject reference = GameObject.Find("REFCAM");
                    movementReference = reference == null ? null : reference.transform;
                    CharacterController body = movementReference == null ? null : movementReference.GetComponentInParent<CharacterController>();
                    playerBody = body == null ? null : body.transform;
                    if (playerBody != null) foreach (PlayMakerFSM fsm in playerBody.GetComponents<PlayMakerFSM>())
                        if (fsm.FsmName == "LOOK") { playerLook = fsm; break; }
                    ViewModels.Attach(Source, vrCamera);Flip.Attach(Source);
                }
            }
            Vector3 hp = InputTracking.GetLocalPosition(XRNode.Head);
            Quaternion hr = InputTracking.GetLocalRotation(XRNode.Head);
            if (recenter) {
                UI.RecenterPanel();ClimbControls.Clear();
                origin = hp; yaw = Source.transform.eulerAngles.y; trackingRotation = Quaternion.Euler(0, -hr.eulerAngles.y, 0);
                recenter = false; Logger.LogInfo("VR recentered.");
            }
            float turn = Right.Valid ? Right.Stick.x : 0;
            if (smoothTurning.Value) {
                turnLatched = false;
                if (Mathf.Abs(turn) > 0.2f && !UI.MenuOpen && !ClimbControls.Holding)
                    yaw += Mathf.Sign(turn) * ((Mathf.Abs(turn) - 0.2f) / 0.8f) * Mathf.Clamp(smoothTurnSpeed.Value, 15, 360) * Time.unscaledDeltaTime;
            } else {
                if (Mathf.Abs(turn) < 0.3f) turnLatched = false;
                if (!turnLatched && Mathf.Abs(turn) > 0.7f && !UI.MenuOpen && !ClimbControls.Holding) { yaw += Mathf.Sign(turn) * Mathf.Clamp(turnAngle.Value, 5, 90); turnLatched = true; }
            }
            // The flat camera auto-faces NPCs during dialogue. VR heading belongs to the player.
            Quaternion basis = Quaternion.Euler(0, yaw, 0) * trackingRotation;
            float ws = Mathf.Clamp(scale.Value, 0.1f, 10f);
            movementRotation=basis*hr;
            Quaternion viewBasis=FlipMath.View(basis,Flip.Rotation);
            HeadRotation = viewBasis * hr;
            // Keep body-relative movement/jump controls aligned after physical and stick turns.
            // Locked game states keep control of the body so their scripted transitions can finish.
            bodyFollowsHead = playerBody != null && playerLook != null && !UI.MenuOpen && !Flip.Animating
                && (playerLook.ActiveStateName == "FREELOOK" || playerLook.ActiveStateName == "GAMEPAD");
            if (bodyFollowsHead) playerBody.rotation = Quaternion.Euler(0, movementRotation.eulerAngles.y, 0);
            HeadPosition = RenderOrigin + viewBasis * (hp - origin) * ws;
            rig.transform.position = RenderOrigin - viewBasis * origin * ws;
            rig.transform.rotation = viewBasis; rig.transform.localScale = Vector3.one * ws;
            vrCamera.CopyFrom(Source); vrCamera.enabled = true;
            vrCamera.stereoTargetEye = StereoTargetEyeMask.Both; vrCamera.targetTexture = null;
            vrCamera.rect = new Rect(0, 0, 1, 1); vrCamera.depth = Source.depth + 0.1f;
            vrCamera.nearClipPlane = 0.03f * ws;
            UI.Update(vrCamera);
            Mushrooms.Update(vrCamera,UI.MenuOpen || menuCamera);
            if (menuCamera) { vrCamera.orthographic = false; vrCamera.clearFlags = CameraClearFlags.SolidColor; vrCamera.backgroundColor = Color.black; vrCamera.cullingMask = 1 << 31; }
            else vrCamera.cullingMask = (Source.cullingMask & ~(1 << 5)) | (1 << 31);
            vrCamera.ResetProjectionMatrix();
            vrCamera.ResetWorldToCameraMatrix();
            vrCamera.ResetCullingMatrix();
            int cameraCount=Camera.allCamerasCount;
            if(cameraBuffer.Length<cameraCount)cameraBuffer=new Camera[cameraCount+8];
            cameraCount=Camera.GetAllCameras(cameraBuffer);
            for(int cameraIndex=0;cameraIndex<cameraCount;cameraIndex++){Camera c=cameraBuffer[cameraIndex];
                if (c == Source || c == vrCamera || c == UI.Capture) continue;
                if (!otherCameras.ContainsKey(c)) otherCameras.Add(c, c.stereoTargetEye);
                c.stereoTargetEye = StereoTargetEyeMask.None;
            }
            // Native Unity tracking owns the child camera pose and render-thread stereo submission.
            XRDevice.DisableAutoXRCameraTracking(vrCamera, false);
            Left.Place(RenderOrigin, viewBasis, origin, ws); Right.Place(RenderOrigin, viewBasis, origin, ws);
            if (Right.Valid) { Pointer.position = Right.Visual.transform.position; Pointer.rotation = Right.Visual.transform.rotation; }
            else { Pointer.position = HeadPosition; Pointer.rotation = HeadRotation; }
            Performance.Mark(0);SprayControls.Update();
            Basketball.Update(ws,bodyFollowsHead);
            CharacterController climbingBody = playerBody == null ? null : playerBody.GetComponent<CharacterController>();
            Feedback.Update(climbingBody);
            Performance.Mark(1);HandsUse.BeforeClimb(climbingBody,ws,bodyFollowsHead);
            Vector3 climbDelta = ClimbControls.Update(climbingBody,basis,RenderOrigin,origin,ws,bodyFollowsHead);
            rig.transform.position += climbDelta; HeadPosition += climbDelta;
            Left.Place(RenderOrigin+climbDelta,viewBasis,origin,ws); Right.Place(RenderOrigin+climbDelta,viewBasis,origin,ws);
            ClimbControls.LockHands(Left,Right,ws);
            Performance.Mark(2);Left.PoseHand(false,ClimbControls.LeftHeld || HandsUse.LeftHolding);
            Right.PoseGesture(Gesture.Active);
            if(Basketball.Held)Right.PoseBall();
            else if(CameraControls.Held)Right.PoseCamera();
            else Right.PoseHand(SprayControls.HoldingCan,ClimbControls.RightHeld || HandsUse.RightHolding);
            GliderGrips.Update(ws);
            Drums.Update(ws,bodyFollowsHead);
            Performance.Mark(3);PushControls.Update(ws,bodyFollowsHead);
            Performance.Mark(4);Touch.Update(ws,bodyFollowsHead);Basketball.PoseHeld();
            Performance.Mark(5);CameraControls.Update(ws);
            HandsUse.AfterPose(ws,bodyFollowsHead); ViewModels.UpdatePhysicalDrink();
            if (Right.Valid) { Pointer.position = Right.Visual.transform.position; Pointer.rotation = Right.Visual.transform.rotation; }
            Right.ShowAim(false);Performance.Mark(6);Performance.End();
        }
        Camera FindGameCamera() {
            if (SceneManager.GetActiveScene().name == "LVL") {
                Camera flight=Mushrooms.FlightCamera;if(flight!=null)return flight;
                if(cachedGameCamera!=null && cachedGameCamera.gameObject.activeInHierarchy)return cachedGameCamera;
                foreach (Camera c in Resources.FindObjectsOfTypeAll<Camera>())
                    if (c.gameObject.activeInHierarchy && c.name == "Main Camera" && Diagnostics.PathOf(c.transform).StartsWith("FOLLOWCAM/")) {cachedGameCamera=c;return c;}
            }
            Camera main = Camera.main;
            return main != vrCamera ? main : null;
        }
        internal static void Pose(HmdMatrix34_t m, out Vector3 position, out Quaternion rotation) {
            position = new Vector3(m.m3, m.m7, -m.m11);
            rotation = Quaternion.LookRotation(new Vector3(-m.m2, -m.m6, m.m10), new Vector3(m.m1, m.m5, -m.m9));
        }
        internal string ActionName(object argument) {
            if (argument is string) return (string)argument;
            if (!ReInput.isReady || !(argument is int)) return "";
            InputAction action = ReInput.mapping.GetAction((int)argument);
            if (action == null) return "";
            if (loggedActions.Add(action.id)) Logger.LogInfo("Game queried action " + action.id + ": " + action.name);
            return action.name;
        }
        internal bool Matches(string name, ConfigEntry<string> entry) {
            foreach (string alias in entry.Value.Split('|')) if (String.Equals(name, alias.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        internal bool Axis(string name, out float value) {
            value = 0;
            if (Matches(name, LookX) || Matches(name, LookY)) return true;
            if (Matches(name, MoveX) || Matches(name, MoveY)) {
                if(Mouse.Active){value=!Left.Valid?0:Matches(name,MoveX)?Left.Stick.x:Left.Stick.y;return true;}
                if (CameraControls.Supporting || ClimbControls.Holding) return true;
                if (!Left.Valid || Source == null) return false;
                // Move relative to the player's view even after physical or snap turning.
                Vector2 stick = Left.Stick;
                float referenceYaw = movementReference == null ? Source.transform.eulerAngles.y : movementReference.eulerAngles.y;
                float angle = (movementRotation.eulerAngles.y - referenceYaw) * Mathf.Deg2Rad;
                value = Matches(name, MoveX) ? stick.x * Mathf.Cos(angle) + stick.y * Mathf.Sin(angle) : stick.y * Mathf.Cos(angle) - stick.x * Mathf.Sin(angle);
                return true;
            }
            return false;
        }
        internal bool Button(string name, string method, out bool value) {
            bool current = false, previous = false; value = false;
            if (Matches(name, Jump)) { if (!Right.Valid) return false; current = Right.A; previous = Right.PreviousA; }
            else if (Matches(name, Use)) { if (!Right.Valid) return false; if (ClimbControls.RightHeld) { value = false; return true; } current = Right.Grip && !Left.Grip; previous = Right.PreviousGrip && !Left.PreviousGrip; }
            else if (Matches(name, Spray)) { if (!Right.Valid) return false; current = Right.Trigger; previous = Right.PreviousTrigger; }
            else if (Matches(name, Pause)) { if (!Left.Valid) return false; current = Left.Menu; previous = Left.PreviousMenu; }
            else if(Matches(name,Fart)) {if(!Left.Valid)return false;current=Left.StickClick;previous=Left.PreviousStickClick;}
            else if (Matches(name, Sprint)) {
                if (!Left.Valid) return false;
                // Physical holds, mouse precision and tool chords keep priority over running.
                bool available = !UI.MenuOpen && !Mouse.Active && !ClimbControls.Holding && !HandsUse.LeftHolding && !CameraControls.Supporting;
                current = available && Left.Grip && !Left.A && !Left.Trigger;
                previous = available && Left.PreviousGrip && !Left.PreviousA && !Left.PreviousTrigger;
            }
            else if (Matches(name, Crouch)) { if (!Left.Valid) return false; current = Left.Trigger && !Left.Grip; previous = Left.PreviousTrigger && !Left.PreviousGrip; }
            else if (Matches(name, CameraAction)) { if (CameraControls.Enabled) { value=CameraControls.EquipButton(method); return true; } if (!Right.Valid) return false; current = Right.Menu; previous = Right.PreviousMenu; }
            else if (Matches(name, Telewarp)) { if (!Left.Valid) return false; current = Left.Grip && Left.A; previous = Left.PreviousGrip && Left.PreviousA; }
            else if (Matches(name, VandalEyes)) { if (!Left.Valid) return false; current = Left.Grip && Left.Trigger; previous = Left.PreviousGrip && Left.PreviousTrigger; }
            else if (Matches(name, Glider)) { if (!Right.Valid) return false; current = Right.A && !UI.MenuOpen; previous = Right.PreviousA && !UI.MenuOpen; }
            else if (Matches(name, Smoke)) { if (!Right.Valid) return false; current = Right.StickClick; previous = Right.PreviousStickClick; }
            else return false;
            value = method.EndsWith("Down") ? current && !previous : method.EndsWith("Up") ? !current && previous : current;
            return true;
        }
        void RestoreSource() {
            Mouse.Clear();Gesture.Clear();Drums.Clear();Touch.Clear();PushControls.Clear();Basketball.Clear();CameraControls.Clear();Feedback.Clear();
            HandsUse.Clear();
            ClimbControls.Clear();
            SprayControls.Clear();
            ViewModels.Clear();Flip.Clear();GliderGrips.Clear(); movementReference = null; playerBody = null; playerLook = null; bodyFollowsHead = false;
            if (Source != null) { Source.stereoTargetEye = sourceStereo; XRDevice.DisableAutoXRCameraTracking(Source, false); }
            Source = null;
        }
        string DescribeGlider() {
            PlayMakerFSM move=null;if(playerBody!=null)foreach(PlayMakerFSM f in playerBody.GetComponents<PlayMakerFSM>())if(f.FsmName=="MOVE"){move=f;break;}
            string text="Glider holdRightA="+(Right.Valid && Right.A)+" move="+(move==null?"none":move.ActiveStateName);
            foreach(string name in new string[]{"HASGLIDER","CANUSEGLIDER","CLOSETOGROUND","HOVER","GLIDIN"}){FsmBool value=move==null?null:move.FsmVariables.FindFsmBool(name);if(value==null)value=FsmVariables.GlobalVariables.FindFsmBool(name);text+=" "+name+"="+(value==null?"?":value.Value.ToString());}
            return text;
        }
        internal string DescribeView() {
            return "VR headYaw=" + HeadRotation.eulerAngles.y + " heading=" + yaw
                + " nativeHands=" + TrackedHandModel.Ready
                + " sourceYaw=" + (Source == null ? 0 : Source.transform.eulerAngles.y)
                + " movementYaw=" + (movementReference == null ? 0 : movementReference.eulerAngles.y)
                + " bodyYaw=" + (playerBody == null ? 0 : playerBody.eulerAngles.y)
                + " bodyFollowsHead=" + bodyFollowsHead + " lookState=" + (playerLook == null ? "none" : playerLook.ActiveStateName)
                + "\nFingers L=" + Left.GripAmount + "," + Left.TriggerAmount + "," + Left.ThumbAmount
                + " R=" + Right.GripAmount + "," + Right.TriggerAmount + "," + Right.ThumbAmount
                + "\n" + Basketball.Describe() + "\n" + Drums.Describe() + "\nMiddleGesture="+Gesture.Active+"\n" + Touch.Describe()+"\n"+PushControls.Describe() + "\n" + CameraControls.Describe() + "\n" + ClimbControls.Describe() + "\n" + HandsUse.Describe() + "\n" + ViewModels.Describe()+"\n"+Mushrooms.Describe()+"\n"+Flip.Describe()+"\n"+DescribeGlider()+"\n"+GliderGrips.Describe()+"\n"+Performance.Describe();
        }
        internal void PulseHand(Hand hand,ushort strength){hand.Haptic(system,strength);}
        internal void PulseBoth(ushort strength){Left.Haptic(system,strength);Right.Haptic(system,strength);}
        internal void PulseRight() { Right.Haptic(system, 500); }
        internal void PulseGrip(Hand hand) { hand.Haptic(system, 900); }
        void StopVR() {
            running = false; headValid = false; RestoreSource();
            UI.Clear();Mushrooms.Clear();
            foreach (KeyValuePair<Camera, StereoTargetEyeMask> pair in otherCameras) if (pair.Key != null) pair.Key.stereoTargetEye = pair.Value;
            otherCameras.Clear();
            Left.Destroy(); Right.Destroy();
            TrackedHandModel.ClearAssets();
            if (Pointer != null) Destroy(Pointer.gameObject); Pointer = null;
            if (rig != null) Destroy(rig); rig = null; vrCamera = null;
            // Unity owns OpenVR initialization and shutdown.
            system = null; XRSettings.enabled = false;
        }
        void OnDestroy() {
            StopVR(); SceneManager.sceneLoaded -= SceneLoaded;
            if (harmony != null) harmony.UnpatchSelf();
            Application.runInBackground = originalBackground; XRSettings.enabled = originalXR; Instance = null;
        }
    }
    internal sealed class Hand {
        internal bool Valid, Trigger, Grip, A, Menu, StickClick, IndexTouch;
        internal float GripAmount, TriggerAmount, ThumbAmount;
        internal bool PreviousTrigger, PreviousGrip, PreviousA, PreviousMenu, PreviousStickClick;
        internal bool MenuDown { get { return Menu && !PreviousMenu; } }
        internal Vector2 Stick;
        internal GameObject Visual;
        Vector3 position;
        Quaternion rotation;
        bool leftHand;
        internal Vector3 GripLocalPoint { get { return new Vector3(leftHand ? .030f : -.030f,0,.040f); } }
        internal Quaternion TrackingRotation { get { return rotation; } }
        internal Vector3 GripTrackingPoint { get { return TrackingPoint(GripLocalPoint); } }
        internal Vector3 TrackingPoint(Vector3 local) { return position+rotation*local; }
        uint index = OpenVR.k_unTrackedDeviceIndexInvalid;
        Material material;
        GameObject marker;
        TrackedHandModel model;
        internal Vector3 IndexTip { get { return model == null ? Visual.transform.TransformPoint(Vector3.forward*.15f) : model.IndexTip; } }
        LineRenderer aimBeam;
        internal void Create(string name, Color color) {
            leftHand = name.Contains("Left");
            Visual = new GameObject(name); Visual.layer = 31;
            UnityEngine.Object.DontDestroyOnLoad(Visual);
            marker = GameObject.CreatePrimitive(PrimitiveType.Capsule); marker.transform.SetParent(Visual.transform, false); marker.layer = 31;
            Collider collider = marker.GetComponent<Collider>(); if (collider != null) UnityEngine.Object.Destroy(collider);
            marker.transform.localScale = new Vector3(0.065f, 0.085f, 0.065f);
            Shader shader = Shader.Find("Unlit/Color"); if (shader == null) shader = Shader.Find("Standard");
            material = new Material(shader); material.color = color; marker.GetComponent<Renderer>().sharedMaterial = material;
            model = new TrackedHandModel(Visual.transform, name.Contains("Left"), 31);
            if (name.Contains("Right")) {
                GameObject beam = new GameObject("SL2VR Aim Beam"); beam.layer = 31; beam.transform.SetParent(Visual.transform, false);
                LineRenderer line = beam.AddComponent<LineRenderer>(); line.sharedMaterial = material;
                aimBeam = line;
                line.useWorldSpace = false; line.positionCount = 2; line.startWidth = 0.002f; line.endWidth = 0.0007f;
                line.SetPosition(0, Vector3.zero); line.SetPosition(1, new Vector3(0, 0, 2.5f));
            }
            Visual.SetActive(false);
        }
        internal void Poll(CVRSystem system, TrackedDevicePose_t[] poses, ETrackedControllerRole role, bool inputAvailable, float threshold) {
            PreviousTrigger = Trigger; PreviousGrip = Grip; PreviousA = A; PreviousMenu = Menu; PreviousStickClick = StickClick;
            Trigger = Grip = A = Menu = StickClick = IndexTouch = false; Stick = Vector2.zero;
            GripAmount = TriggerAmount = ThumbAmount = 0;
            index = system.GetTrackedDeviceIndexForControllerRole(role);
            Valid = inputAvailable && index < poses.Length && poses[index].bDeviceIsConnected && poses[index].bPoseIsValid;
            if (!Valid) { Hide(); return; }
            VRControllerState_t state = new VRControllerState_t();
            if (!system.GetControllerState(index, ref state, (uint)Marshal.SizeOf(typeof(VRControllerState_t)))) { Valid = false; Hide(); return; }
            VrPlugin.Pose(poses[index].mDeviceToAbsoluteTracking, out position, out rotation);
            Stick = new Vector2(state.rAxis0.x, state.rAxis0.y);
            if (Stick.magnitude < 0.18f) Stick = Vector2.zero;
            Trigger = state.rAxis1.x > threshold || Pressed(state, EVRButtonId.k_EButton_SteamVR_Trigger);
            IndexTouch=(state.ulButtonTouched & (1UL << (int)EVRButtonId.k_EButton_SteamVR_Trigger))!=0;
            Grip = Pressed(state, EVRButtonId.k_EButton_Grip);
            A = Pressed(state, EVRButtonId.k_EButton_A);
            Menu = Pressed(state, EVRButtonId.k_EButton_ApplicationMenu);
            StickClick = Pressed(state, EVRButtonId.k_EButton_SteamVR_Touchpad);
            GripAmount = Mathf.Clamp01(state.rAxis2.x); if (GripAmount <= 0 && Grip) GripAmount = 1;
            TriggerAmount = Mathf.Clamp01(state.rAxis1.x); if (TriggerAmount <= 0 && Trigger) TriggerAmount = 1;
            ThumbAmount = ((state.ulButtonTouched & 0x100000082UL) != 0 || (state.ulButtonPressed & 0x100000080UL) != 0) ? 1 : 0;
        }
        static bool Pressed(VRControllerState_t state, EVRButtonId button) { return (state.ulButtonPressed & (1UL << (int)button)) != 0; }
        internal void Place(Vector3 anchor, Quaternion basis, Vector3 origin, float scale) {
            if (Visual == null) return;
            Visual.SetActive(Valid);
            if (!Valid) return;
            Visual.transform.position = anchor + basis * (position - origin) * scale;
            Visual.transform.rotation = basis * rotation;
            Visual.transform.localScale = Vector3.one * scale;
        }
        internal void PoseHand(bool holdingCan, bool climbing) { if(model!=null){model.CameraPose=false;model.HandlePose=false;} if (model != null && model.Update(climbing ? .9f : GripAmount, climbing ? .75f : TriggerAmount, climbing ? 1 : ThumbAmount, holdingCan) && marker != null) marker.SetActive(false); }
        internal Vector3 FingerTip(int finger){return model==null?IndexTip:model.FingerTip(finger);}
        internal void PoseBall(){if(model!=null){model.CameraPose=false;model.HandlePose=false;model.Update(.35f,.3f,1,false);}}
        internal void PoseCamera() {if(model!=null){model.CameraPose=true;model.HandlePose=false;model.Update(.60f,TriggerAmount,1,false);}}
        internal Vector3 HandleGripPoint {get{return model==null?GripLocalPoint:model.HandleGripPoint;}}
        internal void PoseHandle(float radius){if(model!=null){model.CameraPose=false;model.MiddleGesture=false;model.HandlePose=true;model.HandleRadius=radius;model.Update(1,1,1,false);}}
        internal void PoseGesture(bool active) { if(model!=null)model.MiddleGesture=active; }
        internal void ShowAim(bool visible) { if (aimBeam != null) aimBeam.enabled = visible; }
        internal void Hide() { if (Visual != null) Visual.SetActive(false); }
        internal void Haptic(CVRSystem system, ushort duration) { if (Valid && system != null) system.TriggerHapticPulse(index, 0, duration); }
        internal void Destroy() { Valid = false; if (model != null) model.Clear(); model = null; if (Visual != null) UnityEngine.Object.Destroy(Visual); if (material != null) UnityEngine.Object.Destroy(material); Visual = null; }
    }

    internal static class InputBridge {
        internal static void Install(Harmony harmony) {
            harmony.Patch(AccessTools.Method(typeof(Controller), "GetAnyButtonDown", Type.EmptyTypes), postfix: new HarmonyMethod(typeof(InputBridge), "AnyButton"));
            foreach (MethodInfo method in typeof(Player).GetMethods(BindingFlags.Instance | BindingFlags.Public)) {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || (parameters[0].ParameterType != typeof(int) && parameters[0].ParameterType != typeof(string))) continue;
                if (method.Name == "GetAxis" || method.Name == "GetAxisRaw") harmony.Patch(method, postfix: new HarmonyMethod(typeof(InputBridge), "Axis"));
                else if (method.Name == "GetButton" || method.Name == "GetButtonDown" || method.Name == "GetButtonUp") harmony.Patch(method, postfix: new HarmonyMethod(typeof(InputBridge), "Button"));
            }
        }
        static void AnyButton(ref bool __result) {
            if (!VrPlugin.Active || !VrPlugin.Instance.MotionInput.Value) return;
            Hand l = VrPlugin.Instance.Left, r = VrPlugin.Instance.Right;
            __result = __result || (l.Valid && ((l.Trigger && !l.PreviousTrigger) || (l.A && !l.PreviousA) || (l.Menu && !l.PreviousMenu)))
                || (r.Valid && ((r.Trigger && !r.PreviousTrigger) || (r.A && !r.PreviousA) || (r.Menu && !r.PreviousMenu)));
        }
        static void Axis(object[] __args, ref float __result) {
            if (!VrPlugin.Active || !VrPlugin.Instance.MotionInput.Value) return;
            float value;
            if (VrPlugin.Instance.Axis(VrPlugin.Instance.ActionName(__args[0]), out value)) __result = value;
        }
        static void Button(object[] __args, MethodBase __originalMethod, ref bool __result) {
            if (!VrPlugin.Active || !VrPlugin.Instance.MotionInput.Value) return;
            string actionName=VrPlugin.Instance.ActionName(__args[0]);
            if(VrPlugin.Instance.Mouse.Active && (actionName=="Trigger" || actionName=="TWTrigger" || actionName=="AltTrigger")) {
                int button=actionName=="AltTrigger"?1:0;
                __result=__originalMethod.Name=="GetButtonDown"?VirtualMouse.Down(button):__originalMethod.Name=="GetButtonUp"?VirtualMouse.Up(button):VirtualMouse.Held(button);return;
            }
            if (VrPlugin.Instance.Gesture.Blocks(VrPlugin.Instance.ActionName(__args[0])) || VrPlugin.Instance.HandsUse.Blocks(VrPlugin.Instance.ActionName(__args[0])) || VrPlugin.Instance.CameraControls.Blocks(VrPlugin.Instance.ActionName(__args[0])) || VrPlugin.Instance.Basketball.Blocks(VrPlugin.Instance.ActionName(__args[0]))) { __result = false; return; }
            bool value;
            if (__originalMethod.Name == "GetButtonUp" && VrPlugin.Instance.ActionName(__args[0]) == "Trigger"
                && VrPlugin.Instance.SprayControls.ConsumingRelease) { __result = false; return; }
            if (__originalMethod.Name == "GetButtonDown" && GamePatches.TagContext != null
                && VrPlugin.Instance.ActionName(__args[0]) == "Trigger" && VrPlugin.Instance.SprayControls.HandlesTag(GamePatches.TagContext)) {
                __result = VrPlugin.Instance.SprayControls.TagButtonDown(GamePatches.TagContext); return;
            }
            if (VrPlugin.Instance.Button(VrPlugin.Instance.ActionName(__args[0]), __originalMethod.Name, out value)) __result = __result || value;
        }
    }
}


