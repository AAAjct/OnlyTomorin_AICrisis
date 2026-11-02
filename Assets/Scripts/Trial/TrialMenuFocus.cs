using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed class TrialMenuFocus : MonoBehaviour,ISelectHandler,IDeselectHandler,IPointerEnterHandler,IPointerExitHandler
    {
        Image background;Text caption;bool selected,hover;
        void Awake(){background=GetComponent<Image>();caption=GetComponentInChildren<Text>();}
        void Paint()
        {
            if(!background)return;bool active=selected||hover;
            background.color=active?new Color(.95f,.72f,.32f,1):new Color(.19f,.16f,.115f,.96f);
            if(caption)caption.color=active?new Color(.07f,.055f,.03f):new Color(.90f,.87f,.78f);
        }
        void OnEnable(){Paint();}
        void OnDisable(){selected=hover=false;Paint();}
        public void OnSelect(BaseEventData e){selected=true;Paint();}
        public void OnDeselect(BaseEventData e){selected=false;Paint();}
        public void OnPointerEnter(PointerEventData e){hover=true;Paint();}
        public void OnPointerExit(PointerEventData e){hover=false;Paint();}
    }
}
