using System;
using System.Collections;
using System.Linq;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TomorinTrial.Editor
{
    public static partial class TrialNetworkValidation
    {
        [MenuItem("Tools/Tomorin/Verify responsive combat over real TCP")]
        public static void RunResponsiveNetwork()
        {
            if(!EditorApplication.isPlaying || routine!=null || !UnityEngine.Object.FindAnyObjectByType<InkMainMenu>() || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Start InkStart in Play Mode outside a network session.");
            results.Clear();events.Clear();runtimeErrors=0;reportName="ResponsiveNetworkValidation.txt";Status="Running responsive TCP combat";
            Application.logMessageReceived+=CaptureError;routine=ResponsiveNetworkChecks();resumeAt=heartbeat=0;EditorApplication.update+=Step;
        }
        static IEnumerator ResponsiveNetworkChecks()
        {
            var network=TrialNetworkManager.Current;var transport=(TelepathyTransport)network.transport;
            ushort previousPort=transport.port;string previousName=network.PlayerName;var previousLevel=network.SelectedLevel;
            try
            {
                transport.port=18110;network.SelectLevel(TrialLevel.BigFish);network.PlayerName="ResponsiveHost";network.Host();
                incoming=new Unbatcher();lobby=sceneReceived=false;held=default;
                client=new Telepathy.Client(16384){NoDelay=true};
                client.OnConnected=()=>{Send(new ReadyMessage());Send(new TrialHello{name="ResponsivePeer"});};
                client.OnData=Receive;client.Connect("127.0.0.1",transport.port);
                Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                Check(remoteId!=network.LocalId,"Independent TCP peer joins the lobby with its own ID");
                network.BeginTogether();Until();while(Waiting(sceneReceived && UnityEngine.Object.FindAnyObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                Send(new ReadyMessage());Until();while(Waiting(network.Players.Count()==2 && world.players!=null))yield return .02f;
                var game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();var remote=network.Players.First(p=>p!=game.Player);
                game.Player.ChooseBoon(game.Player.OfferA);Send(new TrialChooseBoon{boon=remote.OfferA});
                Until();while(Waiting(!remote.ChoosingBoon))yield return .02f;
                foreach(var enemy in game.Enemies)enemy.Teleport(new Vector3(12,0,36));
                remote.ResetRunBoon();remote.Restore(game.Checkpoint+Vector3.right*3);
                held=new TrialControls{held=1};Send(held);Until();while(Waiting(remote.Guarding))yield return .01f;
                yield return .26f;
                float hp=remote.Health,sp=remote.Stamina;remote.ReceiveHit(null,30,20);
                Until();while(Waiting(events.Any(e=>e.cue==TrialCombatCue.Block && e.player==remoteId)))yield return .01f;
                Check(Mathf.Abs(hp-remote.Health-1.2f)<.1f && remote.Stamina>=sp-13,"Host validates reduced guard chip/stamina cost and sends the owning peer a block event");
                held=default;Send(held);Until();while(Waiting(!remote.Guarding))yield return .01f;
                remote.Restore(game.Checkpoint+Vector3.right*3);var foe=game.Enemies[0];foe.Teleport(remote.transform.position+Vector3.forward*2);
                game.Hud.Notify("HOST_UNCHANGED",10);ResetShake(game);float localHp=game.Player.Health;
                remote.ReceiveHit(foe,25,10,true);
                Until();while(Waiting(events.Any(e=>e.cue==TrialCombatCue.Hurt && e.player==remoteId)))yield return .01f;
                var hurt=events.Last(e=>e.cue==TrialCombatCue.Hurt);
                Check(Vector3.Dot(hurt.forward,Vector3.forward)>.99f,"Reliable hurt event carries the authoritative source direction and victim ID");
                Check(Notice(game)=="HOST_UNCHANGED" && Shake(game)==0 && game.Player.Health==localHp,"Remote damage leaves the host's HUD, camera and health untouched");
                yield return .1f;foe.Teleport(new Vector3(12,0,36));remote.Restore(game.Checkpoint+Vector3.right*3);
                Send(new TrialControls{pressed=1});Until();while(Waiting(remote.Action==FighterAction.Light))yield return .01f;
                double attackAt=EditorApplication.timeSinceStartup;Send(new TrialControls{pressed=2});
                Until();while(Waiting(remote.Action==FighterAction.Heavy))yield return .01f;
                Check(EditorApplication.timeSinceStartup-attackAt<.55,"Buffered light-to-heavy chaining works through real TCP control packets");
                game.Effects.SkillImpact(new Vector3(0,0,25),new Color(.12f,.7f,1));
                Until();while(Waiting(events.Any(e=>e.cue==TrialCombatCue.SkillImpact)))yield return .01f;
                var impact=events.Last(e=>e.cue==TrialCombatCue.SkillImpact);
                Check(impact.player==-1 && impact.forward.z==1,"World skill impact is broadcast once without assigning local-player feedback");
                world.players=new[]{game.Player.CaptureNetwork(0),remote.CaptureNetwork(1)};
                held=default;client.Disconnect();client=null;network.Leave();
                Until();while(Waiting(UnityEngine.Object.FindAnyObjectByType<InkMainMenu>()!=null))yield return .03f;yield return .2f;

                incoming=new Unbatcher();peerReady=false;server=new Telepathy.Server(16384){NoDelay=true};
                server.OnConnected=(id,address)=>serverPeer=id;server.OnData=(id,data)=>Receive(data);
                Check(server.Start(18111),"Independent TCP authority starts for the real receiving-client test");
                network=TrialNetworkManager.Current;((TelepathyTransport)network.transport).port=18111;network.networkAddress="127.0.0.1";network.Join();
                Until();while(Waiting(UnityEngine.Object.FindAnyObjectByType<TrialGame>()?.Ready==true && peerReady))yield return .03f;
                game=UnityEngine.Object.FindAnyObjectByType<TrialGame>();Send(world);yield return .15f;
                Check(network.LocalId==1 && !NetworkServer.active,"The Unity game is a real receiving client with assigned ID 1");
                var bearing=(Image)typeof(TrialHud).GetField("damageBearing",Private).GetValue(game.Hud);
                var veil=(Image)typeof(TrialHud).GetField("damageVeil",Private).GetValue(game.Hud);
                game.Hud.Notify("CLIENT_UNCHANGED",10);ResetShake(game);
                Send(new TrialCombatEvent{player=0,cue=TrialCombatCue.Hurt,position=game.Player.transform.position,forward=Vector3.left});yield return .08f;
                Check(bearing.color.a==0 && Shake(game)==0 && Notice(game)=="CLIENT_UNCHANGED","An observer's hurt event does not activate the receiving client's damage UI");
                Send(new TrialCombatEvent{player=1,cue=TrialCombatCue.Hurt,position=game.Player.transform.position,forward=Vector3.left});
                for(int i=0;i<world.players.Length;i++)if(world.players[i].id==1){var actor=world.players[i];actor.health-=30;world.players[i]=actor;}
                Send(world);yield return .1f;
                Check(bearing.color.a>0 && bearing.rectTransform.anchoredPosition.magnitude>100 && veil.color.a>.1f,"Owning receiving client shows directional hurt and screen-edge health-loss feedback");
                Send(impact);yield return .08f;
                Check(game.Effects.GetComponentsInChildren<MeshRenderer>().Any(r=>r.name=="Breaking water crown" && r.gameObject.activeInHierarchy),"Receiving client renders the host's shared pooled skill detonation");
                Check(runtimeErrors==0,"Host, independent peer, receiving client and transitions produce no runtime errors");
                network.Leave();server.Stop();server=null;yield return .2f;
            }
            finally
            {
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
