using System;
using UnityEngine;
namespace SludgeLife2VR {
    internal static class DrumMath {
        // Exact local top faces of PROP_MPC, shared by both games. +Z is pad normal.
        internal const float Top=.163971f, Half=.06518f, Step=.156466f;
        internal const float NativeXOffset=0f;
        internal static Vector3 Center(int pad){return Center(pad,NativeXOffset);}
        internal static Vector3 Center(int pad,float offset){return new Vector3(-.378202f+(pad%4)*Step+offset,-.371296f+(pad/4)*Step,Top);}
        internal static bool Inside(Vector3 local,int pad){Vector3 c=Center(pad);return Math.Abs(local.x-c.x)<=Half && Math.Abs(local.y-c.y)<=Half;}
        internal static Vector3 Contact(Vector3 previous,Vector3 current) {
            float delta=previous.z-current.z;
            if(delta>0 && previous.z>Top && current.z<Top)return Vector3.Lerp(previous,current,(previous.z-Top)/delta);
            return current;
        }
        internal static bool Strike(float previous,float current,float speed,bool inside,float now,ref bool latched,ref float next) {
            if(current>.020f)latched=false;
            if(latched || !inside || now<next || previous<-.015f || current>.008f || current<-.12f || previous-current>.18f || previous<=current || speed<.12f)return false;
            latched=true;next=now+.065f;return true;
        }
        internal static float Gain(float speed){return .70f+.30f*(float)Math.Sqrt(Math.Min(1,Math.Max(0,speed)/3));}
        internal static float PadGain(int pad,float speed){return Gain(speed)*(pad==0 || pad==4?1.7f:1);}
        // +Y is the top of the authored MPC: pads 12..15 run left to right below the knobs.
        internal static double NoteFrequency(int pad){return pad==12?73.416192:pad==13?97.998859:pad==14?110.0:pad==15?123.470825:0;}
        // Offline sample synthesis: saturated low end, dirty drum-machine noise and reduced-rate grit.
        // The game loads the resulting PCM resource once instead of synthesizing audio during play.
        internal static float[] Sample(int kind,int rate) {
            float seconds=kind>=12?2.8f:kind==0?.28f:kind==4?.32f:kind==3?.7f:.5f;
            float[] data=new float[(int)(seconds*rate)];uint noise=2371u+(uint)kind*3929u;
            double phase=0,prior=0,filtered=0,heldSample=0,dcIn=0,dcOut=0,peak=0,acidIc1=0,acidIc2=0;
            int crush=kind>=12?4:kind==2 || kind==3 || kind==11?5:kind==5?6:4;
            for(int i=0;i<data.Length;i++) {
                double t=(double)i/rate;noise=noise*1664525u+1013904223u;double white=(noise>>8)/8388607.5-1;
                double high=white-prior;prior=white;filtered=.53*filtered+.47*white;double value,drive;
                if(kind>=12){
                    double hz=NoteFrequency(kind);phase+=2*Math.PI*hz/rate;
                    double cycle=phase/(2*Math.PI),saw=2*(cycle-Math.Floor(cycle+.5));
                    double square=Math.Sin(phase)>0?1:-1;
                    double envelope=(1-Math.Exp(-t*180))*(t<1.05?1:Math.Exp(-(t-1.05)*1.2));
                    // A driven single-oscillator acid bass, with a resonant envelope closing into the low register.
                    double raw=Math.Tanh((.84*saw+.16*square)*1.8)*envelope;
                    double cutoff=70+hz*.75+2200*Math.Exp(-t*6.3);
                    double g=Math.Tan(Math.PI*cutoff/rate),resonance=.38;
                    double v1=(acidIc1+g*(raw-acidIc2))/(1+g*(g+resonance));
                    double v2=acidIc2+g*v1;acidIc1=2*v1-acidIc1;acidIc2=2*v2-acidIc2;
                    value=v2+.30*Math.Sin(phase)*envelope;drive=7.0;
                }else if(kind==0 || kind==4){
                    // A short drum-head thud and noisy beater, without the old long pitched 808 tail.
                    bool heavy=kind==4;double hz=(heavy?58:67)+14*Math.Exp(-t*190);phase+=2*Math.PI*hz/rate;
                    double head=.86*Math.Sin(phase)*Math.Exp(-t*11)+.28*Math.Sin(2*Math.PI*(heavy?98:113)*t+.32)*Math.Exp(-t*49);
                    double beater=(.48*filtered+.18*high)*Math.Exp(-t*125);
                    value=head+beater;drive=heavy?5.2:4.3;
                }else if(kind==1){
                    double body=Math.Sin(2*Math.PI*187*t)+.32*Math.Sin(2*Math.PI*317*t);
                    double buzz=(.62*white+.30*high-.12*filtered)*Math.Exp(-t*19);
                    double slap=t>.018?.16*white*Math.Exp(-(t-.018)*48):0;
                    value=(.46*body*Math.Exp(-t*27)+buzz+slap)*(Math.Sin(2*Math.PI*113*t)>.15?1:.72);drive=6.5;
                }else if(kind==2 || kind==3){
                    double metal=(Math.Sin(2*Math.PI*3049*t)>0?1:-1)+(Math.Sin(2*Math.PI*4211*t)>0?1:-1)+(Math.Sin(2*Math.PI*5879*t)>0?1:-1);
                    value=(.57*high+.13*metal-.10*filtered)*Math.Exp(-t*(kind==2?62:10));drive=4.2;
                }else if(kind==5){
                    double burst=Math.Exp(-t*110)+(t>.011?Math.Exp(-(t-.011)*100):0)+(t>.024?Math.Exp(-(t-.024)*23):0);
                    value=(.55*white+.25*high)*burst;drive=6;
                }else if(kind==6){value=(.65*Math.Sin(2*Math.PI*1730*t)+.35*Math.Sin(2*Math.PI*760*t)) *Math.Exp(-t*68)+.12*high*Math.Exp(-t*180);drive=4;}
                else if(kind==7){value=((Math.Sin(2*Math.PI*540*t)>0?1:-1)+(Math.Sin(2*Math.PI*801*t)>0?1:-1))*.36*Math.Exp(-t*16);drive=4;}
                else if(kind>=8 && kind<=10){double hz=kind==8?89:kind==9?124:168;phase+=2*Math.PI*(hz+63*Math.Exp(-t*38))/rate;value=(Math.Sin(phase)+.19*Math.Sin(2*phase)+.13*white)*Math.Exp(-t*14);drive=4.2;}
                else{value=(.7*high+.15*white)*Math.Exp(-t*39)*(.65+.35*Math.Sin(2*Math.PI*43*t));drive=3.2;}
                // Quantization and sample hold are baked into the clip, keeping runtime cost unchanged.
                if(i%crush==0){double bias=kind>=12?.32:.16,steps=kind>=12?64:256;
                    heldSample=Math.Round((Math.Tanh(value*drive+bias)-Math.Tanh(bias))*steps)/steps;
                }
                double shaped=heldSample*((kind==0 || kind==4)?Math.Exp(-t*(kind==0?24:20)):1);
                if(kind>=12 && t>1.05)shaped*=Math.Exp(-(t-1.05)*1.7);
                double cleaned=shaped-dcIn+.9995*dcOut;dcIn=shaped;dcOut=cleaned;
                cleaned*=Math.Min(1,i/16.0)*Math.Min(1,(data.Length-1-i)/(rate*.012));data[i]=(float)cleaned;peak=Math.Max(peak,Math.Abs(cleaned));
            }
            if(peak>.0001)for(int i=0;i<data.Length;i++)data[i]=(float)(data[i]/peak*.94);
            return data;
        }
    }
}
