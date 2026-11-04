using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TomorinTrial
{
    // Move is dispatched to every handler on this selected object. Both this handler
    // and InputField resolve from the same field, so their execution order is immaterial.
    [RequireComponent(typeof(InputField))]
    public sealed class TrialInputFieldNavigation : MonoBehaviour,IMoveHandler
    {
        InputField field;
        void Awake(){field=GetComponent<InputField>();field.shouldActivateOnSelect=false;}
        public void OnMove(AxisEventData data)
        {
            var pad=Gamepad.current;
            if(pad==null || pad.dpad.ReadValue().sqrMagnitude<.1f && pad.leftStick.ReadValue().sqrMagnitude<.25f)return;
            field.DeactivateInputField();
            var nav=field.navigation;
            var next=data.moveDir==MoveDirection.Up?nav.selectOnUp:data.moveDir==MoveDirection.Down?nav.selectOnDown:data.moveDir==MoveDirection.Left?nav.selectOnLeft:nav.selectOnRight;
            if(next && next.IsActive() && next.IsInteractable())data.selectedObject=next.gameObject;
            data.Use();
        }
    }
}
