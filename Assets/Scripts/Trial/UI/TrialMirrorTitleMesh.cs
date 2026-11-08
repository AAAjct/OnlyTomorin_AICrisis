using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    [AddComponentMenu("")]
    sealed class TrialMirrorTitleMesh : BaseMeshEffect
    {
        const int Columns=3,Rows=4;
        readonly List<UIVertex> glyphs=new List<UIVertex>();
        readonly Vector2[,] points=new Vector2[Columns+1,Rows+1];

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
            var random=new System.Random(1847);
            for(int glyph=0;glyph<glyphs.Count;glyph+=4)
            {
                var topLeft=glyphs[glyph];var topRight=glyphs[glyph+1];
                var bottomRight=glyphs[glyph+2];var bottomLeft=glyphs[glyph+3];
                for(int y=0;y<=Rows;y++)
                for(int x=0;x<=Columns;x++)
                {
                    float dx=x==0 || x==Columns?0:Jitter();
                    float dy=y==0 || y==Rows?0:Jitter();
                    points[x,y]=new Vector2((x+dx)/Columns,(y+dy)/Rows);
                }
                for(int y=0;y<Rows;y++)
                for(int x=0;x<Columns;x++)
                {
                    var a=points[x,y];var b=points[x+1,y];var c=points[x,y+1];var d=points[x+1,y+1];
                    if((x+y+glyph/4)%2==0){Shard(a,b,d);Shard(d,c,a);}
                    else{Shard(a,b,c);Shard(b,d,c);}
                }

                void Shard(Vector2 a,Vector2 b,Vector2 c)
                {
                    Vector2 center=(a+b+c)/3;
                    float facet=(float)random.NextDouble();
                    int index=mesh.currentVertCount;
                    Add(a,1,0);Add(b,0,1);Add(c,0,0);
                    mesh.AddTriangle(index,index+1,index+2);

                    void Add(Vector2 point,float edgeA,float edgeB)
                    {
                        Vector2 inset=Vector2.Lerp(point,center,.085f);
                        var vertex=topLeft;
                        vertex.position=Vector3.Lerp(Vector3.Lerp(topLeft.position,topRight.position,inset.x),
                            Vector3.Lerp(bottomLeft.position,bottomRight.position,inset.x),inset.y);
                        vertex.uv0=Vector4.Lerp(Vector4.Lerp(topLeft.uv0,topRight.uv0,point.x),
                            Vector4.Lerp(bottomLeft.uv0,bottomRight.uv0,point.x),point.y);
                        vertex.uv1=new Vector4(edgeA,edgeB,facet,point.y);
                        mesh.AddVert(vertex);
                    }
                }
            }

            float Jitter()=>((float)random.NextDouble()-.5f)*.64f;
        }
    }
}
