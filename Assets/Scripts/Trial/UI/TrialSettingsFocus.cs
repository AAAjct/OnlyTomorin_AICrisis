using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TomorinTrial
{
    // Paint actual graphic colors: ColorTint multiplies the translucent paper tint,
    // which otherwise makes keyboard/controller selection almost invisible.
    public sealed class TrialSettingsFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
    {
        Selectable control;
        TrialSettingsPanel panel;
        Image background;
        Outline outline;
        Color idle;
        bool hover;

        void Awake()
        {
            control=GetComponent<Selectable>();background=GetComponent<Image>();idle=background.color;
            panel=GetComponentInParent<TrialSettingsPanel>();
            control.transition=Selectable.Transition.None;
            outline=gameObject.AddComponent<Outline>();outline.effectColor=new Color(.61f,.18f,.13f,1);
            outline.effectDistance=new Vector2(2,-2);outline.useGraphicAlpha=false;outline.enabled=false;
        }
        void LateUpdate()
        {
            bool usable=control.IsInteractable();
            bool selected=EventSystem.current && EventSystem.current.currentSelectedGameObject==gameObject;
            bool focused=usable && (panel && panel.PointerFocus?hover:selected);
            background.color=!usable?new Color(.15f,.14f,.12f,.035f):focused?new Color(.94f,.78f,.48f,1):idle;
            outline.enabled=focused;
        }
        void OnDisable(){hover=false;if(background)background.color=idle;if(outline)outline.enabled=false;}
        public void OnPointerEnter(PointerEventData e){hover=true;}
        public void OnPointerExit(PointerEventData e){hover=false;}
        public void OnSelect(BaseEventData e)
        {
            if(panel && !(e is PointerEventData))panel.RevealBinding((RectTransform)transform);
        }
    }
}
