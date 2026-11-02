using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu
    {
        GameObject levelList;
        bool listForNetwork;
        UnityEngine.UI.Button courtyardRow,arenaRow;
        UnityEngine.UI.Text listTitle;
        GameObject selectionReturn;

        void OpenLevelList(bool multiplayerList)
        {
            if(multiplayerList && manager.Busy && !manager.IsHost)return;
            if(!levelList)
            {
                var overlay=Panel("Shared level list",root,0,0,1600,900,new Color(.03f,.04f,.04f,.7f));Stretch(overlay);overlay.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;levelList=overlay.gameObject;
                var card=Panel("Level list card",overlay,0,0,840,475,Paper);card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);card.anchoredPosition=Vector2.zero;
                Line(card,0,0,3,475,Red);listTitle=Label(card,"选择关卡",32,26,770,58,34,Ink);
                arenaRow=Button(card,"竞技场",24,115,785,100,"");
                Label(arenaRow.transform,"玩家自由决斗 · 至少两人",44,53,700,30,17,Grey);
                courtyardRow=Button(card,"庭院 (Boss · 大肥鱼)",24,225,785,100,"");
                Label(courtyardRow.transform,"清理庭院守卫，解除封印，再迎战大肥鱼",44,53,700,30,17,Grey);
                Button(card,"返回",24,365,220,60,"←").onClick.AddListener(CloseLevelList);
                arenaRow.onClick.AddListener(()=>SelectListedLevel(TrialLevel.Arena));
                courtyardRow.onClick.AddListener(()=>SelectListedLevel(TrialLevel.BigFish));
                foreach(var row in new[]{arenaRow,courtyardRow})
                {var text=row.GetComponentInChildren<UnityEngine.UI.Text>();text.rectTransform.sizeDelta=new Vector2(720,50);text.fontSize=26;}
            }
            selectionReturn=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            listForNetwork=multiplayerList;arenaRow.gameObject.SetActive(multiplayerList);
            courtyardRow.GetComponent<RectTransform>().anchoredPosition=new Vector2(24,multiplayerList?-225:-115);
            listTitle.text=multiplayerList?"联机关卡":"单人游玩 · 选择关卡";
            levelList.SetActive(true);levelList.transform.SetAsLastSibling();
            (multiplayerList?arenaRow:courtyardRow).Select();
        }
        void SelectListedLevel(TrialLevel level)
        {
            if(listForNetwork)manager.SelectLevel(level);
            CloseLevelList();
            if(!listForNetwork)StartCoroutine(LoadSolo());
        }
        void CloseLevelList(){if(levelList)levelList.SetActive(false);if(selectionReturn && EventSystem.current)EventSystem.current.SetSelectedGameObject(selectionReturn);}
        bool UpdateLevelList()
        {
            if(!levelList || !levelList.activeSelf)return false;
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true || Gamepad.current?.buttonEast.wasPressedThisFrame==true)CloseLevelList();
            return true;
        }
    }
}
