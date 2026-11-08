using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        const int PrismColumns=3,PrismRows=5,PrismDepth=3;
        static readonly int CoffinPrism=Shader.PropertyToID("_CoffinPrism");
        Mesh coffinBakedPose,coffinFallbackMesh;
        readonly List<Vector3> coffinPositions=new List<Vector3>();
        readonly List<Vector2> coffinUv=new List<Vector2>();
        readonly List<Color32> coffinColors=new List<Color32>();
        readonly List<int> coffinIndices=new List<int>();
        readonly (int index,float area)[] coffinSamples=new (int,float)[PrismColumns*PrismRows*PrismDepth];

        bool PrepareCoffinPrisms(SpellVisual coffin)
        {
            var target=game.SpellTarget(coffin.Cue.target);
            if(!target || !target.Visual)return false;
            coffin.CoffinPrepared=true;
            if(!target.Alive)return false;

            var cue=coffin.Cue;
            cue.position=target.transform.position+Vector3.up*.86f;
            foreach(var source in target.Visual.GetComponentsInChildren<Renderer>())
            {
                if(!source.enabled)continue;
                Mesh original,pose;
                if(source is SkinnedMeshRenderer skin && skin.sharedMesh && skin.sharedMesh.isReadable)
                {
                    original=skin.sharedMesh;
                    skin.BakeMesh(coffinBakedPose,true);
                    pose=coffinBakedPose;
                }
                else if(source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                    original=pose=filter.sharedMesh;
                else continue;
                if(!original.isReadable)continue;
                var materials=source.sharedMaterials;
                var mesh=BuildModelPrisms(original,pose,source.localToWorldMatrix,cue.position,materials.Length);
                if(!mesh)continue;

                var effect=AddSpellVisual("Black coffin · model prisms",cue,1.28f,tracked:false);
                var surfaces=new MaterialPropertyBlock[mesh.subMeshCount];
                for(int slot=0;slot<surfaces.Length;slot++)
                {
                    var glass=surfaces[slot]=MirrorSurfaceProperties(source,materials[slot],slot);
                    SetCoffinPrismProperties(glass);
                }
                // Draw offscreen before rupture to warm the mesh and shader on this camera.
                AttachModelFragments(effect,mesh,surfaces,1);
                (coffin.CoffinPrisms??=new List<SpellVisual>()).Add(effect);
            }
            return true;
        }

        void ReleaseCoffinPrisms(TrialCombatEvent cue,SpellVisual shell)
        {
            if(Mirror.NetworkClient.active && !Mirror.NetworkServer.active && game.SpellTarget(cue.target) is TrialEnemy replica)
                replica.PresentCoffinExecution();
            cue.position+=Vector3.up*.86f;
            if(shell?.CoffinPrisms!=null)
            {
                foreach(var prism in shell.CoffinPrisms)
                {
                    prism.Root.position=cue.position;
                    prism.Age=prism.Duration*.03f;
                    PaintMirrorModel(prism,.03f);
                    spellVisuals.Add(prism);
                }
                shell.CoffinPrisms=null;
            }
            else if(coffinFallbackMesh)
            {
                var fallback=AddSpellVisual("Black coffin · prisms",cue,1.28f);
                var glass=new MaterialPropertyBlock();
                SetCoffinPrismProperties(glass);
                AttachModelFragments(fallback,coffinFallbackMesh,new[]{glass},.03f);
            }
        }

        static void SetCoffinPrismProperties(MaterialPropertyBlock glass)
        {
            glass.SetFloat(VioletGlass,1);
            glass.SetFloat(ShardScatter,1.60f);
            glass.SetFloat(CoffinPrism,1);
        }

        Mesh BuildModelPrisms(Mesh original,Mesh pose,Matrix4x4 toWorld,Vector3 origin,int materialCount)
        {
            int slots=Mathf.Min(materialCount,original.subMeshCount);
            if(slots==0 || pose.vertexCount==0)return null;
            pose.GetVertices(coffinPositions);
            original.GetUVs(0,coffinUv);
            original.GetColors(coffinColors);
            var bounds=pose.bounds;
            var size=bounds.size;
            var inverseSize=new Vector3(1/Mathf.Max(.001f,size.x),1/Mathf.Max(.001f,size.y),1/Mathf.Max(.001f,size.z));
            var mesh=new PrismGeometry(slots);
            for(int slot=0;slot<slots;slot++)
            {
                if(original.GetTopology(slot)!=MeshTopology.Triangles)continue;
                original.GetTriangles(coffinIndices,slot);
                System.Array.Clear(coffinSamples,0,coffinSamples.Length);
                // Keep the largest sampled face in each cell, so both the torso and small extremities contribute.
                int stride=3*Mathf.Max(1,Mathf.CeilToInt(coffinIndices.Count/14400f));
                for(int i=0;i+2<coffinIndices.Count;i+=stride)
                {
                    var a=coffinPositions[coffinIndices[i]];
                    var b=coffinPositions[coffinIndices[i+1]];
                    var c=coffinPositions[coffinIndices[i+2]];
                    var cell=Vector3.Scale((a+b+c)/3-bounds.min,inverseSize);
                    int x=Mathf.Clamp((int)(cell.x*PrismColumns),0,PrismColumns-1);
                    int y=Mathf.Clamp((int)(cell.y*PrismRows),0,PrismRows-1);
                    int z=Mathf.Clamp((int)(cell.z*PrismDepth),0,PrismDepth-1);
                    int index=(y*PrismColumns+x)*PrismDepth+z;
                    float area=Vector3.Cross(b-a,c-a).sqrMagnitude;
                    if(area>coffinSamples[index].area)coffinSamples[index]=(i,area);
                }
                foreach(var sample in coffinSamples)
                {
                    if(sample.area<=0)continue;
                    int i=sample.index;
                    int a=coffinIndices[i],b=coffinIndices[i+1],c=coffinIndices[i+2];
                    mesh.Add(slot,Point(a),Point(b),Point(c),Uv(a),Uv(b),Uv(c),Tint(a));
                }
            }
            return mesh.Build("Prisms from "+original.name);

            Vector3 Point(int i)=>toWorld.MultiplyPoint3x4(coffinPositions[i])-origin;
            Vector2 Uv(int i)=>i<coffinUv.Count?coffinUv[i]:Vector2.zero;
            Color32 Tint(int i)=>i<coffinColors.Count?coffinColors[i]:new Color32(255,255,255,255);
        }

        static Mesh BuildFallbackPrisms()
        {
            var mesh=new PrismGeometry(1);
            for(int i=0;i<20;i++)
            {
                float angle=i*2.399963f;
                var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                var across=Vector3.Cross(Vector3.up,radial);
                var center=radial*(.17f+.04f*(i%4))+Vector3.up*(i*.072f-.69f);
                mesh.Add(0,center+Vector3.up*.10f,center-across*.08f-Vector3.up*.05f,
                    center+across*.08f-Vector3.up*.05f,Vector2.up,Vector2.zero,Vector2.right,new Color32(255,255,255,255));
            }
            return mesh.Build("Black coffin · fallback prisms");
        }

        sealed class PrismGeometry
        {
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<Vector4> motion=new List<Vector4>();
            readonly List<Color32> colors=new List<Color32>();
            readonly List<int>[] triangles;
            int shardCount;

            public PrismGeometry(int slots)
            {
                triangles=new List<int>[slots];
                for(int i=0;i<slots;i++)triangles[i]=new List<int>();
            }

            public void Add(int slot,Vector3 a,Vector3 b,Vector3 c,Vector2 uvA,Vector2 uvB,Vector2 uvC,Color32 tint)
            {
                var normal=Vector3.Cross(b-a,c-a).normalized;
                if(normal==Vector3.zero)return;
                var center=(a+b+c)/3;
                var axis=(a-center).normalized;
                var across=Vector3.Cross(normal,axis);
                // Grow the sampled face into a readable chip while retaining its original UVs.
                float radius=Mathf.Clamp(Mathf.Max((a-center).magnitude,(b-center).magnitude,(c-center).magnitude)*2.2f,
                    .128f,.268f);
                a=center+axis*radius;
                b=center+(-axis*.52f+across*.83f)*radius;
                c=center+(-axis*.36f-across*.72f)*radius;
                var depth=normal*(radius*.32f);
                var pivot=center-depth*.5f;
                var shard=new Vector4(pivot.x,pivot.y,pivot.z,++shardCount);
                Face(a,b,c,uvA,uvB,uvC);
                Face(a-depth,c-depth,b-depth,uvA,uvC,uvB);
                Edge(a,b,uvA,uvB);
                Edge(b,c,uvB,uvC);
                Edge(c,a,uvC,uvA);

                void Edge(Vector3 from,Vector3 to,Vector2 uvFrom,Vector2 uvTo)
                {
                    Face(to,from,from-depth,uvTo,uvFrom,uvFrom);
                    Face(to,from-depth,to-depth,uvTo,uvFrom,uvTo);
                }

                void Face(Vector3 first,Vector3 second,Vector3 third,Vector2 firstUv,Vector2 secondUv,Vector2 thirdUv)
                {
                    var faceNormal=Vector3.Cross(second-first,third-first).normalized;
                    Vertex(first,firstUv,faceNormal);
                    Vertex(second,secondUv,faceNormal);
                    Vertex(third,thirdUv,faceNormal);
                }

                void Vertex(Vector3 point,Vector2 texCoord,Vector3 faceNormal)
                {
                    triangles[slot].Add(vertices.Count);
                    vertices.Add(point);
                    uv.Add(texCoord);
                    normals.Add(faceNormal);
                    colors.Add(tint);
                    motion.Add(shard);
                }
            }

            public Mesh Build(string name)
            {
                if(shardCount==0)return null;
                var mesh=new Mesh{name=name};
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);
                mesh.SetUVs(0,uv);mesh.SetUVs(1,motion);
                mesh.subMeshCount=triangles.Length;
                for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i,false);
                mesh.RecalculateBounds();
                var bounds=mesh.bounds;
                bounds.Expand(12);
                mesh.bounds=bounds;
                mesh.UploadMeshData(true);
                return mesh;
            }
        }
    }
}
