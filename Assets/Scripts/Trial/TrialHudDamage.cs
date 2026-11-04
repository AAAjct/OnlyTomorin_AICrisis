using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        Image damageVeil,damageBearing,healingVeil;
        float healUntil;
        public void HealFlash(){healUntil=Time.unscaledTime+.85f;}
        Text damageCaption;
        Texture2D damageTexture;
        Sprite damageSprite;
        float damageUntil,bearingUntil,nextHeartbeat,observedHealth;
        Vector3 damageSource;

        void InitializeDamageFeedback()
        {
            const int size=128;
            damageTexture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Soft damage vignette",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float edge=Mathf.Max(Mathf.Abs(2f*x/(size-1)-1),Mathf.Abs(2f*y/(size-1)-1));
                pixels[y*size+x]=new Color(1,1,1,Mathf.Pow(Mathf.InverseLerp(.42f,1,edge),2.3f));
            }
            damageTexture.SetPixels(pixels);damageTexture.Apply(false,true);
            damageSprite=Sprite.Create(damageTexture,new Rect(0,0,size,size),Vector2.one*.5f);
            var rect=Panel("Damage · soft screen edges",root,Vector2.zero,Vector2.zero,Vector2.zero,Color.clear);
            rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            damageVeil=rect.GetComponent<Image>();damageVeil.sprite=damageSprite;damageVeil.raycastTarget=false;
            damageCaption=Label("Damage · health loss",root,"",Vector2.one*.5f,new Vector2(-200,-125),new Vector2(400,36),23,new Color(1,.65f,.53f),TextAnchor.MiddleCenter);
            damageCaption.raycastTarget=false;
            damageBearing=Panel("Damage · source direction",root,Vector2.one*.5f,Vector2.zero,new Vector2(44,5),Color.clear).GetComponent<Image>();
            damageBearing.raycastTarget=false;damageBearing.rectTransform.pivot=Vector2.one*.5f;
            var healRect=Panel("Spirit dew · green fade",root,Vector2.zero,Vector2.zero,Vector2.zero,Color.clear);
            healRect.anchorMax=Vector2.one;healRect.offsetMin=healRect.offsetMax=Vector2.zero;
            healingVeil=healRect.GetComponent<Image>();healingVeil.sprite=damageSprite;healingVeil.raycastTarget=false;
            observedHealth=game.Player.Health;
        }
        public void SetDamageDirection(Vector3 source)
        {damageSource=source;bearingUntil=Time.unscaledTime+.85f;}
        void UpdateDamageFeedback()
        {
            float hp=game.Player.Health,now=Time.unscaledTime;
            bool playing=game.Mode==TrialMode.Playing && game.Player.Alive;
            if(playing && hp>observedHealth+.5f)HealFlash();
            healingVeil.color=new Color(.12f,.9f,.40f,playing?Mathf.Clamp01((healUntil-now)/.85f)*.35f:0);
            if(playing && hp<observedHealth)
            {
                damageUntil=now+.65f;
                damageCaption.text="受伤  −"+Mathf.CeilToInt(observedHealth-hp);
            }
            if(hp>observedHealth || !playing){damageUntil=0;if(!playing)bearingUntil=0;}
            observedHealth=hp;
            float flash=Mathf.Clamp01((damageUntil-now)/.65f);
            float danger=playing?Mathf.Clamp01((.3f-hp/game.Player.MaxHealth)/.3f):0;
            float pulse=InkMainMenu.ReducedMotion?1:.8f+.2f*Mathf.Sin(now*4);
            damageVeil.color=new Color(.65f,.025f,.012f,Mathf.Max(flash*.7f,danger*.35f*pulse));
            var captionColor=damageCaption.color;captionColor.a=flash;damageCaption.color=captionColor;
            damageBearing.color=new Color(1,.25f,.12f,playing?Mathf.Clamp01((bearingUntil-now)/.85f):0);
            if(bearingUntil>now && damageSource.sqrMagnitude>.001f)
            {
                var local=view.transform.InverseTransformDirection(damageSource);
                float angle=Mathf.Atan2(local.x,local.z);
                damageBearing.rectTransform.anchoredPosition=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle))*145;
                damageBearing.rectTransform.localRotation=Quaternion.Euler(0,0,-angle*Mathf.Rad2Deg);
            }
            if(danger>0 && now>=nextHeartbeat)
            {nextHeartbeat=now+1.25f;game.Effects.Sound("heartbeat",game.Player.transform.position);}
        }
        void DisposeDamageFeedback()
        {if(damageSprite)Destroy(damageSprite);if(damageTexture)Destroy(damageTexture);}
    }
}
