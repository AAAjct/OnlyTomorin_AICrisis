using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed class TrialModalBackdrop : MonoBehaviour, IPointerClickHandler
    {
        RectTransform card;
        System.Action dismiss;

        public static void Bind(RectTransform backdrop, RectTransform card, System.Action dismiss)
        {
            backdrop.GetComponent<Image>().raycastTarget=true;
            var handler=backdrop.gameObject.AddComponent<TrialModalBackdrop>();
            handler.card=card;
            handler.dismiss=dismiss;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button==PointerEventData.InputButton.Left &&
               !RectTransformUtility.RectangleContainsScreenPoint(card,eventData.pressPosition,eventData.pressEventCamera) &&
               !RectTransformUtility.RectangleContainsScreenPoint(card,eventData.position,eventData.pressEventCamera))
                dismiss();
        }
    }
}
