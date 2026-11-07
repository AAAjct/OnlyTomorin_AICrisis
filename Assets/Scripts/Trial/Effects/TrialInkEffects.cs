using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialEffects
    {
        Material inkMaterial,inkMistMaterial;
        Texture2D inkTexture;
        ParticleSystem inkMist;

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

        void InkBloom(Vector3 point,Vector3 drift,float strength)
        {
            if(!inkMist)
            {
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
                var obj=new GameObject("Ink wash droplets");obj.transform.SetParent(transform,false);
                inkMist=obj.AddComponent<ParticleSystem>();inkMist.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=inkMist.main;main.playOnAwake=false;main.loop=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=Application.isMobilePlatform?120:260;
                main.startLifetime=new ParticleSystem.MinMaxCurve(.16f,.42f);main.startSpeed=0;main.gravityModifier=-.08f;
                var emission=inkMist.emission;emission.enabled=false;var shape=inkMist.shape;shape.enabled=false;
                var color=inkMist.colorOverLifetime;color.enabled=true;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.7f,0),new GradientAlphaKey(.4f,.35f),new GradientAlphaKey(0,1)});color.color=gradient;
                var size=inkMist.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.4f,1),new Keyframe(1,1.5f)));
                inkMist.GetComponent<ParticleSystemRenderer>().sharedMaterial=inkMistMaterial;
            }
            inkMist.Play();int count=Mathf.CeilToInt((Application.isMobilePlatform?5:10)*strength*TrialGraphicsBudget.EffectDensity);
            for(int i=0;i<count;i++)
                inkMist.Emit(new ParticleSystem.EmitParams{position=point+Random.insideUnitSphere*.28f*strength,velocity=drift+Random.insideUnitSphere*.9f,startSize=Random.Range(.12f,.38f)*strength,startColor=new Color(.008f,.006f,.012f,.65f),rotation=Random.Range(0,360)},1);
        }

        void DisposeInkEffects()
        {if(inkMaterial)Destroy(inkMaterial);if(inkMistMaterial)Destroy(inkMistMaterial);if(inkTexture)Destroy(inkTexture);}
    }
}
