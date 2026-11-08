using System;
using System.Collections;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEngine;

namespace TomorinTrial.Editor
{
    public static partial class TrialNetworkValidation
    {
        [MenuItem("Tools/Tomorin/Verify ordered sword combo over TCP")]
        public static void RunSwordComboNetwork()
        {
            if(!EditorApplication.isPlaying || routine!=null || !UnityEngine.Object.FindFirstObjectByType<InkMainMenu>() || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Start InkStart in Play Mode outside a network session.");
            results.Clear();events.Clear();runtimeErrors=0;reportName="SwordComboNetworkValidation.txt";Status="Running sword combo TCP";
            Application.logMessageReceived+=CaptureError;routine=SwordComboNetworkChecks();resumeAt=heartbeat=0;EditorApplication.update+=Step;
        }
        static IEnumerator SwordComboNetworkChecks()
        {
            var network=TrialNetworkManager.Current;var transport=(TelepathyTransport)network.transport;
            ushort previousPort=transport.port;string previousName=network.PlayerName;var previousLevel=network.SelectedLevel;
            try
            {
                transport.port=18262;network.SelectLevel(TrialLevel.BigFish);network.PlayerName="SwordHost";network.Host();
                incoming=new Unbatcher();lobby=sceneReceived=false;held=default;
                client=new Telepathy.Client(16384){NoDelay=true};
                client.OnConnected=()=>{Send(new ReadyMessage());Send(new TrialHello{name="SwordPeer"});};
                client.OnData=Receive;client.Connect("127.0.0.1",transport.port);
                Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                network.BeginTogether();Until();while(Waiting(sceneReceived && UnityEngine.Object.FindFirstObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                Send(new ReadyMessage());Until();while(Waiting(network.Players.Count()==2 && world.players!=null))yield return .02f;
                var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();var remote=network.Players.First(p=>p!=game.Player);
                game.Player.ChooseBoon(game.Player.OfferA);Send(new TrialChooseBoon{boon=remote.OfferA});
                Until();while(Waiting(!network.WaitingForBoons))yield return .02f;
                foreach(var enemy in game.Enemies)enemy.Teleport(new Vector3(12,0,36));
                remote.Restore(new Vector3(0,0,20));game.Player.Restore(new Vector3(-4,0,20));
                int[] moves={0,1,2,TrialPlayer.WheelFinish};
                foreach(int expected in moves)
                {
                    Send(new TrialControls{pressed=1});
                    Until();while(Waiting(remote.Action==FighterAction.Light && remote.Combo==expected && remote.Progress>.59f))yield return .01f;
                    Check(remote.transform.position.y<.2f,"Real TCP light route stays grounded at move "+expected);
                }
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                remote.Restore(new Vector3(0,0,20));events.Clear();
                Send(new TrialControls{pressed=64});
                Until();while(Waiting(remote.Combo==TrialPlayer.AirStep && remote.Progress>.59f))yield return .01f;
                foreach(int expected in new[]{TrialPlayer.AirCross,TrialPlayer.AirWheel,TrialPlayer.AirFan,TrialPlayer.AirSandDive})
                {
                    Send(new TrialControls{pressed=1});
                    Until();while(Waiting(remote.Action==FighterAction.Light && remote.Combo==expected))yield return .01f;
                    if(expected!=TrialPlayer.AirSandDive){Until();while(Waiting(remote.Progress>.82f))yield return .01f;}
                }
                Until();while(Waiting(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.BlueSand)==12))yield return .01f;
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.CrimsonBlade)==3,"TCP explicit air route emits exactly three fan blades");
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.SandImpact)==1,"TCP explicit air route emits one landing impact");
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                remote.Restore(new Vector3(0,0,20));events.Clear();
                for(int i=0;i<3;i++)
                {
                    Send(new TrialControls{pressed=1});int expected=i;
                    Until();while(Waiting(remote.Action==FighterAction.Light && remote.Combo==expected && remote.Progress>.59f))yield return .01f;
                }
                Send(new TrialControls{pressed=2});Until();while(Waiting(remote.Action==FighterAction.Heavy && remote.Combo==6))yield return .01f;
                float retreatOrigin=remote.transform.position.z;
                Until();while(Waiting(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.MoonBlade)==3))yield return .01f;
                Check(remote.transform.position.z<retreatOrigin-.7f,"Remote heavy branch physically retreats on the host");
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.MoonBlade)==3,"Three moon crescent events cross real TCP framing");
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                remote.Restore(new Vector3(0,0,20));events.Clear();
                for(int i=0;i<2;i++)
                {
                    Send(new TrialControls{pressed=1});int expected=i;
                    Until();while(Waiting(remote.Action==FighterAction.Light && remote.Combo==expected && remote.Progress>.59f))yield return .01f;
                }
                Send(new TrialControls{pressed=2});Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.SakuraScatter)))yield return .01f;
                var sakura=events.First(e=>e.player==remoteId && e.cue==TrialCombatCue.SakuraScatter);
                Check(Vector3.Distance(sakura.position,sakura.end)>5.5f,"TCP sakura event carries both actual endpoints of the six-metre dash");
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                remote.Restore(new Vector3(0,0,20));events.Clear();
                SendMany(new TrialControls{pressed=1},new TrialControls{pressed=1},new TrialControls{pressed=2});
                Until();while(Waiting(remote.Action==FighterAction.Heavy && remote.Combo==5))yield return .01f;
                Check(true,"Batched real TCP L,L,H keeps repeated light edges and selects sakura thrust");
                Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.SakuraScatter)))yield return .01f;
                held=new TrialControls{held=1};Send(held);
                Until();while(Waiting(remote.Guarding && remote.Action==FighterAction.Free))yield return .01f;
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.weaveGrace>0 && s.weaveFrom==5)))yield return .01f;
                held=default;Send(held);Until();while(Waiting(remote.Combo==TrialPlayer.MoonChase))yield return .01f;
                Check(true,"TCP guard alone after sakura directly selects the pursuit thrust");
                Send(new TrialControls{pressed=2});Until();while(Waiting(remote.Combo==TrialPlayer.ParryAscend))yield return .01f;
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==TrialPlayer.ParryAscend)))yield return .01f;
                Check(true,"Guard pursuit's ascending extension is authoritative and replicated");
                foreach(bool heavyHold in new[]{false,true})
                {
                    Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                    remote.Restore(new Vector3(0,0,20));events.Clear();
                    held=new TrialControls{held=heavyHold?(byte)4:(byte)8};
                    Send(new TrialControls{pressed=heavyHold?(byte)2:(byte)1,held=held.held});
                    foreach(int expected in heavyHold?new[]{0,18,19}:new[]{0,1,2,TrialPlayer.WheelFinish})
                    {
                        Until();while(Waiting(remote.Combo==expected && remote.Action==(heavyHold?FighterAction.Heavy:FighterAction.Light)))yield return .01f;
                        Check(true,"One TCP held "+heavyHold+" advances to move "+expected);
                    }
                    held=default;Send(held);Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                    Check(true,"TCP hold release stops the route after its current move");
                }
                remote.Restore(new Vector3(0,0,20));events.Clear();
                held=new TrialControls{held=1};
                SendMany(new TrialControls{pressed=1},new TrialControls{pressed=1},held);
                Until();while(Waiting(remote.Combo==TrialPlayer.SpiralSword && remote.Action==FighterAction.Heavy))yield return .01f;
                Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.SpiralSword)))yield return .01f;
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.SpiralSword)==1,"Batched TCP L,L,G throws exactly one rotating sword without another attack key");
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==TrialPlayer.SpiralSword && s.orbitSwordTime>0)))yield return .01f;
                Check(true,"TCP snapshot carries the live flying sword and its recall window");
                held=default;Send(new TrialControls{pressed=1});
                Until();while(Waiting(remote.Combo==TrialPlayer.SwordRecallCut))yield return .01f;
                Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.SwordRecall)))yield return .01f;
                var recall=events.First(e=>e.player==remoteId && e.cue==TrialCombatCue.SwordRecall);
                Check(Vector3.Distance(recall.position,recall.end)>1 && Vector3.Distance(remote.transform.position+Vector3.up,recall.end)<.3f,"TCP light teleports the authoritative actor to the blade and sends the actual endpoints");
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==TrialPlayer.SwordRecallCut && s.orbitSwordTime==0)))yield return .01f;
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.SwordRecall)==1,"TCP recall stops the sword flight once and replicates the reclaim cut");
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                remote.Restore(new Vector3(0,0,20));events.Clear();held=new TrialControls{held=1};
                SendMany(new TrialControls{pressed=1},new TrialControls{pressed=1},held);
                Until();while(Waiting(remote.SwordRecallReady))yield return .01f;
                Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.SpiralSword)))yield return .01f;
                var cast=events.First(e=>e.player==remoteId && e.cue==TrialCombatCue.SpiralSword);
                Check(Vector3.Dot((cast.end-cast.position).normalized,cast.forward)>.999f && Mathf.Abs(Vector3.Distance(cast.position,cast.end)-12)<.02f,"TCP sword cast keeps the actor's forward direction and fixed range");
                held=default;Send(new TrialControls{pressed=2});
                Until();while(Waiting(remote.Combo==TrialPlayer.SwordRecallRise && remote.transform.position.y>1.9f))yield return .01f;
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==TrialPlayer.SwordRecallRise && s.position.y>1.9f && s.orbitSwordTime==0)))yield return .01f;
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.SwordRecall)==1,"TCP heavy recalls the flying sword once and replicates the real rising slash");
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                foreach(byte key in new byte[]{1,2})
                {
                    Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                    remote.Restore(new Vector3(0,0,20));events.Clear();
                    Send(new TrialControls{pressed=4});Until();while(Waiting(remote.Action==FighterAction.Dodge && remote.ActionTime>=.10f))yield return .01f;
                    Send(new TrialControls{pressed=key});int move=key==1?TrialPlayer.RollThrust:TrialPlayer.RollFlash;
                    Until();while(Waiting(remote.Combo==move && remote.Action!=FighterAction.Dodge))yield return .01f;
                    Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==move)))yield return .01f;
                    Check(true,"TCP roll cancellation replicates move "+move);
                    if(key==2)
                    {
                        Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.RollFlash) && events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.MoonBlade)))yield return .01f;
                        Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.RollFlash)==1,"TCP flash slash and its woven moon blade have the correct owner and fire once");
                        Send(new TrialControls{pressed=64});Until();while(Waiting(remote.Combo==TrialPlayer.AirStep && remote.transform.position.y>.9f))yield return .01f;
                        Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==TrialPlayer.AirStep && s.position.y>.9f)))yield return .01f;
                        Check(true,"TCP low aerial step carries its actual controller height");
                        foreach(int airMove in new[]{TrialPlayer.AirCross,TrialPlayer.AirWheel,TrialPlayer.AirFan})
                        {
                            Until();while(Waiting(remote.Progress>.73f))yield return .01f;
                            Send(new TrialControls{pressed=1});Until();while(Waiting(remote.Combo==airMove))yield return .01f;
                        }
                        Until();while(Waiting(remote.Progress>.72f))yield return .01f;
                        Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.CrimsonBlade)==3,"TCP airborne route reserves flying blades for the dedicated fan");
                    }
                    else
                    {
                        Send(new TrialControls{pressed=1});Until();while(Waiting(remote.Combo==TrialPlayer.CrossDraw))yield return .01f;
                        Send(new TrialControls{pressed=1});Until();while(Waiting(remote.Combo==TrialPlayer.HiltStrike))yield return .01f;
                        Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.MartialStroke)))yield return .01f;
                        Check(!events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.CrimsonBlade),"TCP rolling thrust reconnects into a physical hilt strike without a flying blade");
                    }
                }
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                remote.Restore(new Vector3(0,0,20));events.Clear();
                var source=game.Enemies[0];source.Teleport(remote.transform.position+Vector3.forward*3);
                held=new TrialControls{held=1};Send(held);Until();while(Waiting(remote.Guarding && remote.GuardAge<.18f))yield return .01f;
                remote.ReceiveHit(source,10,5);held=default;Send(held);
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.parryReady>0)))yield return .01f;
                Check(remote.ParryEmpowered,"A remote player's perfect guard arms an authoritative empowered choice");
                held=new TrialControls{held=4};Send(new TrialControls{pressed=2,held=4});Until();while(Waiting(remote.Action==FighterAction.Charge && remote.Combo==TrialPlayer.ThunderDragon))yield return .01f;
                Until();while(Waiting(remote.ChargeAmount>.45f))yield return .01f;
                held=default;Send(held);Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.ThunderDragon)))yield return .01f;
                var dragon=events.First(e=>e.player==remoteId && e.cue==TrialCombatCue.ThunderDragon);
                Check(dragon.amount>=.4f && dragon.amount<=1 && Vector3.Distance(dragon.position,dragon.end)>14,"TCP charged dragon event retains owner, power, and scaled range");
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                remote.Restore(new Vector3(0,0,20));events.Clear();source.Teleport(remote.transform.position+Vector3.forward*3);
                held=new TrialControls{held=1};Send(held);Until();while(Waiting(remote.Guarding && remote.GuardAge<.18f))yield return .01f;
                remote.ReceiveHit(source,10,5);held=default;Send(new TrialControls{pressed=4});
                Until();while(Waiting(remote.Action==FighterAction.Dodge && remote.Combo==TrialPlayer.ParryDodge && remote.Progress>.5f))yield return .01f;
                Send(new TrialControls{pressed=2});Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.SpiralSword)))yield return .01f;
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.SpiralSword)==1,"Remote empowered dodge links into exactly one spiral sword over TCP");
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                remote.Restore(new Vector3(0,0,20));events.Clear();source.Teleport(remote.transform.position+Vector3.forward*3);
                Send(new TrialControls{pressed=4});Until();while(Waiting(remote.Action==FighterAction.Dodge && remote.ActionTime>.07f))yield return .01f;
                remote.ReceiveHit(source,10,5);
                Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.PerfectDodge) && remote.Progress>.72f))yield return .01f;
                Send(new TrialControls{pressed=2,held=4});Until();while(Waiting(remote.Action==FighterAction.Heavy && remote.Combo==TrialPlayer.DodgeDragon && remote.transform.position.y>1))yield return .01f;
                Check(!remote.DodgeDragonLanded && !events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.ThunderDragon),"Remote perfect-dodge heavy really leaps and reserves the dragon until landing");
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==TrialPlayer.DodgeDragon && s.position.y>1)))yield return .01f;
                Check(true,"TCP snapshots preserve the backward leap's actual height and move ID");
                Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.ThunderDragon)))yield return .01f;
                var leapDragon=events.First(e=>e.player==remoteId && e.cue==TrialCombatCue.ThunderDragon);
                Check(remote.DodgeDragonLanded && remote.transform.position.y<.2f && Mathf.Abs(leapDragon.amount-.5f)<.01f,"Remote landing cleave emits one correctly powered dragon after touching ground");
                Until();while(Waiting(remote.Action==FighterAction.Free))yield return .01f;
                remote.Restore(new Vector3(0,0,20));events.Clear();source.Teleport(remote.transform.position+Vector3.forward*3);
                held=new TrialControls{held=1};Send(held);Until();while(Waiting(remote.Guarding && remote.GuardAge<.18f))yield return .01f;
                remote.ReceiveHit(source,10,5);held=default;Send(new TrialControls{pressed=1});
                Until();while(Waiting(remote.Combo==TrialPlayer.ParryTriple && remote.Progress>.72f))yield return .01f;
                Send(new TrialControls{pressed=1});Until();while(Waiting(remote.Combo==TrialPlayer.ParryMirror && remote.Progress>.78f))yield return .01f;
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==TrialPlayer.ParryMirror)))yield return .01f;
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.MartialStroke && Mathf.RoundToInt(e.amount)==TrialPlayer.ParryMirror)==2,"TCP perfect mirror crossing emits exactly two owner-specific strokes");
                Send(new TrialControls{pressed=1});Until();while(Waiting(remote.Combo==TrialPlayer.ParryAscend && remote.transform.position.y>2))yield return .01f;
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==TrialPlayer.ParryAscend && s.position.y>2)))yield return .01f;
                Check(true,"TCP perfect ascending cut replicates the real controller height and new move ID");
                Check(runtimeErrors==0,"Ordered combo and sword events produce no network runtime errors");
            }
            finally
            {
                held=default;client?.Disconnect();client=null;
                if(TrialNetworkManager.Current)
                {
                    if(TrialNetworkManager.SessionActive)TrialNetworkManager.Current.Leave();
                    ((TelepathyTransport)TrialNetworkManager.Current.transport).port=previousPort;
                    TrialNetworkManager.Current.PlayerName=previousName;TrialNetworkManager.Current.SelectLevel(previousLevel);
                }
            }
        }
    }
}
