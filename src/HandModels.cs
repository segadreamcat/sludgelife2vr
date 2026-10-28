using System.Collections.Generic;
using UnityEngine;

namespace SludgeLife2VR {
    internal sealed class TrackedHandModel {
        internal static Material Skin, Polish;
        static Material glove;
        static Vector3[] flat, relaxed, fist,bar;
        static Vector3 barCenter;static bool barFailed;
        static int[] triangles, fingers;
        static float[] weights, lengths;
        static Vector3 wrist;
        static float nextSearch;
        static Material[] appearance;
        internal static bool Ready { get { return flat != null; } }
        static readonly HashSet<int> nailFaces = new HashSet<int>();
        static int indexFace;
        static readonly int[] fingerFaces=new int[5];
        GameObject model;
        MeshFilter filter;
        Mesh mesh;
        HandDecorations decorations;
        Vector3[] posed;
        readonly bool left;
        float grip, trigger, thumb, held, gesture, camera;
        internal bool MiddleGesture, CameraPose,HandlePose;internal float HandleRadius;
        internal Vector3 HandleGripPoint {get{Vector3 p=bar==null?Vector3.zero:barCenter;if(left)p.x=-p.x;return model.transform.localPosition+model.transform.localRotation*Vector3.Scale(p,model.transform.localScale);}}
        static void LoadBar(){
            if(bar!=null || barFailed)return;
            try{using(System.IO.Stream stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("VRGliderPose"))using(System.IO.BinaryReader reader=new System.IO.BinaryReader(stream)){
                int n=reader.ReadInt32();if(n!=422)throw new System.IO.IOException("Unexpected glider hand topology.");barCenter=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());bar=new Vector3[n];for(int i=0;i<n;i++)bar[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
            }}catch(System.Exception e){barFailed=true;BepInEx.Logging.Logger.CreateLogSource("VR Glider Hands").LogError("Native bar pose: "+e.Message);}
        }
        internal Vector3 FingerTip(int finger) {
            if(posed==null || triangles==null)return model.transform.position;
            int face=fingerFaces[finger];
            int a=triangles[face*3],b=triangles[face*3+1],c=triangles[face*3+2];
            return model.transform.TransformPoint((posed[a]+posed[b]+posed[c])/3);
        }
        internal Vector3 IndexTip {
            get {
                if (posed == null || triangles == null) return model.transform.position;
                int a = triangles[indexFace*3], b = triangles[indexFace*3+1], c = triangles[indexFace*3+2];
                return model.transform.TransformPoint((posed[a]+posed[b]+posed[c])/3+Vector3.forward*.015f);
            }
        }

