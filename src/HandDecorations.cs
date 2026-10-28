using System;
using System.Collections.Generic;
using UnityEngine;

namespace SludgeLife2VR {
    // Barycentric attachments follow the original hand topology through every finger pose.
    internal sealed class HandDecorations {
        struct Point {internal int A,B,C;internal Vector3 Weights;}
        readonly List<Point> points=new List<Point>();
        readonly List<int> green=new List<int>(), black=new List<int>();
        Vector3[] flat, vertices;int[] triangles, fingers;
        Mesh mesh;GameObject model;bool left;
        int Add(Vector3 request,Vector3 normal,int finger) {
            float best=float.MaxValue;Point chosen=new Point();bool found=false;
            for(int i=0;i<triangles.Length;i+=3) {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                if(finger>=0 && (fingers[a]!=finger || fingers[b]!=finger || fingers[c]!=finger))continue;
                Vector3 ab=flat[b]-flat[a],ac=flat[c]-flat[a],n=Vector3.Cross(ab,ac);
                if(Vector3.Dot(n.normalized,normal)<.45f)continue;
                Vector3 p=SurfaceContact.Triangle(request,flat[a],flat[b],flat[c]);float d=(p-request).sqrMagnitude;
                if(d>=best)continue;
                float aa=Vector3.Dot(ab,ab),bb=Vector3.Dot(ac,ac),cc=Vector3.Dot(ab,ac),den=aa*bb-cc*cc;
                if(Mathf.Abs(den)<1e-12f)continue;
                Vector3 ap=p-flat[a];float v=(bb*Vector3.Dot(ap,ab)-cc*Vector3.Dot(ap,ac))/den;
                float w=(aa*Vector3.Dot(ap,ac)-cc*Vector3.Dot(ap,ab))/den;
                chosen=new Point{A=a,B=b,C=c,Weights=new Vector3(1-v-w,v,w)};best=d;found=true;
            }
            if(!found)throw new InvalidOperationException("Hand decoration surface was not recognized.");
            points.Add(chosen);return points.Count-1;
        }
        void Face(List<int> part,int a,int b,int c){part.Add(a);part.Add(left?c:b);part.Add(left?b:c);}
        internal HandDecorations(Vector3[] source,int[] indices,int[] ids,bool isLeft) {
            flat=source;triangles=indices;fingers=ids;left=isLeft;
            float[] x={-.080f,-.026f,.027f,.080f},z={.135f,.145f,.140f,.110f};
            for(int f=0;f<4;f++) {
                Vector3 center=new Vector3(x[f],-.025f,z[f]-.022f);
                int middle=Add(center,Vector3.up,-1);int[] ring=new int[16],edge=new int[16];
                for(int j=0;j<16;j++) {
                    float angle=j*Mathf.PI*2/16;Vector3 delta=new Vector3(Mathf.Cos(angle)*.016f,0,Mathf.Sin(angle)*.019f);
                    ring[j]=Add(center+delta,Vector3.up,-1);edge[j]=Add(center+delta*1.12f,Vector3.up,-1);
                }
                for(int j=0;j<16;j++){int k=(j+1)%16;Face(green,middle,ring[k],ring[j]);Face(black,ring[j],ring[k],edge[j]);Face(black,edge[j],ring[k],edge[k]);}
            }
            if(!left) AddTattoo();
            vertices=new Vector3[points.Count];
        }
        internal Vector3[] Vertices {get{return vertices;}}
        internal int[] Green {get{return green.ToArray();}}
        internal int[] Black {get{return black.ToArray();}}
        internal HandDecorations(Transform parent,Vector3[] source,int[] indices,int[] ids,bool isLeft):this(source,indices,ids,isLeft) {
            model=new GameObject("Biker Glove Openings and Ant Ink",typeof(MeshFilter),typeof(MeshRenderer));
            model.layer=parent.gameObject.layer;model.transform.SetParent(parent,false);
            vertices=new Vector3[points.Count];mesh=new Mesh();mesh.name="Articulated Glove and Tattoo Surface";mesh.MarkDynamic();
            mesh.vertices=vertices;mesh.subMeshCount=2;mesh.SetTriangles(green.ToArray(),0);mesh.SetTriangles(black.ToArray(),1);
            model.GetComponent<MeshFilter>().sharedMesh=mesh;
            model.GetComponent<MeshRenderer>().sharedMaterials=new Material[]{TrackedHandModel.Skin,TrackedHandModel.Polish};
        }
        static float Cross(Vector2 a,Vector2 b){return a.x*b.y-a.y*b.x;}
        static List<Vector2> Clip(List<Vector2> polygon,Vector2 a,Vector2 b,float sign){
            List<Vector2> result=new List<Vector2>();if(polygon.Count==0)return result;
            Vector2 previous=polygon[polygon.Count-1];float dp=Cross(b-a,previous-a)*sign;
            foreach(Vector2 current in polygon){
                float dc=Cross(b-a,current-a)*sign;
                if((dp>=0)!=(dc>=0))result.Add(previous+(current-previous)*(dp/(dp-dc)));
                if(dc>=0)result.Add(current);previous=current;dp=dc;
            }return result;
        }
        void AddTattoo(){
            // Clip the ink to each native triangle before mapping it. This prevents the
            // ant from disappearing inside convex finger facets when the index bends.
            for(int f=0;f<triangles.Length;f+=3){
                int a=triangles[f],b=triangles[f+1],c=triangles[f+2];
                if(fingers[a]!=1 || fingers[b]!=1 || fingers[c]!=1)continue;
                if(Vector3.Cross(flat[b]-flat[a],flat[c]-flat[a]).normalized.x>-.45f)continue;
                Vector2 pa=new Vector2(flat[a].z,flat[a].y),pb=new Vector2(flat[b].z,flat[b].y),pc=new Vector2(flat[c].z,flat[c].y);
                float area=Cross(pb-pa,pc-pa);if(Mathf.Abs(area)<1e-9f)continue;float sign=area>0?1:-1;
                for(int r=0;r<AntTattoo.Rectangles.GetLength(0);r++){
                    float z0=.232f+(.5f-AntTattoo.Rectangles[r,0]/AntTattoo.Width)*.030f;
                    float z1=.232f+(.5f-AntTattoo.Rectangles[r,1]/AntTattoo.Width)*.030f;
                    float y0=-.076f+(.5f-AntTattoo.Rectangles[r,2]/AntTattoo.Height)*.024f;
                    float y1=-.076f+(.5f-AntTattoo.Rectangles[r,3]/AntTattoo.Height)*.024f;
                    List<Vector2> polygon=new List<Vector2>(new Vector2[]{new Vector2(z0,y0),new Vector2(z1,y0),new Vector2(z1,y1),new Vector2(z0,y1)});
                    polygon=Clip(polygon,pa,pb,sign);polygon=Clip(polygon,pb,pc,sign);polygon=Clip(polygon,pc,pa,sign);
                    if(polygon.Count<3)continue;int first=points.Count;
                    foreach(Vector2 v in polygon){float w1=Cross(v-pa,pc-pa)/area,w2=Cross(pb-pa,v-pa)/area;
                        points.Add(new Point{A=a,B=b,C=c,Weights=new Vector3(1-w1-w2,w1,w2)});}
                    for(int j=1;j+1<polygon.Count;j++){Face(black,first,first+j,first+j+1);Face(black,first+j+1,first+j,first);}
                }
            }
        }
        internal void Update(Vector3[] posed) {
            for(int i=0;i<vertices.Length;i++) {
                Point p=points[i];Vector3 a=posed[p.A],b=posed[p.B],c=posed[p.C];
                Vector3 n=Vector3.Cross(b-a,c-a).normalized*(left?-1:1);
                vertices[i]=a*p.Weights.x+b*p.Weights.y+c*p.Weights.z+n*.0012f;
            }
            if(mesh!=null){mesh.vertices=vertices;mesh.RecalculateBounds();}
        }
        internal void Clear(){if(mesh!=null)UnityEngine.Object.Destroy(mesh);if(model!=null)UnityEngine.Object.Destroy(model);mesh=null;model=null;}
    }
}

