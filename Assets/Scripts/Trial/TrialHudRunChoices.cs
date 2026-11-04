using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        GameObject boonPanel;
        UnityEngine.UI.Text boonDescription;
        readonly UnityEngine.UI.Button[] boonButtons=new UnityEngine.UI.Button[3];
        readonly Dictionary<TrialPlayer,RectTransform> nameplates=new Dictionary<TrialPlayer,RectTransform>();
        readonly List<TrialPlayer> expiredNameplates=new List<TrialPlayer>();
        bool boonWasOpen;
        UnityEngine.UI.Text partyWaiting;
        TrialBoon shownOfferA,shownOfferB,shownOfferC;
        readonly UnityEngine.UI.Text[] boonLabels=new UnityEngine.UI.Text[3];
        UnityEngine.UI.Image impactFlash;
        Color flashTint;
        float flashUntil;
        public void Flash(Color color)
        {
            if(!impactFlash)
            {
                var rect=Panel("Impact flash",root,Vector2.zero,Vector2.zero,Vector2.zero,Color.clear);rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                impactFlash=rect.GetComponent<UnityEngine.UI.Image>();impactFlash.raycastTarget=false;
            }
            flashTint=color;flashUntil=Time.unscaledTime+.16f;impactFlash.transform.SetAsLastSibling();
        }

        void UpdateRunChoices()
        {
            if(impactFlash){var color=flashTint;color.a*=Mathf.Clamp01((flashUntil-Time.unscaledTime)/.16f);impactFlash.color=color;}
            bool choosing=game.Player.ChoosingBoon && game.Mode==TrialMode.Playing;
            bool waiting=TrialNetworkManager.SessionActive && TrialNetworkManager.Current.WaitingForBoons && !choosing && game.Mode==TrialMode.Playing;
            if(waiting && !partyWaiting)partyWaiting=Label("Party boon waiting",root,"等待同伴选择祝福…",new Vector2(.5f,.5f),new Vector2(-260,80),new Vector2(520,50),25,gold,TextAnchor.MiddleCenter);
            if(partyWaiting)partyWaiting.gameObject.SetActive(waiting);
            if(choosing && !boonPanel)
            {
                var overlay=Panel("Choose a run boon",root,Vector2.zero,Vector2.zero,Vector2.zero,new Color(.015f,.025f,.035f,.88f));
                overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;overlay.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;boonPanel=overlay.gameObject;
                var card=Panel("Three choices",overlay,new Vector2(.5f,.5f),new Vector2(-570,230),new Vector2(1140,460),black);
                Label("Boon heading",card,"入庭机缘 · 三选一",new Vector2(0,1),new Vector2(35,-25),new Vector2(1070,55),36,gold);
                boonDescription=Label("Boss boon",card,"",new Vector2(0,1),new Vector2(35,-87),new Vector2(1070,55),19,ivory);
                for(int i=0;i<3;i++)
                {
                    int slot=i;
                    boonButtons[i]=Button("Boon "+i,card,new Vector2(35+i*365,-180),new Vector2(340,220),"",()=>{
                        var player=game.Player;game.SelectRunBoon(slot==0?player.OfferA:slot==1?player.OfferB:player.OfferC);
                    });
                    var label=boonButtons[i].GetComponentInChildren<UnityEngine.UI.Text>();boonLabels[i]=label;label.fontSize=23;label.alignment=TextAnchor.MiddleCenter;
                    var r=boonButtons[i].GetComponent<RectTransform>();r.GetComponent<UnityEngine.UI.Image>().color=new Color(.16f,.21f,.23f,.9f);
                }
            }
            if(boonPanel)
            {
                boonPanel.SetActive(choosing);
                if(choosing)
                {
                    Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
                    var p=game.Player;
                    if(!boonWasOpen || p.OfferA!=shownOfferA || p.OfferB!=shownOfferB || p.OfferC!=shownOfferC)
                    {
                        shownOfferA=p.OfferA;shownOfferB=p.OfferB;shownOfferC=p.OfferC;
                        for(int i=0;i<3;i++){var offer=i==0?p.OfferA:i==1?p.OfferB:p.OfferC;boonLabels[i].text=TrialRunBoons.Name(offer)+"\n\n"+TrialRunBoons.Description(offer);}
                        var fish=game.Enemies[game.Enemies.Count-1].Boss;
                        boonDescription.text=game.IsChampionStage?TrialChampionCatalog.Lesson(game.Level):"本局 Boss 增益："+TrialRunBoons.BossName(fish?fish.Boon:TrialBossBoon.None)+"\n增益仅作用于本次挑战，竞技场不启用。";
                    }
                    if(!boonWasOpen && EventSystem.current)boonButtons[0].Select();
                }
            }
            boonWasOpen=choosing;
        }

        void UpdatePlayerNameplates()
        {
            if(!TrialNetworkManager.SessionActive)return;
            var network=TrialNetworkManager.Current;
            expiredNameplates.Clear();
            foreach(var pair in nameplates)if(!pair.Key){if(pair.Value)Destroy(pair.Value.gameObject);expiredNameplates.Add(pair.Key);}
            foreach(var player in expiredNameplates)nameplates.Remove(player);
            foreach(var actor in network.Players)
            {
                if(!actor || actor==game.Player)continue;
                if(!nameplates.TryGetValue(actor,out var plate))
                {
                    plate=Panel("Player nameplate",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(170,42),Color.clear);
                    plate.pivot=new Vector2(.5f,0);
                    Label("Name",plate,"",new Vector2(0,1),Vector2.zero,new Vector2(170,24),14,ivory,TextAnchor.MiddleCenter);
                    Bar("Health",plate,new Vector2(0,-29),new Vector2(170,5),game.IsArena?new Color(.9f,.23f,.12f):new Color(.45f,.8f,.55f));
                    nameplates[actor]=plate;
                }
                plate.gameObject.SetActive(actor.Alive && game.Mode==TrialMode.Playing);
                if(!plate.gameObject.activeSelf)continue;
                ScreenPosition(plate,actor.transform.position+Vector3.up*2.2f);
                plate.GetComponentInChildren<UnityEngine.UI.Text>().text=network.NameOf(actor);
                plate.Find("Health background/Health").GetComponent<UnityEngine.UI.Image>().fillAmount=actor.Health/actor.MaxHealth;
            }
        }
    }
}
