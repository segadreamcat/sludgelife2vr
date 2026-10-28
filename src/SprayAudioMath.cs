using System;
namespace SludgeLife2VR {
    internal static class SprayAudioMath {
        internal static float[] Ball(int rate){
            float[] data=new float[(int)(rate*.105)];uint seed=7319;
            for(int i=0;i<data.Length;i++){
                double t=(double)i/rate;seed=seed*1664525u+1013904223u;double noise=(seed>>8)/8388607.5-1;
                double ring=(Math.Sin(2*Math.PI*2731*t)+.6*Math.Sin(2*Math.PI*4117*t)+.3*Math.Sin(2*Math.PI*1381*t))*Math.Exp(-t*72);
                double knock=noise*Math.Exp(-t*290);data[i]=(float)((.29*ring+.35*knock)*Math.Min(1,i/5.0)*Math.Min(1,(data.Length-1-i)/40.0));
            }return data;
        }
        internal static float[] Hiss(int rate){
            // Periodic filtering makes the generated noise seamless when looped.
            float[] raw=new float[rate],data=new float[rate];uint seed=91317;
            for(int i=0;i<raw.Length;i++){seed=seed*1664525u+1013904223u;raw[i]=(float)((seed>>8)/8388607.5-1);}
            for(int i=0;i<data.Length;i++)data[i]=.28f*raw[i]+.13f*raw[(i+1)%raw.Length]-.17f*raw[(i+raw.Length-1)%raw.Length];
            return data;
        }
    }
}
