using UnityEngine;
using System.Collections.Generic;

namespace SludgeLife2VR {
    internal sealed class NativeSprayCan {
        internal Transform Nozzle;
        GameObject visual;
        Material[] materials;
        ParticleSystem mist;
        Material mistMaterial;
        AudioSource sound;AudioClip sprayClip;
        Mesh suppliedMesh;
        Vector3[] restVertices, pressedVertices;
        float lastPress = -1;
        float nextSearch;
        internal bool Ready { get { return visual != null; } }
        internal bool Create(Transform hand, int layer) {
            if (visual != null) return true;
            if (Time.unscaledTime < nextSearch) return false;
            nextSearch = Time.unscaledTime + 1;
            Renderer original = null; Mesh mesh = null;
            try { suppliedMesh = SuppliedSprayModel.Load(); mesh = suppliedMesh; }
            catch (System.Exception e) { BepInEx.Logging.Logger.CreateLogSource("VR Spray").LogWarning("Supplied model could not load; using native can: " + e.Message); }
            if (mesh == null) foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>()) {
                Mesh source = null;
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                if (skinned != null) source = skinned.sharedMesh;
                else { MeshFilter filter = renderer.GetComponent<MeshFilter>(); if (filter != null) source = filter.sharedMesh; }
                if (source == null || (source.name != "LVL_SPRAY" && source.name != "LVL_SPRAY_UI")) continue;
                original = renderer; mesh = source;
                if (source.name == "LVL_SPRAY") break;
            }
            if (mesh == null) return false;
            visual = new GameObject("VR Tracked Spray Can"); visual.layer = layer; visual.transform.SetParent(hand, false);
            visual.transform.localPosition = new Vector3(-.035f, -.060f, .080f);
            GameObject body = new GameObject("Spray Can Mesh", typeof(MeshFilter), typeof(MeshRenderer)); body.layer = layer;
            body.transform.SetParent(visual.transform, false); body.GetComponent<MeshFilter>().sharedMesh = mesh;
            if (suppliedMesh != null) {
                Shader canShader = Shader.Find("Unlit/Color"); if (canShader == null) canShader = Shader.Find("Sprites/Default");
                // Opaque palette materials preserve depth between the fingers and can.
                Color[] colors = suppliedMesh.colors; int[] indices = suppliedMesh.triangles;
                List<Color> palette = new List<Color>(); List<List<int>> groups = new List<List<int>>();
                for (int i = 0; i < indices.Length; i += 3) {
                    Color color = colors[indices[i]]; int part = palette.IndexOf(color);
                    if (part < 0) { part = palette.Count; palette.Add(color); groups.Add(new List<int>()); }
                    groups[part].Add(indices[i]); groups[part].Add(indices[i+1]); groups[part].Add(indices[i+2]);
                }
                materials = new Material[palette.Count]; suppliedMesh.subMeshCount = palette.Count;
                for (int i = 0; i < palette.Count; i++) {
                    materials[i] = new Material(canShader); materials[i].color = palette[i];
                    suppliedMesh.SetTriangles(groups[i].ToArray(), i);
                }
                // Palette colors are already in materials; fallback sprite shader multiplies by white.
                for (int i = 0; i < colors.Length; i++) colors[i] = Color.white; suppliedMesh.colors = colors;
                suppliedMesh.MarkDynamic(); restVertices = suppliedMesh.vertices; pressedVertices = new Vector3[restVertices.Length];
            } else {
                float scale = .19f / Mathf.Max(.001f, mesh.bounds.size.z);
                body.transform.localRotation = Quaternion.Euler(-90, 0, 0); body.transform.localScale = Vector3.one * scale;
                body.transform.localPosition = -(body.transform.localRotation * mesh.bounds.center) * scale;
                Material[] sourceMaterials = original.sharedMaterials; materials = new Material[sourceMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = new Material(sourceMaterials[i]);
            }
            body.GetComponent<MeshRenderer>().sharedMaterials = materials;
            Nozzle = new GameObject("Spray Nozzle").transform; Nozzle.gameObject.layer = layer; Nozzle.SetParent(visual.transform, false);
            Nozzle.localPosition = new Vector3(0, .088f, .012f);
            sound=Nozzle.gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.loop=true;
            sound.volume=.65f;sound.spatialBlend=.8f;sound.minDistance=.8f;sound.maxDistance=8;sound.dopplerLevel=0;sound.priority=80;
            // A seamless nozzle hiss has no silent animation lead-in or can-rattle outro.
            float[] hiss=SprayAudioMath.Hiss(44100);sprayClip=AudioClip.Create("VR Aerosol Nozzle",hiss.Length,1,44100,false);sprayClip.SetData(hiss,0);sound.clip=sprayClip;
            GameObject particles = new GameObject("VR Spray Mist", typeof(ParticleSystem)); particles.layer = layer; particles.transform.SetParent(Nozzle, false);
            mist = particles.GetComponent<ParticleSystem>(); mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = mist.main; main.loop = true; main.playOnAwake = false;
            main.startLifetime = 0.10f; main.startSpeed = 5f; main.startSize = 0.012f;
            main.startColor = new Color(0.25f, 0.95f, 0.45f, 0.35f); main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = mist.emission; emission.rateOverTime = 100;
            ParticleSystem.ShapeModule shape = mist.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 6; shape.radius = 0.003f;
            Shader shader = Shader.Find("Particles/Alpha Blended"); if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) { mistMaterial = new Material(shader); particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = mistMaterial; }
            visual.SetActive(false);
            BepInEx.Logging.Logger.CreateLogSource("VR Spray").LogInfo("Tracked can: " + mesh.name + "; 19 cm, flat Sludge palette, moving actuator.");
            return true;
        }
        internal void Show(bool held, bool spraying) {
            if (visual == null) return;
            visual.SetActive(held);
            float press = held && VrPlugin.Instance != null ? VrPlugin.Instance.Right.TriggerAmount : 0;
            if (Nozzle != null) Nozzle.localPosition = new Vector3(0,.088f-.003f*press,.012f);
            if (suppliedMesh != null && Mathf.Abs(press-lastPress) > .002f) {
                lastPress = press;
                for (int i = 0; i < restVertices.Length; i++) {
                    pressedVertices[i] = restVertices[i];
                    pressedVertices[i].y -= .003f*press*Mathf.Clamp01((restVertices[i].y-.078f)/.007f);
                }
                suppliedMesh.vertices = pressedVertices; suppliedMesh.RecalculateBounds();
            }
            if (mist == null) return;
            if (sound != null) {
                if (held && spraying) { if (!sound.isPlaying) sound.Play(); }
                else if (sound.isPlaying) sound.Stop();
            }
            if (held && spraying) { if (!mist.isPlaying) mist.Play(); }
            else if (mist.isPlaying) mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        internal string Describe(){return "sprayAudio="+(sound!=null && sound.isPlaying)+" listener="+AudioListener.volume;}
        internal void Clear() {
            if (visual != null) Object.Destroy(visual);
            if(sprayClip!=null)Object.Destroy(sprayClip);sprayClip=null;
            if (materials != null) foreach (Material material in materials) if (material != null) Object.Destroy(material);
            if (mistMaterial != null) Object.Destroy(mistMaterial);
            if (suppliedMesh != null) Object.Destroy(suppliedMesh);
            visual = null; Nozzle = null; mist = null; materials = null; mistMaterial = null; sound = null; nextSearch = 0;
            suppliedMesh = null; restVertices = pressedVertices = null; lastPress = -1;
        }
    }
}
