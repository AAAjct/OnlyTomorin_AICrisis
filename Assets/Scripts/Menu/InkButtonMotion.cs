using UnityEngine;
using UnityEngine.EventSystems;

namespace TomorinTrial
{
    public sealed class InkButtonMotion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform caption, rule;
        [SerializeField] UnityEngine.UI.Image wash;
        [SerializeField] Color baseTint, hoverTint;
        [SerializeField] UnityEngine.UI.Graphic brush;
        [SerializeField] UnityEngine.UI.Text prefix;
        UnityEngine.UI.Text captionText;
        UnityEngine.UI.Selectable selectable;
        bool pointerOver, selected, pressed;
        float amount;
        public void Configure(RectTransform text, RectTransform line, UnityEngine.UI.Image background)
        { caption=text;rule=line;wash=background;Restyle(null); }

        public void Restyle(Font font)
        {
            selectable=GetComponent<UnityEngine.UI.Selectable>();
            if(selectable)selectable.transition=UnityEngine.UI.Selectable.Transition.None;
            if(wash){wash.color=Color.clear;wash.raycastTarget=true;}
            if(rule)rule.gameObject.SetActive(false);
            var rect=(RectTransform)transform;
            if(!brush)
            {
                var obj=new GameObject("Hover ink",typeof(RectTransform),typeof(InkButtonWash));
                var r=obj.GetComponent<RectTransform>();r.SetParent(transform,false);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;r.SetAsFirstSibling();
                brush=obj.GetComponent<InkButtonWash>();brush.raycastTarget=false;
            }
            if(!brush.GetComponent<CanvasRenderer>())brush.gameObject.AddComponent<CanvasRenderer>();
            captionText=caption?caption.GetComponent<UnityEngine.UI.Text>():null;
            if(captionText)
            {
                if(font)captionText.font=font;
                captionText.fontStyle=FontStyle.Normal;captionText.fontSize=rect.sizeDelta.y>=55?25:rect.sizeDelta.y>=40?21:17;
                caption.anchoredPosition=new Vector2(44,0);caption.sizeDelta=new Vector2(rect.sizeDelta.x-60,rect.sizeDelta.y);
            }
            if(!prefix)foreach(var label in GetComponentsInChildren<UnityEngine.UI.Text>(true))if(label!=captionText){prefix=label;break;}
            if(prefix){prefix.fontSize=12;prefix.rectTransform.anchoredPosition=new Vector2(10,0);prefix.rectTransform.sizeDelta=new Vector2(30,rect.sizeDelta.y);}
            baseTint=new Color(.065f,.08f,.08f);hoverTint=new Color(.61f,.18f,.13f);
            amount=0;Apply();
        }
        void Awake(){captionText=caption?caption.GetComponent<UnityEngine.UI.Text>():null;selectable=GetComponent<UnityEngine.UI.Selectable>();}
        void OnDisable(){pointerOver=selected=pressed=false;amount=0;Apply();}
        void Update()
        {
            bool active=(!selectable||selectable.IsInteractable())&&(pointerOver||(selected&&InkMainMenu.NavigationActive));
            float target=active?1:0;
            amount=InkMainMenu.ReducedMotion?target:Mathf.MoveTowards(amount,target,Time.unscaledDeltaTime*9);
            Apply();
        }
        void Apply()
        {
            bool enabled=!selectable||selectable.IsInteractable();
            if(brush)brush.color=new Color(hoverTint.r,hoverTint.g,hoverTint.b,amount*(pressed?.85f:1));
            if(captionText)captionText.color=enabled?Color.Lerp(baseTint,new Color(.98f,.965f,.925f),amount):new Color(.35f,.36f,.34f,.45f);
            if(prefix)prefix.color=Color.Lerp(new Color(.36f,.38f,.35f,.75f),new Color(.98f,.965f,.925f,.7f),amount);
        }
        public void OnPointerEnter(PointerEventData e){pointerOver=true;}
        public void OnPointerExit(PointerEventData e){pointerOver=false;pressed=false;}
        public void OnSelect(BaseEventData e){selected=true;}
        public void OnDeselect(BaseEventData e){selected=false;pressed=false;}
        public void OnPointerDown(PointerEventData e){pressed=true;}
        public void OnPointerUp(PointerEventData e){pressed=false;}
    }
}
