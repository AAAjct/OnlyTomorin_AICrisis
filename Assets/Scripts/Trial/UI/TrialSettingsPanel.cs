using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TomorinTrial
{
    // Both menus create this same view: one layout, one rebinding and preference flow.
    public sealed class TrialSettingsPanel : MonoBehaviour
    {
        Font font;
        RectTransform card;
        Action back;
        UnityEngine.UI.Slider volume;
        Slider editingVolume;
        Navigation volumeNavigation;
        readonly UnityEngine.UI.Slider[] soundVolumes=new UnityEngine.UI.Slider[4];
        UnityEngine.UI.Text sprintLabel,motionLabel,status,frameLabel,syncLabel,frameHint,graphicsLabel;
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
            var obj=new GameObject("Shared settings",typeof(RectTransform),typeof(TrialSettingsPanel));
            var rect=obj.GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var panel=obj.GetComponent<TrialSettingsPanel>();panel.font=font;panel.back=back;panel.Build();obj.SetActive(false);return panel;
        }
        void Build()
        {
            card=Box("Settings card",transform,0,0,1120,800,Paper);
            Box("Cinnabar rule",card,0,0,3,800,Red);
            Label("设置",card,36,26,1040,52,34,Ink);
            graphicsLabel=Button("Graphics preset",card,36,103,300,42,"",()=>{TrialGraphicsBudget.Cycle();Refresh();}).GetComponentInChildren<UnityEngine.UI.Text>();
            Label("主音量",card,36,170,300,32,20,Ink);
            var track=Box("Master volume",card,36,222,300,28,new Color(.15f,.14f,.12f,.12f));
            volume=track.gameObject.AddComponent<UnityEngine.UI.Slider>();volume.minValue=0;volume.maxValue=1;
            var handle=Box("Handle",track,0,0,20,28,Red);handle.pivot=new Vector2(.5f,.5f);volume.handleRect=handle;handle.sizeDelta=new Vector2(20,0);volume.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();
            track.gameObject.AddComponent<TrialSettingsFocus>();
            volume.onValueChanged.AddListener(v=>{AudioListener.volume=v;PlayerPrefs.SetFloat("Ink.Volume",v);});navigation.Add(volume);
            string[] soundNames={"音效音量","攻击与命中","格挡与弹刀","Boss 技能与预警"};
            for(int i=0;i<soundVolumes.Length;i++){int category=i;soundVolumes[i]=SoundSlider(soundNames[i],260+i*64,TrialPreferences.AudioLevel(i),v=>TrialPreferences.SetAudioLevel(category,v));}
            first=Button("Sprint mode",card,36,534,300,42,"",()=>{TrialPreferences.ToggleSprint=!TrialPreferences.ToggleSprint;Refresh();});sprintLabel=first.GetComponentInChildren<UnityEngine.UI.Text>();

            motionLabel=Button("Motion",card,36,590,300,42,"",()=>{InkMainMenu.SetReducedMotion(!InkMainMenu.ReducedMotion);Refresh();}).GetComponentInChildren<UnityEngine.UI.Text>();

            frameLabel=Button("Frame rate limit",card,36,646,300,42,"",()=>{TrialPreferences.CycleMobileFrameRate();Refresh();}).GetComponentInChildren<UnityEngine.UI.Text>();
            syncLabel=Button("VSync",card,36,702,300,42,"",()=>{TrialPreferences.VSyncEnabled=!TrialPreferences.VSyncEnabled;Refresh();}).GetComponentInChildren<UnityEngine.UI.Text>();
            frameHint=Label("",card,36,750,310,34,14,Grey);
            Label("按键设置",card,380,103,135,32,23,Ink);
            Label("操作",card,380,145,138,25,16,Grey);
            Label("键鼠主键",card,538,145,124,25,16,Grey);
            Label("键鼠备用键",card,674,145,124,25,16,Grey);
            Label("手柄操作",card,810,145,262,25,16,Grey);
            int row=0;
            foreach(var entry in TrialBindings.Entries)
            {
                float y=182+row++*32;Label(entry.Label,card,380,y,138,30,18,Ink);
                for(int slot=0;slot<3;slot++)
                {
                    int captured=slot;float x=528+slot*136;float width=slot==2?272:124;
                    if(entry.Defaults[slot]==null)
                    {
                        Label(BindingCaption(entry,slot),card,x+10,y,width-20,30,16,Grey);
                        continue;
                    }
                    UnityEngine.UI.Button button=null;button=Button("Bind "+entry.Id+" "+slot,card,x,y,width,30,"",()=>BeginRebind(entry,captured,button));
                    var label=button.GetComponentInChildren<UnityEngine.UI.Text>();refreshBindings.Add(()=>label.text=BindingCaption(entry,captured));
                }
            }
            status=Label("",card,380,666,692,42,16,Red);
            Button("Restore bindings",card,380,718,300,48,"恢复默认键位",()=>{TrialBindings.ResetDefaults();Refresh();status.text="已恢复默认键位。";});
            Button("Back",card,772,718,300,48,"返回",()=>back());
            foreach(var control in navigation)
            {
                control.navigation=new Navigation
                {
                    mode=Navigation.Mode.Explicit,
                    selectOnUp=FindNeighbor(control,Vector2.up),
                    selectOnDown=FindNeighbor(control,Vector2.down),
                    selectOnLeft=FindNeighbor(control,Vector2.left),
                    selectOnRight=FindNeighbor(control,Vector2.right)
                };
                if(control is Slider slider)BindVolumeEditing(slider);
            }
            TrialMenuLayout.StretchContents(card);
            card.anchorMin=Vector2.zero;card.anchorMax=Vector2.one;
            card.offsetMin=card.offsetMax=Vector2.zero;
        }

        Selectable FindNeighbor(Selectable control,Vector2 direction)
        {
            // All controls share the card's top-left anchor and pivot.
            var origin=(RectTransform)control.transform;
            Vector2 center=origin.anchoredPosition+origin.rect.center;
            bool vertical=direction.x==0;
            Selectable nearest=null;
            Selectable wrap=null;
            Selectable diagonal=null;
            float best=float.PositiveInfinity;
            float wrapScore=float.PositiveInfinity;
            float diagonalScore=float.PositiveInfinity;
            foreach(var candidate in navigation)
            {
                var target=(RectTransform)candidate.transform;
                Vector2 delta=target.anchoredPosition+target.rect.center-center;
                float forward=Vector2.Dot(delta,direction);
                float offset=Mathf.Abs(vertical?delta.x:delta.y);
                float halfWidths=(origin.rect.width+target.rect.width)*.5f;
                if(vertical ? offset>=halfWidths || Mathf.Abs(forward)<=1 : Mathf.Abs(forward)<halfWidths)continue;
                bool aligned=vertical || offset<(origin.rect.height+target.rect.height)*.5f;
                float score=forward+2*offset;
                if(forward>0 && aligned && score<best){nearest=candidate;best=score;}
                float edgeScore=forward+.001f*offset;
                if(forward<0 && aligned && edgeScore<wrapScore){wrap=candidate;wrapScore=edgeScore;}
                if(forward>0 && !aligned && score<diagonalScore){diagonal=candidate;diagonalScore=score;}
            }
            return nearest?nearest:wrap?wrap:diagonal;
        }

        void BindVolumeEditing(Slider slider)
        {
            // A slider changes value when a horizontal move has no navigation target.
            var links=slider.navigation;
            if(!links.selectOnLeft)links.selectOnLeft=slider;
            if(!links.selectOnRight)links.selectOnRight=slider;
            slider.navigation=links;
            var trigger=slider.gameObject.AddComponent<EventTrigger>();
            var submit=new EventTrigger.Entry{eventID=EventTriggerType.Submit};
            submit.callback.AddListener(_=>
            {
                if(editingVolume==slider){EndVolumeEditing();return;}
                editingVolume=slider;
                volumeNavigation=slider.navigation;
                slider.navigation=new Navigation{mode=Navigation.Mode.Explicit};
            });
            var deselect=new EventTrigger.Entry{eventID=EventTriggerType.Deselect};
            deselect.callback.AddListener(_=>EndVolumeEditing());
            trigger.triggers.Add(submit);
            trigger.triggers.Add(deselect);
        }

        void EndVolumeEditing()
        {
            if(!editingVolume)return;
            editingVolume.navigation=volumeNavigation;
            editingVolume=null;
        }
        static string BindingCaption(TrialBindings.Entry entry,int slot)
        {
            var path=TrialBindings.Path(entry,slot);
            if(path==null && slot==2)
            {
                return entry.Id switch
                {
                    "Up" => "左摇杆向上",
                    "Down" => "左摇杆向下",
                    "Left" => "左摇杆向左",
                    "Right" => "左摇杆向右",
                    _ => "—"
                };
            }
            return path switch
            {
                "<Keyboard>/upArrow" => "方向键 ↑",
                "<Keyboard>/downArrow" => "方向键 ↓",
                "<Keyboard>/leftArrow" => "方向键 ←",
                "<Keyboard>/rightArrow" => "方向键 →",
                "<Gamepad>/buttonWest" => "左侧面键 · X / □",
                "<Gamepad>/buttonEast" => "右侧面键 · B / ○",
                "<Gamepad>/buttonSouth" => "下方键 · A / ×",
                "<Gamepad>/buttonNorth" => "上方键 · Y / △",
                "<Gamepad>/leftShoulder" => "左肩键 · LB / L1",
                "<Gamepad>/rightShoulder" => "右肩键 · RB / R1",
                "<Gamepad>/leftTrigger" => "左扳机 · LT / L2",
                "<Gamepad>/rightTrigger" => "右扳机 · RT / R2",
                "<Gamepad>/leftStickPress" => "按下左摇杆 · L3",
                "<Gamepad>/rightStickPress" => "按下右摇杆 · R3",
                "<Gamepad>/dpad/up" => "十字键 ↑",
                "<Gamepad>/dpad/down" => "十字键 ↓",
                "<Gamepad>/dpad/left" => "十字键 ←",
                "<Gamepad>/dpad/right" => "十字键 →",
                "<Gamepad>/start" => "菜单键 · Menu / Options",
                _ => TrialBindings.Display(entry,slot)
            };
        }
        UnityEngine.UI.Slider SoundSlider(string caption,float y,float value,UnityEngine.Events.UnityAction<float> change)
        {
            Label(caption,card,36,y,300,25,18,Ink);
            var track=Box(caption,card,36,y+29,300,20,new Color(.15f,.14f,.12f,.12f));
            var slider=track.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.minValue=0;slider.maxValue=1;
            var handle=Box("Handle",track,0,0,20,20,Red);handle.pivot=new Vector2(.5f,.5f);slider.handleRect=handle;handle.sizeDelta=new Vector2(20,0);slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();
            track.gameObject.AddComponent<TrialSettingsFocus>();slider.SetValueWithoutNotify(value);slider.onValueChanged.AddListener(change);navigation.Add(slider);return slider;
        }
        public void Show()
        {gameObject.SetActive(true);transform.SetAsLastSibling();active=this;openedFrame=Time.frameCount;volume.SetValueWithoutNotify(AudioListener.volume);for(int i=0;i<soundVolumes.Length;i++)soundVolumes[i].SetValueWithoutNotify(TrialPreferences.AudioLevel(i));Refresh();status.text="";first.Select();}
        public void Hide(){gameObject.SetActive(false);}
        void Refresh()
        {
            graphicsLabel.text=TrialGraphicsBudget.Caption;sprintLabel.text=TrialPreferences.SprintCaption;motionLabel.text="画面动效："+(InkMainMenu.ReducedMotion?"关闭":"开启");frameLabel.text=TrialPreferences.MobileFrameCaption;
            syncLabel.text="垂直同步："+(TrialPreferences.VSyncEnabled?"开启":"关闭");
            frameHint.text=Application.isMobilePlatform?"移动设备按帧率限制运行，同步由系统管理。":TrialPreferences.VSyncEnabled?"同步开启时跟随屏幕刷新率，优先于帧率限制。":"";
            foreach(var update in refreshBindings)update();
        }
        public void BeginRebind(TrialBindings.Entry entry,int slot,UnityEngine.UI.Button button)
        {
            if(rebinding)return;pendingEntry=entry;pendingSlot=slot;captureButton=button;rebinding=true;accepted=false;armFrame=Time.frameCount+1;captureDeadline=Time.unscaledTime+12;
            foreach(var item in navigation)item.interactable=false;
            string input=slot==2?"手柄按键":"键盘键或鼠标按钮";
            status.text=$"「{entry.Label}」请在 12 秒内按下新的{input}。按 Esc 或手柄菜单键取消。";
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
            if((keys!=null && (keys.wKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame)) ||
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
            if(editingVolume)
            {
                if(keys?.escapeKey.wasPressedThisFrame==true || pad?.buttonEast.wasPressedThisFrame==true ||
                    pad?.startButton.wasPressedThisFrame==true)EndVolumeEditing();
                return;
            }
            if((Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)||(Gamepad.current!=null && Gamepad.current.startButton.wasPressedThisFrame))back();
        }
        void OnDisable(){EndVolumeEditing();if(operation!=null)operation.Cancel();else if(rebinding)FinishCapture(null);if(active==this)active=null;suppressFrame=Time.frameCount+1;PlayerPrefs.Save();}
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
