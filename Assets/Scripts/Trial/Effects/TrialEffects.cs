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
        float stopUntil,perfectDodgeSlowUntil;
        int nextSource,nextWarningSource;

        public void Initialize(TrialGame owner)
        {
            game=owner;
            particles=new Material(owner.ParticleShader);
            particles.SetFloat("_Surface",1);particles.SetFloat("_Blend",0);particles.SetOverrideTag("RenderType","Transparent");particles.renderQueue=3000;
            particles.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);particles.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);particles.SetInt("_ZWrite",0);
            particles.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particles.SetInt("_Cull",0);
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
            CreateSound("parry",.65f,820,.29f,3);CreateSound("block",.18f,540,.38f,3);CreateSound("warning",.36f,280,.14f,3);
            CreateSound("dodge",.27f,75,.13f,1);CreateSound("heal",.9f,520,.13f,3);CreateSound("death",.9f,65,.24f,2);
            CreateSound("spellThunder",.5f,126,.32f,2);
            CreateSound("spellGlass",.67f,1750,.22f,3);
            CreateSound("spellCoffin",.85f,82,.30f,2);
            for(int i=0;i<14;i++){var voice=new GameObject("SFX voice "+i);voice.transform.SetParent(transform,false);var s=voice.AddComponent<AudioSource>();s.spatialBlend=.65f;s.rolloffMode=AudioRolloffMode.Linear;s.minDistance=2;s.maxDistance=24;s.volume=.55f;sources.Add(s);}
            CreateAmbience();
            InitializeImpactEffects();InitializeTides();LoadCombatAudio();InitializeSpellVoices();
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
            var clip=AudioClip.Create("Original · "+key,samples.Length,1,rate,false);clip.SetData(samples,0);sounds[key]=clip;generatedSounds.Add(clip);
        }

        void CreateAmbience()
        {
            const int rate=11025;var samples=new float[rate*8];var random=new System.Random(36);float wind=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;wind=Mathf.Lerp(wind,(float)random.NextDouble()*2-1,.018f);
                samples[i]=wind*.10f+Mathf.Sin(2*Mathf.PI*55*t)*.018f+Mathf.Sin(2*Mathf.PI*82.5f*t)*.008f;
            }
            var clip=AudioClip.Create("Original · night wind and drone",samples.Length,1,rate,false);clip.SetData(samples,0);sounds["ambience"]=clip;generatedSounds.Add(clip);
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

        int burstFrame=-1,burstParticles;
        public void Burst(Vector3 position,Color tint,int count)
        {
            if(Application.isMobilePlatform)count=Mathf.Max(1,Mathf.CeilToInt(count*.55f));
            count=Mathf.Max(1,Mathf.CeilToInt(count*TrialGraphicsBudget.EffectDensity));
            if(burstFrame!=Time.frameCount){burstFrame=Time.frameCount;burstParticles=0;}
            count=Mathf.Min(count,(TrialGraphicsBudget.Performance?90:180)-burstParticles);
            if(count<=0)return;burstParticles+=count;
            ParticleSystem selected=pool[0];foreach(var ps in pool) if(!ps.IsAlive()){selected=ps;break;}
            selected.transform.position=position;selected.Play();var emit=new ParticleSystem.EmitParams{startColor=tint};selected.Emit(emit,count);
        }
        public void Sound(string key,Vector3 point,float pitch=1)
        {
            if(!sounds.TryGetValue(key,out var clip))return;
            // A fan of simultaneous projectiles has one launch cue, not several stacked copies.
            if(key=="skillCrimson" || key=="skillMoon")
            {
                if(nextFlightSound.TryGetValue(key,out float next) && Time.unscaledTime<next)return;
                nextFlightSound[key]=Time.unscaledTime+.075f;
            }
            clip=NextCombatClip(key,clip);
            bool boss=key=="warning" || key=="tideCharge" || key=="tideImpact" || key=="bubbleCast" || key=="bubblePop" || key=="heavy";
            bool clash=key=="parry" || key=="block" || key=="counter";
            bool attack=ComboAudioSet.Contains(key) || key=="swing" || key=="hit" || key=="impactHeavy" || key=="finish" || key=="charge" || key=="landing";
            var source=sources[boss?10+nextWarningSource++%4:nextSource++%10];
            float gain=TrialPreferences.AudioLevel(0)*(boss?TrialPreferences.AudioLevel(3):clash?TrialPreferences.AudioLevel(2):attack?TrialPreferences.AudioLevel(1):1);
            source.volume=(boss?.92f:key=="block"?.82f:key=="footstep"?.24f:.62f)*gain;source.priority=boss?24:128;
            source.spatialBlend=boss?.18f:.65f;source.minDistance=boss?8:2;source.maxDistance=boss?45:24;
            if(attack){pitch*=.975f+(float)audioRandom.NextDouble()*.05f;source.volume*=.96f+(float)audioRandom.NextDouble()*.08f;}
            source.transform.position=point;source.pitch=pitch;source.Stop();source.clip=clip;source.Play();
        }
        public void HitStop(float duration){stopUntil=Mathf.Max(stopUntil,Time.unscaledTime+duration);}
        public void PlayCombat(TrialCombatEvent value,bool localPlayer)
        {
            if(PlaySpellCue(value,localPlayer))return;
            PolishPlayerArt(value);
            if(PlaySwordCue(value,localPlayer))return;
            if(TrialNetworkManager.SessionActive)
            {
                float hold=value.cue==TrialCombatCue.Parry?.115f:value.cue==TrialCombatCue.Block?.03f:value.cue==TrialCombatCue.Kill || value.cue==TrialCombatCue.FinisherHit?.12f:value.cue==TrialCombatCue.HeavyHit?.095f:value.cue==TrialCombatCue.Hit?.065f:0;
                if(value.cue==TrialCombatCue.HeavyHit && value.amount==TrialCombatEvent.BlinkSlashImpact)hold=TrialCombatEvent.BlinkSlashHitStop;
                if(hold>0 && value.amount!=TrialCombatEvent.ContactOnlyImpact)TrialNetworkManager.Current.PlayerById(value.player)?.HoldCombat(hold);
            }
            if(PlayImpactCue(value,localPlayer))return;
            if(value.cue==TrialCombatCue.Parry)
            {
                Vector3 contact=value.position+value.forward*.8f+Vector3.up;
                InkBloom(contact,-value.forward*.8f,2.1f);
                Burst(contact,new Color(2.4f,.16f,.045f),72);
                Sound("parry",value.position,.86f);Sound("impactHeavy",value.position,.68f);
                SlashEffect(contact,value.forward,new Color(1,.12f,.08f),2.7f,true);
                SlashEffect(contact,value.forward,new Color(1,.76f,.38f),1.6f);
                Art(new TrialCombatEvent{cue=TrialCombatCue.CrescentCut,position=contact,forward=value.forward,player=value.player},.24f);
                if(localPlayer){game.CameraRig.BladeClash();HitStop(.12f);Rumble(.8f,1,.19f);game.Hud.Flash(new Color(1,.45f,.22f,.15f));game.Hud.Notify("完美格挡 · 下一击强化",1.5f);}
            }
            else if(value.cue==TrialCombatCue.Block)
            {
                Vector3 direction=Vector3.ProjectOnPlane(value.forward,Vector3.up).normalized;
                Vector3 center=value.position+Vector3.up;
                Vector3 contact=center+direction*.65f;
                Sound("block",contact);
                Burst(contact,new Color(1.55f,.95f,.42f),32);
                SlashEffect(center,direction,new Color(.48f,.78f,1),.85f,width:.10f,duration:.15f);
                SlashEffect(contact,direction,new Color(1,.85f,.55f),.32f,ring:true,duration:.12f);
                if(localPlayer){game.CameraRig.Kick(.18f);Rumble(.15f,.4f,.06f);HitStop(.025f);}
            }
            else if(localPlayer && value.cue==TrialCombatCue.GuardBreak)game.Hud.Notify("架势崩溃",1.2f);
            else if(localPlayer && value.cue==TrialCombatCue.Death)game.OnPlayerDied();
        }
        public void TickTime()
        {
            TickSignatureEffects();TickImpactEffects();TickTides();TickCombatAudio();TickSwordEffects();TickSpellVisuals();
            float now=Time.unscaledTime;
            if(game.HasResult)Time.timeScale=game.ResultInProgress?TrialGame.ResultSlowScale:0;
            else if(TrialNetworkManager.SessionActive)Time.timeScale=now<slowUntil?.35f:1;
            else if(game.Mode!=TrialMode.Playing || game.Player.ChoosingBoon)Time.timeScale=0;
            else if(now<stopUntil)Time.timeScale=.08f;
            else if(now<slowUntil)Time.timeScale=.30f;
            else Time.timeScale=Mathf.Lerp(.30f,1,Mathf.InverseLerp(perfectDodgeSlowUntil,perfectDodgeSlowUntil+.065f,now));
        }
        public void ResumeSwordReturn(){stopUntil=slowUntil=perfectDodgeSlowUntil=0;}
        void OnDestroy(){if(signatureMaterial)Destroy(signatureMaterial);DisposeImpactEffects();DisposeSwordArtEffects();DisposeSpellVisuals();Time.timeScale=1;foreach(var clip in generatedSounds)if(clip)Destroy(clip);if(particles)Destroy(particles);if(petalMesh)Destroy(petalMesh);}
    }
}
