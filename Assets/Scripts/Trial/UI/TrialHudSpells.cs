using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        static readonly string[] SpellCosts={"满","½"};
        static readonly Color[] SpellTints={
            new Color(.75f,.50f,1),new Color(.55f,1,.93f)
        };
        static readonly Color SpellInk=new Color(.025f,.04f,.075f,.90f);
        static readonly (float Time,string Text)[] CoffinPhrases={
            (0,"隐隐透出浑浊的纹章，"),(2.6f,"桀骜不驯张狂的才能，"),
            (6.9f,"潮涌、否定、麻痹一瞬，阻碍长眠，"),(12.2f,"爬行的铁之公主，不断自残的泥制人偶，"),
            (15.6f,"结合吧，反弹吧，"),(18.1f,"延伸至地面，知晓自身的无力吧！")
        };
        const float SpellHudWidth=242;
        // Voice cues in blackCoffinChant.wav, in seconds from the start of the clip.
        const float CoffinTitleTime=20.35f,CoffinFirstPhraseEnd=21.36f;
        const float CoffinLastPhraseStart=22.26f,CoffinLastPhraseEnd=23.19f;
        static readonly int TitlePhase=Shader.PropertyToID("_TitlePhase");
        RectTransform spellHud,chantCard,coffinTitle,mirrorTitle;
        Canvas coffinTitleCanvas;
        bool coffinTitleVisible;
        Image spellGauge;
        Text chantText;
        TrialCoffinDistortion chantDistortion;
        Material chantGlyphMaterial,coffinTitleMaterial,mirrorTitleMaterial;
        readonly float[] chantStops=new float[CoffinPhrases.Length];
        readonly Button[] spellButtons=new Button[TrialPlayer.SpellCount];
        readonly Image[] spellAccents=new Image[TrialPlayer.SpellCount];
        readonly Text[] spellLabels=new Text[TrialPlayer.SpellCount];
        readonly Text[] spellKeys=new Text[TrialPlayer.SpellCount];
        readonly TrialSpellIcon[] spellIcons=new TrialSpellIcon[TrialPlayer.SpellCount];
        int spellBindingVersion=-1;
        bool spellBindingPad;
        Coroutine mirrorScreenRoutine;
        bool mirrorTitlePending;
        TrialMirrorScreen mirrorScreen;
        Texture2D mirrorFrame;

        void InitializeSpellHud()
        {
            RectTransform parent=touchRoot?touchRoot:root;
            var bottom=new Vector2(.5f,0);
            spellHud=Panel("Spell slots",parent,bottom,new Vector2(0,touchRoot?20:16),new Vector2(SpellHudWidth,84),Color.clear);
            spellHud.pivot=bottom;
            for(int i=0;i<spellButtons.Length;i++)
            {
                int slot=i;
                var size=new Vector2(115,66);
                string name=TrialBindings.Spells[i].Label;
                var rect=Panel(name,spellHud,new Vector2(0,1),new Vector2(3+i*121,0),size,SpellInk);
                var surface=rect.GetComponent<Image>();
                surface.raycastTarget=true;
                var button=spellButtons[i]=rect.gameObject.AddComponent<Button>();
                button.targetGraphic=surface;
                button.navigation=new Navigation{mode=Navigation.Mode.None};
                button.onClick.AddListener(()=>game.Input.PressSpell(slot));
                spellAccents[i]=Panel("School accent",rect,new Vector2(0,1),Vector2.zero,new Vector2(115,2),SpellTints[i]).GetComponent<Image>();
                var key=spellKeys[i]=Label("Key",rect,"",new Vector2(0,1),new Vector2(7,-6),new Vector2(34,18),14,SpellTints[i]);
                key.resizeTextForBestFit=true;key.resizeTextMinSize=7;key.resizeTextMaxSize=14;
                key.verticalOverflow=VerticalWrapMode.Truncate;
                Label("Cost",rect,SpellCosts[i],new Vector2(1,1),new Vector2(-37,-6),new Vector2(29,18),14,SpellTints[i],TextAnchor.MiddleRight);
                var icon=new GameObject("Skill icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(TrialSpellIcon)).GetComponent<TrialSpellIcon>();
                icon.rectTransform.SetParent(rect,false);
                icon.rectTransform.anchorMin=icon.rectTransform.anchorMax=new Vector2(.5f,.5f);
                icon.rectTransform.sizeDelta=new Vector2(48,43);
                icon.rectTransform.anchoredPosition=Vector2.up*7;
                icon.raycastTarget=false;
                icon.SetSlot(i);
                spellIcons[i]=icon;
                spellLabels[i]=Label("Spell",rect,name,new Vector2(0,1),new Vector2(0,-46),new Vector2(115,18),13,ivory,TextAnchor.MiddleCenter);
            }
            const float gaugeWidth=SpellHudWidth-6;
            spellGauge=Bar("Spell energy",spellHud,new Vector2(3,-73),new Vector2(gaugeWidth,9),SpellTints[1]);
            float mirrorThreshold=(float)TrialPlayer.SpellCost(1)/TrialPlayer.MaxSpellEnergy;
            Panel("Cost threshold",spellHud,new Vector2(0,1),new Vector2(3+gaugeWidth*mirrorThreshold,-73),new Vector2(1,9),new Color(.92f,1,1,.65f));
            promptText.rectTransform.anchoredPosition=new Vector2(-320,174);
            InitializeCoffinChant();
            mirrorTitle=Panel("Mirror invocation",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1000,140),new Color(.01f,.034f,.061f,.68f));
            mirrorTitle.pivot=new Vector2(.5f,.5f);
            var caption=Label("Release phrase",mirrorTitle,TrialPlayer.MirrorPhrase,new Vector2(0,1),Vector2.zero,new Vector2(1000,140),82,new Color(.84f,1,1),TextAnchor.MiddleCenter);
            caption.fontStyle=FontStyle.Bold;
            var mirrorShader=Resources.Load<Shader>("Trial/VFX/MirrorTitle");
            if(mirrorShader && mirrorShader.isSupported)
            {
                canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;
                mirrorTitleMaterial=new Material(mirrorShader);
                caption.material=mirrorTitleMaterial;
                caption.gameObject.AddComponent<TrialMirrorTitleMesh>();
            }
            mirrorTitle.gameObject.SetActive(false);
            spellHud.gameObject.SetActive(false);
        }

        void UpdateSpellHud()
        {
            var player=game.Player;
            bool playing=game.Mode==TrialMode.Playing && player.Alive && !player.ChoosingBoon && !SettingsOpen;
            spellHud.gameObject.SetActive(playing);
            bool chanting=playing && player.Chanting;
            float elapsed=player.ChantTime;
            bool showTitle=chanting && elapsed>=CoffinTitleTime;
            chantCard.gameObject.SetActive(chanting && !showTitle);
            // Submit the hidden title during the chant so its first visible frame reuses the mesh and pipeline.
            coffinTitleCanvas.enabled=coffinTitleMaterial?chanting:showTitle;
            if(!showTitle && coffinTitleVisible && coffinTitleMaterial)
                coffinTitleMaterial.SetVector(TitlePhase,new Vector4(0,-1,0,0));
            coffinTitleVisible=showTitle;
            float focus=showTitle?Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-CoffinTitleTime)/.24f)):0;
            chantDistortion?.Update(chanting,Mathf.Clamp01(elapsed/game.Effects.CoffinChantDuration),elapsed,focus);
            game.Effects.UpdateCoffinVoice(player.Chanting,playing,elapsed);
            if((mirrorTitlePending || mirrorScreenRoutine!=null) &&
                (game.HasResult || !player.Alive || !playing && Mirror.NetworkClient.active && !Mirror.NetworkServer.active))
                ClearMirrorScreen();
            mirrorTitle.gameObject.SetActive(playing && mirrorTitlePending);
            if(mirrorScreen)mirrorScreen.gameObject.SetActive(playing);
            if(!playing)return;
            var available=(RectTransform)spellHud.parent;
            spellHud.localScale=Vector3.one*Mathf.Clamp(available.rect.width*.78f/SpellHudWidth,.35f,1);
            if(spellBindingVersion!=TrialBindings.Version || spellBindingPad!=game.Input.UsingGamepad)
            {
                spellBindingVersion=TrialBindings.Version;
                spellBindingPad=game.Input.UsingGamepad;
                for(int i=0;i<spellKeys.Length;i++)
                {
                    var entry=TrialBindings.Spells[i];
                    int slot=spellBindingPad?2:string.IsNullOrEmpty(TrialBindings.Path(entry,0))?1:0;
                    spellKeys[i].text=TrialBindings.Display(entry,slot);
                }
            }
            int energy=player.SpellEnergy;
            spellGauge.fillAmount=(float)energy/TrialPlayer.MaxSpellEnergy;
            for(int i=0;i<spellButtons.Length;i++)
            {
                bool ready=energy>=TrialPlayer.SpellCost(i) && (i!=0 || !player.Chanting);
                spellButtons[i].interactable=ready;
                spellAccents[i].color=ready?SpellTints[i]:SpellTints[i]*.45f;
                spellIcons[i].color=ready?SpellTints[i]:Color.Lerp(SpellTints[i],muted,.65f)*.78f;
                spellLabels[i].color=ready?ivory:muted;
            }
            if(showTitle)UpdateCoffinTitle(elapsed);
            else if(chanting)UpdateCoffinChant(elapsed);
        }

        void InitializeCoffinChant()
        {
            InitializeImpactFlash();
            var glyphShader=Resources.Load<Shader>("Trial/VFX/BlackCoffinChant");
            if(glyphShader && glyphShader.isSupported)
            {
                chantGlyphMaterial=new Material(glyphShader);
                coffinTitleMaterial=new Material(glyphShader);
                coffinTitleMaterial.EnableKeyword("COFFIN_TITLE");
            }
            var pressureShader=Resources.Load<Shader>("Trial/VFX/BlackCoffinPressure");
            if(pressureShader && pressureShader.isSupported)chantDistortion=new TrialCoffinDistortion(view,pressureShader);

            chantCard=Panel("Black coffin chant",root,new Vector2(.5f,.73f),Vector2.zero,new Vector2(950,68),new Color(.018f,.004f,.032f,.52f));
            chantCard.pivot=new Vector2(.5f,.5f);
            chantCard.gameObject.AddComponent<RectMask2D>().softness=new Vector2Int(70,0);
            chantText=Label("Scrolling incantation",chantCard,"",new Vector2(.5f,1),Vector2.zero,new Vector2(0,68),36,new Color(.96f,.86f,1),TextAnchor.MiddleLeft);
            chantText.horizontalOverflow=HorizontalWrapMode.Overflow;
            chantText.supportRichText=false;
            if(chantGlyphMaterial)chantText.material=chantGlyphMaterial;
            var shadow=chantText.gameObject.AddComponent<Shadow>();
            shadow.effectColor=new Color(.08f,.01f,.15f,.92f);shadow.effectDistance=new Vector2(1,-2);
            for(int i=0;i<CoffinPhrases.Length;i++)
            {
                if(i>0)chantText.text+="　";
                float start=chantText.preferredWidth;
                chantText.text+=CoffinPhrases[i].Text;
                chantStops[i]=(start+chantText.preferredWidth)*.5f;
            }
            chantText.rectTransform.sizeDelta=new Vector2(chantText.preferredWidth+4,68);
            chantCard.gameObject.SetActive(false);

            var title=Label("Black coffin close-up",root,"破道之九十·黑棺",new Vector2(.5f,.5f),
                Vector2.zero,new Vector2(1120,210),116,new Color(.97f,.84f,1),TextAnchor.MiddleCenter);
            coffinTitle=title.rectTransform;
            coffinTitle.pivot=new Vector2(.5f,.5f);
            coffinTitleCanvas=title.gameObject.AddComponent<Canvas>();
            title.fontStyle=FontStyle.Bold;
            title.horizontalOverflow=HorizontalWrapMode.Overflow;
            if(title.font.dynamic)title.font.RequestCharactersInTexture(title.text,title.fontSize,title.fontStyle);
            if(coffinTitleMaterial)
            {
                canvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;
                coffinTitleCanvas.additionalShaderChannels|=AdditionalCanvasShaderChannels.TexCoord1;
                title.material=coffinTitleMaterial;
                coffinTitleMaterial.SetVector(TitlePhase,new Vector4(0,-1,0,0));
                title.gameObject.AddComponent<TrialCoffinTitleMesh>();
            }
            coffinTitleCanvas.enabled=false;
        }

        void UpdateCoffinChant(float elapsed)
        {
            chantCard.localScale=Vector3.one*Mathf.Min(1,root.rect.width*.90f/chantCard.rect.width);
            int phrase=0;
            while(phrase<CoffinPhrases.Length-1 && elapsed>=CoffinPhrases[phrase+1].Time)phrase++;
            bool last=phrase==CoffinPhrases.Length-1;
            float nextTime=last?CoffinTitleTime:CoffinPhrases[phrase+1].Time;
            float nextStop=last?chantStops[phrase]+85:chantStops[phrase+1];
            float t=Mathf.InverseLerp(CoffinPhrases[phrase].Time,nextTime,elapsed);
            chantText.rectTransform.anchoredPosition=Vector2.left*Mathf.Lerp(chantStops[phrase],nextStop,t);
        }

        void UpdateCoffinTitle(float elapsed)
        {
            bool reduced=InkMainMenu.ReducedMotion;
            float first=ChantEase(CoffinTitleTime,CoffinFirstPhraseEnd,elapsed);
            float last=ChantEase(CoffinLastPhraseStart,CoffinLastPhraseEnd,elapsed);
            float pulse=ChantEase(CoffinTitleTime,20.58f,elapsed)*(1-ChantEase(21.15f,CoffinFirstPhraseEnd,elapsed))
                +ChantEase(CoffinLastPhraseStart,22.49f,elapsed)*(1-ChantEase(23.04f,CoffinLastPhraseEnd,elapsed));
            float fit=Mathf.Min(1,root.rect.width*.93f/coffinTitle.rect.width);
            var scale=new Vector3(fit,fit,1);
            if(coffinTitle.localScale!=scale)coffinTitle.localScale=scale;
            if(coffinTitleMaterial)coffinTitleMaterial.SetVector(TitlePhase,new Vector4(first,reduced?0:elapsed-CoffinTitleTime+1,reduced?0:pulse,last));
        }

        static float ChantEase(float from,float to,float elapsed)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,elapsed));

        void DisposeSpellHud()
        {
            ClearMirrorScreen();
            chantDistortion?.Dispose();
            if(chantGlyphMaterial)Destroy(chantGlyphMaterial);
            if(coffinTitleMaterial)Destroy(coffinTitleMaterial);
            if(mirrorTitleMaterial)Destroy(mirrorTitleMaterial);
        }

        internal void ShowMirrorTitle()
        {
            if(!mirrorTitle)return;
            ClearMirrorScreen();
            mirrorTitlePending=true;
            mirrorTitle.localScale=Vector3.one*Mathf.Min(1,root.rect.width*.92f/mirrorTitle.rect.width);
            mirrorTitle.SetAsLastSibling();
            mirrorTitle.gameObject.SetActive(CanPresentMirror);
        }

        internal void ShatterMirrorScreen()
        {
            if(!mirrorTitlePending || mirrorScreenRoutine!=null)return;
            mirrorScreenRoutine=StartCoroutine(AnimateMirrorGlass());
        }

        bool CanPresentMirror=>game.Mode==TrialMode.Playing && game.Player.Alive && !game.Player.ChoosingBoon && !SettingsOpen;

        IEnumerator AnimateMirrorGlass()
        {
            yield return new WaitForEndOfFrame();
            while(!CanPresentMirror)yield return new WaitForEndOfFrame();
            mirrorFrame=ScreenCapture.CaptureScreenshotAsTexture();
            mirrorTitlePending=false;
            mirrorTitle.gameObject.SetActive(false);
            var obj=new GameObject("Falling screen glass",typeof(RectTransform),typeof(CanvasRenderer));
            var rect=(RectTransform)obj.transform;
            rect.SetParent(root,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            mirrorScreen=obj.AddComponent<TrialMirrorScreen>();
            mirrorScreen.raycastTarget=false;
            mirrorScreen.Initialize(mirrorFrame);
            for(float age=0;age<TrialPlayer.MirrorGlassDuration;)
            {
                if(CanPresentMirror)
                {
                    mirrorScreen.SetProgress(Mathf.Lerp(.085f,1,age/TrialPlayer.MirrorGlassDuration));
                    age+=Time.unscaledDeltaTime;
                }
                yield return null;
            }
            mirrorScreenRoutine=null;
            ClearMirrorScreen();
        }

        internal void ClearMirrorScreen()
        {
            if(mirrorScreenRoutine!=null)StopCoroutine(mirrorScreenRoutine);
            mirrorTitlePending=false;
            if(mirrorTitle)mirrorTitle.gameObject.SetActive(false);
            if(mirrorScreen)Destroy(mirrorScreen.gameObject);
            if(mirrorFrame)Destroy(mirrorFrame);
            mirrorScreen=null;mirrorFrame=null;mirrorScreenRoutine=null;
        }
    }
}
