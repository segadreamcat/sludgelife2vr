using UnityEngine;
namespace SludgeLife2VR {
    // Share the expensive Unity object enumeration; retain inactive entries for later activation.
    internal static class SceneInventory {
        static PlayMakerFSM[] fsms;static Renderer[] renderers;static MeshFilter[] meshes;
        static float next;static int phase;
        internal static PlayMakerFSM[] Fsms {get{if(fsms==null)fsms=Resources.FindObjectsOfTypeAll<PlayMakerFSM>();return fsms;}}
        internal static Renderer[] Renderers {get{if(renderers==null)renderers=Resources.FindObjectsOfTypeAll<Renderer>();return renderers;}}
        internal static MeshFilter[] Meshes {get{if(meshes==null)meshes=Resources.FindObjectsOfTypeAll<MeshFilter>();return meshes;}}
        internal static void Tick(){
            if(Time.unscaledTime<next)return;
            if(phase==0 && fsms!=null)fsms=Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
            if(phase==1 && renderers!=null)renderers=Resources.FindObjectsOfTypeAll<Renderer>();
            if(phase==2 && meshes!=null)meshes=Resources.FindObjectsOfTypeAll<MeshFilter>();
            phase=(phase+1)%3;next=Time.unscaledTime+10;
        }
        internal static void Clear(){fsms=null;renderers=null;meshes=null;phase=0;next=Time.unscaledTime+10;}
    }
}
