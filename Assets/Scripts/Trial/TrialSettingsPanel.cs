using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TomorinTrial
{
    // Both menus create this same view: one layout, one rebinding and preference flow.
    public sealed class TrialSettingsPanel : MonoBehaviour
    {
        Font font;
        RectTransform card;
        Action back;
        UnityEngine.UI.Slider volume;
        UnityEngine.UI.Text sprintLabel,motionLabel,status;
        UnityEngine.UI.Button first;
        readonly List<UnityEngine.UI.Selectable> navigation=new List<UnityEngine.UI.Selectable>();
        readonly List<Action> refreshBindings=new List<Action>();
        InputAction captureAction;
        InputActionRebindingExtensions.RebindingOperation operation;
        UnityEngine.UI.Button captureButton;
        bool rebinding,accepted;
        int openedFrame,armFrame;
        float captureDeadline;
        TrialBindings.Entry pendingEntry;
        int pendingSlot;
        static TrialSettingsPanel active;
        static int suppressFrame=-1;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInputState(){active=null;suppressFrame=-1;}
        public static bool InputBlocked => active || Time.frameCount<=suppressFrame;
        public bool IsOpen=>gameObject.activeSelf;
        public bool IsRebinding=>rebinding;
        internal bool PointerFocus { get; private set; }
        static readonly Color Paper=new Color(.96f,.945f,.90f,.99f),Ink=new Color(.065f,.08f,.08f),Red=new Color(.61f,.18f,.13f),Grey=new Color(.25f,.28f,.27f);

        public static TrialSettingsPanel Create(RectTransform parent,Font font,Action back)
        {
            var obj=new GameObject("Shared settings",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(TrialSettingsPanel));
            var rect=obj.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            obj.GetComponent<UnityEngine.UI.Image>().color=new Color(.03f,.04f,.04f,.65f);
            var panel=obj.GetComponent<TrialSettingsPanel>();panel.font=font;panel.back=back;panel.Build();obj.SetActive(false);return panel;
        }
        void Build()
        {
            card=Box("Settings card",transform,0,0,1120,800,Paper);card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);card.anchoredPosition=Vector2.zero;
            Box("Cinnabar rule",card,0,0,3,800,Red);
            Label("设置  /  SETTINGS",card,36,26,1040,52,34,Ink);
            Label("声音与操作",card,36,108,300,30,23,Ink);
            Label("主音量",card,36,170,300,32,20,Ink);
            var track=Box("Master volume",card,36,222,300,28,new Color(.15f,.14f,.12f,.12f));
            volume=track.gameObject.AddComponent<UnityEngine.UI.Slider>();volume.minValue=0;volume.maxValue=1;
            var handle=Box("Handle",track,0,0,20,28,Red);handle.pivot=new Vector2(.5f,.5f);volume.handleRect=handle;handle.sizeDelta=new Vector2(20,0);volume.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();
            track.gameObject.AddComponent<TrialSettingsFocus>();
            volume.onValueChanged.AddListener(v=>{AudioListener.volume=v;PlayerPrefs.SetFloat("Ink.Volume",v);});navigation.Add(volume);
            first=Button("Sprint mode",card,36,290,300,48,"",()=>{TrialPreferences.ToggleSprint=!TrialPreferences.ToggleSprint;Refresh();});sprintLabel=first.GetComponentInChildren<UnityEngine.UI.Text>();
            Label("切换：按一次开始，再按一次停止。\n长按：按住奔跑，松开停止。",card,36,354,310,64,16,Grey);
            motionLabel=Button("Motion",card,36,440,300,48,"",()=>{InkMainMenu.SetReducedMotion(!InkMainMenu.ReducedMotion);Refresh();}).GetComponentInChildren<UnityEngine.UI.Text>();
            Label("点击右侧键位后，按下新的按键。\n重复占用会提示冲突，Esc 取消。\n手柄移动与视角保留双摇杆。",card,36,510,310,100,16,Grey);
            Label("按键设置",card,390,108,200,30,23,Ink);
            Label("键鼠主键",card,512,145,130,25,16,Grey);Label("备用键",card,650,145,110,25,16,Grey);Label("手柄",card,772,145,290,25,16,Grey);
            int row=0;
            foreach(var entry in TrialBindings.Entries)
            {
                float y=182+row++*34;Label(entry.Label,card,390,y+3,115,30,18,Ink);
                for(int slot=0;slot<3;slot++)
                {
                    int captured=slot;float x=slot==0?512:slot==1?650:772;float width=slot==0?130:slot==1?110:290;
                    if(entry.Defaults[slot]==null){Label("—",card,x,y+3,width,30,16,Grey);continue;}
                    UnityEngine.UI.Button button=null;button=Button("Bind "+entry.Id+" "+slot,card,x,y,width,30,"",()=>BeginRebind(entry,captured,button));
                    var label=button.GetComponentInChildren<UnityEngine.UI.Text>();refreshBindings.Add(()=>label.text=TrialBindings.Display(entry,captured));
                }
            }
            status=Label("",card,390,640,684,58,16,Red);
            Button("Restore bindings",card,390,718,300,48,"恢复默认键位",()=>{TrialBindings.ResetDefaults();Refresh();status.text="已恢复默认键位。";});
            Button("Back",card,772,718,300,48,"返回",()=>back());
            // Explicit links keep all controls inside the modal, including across device columns.
            for(int i=0;i<navigation.Count;i++)
            {
                var control=navigation[i];var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=navigation[(i+navigation.Count-1)%navigation.Count],selectOnDown=navigation[(i+1)%navigation.Count]};
                if(!(control is UnityEngine.UI.Slider)){nav.selectOnLeft=nav.selectOnUp;nav.selectOnRight=nav.selectOnDown;}control.navigation=nav;
            }
        }
        public void Show()
        {gameObject.SetActive(true);transform.SetAsLastSibling();active=this;openedFrame=Time.frameCount;volume.SetValueWithoutNotify(AudioListener.volume);Refresh();status.text="键位自动保存，开始菜单与游戏内共用。";first.Select();}
        public void Hide(){gameObject.SetActive(false);}
        void Refresh(){sprintLabel.text=TrialPreferences.SprintCaption;motionLabel.text="画面动效："+(InkMainMenu.ReducedMotion?"关闭":"开启");foreach(var update in refreshBindings)update();}
        public void BeginRebind(TrialBindings.Entry entry,int slot,UnityEngine.UI.Button button)
        {
            if(rebinding)return;pendingEntry=entry;pendingSlot=slot;captureButton=button;rebinding=true;accepted=false;armFrame=Time.frameCount+1;captureDeadline=Time.unscaledTime+12;
            foreach(var item in navigation)item.interactable=false;
            status.text="「"+entry.Label+"」：请按下新的"+(slot==2?"手柄按钮":"键盘键或鼠标按钮")+"（Esc 取消，12 秒内有效）。";
        }
        void ArmCapture()
        {
            captureAction=new InputAction("Rebind",InputActionType.Button);captureAction.AddBinding(TrialBindings.Path(pendingEntry,pendingSlot));
            operation=captureAction.PerformInteractiveRebinding(0).WithExpectedControlType("Button").WithCancelingThrough("<Keyboard>/escape").WithMatchingEventsBeingSuppressed().WithTimeout(12).OnMatchWaitForAnother(.08f);
            if(pendingSlot==2)operation.WithControlsHavingToMatchPath("<Gamepad>/*");
            else operation.WithControlsExcluding("<Gamepad>").WithControlsExcluding("<Touchscreen>").WithControlsExcluding("<Joystick>").WithControlsExcluding("<XRController>");
            operation.OnApplyBinding((op,path)=>{accepted=TrialBindings.TrySet(pendingEntry,pendingSlot,path,out var error);status.text=accepted?"已保存「"+pendingEntry.Label+"」的新键位。":error;})
                .OnCancel(op=>FinishCapture("已取消改键。"))
                .OnComplete(op=>FinishCapture(null)).Start();
        }
        void FinishCapture(string message)
        {
            var current=operation;operation=null;current?.Dispose();captureAction?.Dispose();captureAction=null;rebinding=false;
            suppressFrame=Time.frameCount+1;foreach(var item in navigation)item.interactable=true;Refresh();
            if(message!=null)status.text=message;if(captureButton && gameObject.activeInHierarchy)captureButton.Select();
        }
        void Update()
        {
            var mouse=Mouse.current;var keys=Keyboard.current;var pad=Gamepad.current;
            if(mouse!=null && (mouse.delta.ReadValue().sqrMagnitude>.1f || mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))PointerFocus=true;
            if((keys!=null && (keys.upArrowKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame)) ||
                (pad!=null && (pad.dpad.ReadValue().sqrMagnitude>.1f || pad.leftStick.ReadValue().sqrMagnitude>.25f || pad.buttonSouth.wasPressedThisFrame)))PointerFocus=false;
            if(Time.frameCount<=openedFrame)return;
            if(rebinding)
            {
                if((Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)||(Gamepad.current!=null && Gamepad.current.startButton.wasPressedThisFrame)||Time.unscaledTime>=captureDeadline)
                {if(operation!=null)operation.Cancel();else FinishCapture("已取消改键。");return;}
                // Wait for the activating click/confirm to release before accepting a replacement.
                bool held=(Mouse.current!=null && Mouse.current.leftButton.isPressed)||(Keyboard.current!=null && Keyboard.current.enterKey.isPressed)||(Gamepad.current!=null && Gamepad.current.buttonSouth.isPressed);
                if(operation==null && Time.frameCount>armFrame && !held)ArmCapture();return;
            }
            if(Time.frameCount<=suppressFrame)return;
            if((Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)||(Gamepad.current!=null && Gamepad.current.startButton.wasPressedThisFrame))back();
        }
        void OnDisable(){if(operation!=null)operation.Cancel();else if(rebinding)FinishCapture(null);if(active==this)active=null;suppressFrame=Time.frameCount+1;PlayerPrefs.Save();}
        void OnApplicationFocus(bool focus){if(!focus && rebinding){if(operation!=null)operation.Cancel();else FinishCapture("已取消改键。");}}
        RectTransform Box(string name,Transform parent,float x,float y,float w,float h,Color color)
        {
            var obj=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));var rect=obj.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);obj.GetComponent<UnityEngine.UI.Image>().color=color;return rect;
        }
        UnityEngine.UI.Text Label(string text,Transform parent,float x,float y,float w,float h,int size,Color color)
        {
            var obj=new GameObject(text,typeof(RectTransform));var rect=obj.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);
            var label=rect.gameObject.AddComponent<UnityEngine.UI.Text>();label.font=font;label.fontSize=size;label.color=color;label.text=text;label.raycastTarget=false;label.alignment=TextAnchor.MiddleLeft;label.horizontalOverflow=HorizontalWrapMode.Wrap;return label;
        }
        UnityEngine.UI.Button Button(string name,Transform parent,float x,float y,float w,float h,string text,UnityEngine.Events.UnityAction click)
        {
            var rect=Box(name,parent,x,y,w,h,new Color(.15f,.14f,.12f,.07f));var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=rect.GetComponent<UnityEngine.UI.Image>();
            rect.gameObject.AddComponent<TrialSettingsFocus>();
            var caption=Label(text,rect,10,0,w-20,h,h<40?16:20,Ink);
            if(h<40){caption.resizeTextForBestFit=true;caption.resizeTextMinSize=10;caption.resizeTextMaxSize=16;}
            button.onClick.AddListener(click);navigation.Add(button);return button;
        }
    }
}
