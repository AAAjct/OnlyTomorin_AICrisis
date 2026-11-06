using System.IO;
using UnityEditor;

namespace TomorinTrial.Editor
{
    // Keep the existing menu and automation entry point on the current route suite.
    public static class TrialSwordComboValidation
    {
        public static string Status=>TrialComboFlowValidation.Status;
        [MenuItem("Tools/Tomorin/Verify ordered sword combo")]
        public static void Run()
        {
            TrialComboFlowValidation.Run();
            if(File.Exists("Logs/Trial/ComboFlowValidation.txt"))
                File.Copy("Logs/Trial/ComboFlowValidation.txt","Logs/Trial/SwordComboValidation.txt",true);
        }
    }
}
