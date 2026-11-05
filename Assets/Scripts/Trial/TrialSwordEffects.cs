using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        sealed class SwordVisual
        {
            public Transform root;
            public LineRenderer edge,halo;
            public Vector3 direction;
            public TrialCombatCue cue;
            public float remaining;
        }
        readonly SwordVisual[] swordVisuals=new SwordVisual[96];
        int nextSwordVisual;
        ParticleSystem petals;
        Mesh petalMesh;

        bool PlaySwordCue(TrialCombatEvent value,bool local)
        {
            if(PlaySwordArtCue(value,local))return true;
            var cue=value.cue;
            if(cue==TrialCombatCue.CrimsonBlade || cue==TrialCombatCue.MoonBlade || cue==TrialCombatCue.BlueSand)
            {
                int slot=nextSwordVisual++%swordVisuals.Length;
                var visual=swordVisuals[slot];
                if(visual==null)
                {
                    var root=new GameObject("Sword flight "+slot).transform;root.SetParent(transform,false);
                    visual=new SwordVisual{root=root};
                    visual.halo=SwordLine(root,"Aura");visual.edge=SwordLine(root,"Blade");swordVisuals[slot]=visual;
                }
                visual.root.gameObject.SetActive(true);visual.root.SetPositionAndRotation(value.position,Quaternion.LookRotation(value.forward));
                visual.direction=value.forward;visual.cue=cue;visual.remaining=TrialSwordFlight.Lifetime(cue);
                bool sand=cue==TrialCombatCue.BlueSand,moon=cue==TrialCombatCue.MoonBlade;
                int count=sand?5:25;visual.edge.positionCount=visual.halo.positionCount=count;
                for(int i=0;i<count;i++)
                {
                    float a=Mathf.Lerp(-1.45f,1.45f,(float)i/(count-1));
                    Vector3 point=sand?(i==0 || i==4?new Vector3(-.07f,0,0):i==1?new Vector3(0,.13f,.02f):i==2?new Vector3(.08f,0,0):new Vector3(0,-.08f,-.02f)):
                        new Vector3(Mathf.Sin(a)*(moon?1.3f:1.1f),Mathf.Sin(a)*(moon?.08f:.36f),Mathf.Cos(a)*.55f);
                    visual.edge.SetPosition(i,point);visual.halo.SetPosition(i,point);
                }
                visual.edge.widthMultiplier=sand?.07f:.10f;visual.halo.widthMultiplier=sand?.14f:.27f;
                visual.edge.widthCurve=visual.halo.widthCurve=new AnimationCurve(new Keyframe(0,.08f),new Keyframe(.5f,1),new Keyframe(1,.08f));
                Color tint=sand?new Color(.12f,.28f,.55f):moon?new Color(1,.68f,.81f):new Color(.68f,.035f,.10f);
                visual.edge.startColor=visual.edge.endColor=tint;tint.a=.25f;visual.halo.startColor=visual.halo.endColor=tint;
                if(!sand)Sound("swing",value.position,moon?.82f:1.1f);
                return true;
            }
            if(cue==TrialCombatCue.SakuraScatter)
            {
                Vector3 end=value.end==Vector3.zero?value.position+value.forward*2:value.end;
                SakuraCorridor(value.position,end,value.forward);
                Sound("swing",value.position,1.35f);return true;
            }
            if(cue==TrialCombatCue.SandImpact)
            {
                SlashEffect(value.position+Vector3.up*.06f,value.forward,new Color(.16f,.30f,.57f),3.1f,true);
                Burst(value.position,new Color(.10f,.17f,.34f),65);Sound("landing",value.position,.8f);
                if(local){game.CameraRig.Kick(.38f);Rumble(.5f,.65f,.14f);}return true;
            }
            return false;
        }
        LineRenderer SwordLine(Transform root,string label)
        {
            var obj=new GameObject(label);obj.transform.SetParent(root,false);
            var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=particles;line.useWorldSpace=false;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.numCapVertices=2;return line;
        }
        void SakuraCut(Vector3 point,Vector3 forward,int index)
        {
            var arc=arcs[nextArc++%arcs.Length];
            Vector3 right=Vector3.Cross(Vector3.up,forward).normalized;
            float angle=(index*36+18)*Mathf.Deg2Rad;
            Vector3 direction=right*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle);
            for(int i=0;i<slashPoints.Length;i++)
            {
                float t=Mathf.Lerp(-1,1,(float)i/(slashPoints.Length-1));
                slashPoints[i]=point+direction*t*(1.15f+index*.06f)+forward*((1-t*t)*.18f);
            }
            arc.line.SetPositions(slashPoints);arc.line.widthMultiplier=.035f;
            arc.tint=new Color(1,.55f,.74f);arc.until=Time.unscaledTime+.22f;arc.lifetime=.22f;
            arc.line.startColor=arc.line.endColor=arc.tint;arc.line.enabled=true;
        }
        void TickSwordEffects()
        {
            TickSwordArtEffects();
            float dt=Time.deltaTime;
            foreach(var visual in swordVisuals)
            {
                if(visual==null || !visual.root.gameObject.activeSelf)continue;
                float travel=TrialSwordFlight.Speed(visual.cue)*Mathf.Min(dt,visual.remaining);
                bool wall=Physics.SphereCast(visual.root.position,TrialSwordFlight.Radius(visual.cue),visual.direction,out _,travel,1,QueryTriggerInteraction.Ignore);
                visual.root.position+=visual.direction*travel;visual.remaining-=dt;
                if(wall || visual.remaining<=0)visual.root.gameObject.SetActive(false);
            }
        }
        void EmitPetals(Vector3 point,Vector3? end=null)
        {
            if(!petals)
            {
                var obj=new GameObject("Sakura scatter petals");obj.transform.SetParent(transform,false);petals=obj.AddComponent<ParticleSystem>();
                petals.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=petals.main;main.playOnAwake=false;main.loop=false;main.startLifetime=new ParticleSystem.MinMaxCurve(1.1f,2.1f);main.startSize=new ParticleSystem.MinMaxCurve(.12f,.24f);main.gravityModifier=.07f;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=1200;main.startRotation3D=true;
                var emission=petals.emission;emission.enabled=false;var shape=petals.shape;shape.enabled=false;
                var rotation=petals.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;rotation.x=2;rotation.y=4;rotation.z=3;
                var color=petals.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.8f,.3f,.55f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
                // A curved, heart-notched cherry petal, with pale rim and a rose base.
                petalMesh=new Mesh{name="Notched sakura petal"};
                var outline=new[]{new Vector3(0,-.6f,0),new Vector3(-.22f,-.25f,.05f),new Vector3(-.40f,.12f,.08f),new Vector3(-.33f,.47f,.04f),new Vector3(-.14f,.63f,0),new Vector3(0,.45f,.02f),new Vector3(.14f,.63f,0),new Vector3(.33f,.47f,.04f),new Vector3(.40f,.12f,.08f),new Vector3(.22f,-.25f,.05f)};
                var vertices=new Vector3[outline.Length+1];var colors=new Color[vertices.Length];vertices[0]=new Vector3(0,.08f,.12f);colors[0]=new Color(1,.50f,.66f);
                var triangles=new int[outline.Length*6];
                for(int i=0;i<outline.Length;i++){vertices[i+1]=outline[i];colors[i+1]=i==0?new Color(.88f,.25f,.43f):new Color(1,.9f,.94f);int next=(i+1)%outline.Length+1,j=i*6;triangles[j]=0;triangles[j+1]=i+1;triangles[j+2]=next;triangles[j+3]=next;triangles[j+4]=i+1;triangles[j+5]=0;}
                petalMesh.vertices=vertices;petalMesh.colors=colors;petalMesh.triangles=triangles;petalMesh.RecalculateNormals();
                var renderer=petals.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particles;renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=petalMesh;
            }
            petals.Play();int count=Application.isMobilePlatform?70:150;
            Vector3 finish=end??point,forward=(finish-point).normalized,right=Vector3.Cross(Vector3.up,forward).normalized;
            for(int i=0;i<count;i++)
            {
                float t=(i+.5f)/count,angle=t*Mathf.PI*7+Random.Range(-.25f,.25f);Vector3 outward=right*Mathf.Cos(angle)+Vector3.up*Mathf.Sin(angle);
                Vector3 centre=Vector3.Lerp(point,finish,t);
                petals.Emit(new ParticleSystem.EmitParams{position=centre+outward*Random.Range(.15f,.85f),velocity=outward*Random.Range(.5f,1.6f)+Vector3.up*.6f+forward*Random.Range(.3f,1.4f),startColor=Color.Lerp(new Color(1,.62f,.76f),Color.white,Random.value*.55f),rotation3D=Random.insideUnitSphere*Mathf.PI},1);
            }
        }
    }
}
