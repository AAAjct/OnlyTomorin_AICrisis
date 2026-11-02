using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    // A single UI mesh renders a bounded field of drifting ink dust and cinnabar petals.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkAtmosphere : MaskableGraphic
    {
        protected override void Awake(){base.Awake();raycastTarget=false;}
        void Update(){if(!InkMainMenu.ReducedMotion)SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();var bounds=rectTransform.rect;float time=InkMainMenu.ReducedMotion?0:Time.unscaledTime;
            for(int i=0;i<54;i++)
            {
                float seed=i*17.137f;
                float px=Mathf.Repeat(Mathf.Sin(seed)*546.2f,1);
                float py=Mathf.Repeat(Mathf.Cos(seed)*782.3f+time*(.008f+.002f*(i%4)),1);
                float x=bounds.xMin+px*bounds.width+Mathf.Sin(time*.25f+seed)*20;
                float y=bounds.yMin+py*bounds.height;
                float size=i%5==0?4.3f:1.2f+i%3;
                float alpha=Mathf.Sin(py*Mathf.PI)*(.10f+(i%5)*.025f);
                var tint=i%5==0?new Color(.61f,.16f,.10f,alpha*1.7f):new Color(.12f,.14f,.13f,alpha);
                var point=new Vector2(x,y);float angle=time*.25f+seed;
                var a=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*size;
                var b=new Vector2(-a.y,a.x)*.45f;
                int index=mesh.currentVertCount;
                mesh.AddVert(point+a,tint,Vector2.zero);mesh.AddVert(point+b,tint,Vector2.zero);
                mesh.AddVert(point-a,tint,Vector2.zero);mesh.AddVert(point-b,tint,Vector2.zero);
                mesh.AddTriangle(index,index+1,index+2);mesh.AddTriangle(index,index+2,index+3);
            }
        }
    }
}
