using UnityEngine;

namespace TomorinTrial
{
    public static class TrialPreferences
    {
        static bool? toggleSprint;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache(){toggleSprint=null;audioLoaded=false;}
        static bool audioLoaded;
        static readonly float[] audioLevels={1,1,1,1};
        public static float AudioLevel(int category)
        {
            if(!audioLoaded){for(int i=0;i<audioLevels.Length;i++)audioLevels[i]=Mathf.Clamp01(PlayerPrefs.GetFloat("Ink.Sfx."+i,1));audioLoaded=true;}
            return audioLevels[Mathf.Clamp(category,0,3)];
        }
        public static void SetAudioLevel(int category,float value)
        {AudioLevel(0);category=Mathf.Clamp(category,0,3);audioLevels[category]=Mathf.Clamp01(value);PlayerPrefs.SetFloat("Ink.Sfx."+category,audioLevels[category]);}
        public static bool ToggleSprint
        {
            get => toggleSprint ?? (toggleSprint=PlayerPrefs.GetInt("Ink.ToggleSprint",1)!=0).Value;
            set { toggleSprint=value;PlayerPrefs.SetInt("Ink.ToggleSprint",value?1:0);PlayerPrefs.Save(); }
        }
        public static string SprintCaption => "奔跑模式："+(ToggleSprint?"切换":"长按");
        // Keep the saved key so existing installations retain their preference.
        // Zero means unlimited; migrate the retired 90 FPS option to 120 FPS.
        public static int MobileFrameRate
        {
            get
            {
                int saved=PlayerPrefs.GetInt("Ink.MobileFrameRate",60);
                return saved==0 || saved==30 || saved==60 || saved==120 || saved==144?saved:saved==90?120:60;
            }
        }
        public static bool VSyncEnabled
        {
            get=>PlayerPrefs.GetInt("Ink.VSync",0)!=0;
            set{PlayerPrefs.SetInt("Ink.VSync",value?1:0);PlayerPrefs.Save();ApplyMobileFrameRate();}
        }
        public static string MobileFrameCaption => "帧率限制："+(MobileFrameRate==0?"无限制":MobileFrameRate+" FPS");
        public static void CycleMobileFrameRate()
        {
            int current=MobileFrameRate;
            PlayerPrefs.SetInt("Ink.MobileFrameRate",current==30?60:current==60?120:current==120?144:current==144?0:30);
            PlayerPrefs.Save();ApplyMobileFrameRate();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplyMobileFrameRate()
        {
            QualitySettings.vSyncCount=VSyncEnabled?1:0;
            int requested=MobileFrameRate;
            Application.targetFrameRate=(VSyncEnabled && !Application.isMobilePlatform) || requested==0?-1:requested;
        }
    }
}
