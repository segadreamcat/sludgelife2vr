using HutongGames.PlayMaker;
using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class GliderHands {
        sealed class Handle {
            internal SkinnedMeshRenderer Skin;internal Transform Bone;internal Matrix4x4 Bind;internal Vector3 Center,Axis;internal float Radius;
            internal Vector3 Point,Direction;internal float WorldRadius;internal Quaternion Rotation;
            internal bool Frame(){
                if(Skin==null || Bone==null || !Skin.enabled || !Skin.gameObject.activeInHierarchy)return false;
                Matrix4x4 frame=Bone.localToWorldMatrix*Bind;
                GliderGripMath.Frame(frame,Center,Axis,Radius,out Point,out Direction,out WorldRadius);
                Vector3 up=Axis==Vector3.forward?Vector3.up:Vector3.right;
                Rotation=Direction.sqrMagnitude>.9f?Quaternion.LookRotation(Direction,frame.MultiplyVector(up).normalized):Quaternion.identity;
                return Direction.sqrMagnitude>.9f && WorldRadius>.002f;
            }
        }
        PlayMakerFSM native;Handle first,second,left,right;float scan;Quaternion leftRotation,rightRotation;
        internal bool LeftHeld,RightHeld;
        static Handle Find(Transform root,string name){
            Transform t=root.Find(name);SkinnedMeshRenderer skin=t==null?null:t.GetComponent<SkinnedMeshRenderer>();
            if(skin==null || skin.sharedMesh==null || !skin.sharedMesh.isReadable)return null;
            Mesh mesh=skin.sharedMesh;BoneWeight[] weights=mesh.boneWeights;if(weights.Length==0)return null;int bone=weights[0].boneIndex0;
            foreach(BoneWeight w in weights)if(w.boneIndex0!=bone || w.weight0<.999f)return null;
            if(bone>=skin.bones.Length || bone>=mesh.bindposes.Length)return null;
            Vector3 center,axis;float radius;if(!GliderGripMath.Cylinder(mesh.vertices,out center,out axis,out radius))return null;
            return new Handle{Skin=skin,Bone=skin.bones[bone],Bind=mesh.bindposes[bone],Center=center,Axis=axis,Radius=radius};
        }
        void Scan(){
            if(Time.unscaledTime<scan)return;scan=Time.unscaledTime+2;
            if(native!=null && first!=null && second!=null)return;
            Transform root=null;
            foreach(PlayMakerFSM f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())if(f.gameObject.scene.IsValid()){
                if(f.name=="PLAYER" && f.FsmName=="GLIDER")native=f;
                // FOLLOW_TARGET reparents ITEM_GLIDER away from PLAYER during native setup.
                if(f.name=="ITEM_GLIDER" && f.FsmName=="FOLLOW_TARGET")root=f.transform.Find("GLIDER");
            }
            if(root==null && native!=null)root=native.transform.Find("ITEM_GLIDER/GLIDER");
            if(root!=null){first=Find(root,"Cylinder_004");second=Find(root,"Cylinder_005");}
        }
        internal void Update(float scale){
            bool wasLeft=LeftHeld,wasRight=RightHeld;LeftHeld=RightHeld=false;
            VrPlugin p=VrPlugin.Instance;
            if(!VrPlugin.Active || !p.MotionInput.Value || p.UI.MenuOpen){left=right=null;return;}
            Scan();FsmBool flag=native==null?null:native.FsmVariables.FindFsmBool("GLIDIN");
            if(flag==null || !flag.Value || native.ActiveStateName!="OPEN" || first==null || second==null || !first.Frame() || !second.Frame()) {left=right=null;return;}
            if(left==null || right==null){bool aLeft=Vector3.Dot(first.Point-second.Point,p.HeadRotation*Vector3.right)<0;left=aLeft?first:second;right=aLeft?second:first;}
            LeftHeld=p.Left.Valid && !p.HandsUse.LeftHolding && !p.ClimbControls.LeftHeld && !p.CameraControls.ReservesRight && (left.Point-p.HeadPosition).sqrMagnitude<1.6f*1.6f*scale*scale;
            RightHeld=p.Right.Valid && !p.HandsUse.RightHolding && !p.ClimbControls.RightHeld && !p.CameraControls.ReservesRight && !p.SprayControls.HoldingCan && !p.Basketball.ReservesRight && !p.Gesture.Active && (right.Point-p.HeadPosition).sqrMagnitude<1.6f*1.6f*scale*scale;
            if(LeftHeld){p.Left.PoseHandle(left.WorldRadius/scale);Seat(p.Left,left,scale,!wasLeft,ref leftRotation);if(!wasLeft)p.PulseHand(p.Left,500);}
            if(RightHeld){p.Right.PoseHandle(right.WorldRadius/scale);Seat(p.Right,right,scale,!wasRight,ref rightRotation);if(!wasRight)p.PulseHand(p.Right,500);}
        }
        static void Seat(Hand hand,Handle handle,float scale,bool attach,ref Quaternion local){
            if(attach){
                Vector3 axis=handle.Direction,current=hand.Visual.transform.rotation*Vector3.down;if(Vector3.Dot(current,axis)<0)axis=-axis;
                Quaternion grip=Quaternion.FromToRotation(current,axis)*hand.Visual.transform.rotation;
                local=Quaternion.Inverse(handle.Rotation)*grip;
            }
            // Keep the wrist fixed relative to the animated handle until this grip releases.
            Quaternion rotation=handle.Rotation*local;
            hand.Visual.transform.rotation=rotation;hand.Visual.transform.position=handle.Point-rotation*hand.HandleGripPoint*scale;
        }
        internal void Clear(){native=null;first=second=left=right=null;LeftHeld=RightHeld=false;scan=0;}
        internal string Describe(){return "GliderHands left="+LeftHeld+" right="+RightHeld+" anchors="+(first!=null && second!=null)+" pose=NativeHoldBar";}
    }
}
