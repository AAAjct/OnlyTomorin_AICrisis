using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TomorinTrial.Editor
{
    public static class TrialSwordComboValidation
    {
        public static string Status {get;private set;}="Not run";
        static IEnumerator routine;
        static double resumeAt;
        static readonly List<string> checks=new List<string>();
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static int errors;
        static TrialGame game;
        static TrialPlayer p;
        static TrialEnemy target;
        static TrialInput input;
        static GameObject wall;
        [MenuItem("Tools/Tomorin/Verify ordered sword combo")]
        public static void Run()
        {
            if(!EditorApplication.isPlaying || routine!=null)throw new InvalidOperationException("Enter Play Mode first.");
            checks.Clear();errors=0;Status="Running";Application.logMessageReceived+=OnLog;
            routine=Checks();resumeAt=0;EditorApplication.update+=Step;
        }
        static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)errors++;}
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);checks.Add(message);}
        static void Step()
        {
            if(EditorApplication.timeSinceStartup<resumeAt)return;
            try{if(!routine.MoveNext()){Finish(null);return;}resumeAt=EditorApplication.timeSinceStartup+(routine.Current is float seconds?seconds:0);}
            catch(Exception e){Finish(e);}
        }
        static void Finish(Exception e)
        {
            (routine as IDisposable)?.Dispose();routine=null;EditorApplication.update-=Step;Application.logMessageReceived-=OnLog;
            if(wall)UnityEngine.Object.DestroyImmediate(wall);
            if(game){game.enabled=true;if(p)p.Restore(new Vector3(0,0,20));}
            if(input)UnityEngine.Object.Destroy(input);
            Status=e==null?"PASS "+checks.Count:"FAIL "+e;
            Directory.CreateDirectory("Logs/Trial");File.WriteAllText("Logs/Trial/SwordComboValidation.txt",Status+"\n"+string.Join("\n",checks));
            if(e==null)Debug.Log(Status);else Debug.LogException(e);
        }
        static void ClearHold(TrialFighter fighter)=>typeof(TrialFighter).GetMethod("ClearCombatHold",Private).Invoke(fighter,null);
        static void Tick(float dt,byte pressed=0,Vector2 move=default)
        {
            // Advance in collision-sized steps. Contact pauses are tested by the existing polish suite.
            ClearHold(p);ClearHold(target);input.ApplyNetwork(new TrialControls{pressed=pressed,move=move,yaw=0});p.Tick(dt,input);
        }
        static void Advance(float seconds)
        {while(seconds>.0001f){float dt=Mathf.Min(.016f,seconds);Tick(dt);seconds-=dt;}}
        static void Reset()
        {
            p.ResetRunBoon();p.Restore(new Vector3(0,0,20));target.ResetFighter();target.Teleport(new Vector3(12,0,36));
            target.Champion.CancelAttack(true);Time.timeScale=1;Physics.SyncTransforms();
        }
        static void Link(byte key,float progress=.58f)
        {Advance(Mathf.Max(0,p.ActionDuration*progress-p.ActionTime));Tick(.001f,key);}
        static void Lights(int count)
        {Tick(.001f,1);for(int i=1;i<count;i++)Link(1);}
        static int Emissions=>(int)typeof(TrialPlayer).GetField("swordEmissions",Private).GetValue(p);
        static int Flights=>((ICollection)typeof(TrialPlayer).GetField("swordFlights",Private).GetValue(p)).Count;
        static void Input(float dt,byte pressed,byte held=0)
        {ClearHold(p);ClearHold(target);input.ApplyNetwork(new TrialControls{pressed=pressed,held=held,yaw=0});p.Tick(dt,input);}
        static void GrantParry()
        {
            target.Teleport(p.transform.position+Vector3.forward*3);target.transform.rotation=Quaternion.Euler(0,180,0);
            Input(.001f,0,1);float hp=p.Health;p.ReceiveHit(target,15,5);
            Check(p.Health==hp && p.ParryEmpowered,"Perfect frontal guard grants one empowered choice");ClearHold(p);ClearHold(target);
            Input(.001f,0);
        }
        static void ParryArtChecks()
        {
            Reset();GrantParry();float hp=target.Health;Tick(.001f,1);Advance(.17f);float first=target.Health;
            Advance(.18f);float second=target.Health;Advance(.23f);
            Check(p.Combo==TrialPlayer.ParryTriple && Emissions==3 && first<hp && second<first && target.Health<second,"Perfect guard light delivers three separately damaging thrusts through boss guard");
            Check(!p.ParryEmpowered,"Empowerment is consumed exactly once");
            Reset();GrantParry();Tick(.001f,2);Advance(.60f);
            Check(p.Combo==TrialPlayer.FireDragon && Emissions==1,"Perfect guard heavy rolls and launches a single fire dragon");
            Advance(1.3f);Check(target.Health<target.MaxHealth,"Fire dragon damages an enemy along its swept path");
            Reset();GrantParry();Input(.001f,2,4);
            Check(p.Action==FighterAction.Charge && p.Combo==TrialPlayer.FireDragon,"Holding empowered heavy begins dragon charge");
            for(int i=0;i<100 && p.Action==FighterAction.Charge;i++)Input(.016f,0,4);
            Check(p.Action==FighterAction.Heavy && p.Combo==TrialPlayer.FireDragon && p.SwordCharge==1,"Dragon charge automatically releases at the bounded maximum");
            var snapshot=p.CaptureNetwork(5);Check(snapshot.swordCharge==1,"Charge power travels in actor snapshots");
            Reset();Tick(.001f,4);Advance(.42f);float regular=20-p.transform.position.z;
            Reset();GrantParry();float origin=p.transform.position.z;Tick(.001f,4);Advance(.42f);
            Check(p.Combo==TrialPlayer.ParryDodge && origin-p.transform.position.z>regular*1.8f && p.ParryDodgeFollowup,"Empowered dodge travels twice the ordinary distance and reserves its follow-up");
            target.Teleport(new Vector3(0,0,23));target.transform.rotation=Quaternion.identity;Tick(.001f,1);
            Check(p.Combo==TrialPlayer.BehindThrust && p.transform.position.z<target.transform.position.z-.7f && p.transform.position.z>21,"Empowered dodge light blinks behind the opponent");
            Reset();GrantParry();Tick(.001f,4);Advance(.42f);Tick(.001f,1);
            Check(p.Combo==TrialPlayer.BehindThrust && Vector3.Dot(p.transform.position-target.transform.position,target.transform.forward)<-.7f,"Back blink passes through the facing enemy's body to the far side");
            Reset();GrantParry();Tick(.001f,4);Advance(.22f);Tick(.001f,2);Advance(.25f);
            Check(p.Combo==TrialPlayer.SpiralSword && Emissions==1,"Empowered dodge heavy throws one returning spiral sword");
            hp=target.Health;Advance(1.4f);Check(target.Health<hp,"Returning sword sweeps real enemy collisions");
            Reset();GrantParry();Input(.001f,2,4);Input(.1f,0,4);Tick(.001f,4);
            Check(p.Action==FighterAction.Dodge,"Dragon charging cancels directly into dodge");
            Reset();target.Teleport(new Vector3(0,0,23));Tick(.001f,4);Advance(.10f);p.ReceiveHit(target,15,5);
            Advance(.22f);float leapStart=p.transform.position.z;Tick(.001f,2);
            Check(p.Combo==TrialPlayer.DodgeDragon && p.Action==FighterAction.Heavy,"Perfect dodge heavy selects the backward leap and dragon cleave");
            Advance(.32f);Check(p.transform.position.y>1.4f && p.transform.position.z<leapStart-1.3f && Emissions==0,"Dodge dragon really jumps backward and never fires in midair");
            Advance(.52f);Check(p.DodgeDragonLanded && p.transform.position.y<.2f && Emissions==0,"Dodge dragon lands before the release cleave");
            Advance(.12f);Check(Emissions==1,"The grounded downward cleave releases exactly one fire dragon");
            Advance(.2f);Check(Emissions==1,"Landing recovery does not duplicate the dodge dragon");
            foreach(int move in new[]{0,1,2,3,5,6,20,26,29,30,31,32,34,35,36})
            {
                Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{move==5 || move==6 || move==20 || move==32 || move==35 || move==36,move});
                Advance(.09f);Tick(.001f,4);Check(p.Action==FighterAction.Dodge,"Move "+move+" accepts an early dodge cancel");
                Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{move==5 || move==6 || move==20 || move==32 || move==35 || move==36,move});
                Advance(.07f);Input(.001f,0,1);Check(p.Guarding,"Move "+move+" accepts an early guard cancel");
            }
            foreach(var cue in new[]{TrialCombatCue.SakuraScatter,TrialCombatCue.SpiralSword,TrialCombatCue.FireDragon})
            {
                var message=new TrialCombatEvent{player=5,cue=cue,position=Vector3.up,end=new Vector3(3,1,7),forward=Vector3.forward,amount=.75f};
                using(var writer=NetworkWriterPool.Get()){writer.Write(message);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment())){var copy=reader.Read<TrialCombatEvent>();Check(copy.end==message.end && copy.amount==message.amount,"Network event preserves path endpoints and power for "+cue);}}
            }
        }
        static IEnumerator Checks()
        {
            SceneManager.LoadScene("CrimsonSwordTerrace");yield return .8f;
            double deadline=EditorApplication.timeSinceStartup+25;
            do{game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();if(game && game.Ready)break;yield return .1f;}while(EditorApplication.timeSinceStartup<deadline);
            Check(game && game.Ready,"Sword arena initializes");game.StartTrial();game.enabled=false;
            p=game.Player;target=game.Enemies[0];input=game.gameObject.AddComponent<TrialInput>();
            Reset();int frame=Time.frameCount;yield return .12f;Check(Time.frameCount>frame,"Play Mode frame count advances");
            Reset();Lights(1);
            int[] sequence={0,1,2,3,26,27,28,29};
            for(int i=0;i<sequence.Length;i++)
            {
                Check(p.Action==FighterAction.Light && p.Combo==sequence[i],"Ordered light stage "+(i+1)+" uses move "+sequence[i]);
                if(i<sequence.Length-1)Link(1,i>=4?.63f:.58f);
            }
            Advance(.5f);Check(p.transform.position.y<.2f && Emissions==0,"Eighth light lands rather than looping into slash one");
            Check(Flights==12,"Landing emits twelve physical dark-blue sand projectiles");
            Check(!p.AttackActive,"Dive does not also apply the generic sword hitbox");

            Reset();Lights(4);Advance(.35f);
            Check(p.transform.position.y>1.8f && !p.AttackActive,"Fourth light is a real empty leap without a melee hitbox");
            Advance(.08f);float airY=p.transform.position.y;Link(1);Advance(.2f);
            Check(p.Combo==26 && Emissions==1 && Flights==1,"First airborne light emits exactly one crimson ranged blade");
            Check(Mathf.Abs(p.transform.position.y-airY)<.12f,"Air slash keeps actual controller altitude");
            Advance(.11f);Check(Emissions==1 && Flights==1,"Repeated active frames never duplicate a blade");
            var state=p.CaptureNetwork(3);var remote=new GameObject("Sword snapshot validation");var copy=remote.AddComponent<TrialPlayer>();
            typeof(TrialFighter).GetField("game",Private).SetValue(copy,game);
            copy.ApplyNetwork(state,.016f);Check(copy.Combo==26 && Vector3.Distance(copy.transform.position,state.position)<.01f,"Remote snapshot preserves airborne move and physical height");
            UnityEngine.Object.DestroyImmediate(remote);

            Reset();Lights(1);Link(2);Check(p.Action==FighterAction.Heavy && p.Combo==4,"Light one branches into the larger reverse diagonal");
            Reset();Lights(2);Link(2);Check(p.Combo==5,"Light two branches into sakura thrust");float sakuraStart=p.transform.position.z;Advance(.40f);
            Check(p.transform.position.z>sakuraStart+5.5f,"Two-light heavy lunges six metres");
            Check(Emissions==1,"Sakura scatter fires once during the thrust");
            Reset();Lights(3);Link(2);float before=p.transform.position.z;Advance(.30f);
            Check(p.Combo==6 && p.transform.position.z<before-3.7f,"Light three heavy retreats over 3.7 metres before casting");
            Check(Emissions==0,"Retreat completes before moon blades launch");Advance(.4f);
            Check(Emissions==3 && Flights==3,"Retreating heavy casts exactly three moon crescent projectiles");

            Reset();Lights(4);Link(2);float diveX=p.transform.position.x,diveZ=p.transform.position.z;
            Check(p.Combo==20 && p.Action==FighterAction.Heavy,"Fourth light heavy enters the sword-down plunge");Advance(.50f);
            Check(p.transform.position.y<.2f && Mathf.Abs(p.transform.position.x-diveX)<.01f && Mathf.Abs(p.transform.position.z-diveZ)<.01f,"Aerial heavy falls vertically in place and lands");
            Check(Flights==12,"Heavy plunge launches the radial sand fan once");Link(1,.78f);
            Check(p.Combo==30 && p.Action==FighterAction.Light,"Plunge links directly into the sakura lunge");float lunge=p.transform.position.z;Advance(.31f);
            Check(p.transform.position.z>lunge+6.2f && Emissions==1,"Sakura follow-up lunges nearly seven metres and scatters petals");

            Reset();Lights(4);Advance(1.45f);yield return .36f;Advance(.6f);
            Check(p.Action==FighterAction.Free && p.transform.position.y<.2f,"Stopping input exits air suspension and returns to the floor");
            Reset();Lights(4);Link(2);p.ReceiveHit(null,1,0,true);Advance(.6f);
            Check(p.Action==FighterAction.Free,"An interrupted plunge recovers from stagger normally");

            Reset();Lights(3);Link(2);target.Teleport(new Vector3(0,0,30));float hp=target.Health;Advance(1.1f);
            Check(target.Health<hp,"Moon crescents deal real damage beyond melee reach");Advance(.8f);float contactHealth=target.Health;Advance(.5f);
            Check(Mathf.Abs(target.Health-contactHealth)<.01f,"Expired projectiles cannot repeat damage");
            Reset();Lights(3);Link(2);target.Teleport(new Vector3(0,0,30));hp=target.Health;
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1.5f,25);wall.transform.localScale=new Vector3(10,4,.4f);Physics.SyncTransforms();Advance(1.3f);
            Check(Mathf.Abs(target.Health-hp)<.01f,"World geometry blocks ranged damage");UnityEngine.Object.DestroyImmediate(wall);wall=null;

            Reset();Lights(2);Link(2);target.Teleport(p.transform.position+Vector3.forward*3f);hp=target.Health;Advance(.40f);
            Check(target.Health<hp && Emissions==1,"Sakura burst and thrust damage are wired to actual opponents");
            Check(game.Effects.GetComponentsInChildren<ParticleSystem>().Length>0,"Runtime sword presentation initializes without missing resources");
            Reset();Lights(4);Link(1);Link(1,.63f);Link(1,.63f);Link(1,.63f);float diagonalStart=p.transform.position.z;Advance(.45f);
            Check(p.Combo==TrialPlayer.AirSandDive && p.transform.position.y<.2f && p.transform.position.z>diagonalStart+3,"Air ranged finisher dives diagonally forward before landing");
            ParryArtChecks();
            foreach(var cue in new[]{TrialCombatCue.CrimsonBlade,TrialCombatCue.MoonBlade,TrialCombatCue.BlueSand,TrialCombatCue.SakuraScatter,TrialCombatCue.SandImpact})
            {
                var message=new TrialCombatEvent{player=7,cue=cue,position=new Vector3(1,2,3),forward=Vector3.forward};
                using(var writer=NetworkWriterPool.Get())
                {
                    writer.Write(message);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))
                    {var restored=reader.Read<TrialCombatEvent>();Check(restored.cue==cue && restored.player==7 && restored.position==message.position,"Mirror serializes sword event "+cue);}
                }
            }
            Check(errors==0,"No runtime errors during ordered sword combo validation");
        }
    }
}
