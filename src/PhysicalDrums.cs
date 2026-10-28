using System.Collections.Generic;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class PhysicalDrums {
        sealed class Pad {
            internal GameObject Light;
            internal AudioSource Audio;
            internal float Until,Next;
            internal readonly bool[] Latched=new bool[6];
        }
        sealed class Board {
            internal Transform Source;
            internal GameObject Root;
            internal readonly Pad[] Pads=new Pad[16];
            internal readonly Vector3[] Previous=new Vector3[6];
            internal readonly bool[] Valid=new bool[6];
        }
        readonly List<Board> boards=new List<Board>();
        AudioClip[] clips;Material glow;float scan;int strikes;string lastBoard="none";
        internal void Update(float scale,bool freeLook) {
            VrPlugin p=VrPlugin.Instance;
            if(!VrPlugin.Active || !p.MotionInput.Value || !p.PhysicalInteractions.Value)return;
            if(Time.unscaledTime>=scan)Find();
            foreach(Board b in boards) {
                if(b.Source==null || !b.Source.gameObject.activeInHierarchy)continue;
                foreach(Pad pad in b.Pads){bool lit=Time.unscaledTime<pad.Until;if(pad.Light.activeSelf!=lit)pad.Light.SetActive(lit);}
                bool near=(b.Source.position-p.HeadPosition).sqrMagnitude<4*scale*scale;
                bool left=near && freeLook && !p.UI.MenuOpen && p.Left.Valid && !p.HandsUse.LeftHolding && !p.ClimbControls.LeftHeld && !p.GliderGrips.LeftHeld && !p.CameraControls.Supporting;
                bool right=near && freeLook && !p.UI.MenuOpen && p.Right.Valid && !p.HandsUse.RightHolding && !p.ClimbControls.RightHeld && !p.GliderGrips.RightHeld && !p.CameraControls.ReservesRight && !p.SprayControls.HoldingCan && !p.Basketball.ReservesRight && !p.Gesture.Active;
                Hand(b,p.Left,0,left,scale);Hand(b,p.Right,3,right,scale);
            }
        }
        void Find() {
            scan=Time.unscaledTime+4.31f;
            foreach(MeshFilter mf in SceneInventory.Meshes) {
                if(mf==null || mf.name!="MPC" || !mf.gameObject.scene.IsValid() || !mf.gameObject.activeInHierarchy)continue;
                Renderer r=mf.GetComponent<Renderer>();if(r==null || mf.gameObject.layer==5 || mf.gameObject.layer==31)continue;
                string path=Diagnostics.PathOf(mf.transform);if(path.StartsWith("LVL_UI/") || path.StartsWith("FOLLOWCAM/"))continue;
                bool found=false;foreach(Board old in boards)if(old.Source==mf.transform)found=true;if(found)continue;
                // Unbatched meshes can be checked by name; batched boards retain the authored MPC transform.
                if(!r.isPartOfStaticBatch && (mf.sharedMesh==null || mf.sharedMesh.name!="PROP_MPC"))continue;
                Assets();Board b=new Board();b.Source=mf.transform;b.Root=new GameObject("VR Physical MPC Pads");b.Root.layer=31;b.Root.transform.SetParent(mf.transform,false);
                for(int i=0;i<16;i++) {
                    Pad pad=new Pad();b.Pads[i]=pad;
                    GameObject sound=new GameObject("Drum "+i,typeof(AudioSource));sound.transform.SetParent(b.Root.transform,false);sound.transform.localPosition=DrumMath.Center(i);
                    pad.Audio=sound.GetComponent<AudioSource>();pad.Audio.playOnAwake=false;pad.Audio.spatialBlend=.8f;pad.Audio.minDistance=1.5f;pad.Audio.maxDistance=12;pad.Audio.priority=64;pad.Audio.dopplerLevel=0;
                    pad.Light=GameObject.CreatePrimitive(PrimitiveType.Quad);pad.Light.name="Pad touch "+i;pad.Light.layer=31;Object.Destroy(pad.Light.GetComponent<Collider>());
                    pad.Light.transform.SetParent(b.Root.transform,false);pad.Light.transform.localPosition=DrumMath.Center(i)+Vector3.forward*.0015f;
                    pad.Light.transform.localRotation=Quaternion.Euler(0,180,0);pad.Light.transform.localScale=new Vector3(.121f,.121f,1);
                    pad.Light.GetComponent<Renderer>().sharedMaterial=glow;pad.Light.SetActive(false);
                }
                boards.Add(b);BepInEx.Logging.Logger.CreateLogSource("VR MPC").LogInfo("16 physical pads: "+path);
            }
        }
        void Assets() {
            if(clips!=null)return;
            clips=new AudioClip[16];string[] names={"Grimy kick","Dirty snare","Crunch hat","Open hat","Dirty thud","Dusty clap","Rim crack","Grime bell","Low tom","Mid tom","High tom","Shaker","Distorted acid D2","Distorted acid G2","Distorted acid A2","Distorted acid B2"};
            float[][] kit=DrumKit.Load();
            for(int i=0;i<16;i++){float[] samples=kit[i];clips[i]=AudioClip.Create("VR "+names[i],samples.Length,1,44100,false);clips[i].SetData(samples,0);}
            glow=new Material(Shader.Find("Unlit/Color"));glow.color=new Color(.28f,.66f,.35f);
        }
        void Hand(Board b,Hand hand,int offset,bool available,float scale) {
            Vector3 normal=b.Source.TransformDirection(Vector3.forward).normalized;float correction=0;
            for(int finger=0;finger<3;finger++) {
                int slot=offset+finger;
                if(!available){b.Valid[slot]=false;foreach(Pad pad in b.Pads)pad.Latched[slot]=false;continue;}
                Vector3 world=finger<2?hand.FingerTip(finger+1):hand.Visual.transform.TransformPoint(hand.GripLocalPoint);
                Vector3 local=b.Source.InverseTransformPoint(world);
                Vector3 previous=b.Source.InverseTransformPoint(b.Previous[slot]);
                float distance=Vector3.Dot(world-b.Source.TransformPoint(new Vector3(local.x,local.y,DrumMath.Top)),normal)/scale;
                float before=Vector3.Dot(b.Previous[slot]-b.Source.TransformPoint(new Vector3(previous.x,previous.y,DrumMath.Top)),normal)/scale;
                float speed=(before-distance)/Mathf.Max(.001f,Time.unscaledDeltaTime);
                for(int i=0;i<16;i++) {
                    Pad pad=b.Pads[i];Vector3 contact=DrumMath.Contact(previous,local);bool inside=DrumMath.Inside(contact,i);
                    if(b.Valid[slot] && DrumMath.Strike(before,distance,speed,inside,Time.unscaledTime,ref pad.Latched[slot],ref pad.Next)) {
                        strikes++;lastBoard=Diagnostics.PathOf(b.Source);
                        // One voice per synth pad: long notes overlap without piling up on repeated taps.
                        if(i>=12){pad.Audio.Stop();pad.Audio.clip=clips[i];pad.Audio.volume=DrumMath.Gain(speed);pad.Audio.Play();}
                        else pad.Audio.PlayOneShot(clips[i],DrumMath.PadGain(i,speed));pad.Until=Time.unscaledTime+.09f;pad.Light.SetActive(true);VrPlugin.Instance.PulseGrip(hand);
                    }
                    if(inside && distance<.006f && distance>-.045f)correction=Mathf.Max(correction,.006f-distance);
                }
                b.Previous[slot]=world;b.Valid[slot]=true;
            }
            if(available && correction>0)hand.Visual.transform.position+=normal*Mathf.Min(correction,.045f)*scale;
        }
        internal void Clear() {
            foreach(Board b in boards)if(b.Root!=null)Object.Destroy(b.Root);boards.Clear();
            if(clips!=null)foreach(AudioClip clip in clips)if(clip!=null)Object.Destroy(clip);clips=null;
            if(glow!=null)Object.Destroy(glow);glow=null;scan=0;
        }
        internal string Describe(){return "PhysicalMPC boards="+boards.Count+" kit=LoudKick+DistortedAcidDGAB volume=boosted pads=16 strikes="+strikes+" last="+lastBoard;}
    }
}
