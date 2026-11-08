using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        Material inkMaterial,inkMistMaterial;
        Texture2D inkTexture;
        ParticleSystem inkMist,coffinMist;

        Material InkMaterial
        {
            get
            {
                if(!inkMaterial)
                {
                    // Resources keeps the shader in standalone builds as well as the Editor.
                    var shader=Resources.Load<Shader>("Trial/InkWash");
                    inkMaterial=shader?new Material(shader):new Material(particles);
                    inkMaterial.name="Black ink wash · feathered blade";
                }
                return inkMaterial;
            }
        }

        Material InkMistMaterial
        {
            get
            {
                if(inkMistMaterial)return inkMistMaterial;
                inkTexture=new Texture2D(64,64,TextureFormat.RGBA32,false){name="Broken ink droplet",wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[4096];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {
                    Vector2 p=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f);
                    float grain=Mathf.PerlinNoise(x*.19f,y*.19f),r=p.magnitude;
                    float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01((1-r+(grain-.5f)*.26f)*3.5f));
                    pixels[y*64+x]=new Color(1,1,1,alpha*(.65f+grain*.35f));
                }
                inkTexture.SetPixels(pixels);inkTexture.Apply();
                inkMistMaterial=new Material(particles){name="Soft bleeding ink"};inkMistMaterial.SetTexture("_BaseMap",inkTexture);
                return inkMistMaterial;
            }
        }

        void InkBloom(Vector3 point,Vector3 drift,float strength,Color? tint=null)
        {
            if(!inkMist)
            {
                var obj=new GameObject("Ink wash droplets");obj.transform.SetParent(transform,false);
                inkMist=obj.AddComponent<ParticleSystem>();inkMist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=inkMist.main;main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=Application.isMobilePlatform?120:260;
                main.startLifetime=new ParticleSystem.MinMaxCurve(.16f,.42f);main.startSpeed=0;main.gravityModifier=-.08f;
                var emission=inkMist.emission;emission.enabled=false;var shape=inkMist.shape;shape.enabled=false;
                var color=inkMist.colorOverLifetime;color.enabled=true;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.7f,0),new GradientAlphaKey(.4f,.35f),new GradientAlphaKey(0,1)});color.color=gradient;
                var size=inkMist.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.4f,1),new Keyframe(1,1.5f)));
                inkMist.GetComponent<ParticleSystemRenderer>().sharedMaterial=InkMistMaterial;
            }
            inkMist.Play();int count=Mathf.CeilToInt((Application.isMobilePlatform?5:10)*strength*TrialGraphicsBudget.EffectDensity);
            for(int i=0;i<count;i++)
                inkMist.Emit(new ParticleSystem.EmitParams{position=point+Random.insideUnitSphere*.28f*strength,velocity=drift+Random.insideUnitSphere*.9f,startSize=Random.Range(.12f,.38f)*strength,startColor=tint??new Color(.008f,.006f,.012f,.65f),rotation=Random.Range(0,360)},1);
        }

        void InitializeCoffinMist()
        {
            var obj=new GameObject("Black coffin drifting mist");obj.transform.SetParent(transform,false);
            coffinMist=obj.AddComponent<ParticleSystem>();coffinMist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=coffinMist.main;main.playOnAwake=false;main.loop=false;main.duration=1.45f;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=Application.isMobilePlatform?150:360;
            main.startLifetime=new ParticleSystem.MinMaxCurve(.72f,1.42f);
            main.startSpeed=0;main.startRotation=new ParticleSystem.MinMaxCurve(0,2*Mathf.PI);
            var emission=coffinMist.emission;emission.enabled=false;
            var shape=coffinMist.shape;shape.enabled=false;
            var noise=coffinMist.noise;noise.enabled=true;noise.strength=.55f;noise.frequency=.75f;noise.scrollSpeed=.6f;
            var color=coffinMist.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(.62f,0),new GradientAlphaKey(.85f,.17f),new GradientAlphaKey(.36f,.62f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var size=coffinMist.sizeOverLifetime;size.enabled=true;
            size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.45f),new Keyframe(.35f,1),new Keyframe(1,1.65f)));
            var renderer=coffinMist.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial=InkMistMaterial;renderer.sortMode=ParticleSystemSortMode.Distance;
        }

        void EmitCoffinMist(Vector3 foot,Vector3 forward)
        {
            if(!coffinMist.isPlaying)coffinMist.Play();
            Vector3 wind=forward*.85f+Vector3.up*1.45f;
            int count=Mathf.CeilToInt((Application.isMobilePlatform?16:28)*TrialGraphicsBudget.EffectDensity);
            for(int i=0;i<count;i++)
            {
                Vector2 spread=Random.insideUnitCircle;
                Vector3 radial=new Vector3(spread.x,0,spread.y);
                Color tint=Color.Lerp(new Color(.018f,.006f,.036f,.90f),new Color(.35f,.065f,.57f,.65f),Random.value*Random.value);
                coffinMist.Emit(new ParticleSystem.EmitParams{
                    position=foot+Vector3.up*Random.Range(.16f,3f)+radial*.8f,
                    velocity=wind+radial*Random.Range(.9f,2.5f)+Vector3.up*Random.Range(-.35f,1.15f),
                    startSize=Random.Range(.53f,1.42f),startColor=tint
                },1);
            }
        }

        void DisposeInkEffects()
        {if(inkMaterial)Destroy(inkMaterial);if(inkMistMaterial)Destroy(inkMistMaterial);if(inkTexture)Destroy(inkTexture);}
    }
}
