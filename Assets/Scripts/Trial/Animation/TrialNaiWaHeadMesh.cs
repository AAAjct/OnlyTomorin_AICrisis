using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    // The imported guard has one skinned surface. Cut it in bind pose and share
    // the two meshes across all guards so a kill never rebuilds geometry.
    sealed class TrialNaiWaHeadMesh
    {
        static Mesh source;
        static TrialNaiWaHeadMesh cached;
        public Mesh Body { get; }
        public Mesh Head { get; }
        public Vector3 Pivot { get; }

        TrialNaiWaHeadMesh(Mesh body,Mesh head,Vector3 pivot)
        {Body=body;Head=head;Pivot=pivot;}

        public static TrialNaiWaHeadMesh Get(SkinnedMeshRenderer skin,Transform model,int neckIndex)
        {
            Mesh mesh=skin.sharedMesh;
            if(source==mesh && cached!=null && cached.Body && cached.Head)return cached;
            var toModel=model.worldToLocalMatrix*skin.transform.localToWorldMatrix;
            Vector3 normal=toModel.transpose.MultiplyVector(Vector3.up).normalized;
            var vertices=mesh.vertices;
            var distances=new float[vertices.Length];
            // The narrow section below the eyes in the imported NaiWa model.
            for(int i=0;i<vertices.Length;i++)distances[i]=toModel.MultiplyPoint3x4(vertices[i]).y-1.76f;
            var cutter=new Cutter(mesh,vertices,distances,neckIndex);
            Mesh body=cutter.Part(false,normal,out _);
            Mesh head=cutter.Part(true,-normal,out Vector3 pivot);
            source=mesh;
            return cached=new TrialNaiWaHeadMesh(body,head,pivot);
        }

        sealed class Cutter
        {
            readonly Mesh source;
            readonly Vector3[] positions,normals;
            readonly Vector4[] tangents;
            readonly Vector2[] uv;
            readonly Color32[] colors;
            readonly BoneWeight[] weights;
            readonly float[] distances;
            readonly int[] triangles;
            readonly BoneWeight neckWeight;

            public Cutter(Mesh mesh,Vector3[] positions,float[] distances,int neckIndex)
            {
                source=mesh;this.positions=positions;this.distances=distances;
                normals=mesh.normals;tangents=mesh.tangents;uv=mesh.uv;colors=mesh.colors32;
                weights=mesh.boneWeights;triangles=mesh.GetTriangles(0);
                neckWeight=new BoneWeight{boneIndex0=neckIndex,weight0=1};
            }

            public Mesh Part(bool head,Vector3 capNormal,out Vector3 pivot)
            {
                var partPositions=new List<Vector3>();var partNormals=new List<Vector3>();
                var partTangents=new List<Vector4>();var partUv=new List<Vector2>();
                var partColors=new List<Color32>();var partWeights=new List<BoneWeight>();
                var indices=new List<int>();var retained=new Dictionary<int,int>();
                var crossings=new Dictionary<(int,int),int>();var rim=new List<(int,int)>();

                int Add(Vector3 point,Vector3 normal,Vector4 tangent,Vector2 tex,Color32 color,BoneWeight weight)
                {
                    int index=partPositions.Count;
                    partPositions.Add(point);partNormals.Add(normal);partTangents.Add(tangent);
                    partUv.Add(tex);partColors.Add(color);partWeights.Add(weight);
                    return index;
                }
                int Retain(int vertex)
                {
                    if(retained.TryGetValue(vertex,out int index))return index;
                    index=Add(positions[vertex],normals[vertex],tangents[vertex],uv[vertex],colors[vertex],weights[vertex]);
                    retained.Add(vertex,index);return index;
                }
                int Cross(int a,int b)
                {
                    var edge=a<b?(a,b):(b,a);
                    if(crossings.TryGetValue(edge,out int index))return index;
                    float t=distances[a]/(distances[a]-distances[b]);
                    var tangent=Vector4.Lerp(tangents[a],tangents[b],t);tangent.w=t<.5f?tangents[a].w:tangents[b].w;
                    index=Add(Vector3.Lerp(positions[a],positions[b],t),Vector3.Lerp(normals[a],normals[b],t).normalized,
                        tangent,Vector2.Lerp(uv[a],uv[b],t),Color32.Lerp(colors[a],colors[b],t),neckWeight);
                    crossings.Add(edge,index);return index;
                }

                var polygon=new int[4];
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int count=0,first=-1;
                    for(int j=0;j<3;j++)
                    {
                        int a=triangles[i+j],b=triangles[i+(j+1)%3];
                        bool inside=(distances[a]>0)==head;
                        if(inside)polygon[count++]=Retain(a);
                        if(inside==((distances[b]>0)==head))continue;
                        int cross=Cross(a,b);polygon[count++]=cross;
                        if(first<0)first=cross;else rim.Add((first,cross));
                    }
                    for(int j=1;j<count-1;j++)
                    {indices.Add(polygon[0]);indices.Add(polygon[j]);indices.Add(polygon[j+1]);}
                }

                pivot=Vector3.zero;Vector2 capUv=Vector2.zero;
                foreach(int edge in crossings.Values){pivot+=partPositions[edge];capUv+=partUv[edge];}
                pivot/=crossings.Count;capUv/=crossings.Count;
                Vector3 tangentAxis=Vector3.Cross(capNormal,Vector3.right).normalized;
                var capTangent=new Vector4(tangentAxis.x,tangentAxis.y,tangentAxis.z,1);
                Color32 capColor=new Color32(255,255,255,255);
                int center=Add(pivot,capNormal,capTangent,capUv,capColor,neckWeight);
                var capVertices=new Dictionary<int,int>();
                foreach(int edge in crossings.Values)
                    capVertices.Add(edge,Add(partPositions[edge],capNormal,capTangent,partUv[edge],capColor,neckWeight));
                foreach(var (a,b) in rim)
                {
                    bool forward=Vector3.Dot(Vector3.Cross(partPositions[a]-pivot,partPositions[b]-pivot),capNormal)>0;
                    indices.Add(center);indices.Add(capVertices[forward?a:b]);indices.Add(capVertices[forward?b:a]);
                }

                var mesh=new Mesh{name=head?"NaiWa · Detached head":"NaiWa · Headless body"};
                mesh.SetVertices(partPositions);mesh.SetNormals(partNormals);mesh.SetTangents(partTangents);
                mesh.SetUVs(0,partUv);mesh.SetColors(partColors);
                if(!head){mesh.bindposes=source.bindposes;mesh.boneWeights=partWeights.ToArray();}
                mesh.SetTriangles(indices,0);mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
