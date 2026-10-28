using UnityEngine;
namespace SludgeLife2VR {
    internal sealed class SprayRattleSound {
        AudioSource sound;AudioClip clip;int impacts;
        internal void Update(bool held,float dt){if(!held && sound!=null)sound.Stop();}
        internal void Impact(float speed) {
            if(VrPlugin.Instance.Right.Visual==null)return;
            if(sound==null){
                GameObject root=new GameObject("VR Spray Can Mixing Ball",typeof(AudioSource));
                root.transform.SetParent(VrPlugin.Instance.Right.Visual.transform,false);root.transform.localPosition=new Vector3(-.005f,-.060f,.040f);
                sound=root.GetComponent<AudioSource>();sound.playOnAwake=false;sound.loop=false;sound.dopplerLevel=0;
                sound.spatialBlend=.8f;sound.minDistance=.8f;sound.maxDistance=8;sound.priority=80;
                float[] samples=SprayAudioMath.Ball(44100);clip=AudioClip.Create("VR Metallic Mixing Ball",samples.Length,1,44100,false);clip.SetData(samples,0);
            }
            impacts++;sound.pitch=Random.Range(.89f,1.12f);sound.PlayOneShot(clip,Mathf.Clamp(.35f+speed*.18f,.4f,.9f));
        }
        internal void Clear(){if(sound!=null)Object.Destroy(sound.gameObject);if(clip!=null)Object.Destroy(clip);sound=null;clip=null;impacts=0;}
        internal string Describe(){return "rattleAudio="+(clip==null?"pending":clip.name)+" rattling="+(sound!=null && sound.isPlaying)+" impacts="+impacts;}
    }
}
