using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        bool PlayMartialCue(TrialCombatEvent cue,bool local)
        {
            if(cue.cue!=TrialCombatCue.MartialStroke)return false;
            int move=Mathf.RoundToInt(cue.amount);
            Art(cue,move==TrialPlayer.LowVault?.34f:.26f);
            if(TrialPlayer.IsBodyStrike(move))
            {
                Vector3 point=cue.position+Vector3.up*(move==TrialPlayer.HeelTurn?.18f:1)+cue.forward*.9f;
                InkBloom(point,-cue.forward*.25f,.55f);
                if(move==TrialPlayer.HiltStrike || move==TrialPlayer.ElbowBreak)Burst(point,new Color(.9f,.64f,.20f),8);
                if(local)Rumble(.12f,.22f,.035f);
            }
            return true;
        }
        void UpdateMartialStroke(SwordArtVisual art)
        {
            int move=Mathf.RoundToInt(art.cue.amount);
            float t=Mathf.Clamp01(art.age/art.duration),fade=1-Mathf.SmoothStep(0,1,t);
            Vector3 feet=art.owner?art.owner.transform.position:art.cue.position;
            Vector3 f=art.cue.forward.sqrMagnitude>.01f?art.cue.forward.normalized:Vector3.forward,r=Vector3.Cross(Vector3.up,f);
            bool body=TrialPlayer.IsBodyStrike(move);
            for(int i=0;i<65;i++)
            {
                float u=i/64f,a=Mathf.Lerp(-1.6f,1.6f,u),e=Mathf.Sin(u*Mathf.PI);
                Vector3 p,across;
                if(move==TrialPlayer.SwordRecallCut)
                {
                    a=Mathf.Lerp(-1.9f,1.9f,u);
                    Vector3 radial=r*Mathf.Sin(a)+f*Mathf.Cos(a);
                    p=feet+Vector3.up*(1.2f-u*.45f)+radial*1.6f;across=radial*.09f*e;
                }
                else if(move==TrialPlayer.ParryMirror)
                {
                    float side=art.cue.end.y>=art.cue.position.y?1:-1;
                    p=feet+f*(.85f+Mathf.Sin(u*Mathf.PI)*.7f)+r*Mathf.Lerp(-1.4f,1.4f,u)+Vector3.up*(1+side*Mathf.Lerp(-.8f,.8f,u));
                    across=(Vector3.up-r*side)*.065f*e;
                }
                else if(move==TrialPlayer.SwordRecallRise || move==TrialPlayer.ParryAscend)
                {
                    a=Mathf.Lerp(-2.5f,2.5f,u)+t*.6f;
                    Vector3 radial=r*Mathf.Sin(a)+f*Mathf.Cos(a);
                    p=feet+radial*1.7f+Vector3.up*Mathf.Lerp(.2f,2.1f,u);across=radial*.13f*e;
                }
                else if(move==TrialPlayer.HiltStrike || move==TrialPlayer.ElbowBreak)
                {
                    float spoke=Mathf.Floor(u*8),radial=(u*8-spoke)*(.12f+t*.30f);
                    float angle=spoke*Mathf.PI*.25f;
                    p=feet+Vector3.up*1.05f+f*.95f+(r*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle))*radial;
                    across=Vector3.up*.018f*e;
                }
                else if(move==TrialPlayer.HeelTurn)
                {
                    a=Mathf.Lerp(1.8f,-1.8f,u);Vector3 radial=r*Mathf.Sin(a)+f*Mathf.Cos(a);
                    p=feet+Vector3.up*.14f+radial*(.8f+t*.65f);across=radial*.07f*e;
                }
                else if(move==TrialPlayer.ScissorKick)
                {
                    a=Mathf.Lerp(-1.4f,1.4f,u);
                    p=feet+Vector3.up*.3f+f*(Mathf.Cos(a)*.9f)+r*(Mathf.Sin(a)*.5f)+Vector3.up*Mathf.Sin(a)*.7f;
                    across=r*.04f*e;
                }
                else if(move==TrialPlayer.LowVault)
                {
                    p=Vector3.Lerp(art.cue.position,feet+Vector3.up*.4f,u)-f*(1-u)*.6f+r*Mathf.Sin(u*Mathf.PI)*.18f;
                    across=r*.07f*e;
                }
                else if(move==TrialPlayer.FallingDraw)
                {
                    p=feet+Vector3.up*Mathf.Lerp(1.9f,.12f,u)+f*(.7f+Mathf.Sin(u*Mathf.PI)*1.1f)+r*Mathf.Lerp(.75f,-.75f,u);
                    across=r*.10f*e;
                }
                else if(move==TrialPlayer.SheathTurn)
                {
                    p=feet+Vector3.up*.88f+f*.7f+r*Mathf.Lerp(-1.1f,1.1f,u);
                    across=Vector3.up*.045f*e;
                }
                else
                {
                    float radius=move==TrialPlayer.CloudRake?1.3f:1.55f;
                    a=Mathf.Lerp(-2.3f,2.3f,u)+t*.8f;
                    Vector3 radial=r*Mathf.Sin(a)+f*Mathf.Cos(a);
                    float height=move==TrialPlayer.CloudRake?1.4f:move==TrialPlayer.VaultSweep?.9f:.7f;
                    p=feet+Vector3.up*(height+Mathf.Sin(a)*.3f)+radial*radius;across=radial*.12f*e;
                }
                art.points[i]=p;art.vertices[i*2]=p-across;art.vertices[i*2+1]=p;
                art.colors[i*2]=new Color(.004f,.005f,.007f,fade*.50f*e);art.colors[i*2+1]=new Color(.018f,.025f,.03f,fade*.6f*e);
            }
            if(!UploadSwordMesh(art))return;
            bool perfect=move==TrialPlayer.SwordRecallRise || move==TrialPlayer.SwordRecallCut || move==TrialPlayer.ParryMirror || move==TrialPlayer.ParryAscend;
            PaintLine(art.core,art.points,perfect?new Color(1,.8f,.3f,.88f):new Color(1,1,1,body?.32f:.65f),perfect?.03f:body?.012f:.022f,fade);
            PaintLine(art.glow,art.points,perfect?new Color(1,.12f,.055f,.26f):new Color(1,1,1,.12f),perfect?.13f:body?.04f:.085f,fade);
            art.filament.enabled=false;art.detail.enabled=move==TrialPlayer.ScissorKick;
            if(art.detail.enabled)
            {
                for(int i=0;i<65;i++)art.points[i]=feet+Vector3.up*.6f-(art.points[i]-feet-Vector3.up*.6f)+f*1.0f;
                PaintLine(art.detail,art.points,new Color(1,1,1,.3f),.015f,fade);
            }
        }
    }
}
