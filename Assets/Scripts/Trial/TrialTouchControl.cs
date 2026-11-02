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
        int pointer=int.MinValue;
        public void OnPointerDown(PointerEventData e)
        {
            if(pointer!=int.MinValue)return;pointer=e.pointerId;
            if(Guard)Input.TouchGuard=true;
            else if(!Stick&&!Look){if(Action==64)Input.TouchSprint=true;Input.PressTouch(Action);}
            if(Stick)OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if(pointer!=e.pointerId)return;
            if(Look){Input.TouchLook+=e.delta*.13f;return;}
            if(!Stick)return;
            var rect=(RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var local);
            var value=Vector2.ClampMagnitude((local-rect.rect.center)/(rect.rect.width*.38f),1);
            Input.TouchMove=value;if(Knob)Knob.anchoredPosition=value*rect.rect.width*.30f;
        }
        public void OnPointerUp(PointerEventData e){if(pointer==e.pointerId)Release();}
        void Release(){pointer=int.MinValue;if(!Input)return;if(Guard)Input.TouchGuard=false;if(Action==64)Input.TouchSprint=false;if(Stick)Input.TouchMove=Vector2.zero;if(Knob)Knob.anchoredPosition=Vector2.zero;}
        void OnDisable(){Release();}
    }
}
