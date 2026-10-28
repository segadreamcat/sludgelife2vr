using System;
using UnityEngine;

namespace SludgeLife2VR {
    // Native flat/fist meshes share vertex correspondence in both games.
    // Assign fingers anatomically, keeping shared/welded vertices deterministic.
    internal static class HandPoseMath {
        static readonly float[] baseX = { .145f, .080f, .026f, -.027f, -.080f };
        static readonly float[] baseZ = { .055f, .135f, .145f, .140f, .110f };
        static readonly float[] tipX = { .158f, .080f, .026f, -.027f, -.080f };
        static readonly float[] tipZ = { .158f, .258f, .274f, .268f, .233f };
        internal static void Finger(Vector3 nativeFlat, out int finger, out float weight, out float along) {
            float x = -nativeFlat.x, z = nativeFlat.z, best = float.MaxValue;
            finger = 0; along = 0;
            for (int i = 0; i < 5; i++) {
                float dx = tipX[i]-baseX[i], dz = tipZ[i]-baseZ[i];
                float t = ((x-baseX[i])*dx + (z-baseZ[i])*dz) / (dx*dx+dz*dz);
                float nearest = Clamp(t, 0, 1);
                float ex = x-baseX[i]-nearest*dx, ez = z-baseZ[i]-nearest*dz;
                float distance = ex*ex + ez*ez;
                if (distance < best) { best = distance; finger = i; along = t; }
            }
            weight = Clamp((along+.15f)/.50f, 0, 1);
            weight = weight*weight*(3-2*weight);
            if (best > .0025f || z < .045f) weight = 0;
        }
        internal static float Amount(int finger, float grip, float trigger, float thumb, float holding) {
            float normal = finger == 1 ? trigger : finger == 0 ? thumb*grip : grip;
            // Supporting fingers remain wrapped when the spraying trigger releases.
            float can = finger == 1 ? .20f+.18f*trigger : finger == 0 ? .62f : .72f;
            return Clamp(normal + (can-normal)*holding, 0, 1);
        }
        internal static float Follow(float current, float target, float dt) {
            return current + (target-current) * (1-(float)Math.Exp(-22*Clamp(dt, 0, .1f)));
        }
        internal static Vector3 MiddlePose(Vector3 flat,Vector3 fist,Vector3 wrist,int finger,float weight,float grip) {
            return Vector3.Lerp(flat,fist,(finger==2?grip:1)*weight)-wrist;
        }
        internal static Vector3 Blend(Vector3 relaxed, Vector3 flat, Vector3 fist, Vector3 wrist, int finger,
            float weight, float grip, float trigger, float thumb, float holding) {
            float amount = Amount(finger, grip, trigger, thumb, holding)*weight;
            return Vector3.Lerp(Vector3.Lerp(relaxed, flat, holding), fist, amount)-wrist;
        }
        internal static float TipWeight(float along, float weight) {
            float t = Clamp((along-.05f)/.8f, 0, 1); return t*t*(3-2*t)*weight;
        }
        internal static Vector3 CanIndexTarget(float trigger) {
            // The can sits beside the palm. The index presses vertically on its actuator.
            return new Vector3(-(.034f-.003f*trigger)*2, -.035f*2, (.080f+.055f)*2);
        }
        static Vector3[] frameSource;
        static readonly float[] rootY = new float[5], endY = new float[5];
        static void CanFrame(Vector3[] flat, Vector3 wrist, int[] fingers, float[] lengths) {
            if (frameSource == flat) return;
            frameSource=flat;
            for (int f=0; f<5; f++) {
                float r=0,e=0; int nr=0,ne=0;
                for (int i=0; i<flat.Length; i++) if (fingers[i]==f) {
                    if (lengths[i]>=-.05f && lengths[i]<.15f) {r+=flat[i].y-wrist.y;nr++;}
                    if (lengths[i]>.75f) {e+=flat[i].y-wrist.y;ne++;}
                }
                rootY[f]=nr>0?r/nr:0;endY[f]=ne>0?e/ne:0;
            }
        }
        internal static void SeatCan(Vector3[] posed, Vector3[] flat, Vector3 wrist, int[] fingers, float[] lengths, float[] weights,
            int[] triangles, int indexFace, float trigger, float holding) {
            SeatItem(posed,flat,wrist,fingers,lengths,weights,triangles,indexFace,CanIndexTarget(trigger),holding,false);
        }
        internal static void SeatCamera(Vector3[] posed, Vector3[] flat, Vector3 wrist, int[] fingers, float[] lengths, float[] weights,
            int[] triangles, int indexFace, float trigger, float holding) {
            Vector3 p=CameraFit.ShutterHand(trigger);
            Vector3 target=new Vector3(-p.y*2,p.x*2,(p.z+.055f)*2);
            SeatItem(posed,flat,wrist,fingers,lengths,weights,triangles,indexFace,target,holding,true);
        }
        static void SeatItem(Vector3[] posed,Vector3[] flat,Vector3 wrist,int[] fingers,float[] lengths,float[] weights,
            int[] triangles,int indexFace,Vector3 target,float holding,bool indexOnly) {
            CanFrame(flat,wrist,fingers,lengths);
            int a = triangles[indexFace*3], b = triangles[indexFace*3+1], c = triangles[indexFace*3+2];
            Vector3 nail = (flat[a]+flat[b]+flat[c])/3-wrist;
            float rootZ = baseZ[1]-wrist.z, rootX = -baseX[1]-wrist.x;
            float length = nail.z-rootZ;
            // Two links preserve the index's length instead of stretching its tip onto the can.
            float l1 = length*.55f, l2 = length*.45f;
            Vector3 root = new Vector3(rootX,rootY[1],rootZ), end=target, joint=Vector3.zero;
            float nailOffset=nail.y-endY[1];
            for (int iteration=0; iteration<4; iteration++) {
                float dy=end.y-root.y,dz=end.z-root.z, actual=(float)Math.Sqrt(dy*dy+dz*dz);
                float d=Clamp(actual,.001f,(l1+l2)*.995f), uy=dy/Math.Max(.001f,actual), uz=dz/Math.Max(.001f,actual);
                float projection=(l1*l1-l2*l2+d*d)/(2*d);
                float height=(float)Math.Sqrt(Math.Max(0,l1*l1-projection*projection));
                joint=root+new Vector3((end.x-root.x)*.55f,uy*projection+uz*height,uz*projection-uy*height);
                Vector3 direction=(end-joint).normalized;
                if (iteration<3) end=target-new Vector3(nail.x-rootX,nailOffset*direction.z,-nailOffset*direction.y);
            }
            for (int i = 0; i < posed.Length; i++) {
                if (weights[i] <= 0) continue;
                if(indexOnly && fingers[i]!=1)continue;
                Vector3 v = flat[i]-wrist, fit = v;
                int f = fingers[i];
                if (f == 1) {
                    float t = (v.z-rootZ)/Math.Max(.001f,length);
                    Vector3 direction;
                    if (t < .55f) { fit = root+(joint-root)*(t/.55f); direction=(joint-root).normalized; }
                    else { fit=joint+(end-joint)*((t-.55f)/.45f); direction=(end-joint).normalized; }
                    fit.x += v.x-rootX;
                    float thickness=v.y-(rootY[1]+(endY[1]-rootY[1])*t);
                    fit.y += thickness*direction.z; fit.z -= thickness*direction.y;
                } else if (f >= 2) {
                    // Wrap the three supporting fingers around the actual cylindrical can.
                    float z0=baseZ[f]-wrist.z, cy=-.070f, cz=.270f;
                    float y0=rootY[f], radial=(float)Math.Sqrt((y0-cy)*(y0-cy)+(z0-cz)*(z0-cz));
                    float theta0=(float)Math.Atan2(y0-cy,z0-cz);
                    float travel=Math.Max(0,v.z-z0), radius=.078f;
                    float blend=Clamp(travel/.035f,0,1);
                    float r=radial+(radius-radial)*blend;
                    float theta=theta0-travel/radius;
                    float thickness=v.y-(rootY[f]+(endY[f]-rootY[f])*Clamp(lengths[i],0,1));
                    fit.y=cy+(r+thickness)*(float)Math.Sin(theta);
                    fit.z=cz+(r+thickness)*(float)Math.Cos(theta);
                } else {
                    // Thumb opposes the three fingers on the back of the can.
                    fit = Vector3.Lerp(v,posed[i],.7f);
                }
                float seat=holding*weights[i];
                posed[i]=Vector3.Lerp(posed[i],fit,seat);
            }
        }
        internal static int[] NailFaces(Vector3[] flat, int[] triangles, int[] fingers, float[] lengths) {
            int[] result = new int[10];
            for (int finger = 0; finger < 5; finger++) {
                int first = -1, second = -1; float best = float.MaxValue, next = float.MaxValue;
                for (int i = 0; i < triangles.Length; i += 3) {
                    int a = triangles[i], b = triangles[i+1], c = triangles[i+2];
                    if (fingers[a] != finger || fingers[b] != finger || fingers[c] != finger) continue;
                    Vector3 normal = Vector3.Cross(flat[b]-flat[a],flat[c]-flat[a]);
                    if (normal.y <= 0 || normal.y*normal.y < normal.sqrMagnitude*.5f) continue;
                    float along = (lengths[a]+lengths[b]+lengths[c])/3;
                    if (along < .70f || along > 1.1f) continue;
                    Vector3 center = (flat[a]+flat[b]+flat[c])/3;
                    float dx = -center.x-tipX[finger];
                    float dz = center.z-(tipZ[finger]-.018f);
                    float score = dx*dx+dz*dz;
                    if (score < best) { second = first; next = best; first = i/3; best = score; }
                    else if (score < next) { second = i/3; next = score; }
                }
                if (first < 0 || second < 0) throw new InvalidOperationException("Native fingertip topology was not recognized.");
                result[finger*2] = first; result[finger*2+1] = second;
            }
            return result;
        }
        internal static float Clamp(float x, float min, float max) { return x < min ? min : x > max ? max : x; }
    }
}
