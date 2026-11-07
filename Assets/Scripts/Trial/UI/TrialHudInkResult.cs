using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        const float InkResultDuration=3.2f;
        CanvasGroup resultInk;
        RectTransform resultGlyph,resultCut;
        Text resultCharacter;
        readonly RawImage[] resultHalves=new RawImage[2];
        readonly Material[] resultInkMaterials=new Material[3];
        Texture2D resultVictoryInk,resultDefeatInk;
        float resultStarted=-10;
        bool resultWasAlive=true;
        TrialMode resultMode=TrialMode.Title;
        static readonly int RevealId=Shader.PropertyToID("_Reveal");
        static readonly int SplitId=Shader.PropertyToID("_Split");
        static readonly int RedId=Shader.PropertyToID("_Red");

        void InitializeInkResult()
        {
            var holder=Panel("Ink verdict",root,Vector2.one*.5f,Vector2.zero,new Vector2(800,660),Color.clear);
            holder.pivot=Vector2.one*.5f;
            resultInk=holder.gameObject.AddComponent<CanvasGroup>();
            resultInk.blocksRaycasts=false;resultInk.interactable=false;resultInk.alpha=0;
            resultVictoryInk=Resources.Load<Texture2D>("UI/InkVerdictVictory");
            resultDefeatInk=Resources.Load<Texture2D>("UI/InkVerdictDefeat");
            var shader=Resources.Load<Shader>("UI/InkVerdict");
            resultGlyph=new GameObject("Split calligraphy",typeof(RectTransform)).GetComponent<RectTransform>();
            resultGlyph.SetParent(holder,false);resultGlyph.sizeDelta=new Vector2(510,510);resultGlyph.anchoredPosition=new Vector2(0,24);
            for(int i=0;i<2;i++)
            {
                resultHalves[i]=InkResultImage(i==0?"Left ink half":"Right ink half",resultGlyph,new Vector2(510,510));
                if(shader)
                {
                    var material=new Material(shader){name="Verdict ink half "+i};
                    material.SetFloat("_Side",i==0?-1:1);resultInkMaterials[i]=material;resultHalves[i].material=material;
                }
            }
            resultCut=InkResultImage("Central blade incision",holder,new Vector2(510,510)).rectTransform;
            resultCut.anchoredPosition=new Vector2(0,24);
            if(shader)
            {
                resultInkMaterials[2]=new Material(shader){name="Verdict blade incision"};
                resultInkMaterials[2].SetFloat("_Layer",2);resultCut.GetComponent<RawImage>().material=resultInkMaterials[2];
            }
            else resultCut.gameObject.SetActive(false);
            // Keep a readable fallback if an artwork resource is missing.
            resultCharacter=Label("Verdict character",holder,"斩",Vector2.one*.5f,new Vector2(-220,240),new Vector2(440,440),300,new Color(.035f,.028f,.025f),TextAnchor.MiddleCenter);
            holder.gameObject.SetActive(false);
        }

        static RawImage InkResultImage(string name,RectTransform parent,Vector2 size)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(RawImage));
            var rect=obj.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*.5f;rect.sizeDelta=size;
            var image=obj.GetComponent<RawImage>();image.raycastTarget=false;return image;
        }

        public void ShowInkResult(bool victory)
        {
            resultStarted=Time.unscaledTime;resultCharacter.text=victory?"斩":"菜";
            var texture=victory?resultVictoryInk:resultDefeatInk;
            bool useArtwork=texture && resultInkMaterials[0];
            resultCharacter.enabled=!useArtwork;
            foreach(var half in resultHalves){half.texture=texture;half.enabled=useArtwork;}
            resultInk.gameObject.SetActive(true);resultInk.transform.SetAsLastSibling();
            RenderInkResult(0);
        }

        void UpdateInkResult()
        {
            bool alive=game.Player.Alive;
            if(game.Mode==TrialMode.Victory && resultMode!=TrialMode.Victory)ShowInkResult(true);
            else if((!alive && resultWasAlive) || (game.Mode==TrialMode.Dead && resultMode!=TrialMode.Dead))ShowInkResult(false);
            else if((alive && !resultWasAlive) || (game.Mode==TrialMode.Playing && (resultMode==TrialMode.Victory || resultMode==TrialMode.Dead)))resultStarted=-10;
            resultWasAlive=alive;resultMode=game.Mode;
            float age=Time.unscaledTime-resultStarted;
            if(age>=InkResultDuration){if(resultInk.gameObject.activeSelf)resultInk.gameObject.SetActive(false);return;}
            RenderInkResult(age);
        }

        // Unscaled time keeps the ink moving during the victory/death hit stop.
        void RenderInkResult(float age)
        {
            float reveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.56f,age));
            float split=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.78f,1.34f,age));
            float red=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.98f,2.25f,age));
            bool reduced=InkMainMenu.ReducedMotion;
            resultInk.alpha=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.12f,age))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.62f,InkResultDuration,age)));
            resultGlyph.localScale=Vector3.one*(reduced?1:Mathf.Lerp(1.045f,1,reveal));
            for(int i=0;i<2;i++)
            {
                float side=i==0?-1:1;
                resultHalves[i].rectTransform.anchoredPosition=new Vector2(side*split*(reduced?4:30),side*split*(reduced?0:6));
                resultHalves[i].rectTransform.localRotation=Quaternion.Euler(0,0,reduced?0:-side*split*2.3f);
                var material=resultInkMaterials[i];if(!material)continue;
                material.SetFloat(RevealId,reveal);material.SetFloat(SplitId,split);material.SetFloat(RedId,red);
            }
            float incision=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.64f,.82f,age));
            if(resultInkMaterials[2])
            {
                resultInkMaterials[2].SetFloat(RevealId,incision);
                resultInkMaterials[2].SetFloat("_Opacity",(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.84f,1.16f,age)))*(reduced?.45f:1));
            }
            resultCharacter.color=Color.Lerp(new Color(.035f,.028f,.025f),new Color(.72f,.025f,.018f),red);
        }

        void DisposeInkResult()
        {
            foreach(var material in resultInkMaterials)if(material)Destroy(material);
        }
    }
}
