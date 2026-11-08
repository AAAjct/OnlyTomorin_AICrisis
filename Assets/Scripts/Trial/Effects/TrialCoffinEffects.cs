using System.Collections.Generic;
using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        static readonly Vector3 CoffinSize=new Vector3(1.78f,3.2f,1.65f);
        static readonly Color CoffinGlow=new Color(.91f,.15f,1.68f);
        const float CoffinVoiceOverlap=.18f,CoffinRuptureBlend=.12f;
        Mesh coffinShellMesh;
        AudioSource coffinRiseBed;
        bool coffinRiseAudioStarted,coffinRiseAudioPaused,coffinRiseConfirmed,coffinImpactWarmed;
        float coffinRiseFadeEnd;
        float nextCoffinRupture,nextCoffinShake;

        void InitializeCoffinVisuals()
        {
            spellSurface=new Material(Resources.Load<Shader>("Trial/VFX/SpellSurface"));
            if(EnsureMirrorModelMaterial())
            {
                coffinShellMesh=BuildCoffinShell();
                coffinFallbackMesh=BuildFallbackPrisms();
                coffinBakedPose=new Mesh{name="Black coffin · pose buffer"};
                coffinBakedPose.MarkDynamic();
            }
            InitializeCoffinMist();
        }

        void PlayCoffinImpact(TrialCombatEvent cue,bool local)
        {
            switch((TrialCoffinStage)cue.style)
            {
                case TrialCoffinStage.Rise:
                    if(local && coffinRiseBed)coffinRiseConfirmed=true;
                    MakeBlackCoffin(cue);
                    break;
                case TrialCoffinStage.BreakShell: BreakBlackCoffin(cue,local,false);break;
                case TrialCoffinStage.ShatterEnemy: BreakBlackCoffin(cue,local,true);break;
                case TrialCoffinStage.DamageBoss: ShowCoffinFinish(cue);break;
            }
        }

        void UpdateCoffinRiseAudio(bool chanting,bool audible,float elapsed)
        {
            if(!coffinRiseBed)return;
            if(game.HasResult || !game.Player.Alive || (!chanting && !coffinRiseConfirmed) ||
                (coffinRiseFadeEnd>0 && Time.unscaledTime>=coffinRiseFadeEnd))
            {
                StopCoffinRiseAudio();
                return;
            }
            if(!audible)
            {
                if(coffinRiseBed.isPlaying)
                {
                    coffinRiseBed.Pause();
                    coffinRiseAudioPaused=true;
                }
                return;
            }

            if(coffinRiseAudioPaused)
            {
                coffinRiseBed.UnPause();
                coffinRiseAudioPaused=false;
            }
            float overlapTime=elapsed-(CoffinChantDuration-CoffinVoiceOverlap);
            if(!coffinRiseAudioStarted && (coffinRiseConfirmed || (overlapTime>=0 && HasCoffinTarget())))
            {
                coffinRiseBed.time=Mathf.Clamp(overlapTime,0,CoffinVoiceOverlap);
                coffinRiseBed.Play();
                coffinRiseAudioStarted=true;
            }
            float fade=coffinRiseFadeEnd>0?Mathf.Clamp01((coffinRiseFadeEnd-Time.unscaledTime)/CoffinRuptureBlend):1;
            coffinRiseBed.volume=.9f*TrialPreferences.AudioLevel(0)*fade;
        }

        bool HasCoffinTarget()
        {
            foreach(var enemy in game.Enemies)
                if(enemy && enemy.CanBeCoffined)return true;
            return false;
        }

        void StopCoffinRiseAudio()
        {
            if(coffinRiseAudioStarted && coffinRiseBed)coffinRiseBed.Stop();
            coffinRiseAudioStarted=coffinRiseAudioPaused=coffinRiseConfirmed=false;
            coffinRiseFadeEnd=0;
        }

        void MakeBlackCoffin(TrialCombatEvent cue)
        {
            WarmCoffinImpact();
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
            var collider=cube.GetComponent<Collider>();
            collider.enabled=false;
            Destroy(collider);
            var effect=AddSpellVisual("Black coffin",cue,4f,cube);
            effect.RisingCoffin=true;
            effect.CoffinFilter=cube.GetComponent<MeshFilter>();
            effect.CoffinPrepared=cue.end!=Vector3.zero || !coffinBakedPose;
            effect.Size=cue.end==Vector3.zero?CoffinSize:cue.end;
            effect.Root.rotation=Quaternion.LookRotation(cue.forward);
            ApplySpellSurface(effect,cube.GetComponent<Renderer>(),new Color(.025f,.007f,.042f,1),CoffinGlow,0);
            effect.Tint.SetFloat("_Coffin",1);
            PaintBlackCoffin(effect);
        }

        void WarmCoffinImpact()
        {
            if(coffinImpactWarmed)return;
            coffinImpactWarmed=true;
            var view=game.CameraRig.View.transform;
            Vector3 point=view.position+view.forward*2;
            // Exercise both particle renderers and the pooled shock ring while the shell is still rising.
            var warmup=new ParticleSystem.EmitParams{
                position=point,velocity=Vector3.zero,startSize=.08f,startLifetime=.10f,startColor=Color.clear
            };
            var sparks=pool[0];
            if(!sparks.isPlaying)sparks.Play();
            sparks.Emit(warmup,1);
            coffinMist.Play();
            coffinMist.Emit(warmup,1);
            SlashEffect(point,view.forward,Color.clear,.2f,true,.01f,.10f);
        }

        void PaintBlackCoffin(SpellVisual effect)
        {
            float rise=Mathf.SmoothStep(.02f,1,Mathf.Clamp01(effect.Age/TrialPlayer.CoffinRiseDuration));
            float height=effect.Size.y*rise;
            effect.Root.localScale=new Vector3(effect.Size.x,height,effect.Size.z);
            effect.Root.position=effect.Start+Vector3.up*(height*.5f);
            effect.Tint.SetFloat("_Rise",rise);
            effect.Surface.SetPropertyBlock(effect.Tint);
        }

        void BreakBlackCoffin(TrialCombatEvent cue,bool local,bool execution)
        {
            SpellVisual shell=null;
            foreach(var effect in spellVisuals)
            {
                if(!effect.RisingCoffin || effect.Cue.target!=cue.target)continue;
                shell=effect;
                break;
            }

            Vector3 foot=cue.position;
            Vector3 size=cue.end==Vector3.zero?CoffinSize:cue.end;
            if(execution)
            {
                ReleaseCoffinPrisms(cue,shell);
                ShowCoffinFinish(cue);
            }
            cue.position=foot+Vector3.up*(size.y*.5f);
            if(coffinShellMesh)
            {
                if(shell==null)
                {
                    shell=AddSpellVisual("Black coffin · split shell",cue,.88f);
                    shell.Root.rotation=Quaternion.LookRotation(cue.forward);
                }
                shell.Root.position=cue.position;
                shell.Root.localScale=new Vector3(size.x/CoffinSize.x,size.y/CoffinSize.y,size.z/CoffinSize.z);
                shell.RisingCoffin=false;
                shell.Duration=.88f;
                shell.Surface?.SetPropertyBlock(null);
                var glass=shell.Tint??new MaterialPropertyBlock();
                glass.Clear();
                glass.SetFloat(VioletGlass,1);
                glass.SetFloat(ShardScatter,1.95f);
                AttachModelFragments(shell,coffinShellMesh,new[]{glass},.105f);
            }
            else if(shell!=null)
            {
                spellVisuals.Remove(shell);
                DestroySpellVisual(shell);
            }

            if(!execution)Burst(cue.position,CoffinGlow,34);
            SlashEffect(foot+Vector3.up*.12f,cue.forward,new Color(.61f,.08f,1.3f,.85f),Mathf.Max(2.25f,size.x*.70f),true,.075f,.24f);
            if(local && coffinRiseAudioStarted && coffinRiseFadeEnd==0)
                coffinRiseFadeEnd=Time.unscaledTime+CoffinRuptureBlend;
            if(Time.unscaledTime>=nextCoffinRupture)
            {
                // Nearby coffins break in a staggered volley and share one impact sound.
                nextCoffinRupture=Time.unscaledTime+1.4f;
                if(coffinBreakAudio)
                {
                    coffinBreakAudio.transform.position=cue.position;
                    coffinBreakAudio.spatialBlend=local?0:.65f;
                    coffinBreakAudio.volume=.9f*TrialPreferences.AudioLevel(0);
                    coffinBreakAudio.Play();
                }
                else Sound("spellCoffin",cue.position);
            }

            if(!local || Time.unscaledTime<nextCoffinShake)return;
            nextCoffinShake=Time.unscaledTime+1.4f;
            game.CameraRig.Kick(.76f);
            game.CameraRig.Pulse(3);
            Rumble(.72f,.92f,.18f);
            game.Hud.Flash(new Color(.71f,.27f,1,.13f));
        }

        void ShowCoffinFinish(TrialCombatEvent cue)
        {
            Burst(cue.position+Vector3.up*1.04f,new Color(1.3f,.31f,2.5f),38);
            EmitCoffinMist(cue.position,cue.forward);
        }

        static Mesh BuildCoffinShell()
        {
            // Independent triangles let the shared glass shader throw each part of the walls and lid outward.
            var vertices=new List<Vector3>();
            var normals=new List<Vector3>();
            var uv=new List<Vector2>();
            var colors=new List<Color32>();
            var motion=new List<Vector4>();
            var triangles=new List<int>();
            var half=CoffinSize*.5f;
            var random=new System.Random(90);
            for(int wall=0;wall<4;wall++)
            {
                bool side=wall%2==1;
                var rotation=Quaternion.Euler(0,wall*90,0);
                AddFace(rotation*Vector3.forward*(side?half.x:half.z),
                    rotation*Vector3.right*(side?half.z:half.x),Vector3.up*half.y,3);
            }
            AddFace(Vector3.up*half.y,Vector3.right*half.x,Vector3.back*half.z,2);

            var mesh=new Mesh{name="Fractured black coffin walls and lid"};
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(0,uv);
            mesh.SetUVs(1,motion);
            mesh.SetTriangles(triangles,0);
            var bounds=mesh.bounds;
            bounds.Expand(14);
            mesh.bounds=bounds;
            mesh.UploadMeshData(true);
            return mesh;

            void AddFace(Vector3 center,Vector3 across,Vector3 up,int rows)
            {
                const int columns=2;
                var points=new Vector3[columns+1,rows+1];
                for(int y=0;y<=rows;y++)
                for(int x=0;x<=columns;x++)
                {
                    float u=2f*x/columns-1,v=2f*y/rows-1;
                    if(x>0 && x<columns && y>0 && y<rows)
                    {
                        u+=((float)random.NextDouble()-.5f)*.56f;
                        v+=((float)random.NextDouble()-.5f)*.47f;
                    }
                    points[x,y]=center+across*u+up*v;
                }
                for(int y=0;y<rows;y++)
                for(int x=0;x<columns;x++)
                {
                    var a=points[x,y];var b=points[x+1,y];var c=points[x,y+1];var d=points[x+1,y+1];
                    if((x+y)%2==0){AddShard(a,b,d);AddShard(a,d,c);}
                    else{AddShard(a,b,c);AddShard(b,d,c);}
                }
            }

            void AddShard(Vector3 a,Vector3 b,Vector3 c)
            {
                var pivot=(a+b+c)/3;
                var normal=Vector3.Cross(b-a,c-a).normalized;
                var shard=new Vector4(pivot.x,pivot.y,pivot.z,vertices.Count/3+1);
                triangles.Add(vertices.Count);triangles.Add(vertices.Count+1);triangles.Add(vertices.Count+2);
                vertices.Add(a);vertices.Add(b);vertices.Add(c);
                normals.Add(normal);normals.Add(normal);normals.Add(normal);
                uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.up);
                var white=new Color32(255,255,255,255);
                colors.Add(white);colors.Add(white);colors.Add(white);
                motion.Add(shard);motion.Add(shard);motion.Add(shard);
            }
        }
    }
}
