using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Valve.VR;
namespace SludgeLife2VR {
    internal sealed class FrameMonitor {
        readonly double[] total=new double[7],peak=new double[7];
        readonly string[] labels={"cameraUI","tools","grabClimb","handsMPC","push","bodyTouch","cameraButtons"};
        long begin,last;int frames;double frameTotal,framePeak,lateTotal,latePeak;float next,dt;
        string summary="VR performance: collecting frame timings";
        static readonly BepInEx.Logging.ManualLogSource log=BepInEx.Logging.Logger.CreateLogSource("VR Performance");
        internal void Begin(){begin=last=Stopwatch.GetTimestamp();dt=Time.unscaledDeltaTime;}
        internal void Mark(int section){long now=Stopwatch.GetTimestamp();double ms=(now-last)*1000.0/Stopwatch.Frequency;last=now;total[section]+=ms;peak[section]=Math.Max(peak[section],ms);}
        internal void End(){
            frames++;frameTotal+=dt*1000;framePeak=Math.Max(framePeak,dt*1000);
            double ms=(Stopwatch.GetTimestamp()-begin)*1000.0/Stopwatch.Frequency;lateTotal+=ms;latePeak=Math.Max(latePeak,ms);
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+15;
            summary="VR performance frame avg/max="+(frameTotal/frames).ToString("F2")+"/"+framePeak.ToString("F2")+"ms modLate="+(lateTotal/frames).ToString("F2")+"/"+latePeak.ToString("F2")+"ms";
            for(int i=0;i<labels.Length;i++)summary+=" "+labels[i]+"="+(total[i]/frames).ToString("F2")+"/"+peak[i].ToString("F2");
            try{
                CVRCompositor compositor=OpenVR.Compositor;Compositor_FrameTiming timing=new Compositor_FrameTiming();timing.m_nSize=(uint)Marshal.SizeOf(typeof(Compositor_FrameTiming));
                if(compositor!=null && compositor.GetFrameTiming(ref timing,0))summary+=" gpu="+timing.m_flTotalRenderGpuMs.ToString("F2")+"ms compositorInterval="+timing.m_flClientFrameIntervalMs.ToString("F2")+"ms frameDropped="+timing.m_nNumDroppedFrames;
            }catch(Exception){summary+=" compositorTiming=unavailable";}
            log.LogInfo(summary);frames=0;frameTotal=framePeak=lateTotal=latePeak=0;Array.Clear(total,0,total.Length);Array.Clear(peak,0,peak.Length);
        }
        internal string Describe(){return summary;}
        internal void Clear(){next=Time.unscaledTime+15;frames=0;frameTotal=framePeak=lateTotal=latePeak=0;Array.Clear(total,0,total.Length);Array.Clear(peak,0,peak.Length);summary="VR performance: collecting frame timings";}
    }
}
