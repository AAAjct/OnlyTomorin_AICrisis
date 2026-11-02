using UnityEngine;

namespace TomorinTrial
{
    // One broad stroke; no textures, particles, shadow layers or animated geometry.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InkButtonWash : UnityEngine.UI.MaskableGraphic
    {
        static readonly Vector2[] edge={new Vector2(0,.12f),new Vector2(.04f,.04f),new Vector2(.72f,.08f),new Vector2(.98f,.02f),new Vector2(.96f,.23f),new Vector2(1,.82f),new Vector2(.94f,.94f),new Vector2(.24f,.90f),new Vector2(.01f,.97f),new Vector2(.025f,.66f)};
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();var r=rectTransform.rect;
            mesh.AddVert(r.center,color,Vector2.zero);
            foreach(var p in edge)mesh.AddVert(new Vector2(r.xMin+p.x*r.width,r.yMax-p.y*r.height),color,Vector2.zero);
            for(int i=0;i<edge.Length;i++)mesh.AddTriangle(0,i+1,(i+1)%edge.Length+1);
        }
    }
}
