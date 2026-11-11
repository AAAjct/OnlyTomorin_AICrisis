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
        public bool PreserveLayout {get;set;}
        internal bool Emphasized { get; set; }
        internal bool FlatBackground { get; set; }
        internal UnityEngine.UI.Image[] BorderGraphics { get; set; }
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
                captionText.fontStyle=FontStyle.Normal;
                if(!PreserveLayout){captionText.fontSize=rect.sizeDelta.y>=55?25:rect.sizeDelta.y>=40?21:17;
                caption.anchoredPosition=new Vector2(44,0);caption.sizeDelta=new Vector2(rect.sizeDelta.x-60,rect.sizeDelta.y);}
            }
            if(!prefix && !PreserveLayout)foreach(var label in GetComponentsInChildren<UnityEngine.UI.Text>(true))if(label!=captionText){prefix=label;break;}
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
            var paper=new Color(.98f,.965f,.925f);
            if(FlatBackground)
            {
                bool focused=enabled && selected && InkMainMenu.NavigationActive;
                bool hover=enabled && pointerOver && !InkMainMenu.NavigationActive;
                Color fill=!enabled?new Color(.15f,.14f,.12f,.035f):Emphasized?hoverTint:
                    focused?new Color(.77f,.88f,.96f):hover?new Color(.88f,.87f,.82f):Color.clear;
                if(pressed && enabled)fill=Color.Lerp(fill,new Color(.15f,.22f,.29f),.14f);
                if(brush)brush.enabled=false;
                if(wash)wash.color=fill;
                if(captionText)captionText.color=!enabled?new Color(.35f,.36f,.34f,.45f):Emphasized?paper:baseTint;
                var edge=focused?(Emphasized?new Color(.25f,.85f,1f):new Color(.04f,.38f,.8f)):
                    !enabled?new Color(.14f,.16f,.15f,.2f):new Color(.14f,.16f,.15f,hover?.85f:.55f);
                if(BorderGraphics!=null)foreach(var graphic in BorderGraphics)if(graphic)
                {
                    graphic.color=edge;
                    // Keep the focus stroke inside the button so nearby controls cannot clip it.
                    var rect=graphic.rectTransform;
                    float thickness=focused?5f:2f;
                    if(rect.anchorMin.y==rect.anchorMax.y)
                        rect.SetInsetAndSizeFromParentEdge(rect.anchorMin.y>.5f?RectTransform.Edge.Top:RectTransform.Edge.Bottom,0,thickness);
                    else if(rect.anchorMin.x==rect.anchorMax.x)
                        rect.SetInsetAndSizeFromParentEdge(rect.anchorMin.x>.5f?RectTransform.Edge.Right:RectTransform.Edge.Left,0,thickness);
                }
                return;
            }
            if(brush)
            {
                Color fill=Emphasized?Color.Lerp(hoverTint,new Color(.44f,.12f,.09f),amount):hoverTint;
                fill.a=(Emphasized?(enabled?.94f:.14f):amount)*(pressed?.85f:1);
                brush.enabled=!FlatBackground;
                brush.color=fill;
                if(FlatBackground && wash)wash.color=fill;
            }
            if(captionText)captionText.color=enabled?Color.Lerp(baseTint,paper,Emphasized?1:amount):new Color(.35f,.36f,.34f,.45f);
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
