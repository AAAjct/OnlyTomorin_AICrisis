using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        GameObject skillStrip;
        readonly UnityEngine.UI.Image[] skillImages=new UnityEngine.UI.Image[6];
        readonly UnityEngine.UI.Image[] skillMarks=new UnityEngine.UI.Image[6];

        void InitializeArtwork()
        {
            Icon("Game emblem",menu,"GameIcon",new Vector2(0,1),new Vector2(526,-195),new Vector2(88,88));
            menuTitle.rectTransform.sizeDelta=new Vector2(455,110);
            skillStrip=Panel("Skills",root,new Vector2(0,0),new Vector2(42,158),new Vector2(408,88),Color.clear).gameObject;
            string[] assets={"LightSlash","HeavySlash","Parry","Dodge","SpiritDew","TargetLock"};
            string[] keys={"左键","Q","右键","Space","R","Tab"};
            for(int i=0;i<assets.Length;i++)
            {
                var slot=Panel(assets[i]+" slot",skillStrip.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(i*68,0),new Vector2(56,84),Color.clear);
                skillImages[i]=Icon(assets[i],slot,assets[i],new Vector2(0,1),Vector2.zero,new Vector2(56,56));
                skillMarks[i]=Panel("Active",slot,new Vector2(0,1),new Vector2(14,-57),new Vector2(28,2),Color.clear).GetComponent<UnityEngine.UI.Image>();
                Label("Key",slot,keys[i],new Vector2(0,1),new Vector2(0,-64),new Vector2(56,20),11,muted,TextAnchor.MiddleCenter);
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
            bool playing=game.Mode==TrialMode.Playing;
            if(skillStrip.activeSelf!=playing)skillStrip.SetActive(playing);
            if(!playing)return;
            var player=game.Player;
            SkillState(0,player.Alive&&player.Stamina>=19,player.Action==FighterAction.Light);
            SkillState(1,player.Alive&&player.Stamina>=35,player.Action==FighterAction.Heavy);
            SkillState(2,player.Alive&&player.Stamina>1,player.Guarding);
            SkillState(3,player.Alive&&player.Stamina>=25,player.Action==FighterAction.Dodge);
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
