using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed class InkLevelRowFocus : MonoBehaviour,ISelectHandler
    {
        public ScrollRect Scroll;
        public void OnSelect(BaseEventData data)
        {
            if(!Scroll)return;
            Canvas.ForceUpdateCanvases();
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(Scroll.viewport,transform);
            var view=Scroll.viewport.rect;var position=Scroll.content.anchoredPosition;
            if(bounds.min.y<view.yMin)position.y+=view.yMin-bounds.min.y;
            else if(bounds.max.y>view.yMax)position.y-=bounds.max.y-view.yMax;
            position.y=Mathf.Clamp(position.y,0,Mathf.Max(0,Scroll.content.rect.height-view.height));
            Scroll.StopMovement();Scroll.content.anchoredPosition=position;
        }
    }
}
