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
    }
}
