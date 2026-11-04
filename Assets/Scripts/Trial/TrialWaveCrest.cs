using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    // A bounded water sheet follows the actual moving collision ring, with constant width.
    public sealed class TrialWaveCrest : MonoBehaviour
    {
        LineRenderer ring;
        Mesh mesh;
        MeshRenderer sheet;
        MaterialPropertyBlock properties;
        readonly Vector3[] points=new Vector3[65];
        readonly Vector3[] vertices=new Vector3[195];
        public void Initialize(LineRenderer source,Material material)
        {
            ring=source;var obj=new GameObject("Moving tidal crest");obj.transform.SetParent(transform,false);
            mesh=new Mesh{name="Constant-width shockwave crest"};mesh.MarkDynamic();
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;sheet=obj.AddComponent<MeshRenderer>();sheet.sharedMaterial=material;
            sheet.shadowCastingMode=ShadowCastingMode.Off;sheet.receiveShadows=false;sheet.enabled=false;properties=new MaterialPropertyBlock();
            var uv=new Vector2[vertices.Length];var indices=new int[64*12];var colors=new Color[vertices.Length];
            for(int i=0;i<65;i++)for(int j=0;j<3;j++)uv[i*3+j]=new Vector2(i/64f,j*.5f);
            for(int i=0;i<colors.Length;i++)colors[i]=Color.white;
            for(int i=0;i<64;i++)for(int j=0;j<2;j++)
            {int k=i*3+j,t=i*12+j*6;indices[t]=k;indices[t+1]=k+3;indices[t+2]=k+1;indices[t+3]=k+1;indices[t+4]=k+3;indices[t+5]=k+4;}
            mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=indices;
        }
        void LateUpdate()
        {
            if(!ring)return;sheet.enabled=ring.enabled;if(!sheet.enabled)return;
            ring.GetPositions(points);var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
            for(int i=0;i<65;i++)
            {
                var outward=Vector3.ProjectOnPlane(points[i]-bounds.center,Vector3.up).normalized;
                float height=.16f+.16f*Mathf.Sin(i*.98f+Time.time*8);
                vertices[i*3]=sheet.transform.InverseTransformPoint(points[i]-outward*.22f-Vector3.up*.06f);
                vertices[i*3+1]=sheet.transform.InverseTransformPoint(points[i]+Vector3.up*height);
                vertices[i*3+2]=sheet.transform.InverseTransformPoint(points[i]+outward*.22f-Vector3.up*.06f);
            }
            mesh.vertices=vertices;mesh.RecalculateBounds();
            properties.SetColor("_BaseColor",ring.startColor);sheet.SetPropertyBlock(properties);
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
