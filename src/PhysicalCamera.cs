using System;
using System.Collections.Generic;
using System.IO;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace SludgeLife2VR {
    // The native BRAIN and PIC FSMs retain inventory, shutter timing and photo quests.
    internal sealed class PhysicalCamera {
        PlayMakerFSM brain, photo;
        GameObject root;
        Camera lens;
        RenderTexture preview;
        Material screenMaterial;
        Material[] materials;
        Renderer nativeRenderer;
        bool nativeVisible, wanted, previousWanted, shutterContact, tipValid;
        int inputFrame=-1;
        float nextScan, nextPreview, frozenUntil, lastToggle;
        float zoom=55;
        Vector3 previousTip;
        Quaternion stableRotation;
        bool rotationValid, holsterGrip;
        float blockAltUntil;
        internal bool Held { get; private set; }
        internal bool Supporting { get; private set; }
        internal bool ReservesRight { get {return Enabled && (wanted || Held || holsterGrip);} }
        internal bool Blocks(string action) {return Enabled && action=="AltTrigger" && (Held || holsterGrip || Time.unscaledTime<blockAltUntil);}
        internal bool Enabled { get { return VrPlugin.Active && VrPlugin.Instance.MotionInput.Value && VrPlugin.Instance.PhysicalCameraEnabled.Value; } }
        internal Transform Lens { get { return Held && lens!=null ? lens.transform : null; } }
        internal bool PhotoTarget(Fsm fsm) { return Held && fsm!=null && fsm.Name=="CAM_TARGET"; }
        void Find() {
            if (brain!=null && photo!=null || Time.unscaledTime<nextScan) return;
            nextScan=Time.unscaledTime+2;
            foreach (PlayMakerFSM f in SceneInventory.Fsms) {
                if (f==null || !f.gameObject.scene.IsValid()) continue;
                if (f.name=="PLAYER" && f.FsmName=="BRAIN") brain=f;
                if (f.name=="LVL_UI" && f.FsmName=="PIC") photo=f;
            }
        }
        internal bool EquipButton(string method) {
            if (inputFrame!=Time.frameCount) {
                inputFrame=Time.frameCount; previousWanted=wanted; Find();
                VrPlugin p=VrPlugin.Instance;
                FsmBool owned=brain==null?null:brain.FsmVariables.FindFsmBool("HASCAM");
                if (!Enabled || brain==null || owned==null || !owned.Value || p.UI.MenuOpen || !p.Right.Valid) wanted=false;
                else {
                    float scale=Mathf.Max(.1f,p.Right.Visual==null?1:p.Right.Visual.transform.lossyScale.x);
                    Vector3 atHead=p.Right.Visual==null?Vector3.zero:Quaternion.Inverse(Quaternion.Euler(0,p.HeadRotation.eulerAngles.y,0))
                        *(p.Right.Visual.transform.TransformPoint(p.Right.GripLocalPoint)-p.HeadPosition)/scale;
                    bool back=p.Right.Valid && HandInteractionMath.BackHolster(atHead);
                    if (!wanted && back && p.Right.Grip && !p.Right.PreviousGrip && !p.HandsUse.RightHolding && !p.ClimbControls.Holding) {
                        wanted=true;holsterGrip=true;blockAltUntil=Time.unscaledTime+.4f;lastToggle=Time.unscaledTime;p.PulseRight();
                    } else if (wanted && back && !p.Right.Grip && p.Right.PreviousGrip) {
                        wanted=false;holsterGrip=false;blockAltUntil=Time.unscaledTime+.4f;lastToggle=Time.unscaledTime;p.PulseRight();
                    } else if (p.Right.Menu && !p.Right.PreviousMenu && !p.HandsUse.RightHolding && !p.ClimbControls.Holding) {
                        wanted=!wanted;lastToggle=Time.unscaledTime;
                    }
                    if(holsterGrip && !p.Right.Grip){holsterGrip=false;blockAltUntil=Time.unscaledTime+.4f;}
                }
                // Death, laptop or a scripted interruption can dismiss the native tool.
                FsmBool held=brain==null?null:brain.FsmVariables.FindFsmBool("CAMHOLDIN");
                if (wanted && Time.unscaledTime-lastToggle>1 && (held==null || !held.Value)) wanted=false;
            }
            return method.EndsWith("Down")?wanted&&!previousWanted:method.EndsWith("Up")?!wanted&&previousWanted:wanted;
        }
        bool Build() {
            VrPlugin p=VrPlugin.Instance;
            if (root!=null) return true;
            if (p.Source==null) return false;
            Transform prop=p.Source.transform.Find("PROP_CAM"); if (prop==null) return false;
            MeshFilter source=prop.GetComponent<MeshFilter>(); nativeRenderer=prop.GetComponent<Renderer>();
            if (source==null || source.sharedMesh==null || source.sharedMesh.name!="PROP_CAM" || nativeRenderer==null) return false;
            root=new GameObject("VR Native Handheld Camera"); root.layer=31;
            root.transform.SetParent(p.Right.Visual.transform,false); root.transform.localPosition=CameraFit.HandPosition;root.transform.localRotation=CameraFit.HandRotation;
            GameObject model=new GameObject("Original PROP_CAM",typeof(MeshFilter),typeof(MeshRenderer)); model.layer=31;
            model.transform.SetParent(root.transform,false); model.transform.localRotation=Quaternion.Euler(-90,0,0); model.transform.localScale=Vector3.one*.30f;
            model.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;
            Material[] original=nativeRenderer.sharedMaterials;
            foreach(Material m in Resources.FindObjectsOfTypeAll<Material>())if(m.name=="LVL_TEXTURE1") {original=new Material[]{m};break;} materials=new Material[original.Length];
            for (int i=0;i<materials.Length;i++) materials[i]=new Material(original[i]);
            model.GetComponent<MeshRenderer>().sharedMaterials=materials;
            GameObject view=new GameObject("VR Photo Lens",typeof(Camera)); view.layer=31; view.transform.SetParent(root.transform,false);
            view.transform.localPosition=new Vector3(0,.020f,.050f); lens=view.GetComponent<Camera>(); lens.CopyFrom(p.Source);
            lens.enabled=false; lens.stereoTargetEye=StereoTargetEyeMask.None; lens.orthographic=false;
            UnityEngine.XR.XRDevice.DisableAutoXRCameraTracking(lens,true);
            preview=new RenderTexture(640,480,24,RenderTextureFormat.ARGB32); preview.Create();
            GameObject screen=GameObject.CreatePrimitive(PrimitiveType.Quad); screen.name="Camera Live Viewfinder";screen.layer=31;
            UnityEngine.Object.Destroy(screen.GetComponent<Collider>()); screen.transform.SetParent(root.transform,false);
            screen.transform.localPosition=CameraFit.ScreenPosition; screen.transform.localScale=new Vector3(.104f,.078f,1);
            screenMaterial=new Material(Shader.Find("Unlit/Texture"));screenMaterial.mainTexture=preview;
            screen.GetComponent<Renderer>().sharedMaterial=screenMaterial;
            root.SetActive(false);
            Log("Using the game's PROP_CAM mesh and original materials. Handheld lens and rear live viewfinder ready.");
            return true;
        }
        internal void Update(float scale) {
            if (nativeRenderer!=null && nativeVisible && !nativeRenderer.enabled) nativeRenderer.enabled=true;
            nativeVisible=false;
            Held=false; Supporting=false;
            if (!Enabled) { if(root!=null)root.SetActive(false); wanted=false;return; }
            EquipButton("GetButton"); Find(); VrPlugin p=VrPlugin.Instance;
            FsmBool held=brain==null?null:brain.FsmVariables.FindFsmBool("CAMHOLDIN");
            bool active=held!=null && held.Value && (brain.ActiveStateName=="PIC" || brain.ActiveStateName=="PIC 2")
                && p.Right.Valid && !p.UI.MenuOpen && !p.HandsUse.RightHolding && !p.SprayControls.HoldingCan;
            if (!active || !Build()) { if(root!=null)root.SetActive(false); rotationValid=false;tipValid=false;return; }
            Held=true;root.SetActive(true); nativeVisible=nativeRenderer!=null && nativeRenderer.enabled;if(nativeVisible)nativeRenderer.enabled=false;
            Transform hand=p.Right.Visual.transform; root.transform.localPosition=CameraFit.HandPosition;
            root.transform.rotation=hand.rotation*CameraFit.HandRotation;
            Vector3 support=root.transform.TransformPoint(new Vector3(-.07f,0,0));
            Supporting=p.Left.Valid && p.Left.Grip && !p.HandsUse.LeftHolding && !p.ClimbControls.LeftHeld
                && Vector3.Distance(p.Left.Visual.transform.TransformPoint(p.Left.GripLocalPoint),support)<.12f*scale;
            if (Supporting) {
                if(!rotationValid)stableRotation=hand.rotation*CameraFit.HandRotation;
                stableRotation=Quaternion.Slerp(stableRotation,hand.rotation*CameraFit.HandRotation,1-Mathf.Exp(-18*Time.unscaledDeltaTime));
                root.transform.rotation=stableRotation;
                zoom=Mathf.Clamp(zoom-p.Left.Stick.y*35*Time.unscaledDeltaTime,20,80);
            } else stableRotation=hand.rotation*CameraFit.HandRotation;
            rotationValid=true;
            FsmFloat nativeZoom=brain.FsmVariables.FindFsmFloat("FOV_TARGET");if(nativeZoom!=null)nativeZoom.Value=zoom;
            if (Time.unscaledTime>=nextPreview && Time.unscaledTime>=frozenUntil) {
                Configure(scale); lens.targetTexture=preview;lens.Render();nextPreview=Time.unscaledTime+1/15f;
            }
            // The other index finger can also press the native camera's top shutter.
            Vector3 tip=p.Left.IndexTip, shutter=root.transform.TransformPoint(CameraFit.ShutterLocal(0));
            float distance=Vector3.Distance(tip,shutter)/scale;
            if (p.Left.Valid && !p.HandsUse.LeftHolding && !p.ClimbControls.LeftHeld && tipValid
                && HandInteractionMath.Press(distance,Vector3.Distance(tip,previousTip)/scale,ref shutterContact)
                && photo!=null && photo.ActiveStateName=="OFF") photo.SendEvent("G1");
            previousTip=tip;tipValid=true;
        }
        void Configure(float scale) {
            lens.fieldOfView=zoom;lens.aspect=4/3f;lens.nearClipPlane=.02f*scale;
            lens.farClipPlane=VrPlugin.Instance.Source.farClipPlane;
            lens.cullingMask=VrPlugin.Instance.Source.cullingMask & ~(1<<31|1<<5);
            lens.clearFlags=VrPlugin.Instance.Source.clearFlags;lens.backgroundColor=VrPlugin.Instance.Source.backgroundColor;
            lens.ResetProjectionMatrix();lens.ResetWorldToCameraMatrix();lens.ResetCullingMatrix();
        }
        internal bool Capture(TakeScreenshot action) {
            if (!Enabled || !Held || lens==null || action.Fsm.Name!="PIC" || action.Fsm.GameObject.name!="LVL_UI") return false;
            string folder=action.customPath.Value, name=action.filename.Value;
            if (String.IsNullOrEmpty(folder) || String.IsNullOrEmpty(name)) return false;
            RenderTexture target=new RenderTexture(1600,1200,24,RenderTextureFormat.ARGB32);
            RenderTexture previous=RenderTexture.active, oldTarget=lens.targetTexture;Texture2D pixels=null;
            try {
                target.Create();Configure(Mathf.Max(.1f,root.transform.lossyScale.x));lens.targetTexture=target;lens.Render();
                RenderTexture.active=target;pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                Directory.CreateDirectory(folder);string output=Path.Combine(folder,name+".png");
                File.WriteAllBytes(output,pixels.EncodeToPNG());
                Graphics.Blit(target,preview);frozenUntil=Time.unscaledTime+.45f;VrPlugin.Instance.PulseRight();
                Log("Native photo saved from the handheld lens: "+output);return true;
            } finally {
                lens.targetTexture=oldTarget;RenderTexture.active=previous;target.Release();UnityEngine.Object.Destroy(target);
                if(pixels!=null)UnityEngine.Object.Destroy(pixels);
            }
        }
        internal float FieldOfView { get {return zoom;} }
        internal string Describe() {return "PhysicalCamera held="+Held+" native="+(root!=null)+" supporting="+Supporting+" fov="+zoom;}
        internal void Clear() {
            if(nativeRenderer!=null && nativeVisible && !nativeRenderer.enabled)nativeRenderer.enabled=true;
            if(root!=null)UnityEngine.Object.Destroy(root);
            if(preview!=null){preview.Release();UnityEngine.Object.Destroy(preview);}
            if(screenMaterial!=null)UnityEngine.Object.Destroy(screenMaterial);
            if(materials!=null)foreach(Material m in materials)if(m!=null)UnityEngine.Object.Destroy(m);
            root=null;lens=null;preview=null;screenMaterial=null;materials=null;nativeRenderer=null;brain=photo=null;
            Held=Supporting=wanted=previousWanted=rotationValid=tipValid=shutterContact=holsterGrip=false;blockAltUntil=0;inputFrame=-1;nextScan=nextPreview=frozenUntil=0;
        }
        static void Log(string text){BepInEx.Logging.Logger.CreateLogSource("VR Camera").LogInfo(text);}
    }
}
