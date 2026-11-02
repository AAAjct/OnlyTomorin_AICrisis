using System.IO;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class InkStartBuilder
    {
        [MenuItem("Tools/Tomorin/Start menu/Build ink start scene")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode before building the start scene.");
            var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(current.isDirty)throw new System.InvalidOperationException("Save the current scene before generating the start scene.");
            AssetDatabase.Refresh();
            const string imagePath="Assets/Art/UI/InkStart/InkPenguinLandscape.png";
            var importer=(TextureImporter)AssetImporter.GetAtPath(imagePath);
            importer.textureType=TextureImporterType.Default;importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));camera.tag="MainCamera";camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=new Color(.94f,.92f,.86f);
            var networkObject=new GameObject("Mirror · LAN & TCP forwarding");
            var transport=networkObject.AddComponent<TelepathyTransport>();transport.port=7777;
            var network=networkObject.AddComponent<TrialNetworkManager>();network.transport=transport;network.maxConnections=4;
            network.autoCreatePlayer=false;network.offlineScene=TrialNetworkManager.MenuScene;network.onlineScene="";network.dontDestroyOnLoad=true;network.runInBackground=true;
            var root=new GameObject("只鹅_智械危机 · Start");var menu=root.AddComponent<InkMainMenu>();
            const string materialPath="Assets/Art/UI/InkStart/InkLandscape.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!material){material=new Material(Shader.Find("Tomorin/Ink Menu"));AssetDatabase.CreateAsset(material,materialPath);}
            var data=new SerializedObject(menu);
            data.FindProperty("artwork").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            data.FindProperty("inkFont").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/InkKai.ttf");
            data.FindProperty("uiFont").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/InkUI.ttc");
            data.FindProperty("inkMaterial").objectReferenceValue=material;data.ApplyModifiedPropertiesWithoutUndo();
            menu.BuildVisuals();
            EditorSceneManager.SaveScene(scene,TrialNetworkManager.MenuScene);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(TrialNetworkManager.MenuScene,true),new EditorBuildSettingsScene(TrialNetworkManager.GameScene,true)}
                .Concat(EditorBuildSettings.scenes.Where(s=>s.path!=TrialNetworkManager.MenuScene&&s.path!=TrialNetworkManager.GameScene)).ToArray();
            AssetDatabase.SaveAssets();Selection.activeGameObject=root;
            Debug.Log("[InkStart] Menu scene built. Mirror TCP, 4 peers, editable port. Penguin reference integrated.");
        }

        public static void Capture(string path,int width=1600,int height=900)
        {
            var camera=Camera.main;var canvas=Object.FindFirstObjectByType<InkMainMenu>().GetComponentInChildren<Canvas>();
            var mode=canvas.renderMode;var view=canvas.worldCamera;float plane=canvas.planeDistance;
            var target=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(width,height,24);var texture=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally{canvas.renderMode=mode;canvas.worldCamera=view;canvas.planeDistance=plane;camera.targetTexture=target;RenderTexture.active=active;Object.DestroyImmediate(texture);rt.Release();Object.DestroyImmediate(rt);Canvas.ForceUpdateCanvases();}
        }
    }
}
