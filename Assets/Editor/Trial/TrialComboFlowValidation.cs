using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mirror;
using TomorinTrial;
using UnityEngine;

namespace TomorinTrial.Editor
{
public static class TrialComboFlowValidation
{
    public static string Status {get;private set;}="Not run";
    [UnityEditor.MenuItem("Tools/Tomorin/Verify readable combo flow")]
    public static void Run()
    {
        try{Status=Main();Debug.Log(Status);}
        catch(Exception e){Status="FAIL "+e;Debug.LogException(e);}
    }
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    static TrialGame game;
    static TrialPlayer p;
    static TrialInput input;
    static readonly List<string> checks=new List<string>();
    static int errors;
    static float step;
    static void Check(bool pass,string message)
    {if(!pass)throw new Exception(message+"; actual="+p.Action+"/"+p.Combo+" @ "+p.Progress);checks.Add(message);}
    static void Log(string message,string stack,LogType type)
    {if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors++;}
    static void Tick(float dt,byte pressed=0,byte held=0,Vector2 move=default)
    {
        typeof(TrialFighter).GetMethod("ClearCombatHold",Private).Invoke(p,null);
        input.ApplyNetwork(new TrialControls{pressed=pressed,held=held,move=move,yaw=0});
        p.Tick(dt,input);
    }
    static void Advance(float duration,byte held=0)
    {while(duration>.00001f){float dt=Mathf.Min(step,duration);Tick(dt,0,held);duration-=dt;}}
    static void Reset()
    {
        p.ResetRunBoon();p.Restore(new Vector3(0,0,20));
        foreach(var enemy in game.Enemies)enemy.Teleport(new Vector3(35,0,40));
        Physics.SyncTransforms();p.GetComponent<CharacterController>().Move(Vector3.down*.1f);
    }
    static void Link(byte key)
    {
        string method=p.Action==FighterAction.Heavy?"HeavyLinkProgress":"LightLinkProgress";
        float link=(float)typeof(TrialPlayer).GetMethod(method,Static).Invoke(null,new object[]{p.Combo});
        int frames=0,previous=p.Combo;
        while(p.Progress<link+.035f && frames++<300)Tick(step);
        Tick(.001f,key);
        for(int i=0;i<8 && p.Combo==previous;i++)Tick(step);
    }
    static void Move(int id,bool heavy=false)
    {typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{heavy,id});}
    static int Next(bool fromHeavy,int move,bool heavy)
    {
        string name=fromHeavy?(heavy?"HeavyHeavyBranch":"HeavyLightBranch"):(heavy?"LightHeavyBranch":"NextLight");
        return (int)typeof(TrialPlayer).GetMethod(name,Static).Invoke(null,new object[]{move});
    }
    static int Visit(bool heavy,int move,HashSet<int> path)
    {
        int state=move*2+(heavy?1:0);
        if(!path.Add(state))throw new Exception("Combo route loops at "+state);
        int longest=1;
        foreach(bool nextHeavy in new[]{false,true})
        {
            int next=Next(heavy,move,nextHeavy);
            if(next>=0)longest=Math.Max(longest,1+Visit(nextHeavy,next,path));
        }
        path.Remove(state);return longest;
    }
    static void Checks()
    {
        foreach(float fps in new[]{30f,60f,144f})
        {
            step=1/fps;Reset();Tick(.001f,1);
            foreach(int expected in new[]{0,1,2,TrialPlayer.WheelFinish})
            {
                Check(p.Action==FighterAction.Light && p.Combo==expected,"Ground light route stage "+expected+" at "+fps+" FPS");
                Check(p.transform.position.y<.2f,"Light route stays grounded at "+fps+" FPS");
                if(expected!=TrialPlayer.WheelFinish)Link(1);
            }
            Tick(.001f,1);Advance(p.ActionDuration*.80f);
            Check(p.Combo==TrialPlayer.WheelFinish,"Finisher completes its contact before restarting at "+fps+" FPS");
            Advance(.4f);Check(p.Combo==0 && p.Action==FighterAction.Light,"Buffered input restarts only after finisher recovery at "+fps+" FPS");
            Advance(.8f);Check(p.Action==FighterAction.Free,"No queued ghost attacks at "+fps+" FPS");

            Reset();Tick(step,1);Tick(step,1);Tick(step,2);Advance(.53f);
            Check(p.Combo==5 && p.Action==FighterAction.Heavy,"Rapid L,L,H keeps input order at "+fps+" FPS");
            Reset();Tick(.001f,2);Link(2);Check(p.Combo==18,"Second heavy at "+fps+" FPS");
            Link(2);Check(p.Combo==19,"Third heavy finishes at "+fps+" FPS");
            Advance(1.2f);Check(p.Action==FighterAction.Free,"Heavy route stops at "+fps+" FPS");

            foreach(bool heavy in new[]{false,true})
            {
                Reset();byte held=heavy?(byte)4:(byte)8;
                int[] expected=heavy?new[]{0,18,19}:new[]{0,1,2,TrialPlayer.WheelFinish};
                Tick(step,heavy?(byte)2:(byte)1,held);
                foreach(int move in expected)
                {
                    int frames=0;while(p.Combo!=move && frames++<180)Tick(step,0,held);
                    Check(p.Combo==move,"One held "+(heavy?"heavy":"light")+" reaches stage "+move+" at "+fps+" FPS");
                    Tick(step,0,held);
                }
                Tick(step);Advance(1.5f);
                Check(p.Action==FighterAction.Free,"Release ends held "+heavy+" without queued ghost attacks at "+fps+" FPS");
            }
            Reset();Tick(step,1,8);Advance(.12f,8);Tick(step,4,8);Advance(1,8);
            Check(p.Action==FighterAction.Free,"Dodge suppresses a still-held attack until release at "+fps+" FPS");
            Tick(step);Tick(step,1,8);Check(p.Action==FighterAction.Light,"Release and repress rearms held attacks at "+fps+" FPS");
            Reset();Tick(step,1);Tick(step,1);Tick(.08f,0,1);Advance(.18f,1);
            Check(p.Combo==TrialPlayer.SpiralSword,"Rapid L,L,G directly throws the rotating sword at "+fps+" FPS");

            Reset();Tick(step,64|1);Check(p.Combo==TrialPlayer.AirStep,"Jump and light preserve explicit air entry at "+fps+" FPS");
            Advance(.32f);Check(p.Combo==TrialPlayer.AirCross,"Jump's buffered attack becomes air cross at "+fps+" FPS");
            Link(1);Check(p.Combo==TrialPlayer.AirWheel,"Air wheel at "+fps+" FPS");
            Link(1);Check(p.Combo==TrialPlayer.AirFan,"Air fan at "+fps+" FPS");
            Advance(p.ActionDuration*.76f);
            Check((byte)typeof(TrialPlayer).GetField("comboBladeEmissions",Private).GetValue(p)==3,"All three air blades emit before the link at "+fps+" FPS");
            Link(1);Check(p.Combo==TrialPlayer.AirSandDive,"Air chain selects landing at "+fps+" FPS");
            Advance(1.3f);Check(p.Action==FighterAction.Free && p.transform.position.y<.2f,"Air route lands and ends at "+fps+" FPS");
        }
        step=1f/60;
        for(int count=1;count<=3;count++)
        {
            Reset();Tick(.001f,1);for(int i=1;i<count;i++)Link(1);Link(2);
            Check(p.Combo==count+3,"Light count "+count+" selects a distinct heavy finisher");
            Tick(.001f,1);Advance(p.ActionDuration*.72f);
            Check(p.Combo==count+3,"Heavy finisher "+count+" keeps its final contact");
            Advance(.6f);Check(p.Combo==0,"Heavy finisher "+count+" returns to the ground opener");
        }
        foreach(float distance in new[]{3f,15f})
        {
            Reset();game.Enemies[0].Teleport(p.transform.position+Vector3.forward*distance);Physics.SyncTransforms();
            Move(3);Link(1);Check(p.Combo==TrialPlayer.AirBladeFirst,"Target distance "+distance+" does not replace a chosen air move");
        }
        Reset();Tick(.001f,4);Advance(.12f);Tick(.001f,1);
        Check(p.Combo==TrialPlayer.RollThrust,"Roll light enters the close route");
        Link(1);Check(p.Combo==TrialPlayer.CrossDraw,"Roll route enters cross draw");
        Link(1);Check(p.Combo==TrialPlayer.HiltStrike,"Cross draw uses the hilt");
        Link(1);Check(p.Combo==TrialPlayer.HeelTurn,"Hilt leads to sweep recovery");
        Link(2);Check(p.Combo==TrialPlayer.LoftingDraw,"Heavy deliberately launches from the sweep");
        Link(2);Check(p.Combo==TrialPlayer.AerialCascade,"Launcher leads to explicit air blades");
        Link(2);Check(p.Combo==20,"Air blade heavy selects vertical landing");
        Advance(1.2f);Check(p.transform.position.y<.2f && p.Action==FighterAction.Free,"Launcher route ends on the floor");

        Reset();Tick(.001f,1);Tick(.001f,1);Tick(.08f,4);Advance(.9f);
        Check(p.Action==FighterAction.Free,"Dodge clears buffered attacks");
        Reset();Tick(.001f,1);Advance(.08f);Tick(.001f,0,1);
        Check(p.Guarding && p.Action==FighterAction.Free,"Guard can interrupt an opener");
        Tick(.001f,1);Advance(.16f);
        Check(p.Combo==TrialPlayer.CrossDraw,"Guard directly performs cross draw before its buffered follow-up");
        Advance(.08f);Tick(.001f,4);Advance(.9f);
        Check(p.Action==FighterAction.Free,"Weave dodge has no queued attacks");
        Reset();Tick(step,64);Advance(.32f);Tick(.001f,0,1);Advance(.05f);Tick(.001f,4);Advance(.85f);
        Check(p.transform.position.y<.2f,"Dodging the scheduled airborne guard art restores gravity");
        Reset();Tick(.001f,2);Link(2);Advance(.1f);Tick(.001f,0,1);Advance(.16f);
        Check(p.Combo==17 && p.Action==FighterAction.Heavy,"Second heavy plus guard directly selects the descending cleave");

        Reset();Tick(.001f,1);Link(1);Advance(.7f);
        Check(p.ComboGuideHint("L","H","J").Contains("樱花突刺"),"Recovery guide matches the retained second-light branch");
        Tick(.001f,2);Check(p.Combo==5,"Delayed heavy uses the same branch as immediate heavy");
        Reset();Tick(.001f,1);Advance(.7f);
        typeof(TrialPlayer).GetField("chainUntil",Private).SetValue(p,Time.time-1);
        Tick(.001f,1);Check(p.Combo==0,"Expired grace returns to the opener");

        Reset();Tick(.001f,1);Link(1);Advance(.6f);
        var state=p.CaptureNetwork(7);TrialActorState copy;
        using(var writer=NetworkWriterPool.Get())
        {writer.Write(state);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))copy=reader.Read<TrialActorState>();}
        Check(copy.combo==1 && copy.comboGrace>0 && !copy.comboHeavy,"Mirror serializes the combo continuation window");
        p.Restore(new Vector3(0,0,20));p.ApplyNetwork(copy,.1f);
        Check(p.ComboGuideHint("L","H","J").Contains("樱花突刺"),"Client recovery guide uses the authority's continuation window");
        Reset();Tick(.001f,1);Advance(.08f);Tick(.001f,0,1);state=p.CaptureNetwork(7);
        using(var writer=NetworkWriterPool.Get())
        {writer.Write(state);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))copy=reader.Read<TrialActorState>();}
        p.Restore(new Vector3(0,0,20));p.ApplyNetwork(copy,.1f);
        Check(copy.guardComboDelay>0 && p.ComboGuideTitle.Contains("交错斩"),"Client guide preserves the pending automatic guard art");

        foreach(var branch in new[]{
            new[]{0,0,TrialPlayer.CrossDraw},new[]{1,0,TrialPlayer.SpiralSword},
            new[]{2,0,TrialPlayer.MoonChase},new[]{TrialPlayer.CrossDraw,0,TrialPlayer.SpiralSword},new[]{TrialPlayer.OrbitCut,0,TrialPlayer.SpiralSword},new[]{TrialPlayer.DoubleFang,0,TrialPlayer.SpiralSword},
            new[]{TrialPlayer.HiltStrike,0,TrialPlayer.ElbowBreak},new[]{TrialPlayer.WheelFinish,0,TrialPlayer.LowVault},
            new[]{0,1,TrialPlayer.LoftingDraw},new[]{18,1,17},new[]{17,1,TrialPlayer.SakuraFollowup},
            new[]{TrialPlayer.AirCross,0,TrialPlayer.WallRebound},new[]{TrialPlayer.AirFan,0,TrialPlayer.AirPierce}})
        {
            Reset();
            if(TrialPlayer.IsAerialMove(branch[0])){Tick(step,64);Advance(.34f);}
            Move(branch[0],branch[1]==1);Advance(.12f);Tick(.001f,0,1);
            Check(p.Guarding && p.Action==FighterAction.Free,"Guard immediately defends from source "+branch[0]);
            Check(p.GuardFollowupMove==branch[2] && p.ComboGuideTitle.Contains(TrialPlayer.SwordMoveName(false,branch[2])),"Guard guide names the scheduled move "+branch[2]);
            Advance(.16f,1);
            Check(p.Combo==branch[2],"Guard alone from source "+branch[0]+" directly performs "+branch[2]);
            Advance(.08f,1);Check(p.Combo==branch[2],"Holding guard does not retrigger source "+branch[0]);
        }
        Reset();Tick(.001f,1);Advance(.1f);Tick(.001f,0,1);Advance(.16f,1);
        Advance(.09f);Tick(.001f,0,1);
        Check(p.Guarding && p.Action==FighterAction.Free,"Release and repress guard enables another deliberate guard art");
        Reset();Tick(.001f,1);Advance(.08f);Tick(.001f,0,1);Tick(.001f,1);Advance(.5f);
        Check(p.Combo==TrialPlayer.HiltStrike,"Attack pressed during guard windup is preserved after the automatic art");
        Reset();Tick(.001f,1);Advance(.08f);Tick(.001f,0,1);Tick(.001f,4);Advance(1);
        Check(p.Action==FighterAction.Free,"Dodge discards the pending automatic guard art");
        Reset();Tick(.001f,1,8);Advance(.1f,8);Tick(.001f,0,9);Advance(.16f,9);
        Check(p.Combo==TrialPlayer.CrossDraw,"Guard takes precedence over held light");
        Advance(.85f,8);Check(p.Combo==TrialPlayer.HiltStrike || p.Combo==TrialPlayer.HeelTurn,"Held light continues the guard art's physical follow-ups");

        Reset();Tick(.001f,1,8);Advance(.1f,8);Tick(.001f,0,9);Advance(.16f,9);Advance(.85f,9);
        Check(p.Combo==TrialPlayer.HiltStrike || p.Combo==TrialPlayer.HeelTurn,"Keeping guard held across successive attacks does not schedule another guard art");

        foreach(float fps in new[]{30f,60f,144f})
        {
            step=1/fps;Reset();Tick(step,1);Link(1);Advance(.1f);Tick(step,0,1);Advance(.16f);Advance(.35f);
            Check(p.Combo==TrialPlayer.SpiralSword && p.SwordRecallReady,"Guard throws a recallable physical sword at "+fps+" FPS");
            Vector3 blade=p.OrbitSwordPosition,before=p.transform.position;
            Tick(.0001f,1);
            Check(p.Combo==TrialPlayer.SwordRecallCut,"Fresh light immediately selects the recall cut at "+fps+" FPS");
            Check(Vector3.Distance(Vector3.ProjectOnPlane(p.transform.position-blade,Vector3.up),Vector3.zero)<.5f && Vector3.Distance(before,p.transform.position)>1,"Recall teleports beneath the actual flying sword at "+fps+" FPS");
            Check(!p.SwordRecallReady && p.Weapon.gameObject.activeSelf,"Recall removes the projectile and puts the sword in hand at "+fps+" FPS");
            var recallTarget=game.Enemies[0];recallTarget.ResetFighter();recallTarget.Teleport(p.transform.position+p.transform.forward*2);Physics.SyncTransforms();float recallHealth=recallTarget.Health;
            Advance(.3f);
            Check((int)typeof(TrialPlayer).GetField("swordEmissions",Private).GetValue(p)==1 && recallTarget.Health<recallHealth,"Recall cut has one real damaging slash at "+fps+" FPS");
            float landedHealth=recallTarget.Health;Advance(.08f);Check(recallTarget.Health==landedHealth,"Recall cut does not repeat damage each frame at "+fps+" FPS");
            Reset();Move(TrialPlayer.SpiralSword,true);Advance(.36f);blade=p.OrbitSwordPosition;
            Tick(.0001f,2);
            Check(p.Combo==TrialPlayer.SwordRecallRise && Vector3.ProjectOnPlane(p.transform.position-blade,Vector3.up).magnitude<.5f,"Heavy teleports to the actual blade before ascending at "+fps+" FPS");
            Advance(.42f);Check(p.transform.position.y>1.9f && p.Weapon.gameObject.activeSelf && !p.SwordRecallReady,"Heavy recall returns the sword and really rises above the floor at "+fps+" FPS");
        }
        step=1f/60;
        Reset();var sideTarget=game.Enemies[0];sideTarget.ResetFighter();sideTarget.Teleport(p.transform.position+Vector3.right*4);
        typeof(TrialPlayer).GetProperty("Target").SetValue(p,sideTarget);p.transform.rotation=Quaternion.Euler(0,35,0);Vector3 facing=p.transform.forward;
        Move(TrialPlayer.SpiralSword,true);Advance(.36f);
        var throwing=(System.Collections.IList)typeof(TrialPlayer).GetField("parryFlights",Private).GetValue(p);
        var flight=throwing[0];var flightType=flight.GetType();
        Vector3 castStart=(Vector3)flightType.GetField("start").GetValue(flight),castEnd=(Vector3)flightType.GetField("end").GetValue(flight);
        Check(Vector3.Dot((castEnd-castStart).normalized,facing)>.9999f && Mathf.Abs(Vector3.Distance(castStart,castEnd)-12)<.01f,"Throw follows the actor's facing for twelve metres even with an off-axis locked target");
        Reset();Move(TrialPlayer.SpiralSword,true);Advance(.36f);
        var bladeState=p.CaptureNetwork(7);
        using(var writer=NetworkWriterPool.Get())
        {writer.Write(bladeState);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))bladeState=reader.Read<TrialActorState>();}
        Check(bladeState.orbitSwordTime>0 && bladeState.orbitSwordPosition==p.OrbitSwordPosition,"Mirror snapshot carries the flying sword's position and recall window");
        var recallEvent=new TrialCombatEvent{cue=TrialCombatCue.SwordRecall,position=Vector3.zero,end=Vector3.forward*6};
        using(var writer=NetworkWriterPool.Get())
        {writer.Write(recallEvent);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))Check(reader.Read<TrialCombatEvent>().cue==TrialCombatCue.SwordRecall,"Recall cancellation event survives Mirror serialization");}
        Reset();Move(TrialPlayer.SpiralSword,true);Advance(.4f);
        var recallWall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            Vector3 delta=Vector3.ProjectOnPlane(p.OrbitSwordPosition-p.transform.position,Vector3.up);
            recallWall.transform.position=p.transform.position+delta*.5f+Vector3.up*1.5f;
            recallWall.transform.rotation=Quaternion.LookRotation(delta.normalized);recallWall.transform.localScale=new Vector3(8,3,.2f);Physics.SyncTransforms();
            Tick(.0001f,1);
            Check(Vector3.ProjectOnPlane(p.transform.position-new Vector3(0,0,20),Vector3.up).magnitude<delta.magnitude*.5f,"Recall teleport respects a wall between the player and the sword");
        }
        finally{UnityEngine.Object.DestroyImmediate(recallWall);}

        Reset();var foe=game.Enemies[game.Enemies.Count-1];foe.ResetFighter();
        foe.Teleport(p.transform.position+Vector3.forward*2.5f);foe.transform.rotation=Quaternion.Euler(0,180,0);Physics.SyncTransforms();
        Tick(.001f,0,1);float hp=p.Health;p.ReceiveHit(foe,15,5);
        Check(p.Health==hp && p.ParryEmpowered,"Actual perfect frontal guard grants the enhanced choice");
        Tick(.001f);Tick(.001f,1);Check(p.Combo==TrialPlayer.ParryTriple,"Perfect guard light opens with three thrusts");
        Advance(p.ActionDuration*.68f);
        Check((int)typeof(TrialPlayer).GetField("swordEmissions",Private).GetValue(p)==3,"Perfect three thrusts complete before extension");
        Link(1);Check(p.Combo==TrialPlayer.ParryMirror,"Perfect three thrusts extend into mirror crossing");
        foe.Teleport(p.transform.position+Vector3.forward*2);Physics.SyncTransforms();float enemyHp=foe.Health;
        Advance(p.ActionDuration*.72f);
        Check((int)typeof(TrialPlayer).GetField("swordEmissions",Private).GetValue(p)==2 && foe.Health<enemyHp,"Mirror crossing emits two real damaging contacts");
        float afterHits=foe.Health;Advance(.02f);
        Check(foe.Health==afterHits,"Mirror crossing does not repeat a contact every frame");
        Link(1);Check(p.Combo==TrialPlayer.ParryAscend,"Mirror light follows with ascending cut");
        Advance(.40f);Check(p.transform.position.y>2,"Ascending cut raises the actual controller above two metres");
        Link(2);Check(p.Combo==TrialPlayer.AerialCascade,"Ascending heavy continues with three air blades");
        Link(2);Check(p.Combo==20,"Perfect aerial route deliberately ends with a plunge");Advance(1.2f);
        Check(p.Action==FighterAction.Free && p.transform.position.y<.2f,"Perfect aerial route lands cleanly");

        Reset();foe.ResetFighter();foe.Teleport(p.transform.position+Vector3.forward*2.5f);foe.transform.rotation=Quaternion.Euler(0,180,0);Physics.SyncTransforms();
        Tick(.001f,1);Advance(.08f);Tick(.001f,0,1);Advance(.05f,1);hp=p.Health;p.ReceiveHit(foe,15,5);
        Check(p.Health==hp && p.ParryEmpowered,"Guard-art preparation still accepts a real perfect guard");
        Advance(.20f,1);
        Check(p.ParryEmpowered && p.Action==FighterAction.Free,"Real perfect guard cancels the scheduled ordinary guard art");
        Tick(.001f,1);Check(p.Combo==TrialPlayer.ParryTriple,"Perfect guard keeps its chosen enhanced counter after the ordinary art is cancelled");

        foreach(bool heavy in new[]{false,true})
        {
            Reset();typeof(TrialPlayer).GetMethod("EmpowerParry",Private).Invoke(p,null);
            Tick(.001f,heavy?(byte)2:(byte)1);
            Check(p.Combo==(heavy?TrialPlayer.FireDragon:TrialPlayer.ParryTriple),"Perfect guard choice is not replaced by ordinary weaving");
            Link(heavy?(byte)1:(byte)2);
            Check(p.Combo==(heavy?TrialPlayer.MoonChase:TrialPlayer.SpiralSword),"Perfect route has a distinct ranged or pursuit extension");
            Link(heavy?(byte)2:(byte)1);
            Check(p.Combo==(heavy?TrialPlayer.ParryAscend:TrialPlayer.SwordRecallCut),"Perfect extension continues into the planned follow-up");
        }
        Reset();Move(TrialPlayer.ParryMirror);Link(2);
        Check(p.Combo==TrialPlayer.SakuraFollowup,"Mirror heavy chooses a grounded sakura finisher");
        Reset();Move(TrialPlayer.ParryMirror);Tick(.001f,1);Advance(.08f);Tick(.001f,4);Advance(.8f);
        Check(p.Action==FighterAction.Free,"Enhanced mirror dodge clears queued follow-ups");
        Reset();Move(TrialPlayer.ParryAscend);Advance(.35f);Tick(.001f,0,1);Tick(.001f,4);Advance(1);
        Check(p.transform.position.y<.2f,"Enhanced ascending guard-to-dodge cancellation restores gravity");
        Reset();var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            wall.transform.position=p.transform.position+Vector3.right*.95f+Vector3.up*1.5f;wall.transform.localScale=new Vector3(.2f,3,8);Physics.SyncTransforms();
            Move(TrialPlayer.ParryMirror);Advance(.20f);
            Check(p.transform.position.x<.7f,"Mirror side-step is blocked by real world geometry");
        }
        finally{UnityEngine.Object.DestroyImmediate(wall);}
        foreach(int move in new[]{TrialPlayer.ParryMirror,TrialPlayer.ParryAscend})
        {
            Reset();Move(move);Advance(.12f);state=p.CaptureNetwork(7);
            using(var writer=NetworkWriterPool.Get())
            {writer.Write(state);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))copy=reader.Read<TrialActorState>();}
            Check(copy.combo==move,"New perfect move "+move+" survives Mirror snapshots");
        }
        foreach(int root in new[]{0,TrialPlayer.AirStep,TrialPlayer.RollThrust,TrialPlayer.RollFlash,TrialPlayer.GuardNeedle,TrialPlayer.GuardBloom,TrialPlayer.ParryTriple,TrialPlayer.FireDragon,TrialPlayer.BehindThrust,TrialPlayer.SpiralSword,TrialPlayer.SwordRecallCut,TrialPlayer.SwordRecallRise})
            foreach(bool heavy in new[]{false,true})
                Check(Visit(heavy,root,new HashSet<int>())<=16,"Every choice from root "+root+"/"+heavy+" reaches a recovery boundary within sixteen moves");
        Check(errors==0,"No runtime errors during combo flow checks");
    }
    public static string Main()
    {
        game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();
        if(!Application.isPlaying || !game || !game.Ready || TrialNetworkManager.SessionActive)throw new Exception("Open offline AshenCourt in Play Mode first");
        bool enabled=game.enabled,gate=game.GateOpen;game.enabled=false;p=game.Player;
        typeof(TrialGame).GetProperty("GateOpen").SetValue(game,true);
        input=game.gameObject.AddComponent<TrialInput>();checks.Clear();errors=0;
        Application.logMessageReceived+=Log;
        string report;
        try{Checks();report="PASS "+checks.Count;}
        catch(Exception ex){checks.Add("FAIL "+ex);throw;}
        finally
        {
            Application.logMessageReceived-=Log;
            Directory.CreateDirectory("Logs/Trial");
            File.WriteAllText("Logs/Trial/ComboFlowValidation.txt",(checks.Count>0?string.Join("\n",checks):"No checks")+"\n");
            UnityEngine.Object.DestroyImmediate(input);p.Restore(new Vector3(0,0,20));game.enabled=enabled;typeof(TrialGame).GetProperty("GateOpen").SetValue(game,gate);
        }
        File.AppendAllText("Logs/Trial/ComboFlowValidation.txt",report+"\n");return report;
    }
}
}
