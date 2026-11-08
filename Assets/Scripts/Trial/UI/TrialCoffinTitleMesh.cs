using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    [AddComponentMenu("")]
    sealed class TrialCoffinTitleMesh : BaseMeshEffect
    {
        const int Segments=5;
        readonly List<UIVertex> glyphs=new List<UIVertex>();

        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive() || mesh.currentVertCount==0)return;
            glyphs.Clear();
            for(int i=0;i<mesh.currentVertCount;i++)
            {
                UIVertex vertex=default;
                mesh.PopulateUIVertex(ref vertex,i);
                glyphs.Add(vertex);
            }

            mesh.Clear();
            for(int glyph=0;glyph<glyphs.Count;glyph+=4)
            {
                var topLeft=glyphs[glyph];var topRight=glyphs[glyph+1];
                var bottomRight=glyphs[glyph+2];var bottomLeft=glyphs[glyph+3];
                int start=mesh.currentVertCount;
                for(int y=0;y<=Segments;y++)
                for(int x=0;x<=Segments;x++)
                {
                    float u=(float)x/Segments,v=(float)y/Segments;
                    var vertex=topLeft;
                    vertex.position=Vector3.Lerp(Vector3.Lerp(topLeft.position,topRight.position,u),
                        Vector3.Lerp(bottomLeft.position,bottomRight.position,u),v);
                    vertex.uv0=Vector4.Lerp(Vector4.Lerp(topLeft.uv0,topRight.uv0,u),
                        Vector4.Lerp(bottomLeft.uv0,bottomRight.uv0,u),v);
                    vertex.uv1=new Vector4(vertex.position.x,vertex.position.y,glyph/4,1-v);
                    mesh.AddVert(vertex);
                }
                for(int y=0;y<Segments;y++)
                for(int x=0;x<Segments;x++)
                {
                    int a=start+y*(Segments+1)+x;
                    int b=a+1,c=a+Segments+1,d=c+1;
                    mesh.AddTriangle(a,b,d);
                    mesh.AddTriangle(d,c,a);
                }
            }
        }
    }
}
