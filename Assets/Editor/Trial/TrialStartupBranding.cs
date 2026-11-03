using System;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialStartupBranding
    {
        public const string LogoPath="Assets/Art/Startup/AuthorSplashLogo.png";

        [MenuItem("Tools/Tomorin/Configure author splash screen")]
        public static void Apply()
        {
            AssetDatabase.ImportAsset(LogoPath,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(LogoPath);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;
            var spriteSettings=new TextureImporterSettings();importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(spriteSettings);
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
            importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.maxTextureSize=2048;
            importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if(!sprite)throw new InvalidOperationException("Author splash logo did not import as a sprite.");
            PlayerSettings.SplashScreen.show=true;
            PlayerSettings.SplashScreen.showUnityLogo=true;
            PlayerSettings.SplashScreen.drawMode=PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
            PlayerSettings.SplashScreen.unityLogoStyle=PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
            PlayerSettings.SplashScreen.animationMode=PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.backgroundColor=new Color(35/255f,31/255f,32/255f,1);
            PlayerSettings.SplashScreen.background=null;
            PlayerSettings.SplashScreen.backgroundPortrait=null;
            PlayerSettings.SplashScreen.logos=new[]{PlayerSettings.SplashScreenLogo.Create(3f,sprite)};
            AssetDatabase.SaveAssets();
            Debug.Log("[Startup] B站作者:千早九重天 — avatar at left, Unity logo below, 3 seconds.");
        }
    }
}
