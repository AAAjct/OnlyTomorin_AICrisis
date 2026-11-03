using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        RectTransform touchRoot;
        GameObject touchControls,touchMenu,touchInteract;
        Rect safeArea;
        void InitializeTouchControls(bool force=false)
        {
            if(!Application.isMobilePlatform && !force)return;
            if(touchRoot)return;
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().matchWidthOrHeight=1;
            touchRoot=Panel("Mobile safe area",root,Vector2.zero,Vector2.zero,Vector2.zero,Color.clear);
            touchRoot.pivot=new Vector2(.5f,.5f);
            touchControls=Panel("Touch gameplay",touchRoot,Vector2.zero,Vector2.zero,Vector2.zero,Color.clear).gameObject;
            var holder=(RectTransform)touchControls.transform;holder.anchorMin=Vector2.zero;holder.anchorMax=Vector2.one;holder.offsetMin=holder.offsetMax=Vector2.zero;
            var lookPad=Panel("Drag to look",holder,new Vector2(.5f,0),Vector2.zero,Vector2.zero,Color.clear);
            lookPad.anchorMax=Vector2.one;lookPad.offsetMin=Vector2.zero;lookPad.offsetMax=Vector2.zero;
            var look=BindTouch(lookPad);look.Look=true;
            // A transparent left-side pad owns its finger until release, including
            // drags outside the visible stick. Right-side buttons stay on top.
            var movePad=Panel("Move touch area",holder,Vector2.zero,Vector2.zero,Vector2.zero,Color.clear);
            movePad.anchorMax=new Vector2(.5f,1);movePad.offsetMin=movePad.offsetMax=Vector2.zero;
            var stick=Panel("Move joystick",movePad,Vector2.zero,new Vector2(90,160),new Vector2(230,230),new Color(.05f,.09f,.10f,.48f));
            stick.pivot=new Vector2(.5f,.5f);stick.anchoredPosition=new Vector2(205,235);
            var move=BindTouch(movePad);move.Stick=true;move.StickVisual=stick;
            var knob=Panel("Thumb",stick,new Vector2(.5f,.5f),Vector2.zero,new Vector2(65,65),new Color(.8f,.68f,.44f,.85f));knob.pivot=new Vector2(.5f,.5f);move.Knob=knob;
            Label("Move caption",stick,"移动",new Vector2(.5f,.5f),new Vector2(-50,-55),new Vector2(100,35),24,ivory,TextAnchor.MiddleCenter);
            // Four distinct thumb targets; secondary actions stay outside this cluster.
            TouchButton(holder,"轻击",new Vector2(-130,135),1,new Vector2(176,176),"LightSlash");
            TouchButton(holder,"重击",new Vector2(-326,125),2,new Vector2(148,148),"HeavySlash");
            TouchButton(holder,"闪避",new Vector2(-130,337),4,new Vector2(156,156),"Dodge");
            var guard=TouchButton(holder,"格挡",new Vector2(-326,315),0,new Vector2(148,148),"Parry");guard.Guard=true;
            TouchButton(holder,"锁定",new Vector2(-130,510),16,new Vector2(136,112),"TargetLock");
            var potion=TouchButton(holder,"药瓶",new Vector2(120,450),8,new Vector2(136,112),"SpiritDew");
            ((RectTransform)potion.transform).anchorMin=((RectTransform)potion.transform).anchorMax=Vector2.zero;
            flaskText.rectTransform.SetParent(potion.transform,false);
            flaskText.rectTransform.anchorMin=flaskText.rectTransform.anchorMax=new Vector2(0,1);
            flaskText.rectTransform.anchoredPosition=new Vector2(102,-10);flaskText.rectTransform.sizeDelta=new Vector2(28,30);
            flaskText.fontSize=24;flaskText.color=gold;
            var sprint=TouchButton(holder,"奔跑",new Vector2(405,150),64,new Vector2(136,112));
            ((RectTransform)sprint.transform).anchorMin=((RectTransform)sprint.transform).anchorMax=Vector2.zero;
            var interact=TouchButton(holder,"休息",new Vector2(0,180),32,new Vector2(188,88));
            ((RectTransform)interact.transform).anchorMin=((RectTransform)interact.transform).anchorMax=new Vector2(.5f,0);
            touchInteract=interact.gameObject;touchInteract.SetActive(false);
            var pause=TouchButton(touchRoot,"暂停",new Vector2(-100,-80),128,new Vector2(136,88));var r=(RectTransform)pause.transform;r.anchorMin=r.anchorMax=Vector2.one;
            touchMenu=pause.gameObject;
            skillStrip.SetActive(false);
            bossPanel.anchorMin=bossPanel.anchorMax=new Vector2(.5f,1);
            bossPanel.anchoredPosition=new Vector2(-440,-200);
            objectiveText.rectTransform.anchoredPosition=new Vector2(-540,-37);
        }
        TrialTouchControl BindTouch(RectTransform rect)
        {rect.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;var control=rect.gameObject.AddComponent<TrialTouchControl>();control.Input=game.Input;return control;}
        TrialTouchControl TouchButton(RectTransform parent,string label,Vector2 point,byte action,Vector2 size,string icon=null)
        {
            var rect=Panel(label,parent,new Vector2(1,0),point,size,new Color(.035f,.065f,.075f,.78f));
            rect.pivot=new Vector2(.5f,.5f);
            Panel("Accent",rect,new Vector2(.5f,1),new Vector2(-24,-2),new Vector2(48,3),action==1?gold:muted);
            if(icon!=null)Icon("Action icon",rect,icon,new Vector2(.5f,1),new Vector2(-28,-12),new Vector2(56,56));
            Label("Caption",rect,label,new Vector2(0,1),new Vector2(0,icon==null?0:-size.y+44),new Vector2(size.x,icon==null?size.y:40),28,ivory,TextAnchor.MiddleCenter);
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
            bool playing=game.Mode==TrialMode.Playing && game.Player.Alive && !game.Player.ChoosingBoon;
            if(touchControls.activeSelf!=playing){touchControls.SetActive(playing);game.Input.ClearTouch();}
            touchMenu.SetActive(playing);
            bool canInteract=playing && !game.IsArena && Vector3.Distance(game.Player.transform.position,game.World.ShrinePosition)<2.6f;
            if(touchInteract.activeSelf!=canInteract)touchInteract.SetActive(canInteract);
        }
    }
}
