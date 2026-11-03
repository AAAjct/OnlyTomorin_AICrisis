using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        GameObject skillStrip,gameEmblem;
        int artworkBindingVersion=-1;
        bool artworkPad;
        string interactPrompt;
        readonly UnityEngine.UI.Text[] skillKeys=new UnityEngine.UI.Text[6];
        readonly UnityEngine.UI.Image[] skillImages=new UnityEngine.UI.Image[6];
        readonly UnityEngine.UI.Image[] skillMarks=new UnityEngine.UI.Image[6];

        void InitializeArtwork()
        {
            gameEmblem=Icon("Game emblem",menu,"GameIcon",new Vector2(0,1),new Vector2(526,-195),new Vector2(88,88)).gameObject;
            menuTitle.rectTransform.sizeDelta=new Vector2(455,110);
            skillStrip=Panel("Skills",root,new Vector2(0,0),new Vector2(42,158),new Vector2(408,88),Color.clear).gameObject;
            string[] assets={"LightSlash","HeavySlash","Parry","Dodge","SpiritDew","TargetLock"};
            string[] keys={"Attack","Heavy","Guard","Dodge","Heal","Lock"};
            for(int i=0;i<assets.Length;i++)
            {
                var slot=Panel(assets[i]+" slot",skillStrip.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(i*68,0),new Vector2(56,84),Color.clear);
                skillImages[i]=Icon(assets[i],slot,assets[i],new Vector2(0,1),Vector2.zero,new Vector2(56,56));
                skillMarks[i]=Panel("Active",slot,new Vector2(0,1),new Vector2(14,-57),new Vector2(28,2),Color.clear).GetComponent<UnityEngine.UI.Image>();
                skillKeys[i]=Label("Key",slot,TrialBindings.Display(keys[i]),new Vector2(0,1),new Vector2(0,-64),new Vector2(56,20),11,muted,TextAnchor.MiddleCenter);
                if(i==4)
                {
                    flaskText.rectTransform.SetParent(slot,false);flaskText.rectTransform.anchorMin=flaskText.rectTransform.anchorMax=new Vector2(0,1);
                    flaskText.rectTransform.anchoredPosition=new Vector2(34,-35);flaskText.rectTransform.sizeDelta=new Vector2(22,21);flaskText.fontSize=14;flaskText.alignment=TextAnchor.MiddleCenter;flaskText.color=ivory;
                }
            }
        }

        UnityEngine.UI.Image Icon(string label,RectTransform parent,string asset,Vector2 anchor,Vector2 pos,Vector2 size)
        {
            var image=Panel(label,parent,anchor,pos,size,Color.white).GetComponent<UnityEngine.UI.Image>();
            image.sprite=Resources.Load<Sprite>("Trial/Icons/"+asset);image.preserveAspect=true;
            if(!image.sprite){image.color=Color.clear;Debug.LogError("Missing UI icon: "+asset,this);}
            return image;
        }

        void UpdateArtwork()
        {
            if(artworkBindingVersion!=TrialBindings.Version || artworkPad!=game.Input.UsingGamepad)
            {
                artworkBindingVersion=TrialBindings.Version;
                artworkPad=game.Input.UsingGamepad;
                interactPrompt=touchRoot?"点击休息 · 敌人重生":TrialBindings.Display("Interact",artworkPad?2:0)+"  休息（敌人重生）";
                string[] ids={"Attack","Heavy","Guard","Dodge","Heal","Lock"};
                for(int i=0;i<ids.Length;i++)skillKeys[i].text=TrialBindings.Display(ids[i],artworkPad?2:0);
            }
            gameEmblem.SetActive(game.Mode==TrialMode.Title);
            bool playing=game.Mode==TrialMode.Playing;
            bool showSkills=playing&&!touchRoot&&!Application.isMobilePlatform;
            if(skillStrip.activeSelf!=showSkills)skillStrip.SetActive(showSkills);
            if(!playing)return;
            var player=game.Player;
            SkillState(0,player.Alive&&player.Stamina>=19,player.Action==FighterAction.Light);
            SkillState(1,player.Alive&&player.Stamina>=35,player.Action==FighterAction.Heavy);
            SkillState(2,player.Alive&&player.Stamina>1,player.Guarding);
            SkillState(3,player.Alive&&player.Stamina>=player.DodgeCost,player.Action==FighterAction.Dodge);
            SkillState(4,player.Alive&&player.Flasks>0&&player.Health<player.MaxHealth,player.Action==FighterAction.Heal);
            SkillState(5,player.Alive,player.Locked);
        }

        void SkillState(int index,bool available,bool active)
        {
            var tint=available||active?Color.white:new Color(.48f,.48f,.48f,1);
            if(skillImages[index].sprite&&skillImages[index].color!=tint)skillImages[index].color=tint;
            var mark=active?gold:Color.clear;if(skillMarks[index].color!=mark)skillMarks[index].color=mark;
        }
    }
}
