using System;
using System.Reflection;
using TomorinTrial;
using UnityEngine;

// Offline runtime previews; stop Play Mode afterwards to restore the scene.
public static class PreviewComboFlow
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Mirror()=>Show(TrialPlayer.ParryMirror,.30f,false);
    public static string Ascend()=>Show(TrialPlayer.ParryAscend,.45f,false);
    public static string Guard()=>Show(2,.12f,true);
    public static string Throw()=>Show(1,.12f,true);
    public static string Recall()=>Show(TrialPlayer.SpiralSword,.4f,false,true);
    public static string RecallRise()=>Show(TrialPlayer.SpiralSword,.4f,false,true,true);
    static string Show(int move,float time,bool guard,bool recall=false,bool rising=false)
    {
        var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
        if(!Application.isPlaying || !game || !game.Ready || TrialNetworkManager.SessionActive)throw new Exception("Start offline AshenCourt in Play Mode");
        game.Player.ResetRunBoon();game.StartTrial();game.Player.ChooseBoon(game.Player.OfferA);game.enabled=false;
        var p=game.Player;p.Restore(new Vector3(0,0,20));
        foreach(var foe in game.Enemies)foe.Teleport(new Vector3(15,0,36));
        var input=game.gameObject.AddComponent<TrialInput>();
        try
        {
            typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,move});
            while(p.ActionTime<time){input.ApplyNetwork(default);p.Tick(.016f,input);}
            if(guard)
            {
                input.ApplyNetwork(new TrialControls{held=1});p.Tick(.001f,input);
                for(int i=0;i<22;i++){input.ApplyNetwork(new TrialControls{held=1});p.Tick(.016f,input);}
            }
            if(recall)
            {
                input.ApplyNetwork(new TrialControls{pressed=rising?(byte)2:(byte)1});p.Tick(.001f,input);
                while(p.ActionTime<(rising?.42f:.20f)){input.ApplyNetwork(default);p.Tick(.016f,input);}
            }
            game.Effects.TickTime();
            game.CameraRig.Frozen=true;var view=game.CameraRig.View;
            view.transform.position=p.transform.position+new Vector3(3.4f,1.7f,4.1f);view.transform.LookAt(p.transform.position+Vector3.up*.9f);view.fieldOfView=42;
            Canvas.ForceUpdateCanvases();
            return p.ComboGuideTitle+" | "+p.ComboGuideHint("轻","重","Ctrl");
        }
        finally{UnityEngine.Object.DestroyImmediate(input);}
    }
}
