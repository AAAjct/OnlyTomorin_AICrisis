using UnityEngine;

namespace TomorinTrial
{
    public static class TrialPreferences
    {
        static bool? toggleSprint;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache(){toggleSprint=null;}
        public static bool ToggleSprint
        {
            get => toggleSprint ?? (toggleSprint=PlayerPrefs.GetInt("Ink.ToggleSprint",1)!=0).Value;
            set { toggleSprint=value;PlayerPrefs.SetInt("Ink.ToggleSprint",value?1:0);PlayerPrefs.Save(); }
        }
        public static string SprintCaption => "奔跑模式："+(ToggleSprint?"切换":"长按");
        // Mobile's default (-1) can select 30 fps. Use an explicit budget and
        // offer the display refresh rate for devices that can sustain it.
        public static int MobileFrameRate => PlayerPrefs.GetInt("Ink.MobileFrameRate",60);
        public static string MobileFrameCaption => "手机帧率："+(MobileFrameRate==0?"跟随屏幕":MobileFrameRate+" FPS");
        public static void CycleMobileFrameRate()
        {
            int current=MobileFrameRate;
            PlayerPrefs.SetInt("Ink.MobileFrameRate",current==60?90:current==90?120:current==120?0:60);
            PlayerPrefs.Save();ApplyMobileFrameRate();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplyMobileFrameRate()
        {
            if(!Application.isMobilePlatform)return;
            QualitySettings.vSyncCount=0;
            int requested=MobileFrameRate;
            Application.targetFrameRate=requested==0?Mathf.Max(60,Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value)):requested;
        }
    }
}
