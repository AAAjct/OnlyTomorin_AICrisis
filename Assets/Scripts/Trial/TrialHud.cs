using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TomorinTrial
{
    public sealed partial class TrialHud : MonoBehaviour
    {
        readonly Color ivory=new Color(.90f,.87f,.78f),gold=new Color(.76f,.58f,.31f),muted=new Color(.52f,.58f,.59f),black=new Color(.018f,.027f,.032f,.94f);
        TrialGame game;
        Canvas canvas;
        Font font;
        bool ownsFont;
        Sprite barSprite;
        RectTransform root,menu,bossPanel,lockMarker,enemyPanel;
        UnityEngine.UI.Image health,stamina,bossHealth,enemyHealth,posture;
        UnityEngine.UI.Text hpText,flaskText,objectiveText,promptText,noticeText,bossName,menuTitle,menuSubtitle,menuCaption,mainButtonLabel,enemyName;
        UnityEngine.UI.Button mainButton,secondaryButton,settingsButton;
        UnityEngine.UI.Text dangerText;
        UnityEngine.UI.Text playerName;
        UnityEngine.UI.Text bossSkill;
        UnityEngine.UI.Image markerImage;
        Camera view;
        int shownHealth=-1,shownStamina=-1,shownFlasks=-1,shownGuards=-1;
        bool shownGate;
        string shownBossName,shownSkill;
        GameObject titleBrand,footer,stats;
        float noticeUntil;
        RectTransform healthBar;
        Vector2 healthBarHome;
        float previousPlayerHealth,healthShakeUntil;
        const float HealthShakeDuration=.36f;
        string notice="";
        TrialMode lastMode=(TrialMode)(-1);

        public void Initialize(TrialGame owner)
        {
            game=owner;view=owner.CameraRig.View;
            ownsFont=!owner.UiFont;
            font=owner.UiFont?owner.UiFont:Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","SimHei","Arial"},32);
            if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            barSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1);
            var obj=new GameObject("雾庭 · Interface",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));obj.transform.SetParent(transform,false);
            canvas=obj.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var scaler=obj.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=Application.isMobilePlatform?1:.5f;
            root=obj.GetComponent<RectTransform>();
            if(!FindFirstObjectByType<EventSystem>())new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).transform.SetParent(transform,false);

            stats=Panel("Vitals",root,new Vector2(0,1),new Vector2(42,-32),new Vector2(350,111),new Color(0,0,0,.13f)).gameObject;
            var s=stats.GetComponent<RectTransform>();
            playerName=Label("Name",s,"凑企鹅",new Vector2(0,1),new Vector2(18,-12),new Vector2(310,26),16,ivory);
            health=Bar("Vitality",s,new Vector2(18,-48),new Vector2(300,10),new Color(.68f,.16f,.12f));
            healthBar=(RectTransform)health.transform.parent;healthBarHome=healthBar.anchoredPosition;
            previousPlayerHealth=owner.Player.Health;
            stamina=Bar("Resolve",s,new Vector2(18,-69),new Vector2(260,6),new Color(.55f,.68f,.43f));
            hpText=Label("Vitality count",s,"",new Vector2(0,1),new Vector2(18,-83),new Vector2(300,19),12,muted);
            objectiveText=Label("Objective",root,"",new Vector2(1,1),new Vector2(-400,-37),new Vector2(350,45),16,ivory,TextAnchor.UpperRight);

            flaskText=Label("Spirit dew",root,"",new Vector2(0,0),new Vector2(50,99),new Vector2(310,34),23,gold);
            promptText=Label("Interact",root,"",new Vector2(.5f,0),new Vector2(-320,119),new Vector2(640,40),20,ivory,TextAnchor.MiddleCenter);
            noticeText=Label("Announcement",root,"",new Vector2(.5f,.67f),new Vector2(-260,0),new Vector2(520,32),19,gold,TextAnchor.MiddleCenter);

            bossPanel=Panel("Boss",root,new Vector2(.5f,0),new Vector2(-440,65),new Vector2(880,55),Color.clear);
            bossName=Label("Boss name",bossPanel,"大肥鱼 · DeepSeek",new Vector2(.5f,1),new Vector2(-330,0),new Vector2(660,28),20,ivory,TextAnchor.MiddleCenter);
            bossSkill=Label("Boss move",bossPanel,"",new Vector2(.5f,1),new Vector2(-330,38),new Vector2(660,28),18,gold,TextAnchor.MiddleCenter);
            bossHealth=Bar("Boss vitality",bossPanel,new Vector2(0,-36),new Vector2(880,8),new Color(.61f,.12f,.085f));
            posture=Bar("Boss posture",bossPanel,new Vector2(140,-51),new Vector2(600,3),gold);
            lockMarker=Panel("Lock diamond",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(12,12),gold);lockMarker.localRotation=Quaternion.Euler(0,0,45);
            markerImage=lockMarker.GetComponent<UnityEngine.UI.Image>();
            dangerText=Label("Peril warning",root,"危",new Vector2(.5f,.5f),Vector2.zero,new Vector2(110,90),52,new Color(1,.12f,.055f),TextAnchor.MiddleCenter);
            enemyPanel=Panel("Enemy vitality",root,new Vector2(.5f,.5f),Vector2.zero,new Vector2(160,32),Color.clear);
            enemyName=Label("Enemy name",enemyPanel,"蚀影剑卫",new Vector2(0,1),Vector2.zero,new Vector2(160,23),13,ivory,TextAnchor.MiddleCenter);
            enemyHealth=Bar("Enemy health",enemyPanel,new Vector2(0,-26),new Vector2(160,4),new Color(.70f,.23f,.13f));

            menu=Panel("Menu scrim",root,new Vector2(0,1),Vector2.zero,new Vector2(675,900),black);
            menu.anchorMax=new Vector2(0,1);menu.sizeDelta=new Vector2(675,900);
            titleBrand=Label("Brand",menu,"只鹅",new Vector2(0,1),new Vector2(65,-76),new Vector2(550,30),17,gold).gameObject;
            Panel("Gold rule",menu,new Vector2(0,1),new Vector2(66,-135),new Vector2(65,2),gold);
            menuTitle=Label("Title",menu,"残火 · 雾庭",new Vector2(0,1),new Vector2(60,-193),new Vector2(550,110),62,ivory);
            menuSubtitle=Label("Subtitle",menu,"",new Vector2(0,1),new Vector2(66,-324),new Vector2(540,55),23,ivory);
            menuCaption=Label("Description",menu,"击败三名奶蛙，进入庭心。",new Vector2(0,1),new Vector2(66,-380),new Vector2(530,210),18,muted);
            mainButton=Button("Begin",menu,new Vector2(66,-647),new Vector2(420,61),"踏入雾庭   →",()=>PrimaryAction());mainButtonLabel=mainButton.GetComponentInChildren<UnityEngine.UI.Text>();
            secondaryButton=Button("Boss challenge or title",menu,new Vector2(66,-722),new Vector2(420,47),"直接挑战大肥鱼",()=>{if(game.Mode==TrialMode.Title)game.StartBossChallenge();else game.ReturnToTitle();});
            roomButton=Button("Return to room",menu,new Vector2(66,-785),new Vector2(420,47),"返回联机房间",()=>TrialNetworkManager.Current.ReturnToRoom());roomButton.gameObject.SetActive(false);
            footer=Label("Menu footer",menu,"Enter / A",new Vector2(0,1),new Vector2(66,-800),new Vector2(550,30),13,muted).gameObject;
            var uiModule=FindFirstObjectByType<InputSystemUIInputModule>();
            if(uiModule && uiModule.actionsAsset==null)uiModule.AssignDefaultActions();
            var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnDown=secondaryButton};mainButton.navigation=nav;
            nav.selectOnDown=null;nav.selectOnUp=mainButton;secondaryButton.navigation=nav;
            InitializeSettings();InitializeArtwork();InitializeTouchControls();InitializeDamageFeedback();
        }

        UnityEngine.UI.Button roomButton;
        void PrimaryAction()
        {
            switch(game.Mode)
            {case TrialMode.Title:game.StartTrial();break;case TrialMode.Paused:game.Resume();break;case TrialMode.Dead:if(TrialNetworkManager.SessionActive)game.RetryChallenge();else game.Respawn();break;case TrialMode.Victory:game.RetryChallenge();break;}
        }

        void Update()
        {
            if(!game || !game.Ready)return;
            UpdateArtwork();UpdateTouchControls();
            UpdateRunChoices();UpdatePlayerNameplates();
            if(TrialNetworkManager.SessionActive)playerName.text=TrialNetworkManager.Current.NameOf(game.Player);
            health.fillAmount=game.Player.Health/game.Player.MaxHealth;stamina.fillAmount=game.Player.Stamina/game.Player.MaxStamina;
            UpdateDamageFeedback();UpdateHealthShake();
            int hp=Mathf.CeilToInt(game.Player.Health),sp=Mathf.CeilToInt(game.Player.Stamina);
            if(hp!=shownHealth || sp!=shownStamina)
            {shownHealth=hp;shownStamina=sp;hpText.text=$"{hp} / {game.Player.MaxHealth:0}";}
            if(game.Player.Flasks!=shownFlasks){shownFlasks=game.Player.Flasks;flaskText.text=shownFlasks.ToString();}
            if(shownGuards!=game.DefeatedGuards || shownGate!=game.GateOpen)
            {shownGuards=game.DefeatedGuards;shownGate=game.GateOpen;objectiveText.text=game.IsChampionStage?TrialChampionCatalog.LevelName(game.Level):game.GateOpen?"庭门已开":"奶蛙  "+shownGuards+" / 3";}
            if(game.IsArena)objectiveText.text=TrialNetworkManager.Current.ArenaScoreboard();
            promptText.text=game.IsArena?(!game.Player.Alive?"阵亡 · 3 秒后重生":game.Player.SpawnProtected?"重生保护 · 主动攻击后解除":""):game.Mode==TrialMode.Playing && Vector3.Distance(game.Player.transform.position,game.World.ShrinePosition)<2.6f?interactPrompt:"";
            noticeText.text=game.Mode==TrialMode.Playing && Time.unscaledTime<noticeUntil?notice:"";
            var boss=game.Enemies[game.Enemies.Count-1];
            if(game.IsChampionStage)foreach(var e in game.Enemies)if(e.Alive && (!boss.Alive || e.Champion.WindingUp))boss=e;
            bool showBoss=!game.IsArena && boss.Alive && boss.Awakened && game.Mode==TrialMode.Playing;
            bossPanel.gameObject.SetActive(showBoss);
            objectiveText.gameObject.SetActive(game.Mode==TrialMode.Playing&&!showBoss);
            bossHealth.fillAmount=boss.Health/boss.MaxHealth;posture.fillAmount=boss.Posture/boss.PostureLimit;
            UpdateTwinBars();
            string nextName=boss.Champion?ChampionName(boss):(boss.Boss.Frenzied?"大肥鱼 · 深渊狂潮":boss.PhaseTwo?"大肥鱼 · 深海觉醒":"大肥鱼 · DeepSeek")+(game.PartySize>1?$" · {game.PartySize} 人难度":"");
            if(shownBossName!=nextName){shownBossName=nextName;bossName.text=nextName;}
            string nextSkill=boss.Champion?ChampionSkill(boss):boss.Boss && boss.WindingUp?boss.Boss.SkillName+(boss.Boss.CanBeDeflected?" · 可完美弹反":""):"";
            if(shownSkill!=nextSkill){shownSkill=nextSkill;bossSkill.text=nextSkill;}
            bossSkill.color=boss.Unblockable?new Color(1,.29f,.17f):gold;
            dangerText.gameObject.SetActive(showBoss && boss.WindingUp && boss.Unblockable);
            if(dangerText.gameObject.activeSelf)ScreenPosition(dangerText.rectTransform,boss.transform.position+Vector3.up*3.3f);
            TrialFighter target=game.Player.Locked?game.Player.LockTarget:game.NearestOpponent(game.Player,7);
            bool showEnemy=target is TrialEnemy foe && !foe.IsBoss && target.Alive && game.Mode==TrialMode.Playing;
            enemyPanel.gameObject.SetActive(showEnemy);
            lockMarker.gameObject.SetActive(game.Player.Locked && game.Mode==TrialMode.Playing);
            if(showEnemy)
            {ScreenPosition(enemyPanel,target.transform.position+Vector3.up*2.4f);enemyHealth.fillAmount=target.Health/target.MaxHealth;enemyName.text=target is TrialPlayer rival?TrialNetworkManager.Current.NameOf(rival):target.Action==FighterAction.Stagger?"破绽":"";}
            if(game.Player.Locked)
            {
                ScreenPosition(lockMarker,game.Player.LockTarget.transform.position+Vector3.up*1.35f);
                var enemy=game.Player.LockTarget as TrialEnemy;
                markerImage.color=enemy && enemy.WindingUp?(enemy.Unblockable?new Color(1,.12f,.06f):new Color(1,.83f,.37f)):gold;
            }
            if(game.Mode!=lastMode){lastMode=game.Mode;RefreshMenu();}
        }

        void UpdateHealthShake()
        {
            float currentHealth=game.Player.Health;
            bool animate=game.Mode==TrialMode.Playing && !InkMainMenu.ReducedMotion;
            if(animate && currentHealth<previousPlayerHealth)healthShakeUntil=Time.unscaledTime+HealthShakeDuration;
            if(!animate || currentHealth>previousPlayerHealth)healthShakeUntil=0;
            previousPlayerHealth=currentHealth;
            float remaining=Mathf.Clamp01((healthShakeUntil-Time.unscaledTime)/HealthShakeDuration);
            float elapsed=HealthShakeDuration-(healthShakeUntil-Time.unscaledTime);
            // Offset the whole bar from its fixed home so repeated hits never drift.
            healthBar.anchoredPosition=healthBarHome+new Vector2(Mathf.Sin(elapsed*85)*10,Mathf.Sin(elapsed*110)*4)*remaining;
        }

        void ScreenPosition(RectTransform ui,Vector3 world)
        {
            Vector3 point=view.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,point,null,out var local);
            ui.anchoredPosition=local;ui.localScale=point.z>0?Vector3.one:Vector3.zero;
        }

        void RefreshMenu()
        {
            if(game.Mode!=TrialMode.Paused)CloseSettings();
            bool show=game.Mode!=TrialMode.Playing;menu.gameObject.SetActive(show && !SettingsOpen);
            settingsButton.gameObject.SetActive(game.Mode==TrialMode.Paused);
            mainButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(66,game.Mode==TrialMode.Paused?-632:-647);
            secondaryButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(66,game.Mode==TrialMode.Paused?-782:-722);
            menuCaption.fontSize=game.Mode==TrialMode.Paused?16:18;
            ConfigurePauseNavigation();
            stats.SetActive(!show);objectiveText.gameObject.SetActive(!show);
            secondaryButton.gameObject.SetActive(game.Mode==TrialMode.Paused || game.Mode==TrialMode.Title || game.Mode==TrialMode.Dead || game.Mode==TrialMode.Victory);
            secondaryButton.GetComponentInChildren<UnityEngine.UI.Text>().text=game.Mode==TrialMode.Title?(game.IsChampionStage?"直接挑战 Boss   →":"直接挑战大肥鱼   →"):game.Mode==TrialMode.Dead?"退出到主菜单":"返回标题";
            footer.SetActive(game.Mode==TrialMode.Title);
            bool room=TrialNetworkManager.SessionActive && game.Mode==TrialMode.Victory;
            roomButton.gameObject.SetActive(room);
            mainButton.interactable=!(TrialNetworkManager.SessionActive && (game.Mode==TrialMode.Victory || game.Mode==TrialMode.Dead) && !TrialNetworkManager.Current.IsHost);
            if(room){Navigate(secondaryButton,mainButton.interactable?mainButton:roomButton,roomButton);Navigate(roomButton,secondaryButton,mainButton.interactable?mainButton:secondaryButton);}

            if(show && EventSystem.current){EventSystem.current.SetSelectedGameObject(null);EventSystem.current.SetSelectedGameObject(mainButton.interactable?mainButton.gameObject:secondaryButton.gameObject);}
            menuSubtitle.text="";
            if(game.Mode==TrialMode.Title)
            {menuTitle.text=game.IsChampionStage?TrialChampionCatalog.LevelName(game.Level):"雾庭";menuCaption.text=game.IsChampionStage?TrialChampionCatalog.Lesson(game.Level):"击败三名奶蛙，进入庭心。";mainButtonLabel.text="进入";}
            if(game.Mode==TrialMode.Paused)
            {menuTitle.text="暂停";menuCaption.text=TrialBindings.Display("Up")+" / "+TrialBindings.Display("Left")+" / "+TrialBindings.Display("Down")+" / "+TrialBindings.Display("Right")+"  移动    "+TrialBindings.Display("Sprint")+"  "+(TrialPreferences.ToggleSprint?"切换奔跑":"长按奔跑")+"\n"+
                TrialBindings.Display("Attack")+"  轻斩    "+TrialBindings.Display("Heavy")+"  重斩\n"+
                TrialBindings.Display("Guard")+"  格挡    "+TrialBindings.Display("Dodge")+"  闪避\n"+
                TrialBindings.Display("Heal")+"  灵露    "+TrialBindings.Display("Lock")+"  锁定    "+TrialBindings.Display("Interact")+"  休息\n\n手柄："+
                TrialBindings.Display("Attack",2)+" 轻斩 · "+TrialBindings.Display("Heavy",2)+" 重斩\n"+
                TrialBindings.Display("Guard",2)+" 格挡 · "+TrialBindings.Display("Dodge",2)+" 闪避\n轻击→重击：升斩 / 突刺 / 终结斩\n两段轻击→闪避：回风斩 · 闪避→轻击：追击";mainButtonLabel.text="继续";}
            if(game.Mode==TrialMode.Dead)
            {menuTitle.text="败北";menuCaption.text="";mainButtonLabel.text="重来";}
            if(game.Mode==TrialMode.Victory)
            {menuTitle.text="通关";menuCaption.text=$"{Mathf.FloorToInt(game.PlaySeconds/60):00}:{Mathf.FloorToInt(game.PlaySeconds%60):00}    倒下 {game.Deaths} 次";mainButtonLabel.text="重新挑战";}
            if(TrialNetworkManager.SessionActive)
            {
                if(!mainButton.interactable)mainButtonLabel.text="等待房主重来";
                if(game.Mode==TrialMode.Paused)menuCaption.text+="\n\n联机时游戏仍在运行。";
            }
        }

        public void Notify(string message,float duration){notice=message;noticeUntil=Time.unscaledTime+duration;}

        RectTransform Panel(string label,RectTransform parent,Vector2 anchor,Vector2 pos,Vector2 size,Color tint)
        {
            var obj=new GameObject(label,typeof(RectTransform),typeof(UnityEngine.UI.Image));var t=obj.GetComponent<RectTransform>();t.SetParent(parent,false);t.anchorMin=t.anchorMax=anchor;t.pivot=new Vector2(0,1);t.anchoredPosition=pos;t.sizeDelta=size;
            var image=obj.GetComponent<UnityEngine.UI.Image>();image.color=tint;image.raycastTarget=false;return t;
        }
        UnityEngine.UI.Text Label(string label,RectTransform parent,string text,Vector2 anchor,Vector2 pos,Vector2 size,int fontSize,Color tint,TextAnchor align=TextAnchor.UpperLeft)
        {
            var obj=new GameObject(label,typeof(RectTransform),typeof(UnityEngine.UI.Text));var t=obj.GetComponent<RectTransform>();t.SetParent(parent,false);t.anchorMin=t.anchorMax=anchor;t.pivot=new Vector2(0,1);t.anchoredPosition=pos;t.sizeDelta=size;
            var value=obj.GetComponent<UnityEngine.UI.Text>();value.font=font;value.text=text;value.fontSize=fontSize;value.color=tint;value.alignment=align;value.raycastTarget=false;value.supportRichText=true;value.horizontalOverflow=HorizontalWrapMode.Wrap;value.verticalOverflow=VerticalWrapMode.Overflow;return value;
        }
        UnityEngine.UI.Image Bar(string label,RectTransform parent,Vector2 pos,Vector2 size,Color tint)
        {
            var background=Panel(label+" background",parent,new Vector2(0,1),pos,size,new Color(.11f,.12f,.12f,.92f));
            var foreground=Panel(label,background,new Vector2(0,1),new Vector2(1,-1),size-new Vector2(2,2),tint).GetComponent<UnityEngine.UI.Image>();
            foreground.type=UnityEngine.UI.Image.Type.Filled;foreground.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;foreground.fillOrigin=0;
            foreground.sprite=barSprite;return foreground;
        }
        UnityEngine.UI.Button Button(string label,RectTransform parent,Vector2 pos,Vector2 size,string caption,UnityEngine.Events.UnityAction click)
        {
            var rect=Panel(label,parent,new Vector2(0,1),pos,size,new Color(.19f,.16f,.115f,.96f));rect.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(3.8f,3.8f,3.8f);colors.selectedColor=colors.highlightedColor;colors.colorMultiplier=1;colors.pressedColor=new Color(.8f,.68f,.42f);button.colors=colors;button.onClick.AddListener(click);
            Panel("Button edge",rect,new Vector2(0,1),Vector2.zero,new Vector2(3,size.y),gold);
            Label("Caption",rect,caption,new Vector2(0,1),new Vector2(23,-14),size-new Vector2(38,10),22,ivory);
            button.transition=UnityEngine.UI.Selectable.Transition.None;rect.gameObject.AddComponent<TrialMenuFocus>();return button;
        }
        void OnDestroy(){DisposeDamageFeedback();if(ownsFont && font)Destroy(font);if(barSprite)Destroy(barSprite);}
    }
}
