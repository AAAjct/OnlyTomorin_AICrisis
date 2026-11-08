using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        static readonly int MirrorAge=Shader.PropertyToID("_ShatterAge");
        static readonly int VioletGlass=Shader.PropertyToID("_VioletGlass"),ShardScatter=Shader.PropertyToID("_ShardScatter");
        Material mirrorModelMaterial;

        bool EnsureMirrorModelMaterial()
        {
            if(mirrorModelMaterial)return true;
            var shader=Resources.Load<Shader>("Trial/VFX/MirrorModel");
            if(!shader || !shader.isSupported)return false;
            mirrorModelMaterial=new Material(shader);
            return true;
        }

        bool ShatterMirrorModel(TrialCombatEvent cue)
        {
            var target=game.SpellTarget(cue.target);
            if(!target || !target.Visual || !EnsureMirrorModelMaterial())return false;

            Mesh baked=null;
            bool shattered=false;
            foreach(var source in target.Visual.GetComponentsInChildren<Renderer>(true))
            {
                if(!source.enabled || !WasVisible(source.transform,target.Visual))continue;
                Mesh surface;
                if(source is SkinnedMeshRenderer skin && skin.sharedMesh)
                {
                    if(!baked)baked=new Mesh();
                    // Use the scale-compensated bake before applying the renderer's full transform.
                    skin.BakeMesh(baked,true);
                    surface=baked;
                }
                else if(source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter))
                    surface=filter.sharedMesh;
                else continue;
                if(!surface || !surface.isReadable || surface.vertexCount==0)continue;
                var materials=source.sharedMaterials;
                var fragments=FractureSurface(surface,source.transform,cue.position,materials.Length);
                if(!fragments)continue;

                var effect=AddSpellVisual("Mirror · shattered model",cue,1.16f);
                var surfaces=new MaterialPropertyBlock[fragments.subMeshCount];
                for(int slot=0;slot<surfaces.Length;slot++)
                    surfaces[slot]=MirrorSurfaceProperties(source,materials[slot],slot);
                AttachModelFragments(effect,fragments,surfaces,0);
                shattered=true;
            }
            if(baked)Destroy(baked);
            return shattered;
        }

        void AttachModelFragments(SpellVisual effect,Mesh mesh,MaterialPropertyBlock[] surfaces,float initialProgress)
        {
            effect.ModelMesh=mesh;
            effect.ModelMaterials=surfaces;
            var filter=effect.CoffinFilter?effect.CoffinFilter:effect.Root.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh=mesh;
            var renderer=effect.Surface?effect.Surface:effect.Root.gameObject.AddComponent<MeshRenderer>();
            effect.Surface=renderer;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            var materials=new Material[surfaces.Length];
            for(int slot=0;slot<materials.Length;slot++)materials[slot]=mirrorModelMaterial;
            renderer.sharedMaterials=materials;
            effect.Age=effect.Duration*initialProgress;
            PaintMirrorModel(effect,initialProgress);
        }

        // Executions hide the model root, so check visibility only beneath that root.
        static bool WasVisible(Transform part,Transform root)
        {
            for(var current=part;current!=root;current=current.parent)
                if(!current.gameObject.activeSelf)return false;
            return true;
        }

        static MaterialPropertyBlock MirrorSurfaceProperties(Renderer source,Material original,int slot)
        {
            var properties=new MaterialPropertyBlock();
            source.GetPropertyBlock(properties,slot);
            if(properties.isEmpty)source.GetPropertyBlock(properties);
            var glass=new MaterialPropertyBlock();
            var tint=Color.white;
            if(original)
            {
                string map=original.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
                if(original.HasProperty(map))
                {
                    var texture=properties.GetTexture(map);
                    if(!texture)texture=original.GetTexture(map);
                    if(texture)glass.SetTexture("_BaseMap",texture);
                    var scale=original.GetTextureScale(map);
                    var offset=original.GetTextureOffset(map);
                    string transform=map+"_ST";
                    glass.SetVector("_BaseMap_ST",properties.HasVector(transform)?properties.GetVector(transform):new Vector4(scale.x,scale.y,offset.x,offset.y));
                }
                string color=original.HasProperty("_BaseColor")?"_BaseColor":"_Color";
                if(original.HasProperty(color))tint=properties.HasColor(color)?properties.GetColor(color):original.GetColor(color);
            }
            glass.SetColor("_BaseColor",tint);
            return glass;
        }

        static Mesh FractureSurface(Mesh surface,Transform source,Vector3 origin,int materialCount)
        {
            var points=surface.vertices;
            var sourceNormals=surface.normals;
            var sourceUv=surface.uv;
            var sourceColors=surface.colors32;
            int slots=Mathf.Min(materialCount,surface.subMeshCount);
            if(slots==0)return null;
            var bounds=surface.bounds;
            var toWorld=source.localToWorldMatrix;
            var toNormal=source.worldToLocalMatrix.transpose;
            var seeds=new List<Vector3>();
            var random=new System.Random(753);
            int depth=Application.isMobilePlatform?2:3;
            for(int x=0;x<3;x++)
            for(int y=0;y<4;y++)
            for(int z=0;z<depth;z++)
                seeds.Add(new Vector3((x+Jitter())/3f,(y+Jitter())/4f,(z+Jitter())/depth));
            float Jitter()=>.18f+.64f*(float)random.NextDouble();

            var vertices=new List<Vector3>();
            var normals=new List<Vector3>();
            var uv=new List<Vector2>();
            var colors=new List<Color32>();
            var motion=new List<Vector4>();
            var remap=new Dictionary<(int vertex,int shard),int>();
            var triangles=new List<int>[slots];
            for(int slot=0;slot<slots;slot++)
            {
                var indices=triangles[slot]=new List<int>();
                if(surface.GetTopology(slot)!=MeshTopology.Triangles)continue;
                var original=surface.GetTriangles(slot);
                for(int index=0;index<original.Length;index+=3)
                {
                    int a=original[index],b=original[index+1],c=original[index+2];
                    var center=(points[a]+points[b]+points[c])/3-bounds.min;
                    center=new Vector3(center.x/Mathf.Max(.001f,bounds.size.x),center.y/Mathf.Max(.001f,bounds.size.y),center.z/Mathf.Max(.001f,bounds.size.z));
                    int closest=0;
                    float distance=float.MaxValue;
                    for(int shard=0;shard<seeds.Count;shard++)
                    {
                        float next=(center-seeds[shard]).sqrMagnitude;
                        if(next<distance){closest=shard;distance=next;}
                    }
                    indices.Add(Vertex(a,closest));indices.Add(Vertex(b,closest));indices.Add(Vertex(c,closest));
                }
            }
            if(vertices.Count==0)return null;
            var mesh=new Mesh{name="Mirror fragments from "+surface.name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(1,motion);mesh.SetColors(colors);
            mesh.subMeshCount=slots;
            for(int slot=0;slot<slots;slot++)mesh.SetTriangles(triangles[slot],slot,false);
            mesh.RecalculateBounds();
            var movingBounds=mesh.bounds;
            movingBounds.Expand(12);
            mesh.bounds=movingBounds;
            return mesh;

            int Vertex(int index,int shard)
            {
                var key=(index,shard);
                if(remap.TryGetValue(key,out int existing))return existing;
                int next=vertices.Count;
                remap.Add(key,next);
                vertices.Add(toWorld.MultiplyPoint3x4(points[index])-origin);
                normals.Add(index<sourceNormals.Length?toNormal.MultiplyVector(sourceNormals[index]).normalized:Vector3.up);
                uv.Add(index<sourceUv.Length?sourceUv[index]:Vector2.zero);
                colors.Add(index<sourceColors.Length?sourceColors[index]:new Color32(255,255,255,255));
                var pivot=toWorld.MultiplyPoint3x4(bounds.min+Vector3.Scale(bounds.size,seeds[shard]))-origin;
                motion.Add(new Vector4(pivot.x,pivot.y,pivot.z,shard+1));
                return next;
            }
        }

        static void PaintMirrorModel(SpellVisual effect,float progress)
        {
            for(int slot=0;slot<effect.ModelMaterials.Length;slot++)
            {
                var block=effect.ModelMaterials[slot];
                block.SetFloat(MirrorAge,progress);
                effect.Surface.SetPropertyBlock(block,slot);
            }
        }
    }
}
