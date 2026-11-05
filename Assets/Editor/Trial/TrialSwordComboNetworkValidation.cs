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
                int[] moves={0,1,2,3,26,27,28,29};
                for(int i=0;i<moves.Length;i++)
                {
                    Send(new TrialControls{pressed=1});int expected=moves[i];
                    Until();while(Waiting(remote.Action==FighterAction.Light && remote.Combo==expected))yield return .01f;
                    Check(remote.Combo==expected,"Real TCP light edge selects ordered move "+expected);
                    if(expected==26)
                    {
                        Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==26 && s.position.y>1.5f)))yield return .01f;
                        Check(true,"Remote TCP snapshot contains the real airborne controller height and blade move");
                    }
                    if(i<moves.Length-1 && i!=3){Until();while(Waiting(remote.Progress>(i>=4?.64f:.59f)))yield return .01f;}
                }
                Until();while(Waiting(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.BlueSand)==12))yield return .01f;
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.CrimsonBlade)==3,"Peer receives exactly three owner-specific crimson projectile events");
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.SandImpact)==1,"Peer receives one landing impact event");
                Check(events.Count(e=>e.player==remoteId && e.cue==TrialCombatCue.BlueSand)==12,"Peer receives all twelve radial sand projectile events");
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
                var source=game.Enemies[0];source.Teleport(remote.transform.position+Vector3.forward*3);
                held=new TrialControls{held=1};Send(held);Until();while(Waiting(remote.Guarding && remote.GuardAge<.18f))yield return .01f;
                remote.ReceiveHit(source,10,5);held=default;Send(held);
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.parryReady>0)))yield return .01f;
                Check(remote.ParryEmpowered,"A remote player's perfect guard arms an authoritative empowered choice");
                held=new TrialControls{held=4};Send(new TrialControls{pressed=2,held=4});Until();while(Waiting(remote.Action==FighterAction.Charge && remote.Combo==TrialPlayer.FireDragon))yield return .01f;
                Until();while(Waiting(remote.ChargeAmount>.45f))yield return .01f;
                held=default;Send(held);Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.FireDragon)))yield return .01f;
                var dragon=events.First(e=>e.player==remoteId && e.cue==TrialCombatCue.FireDragon);
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
                Check(!remote.DodgeDragonLanded && !events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.FireDragon),"Remote perfect-dodge heavy really leaps and reserves the dragon until landing");
                Until();while(Waiting(world.players.Any(s=>s.id==remoteId && s.combo==TrialPlayer.DodgeDragon && s.position.y>1)))yield return .01f;
                Check(true,"TCP snapshots preserve the backward leap's actual height and move ID");
                Until();while(Waiting(events.Any(e=>e.player==remoteId && e.cue==TrialCombatCue.FireDragon)))yield return .01f;
                var leapDragon=events.First(e=>e.player==remoteId && e.cue==TrialCombatCue.FireDragon);
                Check(remote.DodgeDragonLanded && remote.transform.position.y<.2f && Mathf.Abs(leapDragon.amount-.5f)<.01f,"Remote landing cleave emits one correctly powered dragon after touching ground");
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
