using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using TomorinTrial;
public static class PreviewInkCombat
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Main()
    {
        var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
        if(!game || !game.Ready)throw new Exception("Start offline arena");
        bool ticking=game.enabled;game.enabled=false;
        var effects=game.Effects;
        var make=typeof(TrialEffects).GetMethod("Art",Private);
        var update=typeof(TrialEffects).GetMethod("UpdateArt",Private);
        var cameraObject=new GameObject("Ink preview camera");var camera=cameraObject.AddComponent<Camera>();
        camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.78f,.76f,.70f);camera.cullingMask=1<<31;
        camera.orthographic=true;camera.orthographicSize=3.8f;
        camera.transform.position=new Vector3(0,9,-6);camera.transform.LookAt(new Vector3(0,0,.2f));
        var rt=new RenderTexture(1400,900,24);var pixels=new Texture2D(1400,900,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        object art=null; var roots=new System.Collections.Generic.List<Transform>();
        try
        {
            camera.orthographicSize=6.6f;camera.transform.position=new Vector3(0,18,1.7f);camera.transform.LookAt(new Vector3(0,0,1.7f),Vector3.forward);
            for(byte style=0;style<6;style++)
            {
            Vector3 origin=new Vector3((style%3-1)*4.8f,0,style<3?4.0f:-1.3f);
            TrialCombatCue cue=style==0?TrialCombatCue.MoonBlade:style==1?TrialCombatCue.SwordLeap:style==2?TrialCombatCue.RisingCut:style==3?TrialCombatCue.ThrustTrace:TrialCombatCue.WeaveSigil;
            art=make.Invoke(effects,new object[]{new TrialCombatEvent{cue=cue,style=style,position=style==0?origin-Vector3.forward*3.4f:origin,forward=Vector3.forward,end=origin+Vector3.forward*2,amount=style==4?3:2},style==0?1.36f:.5f,false});
            art.GetType().GetField("owner").SetValue(art,null);
            art.GetType().GetField("age").SetValue(art,style==0?.20f:.13f);update.Invoke(effects,new[]{art});
            var root=(Transform)art.GetType().GetField("root").GetValue(art);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            roots.Add(root);
            }
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1400,900),0,0);pixels.Apply();
            Directory.CreateDirectory("Logs/Trial");File.WriteAllBytes("Logs/Trial/SwordFormsPreview.png",pixels.EncodeToPNG());
            foreach(var root in roots){foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=0;root.gameObject.SetActive(false);}
            return "Logs/Trial/SwordFormsPreview.png";
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;rt.Release();
            UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cameraObject);
            game.enabled=ticking;
        }
    }
}
