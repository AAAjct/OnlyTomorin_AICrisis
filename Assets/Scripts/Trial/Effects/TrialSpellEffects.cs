using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        static readonly Color MirrorTint=new Color(.35f,1.7f,1.85f);
        readonly List<SpellVisual> spellVisuals=new List<SpellVisual>();
        Material spellSurface;
        Mesh spellShard,mirrorPrism;
        AudioSource coffinVoice,mirrorVoice,coffinBreakAudio;
        bool coffinVoiceStarted,coffinVoicePaused;
        float nextMirrorSound;
        internal float CoffinChantDuration=>coffinVoice && coffinVoice.clip?coffinVoice.clip.length:23.55f;
        internal float MirrorLeadIn=>mirrorVoice ? .70f : .24f;

        sealed class SpellVisual
        {
            public TrialCombatEvent Cue;
            public Transform Root;
            public Renderer Surface;
            public MaterialPropertyBlock Tint;
            public MaterialPropertyBlock[] ModelMaterials;
            public Mesh ModelMesh;
            public MeshFilter CoffinFilter;
            public List<SpellVisual> CoffinPrisms;
            public LineRenderer[] Lines;
            public Vector3 Start,End,Size,Drift,Spin;
            public float Age,Duration;
            public bool RisingCoffin,CoffinPrepared,PrismSlash;
        }

        void InitializeSpellVoices()
        {
            coffinVoice=SpellVoice("blackCoffinChant");
            coffinRiseBed=SpellVoice("blackCoffinRiseBed");
            mirrorVoice=SpellVoice("mirrorInvocation");
            coffinBreakAudio=SpellVoice("blackCoffinImpact",true);
            InitializeCoffinVisuals();
            if(mirrorVoice)sounds["mirrorInvocation"]=mirrorVoice.clip;
        }

        AudioSource SpellVoice(string resource,bool positional=false)
        {
            var clip=Resources.Load<AudioClip>("Trial/Audio/"+resource);
            if(!clip)return null;
            var host=gameObject;
            if(positional)
            {
                host=new GameObject(resource);
                host.transform.SetParent(transform,false);
            }
            var source=host.AddComponent<AudioSource>();
            source.playOnAwake=false;
            source.clip=clip;
            source.priority=16;
            return source;
        }

        internal void UpdateCoffinVoice(bool chanting,bool audible,float elapsed)
        {
            UpdateCoffinRiseAudio(chanting,audible,elapsed);
            if(!coffinVoice)return;
            if(!chanting)
            {
                if(coffinVoiceStarted)coffinVoice.Stop();
                coffinVoiceStarted=coffinVoicePaused=false;
                return;
            }
            if(!audible)
            {
                if(coffinVoiceStarted && !coffinVoicePaused)coffinVoice.Pause();
                coffinVoicePaused=coffinVoiceStarted;
                return;
            }

            var clip=coffinVoice.clip;
            int sample=Mathf.Clamp(Mathf.RoundToInt(elapsed*clip.frequency),0,clip.samples-1);
            float blend=coffinRiseAudioStarted?Mathf.InverseLerp(CoffinChantDuration-CoffinVoiceOverlap,CoffinChantDuration,elapsed):0;
            coffinVoice.volume=.9f*TrialPreferences.AudioLevel(0)*Mathf.Cos(blend*Mathf.PI*.5f);
            if(!coffinVoiceStarted)
            {
                coffinVoice.timeSamples=sample;
                coffinVoice.Play();
                coffinVoiceStarted=true;
            }
            else if(coffinVoicePaused)
            {
                coffinVoice.timeSamples=sample;
                coffinVoice.UnPause();
                coffinVoicePaused=false;
            }
            else if(coffinVoice.isPlaying && Mathf.Abs(coffinVoice.timeSamples-sample)>clip.frequency*.22f)
                coffinVoice.timeSamples=sample;
        }

        bool PlaySpellCue(TrialCombatEvent value,bool local)
        {
            switch(value.cue)
            {
                case TrialCombatCue.MirrorCast:
                    if(local)
                    {
                        game.Hud.ShowMirrorTitle();
                        if(mirrorVoice)
                        {
                            mirrorVoice.volume=TrialPreferences.AudioLevel(0);
                            mirrorVoice.Play();
                        }
                    }
                    Burst(value.position,MirrorTint,24);
                    if(!mirrorVoice)Sound("spellGlass",value.position,.59f);
                    else if(!local)Sound("mirrorInvocation",value.position);
                    return true;
                case TrialCombatCue.MirrorScreenBreak:
                    if(local)game.Hud.ShatterMirrorScreen();
                    return true;
                case TrialCombatCue.MirrorPrismCut:
                    MakeMirrorPrismCut(value);
                    if(local && value.style==0)game.CameraRig.Kick(.18f);
                    return true;
                case TrialCombatCue.MirrorBreak:
                    bool execution=value.style==TrialPlayer.MirrorExecutionStyle;
                    bool bossImpact=value.style==TrialPlayer.MirrorBossImpactStyle;
                    Burst(value.position,MirrorTint,execution || bossImpact?36:16);
                    if(Time.unscaledTime>=nextMirrorSound)
                    {
                        nextMirrorSound=Time.unscaledTime+.10f;
                        Sound("spellGlass",value.position,execution?.84f:1);
                    }
                    if(execution && ShatterMirrorModel(value))return true;
                    if(bossImpact)SlashEffect(value.position,value.forward,MirrorTint,2.1f);
                    int count=execution?(Application.isMobilePlatform?12:20):bossImpact?10:5;
                    for(int i=0;i<count;i++)
                    {
                        var fragment=value;
                        fragment.style=(byte)i;
                        MakeGlassShard(fragment);
                    }
                    return true;
                case TrialCombatCue.ThunderChain:
                    MakeLightning(value);
                    return true;
                case TrialCombatCue.BlackCoffin:
                    PlayCoffinImpact(value,local);
                    return true;
                default:
                    return false;
            }
        }

        SpellVisual AddSpellVisual(string name,TrialCombatEvent value,float duration,GameObject obj=null,bool tracked=true)
        {
            var root=(obj?obj:new GameObject(name)).transform;
            root.name=name;
            root.SetParent(transform,false);
            root.position=value.position;
            var effect=new SpellVisual{Cue=value,Root=root,Start=value.position,End=value.end,Duration=duration};
            if(tracked)spellVisuals.Add(effect);
            return effect;
        }

        void ApplySpellSurface(SpellVisual effect,Renderer renderer,Color body,Color glow,float glass)
        {
            if(!spellSurface)spellSurface=new Material(Resources.Load<Shader>("Trial/VFX/SpellSurface"));
            effect.Surface=renderer;
            effect.Tint=new MaterialPropertyBlock();
            renderer.sharedMaterial=spellSurface;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.receiveShadows=false;
            effect.Tint.SetColor("_Body",body);
            effect.Tint.SetColor("_Glow",glow);
            effect.Tint.SetFloat("_Glass",glass);
            effect.Tint.SetFloat("_Seed",effect.Start.x+effect.Cue.style*.173f);
            renderer.SetPropertyBlock(effect.Tint);
        }

        void MakeGlassShard(TrialCombatEvent value)
        {
            if(!spellShard)
            {
                spellShard=new Mesh{name="Double sided mirror fragment"};
                spellShard.vertices=new[]{new Vector3(-.49f,-.32f,0),new Vector3(.35f,-.40f,0),new Vector3(.16f,.58f,0)};
                spellShard.uv=new[]{Vector2.zero,Vector2.right,new Vector2(.7f,1)};
                spellShard.triangles=new[]{0,1,2,2,1,0};
                spellShard.RecalculateBounds();
            }
            float seed=value.style*2.399963f;
            float scale=.15f+(value.style%5)*.035f;
            var effect=AddSpellVisual("Broken mirror",value,.66f+(value.style%5)*.085f);
            effect.Size=new Vector3(scale,scale*1.46f,scale);
            effect.Spin=new Vector3(123+value.style*29,260+value.style*21,370-value.style*11);
            effect.Drift=(Vector3.right*Mathf.Cos(seed)+Vector3.forward*Mathf.Sin(seed))*(2.4f+(value.style%4)*.8f);
            effect.Drift.y=2.1f+(value.style%5)*.67f;
            effect.Root.localScale=effect.Size;
            effect.Root.rotation=Quaternion.Euler(effect.Spin);
            effect.Root.gameObject.AddComponent<MeshFilter>().sharedMesh=spellShard;
            ApplySpellSurface(effect,effect.Root.gameObject.AddComponent<MeshRenderer>(),
                new Color(.10f,.48f,.58f,.98f),MirrorTint,0);
        }

        void MakeMirrorPrismCut(TrialCombatEvent value)
        {
            int cut=value.style;
            float side=cut%2==0?1:-1;
            Vector3 across=Vector3.Cross(Vector3.up,value.forward);
            value.position=Vector3.Lerp(value.position,value.end,Mathf.Lerp(.22f,.78f,cut/(TrialPlayer.MirrorCutCount-1f)))
                +across*(side*.27f)+Vector3.up*(.78f+.19f*(cut%4));
            var slash=AddMirrorPrism("Mirror · prism slash",value,.26f);
            slash.PrismSlash=true;
            slash.Size=new Vector3(3.4f+.33f*(cut%4),.24f+.06f*(cut%3),.21f);
            slash.Root.localScale=new Vector3(slash.Size.x*.24f,slash.Size.y,slash.Size.z);
            slash.Root.rotation=Quaternion.LookRotation(value.forward)*Quaternion.Euler(cut*13,side*(18+cut%3*13),side*(22+cut%5*12));

            // New chips shear off each blade and tumble independently as the next cut appears.
            for(int i=0;i<3;i++)
            {
                var fragment=value;
                fragment.position+=slash.Root.right*((i-1)*.68f)+Vector3.up*.08f*i;
                fragment.style=(byte)(cut*3+i);
                var chip=AddMirrorPrism("Mirror · prism fragment",fragment,.33f+.045f*i);
                chip.Root.localScale=new Vector3(.31f+.08f*i,.10f,.095f);
                chip.Spin=new Vector3(190+cut*23,160+i*100,-side*(260+i*30));
                chip.Root.rotation=Quaternion.Euler(chip.Spin*.4f);
                chip.Drift=slash.Root.right*((i-1)*1.8f)+value.forward*(side*.60f)+Vector3.up*(1.35f+.36f*i);
            }
            Sound(cut%2==0?"cutDraw":"cutSweep",value.position,.95f+.06f*(cut%3));
        }

        SpellVisual AddMirrorPrism(string name,TrialCombatEvent value,float duration)
        {
            if(!mirrorPrism)mirrorPrism=BuildMirrorPrism();
            var effect=AddSpellVisual(name,value,duration);
            effect.Root.gameObject.AddComponent<MeshFilter>().sharedMesh=mirrorPrism;
            ApplySpellSurface(effect,effect.Root.gameObject.AddComponent<MeshRenderer>(),
                new Color(.045f,.009f,.082f,.96f),new Color(1.04f,.26f,2.6f),0);
            effect.Tint.SetFloat("_Prism",1);
            effect.Surface.SetPropertyBlock(effect.Tint);
            return effect;
        }

        static Mesh BuildMirrorPrism()
        {
            // The shared mesh has two tapered tips and three faceted sides.
            var rim=new[]{new Vector3(-.08f,.5f,0),new Vector3(-.08f,-.25f,.5f),new Vector3(-.08f,-.25f,-.5f)};
            var vertices=new List<Vector3>(18);
            var uv=new List<Vector2>(18);
            var triangles=new List<int>(18);
            for(int i=0;i<rim.Length;i++)
            {
                Vector3 a=rim[i],b=rim[(i+1)%rim.Length];
                AddFace(Vector3.left*.5f,b,a);
                AddFace(Vector3.right*.5f,a,b);
            }
            var mesh=new Mesh{name="Tapered mirror prism"};
            mesh.SetVertices(vertices);
            mesh.SetUVs(0,uv);
            mesh.SetTriangles(triangles,0);
            mesh.UploadMeshData(true);
            return mesh;

            void AddFace(Vector3 a,Vector3 b,Vector3 c)
            {
                int index=vertices.Count;
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.up);
                triangles.Add(index);triangles.Add(index+1);triangles.Add(index+2);
            }
        }

        LineRenderer SpellLine(Transform parent,string label,int points)
        {
            var obj=new GameObject(label);
            obj.transform.SetParent(parent,false);
            var line=obj.AddComponent<LineRenderer>();
            line.positionCount=points;
            line.useWorldSpace=true;
            line.sharedMaterial=particles;
            line.shadowCastingMode=ShadowCastingMode.Off;
            line.receiveShadows=false;
            line.numCornerVertices=1;
            line.numCapVertices=2;
            return line;
        }

        void MakeLightning(TrialCombatEvent value)
        {
            var effect=AddSpellVisual("Conducted lightning",value,.34f);
            effect.Lines=new[]{SpellLine(effect.Root,"Gold glow",11),SpellLine(effect.Root,"Lightning core",11)};
            PaintLightning(effect,0);
        }

        void PaintLightning(SpellVisual effect,float progress)
        {
            Vector3 axis=effect.End-effect.Start;
            Vector3 right=Vector3.Cross(axis.normalized,Vector3.up).normalized;
            float fade=1-Mathf.SmoothStep(0,1,progress);
            float pulse=InkMainMenu.ReducedMotion?1:.68f+.32f*Mathf.Abs(Mathf.Sin(effect.Age*78));
            int frame=Mathf.FloorToInt(effect.Age*35);
            for(int i=0;i<effect.Lines.Length;i++)
            {
                var line=effect.Lines[i];
                float width=i==0?.24f:.07f;
                line.widthMultiplier=width*(.58f+.42f*pulse)*Mathf.Sqrt(fade);
                var tint=i==0?new Color(1,.62f,.06f,.46f):new Color(1,.97f,.62f);
                tint.a*=fade*pulse;
                line.startColor=line.endColor=tint;
                for(int j=0;j<line.positionCount;j++)
                {
                    float u=(float)j/(line.positionCount-1);
                    float envelope=Mathf.Sin(u*Mathf.PI);
                    float seed=j*2.41f;
                    float x=Mathf.PerlinNoise(seed,frame*.39f)-.5f;
                    float y=Mathf.PerlinNoise(seed+30.1f,frame*.51f)-.5f;
                    Vector3 shift=(right*x+Vector3.up*y)*.42f*envelope;
                    line.SetPosition(j,effect.Start+axis*u+shift);
                }
            }
        }

        void TickSpellVisuals()
        {
            // Spread skinning and mesh uploads across the sealed portion of the cast.
            if(!game.HasResult)
                foreach(var coffin in spellVisuals)
                    if(coffin.RisingCoffin && !coffin.CoffinPrepared && coffin.Age>=.48f && PrepareCoffinPrisms(coffin))
                        break;
            for(int i=spellVisuals.Count-1;i>=0;i--)
            {
                var effect=spellVisuals[i];
                effect.Age+=Time.deltaTime;
                var cue=effect.Cue.cue;
                if(effect.Age>=effect.Duration || game.HasResult && !game.ResultInProgress)
                {
                    DestroySpellVisual(effect);
                    spellVisuals.RemoveAt(i);
                    continue;
                }
                float progress=effect.Age/effect.Duration;
                if(effect.PrismSlash)
                {
                    float length=Mathf.Lerp(.24f,1,Mathf.InverseLerp(0,.16f,progress));
                    float width=Mathf.Lerp(1,.25f,Mathf.InverseLerp(.30f,1,progress));
                    effect.Root.localScale=Vector3.Scale(effect.Size,new Vector3(length,width,width));
                    effect.Tint.SetFloat("_Fade",1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.30f,1,progress)));
                    effect.Surface.SetPropertyBlock(effect.Tint);
                    continue;
                }
                if(effect.ModelMesh)
                {
                    PaintMirrorModel(effect,progress);
                    continue;
                }
                if(cue==TrialCombatCue.ThunderChain)
                {
                    PaintLightning(effect,progress);
                    continue;
                }
                if(effect.RisingCoffin)
                {
                    PaintBlackCoffin(effect);
                    continue;
                }
                effect.Root.position=effect.Start+effect.Drift*effect.Age-Vector3.up*6*effect.Age*effect.Age;
                effect.Root.rotation=Quaternion.Euler(effect.Spin*(.4f+effect.Age));
                effect.Tint.SetFloat("_Fade",1-Mathf.InverseLerp(.82f,1,progress));
                effect.Surface.SetPropertyBlock(effect.Tint);
            }
        }

        void DisposeSpellVisuals()
        {
            ClearSpellVisuals();
            if(spellSurface)Destroy(spellSurface);
            if(spellShard)Destroy(spellShard);
            if(mirrorPrism)Destroy(mirrorPrism);
            if(mirrorModelMaterial)Destroy(mirrorModelMaterial);
            if(coffinShellMesh)Destroy(coffinShellMesh);
            if(coffinFallbackMesh)Destroy(coffinFallbackMesh);
            if(coffinBakedPose)Destroy(coffinBakedPose);
        }

        internal void ClearSpellVisuals()
        {
            foreach(var effect in spellVisuals)DestroySpellVisual(effect);
            spellVisuals.Clear();
            if(coffinMist)coffinMist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            StopCoffinRiseAudio();
            if(coffinBreakAudio)coffinBreakAudio.Stop();
            if(mirrorVoice)mirrorVoice.Stop();
            nextCoffinRupture=nextCoffinShake=0;
        }

        void DestroySpellVisual(SpellVisual effect)
        {
            if(effect.CoffinPrisms!=null)
                foreach(var prism in effect.CoffinPrisms)DestroySpellVisual(prism);
            if(effect.ModelMesh && effect.ModelMesh!=coffinShellMesh && effect.ModelMesh!=coffinFallbackMesh)Destroy(effect.ModelMesh);
            if(effect.Root)Destroy(effect.Root.gameObject);
        }
    }
}
