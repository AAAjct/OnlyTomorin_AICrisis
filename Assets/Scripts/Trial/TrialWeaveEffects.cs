using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        readonly Material[] paletteEdges=new Material[6],paletteGlows=new Material[6];
        static Color StyleColor(byte style)=>style switch
        {
            1=>new Color(.035f,.52f,1),2=>new Color(.12f,1,.50f),3=>new Color(1,.58f,.055f),
            4=>new Color(.60f,.16f,1),5=>new Color(.55f,.85f,1),_=>new Color(1,.012f,.045f)
        };
        void ApplySwordPalette(SwordArtVisual art)
        {
            int style=Mathf.Clamp(art.cue.style,0,5);if(style==0)return;
            if(!paletteEdges[style])
            {
                Color edge=StyleColor((byte)style)*3.8f;edge.a=1;
                paletteEdges[style]=new Material(particles){name="Sword style edge "+style};paletteEdges[style].SetColor("_BaseColor",edge);paletteEdges[style].SetInt("_DstBlend",(int)BlendMode.One);
                edge=StyleColor((byte)style)*2.3f;edge.a=1;
                paletteGlows[style]=new Material(swordGlowMaterial){name="Sword style glow "+style};paletteGlows[style].SetColor("_BaseColor",edge);
            }
            art.core.sharedMaterial=art.filament.sharedMaterial=paletteEdges[style];
            art.glow.sharedMaterial=art.detail.sharedMaterial=paletteGlows[style];
        }
        void UpdateWeaveSigil(SwordArtVisual art)
        {
            float t=Mathf.Clamp01(art.age/art.duration),fade=1-t;
            Vector3 forward=art.cue.forward.normalized,right=Vector3.Cross(Vector3.up,forward).normalized;
            Color tint=StyleColor(art.cue.style);
            for(int i=0;i<65;i++)
            {
                float u=i/64f,a=u*Mathf.PI*2+art.age*3;
                float petals=art.cue.amount==3?6:art.cue.amount==2?3:8;
                float r=(.6f+t*1.9f)*(1+.20f*Mathf.Cos(a*petals));
                Vector3 radial=right*Mathf.Cos(a)+forward*Mathf.Sin(a);
                Vector3 point=art.cue.position+radial*r+Vector3.up*(.18f*Mathf.Sin(a*3));
                art.points[i]=point;art.vertices[i*2]=point-radial*.18f;art.vertices[i*2+1]=point;
                art.colors[i*2]=new Color(.003f,.004f,.008f,fade*.6f);art.colors[i*2+1]=new Color(tint.r*.16f,tint.g*.16f,tint.b*.16f,fade*.35f);
            }
            if(!UploadSwordMesh(art))return;
            PaintLine(art.core,art.points,Color.white,.028f,fade);PaintLine(art.glow,art.points,new Color(1,1,1,.3f),.19f,fade);
            for(int i=0;i<65;i++){float a=i/64f*Mathf.PI*4;art.points[i]=art.cue.position+right*Mathf.Cos(a)*(.25f+t)+Vector3.up*Mathf.Sin(a)*(.25f+t)+forward*(i/64f-.5f);}
            PaintLine(art.filament,art.points,Color.white,.018f,fade);art.detail.enabled=false;
        }
        void DisposeSwordPalette()
        {foreach(var m in paletteEdges)if(m)Destroy(m);foreach(var m in paletteGlows)if(m)Destroy(m);}
    }
}
