from pathlib import Path
root=Path('Assets')
def edit(path,old,new):
 p=root/path;s=p.read_text(encoding='utf-8-sig');assert old in s,(path,old[:70]);p.write_text(s.replace(old,new),encoding='utf-8')
edit(Path('Scripts/Trial/TrialPlayerRoguelike.cs'),'bool affixDamage;','bool affixDamage,runChoiceOpen;')
edit(Path('Scripts/Trial/TrialPlayerRoguelike.cs'),'game.IsEndless && PendingRunChoices>0;','game.IsEndless && runChoiceOpen && PendingRunChoices>0;')
edit(Path('Scripts/Trial/TrialPlayerRoguelike.cs'),'affixDamage=false;}','affixDamage=runChoiceOpen=false;}')
edit(Path('Scripts/Trial/TrialPlayerRoguelike.cs'),'            bool first=PendingRunChoices==0;\n','')
edit(Path('Scripts/Trial/TrialPlayerRoguelike.cs'),'            if(first && ChoosingRunAffix){RollAffixes();Time.timeScale=0;game.Input.SuppressCombat();}','')
edit(Path('Scripts/Trial/TrialPlayerRoguelike.cs'),'        void RollAffixes()','''        public bool OpenRunUpgrade()
        {
            if(!game.IsEndless || !Alive || game.Mode!=TrialMode.Playing || PendingRunChoices<=0 || runChoiceOpen)return false;
            runChoiceOpen=true;RollAffixes();Time.timeScale=0;game.Input.ResetSprint();game.Input.SuppressCombat();
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
        }
        void RollAffixes()''')
edit(Path('Scripts/Trial/TrialPlayerRoguelike.cs'),'if(PendingRunChoices>0)RollAffixes();','if(PendingRunChoices>0)RollAffixes();else runChoiceOpen=false;')
edit(Path('Scripts/Trial/TrialGame.cs'),'if(Input.Pause){Pause();return;}','if(Input.Pause){Pause();return;}\n            if(Input.Upgrade && Player.OpenRunUpgrade())return;')
edit(Path('Scripts/Trial/TrialBindings.cs'),'new Entry("Pause","暂停","SwordCombat/Pause","<Keyboard>/escape",null,"<Gamepad>/start")','new Entry("Pause","暂停","SwordCombat/Pause","<Keyboard>/escape",null,"<Gamepad>/start"),\n            new Entry("Upgrade","升级","SwordCombat/Upgrade","<Keyboard>/t",null,"<Gamepad>/dpad/down")')
edit(Path('Scripts/Trial/TrialInput.cs'),'public bool Restart { get; private set; }','public bool Restart { get; private set; }\n        public bool Upgrade { get; private set; }')
edit(Path('Scripts/Trial/TrialInput.cs'),'guard,heal,target,pause,confirm;','guard,heal,target,pause,confirm,upgrade;')
edit(Path('Scripts/Trial/TrialInput.cs'),'confirm=Button("Confirm","<Keyboard>/enter","<Gamepad>/buttonSouth");','confirm=Button("Confirm","<Keyboard>/enter","<Gamepad>/buttonSouth");\n            upgrade=Button("Upgrade","<Keyboard>/t","<Gamepad>/dpad/down");')
edit(Path('Scripts/Trial/TrialInput.cs'),'Move=Look=Vector2.zero;Attack=AttackHeld=','Move=Look=Vector2.zero;Upgrade=Attack=AttackHeld=')
edit(Path('Scripts/Trial/TrialInput.cs'),'Jump=jump.WasPressedThisFrame();','Jump=jump.WasPressedThisFrame();Upgrade=upgrade.WasPressedThisFrame();')
edit(Path('Scripts/Trial/TrialInput.cs'),'Attack=AttackHeld=Heavy=HeavyHeld=Dodge=Jump=Guard=Heal=Interact=Lock=false;','Upgrade=Attack=AttackHeld=Heavy=HeavyHeld=Dodge=Jump=Guard=Heal=Interact=Lock=false;')
edit(Path('Scripts/Trial/TrialSettingsPanel.cs'),'182+row++*34','182+row++*32')
edit(Path('Scripts/Trial/TrialHudRoguelike.cs'),'Image experienceBar;','Image experienceBar;\n        Button upgradeButton;')
edit(Path('Scripts/Trial/TrialHudRoguelike.cs'),'if(game.IsEndless)experienceBar=Bar("Experience",root,new Vector2(36,-183),new Vector2(360,7),new Color(.25f,.75f,.85f));','''if(game.IsEndless)
                {
                    var vitals=stats.GetComponent<RectTransform>();vitals.sizeDelta=new Vector2(480,145);
                    experienceBar=Bar("Experience",vitals,new Vector2(96,-86),new Vector2(310,7),new Color(.25f,.75f,.85f));
                    hpText.rectTransform.anchoredPosition=new Vector2(96,-105);
                    upgradeButton=Button("Open upgrade",root,new Vector2(540,180),new Vector2(225,48),"",()=>game.Player.OpenRunUpgrade());
                    var rect=upgradeButton.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=Vector2.zero;
                    if(touchRoot){rect.SetParent(touchRoot,false);rect.anchorMin=rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=new Vector2(-320,640);rect.sizeDelta=new Vector2(164,86);}
                }''')
edit(Path('Scripts/Trial/TrialHudRoguelike.cs'),'            bool choosing=game.Player.ChoosingRunAffix','''            if(upgradeButton)
            {
                bool available=game.Player.PendingRunChoices>0 && game.Mode==TrialMode.Playing && !game.Player.ChoosingRunAffix;
                upgradeButton.gameObject.SetActive(available);
                string label=touchRoot?$"升级 ×{game.Player.PendingRunChoices}":$"升级 ×{game.Player.PendingRunChoices}  [{TrialBindings.Display("Upgrade",game.Input.UsingGamepad?2:0)}]";
                var captionLabel=upgradeButton.GetComponentInChildren<Text>();if(captionLabel.text!=label)captionLabel.text=label;
            }
            bool choosing=game.Player.ChoosingRunAffix''')
edit(Path('Editor/Trial/TrialRunValidation.cs'),'static void ChooseAll(TrialPlayer player){int count=0;','static void ChooseAll(TrialPlayer player){player.OpenRunUpgrade();int count=0;')
edit(Path('Editor/Trial/TrialRunValidation.cs'),'Check(Time.timeScale==0,"Upgrade pauses scaled time");','Check(!player.ChoosingRunAffix && Time.timeScale==1,"Earning levels does not interrupt combat");\n            Check(player.OpenRunUpgrade(),"Player manually opens pending upgrades");\n            Check(Time.timeScale==0,"Upgrade pauses scaled time");')
