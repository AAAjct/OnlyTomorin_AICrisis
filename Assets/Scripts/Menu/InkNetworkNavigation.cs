using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu
    {
        readonly List<Selectable> networkOrder=new List<Selectable>(10);
        void SelectNetworkEntry()
        {
            RefreshNetworkNavigation();
            (manager.Busy ? (manager.IsHost ? begin : leave) : host).Select();
        }
        void RefreshNetworkNavigation()
        {
            networkOrder.Clear();
            AddNetworkChoice(nickname);AddNetworkChoice(address);AddNetworkChoice(port);
            AddNetworkChoice(host);AddNetworkChoice(join);AddNetworkChoice(levelDropdown);
            AddNetworkChoice(begin);AddNetworkChoice(leave);AddNetworkChoice(back);
            for(int i=0;i<networkOrder.Count;i++)
            {
                var nav=new Navigation {mode=Navigation.Mode.Explicit};
                nav.selectOnUp=networkOrder[(i+networkOrder.Count-1)%networkOrder.Count];
                nav.selectOnDown=networkOrder[(i+1)%networkOrder.Count];
                nav.selectOnLeft=nav.selectOnUp;nav.selectOnRight=nav.selectOnDown;
                networkOrder[i].navigation=nav;
            }
            if(!network || !network.gameObject.activeSelf || !EventSystem.current || levelList && levelList.activeSelf)return;
            var current=EventSystem.current.currentSelectedGameObject;
            var selected=current?current.GetComponent<Selectable>():null;
            if(!selected || !selected.IsActive() || !selected.IsInteractable())
            {if(networkOrder.Count>0)networkOrder[networkOrder.Count-1].Select();}
        }
        void AddNetworkChoice(Selectable value)
        {if(value && value.gameObject.activeInHierarchy && value.IsInteractable())networkOrder.Add(value);}
        void UpdateNetworkNavigation(InputField field,Gamepad pad,Keyboard keys)
        {
            if(!network.gameObject.activeSelf || quitPanel.activeSelf)return;
            // InputField treats move events as caret movement while editing. Release editing
            // before EventSystem processes the same controller event, retaining its repeat timer.
            if(field && field.isFocused && pad!=null &&
               (pad.dpad.ReadValue().sqrMagnitude>.1f || pad.leftStick.ReadValue().sqrMagnitude>.25f || pad.buttonEast.wasPressedThisFrame))
                field.DeactivateInputField();
            if(keys!=null && keys.tabKey.wasPressedThisFrame)
            {
                if(field)field.DeactivateInputField();
                var current=EventSystem.current.currentSelectedGameObject;
                int index=networkOrder.FindIndex(s=>s.gameObject==current);
                if(networkOrder.Count>0)networkOrder[(index+(keys.shiftKey.isPressed?-1:1)+networkOrder.Count)%networkOrder.Count].Select();
            }
            if(pad!=null && pad.buttonEast.wasPressedThisFrame && !field)
            {if(manager.Busy)manager.Leave();else Show(home);}
        }
    }
}
