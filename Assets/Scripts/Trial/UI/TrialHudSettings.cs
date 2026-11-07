using UnityEngine;

namespace TomorinTrial
{
    public sealed partial class TrialHud
    {
        TrialSettingsPanel settingsView;
        public bool SettingsOpen => settingsView && settingsView.IsOpen;
        void InitializeSettings()
        {
            settingsButton=Button("Settings",menu,new Vector2(66,-707),new Vector2(420,47),"设置",OpenSettings);
            settingsButton.gameObject.SetActive(false);
            settingsView=TrialSettingsPanel.Create(root,font,CloseSettings);
        }
        static void Navigate(UnityEngine.UI.Selectable control,UnityEngine.UI.Selectable up,UnityEngine.UI.Selectable down)
        {control.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit,selectOnUp=up,selectOnDown=down};}
        void ConfigurePauseNavigation()
        {
            bool paused=game.Mode==TrialMode.Paused;
            Navigate(mainButton,null,paused?settingsButton:secondaryButton);
            Navigate(settingsButton,mainButton,secondaryButton);Navigate(secondaryButton,paused?settingsButton:mainButton,null);
        }
        public void OpenSettings()
        {
            if(game.Mode!=TrialMode.Paused)return;
            game.Input.ResetSprint();menu.gameObject.SetActive(false);pauseBackdrop.gameObject.SetActive(false);settingsView.Show();
        }
        public void CloseSettings()
        {
            if(!SettingsOpen)return;settingsView.Hide();
            if(game.Mode==TrialMode.Paused){RefreshMenu();settingsButton.Select();}
        }
    }
}
