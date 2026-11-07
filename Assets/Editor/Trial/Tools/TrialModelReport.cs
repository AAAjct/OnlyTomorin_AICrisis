using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    [InitializeOnLoad]
    public static class TrialModelReport
    {
        static TrialModelReport() { EditorApplication.delayCall += Report; }

        public static void Report()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Tomorin/Tomorin.fbx");
            if (asset == null) return;
            var text = new StringBuilder();
            foreach (var t in asset.GetComponentsInChildren<Transform>(true))
                text.AppendLine($"{AnimationUtility.CalculateTransformPath(t, asset.transform)} | p={t.localPosition} r={t.localEulerAngles} s={t.localScale}");
            foreach (var r in asset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                text.AppendLine($"MESH {r.name} bounds={r.localBounds} bones={r.bones.Length} materials={string.Join(",",r.sharedMaterials.Select(m=>m ? m.name : "NULL"))}");
            Directory.CreateDirectory("Logs/Trial");
            File.WriteAllText("Logs/Trial/ModelReport.txt", text.ToString());
        }
    }
}
