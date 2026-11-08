using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        static bool IsSwordMotionCue(TrialCombatCue cue)=>cue==TrialCombatCue.SwordLeap || cue==TrialCombatCue.SwordTurn || cue==TrialCombatCue.SwordDescent || cue==TrialCombatCue.SwordLanding || cue==TrialCombatCue.SandImpact;

        bool PlaySwordMotionCue(TrialCombatEvent cue,bool local)
        {
            if(!IsSwordMotionCue(cue.cue))return false;
            bool leap=cue.cue==TrialCombatCue.SwordLeap,turn=cue.cue==TrialCombatCue.SwordTurn;
            bool descent=cue.cue==TrialCombatCue.SwordDescent;
            Art(cue,leap?.64f:turn?.40f:descent?.48f:.56f);
            if(leap)Burst(cue.position,StyleColor(cue.style)*2,14);
            if(cue.cue==TrialCombatCue.SwordLanding)
            {
                Burst(cue.position,StyleColor(cue.style)*2.2f,24);Sound("landing",cue.position,.92f);
                if(local){game.CameraRig.Kick(.14f);Rumble(.67f,.32f,.11f);}
            }
            // Preserve the blue sand fan, impact sound and damage feedback as well.
            return cue.cue!=TrialCombatCue.SandImpact;
        }

        void UpdateSwordMotionArt(SwordArtVisual art)
        {
            var cue=art.cue;float t=Mathf.Clamp01(art.age/art.duration);
            float fade=1-Mathf.SmoothStep(0,1,t);
            bool leap=cue.cue==TrialCombatCue.SwordLeap,turn=cue.cue==TrialCombatCue.SwordTurn;
            bool descent=cue.cue==TrialCombatCue.SwordDescent,landing=!leap && !turn && !descent;
            if(!landing && art.owner && (art.owner.Guarding || art.owner.Action==FighterAction.Stagger || art.owner.Action==FighterAction.Dodge && !(turn && art.owner.Combo==7)))
            {art.root.gameObject.SetActive(false);return;}
            Vector3 forward=Vector3.ProjectOnPlane(cue.forward,Vector3.up).normalized;
            if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            Vector3 feet=art.owner?art.owner.transform.position:cue.end;
            float scale=Mathf.Clamp(cue.amount,.8f,1.25f),radius=(.3f+Mathf.Sin(t*Mathf.PI*.5f)*2.8f)*scale;
            for(int i=0;i<65;i++)
            {
                float u=i/64f,envelope=Mathf.Sin(u*Mathf.PI),angle;
                Vector3 point,across;
                if(leap)
                {
                    angle=u*Mathf.PI*3.2f-art.age*8;
                    Vector3 radial=right*Mathf.Cos(angle)+forward*Mathf.Sin(angle);
                    float width=(.34f+.48f*envelope)*(1-u*.45f);
                    Vector3 top=feet+Vector3.up*1.05f;
                    top.y=Mathf.Max(top.y,cue.position.y+.9f);
                    point=Vector3.Lerp(cue.position,top,u)+radial*width;
                    across=radial*(.10f*envelope);
                }
                else if(turn)
                {
                    angle=Mathf.Lerp(-Mathf.PI*1.25f,Mathf.PI*.3f,u)+art.age*10;
                    Vector3 radial=right*Mathf.Sin(angle)+forward*Mathf.Cos(angle);
                    point=feet+Vector3.up*(.95f+Mathf.Sin(angle)*.28f)+radial*(1.7f+.3f*u)*scale;
                    across=radial*(.32f*envelope)*scale;
                }
                else if(descent)
                {
                    angle=u*Mathf.PI*4+art.age*19;
                    Vector3 radial=right*Mathf.Sin(angle)+forward*Mathf.Cos(angle);
                    Vector3 tip=feet+Vector3.up*.24f;
                    point=Vector3.Lerp(cue.position,tip,u)+radial*(.13f+.26f*(1-u))*envelope;
                    across=radial*(.11f*envelope);
                }
                else
                {
                    angle=u*Mathf.PI*2;
                    Vector3 radial=right*Mathf.Sin(angle)+forward*Mathf.Cos(angle);
                    float scallop=1+Mathf.Sin(angle*8)*.025f*(1-t);
                    point=cue.position+radial*(radius*scallop)+Vector3.up*.025f;
                    across=radial*(.22f*(1-t));
                }
                art.points[i]=point;art.vertices[i*2]=point-across;art.vertices[i*2+1]=point;
                float opacity=fade*(landing?1:envelope);
                art.colors[i*2]=new Color(.12f,.002f,.015f,opacity*.65f);
                var tint=StyleColor(cue.style);art.colors[i*2+1]=new Color(tint.r*.3f,tint.g*.3f,tint.b*.3f,opacity*.7f);
            }
            if(!UploadSwordMesh(art))return;
            float lineWidth=turn?.055f:descent?.04f:landing?.055f*(1-t*.7f):.032f;
            PaintLine(art.core,art.points,new Color(1,1,1,.85f),lineWidth,fade);
            PaintLine(art.glow,art.points,new Color(1,1,1,.38f),lineWidth*8,fade);
            for(int i=0;i<65;i++)
            {
                float u=i/64f,angle=u*Mathf.PI*2;
                if(leap)art.points[i]=cue.position+(right*Mathf.Sin(angle)+forward*Mathf.Cos(angle))*(.4f+t*1.2f)+Vector3.up*.025f;
                else if(turn)art.points[i]+=Vector3.up*.34f-forward*.08f;
                else if(descent)art.points[i]+=right*(Mathf.Sin(u*Mathf.PI*4+art.age*19+Mathf.PI)*.28f);
                else art.points[i]=cue.position+(right*Mathf.Sin(angle)+forward*Mathf.Cos(angle))*radius*.66f+Vector3.up*.04f;
            }
            PaintLine(art.filament,art.points,new Color(1,1,1,.65f),lineWidth*.55f,fade);
            art.detail.enabled=true;art.detail.positionCount=65;
            for(int i=0;i<65;i++)
            {
                float u=i/64f,angle=u*Mathf.PI*2;
                Vector3 radial=right*Mathf.Sin(angle)+forward*Mathf.Cos(angle);
                if(leap)
                {
                    float twist=u*Mathf.PI*3.2f-art.age*8+Mathf.PI;
                    art.points[i]=Vector3.Lerp(cue.position,feet+Vector3.up,u)+(right*Mathf.Cos(twist)+forward*Mathf.Sin(twist))*(.28f+.3f*Mathf.Sin(u*Mathf.PI));
                }
                else if(turn)art.points[i]=feet+Vector3.up*(.35f+u*.8f)+radial*(1.25f+.15f*Mathf.Sin(angle+art.age*10));
                else if(descent)art.points[i]=new Vector3(feet.x,cue.end.y+.04f,feet.z)+radial*(.32f+(1-t)*.35f);
                else art.points[i]=cue.position+radial*radius*(.82f+.10f*Mathf.Cos(angle*8))+Vector3.up*.06f;
            }
            PaintLine(art.detail,art.points,new Color(1,1,1,landing?.4f:.22f),lineWidth*2,fade);
        }
    }
}
