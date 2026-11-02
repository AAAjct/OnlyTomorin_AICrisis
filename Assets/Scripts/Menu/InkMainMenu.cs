using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace TomorinTrial
{
    public sealed class InkMainMenu : MonoBehaviour
    {
        [SerializeField] Texture2D artwork;
        [SerializeField] Font inkFont;
        [SerializeField] Font uiFont;
        [SerializeField] Material inkMaterial;
        [SerializeField] RectTransform root, home, network, settings;
        [SerializeField] UnityEngine.UI.RawImage backdrop;
        [SerializeField] UnityEngine.UI.InputField address, port, nickname;
        [SerializeField] UnityEngine.UI.Text status, members;
        [SerializeField] UnityEngine.UI.Button solo, multiplayer, options, quit, host, join, begin, leave, back, closeSettings;
        [SerializeField] UnityEngine.UI.Slider volume;
        [SerializeField] UnityEngine.UI.Toggle motion;
        [SerializeField] CanvasGroup fade;
        [SerializeField] GameObject quitPanel;
        [SerializeField] UnityEngine.UI.Button confirmQuit, cancelQuit;
        Material liveMaterial;
        TrialNetworkManager manager;
        bool loading;
        public static bool AutoStartTrial;
        public static bool ReducedMotion { get; private set; }
        public static bool NavigationActive { get; private set; }
        static readonly Color Ink=new Color(.065f,.08f,.08f),Red=new Color(.61f,.18f,.13f),Grey=new Color(.25f,.28f,.27f),Paper=new Color(.98f,.965f,.925f);

        void Start()
        {
            Time.timeScale=1;Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            manager=TrialNetworkManager.Current;
            if(!root)BuildVisuals();
            loading=false;fade.blocksRaycasts=true;
            NavigationActive=false;RefreshButtonStyles();
            liveMaterial=new Material(inkMaterial);backdrop.material=liveMaterial;
            ReducedMotion=PlayerPrefs.GetInt("Ink.ReducedMotion",0)==1;
            motion.isOn=!ReducedMotion;volume.value=PlayerPrefs.GetFloat("Ink.Volume",.7f);AudioListener.volume=volume.value;
            address.text=PlayerPrefs.GetString("Ink.Address","127.0.0.1");port.text=PlayerPrefs.GetInt("Ink.Port",7777).ToString();nickname.text=PlayerPrefs.GetString("Ink.Name","旅人");
            solo.onClick.AddListener(()=>StartCoroutine(LoadSolo()));
            multiplayer.onClick.AddListener(()=>Show(network));options.onClick.AddListener(()=>Show(settings));
            quit.onClick.AddListener(()=>{quitPanel.SetActive(true);cancelQuit.Select();});
            confirmQuit.onClick.AddListener(()=>Application.Quit());cancelQuit.onClick.AddListener(()=>{quitPanel.SetActive(false);quit.Select();});
            host.onClick.AddListener(()=>{if(Configure())manager.Host();});join.onClick.AddListener(()=>{if(Configure())manager.Join();});
            begin.onClick.AddListener(()=>manager.BeginTogether());leave.onClick.AddListener(()=>manager.Leave());
            back.onClick.AddListener(()=>{if(manager.Busy)manager.Leave();Show(home);});closeSettings.onClick.AddListener(()=>Show(home));
            volume.onValueChanged.AddListener(v=>{AudioListener.volume=v;PlayerPrefs.SetFloat("Ink.Volume",v);});
            motion.onValueChanged.AddListener(v=>{ReducedMotion=!v;PlayerPrefs.SetInt("Ink.ReducedMotion",v?0:1);});
            manager.Changed+=RefreshNetwork;RefreshNetwork();Show(home);
            if(manager.Status.Contains("断开")||manager.Status.Contains("失败"))Show(network);
            fade.alpha=0;StartCoroutine(Reveal());
        }
        bool Configure()=>manager.Configure(address.text,port.text,nickname.text);
        IEnumerator Reveal(){while(fade.alpha<1){fade.alpha+=Time.unscaledDeltaTime*(ReducedMotion?100:1.7f);yield return null;}}
        IEnumerator LoadSolo()
        {
            if(loading)yield break;loading=true;solo.interactable=false;fade.blocksRaycasts=false;AutoStartTrial=true;
            while(fade.alpha>0){fade.alpha-=Time.unscaledDeltaTime*3;yield return null;}
            yield return SceneManager.LoadSceneAsync(TrialNetworkManager.GameScene);
        }
        void Show(RectTransform panel)
        {
            home.gameObject.SetActive(panel==home);network.gameObject.SetActive(panel==network);settings.gameObject.SetActive(panel==settings);
            if(panel==home)solo.Select();else if(panel==network)nickname.Select();else closeSettings.Select();
        }
        void RefreshNetwork()
        {
            if(!status||!manager)return;
            status.text=manager.Status;
            bool busy=manager.Busy;address.interactable=port.interactable=nickname.interactable=!busy;
            host.interactable=join.interactable=!busy;
            begin.gameObject.SetActive(manager.IsHost);leave.gameObject.SetActive(busy);
            members.text=manager.Members.Length==0?"尚无同伴入庭":string.Join("\n",System.Array.ConvertAll(manager.Members,n=>"●   "+n));
        }
        void Update()
        {
            if(Mouse.current!=null&&(Mouse.current.delta.ReadValue().sqrMagnitude>.1f||Mouse.current.leftButton.wasPressedThisFrame))NavigationActive=false;
            var keys=Keyboard.current;var pad=Gamepad.current;
            if(keys!=null&&(keys.upArrowKey.wasPressedThisFrame||keys.downArrowKey.wasPressedThisFrame||keys.leftArrowKey.wasPressedThisFrame||keys.rightArrowKey.wasPressedThisFrame||keys.tabKey.wasPressedThisFrame||keys.enterKey.wasPressedThisFrame))NavigationActive=true;
            if(pad!=null&&(pad.dpad.ReadValue().sqrMagnitude>.1f||pad.leftStick.ReadValue().sqrMagnitude>.25f||pad.buttonSouth.wasPressedThisFrame))NavigationActive=true;
            if(liveMaterial)
            {
                liveMaterial.SetFloat("_Motion",ReducedMotion?0:1);
                if(Mouse.current!=null){var p=Mouse.current.position.ReadValue();liveMaterial.SetVector("_Pointer",new Vector4(p.x/Screen.width-.5f,p.y/Screen.height-.5f,0,0));}
            }
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {if(quitPanel.activeSelf)quitPanel.SetActive(false);else if(settings.gameObject.activeSelf)Show(home);else if(network.gameObject.activeSelf&&!manager.Busy)Show(home);}
        }
        void OnDestroy(){if(manager)manager.Changed-=RefreshNetwork;if(liveMaterial)Destroy(liveMaterial);PlayerPrefs.Save();}

        // Called by the Editor builder too: the saved scene contains a fully inspectable Canvas.
        public void BuildVisuals()
        {
            var canvas=new GameObject("Ink Start · Canvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=1;
            root=canvas.GetComponent<RectTransform>();fade=canvas.AddComponent<CanvasGroup>();
            var bg=Rect("Ink landscape",root,Vector2.zero,new Vector2(1600,900));Stretch(bg);bg.pivot=new Vector2(1,.5f);
            backdrop=bg.gameObject.AddComponent<UnityEngine.UI.RawImage>();backdrop.texture=artwork;backdrop.material=inkMaterial;backdrop.raycastTarget=false;
            var fit=bg.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();fit.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;fit.aspectRatio=16f/9;
            var dust=Rect("Ink dust & red petals · 54 particles",root,Vector2.zero,Vector2.zero);Stretch(dust);dust.gameObject.AddComponent<InkAtmosphere>();
            home=Rect("Home",root,new Vector2(110,0),new Vector2(650,900));
            Label(home,"序  /  THE MACHINE AWAKENS",0,91,600,28,17,Grey);
            Label(home,"只鹅",-7,166,600,135,112,Ink);
            Label(home,"_智械危机",0,294,650,90,65,Ink);
            var seal=Panel("Cinnabar seal",home,495,203,48,83,Red);Label(seal,"只\n鹅",0,8,48,68,25,Paper,TextAnchor.MiddleCenter);
            Line(home,1,413,64,2,Red);Label(home,"一念为人，万象成械。",83,400,490,31,21,Grey);
            solo=Button(home,"独行入庭",0,486,430,61,"01");
            multiplayer=Button(home,"局域网联机",0,559,430,61,"02");
            options=Button(home,"声音与动效",0,632,430,61,"03");
            quit=Button(home,"离开",0,705,430,61,"04");
            Label(home,"ENTER  确认    /    ↑ ↓  选择",0,826,500,27,17,Grey);
            var footer=Rect("Footer",root,new Vector2(48,866),new Vector2(1504,30));
            Label(footer,"只鹅计划  /  INK & CIRCUITS",0,0,650,28,13,Grey);
            Label(footer,"水墨之间 · 同行入庭",1110,0,390,28,14,Grey,TextAnchor.MiddleRight);
            network=Panel("Network room",root,76,77,670,745,new Color(.96f,.945f,.90f,.98f));
            Line(network,0,0,3,745,Red);Label(network,"联 机  /  CO-OP",36,28,590,25,15,Red);
            Label(network,"与同伴入画",33,76,590,68,48,Ink);
            Label(network,"1–4 人协作 · 局域网 / TCP 端口映射",38,150,590,28,18,Grey);
            nickname=Field(network,"你的名字",38,208,590,PlayerPrefs.GetString("Ink.Name","旅人"));nickname.characterLimit=12;
            address=Field(network,"房主 IP / 穿透域名",38,290,409,"127.0.0.1");address.characterLimit=253;
            port=Field(network,"TCP 端口",465,290,163,"7777");port.characterLimit=5;port.contentType=UnityEngine.UI.InputField.ContentType.IntegerNumber;
            host=Button(network,"创建房间",38,383,280,50,"+");join=Button(network,"加入房间",338,383,290,50,"→");
            status=Label(network,"尚未连接",38,451,590,45,19,Red);
            members=Label(network,"尚无同伴入庭",38,502,590,95,19,Ink);
            begin=Button(network,"共同启程",38,607,280,50,"▶");leave=Button(network,"离开房间",338,607,290,50,"×");
            Label(network,"房主本机："+TrialNetworkManager.LocalAddresses(),38,666,590,38,13,Grey);
            back=Button(network,"返回",38,704,180,32,"←");
            var hint=Panel("Port mapping guide",root,820,643,653,157,new Color(.95f,.935f,.88f,.92f));
            hint.SetParent(network,false);hint.anchoredPosition=new Vector2(739,-566);
            Label(hint,"跨网相逢",23,15,590,28,22,Ink);
            Label(hint,"房主：将穿透服务的 TCP 本地端口映射到此处端口。\n同伴：填写服务给出的域名 / IP 和外部端口。\n同一局域网：填写房主 IPv4 与房主端口。",23,54,606,82,16,Grey);
            settings=Panel("Settings",root,76,117,670,594,new Color(.96f,.945f,.90f,.98f));
            Line(settings,0,0,3,594,Red);Label(settings,"声 画  /  PREFERENCES",38,30,590,28,15,Red);
            Label(settings,"静听墨落",34,90,590,70,52,Ink);
            Label(settings,"主音量",38,222,400,40,25,Ink);
            var track=Panel("Master volume",settings,38,284,580,30,Color.clear);track.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            Line(track,0,12,580,6,new Color(.13f,.14f,.13f,.16f));
            volume=track.gameObject.AddComponent<UnityEngine.UI.Slider>();volume.minValue=0;volume.maxValue=1;
            var handle=Panel("Handle",track,0,15,22,22,Red);handle.pivot=new Vector2(.5f,.5f);volume.handleRect=handle;volume.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();
            var toggle=Panel("Motion enabled",settings,38,371,31,31,new Color(.15f,.14f,.12f,.1f));
            motion=toggle.gameObject.AddComponent<UnityEngine.UI.Toggle>();var check=Panel("Check",toggle,7,7,17,17,Red);motion.graphic=check.GetComponent<UnityEngine.UI.Image>();motion.targetGraphic=toggle.GetComponent<UnityEngine.UI.Image>();motion.isOn=true;
            toggle.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            Label(settings,"启用墨雾、粒子与按钮动效",90,367,530,45,23,Ink);
            Label(settings,"关闭后保留静态水墨画面。",38,433,590,36,17,Grey);
            closeSettings=Button(settings,"返回",38,515,590,49,"←");
            var modal=Panel("Quit confirmation",root,0,0,1600,900,new Color(.04f,.06f,.06f,.4f));Stretch(modal);modal.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;quitPanel=modal.gameObject;
            var popup=Panel("Quit dialog",modal,0,0,674,282,new Color(.96f,.945f,.90f,.99f));popup.anchorMin=popup.anchorMax=popup.pivot=new Vector2(.5f,.5f);popup.anchoredPosition=Vector2.zero;
            Label(popup,"暂别此间？",38,34,590,70,45,Ink);
            confirmQuit=Button(popup,"离开游戏",38,173,280,57,"→");cancelQuit=Button(popup,"再留片刻",344,173,290,57,"←");quitPanel.SetActive(false);
            network.gameObject.SetActive(false);settings.gameObject.SetActive(false);
            if(!FindFirstObjectByType<EventSystem>())new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        }
        RectTransform Rect(string name,Transform parent,Vector2 point,Vector2 size)
        {
            var obj=new GameObject(name,typeof(RectTransform));var r=obj.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(point.x,-point.y);r.sizeDelta=size;return r;
        }
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        RectTransform Panel(string name,Transform parent,float x,float y,float w,float h,Color tint)
        {var r=Rect(name,parent,new Vector2(x,y),new Vector2(w,h));var image=r.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=tint;image.raycastTarget=false;return r;}
        void Line(Transform p,float x,float y,float w,float h,Color tint)=>Panel("Ink rule",p,x,y,w,h,tint);
        UnityEngine.UI.Text Label(Transform p,string text,float x,float y,float w,float h,int size,Color tint,TextAnchor align=TextAnchor.MiddleLeft)
        {
            var r=Rect(text,p,new Vector2(x,y),new Vector2(w,h));var label=r.gameObject.AddComponent<UnityEngine.UI.Text>();label.font=size>=45?inkFont:(uiFont?uiFont:inkFont);label.fontSize=size;label.text=text;label.color=tint;label.alignment=align;label.raycastTarget=false;label.supportRichText=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;return label;
        }
        UnityEngine.UI.Button Button(Transform p,string text,float x,float y,float w,float h,string number)
        {
            var r=Panel(text,p,x,y,w,h,Color.clear);var image=r.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=true;
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.transition=UnityEngine.UI.Selectable.Transition.ColorTint;
            var colors=button.colors;colors.disabledColor=new Color(.6f,.6f,.6f,.4f);button.colors=colors;
            var caption=Label(r,text,44,0,w-60,h,24,Ink);
            Label(r,number,10,0,30,h,12,Grey);
            r.gameObject.AddComponent<InkButtonMotion>().Configure(caption.rectTransform,null,image);return button;
        }
        public void RefreshButtonStyles()
        {foreach(var button in GetComponentsInChildren<InkButtonMotion>(true))button.Restyle(uiFont?uiFont:inkFont);}
        UnityEngine.UI.InputField Field(Transform p,string title,float x,float y,float w,string value)
        {
            Label(p,title,x,y-28,w,27,16,Grey);
            var r=Panel(title,p,x,y,w,48,new Color(.13f,.14f,.13f,.045f));var image=r.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=true;
            var input=r.gameObject.AddComponent<UnityEngine.UI.InputField>();var text=Label(r,value,13,0,w-26,48,22,Ink);input.textComponent=text;input.text=value;input.lineType=UnityEngine.UI.InputField.LineType.SingleLine;input.targetGraphic=image;
            Line(r,0,47,w,1,new Color(.13f,.14f,.13f,.24f));return input;
        }
    }
}
