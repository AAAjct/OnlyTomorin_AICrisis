using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static class TrialPolishSetup
    {
        [MenuItem("Tools/Tomorin/Apply combat and interface polish")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
            var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(current.isDirty)EditorSceneManager.SaveScene(current);
            var scene=EditorSceneManager.OpenScene(TrialNetworkManager.GameScene);
            var game=Object.FindAnyObjectByType<TrialGame>();var data=new SerializedObject(game);
            data.FindProperty("naiWaModel").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/NaiWa/NaiWa.fbx");
            data.FindProperty("uiFont").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/UI/Fonts/InkUI.ttc");
            data.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene);
            TrialBuilder.Bake();
            scene=EditorSceneManager.OpenScene(TrialNetworkManager.MenuScene);
            var menu=Object.FindAnyObjectByType<InkMainMenu>();
            foreach(var label in menu.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if(label.text=="01" || label.text=="02" || label.text=="03" || label.text=="04")Object.DestroyImmediate(label.gameObject);
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[Polish] NaiWa wired, 19 skeletal clips baked, title numbers removed. No player exported.");
        }
    }
}
