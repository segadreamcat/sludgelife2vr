using System;
using System.IO;
using System.Reflection;
namespace SludgeLife2VR {
    internal static class DrumKit {
        internal static float[][] Read(Stream stream){
            if(stream==null)throw new IOException("Missing baked drum kit");
            using(BinaryReader reader=new BinaryReader(stream)){
                if(reader.ReadUInt32()!=0x314D5244 || reader.ReadInt32()!=44100 || reader.ReadInt32()!=16)throw new IOException("Unexpected drum kit format");
                float[][] kit=new float[16][];
                for(int pad=0;pad<16;pad++){
                    int count=reader.ReadInt32();if(count<441 || count>(pad>=12?176400:88200))throw new IOException("Invalid drum sample length");
                    kit[pad]=new float[count];for(int i=0;i<count;i++)kit[pad][i]=reader.ReadInt16()/32768f;
                }
                if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new IOException("Unexpected drum kit data");return kit;
            }
        }
        internal static float[][] Load(){return Read(Assembly.GetExecutingAssembly().GetManifestResourceStream("VRDrumKit"));}
    }
}
