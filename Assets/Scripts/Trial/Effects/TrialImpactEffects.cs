using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        sealed class ArcEffect { public LineRenderer line;public float until,lifetime;public Color tint; }
        sealed class GhostFrame
        {
            public GameObject root;public readonly List<Mesh> meshes=new List<Mesh>();
            public readonly List<MeshRenderer> renderers=new List<MeshRenderer>();public float until,lifetime,strength;
        }
        readonly ArcEffect[] arcs=new ArcEffect[16];
        readonly GhostFrame[] ghosts=new GhostFrame[5];
        readonly GhostFrame[] perfectDodgeGhosts=new GhostFrame[8];
        static readonly Color PerfectDodgeTint=new Color(.24f,.78f,1.18f);
        readonly Vector3[] slashPoints=new Vector3[49];
        MaterialPropertyBlock ghostProperties;
        Material ghostMaterial,perfectDodgeMaterial;
        AudioSource courtMusic,bossMusic;
        float rumbleUntil,slowUntil;
        int nextArc,nextGhost,nextPerfectDodgeGhost;
        public void SlowMotion(float seconds){slowUntil=Mathf.Max(slowUntil,Time.unscaledTime+seconds);}

        void InitializeImpactEffects()
        {
            ghostProperties=new MaterialPropertyBlock();
            ghostMaterial=new Material(InkMaterial);ghostMaterial.SetFloat("_Feather",0);
            for(int i=0;i<arcs.Length;i++)
            {
                var obj=new GameObject("Impact slash "+i);obj.transform.SetParent(transform,false);
                var line=obj.AddComponent<LineRenderer>();line.sharedMaterial=particles;line.useWorldSpace=true;line.positionCount=slashPoints.Length;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.enabled=false;
                arcs[i]=new ArcEffect{line=line};
            }
            CreateSound("impactHeavy",.38f,62,.65f,2);CreateSound("finish",.65f,46,.65f,2);
            CreateSound("blink",.36f,260,.3f,1);CreateSound("counter",.7f,1140,.42f,3);
            courtMusic=Music("Courtyard · dusk strings",false);bossMusic=Music("Deep tide · battle drums",true);bossMusic.volume=0;
        }
        public void Rumble(float low,float high,float seconds)
        {
            if(!game.Input.UsingGamepad || Gamepad.current==null)return;
            Gamepad.current.SetMotorSpeeds(low,high);rumbleUntil=Time.unscaledTime+seconds;
        }
        public void SlashEffect(Vector3 position,Vector3 forward,Color tint,float radius,bool ring=false,float width=0,float duration=0)
        {
            var arc=arcs[nextArc++%arcs.Length];float yaw=Mathf.Atan2(forward.x,forward.z);
            for(int i=0;i<slashPoints.Length;i++)
            {
                float angle=yaw+Mathf.Lerp(ring?0:-2.1f,ring?Mathf.PI*2:2.1f,(float)i/(slashPoints.Length-1));
                slashPoints[i]=position+new Vector3(Mathf.Sin(angle)*radius,ring?0:Mathf.Sin(angle-yaw)*radius*.23f,Mathf.Cos(angle)*radius);
            }
            arc.line.SetPositions(slashPoints);arc.line.widthMultiplier=width>0?width:ring?.045f:.13f;arc.tint=tint;arc.lifetime=duration>0?duration:ring?.32f:.19f;arc.until=Time.unscaledTime+arc.lifetime;arc.line.enabled=true;
            arc.line.startColor=arc.line.endColor=tint;
        }
        float nextGhostBake;
        public void Afterimage(TrialPlayer player,bool perfect=true)
        {
            // Shared budget bounds BakeMesh cost even in a large cooperative room.
            if(Time.unscaledTime<nextGhostBake)return;
            nextGhostBake=Time.unscaledTime+(Application.isMobilePlatform || TrialGraphicsBudget.Performance?.15f:.09f);
            int index=nextGhost++%(TrialGraphicsBudget.Performance?3:ghosts.Length);
            CaptureGhost(player,ref ghosts[index],ghostMaterial,Vector3.zero,perfect?.38f:.18f,perfect?.76f:.32f,false);
            InkBloom(player.transform.position+Vector3.up*.8f,-player.DodgeDirection*.6f,perfect?1.2f:.55f);
        }

        internal void PerfectDodgeAfterimage(TrialPlayer player,Vector3 offset,int step)
        {
            if(!perfectDodgeMaterial)
            {
                var shader=Resources.Load<Shader>("Trial/VFX/PerfectDodgeGhost");
                perfectDodgeMaterial=new Material(shader?shader:ghostMaterial.shader);
            }
            bool limited=Application.isMobilePlatform || TrialGraphicsBudget.Performance;
            int index=nextPerfectDodgeGhost++%(limited?4:perfectDodgeGhosts.Length);
            CaptureGhost(player,ref perfectDodgeGhosts[index],perfectDodgeMaterial,offset,.58f-step*.045f,.82f-step*.09f,true);
        }

        void CaptureGhost(TrialPlayer player,ref GhostFrame ghost,Material material,Vector3 offset,float lifetime,float strength,bool dodgeTrail)
        {
            var skins=player.GhostSkins;
            if(skins==null || skins.Length==0)return;
            if(ghost==null)
            {
                ghost=new GhostFrame{root=new GameObject(dodgeTrail?"Perfect dodge afterimage":"Blink afterimage")};
                ghost.root.transform.SetParent(transform,false);
            }
            while(ghost.meshes.Count<skins.Length)
            {
                var obj=new GameObject("Ghost mesh");obj.transform.SetParent(ghost.root.transform,false);
                var mesh=new Mesh{name="Baked blink silhouette"};obj.AddComponent<MeshFilter>().sharedMesh=mesh;ghost.meshes.Add(mesh);
                var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;ghost.renderers.Add(renderer);
            }
            ghost.lifetime=lifetime;ghost.strength=strength;ghost.until=Time.unscaledTime+lifetime;
            for(int i=0;i<ghost.meshes.Count;i++)
            {
                var renderer=ghost.renderers[i];
                var skin=i<skins.Length?skins[i]:null;
                renderer.enabled=skin && skin.sharedMesh && skin.enabled && skin.gameObject.activeInHierarchy;
                if(!renderer.enabled)continue;
                skin.BakeMesh(ghost.meshes[i],true);
                var t=renderer.transform;
                t.SetPositionAndRotation(skin.transform.position+offset,skin.transform.rotation);
                t.localScale=skin.transform.lossyScale;
                if(dodgeTrail && renderer.sharedMaterials.Length!=ghost.meshes[i].subMeshCount)
                {
                    var materials=new Material[ghost.meshes[i].subMeshCount];
                    for(int slot=0;slot<materials.Length;slot++)materials[slot]=material;
                    renderer.sharedMaterials=materials;
                }
            }
            ghost.root.SetActive(true);
            PaintGhost(ghost,1);
        }

        internal void PerfectDodgeWake(Vector3 from,Vector3 to,int step)
        {
            Vector3 side=Vector3.Cross(Vector3.up,(to-from).normalized);
            int strands=TrialGraphicsBudget.Performance || Application.isMobilePlatform?1:2;
            for(int strand=0;strand<strands;strand++)
            {
                var ribbon=Ribbon(PerfectDodgeTint,strand==0?.10f:.055f,.32f-step*.032f,9);
                float offset=strand==0?-.32f:.34f;
                for(int i=0;i<9;i++)
                {
                    float t=i/8f;
                    Vector3 point=Vector3.Lerp(from,to,t)+side*(offset*(1-.6f*t));
                    point.y+=.54f+strand*.26f+Mathf.Sin(t*Mathf.PI)*.12f;
                    RibbonPoint(ribbon,i,point);
                }
            }
        }
        bool PlayImpactCue(TrialCombatEvent value,bool local)
        {
            var cue=value.cue;
            if(cue==TrialCombatCue.HeavyLand)
            {SlashEffect(value.position+Vector3.up*.06f,value.forward,new Color(1,.68f,.26f),1.65f,true);Burst(value.position,new Color(1,.8f,.45f),30);Sound("landing",value.position);if(local){game.CameraRig.Kick(.22f);Rumble(.35f,.45f,.1f);}return true;}
            if(cue==TrialCombatCue.Dodge)
            {
                Sound("dodge",value.position);
                var actor=TrialNetworkManager.SessionActive?TrialNetworkManager.Current.PlayerById(value.player):game.Player;
                if(actor)Afterimage(actor,false);
                return true;
            }
            if(cue==TrialCombatCue.Heal)
            {Sound("heal",value.position);if(local)game.Hud.HealFlash();return true;}
            if(cue==TrialCombatCue.ChargeReady)
            {SlashEffect(value.position,value.forward,new Color(1,.72f,.25f),.9f,true);Burst(value.position,new Color(1,.8f,.3f),22);Sound("charge",value.position);if(local)Rumble(.15f,.35f,.08f);return true;}
            if(cue==TrialCombatCue.CutThrough)
            {SlashEffect(value.position,value.forward,new Color(1,.85f,.45f),2.2f);Burst(value.position,new Color(1,.86f,.5f),60);Sound("counter",value.position);if(local){HitStop(.09f);Rumble(.6f,.85f,.15f);game.CameraRig.Pulse(6);game.Hud.Flash(new Color(1,.84f,.4f,.12f));}return true;}
            if(cue==TrialCombatCue.SkillImpact)
            {ShowSkillImpact(value.position,new Color(value.forward.x,value.forward.y,value.forward.z));return true;}
            if(cue==TrialCombatCue.Hurt)
            {
                Sound("hurt",value.position,Random.Range(.94f,1.04f));
                if(local)
                {
                    game.Hud.SetDamageDirection(value.forward);
                    if(!InkMainMenu.ReducedMotion){game.CameraRig.Kick(.32f);game.CameraRig.Pulse(2);}
                    Rumble(.65f,.28f,.18f);
                }
                return true;
            }
            if(cue==TrialCombatCue.Hit || cue==TrialCombatCue.HeavyHit || cue==TrialCombatCue.FinisherHit || cue==TrialCombatCue.Kill || cue==TrialCombatCue.SwordFlowHit)
            {
                bool heavy=cue!=TrialCombatCue.Hit,kill=cue==TrialCombatCue.Kill,finish=cue==TrialCombatCue.FinisherHit;
                Burst(value.position,kill?new Color(1,.4f,.12f):new Color(1,.85f,.6f),kill?85:heavy?50:24);
                SlashEffect(value.position,value.forward,kill?new Color(1,.35f,.1f):Color.white,heavy?1.8f:1.1f);
                if(value.amount==TrialCombatEvent.ContactOnlyImpact)return true;
                Sound(ContactSoundKey(value,kill || finish?"finish":heavy?"impactHeavy":"hit"),value.position,Random.Range(finish?.82f:.94f,finish?.92f:1.05f));
                if(local){game.CameraRig.Kick(kill || finish?.52f:heavy?.34f:.18f);game.CameraRig.Pulse(kill || finish?4:heavy?2:1);if(cue!=TrialCombatCue.SwordFlowHit)HitStop(value.amount==TrialCombatEvent.BlinkSlashImpact?TrialCombatEvent.BlinkSlashHitStop:kill || finish?.115f:heavy?.085f:.055f);Rumble(finish?.65f:heavy?.5f:.18f,kill || finish?.9f:.55f,finish?.14f:heavy?.10f:.06f);}
                return true;
            }
            if(cue==TrialCombatCue.PerfectDodge)
            {
                var actor=TrialNetworkManager.SessionActive?TrialNetworkManager.Current.PlayerById(value.player):game.Player;
                if(actor)actor.ShowPerfectDodge(value.position-Vector3.up);
                InkBloom(value.position,Vector3.up*.3f,.85f,new Color(.12f,.43f,.67f,.54f));
                Burst(value.position,PerfectDodgeTint,16);
                Sound("blink",value.position,.88f);
                if(local)
                {
                    if(!TrialNetworkManager.SessionActive && !InkMainMenu.ReducedMotion)perfectDodgeSlowUntil=Time.unscaledTime+.34f;
                    game.CameraRig.Pulse(3);Rumble(.26f,.55f,.13f);
                    game.Hud.Flash(new Color(.34f,.78f,1,.10f));
                    game.Hud.Notify("完美闪避 · 轻：闪身横斩 / 重：后跃落劈雷吼炮·雷龙",1.8f);
                }
                return true;
            }
            if(cue==TrialCombatCue.BlinkSlash || cue==TrialCombatCue.Reversal)
            {
                bool counter=cue==TrialCombatCue.Reversal;
                Color tint=counter?new Color(1,.73f,.22f):new Color(.48f,.018f,.045f);
                SlashEffect(value.position,value.forward,tint,counter?2.6f:1.7f);
                Burst(value.position,tint,counter?85:24);
                Sound(counter?"counter":"blink",value.position);
                if(counter)SlowMotion(.42f);
                if(local)
                {
                    if(!counter)perfectDodgeSlowUntil=Mathf.Min(perfectDodgeSlowUntil,Time.unscaledTime);
                    game.CameraRig.Pulse(counter?8:4);Rumble(.3f,counter?.9f:.4f,.12f);
                    game.Hud.Flash(new Color(tint.r,tint.g,tint.b,counter?.16f:.08f));
                    if(counter)game.Hud.Notify("反斩",.8f);
                }
                return true;
            }
            return false;
        }
        void TickImpactEffects()
        {
            foreach(var arc in arcs)if(arc!=null && arc.line.enabled)
            {float alpha=Mathf.Clamp01((arc.until-Time.unscaledTime)/arc.lifetime);arc.line.enabled=alpha>0;var c=arc.tint;c.a=alpha;arc.line.startColor=arc.line.endColor=c;}
            FadeGhosts(ghosts);FadeGhosts(perfectDodgeGhosts);
            if(rumbleUntil>0 && Time.unscaledTime>=rumbleUntil){Gamepad.current?.ResetHaptics();rumbleUntil=0;}
            if(courtMusic && bossMusic)
            {
                bool battle=game.IsArena || (game.Enemies.Count>0 && game.Enemies[game.Enemies.Count-1] && game.Enemies[game.Enemies.Count-1].Awakened);
                courtMusic.volume=Mathf.MoveTowards(courtMusic.volume,battle?.055f:.13f,Time.unscaledDeltaTime*.1f);
                bossMusic.volume=Mathf.MoveTowards(bossMusic.volume,battle?.20f:0,Time.unscaledDeltaTime*.13f);
            }
        }

        void FadeGhosts(GhostFrame[] frames)
        {
            foreach(var ghost in frames)if(ghost!=null && ghost.root.activeSelf)
            {
                float alpha=Mathf.Clamp01((ghost.until-Time.unscaledTime)/ghost.lifetime);
                if(alpha<=0)ghost.root.SetActive(false);
                else PaintGhost(ghost,alpha);
            }
        }

        void PaintGhost(GhostFrame ghost,float alpha)
        {
            ghostProperties.SetColor("_BaseColor",new Color(.009f,.006f,.014f,alpha*ghost.strength));
            ghostProperties.SetFloat("_Dissolve",(1-alpha)*.85f);
            foreach(var renderer in ghost.renderers)if(renderer.enabled)renderer.SetPropertyBlock(ghostProperties);
        }

        AudioSource Music(string label,bool battle)
        {
            const int rate=16000;const float length=16;
            var data=new float[(int)(rate*length)];int[] notes={0,7,10,7,3,7,12,10,0,7,15,12,10,7,3,7};
            var random=new System.Random(battle?271:773);
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate,beat=t%.5f;int step=(int)(t/.5f)%notes.Length;
                float frequency=220*Mathf.Pow(2,notes[step]/12f),pluck=Mathf.Exp(-beat*(battle?12:7));
                float melody=(Mathf.Sin(2*Mathf.PI*frequency*t)+.25f*Mathf.Sin(2*Mathf.PI*frequency*2*t))*pluck*.09f;
                float drone=(Mathf.Sin(2*Mathf.PI*55*t)+Mathf.Sin(2*Mathf.PI*82.5f*t))*.022f;
                float drum=battle?Mathf.Sin(2*Mathf.PI*(65*beat-45*beat*beat))*Mathf.Exp(-beat*28)*.23f:0;
                float noise=battle?((float)random.NextDouble()*2-1)*Mathf.Exp(-beat*65)*.045f:0;
                float seam=Mathf.Min(1,Mathf.Min(t,length-t)*18);
                data[i]=(melody+drone+drum+noise)*seam;
            }
            var clip=AudioClip.Create(label,data.Length,1,rate,false);clip.SetData(data,0);sounds[label]=clip;generatedSounds.Add(clip);
            var source=gameObject.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.spatialBlend=0;source.volume=.13f;source.Play();return source;
        }
        void DisposeImpactEffects()
        {
            Gamepad.current?.ResetHaptics();
            if(tideMaterial)Destroy(tideMaterial);
            foreach(var ghost in ghosts)if(ghost!=null)foreach(var mesh in ghost.meshes)Destroy(mesh);
            foreach(var ghost in perfectDodgeGhosts)if(ghost!=null)foreach(var mesh in ghost.meshes)Destroy(mesh);
            if(ghostMaterial)Destroy(ghostMaterial);
            if(perfectDodgeMaterial)Destroy(perfectDodgeMaterial);
            DisposeInkEffects();
        }
        void OnApplicationFocus(bool focus){if(!focus){Gamepad.current?.ResetHaptics();rumbleUntil=0;}}
    }
}
