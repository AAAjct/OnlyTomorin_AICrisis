using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed class TrialSpellIcon : Graphic
    {
        int slot;
        internal void SetSlot(int value){slot=value;SetVerticesDirty();}

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var shade=color;
            shade.a*=.23f;
            var glint=Color.Lerp(color,Color.white,.48f);
            switch(slot)
            {
                case 0:
                    // Coffin seen from above, with its lid and vertical seal.
                    Quad(P(-.6f,.53f),P(.07f,.87f),P(.64f,.52f),P(-.02f,.2f),shade);
                    Quad(P(-.6f,.53f),P(-.02f,.2f),P(-.02f,-.86f),P(-.6f,-.55f),shade);
                    Line(-.6f,.53f,.07f,.87f,.07f,glint);
                    Line(.07f,.87f,.64f,.52f,.07f,glint);
                    Line(.64f,.52f,-.02f,.2f,.07f,color);
                    Line(-.02f,.2f,-.6f,.53f,.07f,color);
                    Line(-.6f,.53f,-.6f,-.55f,.07f,color);
                    Line(-.02f,.2f,-.02f,-.86f,.07f,glint);
                    Line(.64f,.52f,.64f,-.54f,.07f,color);
                    Line(-.6f,-.55f,-.02f,-.86f,.07f,color);
                    Line(-.02f,-.86f,.64f,-.54f,.07f,color);
                    Line(.30f,.06f,.30f,-.49f,.05f,color);
                    Line(.19f,-.26f,.43f,-.15f,.05f,color);
                    break;
                case 1:
                    // Separate facets leave the cracks visible at small sizes.
                    Triangle(P(-.06f,.89f),P(-.67f,.14f),P(-.09f,.21f),color);
                    Triangle(P(.07f,.87f),P(.02f,.24f),P(.62f,.36f),shade);
                    Triangle(P(-.64f,.03f),P(-.03f,.12f),P(-.25f,-.76f),shade);
                    Triangle(P(.08f,.13f),P(.58f,.26f),P(.24f,-.35f),glint);
                    Triangle(P(.04f,.02f),P(.14f,-.41f),P(-.15f,-.8f),color);
                    Triangle(P(.48f,-.28f),P(.8f,-.46f),P(.42f,-.72f),color);
                    Line(.08f,.86f,.61f,.35f,.045f,color);
                    Line(-.64f,.03f,-.25f,-.76f,.045f,color);
                    break;
            }

            Vector2 P(float x,float y)=>new Vector2(x,y);
            Vector2 Scale(Vector2 point)=>Vector2.Scale(point,rectTransform.rect.size*.5f);
            void Triangle(Vector2 a,Vector2 b,Vector2 c,Color tint)
            {
                int index=mesh.currentVertCount;
                mesh.AddVert(Scale(a),tint,Vector2.zero);
                mesh.AddVert(Scale(b),tint,Vector2.zero);
                mesh.AddVert(Scale(c),tint,Vector2.zero);
                mesh.AddTriangle(index,index+1,index+2);
            }
            void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
            {Triangle(a,b,c,tint);Triangle(a,c,d,tint);}
            void Line(float x,float y,float endX,float endY,float width,Color tint)
            {
                var a=P(x,y);
                var b=P(endX,endY);
                var offset=Vector2.Perpendicular((b-a).normalized)*width*.5f;
                Quad(a-offset,a+offset,b+offset,b-offset,tint);
            }
        }
    }
}
