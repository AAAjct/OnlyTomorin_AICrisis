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
        static void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert)errors++;}
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
            // This suite uses a stationary combat target, outside its spawn cinematic.
            typeof(TrialChampion).GetField("introPlayed",Private).SetValue(target.Champion,true);
            target.Champion.CancelAttack(true);Time.timeScale=1;Physics.SyncTransforms();
        }
        static void Link(byte key,float progress=.58f)
        {Advance(Mathf.Max(0,p.ActionDuration*progress-p.ActionTime));Tick(.001f,key);}
        static void Lights(int count)
        {Tick(.001f,1);for(int i=1;i<count;i++)Link(1);}
        static int Emissions=>(int)typeof(TrialPlayer).GetField("swordEmissions",Private).GetValue(p);
        static int Flights=>((ICollection)typeof(TrialPlayer).GetField("swordFlights",Private).GetValue(p)).Count;
        static int MotionVisuals(TrialCombatCue cue)
        {
            int count=0;var arts=(Array)typeof(TrialEffects).GetField("swordArts",Private).GetValue(game.Effects);
            foreach(var art in arts)
            {
                if(art==null)continue;var type=art.GetType();
                var root=(Transform)type.GetField("root").GetValue(art);
                if(root.gameObject.activeSelf && ((TrialCombatEvent)type.GetField("cue").GetValue(art)).cue==cue)count++;
            }
            return count;
        }
        static void Input(float dt,byte pressed,byte held=0)
        {ClearHold(p);ClearHold(target);input.ApplyNetwork(new TrialControls{pressed=pressed,held=held,yaw=0});p.Tick(dt,input);}
        static void GrantParry()
        {
            target.Teleport(p.transform.position+Vector3.forward*3);target.transform.rotation=Quaternion.Euler(0,180,0);
            Input(.001f,0,1);float hp=p.Health;p.ReceiveHit(target,15,5);
            Check(p.Health==hp && p.ParryEmpowered,"Perfect frontal guard grants one empowered choice");ClearHold(p);ClearHold(target);
            Input(.001f,0);
        }
        static IEnumerator FlowChecks()
        {
            // Actual edges at several frame rates: the second light must not be
            // overwritten by the heavy, even when both arrive before link-open.
            foreach(float dt in new[]{1f/30,1f/60,1f/144})
            {
                Reset();Tick(dt,1);Tick(dt,1);Tick(dt,2);Advance(.52f);
                Check(p.Action==FighterAction.Heavy && p.Combo==5,"Rapid L,L,H preserves sakura thrust at "+Mathf.RoundToInt(1/dt)+" FPS");
                Advance(1.2f);Check(p.Action==FighterAction.Free,"Consumed rapid taps do not replay extra attacks");
            }
            Reset();Tick(.001f,1);p.HoldCombat(.50f);Input(0,1);Input(0,2);
            yield return .55f;Advance(.53f);
            Check(p.Action==FighterAction.Heavy && p.Combo==5,"Ordered light and heavy taps survive a long real contact hold");

            Reset();Lights(2);Advance(.48f);Check(p.Action==FighterAction.Free,"Second light reaches free recovery");
            yield return .40f;Tick(.001f,2);
            Check(p.Action==FighterAction.Heavy && p.Combo==5,"Delayed heavy retains the two-light branch during the extended grace");
            Reset();Lights(2);Advance(.48f);yield return .60f;Tick(.001f,2);
            Check(p.Combo==0,"Expired combo grace returns to the base heavy");

            Reset();Tick(.001f,2);Advance(.9f);Tick(.001f,2);
            Check(p.Combo==18,"Heavy recovery uses the same next branch as an immediate link");
            Reset();Lights(2);Link(2);Link(1,.70f);Check(p.Combo==22,"Sakura thrust retracts into the draw cut");
            Link(1);Check(p.Combo==TrialPlayer.CrossDraw,"Draw cut enters the extended grounded chain");
            Reset();Lights(2);Link(2);Link(2,.70f);
            Check(p.Combo==TrialPlayer.FlowingSweep,"Sakura thrust connects into a new flowing sweep");
            Link(2,.70f);Check(p.Combo==TrialPlayer.RisingBridge,"Flowing sweep connects into the rising bridge");
            Link(1,.70f);Check(p.Combo==3,"Rising bridge launches directly into the aerial route");
            Link(2);Advance(.50f);Link(1,.78f);Check(p.Combo==TrialPlayer.SakuraFollowup,"Extended route rejoins the landing sakura thrust");
            Link(2);Check(p.Combo==TrialPlayer.FlowingSweep,"Landing sakura thrust can continue into the sweep");
            Reset();Lights(3);Link(2);Link(1,.72f);
            Check(p.Combo==TrialPlayer.MoonChase,"Three moon blades connect into pursuit thrust");
            float origin=p.transform.position.z;Advance(.28f);
            Check(p.transform.position.z>origin+3.8f,"Moon pursuit closes the actual distance after a ranged retreat");
            Link(2);Check(p.Combo==TrialPlayer.FlowingSweep,"Pursuit thrust reconnects to the sweep route");

            Reset();Lights(2);Link(2);
            for(int cycle=0;cycle<10;cycle++)
            {
                Link(2,.72f);Check(p.Combo==TrialPlayer.FlowingSweep,"Sustained chain reaches sweep cycle "+cycle);
                Link(2,.72f);Check(p.Combo==TrialPlayer.RisingBridge,"Sustained chain reaches rising bridge cycle "+cycle);
                Link(2,.72f);Check(p.Combo==6,"Sustained chain reaches moon blades cycle "+cycle);
                Link(1,.72f);Check(p.Combo==TrialPlayer.MoonChase,"Sustained chain reaches pursuit cycle "+cycle);
            }
            Check(p.Stamina>p.DodgeCost*2,"Forty linked attacks retain stamina for two emergency dodges without idle recovery");

            foreach(int move in new[]{TrialPlayer.FlowingSweep,TrialPlayer.RisingBridge,TrialPlayer.MoonChase})
            {
                Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{true,move});
                target.Teleport(p.transform.position+Vector3.forward*2.5f);Physics.SyncTransforms();float hp=target.Health;
                Advance(p.ActionDuration*.5f);Check(target.Health<hp,"Bridge move "+move+" deals real sword damage");
                float after=target.Health;Advance(.08f);Check(target.Health==after,"Bridge move "+move+" hits each target once");
                Tick(.001f,4);Check(p.Action==FighterAction.Dodge,"Bridge move "+move+" allows dodge cancellation");
                Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{true,move});
                Advance(.10f);Input(.001f,0,1);Check(p.Action==FighterAction.Free && p.Guarding,"Bridge move "+move+" allows guard cancellation");
                Advance(.7f);Check(p.Action==FighterAction.Free,"Guard cancellation discards queued attacks");
                var state=p.CaptureNetwork(7);state.combo=(byte)move;state.action=(byte)FighterAction.Heavy;
                using(var writer=NetworkWriterPool.Get())
                {writer.Write(state);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment()))Check(reader.Read<TrialActorState>().combo==move,"Bridge move "+move+" survives snapshot serialization");}
            }
            Reset();Tick(.001f,1);Tick(.001f,1);Tick(.001f,2);Tick(.08f,4);Advance(1);
            Check(p.Action==FighterAction.Free,"Defensive dodge clears both queued follow-ups");
            Reset();Tick(.001f,1);Tick(.001f,1);Tick(.001f,2);p.ReceiveHit(null,1,0,true);Advance(1);
            Check(p.Action==FighterAction.Free,"Stagger clears both queued follow-ups");
            Reset();Lights(2);Advance(.18f);
            var rig=p.GetComponent<TrialRig>();var render=typeof(TrialRig).GetMethod("LateUpdate",Private);
            typeof(TrialRig).GetField("hasRenderedPose",Private).SetValue(rig,false);render.Invoke(rig,null);
            Vector3 hand=p.Weapon.position;Quaternion sword=p.Weapon.rotation;Vector3 root=p.Visual.localPosition;
            Link(2);render.Invoke(rig,null);
            Check(Vector3.Distance(p.Weapon.position,hand)<.04f && Quaternion.Angle(p.Weapon.rotation,sword)<1,"Sakura transition starts at the rendered hand and blade pose");
            Check(Vector3.Distance(root,p.Visual.localPosition)<.01f,"Combo transition preserves the rendered visual root");
            yield return .16f;Advance(.20f);render.Invoke(rig,null);
            Check(Vector3.Distance(p.Weapon.position,rig.RightHand.position)<.001f,"Sword remains attached to the smoothed hand during the new move");
            Reset();Lights(4);Advance(.10f);
            Check(MotionVisuals(TrialCombatCue.SwordLeap)==1,"Air launch creates one rising scarlet ribbon and foot ring");
            Advance(.10f);Check(MotionVisuals(TrialCombatCue.SwordLeap)==1,"Launch effect does not repeat during its active frames");
            Link(2);Advance(.11f);
            Check(MotionVisuals(TrialCombatCue.SwordDescent)==1,"Sword-down plunge emits a body-following descent ribbon");
            Advance(.45f);
            Check(MotionVisuals(TrialCombatCue.SandImpact)==1 && Flights==12,"Landing adds layered scarlet rings while preserving the twelve blue sand flights");
            Reset();Lights(3);Advance(.24f);
            Check(MotionVisuals(TrialCombatCue.SwordTurn)==1,"Third light's turn-out has one curved waist-height sword ribbon");
            Reset();Lights(2);Link(2);Link(2,.70f);Advance(.18f);
            Check(MotionVisuals(TrialCombatCue.SwordTurn)==1,"Flowing sweep has a dedicated turning ribbon");
            Reset();Lights(4);Link(2);Advance(.11f);Input(.001f,0,1);
            typeof(TrialEffects).GetMethod("TickSwordArtEffects",Private).Invoke(game.Effects,null);
            Check(MotionVisuals(TrialCombatCue.SwordDescent)==0 && MotionVisuals(TrialCombatCue.SwordLeap)==0,"Guard cancellation removes attached leap and descent effects");
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
        static void RollAndExtendedChecks()
        {
            foreach(float dt in new[]{1f/30,1f/60,1f/144})
            foreach(byte key in new byte[]{1,2})
            {
                Reset();Tick(dt,4);Advance(.12f);Tick(dt,key);
                int expected=key==1?TrialPlayer.RollThrust:TrialPlayer.RollFlash;
                Check(p.Combo==expected && p.Action==(key==1?FighterAction.Light:FighterAction.Heavy),"Roll cancels into "+expected+" at "+Mathf.RoundToInt(1/dt)+" FPS");
                Check(!p.Invulnerable,"Roll cancellation ends roll invulnerability");
                float z=p.transform.position.z;Advance(.24f);
                Check(p.transform.position.z>z+(key==1?4.4f:1.2f),"Roll follow-up advances along character forward");
            }
            Reset();Tick(.001f,4);Tick(.03f,1);
            Check(p.Action==FighterAction.Dodge,"Early attack waits for the roll cancel window");Advance(.08f);
            Check(p.Combo==TrialPlayer.RollThrust,"Early buffered attack cancels the roll at 100 ms");
            foreach(byte key in new byte[]{1,2})
            {
                Reset();Tick(.001f,4);Advance(.12f);float stamina=p.Stamina;
                typeof(TrialFighter).GetProperty("Stamina").SetValue(p,0f);Tick(.001f,key);
                Check(p.Action==FighterAction.Dodge,"Insufficient stamina preserves the roll");
                typeof(TrialFighter).GetProperty("Stamina").SetValue(p,stamina);
            }
            Reset();Tick(.001f,4);Advance(.12f);Tick(.001f,1);
            foreach(int move in new[]{TrialPlayer.CrossDraw,TrialPlayer.HiltStrike,TrialPlayer.HeelTurn,TrialPlayer.LowVault,TrialPlayer.VaultSweep,TrialPlayer.ScissorKick,TrialPlayer.CloudRake,TrialPlayer.FallingDraw,22})
            {Link(1,p.Combo==TrialPlayer.FallingDraw?.75f:p.Combo==TrialPlayer.ScissorKick || p.Combo==TrialPlayer.CloudRake?.70f:.59f);Check(p.Combo==move,"Rolling thrust light route reaches "+move);}
            Reset();Tick(.001f,4);Advance(.12f);Tick(.001f,2);Link(2,.70f);
            Check(p.Combo==TrialPlayer.AirStep,"Roll flash heavy branches into a low aerial step");
            Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{true,TrialPlayer.WheelFinish});Link(2,.70f);
            Check(p.Combo==TrialPlayer.RetreatDraw,"Wheel heavy branches into retreating draw");Link(1,.70f);
            Check(p.Combo==TrialPlayer.ThreadPierce,"Retreating draw light reconnects with a piercing advance");
            foreach(int move in new[]{TrialPlayer.CrossDraw,TrialPlayer.RollFlash,TrialPlayer.DoubleFang,TrialPlayer.WheelFinish,TrialPlayer.FlowingSweep,TrialPlayer.RisingBridge,TrialPlayer.MoonChase,TrialPlayer.RetreatDraw,TrialPlayer.ThreadPierce})
            {
                Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{move==TrialPlayer.RollFlash,move});
                Advance(p.ActionDuration*.72f);
                Check(Flights==(move==TrialPlayer.WheelFinish?2:move==TrialPlayer.RollFlash || move==TrialPlayer.MoonChase?1:0),"Ground move "+move+" respects its physical-versus-cast role");
                Input(.001f,0,1);int flights=Flights;Advance(.3f);
                Check(Flights==flights,"Defensive cancel never duplicates woven blades for "+move);
            }
            Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,TrialPlayer.DoubleFang});
            Advance(.06f);Input(.001f,0,1);Advance(.6f);Check(Flights==0,"Cancel before release prevents both woven blades");
            Reset();Tick(.001f,4);Advance(.12f);Tick(.001f,1);
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=p.transform.position+Vector3.forward*1.6f+Vector3.up;wall.transform.localScale=new Vector3(8,4,.3f);Physics.SyncTransforms();
            float start=p.transform.position.z;Advance(.25f);Check(p.transform.position.z<start+1.4f,"Rolling thrust is stopped by solid world geometry");UnityEngine.Object.DestroyImmediate(wall);wall=null;
            var shader=Resources.Load<Shader>("Trial/InkWash");
            Check(shader && shader.isSupported && !UnityEditor.ShaderUtil.ShaderHasError(shader),"Runtime ink shader is included and compiles for this renderer");
            Reset();game.Effects.Afterimage(p,false);game.Effects.Afterimage(p,true);
            var ghosts=(Array)typeof(TrialEffects).GetField("ghosts",Private).GetValue(game.Effects);
            bool normal=false;foreach(var ghost in ghosts)if(ghost!=null)normal|=(float)ghost.GetType().GetField("strength").GetValue(ghost)<.5f;
            Check(normal,"Ordinary roll uses the lighter ink silhouette");
            p.ShowPerfectDodge();
            Check((float)typeof(TrialPlayer).GetField("perfectGhostUntil",Private).GetValue(p)>Time.unscaledTime,"Perfect dodge reserves the stronger silhouette window");
            Reset();Tick(.001f,4);Advance(.12f);Tick(.001f,2);Link(2,.70f);Advance(.30f);
            Check(p.transform.position.y>1 && p.transform.position.y<1.5f,"Small leap holds the real controller at a low aerial height");
            Link(1,.70f);Check(p.Combo==TrialPlayer.AirCross,"Small leap light begins the airborne cross cut");
            Advance(p.ActionDuration*.72f);Check(Flights==0 && p.transform.position.y>1,"Air cross holds altitude as a melee cut without another flying blade");
            Link(1,.75f);Check(p.Combo==TrialPlayer.AirWheel,"Air cross links into a spinning aerial cut");
            Link(1,.75f);Check(p.Combo==TrialPlayer.AirFan,"Aerial spin links into the three-blade fan");
            Advance(p.ActionDuration*.72f);
            Check((byte)typeof(TrialPlayer).GetField("comboBladeEmissions",Private).GetValue(p)==3,"Aerial fan emits three bounded flying blades before link opens");
            Link(1,.81f);Check(p.Combo==TrialPlayer.AirSandDive,"Low aerial route finishes with a controlled diagonal dive");Advance(.45f);
            Check(p.transform.position.y<.2f,"Low aerial route returns the actual body to the floor");
            Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,TrialPlayer.AirStep});Advance(.32f);
            Input(.001f,0,1);bool guarded=p.Guarding;Advance(.55f);Check(guarded && p.transform.position.y<.2f,"Guard cancel releases low aerial suspension and gravity");
            Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,TrialPlayer.AirStep});Advance(.32f);
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=p.transform.position+Vector3.forward*1.4f;wall.transform.localScale=new Vector3(8,6,.3f);Physics.SyncTransforms();
            float rebound=p.transform.position.z;Link(2,.70f);Advance(.34f);
            Check(p.Combo==TrialPlayer.WallRebound && p.transform.position.z<rebound-2.8f && p.transform.position.y>1,"Nearby wall supplies a collision-safe airborne rebound");
            UnityEngine.Object.DestroyImmediate(wall);wall=null;Link(1,.70f);Check(p.Combo==TrialPlayer.AirFan,"Wall rebound can continue into the flying blade fan");
        }
        static void WeaveChecks()
        {
            Reset();Tick(.001f,1);Input(.12f,0,8);Input(.12f,0,8);Input(.04f,0,8);
            Check(p.Combo==TrialPlayer.GatheredPierce,"Holding light creates a gathered piercing follow-up without delaying the initial tap");
            for(int i=0;i<100;i++)Input(.016f,0,8);
            Check(p.Action==FighterAction.Free,"One continuous hold cannot loop attacks forever");
            Reset();Tick(.001f,1);Input(.16f,0,4);Input(.16f,0,4);
            Check(p.Combo==TrialPlayer.StormWheel,"Holding heavy inside a light chain selects the wind wheel");
            Reset();Tick(.001f,1);Advance(.1f);Input(.001f,0,1);Input(.001f,1);
            Check(p.Combo==TrialPlayer.GuardNeedle,"Inserted guard transforms the next light into a needle counter");
            Link(2);Check(p.Combo==TrialPlayer.GuardBloom,"Needle counter heavy connects to the blossom cut");
            Reset();Tick(.001f,1);Advance(.1f);Input(.001f,0,1);Input(.001f,1,1);
            for(int i=0;i<10;i++)Input(.016f,0,1);
            Check(p.Combo==TrialPlayer.GuardNeedle && p.Action==FighterAction.Light,"A held guard does not immediately cancel the counter it just started");
            Input(.001f,0);Input(.001f,0,1);
            Check(p.Guarding && p.Action==FighterAction.Free,"Releasing then pressing guard still cancels the counter defensively");
            Reset();Tick(.001f,1);Advance(.1f);Input(.001f,0,1);Input(.001f,2);
            Check(p.Combo==TrialPlayer.GuardBloom,"Inserted guard transforms the next heavy into a blossom fan");
            Advance(p.ActionDuration*.72f);Check((byte)typeof(TrialPlayer).GetField("comboBladeEmissions",Private).GetValue(p)==3,"Blossom guard branch launches three distinct fan blades");
            Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,TrialPlayer.AirStep});Advance(.32f);Input(.001f,0,1);Input(.001f,1);
            Check(p.Combo==TrialPlayer.AirPierce && p.transform.position.y>.8f,"Airborne inserted guard reconnects into a floating pierce");
            Advance(.12f);Check(p.transform.position.y>1,"Airborne guard follow-up keeps bounded low suspension");
            Reset();target.Teleport(new Vector3(3,0,23));p.ToggleLock();Tick(.001f,1);
            target.Teleport(new Vector3(-2,0,23));Advance(.13f);
            Check(p.Locked && Vector3.Angle(p.transform.forward,Vector3.ProjectOnPlane(target.transform.position-p.transform.position,Vector3.up))<15,"Locked combo corrects toward a boss moving across the front");
            Reset();target.Teleport(new Vector3(0,0,23));p.ToggleLock();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,TrialPlayer.RollThrust});Advance(.24f);
            Check(p.transform.position.z<target.transform.position.z-1,"Tracked pursuit brakes before passing through the boss");
            var controls=new TrialControls{held=8};input.ApplyNetwork(controls);
            Check(input.AttackHeld && (input.CaptureNetwork(0).held&8)!=0,"Light hold survives the existing network control message");
            var message=new TrialCombatEvent{cue=TrialCombatCue.WeaveSigil,style=4,player=9};
            using(var writer=NetworkWriterPool.Get()){writer.Write(message);using(var reader=NetworkReaderPool.Get(writer.ToArraySegment())){var copy=reader.Read<TrialCombatEvent>();Check(copy.style==4 && copy.cue==message.cue,"VFX style and weave sigil survive Mirror serialization");}}
        }
        static void MartialChecks()
        {
            foreach(float dt in new[]{1f/30,1f/60,1f/144})
            {
                Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,TrialPlayer.CrossDraw});
                foreach(int move in new[]{TrialPlayer.HiltStrike,TrialPlayer.HeelTurn,TrialPlayer.LowVault,TrialPlayer.VaultSweep,TrialPlayer.ScissorKick,TrialPlayer.CloudRake,TrialPlayer.FallingDraw,22})
                {
                    float link=p.Combo==TrialPlayer.FallingDraw?.76f:p.Combo==TrialPlayer.CloudRake || p.Combo==TrialPlayer.ScissorKick?.71f:.60f;
                    while(p.Progress<link)Tick(dt);Tick(dt,1);
                    Check(p.Combo==move,"Physical light chain reaches "+move+" at "+Mathf.RoundToInt(1/dt)+" FPS");
                }
                Check(p.transform.position.y<.25f && Flights==0,"Physical sequence returns to ground without blink or flying-blade repetition at "+Mathf.RoundToInt(1/dt)+" FPS");
            }
            Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,TrialPlayer.HiltStrike});
            Link(2,.70f);Check(p.Combo==TrialPlayer.ElbowBreak,"Hilt strike heavy changes body mechanics to an elbow break");
            Link(1,.70f);Check(p.Combo==TrialPlayer.OrbitCut,"Elbow light connects into an actual curved sidestep");
            Vector3 before=p.transform.position;float widest=0;
            for(int i=0;i<22;i++){Advance(.016f);widest=Mathf.Max(widest,Mathf.Abs(p.transform.position.x-before.x));}
            Check(widest>1.1f,"Orbit cut traces a visible side arc with CharacterController");
            Link(1,.70f);Check(p.Combo==TrialPlayer.SheathTurn,"Orbit light reverses direction into a sheathed draw");
            Link(2,.70f);Check(p.Combo==TrialPlayer.CloudRake,"Sheathed heavy can rise directly into cloud rake");
            foreach(int move in new[]{TrialPlayer.HiltStrike,TrialPlayer.HeelTurn,TrialPlayer.VaultSweep,TrialPlayer.OrbitCut,TrialPlayer.FallingDraw,TrialPlayer.ElbowBreak,TrialPlayer.ScissorKick,TrialPlayer.SheathTurn,TrialPlayer.CloudRake})
            {
                Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,move});target.Teleport(p.transform.position+Vector3.forward*1.65f);Physics.SyncTransforms();
                float hp=target.Health;Advance(p.ActionDuration*.42f);float first=target.Health;
                Check(first<hp,"New physical move "+move+" deals actual nearby damage");
                Advance(p.ActionDuration*.18f);float second=target.Health;
                Check(move==TrialPlayer.ScissorKick || move==TrialPlayer.CloudRake?second<first:second==first,"Physical move "+move+" preserves its one- or two-contact rhythm");
                Check(Flights==0,"Physical move "+move+" never falls back to a ranged blade");
                Input(.001f,0,1);bool guarded=p.Guarding;Advance(.5f);
                Check(guarded && p.transform.position.y<.2f,"Physical move "+move+" supports defensive cancellation and gravity");
            }
            foreach(int move in new[]{3,TrialPlayer.AirStep,TrialPlayer.LowVault})
            {
                Reset();typeof(TrialFighter).GetProperty("Stamina").SetValue(p,5f);
                bool began=(bool)typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{true,move});
                Check(began && p.Stamina==0,"Leap "+move+" starts with only five stamina even from a heavy branch");
            }
            Reset();Lights(4);Advance(.30f);target.Teleport(p.transform.position+Vector3.forward*2.6f);Link(1,.70f);
            Check(p.Combo==TrialPlayer.VaultSweep && Flights==0,"The default aerial chain uses a physical sweeping cut when the boss is nearby");
            Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,TrialPlayer.LowVault});Advance(.32f);
            Check(p.transform.position.y>.8f && p.transform.position.y<1.3f,"Cheap low vault moves the real body into a low hop");
            Input(.001f,0,1);Advance(.5f);Check(p.transform.position.y<.2f,"Low vault does not suspend the actor after guard cancellation");
            foreach(int entry in new[]{TrialPlayer.CrossDraw,TrialPlayer.HeelTurn,TrialPlayer.GatheredPierce})
            {
                Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,entry});Link(2,.70f);
                Check(p.Combo==TrialPlayer.LoftingDraw,"Ground entry "+entry+" can rise directly with a heavy input");
                Advance(.38f);Check(p.transform.position.y>1.45f,"Lofting draw raises the actual controller before the next attack");
                Link(2,.70f);Check(p.Combo==TrialPlayer.AerialCascade,"Lofting heavy connects directly into an aerial blade cascade");
                Advance(p.ActionDuration*.75f);
                Check((byte)typeof(TrialPlayer).GetField("comboBladeEmissions",Private).GetValue(p)==3 && p.transform.position.y>1.4f,"Aerial cascade releases exactly three blades while suspended");
                Link(1,.83f);Check(p.Combo==TrialPlayer.CloudRake,"Blade cascade can return to the physical aerial route");
            }
            Reset();typeof(TrialPlayer).GetMethod("StartAttack",Private).Invoke(p,new object[]{false,TrialPlayer.LowVault});Advance(.35f);Link(1,.70f);Link(2,.70f);
            Check(p.Combo==TrialPlayer.AerialCascade,"Vault sweep heavy offers another direct flying-blade opportunity");
            Advance(.12f);Input(.001f,0,1);Advance(.5f);Check(Flights==0 && p.transform.position.y<.2f,"Cascade canceled before release creates no blades and restores gravity");
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

            Reset();Lights(4);Advance(1.45f);yield return .60f;Advance(.6f);
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
            var flow=FlowChecks();while(flow.MoveNext())yield return flow.Current;
            ParryArtChecks();
            RollAndExtendedChecks();
            WeaveChecks();
            MartialChecks();
            Reset();
            foreach(var cue in new[]{TrialCombatCue.MoonBlade,TrialCombatCue.CrimsonBlade,TrialCombatCue.SwordLeap,TrialCombatCue.SwordTurn,TrialCombatCue.SwordDescent,TrialCombatCue.SwordLanding})
                game.Effects.PlayCombat(new TrialCombatEvent{cue=cue,position=p.transform.position+Vector3.up,forward=Vector3.forward,end=p.transform.position,amount=1},false);
            bool finite=true;
            foreach(var art in (Array)typeof(TrialEffects).GetField("swordArts",Private).GetValue(game.Effects))
            {
                if(art==null)continue;var type=art.GetType();var root=(Transform)type.GetField("root").GetValue(art);if(!root.gameObject.activeSelf)continue;
                foreach(var vertex in ((Mesh)type.GetField("mesh").GetValue(art)).vertices)
                    finite&=float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z);
            }
            Check(finite,"Wave and motion ribbon geometry stays finite at all sampled endpoints");
            foreach(var cue in new[]{TrialCombatCue.CrimsonBlade,TrialCombatCue.MoonBlade,TrialCombatCue.BlueSand,TrialCombatCue.SakuraScatter,TrialCombatCue.SandImpact,TrialCombatCue.SwordLeap,TrialCombatCue.SwordTurn,TrialCombatCue.SwordDescent,TrialCombatCue.SwordLanding})
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
