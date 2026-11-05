using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using TomorinTrial;
public static class PreviewMartialCombat
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Main()
    {
        var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
        if(!game || !game.Ready)throw new Exception("Start an offline stage first");
        bool ticking=game.enabled;game.enabled=false;game.CameraRig.Frozen=true;
        var p=game.Player;var input=game.gameObject.AddComponent<TrialInput>();
        var rig=p.GetComponent<TrialRig>();
        var start=typeof(TrialPlayer).GetMethod("StartAttack",Private);
        var clear=typeof(TrialFighter).GetMethod("ClearCombatHold",Private);
        var pose=typeof(TrialRig).GetMethod("LateUpdate",Private);
        var obj=new GameObject("Martial pose review");var camera=obj.AddComponent<Camera>();camera.enabled=false;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.19f,.21f);camera.cullingMask=1<<31;
        camera.orthographic=true;camera.orthographicSize=1.65f;
        var layers=new Dictionary<GameObject,int>();foreach(var t in p.GetComponentsInChildren<Transform>(true)){layers[t.gameObject]=t.gameObject.layer;t.gameObject.layer=31;}
        var rt=new RenderTexture(600,700,24);var pixels=new Texture2D(600,700,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        int[] moves={TrialPlayer.HiltStrike,TrialPlayer.HeelTurn,TrialPlayer.LowVault,TrialPlayer.VaultSweep,TrialPlayer.OrbitCut,TrialPlayer.FallingDraw,TrialPlayer.ElbowBreak,TrialPlayer.ScissorKick,TrialPlayer.SheathTurn,TrialPlayer.CloudRake};
        try
        {
            Directory.CreateDirectory("Logs/Trial/MartialPoses");
            foreach(int move in moves)
            {
                p.Restore(new Vector3(0,0,20));start.Invoke(p,new object[]{false,move});
                float sample=move==TrialPlayer.HeelTurn?.34f:move==TrialPlayer.ScissorKick?.27f:move==TrialPlayer.LowVault?.55f:.4f;
                while(p.Progress<sample){clear.Invoke(p,null);input.ApplyNetwork(default);p.Tick(.016f,input);}
                typeof(TrialRig).GetField("renderingTransition",Private).SetValue(rig,false);rig.Pose(p.Action,p.Progress,p.Combo,0,false,0);
                Vector3 centre=p.transform.position+Vector3.up*.85f;
                camera.transform.position=centre+new Vector3(3.4f,1.1f,4.5f);camera.transform.LookAt(centre);
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,600,700),0,0);pixels.Apply();
                File.WriteAllBytes("Logs/Trial/MartialPoses/"+move+".png",pixels.EncodeToPNG());
            }
            return "Rendered 10 distinct runtime skeleton poses";
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;rt.Release();
            foreach(var item in layers)item.Key.layer=item.Value;
            UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(input);
            p.Restore(new Vector3(0,0,20));game.CameraRig.Frozen=false;game.enabled=ticking;
        }
    }
}
