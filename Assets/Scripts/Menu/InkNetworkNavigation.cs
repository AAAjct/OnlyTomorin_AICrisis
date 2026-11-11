using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu
    {
        readonly List<Selectable> networkOrder=new List<Selectable>(24);
        int networkEditEndedFrame=-1;
        EventSystem manualNavigationEvents;
        bool originalNavigationEvents;
        Vector2 navigationDirection;
        float nextNavigationMove;
        void SelectNetworkEntry()
        {
            SelectNetworkDefault();
            RefreshNetworkNavigation();
        }

        void SelectNetworkDefault()
        {
            Selectable entry=manager.Busy ? (manager.IsHost ? begin : leave) : roomFormOpen ? (creatingRoom ? host : join) : browseRooms;
            if(CanNavigate(entry))entry.Select();
            else if(CanNavigate(leave))leave.Select();
            else if(CanNavigate(back))back.Select();
        }

        static bool CanNavigate(Selectable choice)=>choice && choice.IsActive() && choice.IsInteractable();

        static void SetDirectionalNavigation(Selectable choice,Selectable up=null,Selectable down=null,
            Selectable left=null,Selectable right=null)
        {
            choice.navigation=new Navigation
            {
                mode=Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down,
                selectOnLeft=left,selectOnRight=right
            };
        }

        void RefreshNetworkNavigation()
        {
            networkOrder.Clear();
            int previousRow=-1;
            AddRow(browseRooms);
            AddRow(createRoomTab);
            AddRow(joinRoomTab);
            AddRow(nickname);
            AddRow(searchRooms);
            foreach(var row in roomRows) AddRow(row);
            AddRow(moreRooms);
            AddRow(address);
            AddRow(roomName);
            AddRow(changeLevel);
            AddRow(decreaseCapacity,increaseCapacity);
            AddRow(relayHostMode,directHostMode);
            AddRow(roomVisibility,configureTunnel,port);
            AddRow(publicEndpoint);
            AddRow(host,join);
            AddRow(previousRosterPage,nextRosterPage);
            AddRow(begin,leave);
            AddRow(back,copyRoomCode);
            if(previousRow>=0)
            {
                for(int i=0;i<networkOrder.Count && !networkOrder[i].navigation.selectOnUp;i++)
                {
                    var nav=networkOrder[i].navigation;
                    nav.selectOnUp=networkOrder[previousRow];
                    networkOrder[i].navigation=nav;
                }
                for(int i=previousRow;i<networkOrder.Count;i++)
                {
                    var nav=networkOrder[i].navigation;
                    nav.selectOnDown=networkOrder[0];
                    networkOrder[i].navigation=nav;
                }
            }
            if(!manager.Busy)
            {
                Selectable first=RoomBrowserOpen
                    ? (roomRows.Count>0 && CanNavigate(roomRows[0])?roomRows[0]:searchRooms)
                    : creatingRoom?roomName:address;
                var sidebar=new[]{browseRooms,createRoomTab,joinRoomTab,back};
                for(int i=0;i<sidebar.Length;i++)
                {
                    var nav=sidebar[i].navigation;
                    nav.selectOnUp=sidebar[(i+sidebar.Length-1)%sidebar.Length];
                    nav.selectOnDown=sidebar[(i+1)%sidebar.Length];
                    nav.selectOnRight=CanNavigate(first)?first:nickname;
                    sidebar[i].navigation=nav;
                }
                Selectable activeTab=roomFormOpen?(creatingRoom?createRoomTab:joinRoomTab):browseRooms;
                foreach(var control in networkOrder)
                {
                    if(System.Array.IndexOf(sidebar,control)>=0)continue;
                    var nav=control.navigation;
                    if(nav.selectOnLeft==control || nav.selectOnLeft==null)nav.selectOnLeft=activeTab;
                    control.navigation=nav;
                }
            }
            RestoreNetworkFocus();

            void AddRow(params Selectable[] choices)
            {
                int first=networkOrder.Count;
                foreach(var choice in choices)
                    if(CanNavigate(choice))networkOrder.Add(choice);
                if(first==networkOrder.Count)return;

                var up=previousRow>=0?networkOrder[previousRow]:null;
                for(int i=first;i<networkOrder.Count;i++)
                    SetDirectionalNavigation(networkOrder[i],up:up,
                        left:networkOrder[i>first?i-1:networkOrder.Count-1],
                        right:networkOrder[i+1<networkOrder.Count?i+1:first]);
                // A vertical move enters the adjacent row at its first available control.
                for(int i=previousRow;i>=0 && i<first;i++)
                {
                    var nav=networkOrder[i].navigation;
                    nav.selectOnDown=networkOrder[first];
                    networkOrder[i].navigation=nav;
                }
                previousRow=first;
            }
        }

        void RestoreNetworkFocus()
        {
            if(!EventSystem.current || !network.gameObject.activeSelf || quitPanel.activeSelf ||
                settings.gameObject.activeInHierarchy || TrialSettingsPanel.InputBlocked || NetworkLevelsOpen || levelList && levelList.activeSelf)return;
            var current=EventSystem.current.currentSelectedGameObject;
            var selected=current?current.GetComponent<Selectable>():null;
            if(!networkOrder.Contains(selected) && networkOrder.Count>0)
                SelectNetworkDefault();
        }

        void SetManualNetworkNavigation(bool active)
        {
            var events=EventSystem.current;
            if(active && events)
            {
                if(manualNavigationEvents!=events)
                {
                    SetManualNetworkNavigation(false);
                    manualNavigationEvents=events;
                    originalNavigationEvents=events.sendNavigationEvents;
                }
                // Keep pointer and text input in the UI module. Dispatch navigation once here.
                events.sendNavigationEvents=false;
            }
            else
            {
                if(manualNavigationEvents)manualNavigationEvents.sendNavigationEvents=originalNavigationEvents;
                manualNavigationEvents=null;
                navigationDirection=Vector2.zero;
            }
        }

        bool UpdateNetworkNavigation(InputField field,Gamepad pad,Keyboard keys)
        {
            bool active=network.gameObject.activeInHierarchy && !quitPanel.activeSelf && !settings.gameObject.activeInHierarchy;
            SetManualNetworkNavigation(active);
            if(!active)return false;
            if(!EventSystem.current || TrialSettingsPanel.InputBlocked)return true;
            if(NetworkLevelsOpen)
            {
                var selected=EventSystem.current.currentSelectedGameObject;
                if(!selected || !selected.transform.IsChildOf(networkLevels))networkDifficulty[0].Select();
            }
            else RestoreNetworkFocus();

            bool cancel=keys?.escapeKey.wasPressedThisFrame==true || pad?.buttonEast.wasPressedThisFrame==true;
            bool submit=keys?.enterKey.wasPressedThisFrame==true || keys?.spaceKey.wasPressedThisFrame==true || pad?.buttonSouth.wasPressedThisFrame==true;
            if(keys?.tabKey.wasPressedThisFrame==true)
            {
                if(field)field.DeactivateInputField();
                var order=networkOrder;
                if(NetworkLevelsOpen)
                {
                    order=new List<Selectable>(networkDifficulty);
                    foreach(var row in networkLevelRows)order.Add(row.button);
                    order.Add(closeNetworkLevels);
                }
                int index=order.FindIndex(s=>s.gameObject==EventSystem.current.currentSelectedGameObject);
                if(order.Count>0)order[(index+(keys.shiftKey.isPressed?-1:1)+order.Count)%order.Count].Select();
                NavigationActive=true;
                return true;
            }
            if(field && field.isFocused)
            {
                if(cancel || (pad?.buttonSouth.wasPressedThisFrame==true))field.DeactivateInputField();
                navigationDirection=Vector2.zero;
                return true;
            }
            if(networkEditEndedFrame==Time.frameCount)return true;
            if(cancel)
            {
                if(NetworkLevelsOpen)CloseNetworkLevels();
                else if(manager.Busy)LeaveNetworkRoom();
                else if(roomFormOpen)OpenRoomBrowser();
                else Show(home);
                return true;
            }

            Vector2 move=Vector2.zero;
            if(keys!=null)
            {
                move.x=(keys.dKey.isPressed || keys.rightArrowKey.isPressed?1:0)-(keys.aKey.isPressed || keys.leftArrowKey.isPressed?1:0);
                move.y=(keys.wKey.isPressed || keys.upArrowKey.isPressed?1:0)-(keys.sKey.isPressed || keys.downArrowKey.isPressed?1:0);
            }
            if(move==Vector2.zero && pad!=null)
                move=pad.dpad.ReadValue().sqrMagnitude>.25f?pad.dpad.ReadValue():pad.leftStick.ReadValue();
            if(move.sqrMagnitude<.3f)move=Vector2.zero;
            else move=Mathf.Abs(move.x)>Mathf.Abs(move.y)?new Vector2(Mathf.Sign(move.x),0):new Vector2(0,Mathf.Sign(move.y));

            if(move!=Vector2.zero && (move!=navigationDirection || Time.unscaledTime>=nextNavigationMove))
            {
                nextNavigationMove=Time.unscaledTime+(move!=navigationDirection?.35f:.11f);
                NavigationActive=true;
                var axis=new AxisEventData(EventSystem.current)
                {
                    moveVector=move,
                    moveDir=move.x>0?MoveDirection.Right:move.x<0?MoveDirection.Left:move.y>0?MoveDirection.Up:MoveDirection.Down
                };
                ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,axis,ExecuteEvents.moveHandler);
            }
            navigationDirection=move;
            if(submit)
            {
                NavigationActive=true;
                var selected=EventSystem.current.currentSelectedGameObject;
                var input=selected?selected.GetComponent<InputField>():null;
                if(input)input.ActivateInputField();
                else ExecuteEvents.Execute(selected,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
            }
            return true;
        }
    }
}
