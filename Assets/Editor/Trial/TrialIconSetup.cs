using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialIconSetup
    {
        const string Folder="Assets/Art/UI/Resources/Trial/Icons/";
        static readonly string[] Names={"GameIcon","LightSlash","HeavySlash","Parry","Dodge","SpiritDew","TargetLock","Jump"};

        [MenuItem("Tools/Tomorin/Configure icons")]
        public static void Configure()
        {
            AssetDatabase.Refresh();
            foreach(var name in Names)
            {
                var path=Folder+name+".png";
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(!importer)throw new InvalidOperationException("Icon missing: "+path);
                importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.mipmapEnabled=false;importer.isReadable=false;importer.sRGBTexture=true;
                importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;
                importer.maxTextureSize=name=="GameIcon"?1024:256;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                if(!AssetDatabase.LoadAssetAtPath<Sprite>(path))throw new InvalidOperationException("Sprite import failed: "+path);
            }
            var icon=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"GameIcon.png");
            SetIcon(NamedBuildTarget.Unknown,icon);SetIcon(NamedBuildTarget.Standalone,icon);
            AssetDatabase.SaveAssets();
            Debug.Log("[Tomorin] "+Names.Length+" icons imported; default and Windows application icons configured.");
        }

        static void SetIcon(NamedBuildTarget target,Texture2D icon)
        {
            var sizes=PlayerSettings.GetIconSizes(target,IconKind.Any);
            var icons=new Texture2D[sizes.Length];for(int i=0;i<icons.Length;i++)icons[i]=icon;
            PlayerSettings.SetIcons(target,icons,IconKind.Any);
        }
    }
}
