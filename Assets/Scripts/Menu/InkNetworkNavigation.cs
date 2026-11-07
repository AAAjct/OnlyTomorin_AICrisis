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
        void SelectNetworkEntry()
        {
            SelectNetworkDefault();
            RefreshNetworkNavigation();
        }

        void SelectNetworkDefault()
        {
            Selectable entry=manager.Busy ? (manager.IsHost ? begin : leave) : host;
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
            AddRow(nickname);
            AddRow(address,port,configureTunnel);
            AddRow(host,join);
            AddRow(begin,leave);
            AddRow(back);
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
            if(networkOrder.Count>0 && CanNavigate(networkDifficulty[0]))
            {
                int leftCount=networkOrder.Count;
                var leftEntry=networkOrder[0];
                var firstLevel=networkLevelRows[0].button;
                var lastLevel=networkLevelRows[networkLevelRows.Count-1].button;
                WireDifficultyNavigation(networkDifficulty,lastLevel,firstLevel,leftEntry);
                networkOrder.AddRange(networkDifficulty);
                for(int i=0;i<networkLevelRows.Count;i++)
                {
                    var row=networkLevelRows[i];
                    SetDirectionalNavigation(row.button,
                        up:i>0?networkLevelRows[i-1].button:networkDifficulty[0],
                        down:i+1<networkLevelRows.Count?networkLevelRows[i+1].button:networkDifficulty[0],
                        left:leftEntry);
                    networkOrder.Add(row.button);
                }
                for(int i=0;i<leftCount;i++)
                {
                    var nav=networkOrder[i].navigation;
                    if(networkOrder.IndexOf(nav.selectOnRight)>i)continue;
                    nav.selectOnRight=networkDifficulty[0];
                    networkOrder[i].navigation=nav;
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
            if(!EventSystem.current || !network.gameObject.activeSelf || quitPanel.activeSelf || levelList && levelList.activeSelf)return;
            var current=EventSystem.current.currentSelectedGameObject;
            var selected=current?current.GetComponent<Selectable>():null;
            if(!networkOrder.Contains(selected) && networkOrder.Count>0)
                SelectNetworkDefault();
        }

        void UpdateNetworkNavigation(InputField field,Gamepad pad,Keyboard keys)
        {
            if(!network.gameObject.activeSelf || quitPanel.activeSelf)return;
            if(NavigationActive)RestoreNetworkFocus();
            if(field && field.isFocused && pad!=null && pad.buttonEast.wasPressedThisFrame)
            {
                field.DeactivateInputField();
                return;
            }
            if(EventSystem.current && keys!=null && keys.tabKey.wasPressedThisFrame)
            {
                if(field)field.DeactivateInputField();
                var current=EventSystem.current.currentSelectedGameObject;
                int index=networkOrder.FindIndex(s=>s.gameObject==current);
                if(networkOrder.Count>0)networkOrder[(index+(keys.shiftKey.isPressed?-1:1)+networkOrder.Count)%networkOrder.Count].Select();
            }
            if(pad!=null && pad.buttonEast.wasPressedThisFrame && networkEditEndedFrame!=Time.frameCount)
            {if(manager.Busy)manager.Leave();else Show(home);}
        }
    }
}
