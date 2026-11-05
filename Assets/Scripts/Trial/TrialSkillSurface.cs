using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    // Presentation follows the existing authoritative warning polygon, including safe holes.
    public sealed class TrialSkillSurface : MonoBehaviour
    {
        LineRenderer boundary;
        TrialBoss fish;
        TrialChampion champion;
        Mesh mesh;
        MeshRenderer surface;
        readonly Vector3[] points=new Vector3[65];
        readonly Vector3[] vertices=new Vector3[65];
        readonly int[] triangles=new int[63*3];
        MaterialPropertyBlock properties;
        int previousCount;
        public void Initialize(LineRenderer line)
        {
            boundary=line;fish=GetComponentInParent<TrialBoss>();champion=GetComponentInParent<TrialChampion>();
            var obj=new GameObject("Layered skill sigil");obj.transform.SetParent(transform,false);
            mesh=new Mesh{name="Authoritative warning footprint"};mesh.MarkDynamic();
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            surface=obj.AddComponent<MeshRenderer>();surface.sharedMaterial=Resources.Load<Material>("Trial/VFX/TideSigil");
            surface.shadowCastingMode=ShadowCastingMode.Off;surface.receiveShadows=false;
            properties=new MaterialPropertyBlock();surface.enabled=false;
        }
        void LateUpdate()
        {
            if(!boundary || !surface)return;
            surface.enabled=boundary.enabled && boundary.gameObject.activeInHierarchy;
            if(!surface.enabled)return;
            int count=Mathf.Min(65,boundary.positionCount);
            if(count<2){surface.enabled=false;return;}
            boundary.GetPositions(points);
            Vector3 center=Vector3.zero;
            for(int i=0;i<count;i++)center+=points[i];
            center/=Mathf.Max(1,count);
            if(champion && champion.Attacking && champion.Move.shape==ChampionShape.Annulus)center=champion.transform.position;
            if(count==2)
            {
                var side=Vector3.Cross(Vector3.up,(points[1]-points[0]).normalized)*boundary.widthMultiplier*.5f;
                vertices[0]=points[0]-side;vertices[1]=points[1]-side;vertices[2]=points[1]+side;vertices[3]=points[0]+side;
                count=4;
            }
            else for(int i=0;i<count;i++)vertices[i]=points[i];
            // Convert world coordinates once; pooled surfaces can remain under moving actors.
            for(int i=0;i<count;i++)vertices[i]=surface.transform.InverseTransformPoint(vertices[i]-Vector3.up*.016f);
            // Remove old indices before a smaller vertex buffer makes them invalid.
            if(previousCount!=count)mesh.Clear();
            mesh.SetVertices(vertices,0,count);
            if(previousCount!=count)
            {
                for(int i=0;i<count-2;i++){triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=i+2;}
                mesh.SetTriangles(triangles,0,(count-2)*3,0);previousCount=count;
            }
            mesh.RecalculateBounds();
            float progress=fish?fish.WarningProgress:champion?champion.WarningProgress:0;
            properties.SetColor("_Tint",boundary.startColor);
            properties.SetFloat("_Charge",progress);
            properties.SetVector("_Center",center);
            properties.SetFloat("_Inner",champion && champion.Attacking && champion.Move.shape==ChampionShape.Annulus?champion.Move.inner:0);
            surface.SetPropertyBlock(properties);
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
