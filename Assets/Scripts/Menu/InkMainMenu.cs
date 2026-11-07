using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace TomorinTrial
{
    public sealed partial class InkMainMenu : MonoBehaviour
    {
        [SerializeField] Texture2D artwork;
        [SerializeField] Font inkFont;
        [SerializeField] Font uiFont;
        [SerializeField] Material inkMaterial;
        [SerializeField] RectTransform root, home, network;
        [SerializeField] UnityEngine.UI.RawImage backdrop;
        [SerializeField] UnityEngine.UI.InputField address, port, nickname;
        [SerializeField] UnityEngine.UI.Text status, members;
        [SerializeField] UnityEngine.UI.Button solo, multiplayer, options, quit, host, join, begin, leave, back;
        TrialSettingsPanel settingsView;
        RectTransform settings;
        [SerializeField] CanvasGroup fade;
        [SerializeField] GameObject quitPanel;
        [SerializeField] UnityEngine.UI.Button confirmQuit, cancelQuit;
        Material liveMaterial;
        TrialNetworkManager manager;
        UnityEngine.UI.Button configureTunnel;
        UnityEngine.UI.Text levelCaption;
        RectTransform networkLevels;
        bool loading;
        bool endpointSplitPending;
        public static bool AutoStartTrial;
        public static bool ReducedMotion { get; private set; }
        public static void SetReducedMotion(bool value){ReducedMotion=value;PlayerPrefs.SetInt("Ink.ReducedMotion",value?1:0);}
        public static bool NavigationActive { get; private set; }
        static readonly Color Ink=new Color(.065f,.08f,.08f),Red=new Color(.61f,.18f,.13f),Grey=new Color(.25f,.28f,.27f),Paper=new Color(.98f,.965f,.925f);

        void Start()
        {
            Time.timeScale=1;Cursor.visible=true;Cursor.lockState=CursorLockMode.None;
            manager=TrialNetworkManager.Current;
            if(!root)BuildVisuals();
            InstallTitleArtwork();
            loading=false;fade.blocksRaycasts=true;
            InstallSettings();
            InstallWardrobeEntry();
            InstallNetworkLayout();
            TrialModalBackdrop.Bind((RectTransform)quitPanel.transform,(RectTransform)confirmQuit.transform.parent,()=>cancelQuit.onClick.Invoke());
            SetDirectionalNavigation(solo,up:quit,down:multiplayer);
            SetDirectionalNavigation(multiplayer,up:solo,down:options);
            SetDirectionalNavigation(options,up:multiplayer,down:wardrobeButton);
            SetDirectionalNavigation(wardrobeButton,up:options,down:quit);
            SetDirectionalNavigation(quit,up:wardrobeButton,down:solo);
            SetDirectionalNavigation(confirmQuit,left:cancelQuit,right:cancelQuit);
            SetDirectionalNavigation(cancelQuit,left:confirmQuit,right:confirmQuit);
            NavigationActive=false;RefreshButtonStyles();
            StretchNetworkColumn(network,0,.5f);
            StretchNetworkColumn(networkLevels,.5f,1);
            liveMaterial=new Material(inkMaterial);backdrop.material=liveMaterial;
            ReducedMotion=PlayerPrefs.GetInt("Ink.ReducedMotion",0)==1;
            AudioListener.volume=PlayerPrefs.GetFloat("Ink.Volume",.7f);
            address.text=PlayerPrefs.GetString("Ink.Address","127.0.0.1");port.text=PlayerPrefs.GetInt("Ink.Port",7777).ToString();nickname.text=PlayerPrefs.GetString("Ink.Name","旅人");
            address.onValueChanged.AddListener(_=>endpointSplitPending=true);
            SplitPastedEndpoint(address.text);
            foreach(var label in solo.GetComponentsInChildren<UnityEngine.UI.Text>(true))if(label.text=="独行入庭")label.text="单人游玩";
            solo.onClick.AddListener(OpenLevelList);
            multiplayer.onClick.AddListener(()=>Show(network));options.onClick.AddListener(()=>Show(settings));
            wardrobeButton.onClick.AddListener(OpenWardrobe);
            quit.onClick.AddListener(()=>{quitPanel.SetActive(true);cancelQuit.Select();});
            confirmQuit.onClick.AddListener(()=>Application.Quit());cancelQuit.onClick.AddListener(()=>{quitPanel.SetActive(false);quit.Select();});
            host.onClick.AddListener(()=>{if(Configure())manager.Host();});join.onClick.AddListener(()=>{if(Configure())manager.Join();});
            begin.onClick.AddListener(()=>manager.BeginTogether());leave.onClick.AddListener(()=>manager.Leave());
            back.onClick.AddListener(()=>{if(manager.Busy)manager.Leave();Show(home);});
            manager.Changed+=RefreshNetwork;RefreshNetwork();Show(manager.Busy?network:home);
            if(manager.Status.Contains("断开")||manager.Status.Contains("失败"))Show(network);
            fade.alpha=0;StartCoroutine(Reveal());
        }
        void InstallSettings()
        {
            foreach(var label in options.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if(label.text=="声音与动效")label.text="设置";
            settingsView=TrialSettingsPanel.Create(root,uiFont?uiFont:inkFont,()=>Show(home));
            settings=(RectTransform)settingsView.transform;
        }
        public static bool TrySplitEndpoint(string value,out string hostName,out string portNumber)
        {
            hostName=portNumber=null;if(string.IsNullOrWhiteSpace(value))return false;
            value=value.Trim();int separator=value.LastIndexOf(':');
            if(separator<=0 || separator==value.Length-1)return false;
            string candidate=value.Substring(0,separator);
            // Accept hostname/IPv4:port and bracketed IPv6; never split a bare IPv6 address.
            if(candidate.Contains(":" ) && !(candidate.StartsWith("[") && candidate.EndsWith("]")))return false;
            if(!ushort.TryParse(value.Substring(separator+1),out var number)||number==0)return false;
            hostName=candidate.Trim('[',']').Trim();if(hostName.Length==0)return false;
            portNumber=number.ToString();return true;
        }
        void SplitPastedEndpoint(string value)
        {
            endpointSplitPending=false;
            if(TrySplitEndpoint(value,out var hostName,out var portNumber))
            {
                address.SetTextWithoutNotify(hostName);port.SetTextWithoutNotify(portNumber);
                address.caretPosition=hostName.Length;
            }
        }
        void LateUpdate()
        {
            // InputField pastes one character at a time and notifies for each one.
            // Wait until input processing finishes before rewriting either field.
            if(endpointSplitPending)SplitPastedEndpoint(address.text);
        }
        bool Configure(){SplitPastedEndpoint(address.text);return manager.Configure(address.text,port.text,nickname.text);}
        void ConfigureHostTunnel()
        {
            if(manager.Busy)return;
            if(!TrialNetworkManager.TryFindFreeTcpPort(out var freePort))
            {
                status.text="未能分配可用的 TCP 端口，请重试。";
                return;
            }
            address.SetTextWithoutNotify("127.0.0.1");
            port.SetTextWithoutNotify(freePort.ToString());
            endpointSplitPending=false;
            status.text=$"已填入本机地址 · 请将穿透服务的本地 TCP 端口设为 {freePort}";
        }
        IEnumerator Reveal(){while(!loading && fade.alpha<1){fade.alpha+=Time.unscaledDeltaTime*(ReducedMotion?100:1.7f);yield return null;}}
        IEnumerator LoadSolo()
        {
            if(loading)yield break;loading=true;solo.interactable=false;fade.blocksRaycasts=false;AutoStartTrial=true;
            while(fade.alpha>0){fade.alpha-=Time.unscaledDeltaTime*3;yield return null;}
            yield return SceneManager.LoadSceneAsync(TrialChampionCatalog.Scene(soloLevel));
        }
        void Show(RectTransform panel)
        {
            home.gameObject.SetActive(panel==home);network.gameObject.SetActive(panel==network);
            networkLevels.gameObject.SetActive(panel==network);
            if(panel==settings)settingsView.Show();else settingsView.Hide();
            if(wardrobePanel)
            {
                bool open=panel==wardrobePanel;
                wardrobePanel.gameObject.SetActive(open);
                if(wardrobeStage)wardrobeStage.SetActive(open);
            }
            if(panel==home)solo.Select();else if(panel==network)SelectNetworkEntry();
        }
        void RefreshNetwork()
        {
            if(!status||!manager)return;
            status.text=manager.Status;
            bool busy=manager.Busy;address.interactable=port.interactable=nickname.interactable=!busy;
            host.interactable=join.interactable=configureTunnel.interactable=!busy;
            begin.gameObject.SetActive(manager.IsHost);leave.gameObject.SetActive(busy);
            RefreshNetworkLevels(!busy || manager.IsHost);
            levelCaption.text=manager.LevelName+(manager.SelectedLevel==TrialLevel.Arena?"":" · "+TrialDifficultySettings.Label(manager.SelectedDifficulty));
            begin.interactable=manager.SelectedLevel!=TrialLevel.Arena || manager.Members.Length>=2;
            var visibleMembers=new string[System.Math.Min(3,manager.Members.Length)];System.Array.Copy(manager.Members,visibleMembers,visibleMembers.Length);
            members.text=manager.Members.Length==0?"尚无同伴入庭":$"房间人数：{manager.Members.Length}  · "+string.Join(" / ",visibleMembers)+(manager.Members.Length>3?$" 等 {manager.Members.Length} 人":"");
            RefreshNetworkNavigation();
        }
        void InstallNetworkLayout()
        {
            // Apply the same layout to the saved menu and the generated fallback.
            var labels=network.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            foreach(var label in labels)
            {
                if(label.transform.parent!=network)continue;
                if(label.text=="局域网联机")Place(label.rectTransform,38,24,590,64);
            }
            Place(network,0,0,670,770);
            var background=network.GetComponent<UnityEngine.UI.Image>();
            background.color=new Color(.96f,.945f,.90f);
            background.raycastTarget=true;
            if(network.Find("Ink rule") is RectTransform border)border.sizeDelta=new Vector2(3,770);
            PlaceNetworkField(nickname,labels,38,119,590);
            PlaceNetworkField(address,labels,38,204,292);
            PlaceNetworkField(port,labels,350,204,112);

            var tunnelButton=Panel("一键配置",network,482,204,146,48,new Color(.61f,.18f,.13f,.12f));
            var tunnelBackground=tunnelButton.GetComponent<UnityEngine.UI.Image>();
            tunnelBackground.raycastTarget=true;
            configureTunnel=tunnelButton.gameObject.AddComponent<UnityEngine.UI.Button>();
            configureTunnel.targetGraphic=tunnelBackground;
            tunnelButton.gameObject.AddComponent<TrialSettingsFocus>();
            Label(tunnelButton,"一键配置",0,0,146,48,20,Ink,TextAnchor.MiddleCenter);
            configureTunnel.onClick.AddListener(ConfigureHostTunnel);

            var divider=new Color(.13f,.14f,.13f,.18f);
            Line(network,38,276,590,1,divider);
            Label(network,"当前关卡",38,293,590,26,16,Grey);
            levelCaption=Label(network,"",38,326,590,43,23,Ink);

            Place((RectTransform)host.transform,38,395,285,50);
            Place((RectTransform)join.transform,343,395,285,50);
            Line(network,38,465,590,1,divider);
            Place(status.rectTransform,38,488,590,44);
            status.fontSize=18;
            Place(members.rectTransform,38,542,590,54);
            members.fontSize=16;
            Place((RectTransform)begin.transform,38,621,285,50);
            Place((RectTransform)leave.transform,343,621,285,50);
            Line(network,38,688,590,1,divider);
            Place((RectTransform)back.transform,38,705,180,42);
            BuildNetworkLevels();
        }

        static void StretchNetworkColumn(RectTransform panel,float left,float right)
        {
            TrialMenuLayout.StretchContents(panel);
            panel.anchorMin=new Vector2(left,0);
            panel.anchorMax=new Vector2(right,1);
            panel.offsetMin=panel.offsetMax=Vector2.zero;
        }

        void PlaceNetworkField(UnityEngine.UI.InputField field,UnityEngine.UI.Text[] labels,float x,float y,float width)
        {
            var rect=(RectTransform)field.transform;
            field.shouldActivateOnSelect=false;
            field.gameObject.AddComponent<TrialSettingsFocus>();
            field.onEndEdit.AddListener(_=>networkEditEndedFrame=Time.frameCount);
            Place(rect,x,y,width,48);
            Place(field.textComponent.rectTransform,13,0,width-26,48);
            if(rect.Find("Ink rule") is RectTransform rule)rule.sizeDelta=new Vector2(width,1);
            foreach(var label in labels)
                if(label.transform.parent==network && label.text==field.name)
                    Place(label.rectTransform,x,y-28,width,27);
        }

        static void Place(RectTransform rect,float x,float y,float width,float height)
        {
            rect.anchoredPosition=new Vector2(x,-y);
            rect.sizeDelta=new Vector2(width,height);
        }
        void Update()
        {
            if(Mouse.current!=null&&(Mouse.current.delta.ReadValue().sqrMagnitude>.1f||Mouse.current.leftButton.wasPressedThisFrame))NavigationActive=false;
            var keys=Keyboard.current;var pad=Gamepad.current;
            if(keys!=null&&(keys.upArrowKey.wasPressedThisFrame||keys.downArrowKey.wasPressedThisFrame||keys.leftArrowKey.wasPressedThisFrame||keys.rightArrowKey.wasPressedThisFrame||keys.tabKey.wasPressedThisFrame||keys.enterKey.wasPressedThisFrame))NavigationActive=true;
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            var focusedField=selected?selected.GetComponent<UnityEngine.UI.InputField>():null;
            if(keys!=null && !(focusedField && focusedField.isFocused) &&
                (keys.wKey.wasPressedThisFrame||keys.aKey.wasPressedThisFrame||keys.sKey.wasPressedThisFrame||keys.dKey.wasPressedThisFrame))NavigationActive=true;
            if(pad!=null&&(pad.dpad.ReadValue().sqrMagnitude>.1f||pad.leftStick.ReadValue().sqrMagnitude>.25f||
                pad.rightStick.ReadValue().sqrMagnitude>.25f||pad.buttonSouth.wasPressedThisFrame))NavigationActive=true;
            if(UpdateLevelList())return;
            UpdateNetworkNavigation(focusedField,pad,keys);
            if(liveMaterial)
            {
                liveMaterial.SetFloat("_Motion",ReducedMotion?0:1);
                liveMaterial.SetVector("_Pointer",Vector4.zero);
            }
            if(TrialSettingsPanel.InputBlocked)return;
            if(wardrobePanel && wardrobePanel.gameObject.activeSelf &&
                (keys!=null && keys.escapeKey.wasPressedThisFrame || pad!=null && pad.buttonEast.wasPressedThisFrame))
            {CloseWardrobe();return;}
            if(keys!=null && keys.escapeKey.wasPressedThisFrame && focusedField &&
                (focusedField.isFocused || networkEditEndedFrame==Time.frameCount))
            {focusedField.DeactivateInputField();return;}
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {if(quitPanel.activeSelf)quitPanel.SetActive(false);else if(settings.gameObject.activeSelf)Show(home);else if(network.gameObject.activeSelf&&!manager.Busy)Show(home);}
        }
        void OnDestroy()
        {
            if(manager)manager.Changed-=RefreshNetwork;
            if(liveMaterial)Destroy(liveMaterial);
            ReleaseWardrobePreview();
            PlayerPrefs.Save();
        }

        // Called by the Editor builder too: the saved scene contains a fully inspectable Canvas.
        public void InstallTitleArtwork()
        {
            if(!home)return;
            var texture=Resources.Load<Texture2D>("UI/InkTitleCalligraphy");
            if(!texture)
            {
                if(!home.Find("只鹅"))Label(home,"只鹅",-7,166,600,135,112,Ink);
                if(!home.Find("_智械危机"))Label(home,"_智械危机",0,294,650,90,65,Ink);
                return;
            }
            var existing=home.Find("Ink calligraphy title");
            var rect=existing?(RectTransform)existing:Rect("Ink calligraphy title",home,new Vector2(-12,98),new Vector2(620,310));
            var image=rect.GetComponent<UnityEngine.UI.RawImage>();if(!image)image=rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.texture=texture;image.raycastTarget=false;
        }

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
            Line(home,1,413,64,2,Red);Label(home,"一念为人，万象成械。",83,400,490,31,21,Grey);
            solo=Button(home,"单人游玩",0,486,430,61,"");
            multiplayer=Button(home,"局域网联机",0,559,430,61,"");
            options=Button(home,"设置",0,632,430,61,"");
            quit=Button(home,"离开",0,705,430,61,"");
            InstallWardrobeEntry();
            var footer=Rect("Footer",root,new Vector2(48,866),new Vector2(1504,30));
            Label(footer,"只鹅计划  /  INK & CIRCUITS",0,0,650,28,13,Grey);
            Label(footer,"水墨之间 · 同行入庭",1110,0,390,28,14,Grey,TextAnchor.MiddleRight);
            network=Panel("Network room",root,76,77,670,745,new Color(.96f,.945f,.90f,.98f));
            Line(network,0,0,3,745,Red);
            Label(network,"局域网联机",33,76,590,68,48,Ink);
            nickname=Field(network,"你的名字",38,208,590,PlayerPrefs.GetString("Ink.Name","旅人"));nickname.characterLimit=12;
            address=Field(network,"房主 IP / 穿透域名",38,290,300,"127.0.0.1");address.characterLimit=253;
            port=Field(network,"TCP 端口",352,290,120,"7777");port.characterLimit=5;port.contentType=UnityEngine.UI.InputField.ContentType.IntegerNumber;
            host=Button(network,"创建房间",38,383,280,50,"+");join=Button(network,"加入房间",338,383,290,50,"→");
            status=Label(network,"尚未连接",38,451,590,45,19,Red);
            members=Label(network,"尚无同伴入庭",38,502,590,95,19,Ink);
            begin=Button(network,"共同启程",38,607,280,50,"▶");leave=Button(network,"离开房间",338,607,290,50,"×");
            back=Button(network,"返回",38,704,180,32,"←");
            var modal=Panel("Quit confirmation",root,0,0,1600,900,new Color(.04f,.06f,.06f,.4f));Stretch(modal);modal.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;quitPanel=modal.gameObject;
            var popup=Panel("Quit dialog",modal,0,0,674,282,new Color(.96f,.945f,.90f,.99f));popup.anchorMin=popup.anchorMax=popup.pivot=new Vector2(.5f,.5f);popup.anchoredPosition=Vector2.zero;
            Label(popup,"暂别此间？",38,34,590,70,45,Ink);
            confirmQuit=Button(popup,"离开游戏",38,173,280,57,"→");cancelQuit=Button(popup,"再留片刻",344,173,290,57,"←");quitPanel.SetActive(false);
            network.gameObject.SetActive(false);
            InstallTitleArtwork();
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
            if(!string.IsNullOrEmpty(number))Label(r,number,10,0,30,h,12,Grey);
            r.gameObject.AddComponent<InkButtonMotion>().Configure(caption.rectTransform,null,image);return button;
        }
        public void RefreshButtonStyles()
        {foreach(var button in GetComponentsInChildren<InkButtonMotion>(true))button.Restyle(uiFont?uiFont:inkFont);}
        UnityEngine.UI.InputField Field(Transform p,string title,float x,float y,float w,string value)
        {
            Label(p,title,x,y-28,title=="你的名字"?120:w,27,16,Grey);
            var r=Panel(title,p,x,y,w,48,new Color(.13f,.14f,.13f,.045f));var image=r.GetComponent<UnityEngine.UI.Image>();image.raycastTarget=true;
            var input=r.gameObject.AddComponent<UnityEngine.UI.InputField>();var text=Label(r,value,13,0,w-26,48,22,Ink);input.textComponent=text;input.text=value;input.lineType=UnityEngine.UI.InputField.LineType.SingleLine;input.targetGraphic=image;
            Line(r,0,47,w,1,new Color(.13f,.14f,.13f,.24f));return input;
        }
    }
}
