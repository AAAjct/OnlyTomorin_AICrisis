#if UNITY_ANDROID
using System.IO;
using System.Xml.Linq;
using UnityEditor.Android;

namespace TomorinTrial.Editor
{
    public sealed class TrialAndroidIconPostprocessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string resources = Path.GetFullPath(Path.Combine(path, "..", "launcher", "src", "main", "res"));
            string adaptiveIcon = Path.Combine(resources, "mipmap-anydpi-v26", "app_icon.xml");
            string mdpi = Path.Combine(resources, "mipmap-mdpi");
            string foregroundImage = Path.Combine(mdpi, "ic_launcher_foreground.png");
            if (!File.Exists(adaptiveIcon) || !File.Exists(foregroundImage))
                return;

            // Package installers can show the bitmap fallback instead of the adaptive drawable.
            File.Copy(foregroundImage, Path.Combine(mdpi, "app_icon.png"), overwrite: true);

            XNamespace android = "http://schemas.android.com/apk/res/android";
            var icon = XDocument.Load(adaptiveIcon);
            var foreground = icon.Root?.Element("foreground");
            var drawable = foreground?.Attribute(android + "drawable");
            if (drawable == null)
                return;

            // Fit the full-bleed artwork closer to the launcher's masked viewport.
            foreground.Add(new XElement("inset",
                new XAttribute(android + "drawable", drawable.Value),
                new XAttribute(android + "inset", "15%")));
            drawable.Remove();
            icon.Save(adaptiveIcon);
        }
    }
}
#endif
