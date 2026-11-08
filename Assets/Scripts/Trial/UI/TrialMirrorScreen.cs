using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TrialMirrorScreen : Graphic
    {
        struct Shard
        {
            public Vector2 A,B,C;
            public float Drift,Drop,Spin,Tint,Delay,Side;
        }

        readonly List<Shard> shards=new List<Shard>();
        Texture frame;
        float progress;
        public override Texture mainTexture=>frame?frame:Texture2D.whiteTexture;

        internal void Initialize(Texture screenshot)
        {
            frame=screenshot;
            const int columns=8,rows=6;
            var points=new Vector2[columns+1,rows+1];
            var random=new System.Random(1329);
            for(int x=0;x<=columns;x++)
            for(int y=0;y<=rows;y++)
            {
                float dx=x==0 || x==columns?0:(float)random.NextDouble()*.68f-.34f;
                float dy=y==0 || y==rows?0:(float)random.NextDouble()*.68f-.34f;
                points[x,y]=new Vector2((x+dx)/columns,(y+dy)/rows);
            }
            for(int x=0;x<columns;x++)
            for(int y=0;y<rows;y++)
            {
                var a=points[x,y];
                var b=points[x+1,y];
                var c=points[x+1,y+1];
                var d=points[x,y+1];
                if((x+y)%2==0){Add(a,b,c);Add(a,c,d);}
                else {Add(a,b,d);Add(b,c,d);}
            }
            SetAllDirty();

            void Add(Vector2 a,Vector2 b,Vector2 c)
            {
                Vector2 center=(a+b+c)/3f-new Vector2(.5f,.5f);
                shards.Add(new Shard
                {
                    A=a,B=b,C=c,
                    Drift=center.x*.20f+((float)random.NextDouble()-.5f)*.09f,
                    Drop=.72f+(float)random.NextDouble()*.63f,
                    Spin=(float)random.NextDouble()*2.2f-1.1f,
                    Tint=.87f+(float)random.NextDouble()*.13f,
                    Delay=Mathf.Abs(center.y)*.84f+Mathf.Abs(center.x)*.16f,
                    Side=Mathf.Sign(center.y)
                });
            }
        }

        internal void SetProgress(float value){progress=value;SetVerticesDirty();}

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect area=rectTransform.rect;
            float motion=InkMainMenu.ReducedMotion?.48f:1;
            foreach(var shard in shards)
            {
                Vector2 a=area.min+Vector2.Scale(shard.A,area.size);
                Vector2 b=area.min+Vector2.Scale(shard.B,area.size);
                Vector2 c=area.min+Vector2.Scale(shard.C,area.size);
                Vector2 center=(a+b+c)/3;
                float age=Mathf.InverseLerp(shard.Delay,1,progress);
                float split=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.24f,age));
                float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,1,age));
                float angle=shard.Spin*age*motion;
                float sine=Mathf.Sin(angle),cosine=Mathf.Cos(angle);
                Vector2 drop=new Vector2(shard.Drift*area.width*age*motion,
                    area.height*(shard.Side*.052f*split-shard.Drop*age*age)*motion);
                float shrink=Mathf.Lerp(1,.87f,split);
                float shade=Mathf.Lerp(1,shard.Tint,split);
                Color tint=frame?new Color(shade*Mathf.Lerp(1,.88f,split),shade*Mathf.Lerp(1,.97f,split),shade,fade):
                    new Color(.08f,shard.Tint*.62f,shard.Tint*.82f,fade*split*.66f);
                int index=mesh.currentVertCount;
                mesh.AddVert(Fall(a),tint,shard.A);
                mesh.AddVert(Fall(b),tint,shard.B);
                mesh.AddVert(Fall(c),tint,shard.C);
                mesh.AddTriangle(index,index+1,index+2);

                Vector2 Fall(Vector2 point)
                {
                    Vector2 local=(point-center)*shrink;
                    return center+drop+new Vector2(local.x*cosine-local.y*sine,local.x*sine+local.y*cosine);
                }
            }
        }
    }
}