        internal TrackedHandModel(Transform parent, bool isLeft, int layer) {
            left = isLeft;
            model = new GameObject("Native Articulated Glove", typeof(MeshFilter), typeof(MeshRenderer));
            model.layer = layer; model.transform.SetParent(parent, false);
            model.transform.localPosition = new Vector3(0, 0, -.055f);
            // Blue Prince's hand frame: fingers forward, thumbs up, mirrored wrists.
            model.transform.localRotation = Quaternion.AngleAxis(left ? 90 : -90, Vector3.forward);
            model.transform.localScale = Vector3.one * .5f;
            filter = model.GetComponent<MeshFilter>(); model.SetActive(false);
        }
        internal bool Update(bool close) { return Update(close ? 1 : 0, close ? 1 : 0, 1, false); }
        internal bool Update(float gripAmount, float triggerAmount, float thumbAmount, bool holdingCan) {
            if (!EnsureAssets()) return false;
            if (mesh == null) Build();
            float dt = Time.unscaledDeltaTime;
            grip = HandPoseMath.Follow(grip, gripAmount, dt);
            trigger = HandPoseMath.Follow(trigger, triggerAmount, dt);
            thumb = HandPoseMath.Follow(thumb, thumbAmount, dt);
            held = HandPoseMath.Follow(held, holdingCan ? 1 : 0, dt);
            gesture=HandPoseMath.Follow(gesture,MiddleGesture?1:0,dt);
            camera=HandPoseMath.Follow(camera,CameraPose?1:0,dt);
            for (int i = 0; i < posed.Length; i++) {
                posed[i] = HandPoseMath.Blend(relaxed[i], flat[i], fist[i], wrist, fingers[i], weights[i], grip, trigger, thumb, held);
            }
            if(gesture>.001f)for(int i=0;i<posed.Length;i++) {
                // Grip is the middle finger sensor. Other fingers stay curled during this gesture.
                Vector3 target=HandPoseMath.MiddlePose(flat[i],fist[i],wrist,fingers[i],weights[i],grip);
                posed[i]=Vector3.Lerp(posed[i],target,gesture);
            }
            if (!left && held > .001f) {
                HandPoseMath.SeatCan(posed, flat, wrist, fingers, lengths, weights, triangles, indexFace, trigger, held);
            }
            if(!left && !holdingCan && camera>.001f)HandPoseMath.SeatCamera(posed,flat,wrist,fingers,lengths,weights,triangles,indexFace,trigger,camera);
            if(HandlePose){LoadBar();if(bar!=null)for(int i=0;i<posed.Length;i++)posed[i]=GliderGripMath.Wrap(bar[i],barCenter,HandleRadius*2,weights[i]);}
            if (left) for (int i = 0; i < posed.Length; i++) posed[i].x = -posed[i].x;
            mesh.vertices = posed; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if(decorations!=null)decorations.Update(posed);
            model.GetComponent<MeshRenderer>().sharedMaterials = appearance;
            model.SetActive(true); return true;
        }
        internal static bool EnsureAssets() {
            if (flat != null) return true;
            if (Time.unscaledTime < nextSearch) return false;
            nextSearch = Time.unscaledTime + 1;
            Mesh open = null, natural = null, closed = null;
            foreach (Mesh source in Resources.FindObjectsOfTypeAll<Mesh>()) {
                if (source.name == "MESH_HAND_00_FLAT") open = source;
                else if (source.name == "MESH_HAND_01_NATURAL") natural = source;
                else if (source.name == "MESH_HAND_18_FIST1") closed = source;
            }
            if (open == null || natural == null || closed == null || !open.isReadable || !natural.isReadable || !closed.isReadable) return false;
            if (open.vertexCount != 422 || closed.vertexCount != 422 || natural.vertexCount != 422) return false;
            Shader shader = Shader.Find("Unlit/Color"); if (shader == null) shader = Shader.Find("Sprites/Default"); if (shader == null) return false;
            Vector3[] openVertices = open.vertices, naturalVertices = natural.vertices, closedVertices = closed.vertices;
            Vector3 naturalWrist = Vector3.zero, closedWrist = Vector3.zero; wrist = Vector3.zero; int count = 0;
            for (int i = 0; i < openVertices.Length; i++) if (openVertices[i].z < -.065f) {
                wrist += openVertices[i]; naturalWrist += naturalVertices[i]; closedWrist += closedVertices[i]; count++;
            }
            if (count == 0) return false;
            wrist /= count; naturalWrist /= count; closedWrist /= count;
            flat = openVertices; relaxed = new Vector3[flat.Length]; fist = new Vector3[flat.Length];
            triangles = open.triangles; fingers = new int[flat.Length]; weights = new float[flat.Length]; lengths = new float[flat.Length];
            for (int i = 0; i < flat.Length; i++) {
                HandPoseMath.Finger(flat[i], out fingers[i], out weights[i], out lengths[i]);
                relaxed[i] = Vector3.Lerp(flat[i], naturalVertices[i]-naturalWrist+wrist, weights[i]*.65f);
                fist[i] = closedVertices[i]-closedWrist+wrist;
            }
            int[] nails = HandPoseMath.NailFaces(flat, triangles, fingers, lengths);
            for(int f=0;f<5;f++)fingerFaces[f]=nails[f*2];
            nailFaces.Clear(); foreach (int face in nails) nailFaces.Add(face); indexFace = nails[2];
            Skin = new Material(shader); Skin.name = "VR Sludge Green Skin"; Skin.color = new Color(.25f,.50f,.30f);
            glove = new Material(shader); glove.name = "VR Black Fingerless Gloves"; glove.color = new Color(.065f,.065f,.08f);
            Polish = new Material(shader); Polish.name = "VR Black Nail Polish"; Polish.color = new Color(.018f,.02f,.023f);
            appearance = new Material[] { glove, Skin, Polish };
            BepInEx.Logging.Logger.CreateLogSource("VR Hands").LogInfo("Native green fingers / black fingerless gloves. Independent grip, index and thumb; Blue Prince wrist angles.");
            return true;
        }
        void Build() {
            mesh = new Mesh(); mesh.name = "VR Articulated Native Hand"; mesh.MarkDynamic(); posed = new Vector3[flat.Length];
            List<int>[] parts = { new List<int>(), new List<int>(), new List<int>() };
            Color[] colors = new Color[flat.Length];
            for (int i = 0; i < flat.Length; i++) colors[i] = Color.white;
            for (int i = 0; i < triangles.Length; i += 3) {
                int a = triangles[i], b = triangles[i+1], c = triangles[i+2];
                float exposed = (lengths[a]*weights[a]+lengths[b]*weights[b]+lengths[c]*weights[c])/3;
                int part = nailFaces.Contains(i/3) ? 2 : exposed > .45f ? 1 : 0;
                parts[part].Add(a); parts[part].Add(left ? c : b); parts[part].Add(left ? b : c);
            }
            mesh.vertices = flat; mesh.colors = colors; mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(parts[i].ToArray(), i);
            filter.sharedMesh = mesh;
            try { decorations=new HandDecorations(model.transform,flat,triangles,fingers,left); }
            catch(System.Exception e) {BepInEx.Logging.Logger.CreateLogSource("VR Hands").LogWarning("Hand details unavailable: "+e.Message);}
        }
        internal void Clear() { if(decorations!=null)decorations.Clear();decorations=null; if (mesh != null) Object.Destroy(mesh); mesh = null; posed = null; }
        internal static void ClearAssets() {
            if (Skin != null) Object.Destroy(Skin); if (glove != null) Object.Destroy(glove); if (Polish != null) Object.Destroy(Polish);
            flat = relaxed = fist = bar = null;barFailed=false; fingers = triangles = null; weights = lengths = null;
            Skin = Polish = glove = null; appearance = null; nextSearch = 0;
        }
    }
}
