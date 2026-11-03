using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEffects : MonoBehaviour
    {
        readonly List<ParticleSystem> pool=new List<ParticleSystem>();
        readonly Dictionary<string,AudioClip> sounds=new Dictionary<string,AudioClip>();
        readonly List<AudioSource> sources=new List<AudioSource>();
        Material particles;
        TrialGame game;
        float stopUntil;
        int nextSource;

        public void Initialize(TrialGame owner)
        {
            game=owner;
            particles=new Material(owner.ParticleShader);
            particles.SetFloat("_Surface",1);particles.SetFloat("_Blend",0);particles.SetOverrideTag("RenderType","Transparent");particles.renderQueue=3000;
            particles.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);particles.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);particles.SetInt("_ZWrite",0);
            particles.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            for(int i=0;i<10;i++)
            {
                var obj=new GameObject("Spark pool "+i);obj.transform.SetParent(transform,false);
                var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.playOnAwake=false;main.loop=false;main.duration=.5f;main.startLifetime=new ParticleSystem.MinMaxCurve(.18f,.55f);main.startSpeed=new ParticleSystem.MinMaxCurve(1.5f,4.5f);main.startSize=new ParticleSystem.MinMaxCurve(.025f,.065f);main.gravityModifier=.5f;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=100;
                var emission=ps.emission;emission.enabled=false;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.10f;
                var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.35f,.05f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=particles;pool.Add(ps);
            }
            CreateSound("swing",.32f,180,.23f,1);CreateSound("hit",.22f,90,.35f,2);
            CreateSound("parry",.65f,820,.29f,3);CreateSound("warning",.36f,280,.14f,3);
            CreateSound("dodge",.27f,75,.13f,1);CreateSound("heal",.9f,520,.13f,3);CreateSound("death",.9f,65,.24f,2);
            for(int i=0;i<10;i++){var voice=new GameObject("SFX voice "+i);voice.transform.SetParent(transform,false);var s=voice.AddComponent<AudioSource>();s.spatialBlend=.65f;s.rolloffMode=AudioRolloffMode.Linear;s.minDistance=2;s.maxDistance=24;s.volume=.55f;sources.Add(s);}
            CreateAmbience();
            InitializeImpactEffects();
            AmbientParticles(owner.World.Root,owner.World.ShrinePosition+Vector3.up,26,new Color(1,.58f,.16f),.035f,1.2f);
            AmbientParticles(owner.World.Root,new Vector3(0,5,5),100,new Color(.65f,.11f,.035f),.065f,.7f,24);
        }

        void CreateSound(string key,float duration,float frequency,float volume,int kind)
        {
            const int rate=22050;var samples=new float[(int)(duration*rate)];var random=new System.Random(key.Length*79+kind);
            float filtered=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate,p=t/duration,n=(float)random.NextDouble()*2-1;
                filtered=Mathf.Lerp(filtered,n,.18f);
                float wave=kind==1?filtered*(Mathf.Sin(p*Mathf.PI)):kind==2?Mathf.Sin(2*Mathf.PI*(frequency*t-frequency*.30f*t*t))+n*.45f:Mathf.Sin(2*Mathf.PI*frequency*t)+Mathf.Sin(2*Mathf.PI*frequency*2.71f*t)*.3f;
                float envelope=Mathf.Min(1,t/.008f)*Mathf.Exp(-p*(kind==3?5:10));
                samples[i]=wave*envelope*volume;
            }
            var clip=AudioClip.Create("Original · "+key,samples.Length,1,rate,false);clip.SetData(samples,0);sounds[key]=clip;
        }

        void CreateAmbience()
        {
            const int rate=11025;var samples=new float[rate*8];var random=new System.Random(36);float wind=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;wind=Mathf.Lerp(wind,(float)random.NextDouble()*2-1,.018f);
                samples[i]=wind*.10f+Mathf.Sin(2*Mathf.PI*55*t)*.018f+Mathf.Sin(2*Mathf.PI*82.5f*t)*.008f;
            }
            var clip=AudioClip.Create("Original · night wind and drone",samples.Length,1,rate,false);clip.SetData(samples,0);sounds["ambience"]=clip;
            var source=gameObject.AddComponent<AudioSource>();source.clip=clip;source.volume=.35f;source.loop=true;source.spatialBlend=0;source.Play();
        }

        void AmbientParticles(Transform parent,Vector3 point,int count,Color tint,float size,float speed,float radius=.25f)
        {
            var obj=new GameObject("Drifting incense and maple leaves");obj.transform.SetParent(parent);obj.transform.position=point;
            var ps=obj.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=radius>1?12:2;main.startSpeed=speed;main.startSize=size;main.startColor=tint;main.maxParticles=count*2;main.simulationSpace=ParticleSystemSimulationSpace.World;main.gravityModifier=radius>1?.015f:-.03f;
            var shape=ps.shape;shape.shapeType=radius>1?ParticleSystemShapeType.Box:ParticleSystemShapeType.Cone;shape.scale=radius>1?new Vector3(radius,3,55):Vector3.one*.3f;
            var emission=ps.emission;emission.rateOverTime=count/(radius>1?12f:2);
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=particles;
        }

        public void Burst(Vector3 position,Color tint,int count)
        {
            if(Application.isMobilePlatform)count=Mathf.Max(1,Mathf.CeilToInt(count*.55f));
            ParticleSystem selected=pool[0];foreach(var ps in pool) if(!ps.IsAlive()){selected=ps;break;}
            selected.transform.position=position;selected.Play();var emit=new ParticleSystem.EmitParams{startColor=tint};selected.Emit(emit,count);
        }
        public void Sound(string key,Vector3 point,float pitch=1)
        {
            if(!sounds.TryGetValue(key,out var clip))return;
            var source=sources[nextSource++%sources.Count];source.transform.position=point;source.pitch=pitch;source.PlayOneShot(clip);
        }
        public void HitStop(float duration){stopUntil=Mathf.Max(stopUntil,Time.unscaledTime+duration);}
        public void PlayCombat(TrialCombatEvent value,bool localPlayer)
        {
            if(TrialNetworkManager.SessionActive)
            {
                float hold=value.cue==TrialCombatCue.Parry?.085f:value.cue==TrialCombatCue.Block?.03f:value.cue==TrialCombatCue.Kill?.09f:value.cue==TrialCombatCue.HeavyHit?.055f:value.cue==TrialCombatCue.Hit?.035f:0;
                if(hold>0)TrialNetworkManager.Current.PlayerById(value.player)?.HoldCombat(hold);
            }
            if(PlayImpactCue(value,localPlayer))return;
            if(value.cue==TrialCombatCue.Parry)
            {
                Burst(value.position+value.forward+Vector3.up,new Color(1,.86f,.38f),45);
                Sound("parry",value.position);
                SlashEffect(value.position+Vector3.up,value.forward,new Color(1,.87f,.4f),2.3f,true);
                if(localPlayer){game.CameraRig.Kick(.55f);HitStop(.095f);game.CameraRig.Pulse(5);Rumble(.45f,.85f,.13f);game.Hud.Flash(new Color(1,.88f,.5f,.13f));game.Hud.Notify("完美弹反",.6f);}
            }
            else if(value.cue==TrialCombatCue.Block)
            {
                Sound("parry",value.position,.65f);
                Burst(value.position+Vector3.up,new Color(1,.8f,.5f),12);
                if(localPlayer){game.CameraRig.Kick(.18f);Rumble(.15f,.4f,.06f);HitStop(.025f);}
            }
            else if(localPlayer && value.cue==TrialCombatCue.GuardBreak)game.Hud.Notify("架势崩溃",1.2f);
            else if(localPlayer && value.cue==TrialCombatCue.Death)game.OnPlayerDied();
        }
        public void TickTime()
        {TickImpactEffects();Time.timeScale=game.Mode==TrialMode.Victory?0:TrialNetworkManager.SessionActive?(Time.unscaledTime<slowUntil?.35f:1):game.Mode==TrialMode.Playing?(Time.unscaledTime<stopUntil?.08f:Time.unscaledTime<slowUntil?.30f:1):0;}
        void OnDestroy(){DisposeImpactEffects();Time.timeScale=1;foreach(var clip in sounds.Values)if(clip)Destroy(clip);if(particles)Destroy(particles);}
    }
}
