using UnityEngine;
using UnityEngine.EventSystems;

namespace TomorinTrial
{
    public sealed class TrialTouchControl : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler
    {
        public TrialInput Input;
        public byte Action;
        public bool Stick,Look,Guard;
        public RectTransform Knob;
        public RectTransform StickVisual;
        Vector2 stickCenter,stickHome;
        int pointer=int.MinValue;
        UnityEngine.UI.Image surface;
        Color idleTint;
        void Awake(){surface=GetComponent<UnityEngine.UI.Image>();if(surface)idleTint=surface.color;}
        public void OnPointerDown(PointerEventData e)
        {
            if(pointer!=int.MinValue)return;pointer=e.pointerId;
            if(surface&&!Stick&&!Look)surface.color=new Color(.38f,.30f,.16f,.95f);
            if(Guard)Input.TouchGuard=true;
            else if(!Stick&&!Look){if(Action==2)Input.TouchHeavy=true;if(Action==1)Input.TouchLight=true;Input.PressTouch(Action);}
            if(Stick)
            {
                if(StickVisual)
                {
                    var rect=(RectTransform)transform;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out stickCenter);
                    stickHome=StickVisual.anchoredPosition;
                    StickVisual.anchoredPosition=stickCenter-rect.rect.min;
                }
                OnDrag(e);
            }
        }
        public void OnDrag(PointerEventData e)
        {
            if(pointer!=e.pointerId)return;
            if(Look){Input.TouchLook+=e.delta*.13f;return;}
            if(!Stick)return;
            var rect=(RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var local);
            float width=StickVisual?StickVisual.rect.width:rect.rect.width;
            var value=Vector2.ClampMagnitude((local-(StickVisual?stickCenter:rect.rect.center))/(width*.38f),1);
            Input.TouchMove=value;
            // A small release margin keeps a finger near the rim from flickering sprint.
            Input.TouchSprint=value.sqrMagnitude>=(Input.TouchSprint?.85f*.85f:.95f*.95f);
            if(Knob)Knob.anchoredPosition=value*width*.30f;
        }
        public void OnPointerUp(PointerEventData e){if(pointer==e.pointerId)Release();}
        void Release(){bool held=pointer!=int.MinValue;pointer=int.MinValue;if(surface)surface.color=idleTint;if(!Input)return;if(Guard)Input.TouchGuard=false;if(Action==2)Input.TouchHeavy=false;if(Action==1)Input.TouchLight=false;if(Stick)Input.TouchSprint=false;if(Stick)Input.TouchMove=Vector2.zero;if(Knob)Knob.anchoredPosition=Vector2.zero;if(held&&StickVisual)StickVisual.anchoredPosition=stickHome;}
        void OnDisable(){Release();}
        void OnApplicationFocus(bool focused){if(!focused)Release();}
    }
}
