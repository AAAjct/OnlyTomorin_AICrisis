using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        sealed class SignatureRibbon
        {
            public LineRenderer core,halo;
            public Color color;
            public float until,duration,width;
        }
        readonly SignatureRibbon[] signatureRibbons=new SignatureRibbon[72];
        int nextSignatureRibbon;
        Material signatureMaterial;
        SignatureRibbon Ribbon(Color tint,float width,float life,int count)
        {
            if(!signatureMaterial)signatureMaterial=new Material(Resources.Load<Shader>("Trial/VFX/SignatureEnergy")){name="Signature combat luminous filaments"};
            int slot=nextSignatureRibbon++%signatureRibbons.Length;var r=signatureRibbons[slot];
            if(r==null)
            {
                r=new SignatureRibbon();signatureRibbons[slot]=r;
                var root=new GameObject("Pooled layered combat ribbon "+slot);root.layer=2;root.transform.SetParent(transform,false);
                r.core=RibbonLine(root.transform,"Pearl core");r.halo=RibbonLine(root.transform,"Soft colored wake");
                r.core.widthCurve=r.halo.widthCurve=new AnimationCurve(new Keyframe(0,.025f),new Keyframe(.22f,.8f),new Keyframe(.55f,1),new Keyframe(1,.025f));
            }
            r.core.enabled=r.halo.enabled=true;r.color=tint;r.duration=life;r.until=Time.time+life;r.width=width;
            r.core.positionCount=r.halo.positionCount=count;r.core.widthMultiplier=width*.22f;r.halo.widthMultiplier=width;
            r.core.startColor=r.core.endColor=Color.Lerp(tint,Color.white,.65f);tint.a=.36f;r.halo.startColor=r.halo.endColor=tint;return r;
        }
        LineRenderer RibbonLine(Transform parent,string label)
        {
            var go=new GameObject(label);go.layer=2;go.transform.SetParent(parent,false);
            var line=go.AddComponent<LineRenderer>();line.sharedMaterial=signatureMaterial;line.useWorldSpace=true;line.numCapVertices=3;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        static void RibbonPoint(SignatureRibbon r,int i,Vector3 p){r.core.SetPosition(i,p);r.halo.SetPosition(i,p);}
        public void DodgeWake(Vector3 position,Vector3 direction,Color tint)
        {
            direction=TrialChampion.Planar(direction).normalized;if(direction.sqrMagnitude<.1f)direction=Vector3.back;
            Vector3 side=Vector3.Cross(Vector3.up,direction);
            for(int j=0;j<3;j++)
            {
                var r=Ribbon(tint,j==1?.25f:.11f,.34f+j*.06f,25);
                for(int i=0;i<25;i++)
                {
                    float t=i/24f,a=t*Mathf.PI*1.7f+j*2.1f;
                    Vector3 p=position+direction*(t*2.6f-.7f)+side*(Mathf.Sin(a)*(.28f+.30f*t))+Vector3.up*(.25f+j*.3f+Mathf.Cos(a)*.18f);
                    RibbonPoint(r,i,p);
                }
            }
            Burst(position+Vector3.up*.15f,tint,14);
        }
        public void SignatureSlash(Vector3 position,Vector3 forward,ChampionKind kind,float radius,int variant)
        {
            Color tint=TrialChampionCatalog.Color(kind);forward=TrialChampion.Planar(forward).normalized;
            if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
            Vector3 side=Vector3.Cross(Vector3.up,forward);
            int layers=kind==ChampionKind.Anon?2:kind==ChampionKind.YaYa?3:1;
            for(int j=0;j<layers;j++)
            {
                var r=Ribbon(tint,kind==ChampionKind.Penguin?.7f:.25f,.27f+j*.06f,33);
                for(int i=0;i<33;i++)
                {
                    float t=i/32f,a=Mathf.Lerp(-1.4f,1.4f,t)*(variant%2==0?1:-1);
                    Vector3 p;
                    if(kind==ChampionKind.Taffy)p=position+forward*(t*radius)+side*Mathf.Sin(t*18)*.03f;
                    else if(kind==ChampionKind.FeiBi)p=position+forward*(t*radius*.75f)+side*Mathf.Sin(t*Mathf.PI*2)*.2f+Vector3.up*Mathf.Sin(t*Mathf.PI)*.4f;
                    else if(kind==ChampionKind.Penguin)p=position+side*Mathf.Sin(a)*radius+forward*Mathf.Cos(a)*radius*.65f+Vector3.down*(.6f*Mathf.Sin(t*Mathf.PI));
                    else if(kind==ChampionKind.YaYa)p=position+forward*(t*radius)+side*Mathf.Sin(t*9+j*1.4f)*.8f+Vector3.up*Mathf.Cos(t*8+j)*.4f;
                    else p=position+side*Mathf.Sin(a)*radius+forward*Mathf.Cos(a)*radius*.65f+Vector3.up*(Mathf.Sin(a)*(kind==ChampionKind.Anon?(j==0?.45f:-.45f):.25f));
                    RibbonPoint(r,i,p);
                }
            }
        }
        public void SignatureGlyph(Vector3 position,ChampionKind kind,float radius,int variant)
        {
            position=TrialChampion.Planar(position)+Vector3.up*.085f;Color tint=TrialChampionCatalog.Color(kind);
            int count=kind==ChampionKind.Anon?6:kind==ChampionKind.Taffy?9:49;
            var r=Ribbon(tint,.09f,.65f,count);
            for(int i=0;i<count;i++)
            {
                float t=i/(float)(count-1),a=t*Mathf.PI*2;Vector3 p;
                if(kind==ChampionKind.Anon){float star=(i*2%5)*Mathf.PI*2/5;p=new Vector3(Mathf.Sin(star),0,Mathf.Cos(star))*radius;}
                else if(kind==ChampionKind.Taffy){float q=i%4*Mathf.PI*.5f;p=new Vector3(Mathf.Sin(q),0,Mathf.Cos(q))*radius*(i<5?1:.35f);}
                else if(kind==ChampionKind.FeiBi){float petal=radius*(.65f+.35f*Mathf.Cos(a*6));p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*petal;}
                else if(kind==ChampionKind.NuoNuo){p=new Vector3(Mathf.Sin(a)*(i>24?.48f:1),0,Mathf.Cos(a))*radius;}
                else if(kind==ChampionKind.Penguin){float tooth=radius*(i%4==0?1:.52f);p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*tooth;}
                else if(kind==ChampionKind.YaYa){float weave=radius*(.6f+.3f*Mathf.Sin(a*8));p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*weave;}
                else{p=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius*(1-t*.65f);}
                RibbonPoint(r,i,position+p);
            }
        }
        void PolishPlayerArt(TrialCombatEvent cue)
        {
            if(cue.cue!=TrialCombatCue.ThunderDragon && cue.cue!=TrialCombatCue.SpiralSword && cue.cue!=TrialCombatCue.SakuraScatter && cue.cue!=TrialCombatCue.ThrustTrace)return;
            bool thunder=cue.cue==TrialCombatCue.ThunderDragon,petal=cue.cue==TrialCombatCue.SakuraScatter;
            Color color=thunder?ThunderGold:petal?new Color(1,.48f,.72f):new Color(.4f,.85f,1);
            if(cue.style>0 && !thunder && !petal)color=StyleColor(cue.style);
            Vector3 forward=cue.forward.sqrMagnitude>.1f?cue.forward.normalized:Vector3.forward;
            Vector3 side=Vector3.Cross(Vector3.up,forward).normalized;
            for(int j=0;j<2;j++)
            {
                var r=Ribbon(color,thunder?.24f:.12f,thunder?.55f:.4f,41);
                for(int i=0;i<41;i++)
                {
                    float t=i/40f,a=t*Mathf.PI*4+j*Mathf.PI;
                    float radius=Mathf.Sin(t*Mathf.PI)*(thunder?.8f:.5f);
                    Vector3 p=cue.position+forward*(t*(thunder?3.2f:2.1f))+(side*Mathf.Sin(a)+Vector3.up*Mathf.Cos(a))*radius;
                    RibbonPoint(r,i,p);
                }
            }
            Burst(cue.position,color,thunder?32:18);
        }
        void TickSignatureEffects()
        {
            foreach(var r in signatureRibbons)
            {
                if(r==null || !r.core.enabled)continue;
                float left=r.until-Time.time;if(left<=0){r.core.enabled=r.halo.enabled=false;continue;}
                r.halo.enabled=!TrialGraphicsBudget.Performance;
                float fade=Mathf.Clamp01(left/r.duration);Color c=Color.Lerp(r.color,Color.white,.65f);c.a=fade;r.core.startColor=r.core.endColor=c;
                c=r.color;c.a=fade*.36f;r.halo.startColor=r.halo.endColor=c;
                r.halo.widthMultiplier=r.width*(1+(1-fade)*.8f);r.core.widthMultiplier=r.width*.22f*fade;
            }
        }
    }
}
