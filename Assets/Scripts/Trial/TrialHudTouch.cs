using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        RectTransform touchRoot;
        GameObject touchControls,touchMenu;
        Rect safeArea;
        void InitializeTouchControls(bool force=false)
        {
            if(!Application.isMobilePlatform && !force)return;
            touchRoot=Panel("Mobile safe area",root,Vector2.zero,Vector2.zero,Vector2.zero,Color.clear);
            touchRoot.pivot=new Vector2(.5f,.5f);
            touchControls=Panel("Touch gameplay",touchRoot,Vector2.zero,Vector2.zero,Vector2.zero,Color.clear).gameObject;
            var holder=(RectTransform)touchControls.transform;holder.anchorMin=Vector2.zero;holder.anchorMax=Vector2.one;holder.offsetMin=holder.offsetMax=Vector2.zero;
            var lookPad=Panel("Drag to look",holder,new Vector2(.5f,0),Vector2.zero,Vector2.zero,Color.clear);
            lookPad.anchorMax=Vector2.one;lookPad.offsetMin=Vector2.zero;lookPad.offsetMax=Vector2.zero;
            var look=BindTouch(lookPad);look.Look=true;
            var stick=Panel("Move joystick",holder,new Vector2(0,0),new Vector2(65,265),new Vector2(190,190),new Color(.05f,.09f,.10f,.48f));
            var move=BindTouch(stick);move.Stick=true;
            var knob=Panel("Thumb",stick,new Vector2(.5f,.5f),Vector2.zero,new Vector2(65,65),new Color(.8f,.68f,.44f,.85f));knob.pivot=new Vector2(.5f,.5f);move.Knob=knob;
            TouchButton(holder,"轻击",new Vector2(-180,160),1);
            TouchButton(holder,"重击",new Vector2(-285,225),2);
            TouchButton(holder,"闪避",new Vector2(-80,255),4);
            var guard=TouchButton(holder,"格挡",new Vector2(-180,340),0);guard.Guard=true;
            TouchButton(holder,"锁定",new Vector2(-285,350),16);
            var potion=TouchButton(holder,"药瓶",new Vector2(-80,405),8);
            flaskText.rectTransform.SetParent(potion.transform,false);
            flaskText.rectTransform.anchorMin=flaskText.rectTransform.anchorMax=new Vector2(0,1);
            flaskText.rectTransform.anchoredPosition=new Vector2(62,-54);flaskText.rectTransform.sizeDelta=new Vector2(24,22);
            flaskText.fontSize=16;flaskText.color=gold;
            TouchButton(holder,"奔跑",new Vector2(-390,160),64);
            TouchButton(holder,"交互",new Vector2(-390,280),32);
            var pause=TouchButton(touchRoot,"暂停",new Vector2(-120,-25),128);var r=(RectTransform)pause.transform;r.anchorMin=r.anchorMax=Vector2.one;
            touchMenu=pause.gameObject;
        }
        TrialTouchControl BindTouch(RectTransform rect)
        {rect.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;var control=rect.gameObject.AddComponent<TrialTouchControl>();control.Input=game.Input;return control;}
        TrialTouchControl TouchButton(RectTransform parent,string label,Vector2 point,byte action)
        {
            var rect=Panel(label,parent,new Vector2(1,0),point,new Vector2(90,80),new Color(.05f,.09f,.10f,.75f));
            Label("Caption",rect,label,new Vector2(0,1),Vector2.zero,rect.sizeDelta,20,ivory,TextAnchor.MiddleCenter);
            var control=BindTouch(rect);control.Action=action;return control;
        }
        void UpdateTouchControls()
        {
            if(!touchRoot)return;
            if(safeArea!=Screen.safeArea || touchRoot.anchorMax==touchRoot.anchorMin)
            {
                safeArea=Screen.safeArea;
                touchRoot.anchorMin=new Vector2(safeArea.xMin/Screen.width,safeArea.yMin/Screen.height);
                touchRoot.anchorMax=new Vector2(safeArea.xMax/Screen.width,safeArea.yMax/Screen.height);
                touchRoot.offsetMin=touchRoot.offsetMax=Vector2.zero;
            }
            bool playing=game.Mode==TrialMode.Playing;
            if(touchControls.activeSelf!=playing){touchControls.SetActive(playing);game.Input.ClearTouch();}
            touchMenu.SetActive(playing);
        }
    }
}
