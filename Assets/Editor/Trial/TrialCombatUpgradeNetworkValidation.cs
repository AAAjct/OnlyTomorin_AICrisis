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
        [MenuItem("Tools/Tomorin/Verify combat upgrade over TCP")]
        public static void RunCombatUpgradeNetwork()
        {
            if(!EditorApplication.isPlaying || routine!=null || !UnityEngine.Object.FindFirstObjectByType<InkMainMenu>() || TrialNetworkManager.SessionActive)
                throw new InvalidOperationException("Start InkStart in Play Mode outside a network session.");
            results.Clear();events.Clear();runtimeErrors=0;reportName="CombatUpgradeNetworkValidation.txt";Status="Running combat upgrade TCP";
            Application.logMessageReceived+=CaptureError;routine=CombatUpgradeNetworkChecks();resumeAt=heartbeat=0;EditorApplication.update+=Step;
        }
        static IEnumerator CombatUpgradeNetworkChecks()
        {
            var network=TrialNetworkManager.Current;var transport=(TelepathyTransport)network.transport;
            ushort previousPort=transport.port;string previousName=network.PlayerName;var previousLevel=network.SelectedLevel;
            try
            {
                transport.port=18260;network.SelectLevel(TrialLevel.BigFish);network.PlayerName="UpgradeHost";network.Host();
                incoming=new Unbatcher();lobby=sceneReceived=false;held=default;
                client=new Telepathy.Client(16384){NoDelay=true};
                client.OnConnected=()=>{Send(new ReadyMessage());Send(new TrialHello{name="UpgradePeer"});};
                client.OnData=Receive;client.Connect("127.0.0.1",transport.port);
                Until();while(Waiting(lobby && network.Members.Length==2))yield return .02f;
                network.BeginTogether();Until();while(Waiting(sceneReceived && UnityEngine.Object.FindFirstObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                Send(new ReadyMessage());Until();while(Waiting(network.Players.Count()==2 && world.players!=null))yield return .02f;
                var game=UnityEngine.Object.FindFirstObjectByType<TrialGame>();var remote=network.Players.First(p=>p!=game.Player);
                game.Player.ChooseBoon(game.Player.OfferA);
                float seconds=game.PlaySeconds;var position=remote.transform.position;
                var enemy=game.Enemies[0];enemy.Teleport(game.Player.transform.position+Vector3.forward*2);
                float hp=game.Player.Health;
                held=new TrialControls{move=Vector2.up,held=2};Send(held);yield return .45f;
                Check(network.WaitingForBoons && world.waitingForBoons,"Host and peer snapshot both expose the pending party barrier");
                Check(game.PlaySeconds==seconds && game.Player.Health==hp && Vector3.Distance(position,remote.transform.position)<.01f,"No combat, timer or player movement advances while one peer is choosing");
                Send(new TrialChooseBoon{boon=remote.OfferA});Until();while(Waiting(!network.WaitingForBoons && game.PlaySeconds>seconds+.05f))yield return .02f;
                Check(!remote.ChoosingBoon,"Final peer selection releases the shared barrier");
                foreach(var foe in game.Enemies)foe.Teleport(new Vector3(12,0,36));
                remote.Restore(game.Checkpoint+Vector3.right*3);
                held=new TrialControls{held=4};Send(new TrialControls{held=4,pressed=2});
                Until();while(Waiting(remote.Action==FighterAction.Charge))yield return .01f;
                yield return .75f;held=default;Send(held);
                Until();while(Waiting(remote.Action==FighterAction.Heavy && remote.Combo==17))yield return .01f;
                Check(events.Any(e=>e.cue==TrialCombatCue.ChargeReady && e.player==remoteId),"TCP held/release produces charged cleave and owner-specific charge cue");
                foreach(var guard in game.Enemies)if(!guard.IsBoss)guard.ReceiveHit(game.Player,99999,100,true);
                var boss=game.Enemies[game.Enemies.Count-1];boss.ReceiveHit(game.Player,99999,100,true);
                Until();while(Waiting(game.Mode==TrialMode.Victory))yield return .02f;
                Send(new TrialRetryChallenge());yield return .2f;
                Check(game.Mode==TrialMode.Victory,"Server rejects a real remote retry request");
                network.RetryChallenge();yield return .15f;
                Check(game.Mode==TrialMode.Playing && network.Members.Length==2 && network.WaitingForBoons,"Host retry keeps both connections and starts a fresh shared boon barrier");
                game.Player.ChooseBoon(game.Player.OfferA);Send(new TrialChooseBoon{boon=remote.OfferA});
                Until();while(Waiting(!network.WaitingForBoons))yield return .02f;
                foreach(var guard in game.Enemies)if(!guard.IsBoss)guard.ReceiveHit(game.Player,99999,100,true);
                boss.ReceiveHit(game.Player,99999,100,true);yield return .15f;
                sceneReceived=false;Send(new TrialReturnToRoom());
                Until();while(Waiting(sceneReceived && UnityEngine.Object.FindFirstObjectByType<InkMainMenu>()!=null))yield return .02f;
                Send(new ReadyMessage());yield return .2f;
                Check(client.Connected && NetworkClient.isConnected && network.Members.Length==2,"Victory return loads the existing room without disconnecting either member");
                var menu=UnityEngine.Object.FindFirstObjectByType<InkMainMenu>();
                var panel=(RectTransform)typeof(InkMainMenu).GetField("network",Private).GetValue(menu);
                Check(panel.gameObject.activeInHierarchy,"Returning title scene opens the existing room directly");
                sceneReceived=false;network.BeginTogether();
                Until();while(Waiting(sceneReceived && UnityEngine.Object.FindFirstObjectByType<TrialGame>()?.Ready==true))yield return .02f;
                Send(new ReadyMessage());Until();while(Waiting(network.Players.Count()==2))yield return .02f;
                Check(client.Connected && network.WaitingForBoons,"The same peer enters another challenge without rejoining");
                Check(runtimeErrors==0,"Shared barrier, permissions and repeated room transitions produce no runtime errors");
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
